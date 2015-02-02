﻿using System;
using System.Collections.Generic;
using System.Linq;
using Enyim.Caching.Memcached.Results;

namespace Enyim.Caching.Memcached.Operations
{
	public class StoreOperation : BinarySingleItemOperation<IOperationResult>, IStoreOperation
	{
		protected const int ExtraLength = 8;
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(StoreOperation));

		private readonly CacheItem value;

		public StoreOperation(StoreMode mode, byte[] key, CacheItem value, uint expires) :
			base(key)
		{
			Mode = mode;
			Expires = expires;
			this.value = value;
		}

		public StoreMode Mode { get; private set; }
		public uint Expires { get; private set; }
		public bool Silent { get; set; }

		protected override BinaryRequest CreateRequest()
		{
			OpCode op;

			// figure out the op code
			switch (Mode)
			{
				case StoreMode.Add: op = OpCode.Add; break;
				case StoreMode.Replace: op = OpCode.Replace; break;
				case StoreMode.Set: op = OpCode.Set; break;
				default: throw new ArgumentOutOfRangeException("Unknown mode: " + Mode);
			}

			// make it silent
			if (Silent) op = (OpCode)((byte)op | Protocol.SILENT_MASK);

			var request = new BinaryRequest(op, ExtraLength)
			{
				Key = Key,
				Cas = Cas,
				Data = value.Data
			};

			var extra = request.Extra.Array;
			var offset = request.Extra.Offset;

			// store the extra values
			BinaryConverter.EncodeUInt32((uint)value.Flags, extra, offset);
			BinaryConverter.EncodeUInt32(Expires, extra, offset + 4);

			return request;
		}

		protected override IOperationResult CreateResult(BinaryResponse response)
		{
			var retval = new BinaryOperationResult();

			if (response == null)
				return retval.Success(this);

			return retval.WithResponse(response);
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
