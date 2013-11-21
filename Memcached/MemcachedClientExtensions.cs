﻿using System;
using System.Threading.Tasks;

namespace Enyim.Caching.Memcached
{
	public static class MemcachedClientExtensions
	{
		public static bool Append(this IMemcachedClient self, string key, ArraySegment<byte> data, ulong cas = 0)
		{
			return self.Concate(ConcatenationMode.Append, key, data, cas);
		}

		public static bool Prepend(this IMemcachedClient self, string key, ArraySegment<byte> data, ulong cas = 0)
		{
			return self.Concate(ConcatenationMode.Prepend, key, data, cas);
		}

		public static ulong Increment(this IMemcachedClient self, string key, ulong defaultValue, ulong delta, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Mutate(MutationMode.Increment, key, defaultValue, delta, cas, MakeExpiration(validFor, expiresAt));
		}

		private static DateTime MakeExpiration(TimeSpan? validFor, DateTime? expiresAt)
		{
			if (validFor != null)
			{
				if (expiresAt != null)
					throw new ArgumentException("Cannot specify both validFor and expiresAt");

				return DateTime.Now + validFor.Value;
			}

			return expiresAt ?? DateTime.MaxValue;
		}

		public static ulong Decrement(this IMemcachedClient self, string key, ulong defaultValue, ulong delta, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Mutate(MutationMode.Decrement, key, defaultValue, delta, cas, MakeExpiration(validFor, expiresAt));
		}

		public static Task<bool> AppendAsync(this IMemcachedClient self, string key, ArraySegment<byte> data, ulong cas = 0)
		{
			return self.ConcateAsync(ConcatenationMode.Append, key, data, cas);
		}

		public static Task<bool> PrependAsync(this IMemcachedClient self, string key, ArraySegment<byte> data, ulong cas = 0)
		{
			return self.ConcateAsync(ConcatenationMode.Prepend, key, data, cas);
		}

		public static Task<ulong> IncrementAsync(this IMemcachedClient self, string key, ulong defaultValue, ulong delta, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.MutateAsync(MutationMode.Increment, key, defaultValue, delta, cas, MakeExpiration(validFor, expiresAt));
		}

		public static Task<ulong> DecrementAsync(this IMemcachedClient self, string key, ulong defaultValue, ulong delta, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.MutateAsync(MutationMode.Decrement, key, defaultValue, delta, cas, MakeExpiration(validFor, expiresAt));
		}

		public static bool Add(this IMemcachedClient self, string key, object value, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Store(StoreMode.Add, key, value, cas, MakeExpiration(validFor, expiresAt));
		}

		public static Task<bool> AddAsync(this IMemcachedClient self, string key, object value, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.StoreAsync(StoreMode.Add, key, value, cas, MakeExpiration(validFor, expiresAt));
		}

		public static bool Replace(this IMemcachedClient self, string key, object value, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Store(StoreMode.Replace, key, value, cas, MakeExpiration(validFor, expiresAt));
		}

		public static Task<bool> ReplaceAsync(this IMemcachedClient self, string key, object value, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.StoreAsync(StoreMode.Replace, key, value, cas, MakeExpiration(validFor, expiresAt));
		}

		public static bool Set(this IMemcachedClient self, string key, object value, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Store(StoreMode.Set, key, value, cas, MakeExpiration(validFor, expiresAt));
		}

		public static Task<bool> SetAsync(this IMemcachedClient self, string key, object value, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.StoreAsync(StoreMode.Set, key, value, cas, MakeExpiration(validFor, expiresAt));
		}
	}
}