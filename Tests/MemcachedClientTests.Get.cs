﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached.Results;
using Xunit;
using Enyim.Caching.Memcached;

namespace Enyim.Caching.Tests
{
	public partial class MemcachedClientTests
	{
		[Fact]
		public void Can_Read_Items_Larger_Than_Receive_Buffer()
		{
			var key = GetUniqueKey("Large_Buffer");
			var value = new byte[32768 * 3 + 4];

			value[0] = 100;
			value[32768] = 100;
			value[value.Length - 1] = 100;

			Assert.True(Store(key: key, value: value));

			var result = client.Get(key) as byte[];
			Assert.NotNull(result);

			Assert.Equal(result.Length, value.Length);
			Assert.Equal(value.AsEnumerable(), result.AsEnumerable());
		}

		[Fact]
		public void When_Getting_Existing_Item_Value_Is_Not_Null_And_Result_Is_Successful()
		{
			var key = GetUniqueKey("Get_Existing");
			var value = GetRandomString();

			Assert.True(Store(key: key, value: value));
			Assert.Equal(value, client.Get(key));
		}

		[Fact]
		public void When_Getting_Item_For_Invalid_Key_HasValue_Is_False_And_Result_Is_Not_Successful()
		{
			var key = GetUniqueKey("Get_Invalid");

			Assert.Null(client.Get<object>(key));
		}

		[Fact]
		public void When_Generic_Getting_Existing_Item_Value_Is_Not_Null_And_Result_Is_Successful()
		{
			var key = GetUniqueKey("Generic_Get");
			var value = GetRandomString();

			Assert.True(Store(key: key, value: value));
			Assert.Equal(value, client.Get<string>(key));
		}

		[Fact]
		public void When_Getting_Multiple_Keys_Result_Is_Successful()
		{
			var data = GetUniqueKeys().ToDictionary(k => "Value for " + k);

			foreach (var kvp in data)
				Assert.True(Store(key: kvp.Key, value: kvp.Value));

			var results = client.Get(data.Keys).OrderBy(kvp => kvp.Key).ToArray();
			var source = data.OrderBy(kvp => kvp.Key).ToArray();

			Assert.Equal(source.Select(kvp => kvp.Key), results.Select(kvp => kvp.Key));
			Assert.Equal(source.Select(kvp => kvp.Value), results.Select(kvp => kvp.Value));
		}

		[Fact]
		public void When_Getting_Byte_Result_Is_Successful()
		{
			const byte expectedValue = 1;
			var key = GetUniqueKey("Get_Byte");

			Assert.True(Store(key: key, value: expectedValue));
			Assert.Equal(expectedValue, client.Get(key));
		}

		[Fact]
		public void When_Getting_SByte_Result_Is_Successful()
		{
			const sbyte expectedValue = 1;
			var key = GetUniqueKey("Get_Sbyte");

			Assert.True(Store(key: key, value: expectedValue));
			Assert.Equal(expectedValue, client.Get(key));
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
