using System;
#if NET451
using System.Diagnostics.CodeAnalysis;
#endif

namespace Enyim.Caching
{
#if NET451
    [SuppressMessage("Potential Code Quality Issues", "NonReadonlyReferencedInGetHashCodeIssue:Non-readonly field referenced in 'GetHashCode()'", Justification = "Only Dispose() changes the array")]
#endif
    public struct Key : IDisposable
	{
		public static readonly Key Empty = new Key { array = new byte[0] };

		private IBufferAllocator allocator;
		private byte[] array;
		private readonly int length;

		public Key(IBufferAllocator allocator, int length)
			: this()
		{
			Require.NotNull(allocator, "allocator");
			Require.That(length >= 0, "length must be >= 0");

			this.allocator = allocator;
			this.length = length;
			array = allocator.Take(length);
		}

		public Key(IBufferAllocator allocator, byte[] array, int length)
		{
			Require.NotNull(allocator, "allocator");
			Require.NotNull(array, "array");
			Require.That(length >= 0, "length must be >= 0");

			this.allocator = allocator;
			this.array = array;
			this.length = length;
		}

		public byte[] Array { get { return array; } }
		public int Length { get { return length; } }

		public void Dispose()
		{
			if (allocator != null)
			{
				allocator.Return(array);
				array = null;
				allocator = null;
			}
		}

		public override bool Equals(object obj)
		{
			return obj is Key && Equals((Key)obj);
		}

		public bool Equals(Key obj)
		{
			return obj.array == array && obj.length == length;
		}

		public override int GetHashCode()
		{
			return array == null ? 0 : array.GetHashCode() ^ length;
		}

		public static bool operator ==(Key a, Key b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Key a, Key b)
		{
			return !a.Equals(b);
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
