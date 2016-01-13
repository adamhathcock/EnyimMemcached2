using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Tracing;

namespace Enyim.Caching
{
	public abstract class NodeBase : INode
	{
		private const int MODE_SEND = 0;
		private const int MODE_RECEIVE = 1;

		private const int ALIVE = 1;
		private const int DEAD = 0;

		private static readonly ILog log = LogManager.GetLogger<NodeBase>();
        private static readonly bool LogTraceEnabled = log.IsTraceEnabled;
		private static readonly bool LogDebugEnabled = log.IsDebugEnabled;
		private static readonly bool LogInfoEnabled = log.IsInfoEnabled;
		private readonly CoreEventSource trace = EventSources.CoreEventSource;

		private readonly ICluster owner;
		private readonly IPEndPoint endpoint;
		private readonly IFailurePolicy failurePolicy;
		private readonly string name; // used for tracing

		private readonly ISocket socket;

		private int runMode; // state machine flag, if it should sending (MODE_SEND) or receiving (MODE_RECEIVE) data
		private int isAlive; // if the socket is alive (ALIVE) or not (DEAD)
		private int workLock; // if it is "running", to prevent re-entrancy when a node is queued for IO multiple times
		private object failLock;

		private readonly ConcurrentQueue<Data> writeQueue;
		private readonly Queue<Data> readQueue;

		private Data currentWriteOp;
		private IRequest currentWriteCopier;
		private IResponse inprogressResponse;

		private bool mustReconnect;

		private readonly ICounter counterEnqueuePerSec;
		private readonly ICounter counterDequeuePerSec;
		private readonly ICounter counterOpReadPerSec;
		private readonly ICounter counterWritePerSec;
		private readonly ICounter counterErrorPerSec;
		private readonly ICounter counterItemCount;
		private readonly ICounter counterWriteQueue;
		private readonly ICounter counterReadQueue;
		private readonly IGauge gaugeSendSpeed;

		protected NodeBase(ICluster owner, IPEndPoint endpoint, IFailurePolicy failurePolicy, ISocket socket)
		{
			this.owner = owner;
			this.endpoint = endpoint;
			this.socket = socket;
			this.failurePolicy = failurePolicy;
			this.name = endpoint.ToString();

			failLock = new Object();
			writeQueue = new ConcurrentQueue<Data>();
			readQueue = new Queue<Data>();

			mustReconnect = true;
			IsAlive = true;

			counterEnqueuePerSec = Metrics.Meter("node write enqueue/sec", endpoint.ToString(), Interval.Seconds);
			counterDequeuePerSec = Metrics.Meter("node write dequeue/sec", endpoint.ToString(), Interval.Seconds);
			counterOpReadPerSec = Metrics.Meter("node op read/sec", endpoint.ToString(), Interval.Seconds);
			counterWriteQueue = Metrics.Counter("write queue length", endpoint.ToString());
			counterReadQueue = Metrics.Counter("read queue length", endpoint.ToString());

			counterWritePerSec = Metrics.Meter("node write/sec", endpoint.ToString(), Interval.Seconds);
			counterErrorPerSec = Metrics.Meter("node in error/sec", endpoint.ToString(), Interval.Seconds);
			counterItemCount = Metrics.Counter("commands", endpoint.ToString());
			gaugeSendSpeed = Metrics.Gauge("send speed", endpoint.ToString());
		}

		protected abstract IResponse CreateResponse();

		protected WriteBuffer WriteBuffer { get { return socket.WriteBuffer; } }
		public IPEndPoint EndPoint { get { return endpoint; } }

		public bool IsAlive
		{
			get { return Volatile.Read(ref isAlive) == ALIVE; }
			private set { Volatile.Write(ref isAlive, value ? ALIVE : DEAD); }
		}

		public void Connect()
		{
			Connect(CancellationToken.None);
		}

		public virtual void Connect(CancellationToken token)
		{
			Debug.Assert(currentWriteCopier == null);
			Debug.Assert(inprogressResponse == null);
			Debug.Assert(readQueue.Count == 0);

			if (LogDebugEnabled) log.Debug("Connecting node to " + endpoint);

			socket.Connect(endpoint, token);

			mustReconnect = false;
			IsAlive = true;
		}

		public void Shutdown()
		{
			IsAlive = false;

			if (socket != null)
			{
				socket.Dispose();
				//socket = null;
			}
		}

		public virtual Task<IOperation> Enqueue(IOperation op)
		{
			var tcs = new TaskCompletionSource<IOperation>();
			counterItemCount.Increment();

			if (IsAlive)
			{
				writeQueue.Enqueue(new Data { Op = op, Task = tcs });
				counterEnqueuePerSec.Increment();
				counterWriteQueue.Increment();

				if (trace.IsEnabled()) trace.EnqueueWriteOp(name);
			}
			else
			{
				counterErrorPerSec.Increment();
				tcs.SetException(new IOException(endpoint + " is not alive"));
			}

			return tcs.Task;
		}

		public virtual void Run()
		{
			if (!MarkAsBusy()) return;

			try
			{
				if (mustReconnect) Connect(CancellationToken.None);
				if (!IsAlive)
				{
					var dead = new IOException("Node is dead: " + EndPoint);
					FailQueue(writeQueue, dead);
					throw dead;
				}

				// socket still working but we got requeued, so quit
				// happens when OPs are coming in, but we're still processing the previous batch
				if (socket.IsBusy)
				{
					if (LogTraceEnabled) log.Trace("Node {0}'s socket is busy", endpoint);
					return;
				}

				DoRun();
			}
			catch (Exception e)
			{
				if (FailMe(e)) throw;
			}
		}

		private bool MarkAsBusy()
		{
			return Interlocked.CompareExchange(ref workLock, 1, 0) == 0;
		}

		private void MarkAsReady()
		{
#if DEBUG
			var v = Interlocked.CompareExchange(ref workLock, 0, 1);
			Debug.Assert(v == 1);
#else
			Interlocked.Exchange(ref workLock, 0);
#endif
		}

		private void DoRun()
		{
			switch (runMode)
			{
				case MODE_SEND:
					// write the current (in progress) op into the write buffer
					// - or -
					// start writing a new one (until we run out of ops or space)
					if (!ContinueWritingCurrentOp())
					{
						while (!socket.WriteBuffer.IsFull)
						{
							var data = GetNextOp();
							if (data.IsEmpty) break;

							counterDequeuePerSec.Increment();
							if (trace.IsEnabled()) trace.DequeueWriteOp(name);
							WriteOp(data);
						}
					}

					// did we write anything?
					if (socket.WriteBuffer.Position > 0)
					{
						FlushWriteBuffer();
					}
					else
					{
						// did not have any ops to send, quit
						MarkAsReady();
					}

					break;

				case MODE_RECEIVE:
					// do we have ops queued to be read?
					if (readQueue.Count > 0)
					{
						PerformReceive();
					}
					else
					{
						// nothing to receive, switch to send mode
						Volatile.Write(ref runMode, MODE_SEND);
						goto case MODE_SEND;
					}
					break;
			}
		}

		/// <summary>
		/// Sends the current chunked op. Happens when an op's data cannot fit the write buffer in one pass.
		/// </summary>
		/// <returns>returns true if further IO is required; false if no inprogress op present or the last chunk was successfully added to the buffer</returns>
		private bool ContinueWritingCurrentOp()
		{
			// check if we have an op in progress
			if (currentWriteCopier == null) return false;
			if (currentWriteCopier.WriteTo(socket.WriteBuffer)) return true;

			// last chunk was sent
			if (LogTraceEnabled) log.Trace("Sent & finished " + currentWriteOp.Op);


			// op is sent fully; response can be expected
			readQueue.Enqueue(currentWriteOp);
			counterReadQueue.Increment();
			if (trace.IsEnabled()) trace.EnqueueReadOp(name);

			// clean up
			currentWriteCopier.Dispose();
			currentWriteCopier = null;
			currentWriteOp = Data.Empty;

			return false;
		}

		protected virtual void FlushWriteBuffer()
		{
			counterWritePerSec.Increment();

			socket.ScheduleSend(success =>
			{
				if (success)
				{
					// successfully sent the buffer, switch to receive mode
					Volatile.Write(ref runMode, MODE_RECEIVE);
					MarkAsReady();
					owner.NeedsIO(this);
				}
				else
				{
					// this is a soft fail (cannot throw from other thread)
					// so we requeue for IO and Run() will throw instead
					FailMe(new IOException("send fail"));
				}
			});
		}

		protected virtual Data GetNextOp()
		{
			Data data;

			if (writeQueue.TryDequeue(out data))
			{
				if (trace.IsEnabled()) trace.DequeueWriteOp(name);
				counterWriteQueue.Decrement();
				return data;
			}

			return Data.Empty;
		}

		/// <summary>
		/// Writes an operation to the output buffer. Handles the case where the op does not fit the buffer fully.
		/// </summary>
		/// <param name="data"></param>
		protected virtual void WriteOp(Data data)
		{
			if (currentWriteCopier != null)
				throw new InvalidOperationException("Cannot write an operation while another is in progress.");

			var request = data.Op.CreateRequest();

			if (!request.WriteTo(socket.WriteBuffer)) // no pending IO => fully written
			{
				readQueue.Enqueue(data);
				counterReadQueue.Increment();
				if (trace.IsEnabled()) trace.EnqueueReadOp(name);
				request.Dispose();

				if (LogTraceEnabled) log.Trace("Full send of " + data.Op);
			}
			else
			{
				// it did not fit into the write buffer, so save the current op
				// as "in-progress"; DoRun will loop until it's fully sent
				currentWriteOp = data;
				currentWriteCopier = request;
				if (LogTraceEnabled) log.Trace("Partial send of " + data.Op);
			}
		}

		private void PerformReceive()
		{
		fill:
			// no data to process => read the socket
			if (socket.ReadBuffer.IsEmpty)
			{
				if (LogTraceEnabled) log.Trace("Read buffer is empty, ask for more.");

				socket.ScheduleReceive(success =>
				{
					if (success)
					{
						MarkAsReady();
						owner.NeedsIO(this);
					}
					else
					{
						// this is a soft fail (cannot throw from other thread),
						// so we requeue for IO and exception will be thrown by Receive()
						FailMe(new IOException("Failed receiving from " + endpoint));
					}
				});

				return;
			}

			// process the commands in the readQueue
			while (readQueue.Count > 0)
			{
				// continue filling the previously unfinished response,
				// or create a new one
				var response = inprogressResponse ?? CreateResponse();

				// continue filling the Response object from the buffer
				// Read() returns true if further data (IO) is required
				// (usually when the current response data is larger than the receive buffer size)
				if (response.Read(socket.ReadBuffer))
				{
					inprogressResponse = response;
					if (LogTraceEnabled) log.Trace("Response is not read fully, continue reading from the socket.");

					// refill the buffer
					// TODO if Receive returns synchrously several times, a node with a huge inprogress response can monopolize the IO thread
					goto fill;
				}

				// successfully read a response from the read buffer
				inprogressResponse = null;
				var matching = false;

				while (!matching && readQueue.Count > 0)
				{
					var data = readQueue.Peek();
					Debug.Assert(!data.IsEmpty);

					// if the response does not matches the current op, it means it's a
					// response to later command in the queue, so all commands before it are silent commands
					// successful silent ops will receive null as response (since we have no real response)
					// (or we've ran into a bug)
					matching = data.Op.Handles(response);
					if (LogTraceEnabled) log.Trace("Command {0} handles reponse: {1}", data.Op, matching);

					// returns false when no more IO is required => command is processed
					// otherwise continue filling the buffer
					if (!data.Op.ProcessResponse(matching ? response : null))
					{
						readQueue.Dequeue();
						counterReadQueue.Decrement();
						counterOpReadPerSec.Increment();
						if (trace.IsEnabled()) trace.DequeueReadOp(name);

						if (data.Task != null)
							data.Task.TrySetResult(data.Op);
					}
				}

				response.Dispose();
			}

			// set the node into send mode and requeue for IO
			Volatile.Write(ref runMode, MODE_SEND);
			MarkAsReady();
			owner.NeedsIO(this);
		}

		protected struct Data
		{
			public static readonly Data Empty = new Data();

			public IOperation Op;
			public TaskCompletionSource<IOperation> Task;

			public bool IsEmpty { get { return Op == null; } }
		}

		#region [ Failure handlers             ]

		private bool FailMe(Exception e)
		{
			lock (failLock)
			{
				var fail = (e is IOException) ? e : new IOException("io fail; see inner exception", e);

				// empty all queues
				FailQueue(writeQueue, fail);
				FailQueue(readQueue, fail);
				counterReadQueue.Reset();
				counterWriteQueue.Reset();

				// kill the partially sent op (if any)
				if (currentWriteCopier != null)
				{
					currentWriteOp.Task.SetException(fail);
					currentWriteOp = Data.Empty;
					currentWriteCopier = null;
				}

				// kill the partially read response
				if (inprogressResponse != null)
				{
					inprogressResponse.Dispose();
					inprogressResponse = null;
				}

				Volatile.Write(ref runMode, MODE_SEND);

				// mark as dead if policy says so
				if (failurePolicy.ShouldFail(this))
				{
					IsAlive = false;
					MarkAsReady();
					return true;
				}

				// otherwise reconnect immediately
				// (when it's our turn again, to be precise)
				mustReconnect = true;
				MarkAsReady();

				// reconnect from IO thread
				owner.NeedsIO(this);

				return false;
			}
		}

		/// <summary>
		/// Cleans up an AdvQueue, marking all items as failed
		/// </summary>
		private void FailQueue(Queue<Data> queue, Exception e)
		{
			foreach (var data in queue)
			{
				var t = data.Task;
				if (t != null) t.TrySetException(e);
				counterErrorPerSec.IncrementBy(queue.Count);
			}

			queue.Clear();
		}

		/// <summary>
		/// Cleans up a ConcurrentQueue, marking all items as failed
		/// </summary>
		private void FailQueue(ConcurrentQueue<Data> queue, Exception e)
		{
			Data data;
			var have = queue.Count;
			var i = 0;

			while (queue.TryDequeue(out data))// && i < have)
			{
				var t = data.Task;
				if (t != null) t.SetException(e);
				counterErrorPerSec.IncrementBy(have);
				i++;
			}

			Debug.Assert(queue.Count == 0);
		}

		#endregion
	}
}

#region [ License information          ]

/* ************************************************************
 *
 *    Copyright (c) Attila Kiskó, enyim.com
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
