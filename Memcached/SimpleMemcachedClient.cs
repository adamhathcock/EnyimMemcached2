using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Enyim.Caching.Memcached
{
	public partial class SimpleMemcachedClient : MemcachedClientBase, ISimpleMemcachedClient
	{
	    private static readonly ILog log = LogManager.GetLogger<SimpleMemcachedClient>();

		public SimpleMemcachedClient() { }
		public SimpleMemcachedClient(IContainer container) : base(container) { }
		public SimpleMemcachedClient(ICluster cluster, IOperationFactory opFactory, IKeyTransformer keyTransformer, ITranscoder transcoder)
			: base(cluster, opFactory, keyTransformer, transcoder)
		{ }

		public Task<T> GetAsync<T>(string key)
		{
			return DoGet<T>(PerformGetCore(key, 0));
		}

		public async Task<IDictionary<string, object>> GetAsync(IEnumerable<string> keys)
		{
			try
			{
				var ops = await MultiGetCore(keys).ConfigureAwait(false);
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
						retval[kvp.Key] = null;
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

		public Task<T> GetAndTouchAsync<T>(string key, Expiration expiration)
		{
			return DoGet<T>(PerformGetAndTouchCore(key, expiration, Protocol.NO_CAS));
		}

		private async Task<T> DoGet<T>(Task<Results.IGetOperationResult> getter)
		{
			try
			{
				var result = await getter.ConfigureAwait(false);
				var converted = ConvertToValue(result);

				return (T)converted;
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled) log.Error(e);

				return default(T);
			}
		}

		public Task<bool> TouchAsync(string key, Expiration expiration)
		{
			return HandleErrors(PerformTouch(key, expiration, Protocol.NO_CAS));
		}

		public Task<bool> StoreAsync(StoreMode mode, string key, object value, Expiration expiration)
		{
			return HandleErrors(PerformStoreAsync(mode, key, value, expiration, Protocol.NO_CAS));
		}

		public Task<bool> RemoveAsync(string key)
		{
			return HandleErrors(PerformRemove(key, 0));
		}

		public Task<bool> ConcateAsync(ConcatenationMode mode, string key, ArraySegment<byte> data)
		{
			return HandleErrors(PerformConcate(mode, key, 0, data));
		}

		public async Task<ulong> MutateAsync(MutationMode mode, string key, Expiration expiration, ulong delta, ulong defaultValue)
		{
			try
			{
				var result = await PerformMutate(mode, key, expiration, delta, defaultValue, 0).ConfigureAwait(false);

				return result.Value;
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled) log.Error(e);

				return 0;
			}
		}

		public async Task<ServerStats> StatsAsync(string key)
		{
			try
			{
				var result = await PerformStats(key).ConfigureAwait(false);

				return result.Value;
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled) log.Error(e);

				return new ServerStats();
			}
		}

		public Task<bool> FlushAllAsync()
		{
			return HandleErrors(PerformFlushAll());
		}

		private static async Task<bool> HandleErrors(Task<Results.IOperationResult> task)
		{
			try
			{
				var result = await task.ConfigureAwait(false);

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
