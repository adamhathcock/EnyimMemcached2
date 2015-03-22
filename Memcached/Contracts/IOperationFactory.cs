﻿using System;

namespace Enyim.Caching.Memcached
{
	public interface IOperationFactory
	{
		IGetOperation Get(Key key);
		IGetAndTouchOperation GetAndTouch(Key key, uint expires);

		IStoreOperation Store(StoreMode mode, Key key, CacheItem value, ulong cas, uint expires);
		IDeleteOperation Delete(Key key, ulong cas);
		IMutateOperation Mutate(MutationMode mode, Key key, ulong defaultValue, ulong delta, ulong cas, uint expires);
		ITouchOperation Touch(Key key, uint expires);
		IConcatOperation Concat(ConcatenationMode mode, Key key, ulong cas, ArraySegment<byte> data);

		IStatsOperation Stats(string type);
		IFlushOperation Flush();
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
