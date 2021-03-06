﻿using System;
using System.Collections.Generic;
using System.Linq;
using Enyim.Caching.Memcached.Operations;

namespace Enyim.Caching.Memcached
{
	public class MemcachedOperationFactory : IOperationFactory
	{
		private readonly IBufferAllocator allocator;

		public MemcachedOperationFactory(IBufferAllocator allocator)
		{
			this.allocator = allocator;
		}

		public IGetOperation Get(Key key, ulong cas)
		{
			return new GetOperation(allocator, key) { Cas = cas };
		}

		public IGetAndTouchOperation GetAndTouch(Key key, uint expires, ulong cas)
		{
			return new GetAndTouchOperation(allocator, key)
			{
				Expires = expires,
				Cas = cas
			};
		}

		public IStoreOperation Store(StoreMode mode, Key key, CacheItem value, uint expires, ulong cas)
		{
			return new StoreOperation(allocator, mode, key, value)
			{
				Cas = cas,
				Expires = expires
			};
		}

		public IDeleteOperation Delete(Key key, ulong cas)
		{
			return new DeleteOperation(allocator, key)
			{
				Cas = cas
			};
		}

		public IMutateOperation Mutate(MutationMode mode, Key key, uint expires, ulong delta, ulong defaultValue, ulong cas)
		{
			return new MutateOperation(allocator, mode, key)
			{
				DefaultValue = defaultValue,
				Delta = delta,
				Cas = cas,
				Expires = expires
			};

		}

		public ITouchOperation Touch(Key key, uint expires, ulong cas)
		{
			return new TouchOperation(allocator, key)
			{
				Expires = expires,
				Cas = cas
			};
		}

		public IConcatOperation Concat(ConcatenationMode mode, Key key, ArraySegment<byte> data, ulong cas)
		{
			return new ConcatOperation(allocator, mode, key)
			{
				Cas = cas,
				Data = data
			};
		}

		public IFlushOperation Flush()
		{
			return new FlushOperation(allocator);
		}

		public IStatsOperation Stats(string type)
		{
			return new StatsOperation(allocator, type);
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
