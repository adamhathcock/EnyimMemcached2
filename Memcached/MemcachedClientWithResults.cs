﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enyim.Caching.Memcached.Results;

namespace Enyim.Caching.Memcached
{
	public partial class MemcachedClientWithResults : MemcachedClientBase, IMemcachedClientWithResults
	{
		public MemcachedClientWithResults(ICluster cluster, IOperationFactory opFactory, IKeyTransformer keyTransformer, ITranscoder transcoder)
			: base(cluster, opFactory, keyTransformer, transcoder) { }

		public IGetOperationResult<T> Get<T>(string key)
		{
			return ((IMemcachedClientWithResults)this).GetAsync<T>(key).Result;
		}

		public IDictionary<string, IGetOperationResult<object>> Get(IEnumerable<string> keys)
		{
			return ((IMemcachedClientWithResults)this).GetAsync(keys).Result;
		}

		public async Task<IGetOperationResult<T>> GetAsync<T>(string key)
		{
			var result = await PerformGetCore(key);
			var converted = ConvertToResult<T>(result);

			return converted;
		}

		public async Task<IDictionary<string, IGetOperationResult<object>>> GetAsync(IEnumerable<string> keys)
		{
			var ops = await MultiGetCore(keys);
			var retval = new Dictionary<string, IGetOperationResult<object>>();

			foreach (var kvp in ops)
			{
				retval[kvp.Key] = ConvertToResult<object>(kvp.Value.Result);
			}

			return retval;
		}

		public IOperationResult Store(StoreMode mode, string key, object value, ulong cas, DateTime expiresAt)
		{
			return PerformStoreAsync(mode, key, value, cas, GetExpiration(expiresAt)).Result;
		}

		public Task<IOperationResult> StoreAsync(StoreMode mode, string key, object value, ulong cas, DateTime expiresAt)
		{
			return PerformStoreAsync(mode, key, value, cas, GetExpiration(expiresAt));
		}

		public IOperationResult Remove(string key, ulong cas)
		{
			return PerformRemove(key, cas).Result;
		}

		public Task<IOperationResult> RemoveAsync(string key, ulong cas)
		{
			return PerformRemove(key, cas);
		}

		public IOperationResult Concate(ConcatenationMode mode, string key, ArraySegment<byte> data, ulong cas)
		{
			return PerformConcate(mode, key, cas, data).Result;
		}

		public Task<IOperationResult> ConcateAsync(ConcatenationMode mode, string key, ArraySegment<byte> data, ulong cas)
		{
			return PerformConcate(mode, key, cas, data);
		}

		public IMutateOperationResult Mutate(MutationMode mode, string key, ulong defaultValue, ulong delta, ulong cas, DateTime expiresAt)
		{
			return PerformMutate(mode, key, defaultValue, delta, cas, GetExpiration(expiresAt)).Result;
		}

		public Task<IMutateOperationResult> MutateAsync(MutationMode mode, string key, ulong defaultValue, ulong delta, ulong cas, DateTime expiresAt)
		{
			return PerformMutate(mode, key, defaultValue, delta, cas, GetExpiration(expiresAt));
		}


		public IOperationResult FlushAll()
		{
			return PerformFlushAll().Result;
		}

		public Task<IOperationResult> FlushAllAsync()
		{
			return PerformFlushAll();
		}

		public IStatsOperationResult Stats(string key)
		{
			return PerformStats(key).Result;
		}

		public Task<IStatsOperationResult> StatsAsync(string key)
		{
			return PerformStats(key);
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
