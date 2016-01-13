using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Enyim.Caching
{
	public abstract class ClusterBase : ICluster
	{
		private static readonly ILog log = LogManager.GetLogger<ClusterBase>();
        private static readonly bool LogTraceEnabled = log.IsTraceEnabled;
		private static readonly bool LogDebugEnabled = log.IsDebugEnabled;
		private static readonly bool LogInfoEnabled = log.IsInfoEnabled;

		private static readonly TaskCompletionSource<IOperation> failSingle;
		private static readonly TaskCompletionSource<IOperation[]> failBroadcast;

		private readonly IPEndPoint[] endpoints;
		private readonly INodeLocator locator;
		private readonly IReconnectPolicy reconnectPolicy;
		private readonly object ReconnectLock;

		private readonly CancellationTokenSource shutdownToken;

		private readonly Thread worker;
		private readonly ManualResetEventSlim workerIsDone;

		private INode[] allNodes; // all nodes in the cluster known by us
		private INode[] workingNodes; // the nodes that are still working
		private NodeQueue ioQueue; // the nodes that has IO pending

		protected ClusterBase(IEnumerable<IPEndPoint> endpoints, INodeLocator locator, IReconnectPolicy reconnectPolicy)
		{
			this.endpoints = endpoints.ToArray();
			this.locator = locator;
			this.reconnectPolicy = reconnectPolicy;
			this.ReconnectLock = new Object();

			this.worker = new Thread(Worker) { Name = "IO Thread {" + String.Join(", ", endpoints.Select(ep => ep.ToString())) + "}" };

			this.shutdownToken = new CancellationTokenSource();
			this.workerIsDone = new ManualResetEventSlim(false);
		}

		static ClusterBase()
		{
			var allDead = new IOException("All nodes are dead.");

			// cached tasks for error reporting
			failSingle = new TaskCompletionSource<IOperation>();
			failBroadcast = new TaskCompletionSource<IOperation[]>();
			failSingle.SetException(allDead);
			failBroadcast.SetException(allDead);
		}

		protected abstract INode CreateNode(IPEndPoint endpoint);

		public virtual void Start()
		{
			allNodes = endpoints.Select(CreateNode).ToArray();
			ioQueue = new NodeQueue(allNodes);
			workingNodes = allNodes.ToArray();
			locator.Initialize(allNodes);

			worker.Start();
		}

		public virtual void Dispose()
		{
			// if the cluster is not stopped yet, we should clean up
			if (shutdownToken != null && !shutdownToken.IsCancellationRequested)
			{
				shutdownToken.Cancel();
				using (workerIsDone) workerIsDone.Wait();

				foreach (var node in allNodes)
				{
					try { node.Shutdown(); }
					catch (Exception e)
					{
						if (log.IsErrorEnabled)
							log.Error("Error while shutting down " + node, e);
					}
				}

				shutdownToken.Dispose();
			}

			SocketAsyncEventArgsFactory.Instance.Compact();
		}

		public virtual Task<IOperation> Execute(IItemOperation op)
		{
			var node = locator.Locate(op.Key);

			if (!node.IsAlive)
				return failSingle.Task;

			var retval = node.Enqueue(op);
			ioQueue.Add(node);

			return retval;
		}

		public virtual Task<IOperation[]> Broadcast(Func<INode, IOperation> createOp)
		{
			// create local "copy" of the reference, as
			// workingNodes is never changed but replaced
			var nodes = workingNodes;
			var tasks = new List<Task<IOperation>>(nodes.Length);

			foreach (var node in nodes)
			{
				if (node.IsAlive)
				{
					tasks.Add(node.Enqueue(createOp(node)));
					ioQueue.Add(node);
				}
			}

			if (tasks.Count == 0)
				return failBroadcast.Task;

			return Task.WhenAll(tasks);
		}

		/// <summary>
		/// Put the node into the pending work queue.
		/// </summary>
		/// <param name="node"></param>
		public void NeedsIO(INode node)
		{
			if (LogTraceEnabled) log.Trace("Node {0} has pending IO, requeueing", node);
			ioQueue.Add(node);
		}

		private void Worker()
		{
			while (!shutdownToken.IsCancellationRequested)
			{
				try
				{
					var node = ioQueue.Take(shutdownToken.Token);

					try
					{
						node.Run();
						if (shutdownToken.IsCancellationRequested) break;
					}
					catch (Exception e)
					{
						FailNode(node, e);
					}
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}

			if (LogDebugEnabled) log.Debug("shutdownToken was cancelled, finishing work");

			workerIsDone.Set();
		}

		/// <summary>
		/// Marks a node as failed.
		/// </summary>
		/// <param name="node">The failed node</param>
		/// <param name="e">The reason of the failure</param>
		/// <remarks>Only called from the IO thread.</remarks>
		private void FailNode(INode node, Exception e)
		{
			if (log.IsWarnEnabled) log.Warn("Node {0} failed", node.EndPoint);

			// serialize the reconnect attempts to make
			// IReconnectPolicy and INodeLocator implementations simpler
			lock (ReconnectLock)
			{
				var original = Volatile.Read(ref workingNodes);

				// even though we're locking we still do the CAS,
				// because the IO thread does not use any locking
				while (true)
				{
					var updated = original.Where(n => n != node).ToArray();
					var previous = Interlocked.CompareExchange(ref workingNodes, updated, original);

					if (ReferenceEquals(original, previous))
					{
						locator.Initialize(updated);
						break;
					}

					original = previous;
				}
			}

			ScheduleReconnect(node);
		}

		/// <summary>
		/// Schedules a failed node for reconnection.
		/// </summary>
		/// <param name="node"></param>
		protected virtual void ScheduleReconnect(INode node)
		{
			if (LogInfoEnabled) log.Info("Scheduling reconnect for " + node.EndPoint);

			var when = reconnectPolicy.Schedule(node);

			if (when == TimeSpan.Zero)
			{
				if (LogInfoEnabled) log.Info("Will reconnect now");
				ReconnectNow(node);
			}
			else
			{
				if (LogInfoEnabled) log.Info("Will reconnect after " + when);
				Task
					.Delay(when, shutdownToken.Token)
					.ContinueWith(_ => ReconnectNow(node), TaskContinuationOptions.OnlyOnRanToCompletion);
			}
		}

		protected virtual void ReconnectNow(INode node)
		{
			try
			{
				if (shutdownToken.IsCancellationRequested) return;

				node.Connect(shutdownToken.Token);

				ReAddNode(node);
				ioQueue.Add(node); // trigger IO on this node
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled)
					log.Error("Failed to reconnect", e);

				ScheduleReconnect(node);
			}
		}

		/// <summary>
		/// Mark the specified node as working.
		/// </summary>
		/// <param name="node"></param>
		/// <remarks>Can be called from a background thread (Task pool)</remarks>
		private void ReAddNode(INode node)
		{
			if (LogDebugEnabled) log.Debug("Node {0} was reconnected", node.EndPoint);

			// serialize the reconnect attempts to make
			// IReconnectPolicy and INodeLocator implementations simpler
			lock (ReconnectLock)
			{
				reconnectPolicy.Reset(node);

				var original = Volatile.Read(ref workingNodes);

				// even though we're locking we still do the CAS,
				// because the IO thread does not use any locking
				while (true)
				{
					// append the new node to the list of working nodes
					var updated = new INode[original.Length + 1];
					Array.Copy(original, 0, updated, 0, original.Length);
					updated[original.Length] = node;

					var previous = Interlocked.CompareExchange(ref workingNodes, updated, original);
					if (ReferenceEquals(original, previous))
					{
						locator.Initialize(updated);
						break;
					}

					original = previous;
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
