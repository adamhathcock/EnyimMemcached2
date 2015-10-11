﻿using System;
using System.Threading.Tasks;
using Enyim.Caching.Memcached.Results;

namespace Enyim.Caching.Memcached
{
	public static partial class MemcachedClientExtensions
	{
		public static Task<IMutateOperationResult> DecrementAsync(this IMemcachedClient self, string key, ulong delta = Protocol.MUTATE_DEFAULT_DELTA, ulong defaultValue = Protocol.MUTATE_DEFAULT_VALUE, ulong cas = Protocol.NO_CAS)
		{
			return self.MutateAsync(MutationMode.Decrement, key, Expiration.Never, delta, defaultValue, cas);
		}

		public static Task<IMutateOperationResult> DecrementAsync(this IMemcachedClient self, string key, Expiration expiration, ulong delta = Protocol.MUTATE_DEFAULT_DELTA, ulong defaultValue = Protocol.MUTATE_DEFAULT_VALUE, ulong cas = Protocol.NO_CAS)
		{
			return self.MutateAsync(MutationMode.Decrement, key, expiration, delta, defaultValue, cas);
		}

		public static IMutateOperationResult Decrement(this IMemcachedClient self, string key, ulong delta = Protocol.MUTATE_DEFAULT_DELTA, ulong defaultValue = Protocol.MUTATE_DEFAULT_VALUE, ulong cas = Protocol.NO_CAS)
		{
			return self.Mutate(MutationMode.Decrement, key, Expiration.Never, delta, defaultValue, cas);
		}

		public static IMutateOperationResult Decrement(this IMemcachedClient self, string key, Expiration expiration, ulong delta = Protocol.MUTATE_DEFAULT_DELTA, ulong defaultValue = Protocol.MUTATE_DEFAULT_VALUE, ulong cas = Protocol.NO_CAS)
		{
			return self.Mutate(MutationMode.Decrement, key, expiration, delta, defaultValue, cas);
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
