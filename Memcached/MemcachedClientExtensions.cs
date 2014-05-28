﻿using System;
using System.Threading.Tasks;

namespace Enyim.Caching.Memcached
{
	public static class MemcachedClientExtensions
	{
		public static object Get(this IMemcachedClient self, string key)
		{
			return self.Get<object>(key);
		}

		public static Task<object> GetAsync(this IMemcachedClient self, string key)
		{
			return self.GetAsync<object>(key);
		}

		public static bool Append(this IMemcachedClient self, string key, byte[] data)
		{
			return self.Append(key, new ArraySegment<byte>(data));
		}

		public static bool Append(this IMemcachedClient self, string key, ArraySegment<byte> data)
		{
			return self.Concate(ConcatenationMode.Append, key, data);
		}

		public static bool Prepend(this IMemcachedClient self, string key, byte[] data)
		{
			return self.Prepend(key, new ArraySegment<byte>(data));
		}

		public static bool Prepend(this IMemcachedClient self, string key, ArraySegment<byte> data)
		{
			return self.Concate(ConcatenationMode.Prepend, key, data);
		}

		public static ulong Increment(this IMemcachedClient self, string key, ulong defaultValue, ulong delta, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Mutate(MutationMode.Increment, key, defaultValue, delta, MakeExpiration(validFor, expiresAt));
		}

		public static ulong Decrement(this IMemcachedClient self, string key, ulong defaultValue, ulong delta, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Mutate(MutationMode.Decrement, key, defaultValue, delta, MakeExpiration(validFor, expiresAt));
		}

		public static Task<bool> AppendAsync(this IMemcachedClient self, string key, byte[] data)
		{
			return self.AppendAsync(key, new ArraySegment<byte>(data));
		}

		public static Task<bool> AppendAsync(this IMemcachedClient self, string key, ArraySegment<byte> data)
		{
			return self.ConcateAsync(ConcatenationMode.Append, key, data);
		}

		public static Task<bool> PrependAsync(this IMemcachedClient self, string key, byte[] data)
		{
			return self.PrependAsync(key, new ArraySegment<byte>(data));
		}

		public static Task<bool> PrependAsync(this IMemcachedClient self, string key, ArraySegment<byte> data)
		{
			return self.ConcateAsync(ConcatenationMode.Prepend, key, data);
		}

		public static Task<ulong> IncrementAsync(this IMemcachedClient self, string key, ulong defaultValue, ulong delta, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.MutateAsync(MutationMode.Increment, key, defaultValue, delta, MakeExpiration(validFor, expiresAt));
		}

		public static Task<ulong> DecrementAsync(this IMemcachedClient self, string key, ulong defaultValue, ulong delta, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.MutateAsync(MutationMode.Decrement, key, defaultValue, delta, MakeExpiration(validFor, expiresAt));
		}

		public static bool Add(this IMemcachedClient self, string key, object value, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Store(StoreMode.Add, key, value, MakeExpiration(validFor, expiresAt));
		}

		public static Task<bool> AddAsync(this IMemcachedClient self, string key, object value, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.StoreAsync(StoreMode.Add, key, value, MakeExpiration(validFor, expiresAt));
		}

		public static bool Replace(this IMemcachedClient self, string key, object value, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Store(StoreMode.Replace, key, value, MakeExpiration(validFor, expiresAt));
		}

		public static Task<bool> ReplaceAsync(this IMemcachedClient self, string key, object value, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.StoreAsync(StoreMode.Replace, key, value, MakeExpiration(validFor, expiresAt));
		}

		public static bool Set(this IMemcachedClient self, string key, object value, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Store(StoreMode.Set, key, value, MakeExpiration(validFor, expiresAt));
		}

		public static Task<bool> SetAsync(this IMemcachedClient self, string key, object value, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.StoreAsync(StoreMode.Set, key, value, MakeExpiration(validFor, expiresAt));
		}

		public static bool Store(this IMemcachedClient self, StoreMode mode, string key, object value, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Store(mode, key, value, MemcachedClientExtensions.MakeExpiration(validFor, expiresAt));
		}

		public static Task<bool> StoreAsync(this IMemcachedClient self, StoreMode mode, string key, object value, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.StoreAsync(mode, key, value, MemcachedClientExtensions.MakeExpiration(validFor, expiresAt));
		}

		internal static DateTime MakeExpiration(TimeSpan? validFor, DateTime? expiresAt)
		{
			if (validFor != null)
			{
				if (expiresAt != null)
					throw new ArgumentException("Cannot specify both validFor and expiresAt");

				return DateTime.Now + validFor.Value;
			}

			return expiresAt ?? DateTime.MaxValue;
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
