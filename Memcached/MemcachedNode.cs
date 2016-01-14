using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Enyim.Caching.Memcached.Operations;

namespace Enyim.Caching.Memcached
{
	public class MemcachedNode : NodeBase
	{
		private const int SilentCountThreshold = 50;
		private static readonly ILog log = LogManager.GetLogger<MemcachedNode>();

		private readonly IBufferAllocator allocator;
		private int silentCount;
		private bool lasWasSilent = false;

		public MemcachedNode(IBufferAllocator allocator, ICluster owner, IPEndPoint endpoint, IFailurePolicy failurePolicy, ISocket socket)
			: base(owner, endpoint, failurePolicy, socket)
		{
			this.allocator = allocator;
		}

		public override void Connect(CancellationToken token)
		{
			silentCount = 0;

			base.Connect(token);
		}

		protected override IResponse CreateResponse()
		{
			return new BinaryResponse(allocator);
		}

		public override Task<IOperation> Enqueue(IOperation op)
		{
			EnqueueNoOpIfNeeded(op);

			return base.Enqueue(op);
		}

		/// <summary>
		///  Add a NoOp after every {SilentCountThreshold}th continous silent op
		/// </summary>
		/// <param name="op"></param>
		private void EnqueueNoOpIfNeeded(IOperation op)
		{
			if (IsSilent(op))
			{
				if (log.IsTraceEnabled) log.Trace("Got a silent op " + op + " count: " + silentCount);

				if (++silentCount < SilentCountThreshold)
					return;

				if (log.IsTraceEnabled) log.Trace("Got to threshold, injecting NoOp");

				base.Enqueue(new NoOp(allocator));
			}

			silentCount = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsSilent(IOperation op)
		{
			var silent = op as ICanBeSilent;

			return silent != null && silent.Silent;
		}

		protected override void WriteOp(Data data)
		{
			// remember of the last op was silent so that when we
			// run out of ops we'll know if we have to emit an additional NoOp
			// ***SSSSS<EOF>
			// * = normal
			// S = Silent
			// noop should be at <EOF> otherwise we won't get responses to the last
			// command until we get a new op queued up
			lasWasSilent = IsSilent(data.Op);

			base.WriteOp(data);
		}

		protected override Data GetNextOp()
		{
			var data = base.GetNextOp();

			// we've temporarily ran out of commands
			if (data.IsEmpty && lasWasSilent)
				return new Data { Op = new NoOp(allocator) };

			return data;
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
