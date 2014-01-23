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

		private readonly IPEndPoint endpoint;
		private ISocket socket;

		private int state;
		private readonly ConcurrentQueue<Data> writeQueue;
		private readonly Queue<Data> readQueue;
		private readonly Queue<Data> bufferQueue;

		private WriteBuffer writeBuffer;
		private ReceiveBuffer readStream;

		private SegmentListCopier currentWriteCopier;
		private IResponse inprogressResponse;
		private IFailurePolicy failurePolicy;

		private bool mustReconnect;

		protected NodeBase(IPEndPoint endpoint, IFailurePolicy failurePolicy, ISocket socket)
		{
			this.BufferSize = 16384;

			this.endpoint = endpoint;
			this.socket = socket;
			this.failurePolicy = failurePolicy;

			this.writeQueue = new ConcurrentQueue<Data>();
			this.readQueue = new Queue<Data>();
			this.bufferQueue = new Queue<Data>();

			this.mustReconnect = true;
			IsAlive = true;
		}

		public int BufferSize { get; set; } // TODO throw after it's connected
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

		public void Connect(bool reset, CancellationToken token)
		{
			if (log.IsDebugEnabled) log.Debug("Connecting node to {0}, will reset write queue: {1}", endpoint, reset);

			socket.Connect(endpoint, token);

			writeBuffer = new WriteBuffer(BufferSize);
			readStream = new ReceiveBuffer(BufferSize);

			Debug.Assert(currentWriteCopier == null);
			Debug.Assert(inprogressResponse == null);
			Debug.Assert(readQueue.Count == 0);
			Debug.Assert(bufferQueue.Count == 0);

			if (reset)
			{
				writeQueue.Clear();
				writeBuffer.Reset();
			}

			mustReconnect = false;
			IsAlive = true;
		}

		public void Shutdown()
		{
			IsAlive = false;

			if (socket != null)
			{
				socket.Dispose();
				socket = null;
			}
		}

		public Task<IOperation> Enqueue(IOperation op)
		{
			var tcs = new TaskCompletionSource<IOperation>();

			if (!IsAlive)
			{
				tcs.SetException(new IOException(endpoint + " is not alive"));
			}
			else
			{
				var data = new Data
				{
					Op = op,
					Task = tcs
				};

				writeQueue.Enqueue(data);
			}

			return tcs.Task;
		}

		public virtual bool Send()
		{
			return Run(PerformSend);
		}

		public virtual bool Receive()
		{
			return Run(PerformReceive);
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

		private void HandleIOFail(Exception e)
		{
			while (bufferQueue.Count > 0)
			{
				var data = bufferQueue.Dequeue();
				data.Task.SetException(new IOException("fail fast", e));
			}

			while (readQueue.Count > 0)
			{
				var data = readQueue.Dequeue();
				data.Task.SetException(new IOException("fail fast", e));
			}

			writeBuffer.Reset();
			readStream.Reset();
			currentWriteCopier = null;
			inprogressResponse = null;
		}

		protected void AddToBuffer(IOperation op)
		{
			var request = op.CreateRequest();
			new SegmentListCopier(request.CreateBuffer()).WriteTo(writeBuffer);

			bufferQueue.Enqueue(new Data { Op = op });
		}

		private bool PerformSend()
		{
			Debug.Assert(IsAlive);

			if (currentWriteCopier != null)
			{
				if (!currentWriteCopier.WriteTo(writeBuffer))
				{
					var data = DequeueWriteOp();
					bufferQueue.Enqueue(data);
					currentWriteCopier = null;

					if (log.IsTraceEnabled) log.Trace("Sent & finished " + data.Op);
				}
			}

			if (currentWriteCopier == null)
			{
				while (!writeBuffer.IsFull && !writeQueue.IsEmpty)
				{
					var data = PeekWriteOp();
					var request = data.Op.CreateRequest();
					var copier = new SegmentListCopier(request.CreateBuffer());

					BeforeWriteOp(copier, writeBuffer, data.Op);

					if (copier.WriteTo(writeBuffer))
					{
						currentWriteCopier = copier;
						if (log.IsTraceEnabled) log.Trace("Partial send of " + data.Op);
						break;
					}

					DequeueWriteOp();
					bufferQueue.Enqueue(data);

					if (log.IsTraceEnabled) log.Trace("Full send of " + data.Op);
				}
			}

			if (writeBuffer.Position > 0)
			{
				FinalizeWriteBuffer(writeBuffer);

				var data = writeBuffer.GetBuffer();
				socket.Send(data, 0, writeBuffer.Position);
				writeBuffer.Reset();

				if (bufferQueue.Count > 0) readQueue.Enqueue(bufferQueue);
				if (log.IsTraceEnabled) log.Trace("Flush write buffer");

				return true;
			}

			Debug.Assert(bufferQueue.Count == 0);

			return false;
		}

		protected virtual void BeforeWriteOp(SegmentListCopier copier, WriteBuffer writeBuffer, IOperation op)
		{
		}

		protected virtual void FinalizeWriteBuffer(WriteBuffer buffer)
		{
		}

		private Data PeekWriteOp()
		{
			Data data;

			if (!writeQueue.TryPeek(out data))
				throw new InvalidOperationException("Write queue is empty and should not be.");

			return data;
		}

		private Data DequeueWriteOp()
		{
			Data data;

			if (!writeQueue.TryDequeue(out data))
				throw new InvalidOperationException("Write queue is empty and should not be.");

			return data;
		}

		protected abstract IResponse CreateResponse();

		private bool PerformReceive()
		{
			Debug.Assert(IsAlive);

			if (readQueue.Count == 0) return false;

			//? TODO check for availability?
			if (readStream.EOF) readStream.Fill(socket);

			while (readQueue.Count > 0)
			{
				var response = inprogressResponse ?? CreateResponse();

				if (response.Read(readStream)) // is IO pending? (if response is not read fully)
				{
					inprogressResponse = response;
					return true;
				}

				inprogressResponse = null;
				var matching = false;

				while (!matching && readQueue.Count > 0)
				{
					var data = readQueue.Peek();
					matching = data.Op.Matches(response);

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
			}

			return false;
		}

		private struct Data
		{
			public IOperation Op;
			public TaskCompletionSource<IOperation> Task;
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
