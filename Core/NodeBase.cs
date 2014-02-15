﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Enyim.Caching
{
	public abstract class NodeBase : INode
	{
		private static readonly ILog log = LogManager.GetCurrentClassLogger();

		private readonly ICluster owner;
		private readonly IPEndPoint endpoint;
		private readonly IFailurePolicy failurePolicy;

		private readonly ISocket socket;

		private readonly ConcurrentQueue<Data> writeQueue;
		private readonly Queue<Data> readQueue;
		private readonly Queue<Data> bufferQueue;

		private readonly WriteBuffer writeBuffer;
		private readonly ReceiveBuffer readStream;

		private int state;
		private IRequest currentWriteCopier;
		private Data currentWriteOp;
		private IResponse inprogressResponse;

		private bool mustReconnect;

		protected NodeBase(ICluster owner, IPEndPoint endpoint, IFailurePolicy failurePolicy, ISocket socket)
		{
			this.owner = owner;
			this.endpoint = endpoint;
			this.socket = socket;
			this.failurePolicy = failurePolicy;

			this.writeQueue = new ConcurrentQueue<Data>();
			this.readQueue = new Queue<Data>();
			this.bufferQueue = new Queue<Data>();

			this.writeBuffer = new WriteBuffer(CONSTS.BufferSize);
			this.readStream = new ReceiveBuffer(CONSTS.BufferSize);

			this.mustReconnect = true;
			IsAlive = true;
		}

		protected WriteBuffer WriteBuffer { get { return writeBuffer; } }
		public IPEndPoint EndPoint { get { return endpoint; } }

		public bool IsAlive
		{
			get { return state == 1; }
			private set { Interlocked.Exchange(ref state, value ? 1 : 0); }
		}

		public void Connect()
		{
			Connect(true, CancellationToken.None);
		}

		public virtual void Connect(bool reset, CancellationToken token)
		{
			Debug.Assert(currentWriteCopier == null);
			Debug.Assert(inprogressResponse == null);
			Debug.Assert(readQueue.Count == 0);
			Debug.Assert(bufferQueue.Count == 0);

			if (log.IsDebugEnabled) log.Debug("Connecting node to {0}, will reset write queue: {1}", endpoint, reset);

			socket.Connect(endpoint, token);
			if (reset) writeQueue.Clear();

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

			if (IsAlive)
				writeQueue.Enqueue(new Data { Op = op, Task = tcs });
			else
				tcs.SetException(new IOException(endpoint + " is not alive"));

			return tcs.Task;
		}

		public virtual bool Send()
		{
			return Run(PerformSend);
		}

		public virtual bool Receive()
		{
			return Run(PerformReceive2);
		}

		private bool Run(Func<bool> work)
		{
			try
			{
				if (mustReconnect) Connect(false, CancellationToken.None);

				return IsAlive && work();
			}
			catch (Exception e)
			{
				if (failurePolicy.ShouldFail())
				{
					IsAlive = false;
					HandleIOFail(e);
					throw;
				}

				mustReconnect = true;
				return true;
			}
		}

		protected virtual void HandleIOFail(Exception e)
		{
			var fail = new IOException("io fail; see inner exception", e);

			FailQueue(bufferQueue, fail);
			FailQueue(readQueue, fail);
			if (currentWriteOp.Task != null) currentWriteOp.Task.SetException(fail);

			// data read previously cannot be trusted
			readStream.Reset();
			// write buffer may start with a partial op when a previous send fails
			writeBuffer.Reset();

			currentWriteOp = Data.Empty;
			currentWriteCopier = null;
			inprogressResponse = null;
		}

		private void FailQueue(Queue<Data> queue, Exception e)
		{
			foreach (var data in queue)
				data.Task.SetException(e);

			queue.Clear();
		}

		/// <summary>
		/// Sends the current chunked op (its data could not fit the write buffer in one pass)
		/// </summary>
		/// <returns>returns true if further IO is required; false if no inprogress op present or the last chunk was successfully added to the buffer</returns>
		private bool SendInProgressOp()
		{
			// check if we have an op in progress
			if (currentWriteCopier == null) return false;
			if (currentWriteCopier.WriteTo(writeBuffer)) return true;

			// last chunk was sent
			if (log.IsTraceEnabled) log.Trace("Sent & finished " + currentWriteOp.Op);

			// finished writing, clean up
			bufferQueue.Enqueue(currentWriteOp);
			currentWriteCopier.Dispose();
			currentWriteCopier = null;
			currentWriteOp = Data.Empty;

			return false;
		}

		private bool PerformSend()
		{
			Debug.Assert(IsAlive);

			if (!SendInProgressOp())
			{
				// no in progress op (or just finished), try filling up the buffer
				while (!writeBuffer.IsFull && DequeueAndWrite()) ;
				//{
				//	if (!DequeueAndWrite()) 
				//		break;
				//}
			}

			// did we write anything?
			if (writeBuffer.Position > 0)
			{
				FlushWriteBuffer();

				return true;
			}

			Debug.Assert(bufferQueue.Count == 0);

			return false;
		}

		protected virtual void FlushWriteBuffer()
		{
			var data = writeBuffer.GetBuffer();
			socket.Send(data, 0, writeBuffer.Position);
			writeBuffer.Reset();

			if (bufferQueue.Count > 0) readQueue.Enqueue(bufferQueue);
			if (log.IsTraceEnabled) log.Trace("Flush write buffer " + bufferCounter++);
		}

		private uint bufferCounter;

		protected virtual Data GetNextOp()
		{
			Data data;

			return writeQueue.TryDequeue(out data)
					? data
					: Data.Empty;
		}

		private bool DequeueAndWrite()
		{
			var data = GetNextOp();

			if (data.IsEmpty)
				return false;

			WriteOp(data);

			return true;
		}

		protected virtual void WriteOp(Data data)
		{
			if (currentWriteCopier != null)
				throw new InvalidOperationException("Cannot write operation while another is in progress.");

			var request = data.Op.CreateRequest();

			if (!request.WriteTo(writeBuffer)) // fully written
			{
				bufferQueue.Enqueue(data);
				request.Dispose();

				if (log.IsTraceEnabled) log.Trace("Full send of " + data.Op);
			}
			else
			{
				// it did not fit into the writeBuffer, so save the current op
				// as "in-progress"; PerformSend will loop until it's fully sent
				currentWriteOp = data;
				currentWriteCopier = request;
				if (log.IsTraceEnabled) log.Trace("Partial send of " + data.Op);
			}
		}

		protected abstract IResponse CreateResponse();

		private bool receiveInProgress;

		private bool PerformReceive2()
		{
			Debug.Assert(IsAlive);
			if (readQueue.Count == 0 || receiveInProgress) return false;

		fill:
			// no data to process => read the socket
			if (readStream.EOF)
			{
				receiveInProgress = true;
				readStream.FillAsync(socket, () =>
				{
					receiveInProgress = false;
					owner.NeedsIO(this);
				});

				return false;
			}

			while (readQueue.Count > 0)
			{
				var response = inprogressResponse ?? CreateResponse();

				if (response.Read(readStream)) // is IO pending? (if response is not read fully)
				{
					// the ony reason to need data should be an empty receive buffer
					Debug.Assert(readStream.EOF);
					Debug.Assert(inprogressResponse == null);

					inprogressResponse = response;
					goto fill;
					//return true;
				}

				if (inprogressResponse != null)
				{
					inprogressResponse.Dispose();
					inprogressResponse = null;
				}

				var matching = false;

				while (!matching && readQueue.Count > 0)
				{
					var data = readQueue.Peek();
					matching = data.Op.Handles(response);

					// null is a response to a successful quiet op
					// we have to feed the responses to the current op
					// until it returns false
					if (!data.Op.ProcessResponse(matching ? response : null))
					{
						readQueue.Dequeue();

						if (data.Task != null)
							data.Task.TrySetResult(data.Op);
					}
				}

				response.Dispose();
			}

			return false;
		}

		protected struct Data
		{
			public static readonly Data Empty = new Data();

			public IOperation Op;
			public TaskCompletionSource<IOperation> Task;

			public bool IsEmpty { get { return Op == null; } }
		}
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
