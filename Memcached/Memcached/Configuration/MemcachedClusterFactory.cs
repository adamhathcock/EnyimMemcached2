﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Enyim.Caching.Configuration;
using Funq;

namespace Enyim.Caching.Memcached.Configuration
{
	public class MemcachedClusterFactory : ClusterFactoryBase
	{
		public MemcachedClusterFactory()
		{
			Container
				.AutoWireAs<INode, MemcachedNode, IPEndPoint>()
				.InitializedBy((c, n) =>
				{
					BufferSize = BufferSize;
				});
		}
	}
}
