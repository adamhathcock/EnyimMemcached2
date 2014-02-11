﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Enyim.Caching.Memcached.Operations;

namespace Enyim.Caching.Memcached
{
	public class MemcachedOperationFactory : IOperationFactory
	{
		public IGetOperation Get(byte[] key)
		{
			return new GetOperation(key) { Silent = true };
		}

		public IStoreOperation Store(StoreMode mode, byte[] key, CacheItem value, ulong cas, uint expires)
		{
			return new StoreOperation(mode, key, value, expires) { Cas = cas, Silent = true };
		}

		public IDeleteOperation Delete(byte[] key, ulong cas)
		{
			return new DeleteOperation(key) { Cas = cas, Silent = true };
		}

		public IMutateOperation Mutate(MutationMode mode, byte[] key, ulong defaultValue, ulong delta, ulong cas, uint expires)
		{
			return new MutateOperation(mode, key, defaultValue, delta, expires) { Cas = cas };
		}

		public IConcatOperation Concat(ConcatenationMode mode, byte[] key, ulong cas, ArraySegment<byte> data)
		{
			return new ConcatOperation(mode, key, data) { Cas = cas, Silent = true };
		}

		public IFlushOperation Flush()
		{
			return new FlushOperation();
		}

		public IStatsOperation Stats(string type)
		{
			return new StatsOperation(type);
		}
	}
}
