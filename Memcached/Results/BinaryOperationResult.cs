﻿using System;
using System.Collections.Generic;
using System.Linq;
using Enyim.Caching.Memcached.Operations;

namespace Enyim.Caching.Memcached.Results
{
	public class BinaryOperationResult : IOperationResult
	{
		public bool Success { get; set; }
		public string Message { get; set; }
		public Exception Exception { get; set; }
		public int? StatusCode { get; set; }
		public IOperationResult InnerResult { get; set; }

		public ulong Cas { get; set; }

		public static BinaryOperationResult FromResponse(BinaryResponse response, string failMessage = null)
		{
			var success = response.StatusCode == 0;

			var retval = new BinaryOperationResult
			{
				StatusCode = response.StatusCode,
				Success = success,
				Message = success ? null : response.GetStatusMessage() ?? failMessage,
				Cas = response.CAS
			};

			return retval;
		}
	}

	public class BinaryOperationResult<T> : BinaryOperationResult, IOperationResult<T>
	{
		public T Value { get; set; }
	}
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
 *    @copyright 2012 Attila Kiskó, enyim.com
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
