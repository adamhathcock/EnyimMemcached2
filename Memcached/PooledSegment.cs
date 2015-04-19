using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Enyim.Caching.Memcached
{
	/// <summary>
	/// Provides a reference to an segment of bytes allocated from a buffer pool.
	/// </summary>
	[SuppressMessage("Potential Code Quality Issues", "NonReadonlyReferencedInGetHashCodeIssue:Non-readonly field referenced in 'GetHashCode()'", Justification = "Only Dispose() changes the array")]
	public struct PooledSegment : IDisposable
	{
		public static readonly PooledSegment Empty = new PooledSegment() { array = new byte[0] };

		private IBufferAllocator allocator;
		private byte[] array;
		private readonly int count;

		public PooledSegment(IBufferAllocator allocator, int count)
		{
			Require.NotNull(allocator, "allocator");
			Require.That(count >= 0, "count must be >= 0");

			this.allocator = allocator;
			this.array = allocator.Take(count);
			this.count = count;
		}

		public PooledSegment(IBufferAllocator allocator, byte[] array, int count)
		{
			Require.NotNull(allocator, "allocator");
			Require.NotNull(array, "array");
			Require.That(count >= 0, "count must be >= 0");

			this.allocator = allocator;
			this.array = array;
			this.count = count;
		}

		/// <summary>
		/// Unowned buffer
		/// </summary>
		public PooledSegment(byte[] array, int count)
		{
			Require.NotNull(array, "array");
			Require.That(count >= 0, "count must be >= 0");

			this.allocator = null;
			this.array = array;
			this.count = count;
		}

		/// <summary>
		/// Unowned buffer
		/// </summary>
		public static PooledSegment From(ArraySegment<byte> source, IBufferAllocator allocator = null)
		{
			var c = source.Count;
			if (source.Offset == 0)
				return new PooledSegment(source.Array, c);

			var retval = allocator == null
							? new PooledSegment(new byte[c], c)
							: new PooledSegment(allocator, c);

			Buffer.BlockCopy(source.Array, source.Offset, retval.array, 0, c);

			return retval;
		}

		public byte[] Array { get { return array; } }
		public int Count { get { return count; } }

		public void Dispose()
		{
			if (allocator != null)
			{
				allocator.Return(array);
				array = null;
				allocator = null;
			}
		}

		public ArraySegment<byte> AsArraySegment()
		{
			return new ArraySegment<byte>(array, 0, count);
		}

		public override bool Equals(object obj)
		{
			return obj is PooledSegment && Equals((PooledSegment)obj);
		}

		public bool Equals(PooledSegment obj)
		{
			return obj.array == array && obj.count == count;
		}

		public override int GetHashCode()
		{
			return array == null
					? 0
					: array.GetHashCode() ^ count;
		}

		public static bool operator ==(PooledSegment a, PooledSegment b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(PooledSegment a, PooledSegment b)
		{
			return !a.Equals(b);
		}
	}
}
