using System;
using System.Collections.Generic;
#if NET451
using System.Diagnostics.CodeAnalysis;
#endif
using System.IO;

namespace Enyim.Caching.Memcached
{
#if NET451
    [SuppressMessage("Potential Code Quality Issues", "NonReadonlyReferencedInGetHashCodeIssue:Non-readonly field referenced in 'GetHashCode()'", Justification = "Only Dispose() changes the array")]
#endif
	public struct CacheItem : IDisposable
	{
		public static readonly CacheItem Empty = new CacheItem(0, PooledSegment.Empty);

		private readonly uint flags;
		private readonly PooledSegment segment;

		public CacheItem(uint flags, PooledSegment segment)
		{
			this.flags = flags;
			this.segment = segment;
		}

		public uint Flags { get { return flags; } }
		public PooledSegment Segment { get { return segment; } }

		public void Dispose()
		{
			segment.Dispose();
		}

		public override bool Equals(object obj)
		{
			return obj is CacheItem && Equals((CacheItem)obj);
		}

		public bool Equals(CacheItem obj)
		{
			return obj.flags == flags && obj.segment.Equals(segment);
		}

		public override int GetHashCode()
		{
			return flags.GetHashCode() ^ segment.GetHashCode();
		}

		public static bool operator ==(CacheItem a, CacheItem b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(CacheItem a, CacheItem b)
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
