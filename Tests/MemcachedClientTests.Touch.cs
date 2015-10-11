﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Enyim.Caching.Memcached;
using Xunit;

namespace Enyim.Caching.Tests
{
	public partial class MemcachedClientTests
	{
		public const int DefaultExpiration = SimpleMemcachedClientTests.DefaultExpiration;
		public const int WaitButStillAlive = SimpleMemcachedClientTests.WaitButStillAlive;
		public const int NewExpiration = SimpleMemcachedClientTests.NewExpiration;
		public const int WaitUntilExpires = SimpleMemcachedClientTests.WaitUntilExpires;

		[Fact]
		[Trait("slow", "yes")]
		public async void When_Getting_An_Expired_Item_It_Should_Be_Null()
		{
			var key = GetUniqueKey("Get_Expired");
			var value = GetRandomString();

			Assert.True(DefaultExpiration > WaitButStillAlive);

			ShouldPass(await client.StoreAsync(StoreMode.Set, key, value, TimeSpan.FromMilliseconds(DefaultExpiration), Protocol.NO_CAS), operation: "initial store");
			Thread.Sleep(WaitButStillAlive);
			AreEqual(value, await client.GetAsync<string>(key, Protocol.NO_CAS), operation: "retrieve " + key);

			Thread.Sleep(WaitUntilExpires);
			ShouldFail(await client.GetAsync<string>(key, Protocol.NO_CAS));
		}

		[Fact]
		[Trait("slow", "yes")]
		public async void When_Getting_And_Touching_An_Item_It_Should_Not_Expire()
		{
			var key = GetUniqueKey("Get_And_Touch");
			var value = GetRandomString();

			ShouldPass(await client.StoreAsync(StoreMode.Set, key, value, DateTime.Now.AddMilliseconds(DefaultExpiration), Protocol.NO_CAS));
			Thread.Sleep(WaitButStillAlive);
			AreEqual(value, await client.GetAndTouchAsync<string>(key, DateTime.Now.AddSeconds(SimpleMemcachedClientTests.NewExpiration), Protocol.NO_CAS));

			Thread.Sleep(WaitUntilExpires);
			AreEqual(value, await client.GetAsync<string>(key, Protocol.NO_CAS));
		}

		[Fact]
		[Trait("slow", "yes")]
		public async void When_Touching_An_Item_It_Should_Not_Expire()
		{
			var key = GetUniqueKey("Touch");
			var value = GetRandomString();

			ShouldPass(await client.StoreAsync(StoreMode.Set, key, value, DateTime.Now.AddMilliseconds(DefaultExpiration), Protocol.NO_CAS));
			Thread.Sleep(WaitButStillAlive);
			ShouldPass(await client.TouchAsync(key, DateTime.Now.AddSeconds(SimpleMemcachedClientTests.NewExpiration), Protocol.NO_CAS), checkCas: false);

			Thread.Sleep(WaitUntilExpires);
			AreEqual(value, await client.GetAsync<string>(key, Protocol.NO_CAS));
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
