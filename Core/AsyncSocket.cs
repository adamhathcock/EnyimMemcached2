using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Enyim.Caching
{
	[DebuggerDisplay("[ Address: {endpoint}, IsAlive = {IsAlive} ]")]
	public class AsyncSocket : ISocket
	{
		#region [ Defaults                     ]

		public static class Defaults
		{
			public const int MaxBufferSize = 1 * 1024 * 1024;
			public const int MinBufferSize = 4096;

			public const int SendBufferSize = 64 * 1024;
			public const int ReceiveBufferSize = 64 * 1024;

			public const int ConnectionTimeoutMsec = 10000;
			public const int SendTimeoutMsec = 10000;
			public const int ReceiveTimeoutMsec = 10000;
		}

		#endregion

		private static readonly ILog log = LogManager.GetLogger<AsyncSocket>();
		private readonly object ConnectLock = new object();
		private readonly CoreEventSource trace = EventSources.CoreEventSource;

		private IPEndPoint endpoint;
		private string name; // used for tracing

		private TimeSpan connectionTimeout;
		private TimeSpan sendTimeout;
		private TimeSpan receiveTimeout;
		private int sendBufferSize;
		private int receiveBufferSize;

		private Socket socket;
		private int isAlive;
		private int isWorking;

		private SocketAsyncEventArgs recvArgs;
		private ReadBuffer recvBuffer;

		private SocketAsyncEventArgs sendArgs;
		private WriteBuffer sendBuffer;

		public AsyncSocket()
		{
			ConnectionTimeout = TimeSpan.FromMilliseconds(Defaults.ConnectionTimeoutMsec);
			SendTimeout = TimeSpan.FromMilliseconds(Defaults.SendTimeoutMsec);
			ReceiveTimeout = TimeSpan.FromMilliseconds(Defaults.ReceiveTimeoutMsec);

			SendBufferSize = Defaults.SendBufferSize;
			ReceiveBufferSize = Defaults.ReceiveBufferSize;
		}

		public void Connect(IPEndPoint endpoint, CancellationToken token)
		{
			lock (ConnectLock)
			{
				PerformConnect(endpoint, token);
			}
		}

		private void PerformConnect(IPEndPoint endpoint, CancellationToken token)
		{
			InitBuffers();

			this.endpoint = endpoint;
			this.name = endpoint.ToString();
			this.IsAlive = false;

			using (var mre = new ManualResetEventSlim(false))
			using (var opt = new SocketAsyncEventArgs { RemoteEndPoint = endpoint })
			{
				trace.ConnectStart(name);

				opt.Completed += (a, b) => mre.Set();
				RecreateSocket();
				IsBusy = true;

				try
				{
					if (socket.ConnectAsync(opt)
						&& !mre.Wait((int)ConnectionTimeout.TotalMilliseconds, token))
					{
						trace.ConnectFail(name, SocketError.TimedOut);
						Socket.CancelConnectAsync(opt);
						throw new TimeoutException($"Connection timeout {ConnectionTimeout} has been exceeded while trying to connect to {endpoint}");
					}

					if (opt.SocketError != SocketError.Success)
					{
						trace.ConnectFail(name, opt.SocketError);
						throw new IOException($"Could not connect to {endpoint}");
					}

					IsAlive = true;
				}
				finally
				{
					IsBusy = false;
				}
			}
		}

		#region [ Init                         ]

		private void InitBuffers()
		{
			if (sendArgs == null)
			{
				sendArgs = SocketAsyncEventArgsFactory.Instance.Take(SendBufferSize);
				sendArgs.Completed += SendAsyncCompleted;
				sendBuffer = new WriteBuffer(sendArgs.Buffer, sendArgs.Offset, sendArgs.Count);

				recvArgs = SocketAsyncEventArgsFactory.Instance.Take(ReceiveBufferSize);
				recvArgs.Completed += ReceiveAsyncCompleted;
				recvBuffer = new ReadBuffer(recvArgs.Buffer, recvArgs.Offset, recvArgs.Count);
			}
			else
			{
				sendBuffer.Reset();
				recvBuffer.SetAvailableLength(0);
			}
		}

		private void RecreateSocket()
		{
			DestroySocket();

			socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
			{
				NoDelay = true,
				ReceiveBufferSize = ReceiveBufferSize,
				SendBufferSize = SendBufferSize,
				ReceiveTimeout = ToTimeout(ReceiveTimeout),
				SendTimeout = ToTimeout(SendTimeout)
			};
		}

		#endregion

		public void ScheduleSend(Action<bool> whenDone)
		{
			if (trace.IsEnabled()) trace.SendStart(name, IsAlive, sendBuffer.Position);

			if (!IsAlive)
			{
				whenDone(false);
				return;
			}

			IsBusy = true;
			sendArgs.UserToken = whenDone;

			PerformSend(sendBuffer.BufferOffset, sendBuffer.Position);
		}

		private void PerformSend(int sendOffset, int sendCount)
		{
			for (;;)
			{
				// try sending all our data
				sendArgs.SetBuffer(sendOffset, sendCount);
				// send is being done asynchrously (SendAsyncCompleted will clean up)
				if (socket.SendAsync(sendArgs)) break;

				// send was done synchronously
				var sent = sendArgs.BytesTransferred;
				if (trace.IsEnabled()) trace.SendChunk(name, IsAlive, sent, sendArgs.SocketError);

				// check for fail
				if (sendArgs.SocketError != SocketError.Success || sent < 1)
				{
					// socket error
					FinishSending(false);
					break;
				}

				sendOffset += sent;
				sendCount -= sent;

				Debug.Assert(sendCount >= 0);

				// no data is remaining: quit
				// otherwise try sending a new chunk
				if (sendCount == 0)
				{
					sendBuffer.Reset();
					FinishSending(true);
					break;
				}
			}
		}

		private void SendAsyncCompleted(object sender, SocketAsyncEventArgs e)
		{
			var sent = sendArgs.BytesTransferred;
			if (trace.IsEnabled()) trace.SendChunk(name, IsAlive, sent, sendArgs.SocketError);

			// failed during send
			if (sendArgs.SocketError != SocketError.Success || sent < 1)
			{
				FinishSending(false);
				return;
			}

			var sendOffset = sendArgs.Offset + sent;
			var sendCount = sendArgs.Count - sent;

			// OS sent less data than we asked for, so send the remaining data
			if (sendCount > 0)
			{
				PerformSend(sendOffset, sendCount);
			}
			else
			{
				Debug.Assert(sendCount == 0);

				// all data was sent
				sendBuffer.Reset();
				FinishSending(true);
			}
		}

		private void FinishSending(bool success)
		{
			if (trace.IsEnabled()) trace.SendStop(name, IsAlive, success);

			var callback = (Action<bool>)sendArgs.UserToken;
			IsBusy = false;
			callback(success);
		}

		public void ScheduleReceive(Action<bool> whenDone)
		{
			if (trace.IsEnabled()) trace.ReceiveStart(name, IsAlive);

			if (!IsAlive)
			{
				whenDone(false);
				return;
			}

			IsBusy = true;
			recvArgs.UserToken = whenDone;

			if (!socket.ReceiveAsync(recvArgs))
			{
				// receive was done synchronously
				ReceiveAsyncCompleted(null, recvArgs);
			}
		}

		private void ReceiveAsyncCompleted(object sender, SocketAsyncEventArgs recvArgs)
		{
			var received = recvArgs.BytesTransferred;
			if (trace.IsEnabled()) trace.ReceiveChunk(name, IsAlive, received, recvArgs.SocketError);

			var success = recvArgs.SocketError == SocketError.Success && received > 0;
			recvBuffer.SetAvailableLength(success ? received : 0);

			FinishReceiving(success);
		}

		private void FinishReceiving(bool success)
		{
			if (trace.IsEnabled()) trace.ReceiveStop(name, IsAlive, success);

			var callback = (Action<bool>)recvArgs.UserToken;
			IsBusy = false;
			callback(success);
		}

		private static int ToTimeout(TimeSpan time)
		{
			return time == TimeSpan.MaxValue ? Timeout.Infinite : (int)time.TotalMilliseconds;
		}

		#region [ Property noise               ]

		public bool IsAlive
		{
			get { return Volatile.Read(ref isAlive) == 1; }
			private set { Volatile.Write(ref isAlive, value ? 1 : 0); }
		}

		public bool IsBusy
		{
			get { return Volatile.Read(ref isWorking) == 1; }
			private set { Volatile.Write(ref isWorking, value ? 1 : 0); }
		}

		public ReadBuffer ReadBuffer { get { return recvBuffer; } }
		public WriteBuffer WriteBuffer { get { return sendBuffer; } }

		private void ThrowIfConnected()
		{
			Require.That(!IsAlive, "Cannot change socket options while connected.");
		}

		public TimeSpan ConnectionTimeout
		{
			get { return connectionTimeout; }
			set
			{
				ThrowIfConnected();
				Require.Value("value", value >= TimeSpan.Zero, "ConnectionTimeout must be > 0");

				connectionTimeout = value;
			}
		}

		public TimeSpan SendTimeout
		{
			get { return sendTimeout; }
			set
			{
				ThrowIfConnected();
				Require.Value("value", value >= TimeSpan.Zero, "SendTimeout must be > 0");

				sendTimeout = value;
			}
		}

		public TimeSpan ReceiveTimeout
		{
			get { return receiveTimeout; }
			set
			{
				ThrowIfConnected();
				Require.Value("value", value >= TimeSpan.Zero, "ReceiveTimeout must be > 0");

				receiveTimeout = value;
			}
		}

		public int SendBufferSize
		{
			get { return sendBufferSize; }
			set
			{
				ThrowIfConnected();
				Require.Value("value", value >= Defaults.MinBufferSize, "SendBufferSize must be >= " + Defaults.MinBufferSize);
				Require.Value("value", value <= Defaults.MaxBufferSize, "SendBufferSize must be <= " + Defaults.MaxBufferSize);
				Require.Value("value", value % 4096 == 0, "SendBufferSize must be a multiply of 4k");

				sendBufferSize = value;
			}
		}

		public int ReceiveBufferSize
		{
			get { return receiveBufferSize; }
			set
			{
				ThrowIfConnected();
				Require.Value("value", value >= Defaults.MinBufferSize, "ReceiveBufferSize must be > 0" + Defaults.MinBufferSize);
				Require.Value("value", value <= Defaults.MaxBufferSize, "ReceiveBufferSize must be <= " + Defaults.MaxBufferSize);
				Require.Value("value", value % 4096 == 0, "ReceiveBufferSize must be a multiply of 4k");

				receiveBufferSize = value;
			}
		}

		#endregion
		#region [ Cleanup                      ]

		~AsyncSocket()
		{
			Dispose();
		}

		public void Dispose()
		{
			GC.SuppressFinalize(this);

			lock (ConnectLock)
			{
				if (socket != null)
				{
					sendArgs.Completed -= SendAsyncCompleted;
					recvArgs.Completed -= ReceiveAsyncCompleted;

					SocketAsyncEventArgsFactory.Instance.Return(recvArgs);
					SocketAsyncEventArgsFactory.Instance.Return(sendArgs);

					DestroySocket();
				}
			}
		}

		private void DestroySocket()
		{
			if (socket != null)
			{
				try
				{
					using (socket)
					{
						socket.Shutdown(SocketShutdown.Both);
						socket.Dispose();
					}
				}
				catch (Exception e)
				{
					if (log.IsDebugEnabled) log.Debug("Exception while destroying socket.", e);
				}

				socket = null;
			}
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
