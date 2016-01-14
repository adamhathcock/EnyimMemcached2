using System;
using System.Collections.Generic;
using System.Linq;
using Enyim.Caching.Memcached;

namespace Enyim.Caching.Tests
{
	public class _FailurePolicy : IFailurePolicy
	{
		internal _FailurePolicy(StepRecorder recorder)
		{
			recorder.Mark("FailurePolicy");
		}

		public int Property { get; set; }
		public int Counter { get; set; }

		public void Reset(INode node)
		{
		}

		public bool ShouldFail(INode node)
		{
			return false;
		}
	}

	public class _NodeLocator : INodeLocator
	{
		internal _NodeLocator(StepRecorder recorder)
		{
			recorder.Mark("NodeLocator");
		}

		public string Property { get; set; }

		public void Initialize(IEnumerable<INode> nodes)
		{
		}

		public INode Locate(Key key)
		{
			throw new NotImplementedException();
		}
	}

	public class _ReconnectPolicy : IReconnectPolicy
	{
		internal _ReconnectPolicy(StepRecorder recorder)
		{
			recorder.Mark("ReconnectPolicy");
		}

		public TimeSpan Property { get; set; }

		public TimeSpan Schedule(INode node)
		{
			return TimeSpan.Zero;
		}

		public void Reset(INode node)
		{
		}
	}

	public class _KeyTransformer : IKeyTransformer
	{
		public Key Transform(string key)
		{
			throw new NotImplementedException();
		}
	}

	public class _Transcoder : ITranscoder
	{
		public CacheItem Serialize(object value)
		{
			throw new NotImplementedException();
		}

		public object Deserialize(Type type, CacheItem item)
		{
			throw new NotImplementedException();
		}

		public CacheItem Serialize2(object value)
		{
			throw new NotImplementedException();
		}
	}

	public class _OperationFactory : IOperationFactory
	{
		public IConcatOperation Concat(ConcatenationMode mode, Key key, ArraySegment<byte> data, ulong cas)
		{
			throw new NotImplementedException();
		}

		public IDeleteOperation Delete(Key key, ulong cas)
		{
			throw new NotImplementedException();
		}

		public IFlushOperation Flush()
		{
			throw new NotImplementedException();
		}

		public IGetOperation Get(Key key, ulong cas)
		{
			throw new NotImplementedException();
		}

		public IGetAndTouchOperation GetAndTouch(Key key, uint expires, ulong cas)
		{
			throw new NotImplementedException();
		}

		public IMutateOperation Mutate(MutationMode mode, Key key, uint expires, ulong delta, ulong defaultValue, ulong cas)
		{
			throw new NotImplementedException();
		}

		public IStatsOperation Stats(string type)
		{
			throw new NotImplementedException();
		}

		public IStoreOperation Store(StoreMode mode, Key key, CacheItem value, uint expires, ulong cas)
		{
			throw new NotImplementedException();
		}

		public ITouchOperation Touch(Key key, uint expires, ulong cas)
		{
			throw new NotImplementedException();
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
