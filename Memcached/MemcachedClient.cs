﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Enyim.Caching.Memcached
{
	public partial class MemcachedClient : MemcachedClientBase, IMemcachedClient
	{
		private static readonly ILog log = LogManager.GetCurrentClassLogger();

		public MemcachedClient() : base() { }
		public MemcachedClient(IContainer container) : base(container) { }
		public MemcachedClient(ICluster cluster, IOperationFactory opFactory, IKeyTransformer keyTransformer, ITranscoder transcoder)
			: base(cluster, opFactory, keyTransformer, transcoder) { }

		public T Get<T>(string key)
		{
			return GetAsync<T>(key).Result;
		}

		public IDictionary<string, object> Get(IEnumerable<string> keys)
		{
			return GetAsync(keys).Result;
		}

		public async Task<T> GetAsync<T>(string key)
		{
			try
			{
				var result = await PerformGetCore(key);
				var converted = ConvertToValue(result);

				return (T)converted;
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled) log.Error(e);

				return default(T);
			}
		}

		public async Task<IDictionary<string, object>> GetAsync(IEnumerable<string> keys)
		{
			try
			{
				var ops = await MultiGetCore(keys);
				var retval = new Dictionary<string, object>();

				foreach (var kvp in ops)
				{
					try
					{
						retval[kvp.Key] = ConvertToValue(kvp.Value.Result);
					}
					catch (Exception e)
					{
						if (log.IsErrorEnabled) log.Error(e);
					}
				}

				return retval;
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled) log.Error(e);

				return new Dictionary<string, object>();
			}
		}

		public bool Store(StoreMode mode, string key, object value, DateTime expiresAt)
		{
			return StoreAsync(mode, key, value, expiresAt).Result;
		}

		public Task<bool> StoreAsync(StoreMode mode, string key, object value, DateTime expiresAt)
		{
			return HandleErrors(PerformStoreAsync(mode, key, value, 0, GetExpiration(expiresAt)));
		}

		public bool Remove(string key)
		{
			return RemoveAsync(key).Result;
		}

		public Task<bool> RemoveAsync(string key)
		{
			return HandleErrors(PerformRemove(key, 0));
		}

		public bool Concate(ConcatenationMode mode, string key, ArraySegment<byte> data)
		{
			return ConcateAsync(mode, key, data).Result;
		}

		public Task<bool> ConcateAsync(ConcatenationMode mode, string key, ArraySegment<byte> data)
		{
			return HandleErrors(PerformConcate(mode, key, 0, data));
		}

		public ulong Mutate(MutationMode mode, string key, ulong defaultValue, ulong delta, DateTime expiresAt)
		{
			return MutateAsync(mode, key, defaultValue, delta, expiresAt).Result;
		}

		public async Task<ulong> MutateAsync(MutationMode mode, string key, ulong defaultValue, ulong delta, DateTime expiresAt)
		{
			try
			{
				var result = await PerformMutate(mode, key, defaultValue, delta, 0, GetExpiration(expiresAt));

				return result.Value;
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled) log.Error(e);

				return 0;
			}
		}

		public ServerStats Stats(string key)
		{
			return PerformStats(key).Result.Value;
		}

		public async Task<ServerStats> StatsAsync(string key)
		{
			try
			{
				var result = await PerformStats(key);

				return result.Value;
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled) log.Error(e);

				return ServerStats.Empty;
			}
		}

		public bool FlushAll()
		{
			return FlushAllAsync().Result;
		}

		public Task<bool> FlushAllAsync()
		{
			return HandleErrors(PerformFlushAll());
		}

		private static async Task<bool> HandleErrors(Task<Results.IOperationResult> task)
		{
			try
			{
				var result = await task;

				return result.Success;
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled) log.Error(e);

				return false;
			}
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
