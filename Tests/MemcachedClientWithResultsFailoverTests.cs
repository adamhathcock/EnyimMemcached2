﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Configuration;
using Enyim.Caching.Memcached.Results;
using Xunit;

namespace Enyim.Caching.Tests
{
	public partial class MemcachedClientWithResultsFailoverTests : MemcachedClientTestBase, IDisposable
	{
		private IContainer config;
		private IMemcachedClientWithResults client;

		public MemcachedClientWithResultsFailoverTests()
			: base("MemcachedClientWithResultsFailoverTests")
		{
			// note: we're intentionally adding a dead server
			new ClusterBuilder("MemcachedClientWithResultsFailoverTests")
					.Endpoints("localhost:11300")
					.SocketOpts(connectionTimeout: TimeSpan.FromMilliseconds(100))
					.Use
						.ReconnectPolicy(() => new PeriodicReconnectPolicy { Interval = TimeSpan.FromHours(1) })
					.Register();

			config = new ClientConfigurationBuilder().Cluster("MemcachedClientWithResultsFailoverTests").Create();
			client = new MemcachedClientWithResults(config);
		}

		public void Dispose()
		{
			config.Dispose();
			ClusterManager.Shutdown("MemcachedClientWithResultsFailoverTests");
		}

		[Fact]
		public void Store_Should_Fail_With_IOException_When_Target_Node_Is_Offline()
		{
			IfThrows<IOException>(client.Store(StoreMode.Set, GetUniqueKey(), "store"));
		}

		[Fact]
		public void Remove_Should_Fail_With_IOException_When_Target_Node_Is_Offline()
		{
			IfThrows<IOException>(client.Remove(GetUniqueKey()));
		}

		[Fact]
		public void Concate_Should_Fail_With_IOException_When_Target_Node_Is_Offline()
		{
			IfThrows<IOException>(client.Concate(ConcatenationMode.Append, GetUniqueKey(), new ArraySegment<byte>(new byte[] { 1 }), 0));
		}

		[Fact]
		public void Mutate_Should_Fail_With_IOException_When_Target_Node_Is_Offline()
		{
			IfThrows<IOException>(client.Mutate(MutationMode.Increment, GetUniqueKey(), 100, 100, 0, DateTime.Now.AddDays(1)));
		}

		[Fact]
		public void Storing_Unserializable_Object_Should_Throw_SerializationException()
		{
			Assert.Throws<SerializationException>(() => client.Store(StoreMode.Set, GetUniqueKey(), new Unserializable()));
		}

		[Fact]
		public async Task Storing_Unserializable_Object_Asynchronously_Should_Throw_SerializationException()
		{
			await IfThrowsAsync<SerializationException>(client.StoreAsync(StoreMode.Set, GetUniqueKey(), new Unserializable()));
		}

		[Fact]
		public async void StoreAsync_Should_Fail_With_IOException_When_Target_Node_Is_Offline()
		{
			var result = await client.StoreAsync(StoreMode.Set, GetUniqueKey(), "store");
			IfThrows<IOException>(result);
		}

		[Fact]
		public async void RemoveAsync_Should_Fail_With_IOException_When_Target_Node_Is_Offline()
		{
			var result = await client.RemoveAsync(GetUniqueKey());
			IfThrows<IOException>(result);
		}

		[Fact]
		public async void ConcateAsync_Should_Fail_With_IOException_When_Target_Node_Is_Offline()
		{
			var result = await client.ConcateAsync(ConcatenationMode.Append, GetUniqueKey(), new ArraySegment<byte>(new byte[] { 1 }), 0);
			IfThrows<IOException>(result);
		}

		[Fact]
		public async void MutateAsync_Should_Fail_With_IOException_When_Target_Node_Is_Offline()
		{
			var result = await client.MutateAsync(MutationMode.Increment, GetUniqueKey(), 100, 100, 0, DateTime.Now.AddDays(1));
			IfThrows<IOException>(result);
		}
	}

	class Unserializable { }
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
