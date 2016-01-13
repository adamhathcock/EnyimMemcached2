using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Enyim.Caching
{
	public sealed class PooledMemoryStream : Stream
	{
		private const int MinimumChunkSize = 128;
		private const int ChunkGrowthFactor = 2;

		private static readonly byte[] EmptyChunk = new byte[0];

		private IBufferAllocator allocator;
		private List<byte[]> chunks; // list of chunks representing the stream data
		private List<int> lengths; // stores the actual maximum length of the stream at each chunk allocation (helper for seeking in the stream)

		private int length; // lenght of the stream
		private byte[] currentChunk; // the current chunk being read/written
		private int currentIndex; // the index of the current chunk (in 'chunks')
		private int chunkPos; // pointer to the current position inside the current chunk
		private int position; // position of the stream pointer

		private byte[] byteArray; // helper for Read/WriteByte

		public PooledMemoryStream(IBufferAllocator allocator)
		{
			this.allocator = allocator;

			chunks = new List<byte[]>();
			lengths = new List<int>();
			currentChunk = EmptyChunk;
			currentIndex = -1;

			byteArray = new byte[1];
		}

		public override bool CanSeek { get { return true; } }
		public override bool CanRead { get { return true; } }
		public override bool CanWrite { get { return true; } }
		public override long Length { get { return length; } }

		public override long Position
		{
			get { return position; }
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException("Position must be a positive integer");
				if (value > length) throw new ArgumentOutOfRangeException("Position cannot be larger than length");

				position = (int)value;
				currentIndex = lengths.FindIndex(v => v >= position);
				Debug.Assert(currentIndex > -1);
				Debug.Assert(currentIndex < chunks.Count);

				currentChunk = chunks[currentIndex];
				chunkPos = position - lengths[currentIndex] + currentChunk.Length;
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (allocator != null)
			{
				foreach (var chunk in chunks)
					allocator.Return(chunk);

				chunks = null;
				currentChunk = null;
				allocator = null;
			}
		}

		public static PooledMemoryStream From(BufferManagerAllocator pool, byte[] buffer, int length)
		{
			var retval = new PooledMemoryStream(pool);

			retval.chunks.Add(buffer);
			retval.lengths.Add(buffer.Length);
			retval.currentChunk = buffer;
			retval.currentIndex = 0;
			retval.length = length;

			return retval;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (position == length) return -1;

			var read = 0;
			var remaining = count;

			while (remaining > 0 && position < length)
			{
				var available = currentChunk.Length - chunkPos;
				if (available == 0)
				{
					if (currentIndex == chunks.Count - 1)
						break;

					currentChunk = chunks[++currentIndex];
					available = currentChunk.Length;
					chunkPos = 0;
				}

				if (available > length - position) available = length - position;

				var toRead = available > remaining ? remaining : available;
				Buffer.BlockCopy(currentChunk, chunkPos, buffer, offset, toRead);

				read += toRead;
				offset += toRead;
				chunkPos += toRead;
				remaining -= toRead;
				position += toRead;
			}

			return read;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			var remaining = count;

			while (remaining > 0)
			{
				var available = currentChunk.Length - chunkPos;

				if (available == 0)
					available = EnsureCapacity(remaining);

				var toWrite = available > remaining ? remaining : available;
				Buffer.BlockCopy(buffer, offset, currentChunk, chunkPos, toWrite);

				chunkPos += toWrite;
				offset += toWrite;
				remaining -= toWrite;
				position += toWrite;
			}

			if (position > length) length = position;
		}

		private int EnsureCapacity(int capacity)
		{
			if (currentIndex == chunks.Count - 1)
			{
				var nextSize = currentChunk.Length * ChunkGrowthFactor;

				// get a buffer at least the size of MinSize or the size of the remaining data
				if (nextSize < capacity) nextSize = capacity;
				if (nextSize < MinimumChunkSize) nextSize = MinimumChunkSize;

				currentChunk = allocator.Take(nextSize);

				chunks.Add(currentChunk);
				lengths.Add(lengths.LastOrDefault() + currentChunk.Length);
				currentIndex++;
			}
			else
			{
				currentChunk = chunks[++currentIndex];
			}

			chunkPos = 0;

			return currentChunk.Length;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			switch (origin)
			{
				case SeekOrigin.Begin: Position = offset; break;
				case SeekOrigin.Current: Position = position + offset; break;
				case SeekOrigin.End: Position = length - offset; break;
				default: throw new ArgumentOutOfRangeException("offset");
			}

			return Position;
		}

		public override int ReadByte()
		{
			var read = Read(byteArray, 0, 1);

			return read < 0 ? read : byteArray[0];
		}

		public override void WriteByte(byte value)
		{
			byteArray[0] = value;

			Write(byteArray, 0, 1);
		}

        #region [ Stream overrides             ]

#if NETCORE451
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			throw new NotSupportedException();
		}

		public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			throw new NotSupportedException();
		}

		public override int EndRead(IAsyncResult asyncResult)
		{
			throw new NotSupportedException();
		}

		public override void EndWrite(IAsyncResult asyncResult)
		{
			throw new NotSupportedException();
		}

		public override Task FlushAsync(CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}
#endif

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public override void Flush() { }

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
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
