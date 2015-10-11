﻿using System;
using System.Collections.Generic;
using System.Linq;
using Enyim.Caching.Memcached;
using Xunit;

namespace Enyim.Caching.Tests
{
	public partial class MemcachedClientTests
	{
		[Fact]
		public async void When_Removing_A_Valid_Key_Result_Is_Successful()
		{
			var key = GetUniqueKey("Remove_Valid");

			ShouldPass(await Store(key: key));
			ShouldPass(await client.RemoveAsync(key, Protocol.NO_CAS), checkCas: false);
			ShouldFail(await client.GetAsync<object>(key, Protocol.NO_CAS));
		}

		[Fact]
		public async void When_Removing_An_Invalid_Key_Result_Is_Not_Successful()
		{
			var key = GetUniqueKey("Remove_Invalid");

			ShouldFail(await client.GetAsync<object>(key, Protocol.NO_CAS)); // sanity-check
			ShouldFail(await client.RemoveAsync(key, Protocol.NO_CAS));
		}
	}
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @author Attila Kiskó <a@enyim.com>
 *    @copyright 2012 Couchbase, Inc.
 *    @copyright 2014 Attila Kiskó, enyim.com
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
