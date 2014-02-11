﻿using System;
using System.Threading.Tasks;
using Enyim.Caching.Memcached.Results;

namespace Enyim.Caching.Memcached
{
	public static class MemcachedClientWithResultsExtensions
	{
		public static IOperationResult Append(this IMemcachedClientWithResults self, string key, ArraySegment<byte> data, ulong cas = 0)
		{
			return self.Concate(ConcatenationMode.Append, key, data, cas);
		}

		public static IOperationResult Prepend(this IMemcachedClientWithResults self, string key, ArraySegment<byte> data, ulong cas = 0)
		{
			return self.Concate(ConcatenationMode.Prepend, key, data, cas);
		}

		public static IMutateOperationResult Increment(this IMemcachedClientWithResults self, string key, ulong defaultValue, ulong delta, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Mutate(MutationMode.Increment, key, defaultValue, delta, cas, MemcachedClientExtensions.MakeExpiration(validFor, expiresAt));
		}

		public static IMutateOperationResult Decrement(this IMemcachedClientWithResults self, string key, ulong defaultValue, ulong delta, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Mutate(MutationMode.Decrement, key, defaultValue, delta, cas, MemcachedClientExtensions.MakeExpiration(validFor, expiresAt));
		}

		public static Task<IOperationResult> AppendAsync(this IMemcachedClientWithResults self, string key, ArraySegment<byte> data, ulong cas = 0)
		{
			return self.ConcateAsync(ConcatenationMode.Append, key, data, cas);
		}

		public static Task<IOperationResult> PrependAsync(this IMemcachedClientWithResults self, string key, ArraySegment<byte> data, ulong cas = 0)
		{
			return self.ConcateAsync(ConcatenationMode.Prepend, key, data, cas);
		}

		public static Task<IMutateOperationResult> IncrementAsync(this IMemcachedClientWithResults self, string key, ulong defaultValue, ulong delta, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.MutateAsync(MutationMode.Increment, key, defaultValue, delta, cas, MemcachedClientExtensions.MakeExpiration(validFor, expiresAt));
		}

		public static Task<IMutateOperationResult> DecrementAsync(this IMemcachedClientWithResults self, string key, ulong defaultValue, ulong delta, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.MutateAsync(MutationMode.Decrement, key, defaultValue, delta, cas, MemcachedClientExtensions.MakeExpiration(validFor, expiresAt));
		}

		public static IOperationResult Add(this IMemcachedClientWithResults self, string key, object value, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Store(StoreMode.Add, key, value, cas, MemcachedClientExtensions.MakeExpiration(validFor, expiresAt));
		}

		public static Task<IOperationResult> AddAsync(this IMemcachedClientWithResults self, string key, object value, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.StoreAsync(StoreMode.Add, key, value, cas, MemcachedClientExtensions.MakeExpiration(validFor, expiresAt));
		}

		public static IOperationResult Replace(this IMemcachedClientWithResults self, string key, object value, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Store(StoreMode.Replace, key, value, cas, MemcachedClientExtensions.MakeExpiration(validFor, expiresAt));
		}

		public static Task<IOperationResult> ReplaceAsync(this IMemcachedClientWithResults self, string key, object value, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.StoreAsync(StoreMode.Replace, key, value, cas, MemcachedClientExtensions.MakeExpiration(validFor, expiresAt));
		}

		public static IOperationResult Set(this IMemcachedClientWithResults self, string key, object value, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.Store(StoreMode.Set, key, value, cas, MemcachedClientExtensions.MakeExpiration(validFor, expiresAt));
		}

		public static Task<IOperationResult> SetAsync(this IMemcachedClientWithResults self, string key, object value, ulong cas = 0, TimeSpan? validFor = null, DateTime? expiresAt = null)
		{
			return self.StoreAsync(StoreMode.Set, key, value, cas, MemcachedClientExtensions.MakeExpiration(validFor, expiresAt));
		}
	}
}