﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Enyim.Caching.Memcached;
using Xunit;

namespace Enyim.Caching.Tests
{
	public partial class SimpleMemcachedClientTests
	{
		[Fact]
		public async void When_Getting_The_Stats_It_Has_The_Default_Items()
		{
			long l;
			var keys = new[] { "pid", "time", "uptime", "get_hits", "get_misses" };

			var stats = await client.StatsAsync(null);
			Assert.NotNull(stats);

			foreach (var k in keys)
			{
				foreach (var server in stats.Endpoints)
				{
					var value = stats.GetRaw(server, k);
					Assert.True(Int64.TryParse(value, out l), $"value of '{k}' is not a number, but '{value}'");
				}
			}
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
