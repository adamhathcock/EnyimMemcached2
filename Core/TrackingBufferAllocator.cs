#if NET451
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.ServiceModel.Channels;

namespace Enyim.Caching
{
	public sealed class TrackingBufferAllocator : IBufferAllocator
	{
		private readonly BufferManager pool;

		public TrackingBufferAllocator(int maxBufferSize, long maxBufferPoolSize)
		{
			pool = BufferManager.CreateBufferManager(maxBufferPoolSize, maxBufferSize);
		}

		public void Dispose()
		{
			pool.Clear();
		}

		public byte[] Take(int size)
		{
			var buffer = pool.TakeBuffer(size);
			Array.Clear(buffer, 0, size);

			trackers.GetOrCreateValue(buffer).Remember();

			return buffer;
		}

		public void Return(byte[] buffer)
		{
			Tracker value;
			if (trackers.TryGetValue(buffer, out value))
				value.Forget();

			pool.ReturnBuffer(buffer);
		}

#region [ Leak detection               ]

		private readonly ConditionalWeakTable<byte[], Tracker> trackers = new ConditionalWeakTable<byte[], Tracker>();

		private class Tracker
		{
			private StackTrace stackTrace;

			public void Remember()
			{
				ThrowIfLeaking();

				stackTrace = new StackTrace(4, true);
			}

			~Tracker()
			{
				ThrowIfLeaking();
			}

			public void Forget()
			{
				stackTrace = null;
			}

			private void ThrowIfLeaking()
			{
				if (stackTrace != null)
					throw new InvalidOperationException("Buffer leak: " + stackTrace);
			}
		}

#endregion
	}
}

#endif

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
