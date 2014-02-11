﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Enyim.Caching
{
	public class DefaultCluster : ICluster
	{
		private static readonly ILog log = LogManager.GetCurrentClassLogger();
		private static readonly TaskCompletionSource<IOperation> failSingle;
		private static readonly TaskCompletionSource<KeyValuePair<IOperation, INode>[]> failBroadcast;

		private readonly INodeLocator locator;
		private readonly IReconnectPolicy policy;
		private readonly Func<IPEndPoint, INode> nodeFactory;

		private readonly CancellationTokenSource shutdownToken;

		private readonly Thread worker;
		private readonly ManualResetEventSlim workerIsDone;
		private readonly ManualResetEventSlim hasWork;
		private readonly INode[] allNodes;
		private readonly ConcurrentQueue<INode> reconnectedNodes;

		private INode[] workingNodes;

		public DefaultCluster(IEnumerable<IPEndPoint> endpoints,
								INodeLocator locator,
								IReconnectPolicy policy,
								Func<IPEndPoint, INode> nodeFactory)
		{
			this.allNodes = endpoints.Select(nodeFactory).ToArray();
			this.workingNodes = allNodes.ToArray();

			this.locator = locator;
			this.policy = policy;
			this.nodeFactory = nodeFactory;

			this.worker = new Thread(Worker) { Name = "The Worker" };

			this.shutdownToken = new CancellationTokenSource();
			this.hasWork = new ManualResetEventSlim();
			this.workerIsDone = new ManualResetEventSlim(false);
			this.reconnectedNodes = new ConcurrentQueue<INode>();

			locator.Initialize(allNodes);
		}

		static DefaultCluster()
		{
			failSingle = new TaskCompletionSource<IOperation>();
			failSingle.SetException(new IOException("All nodes are dead."));

			failBroadcast = new TaskCompletionSource<KeyValuePair<IOperation, INode>[]>();
			failBroadcast.SetException(new IOException("All nodes are dead."));
		}

		public virtual void Start()
		{
			//// TODO nodes should connect lazily (the first time they start processing ops)
			//Parallel.ForEach(allNodes, n =>
			//{
			//	try { n.Connect(true, shutdownToken.Token); }
			//	catch (Exception e) { FailNode(n, e); }
			//});

			worker.Start();
		}

		public virtual void Dispose()
		{
			if (!shutdownToken.IsCancellationRequested)
			{
				shutdownToken.Cancel();
				workerIsDone.Wait();

				foreach (var node in allNodes)
				{
					try { node.Shutdown(); }
					catch (Exception e)
					{
						if (log.IsErrorEnabled)
							log.Error("Error while shutting down " + node, e);
					}
				}
			}
		}

		public virtual Task Execute(ISingleKeyOperation op)
		{
			var node = locator.Locate(op.Key);

			if (node.IsAlive)
			{
				var retval = node.Enqueue(op);
				hasWork.Set();

				return retval;
			}

			return failSingle.Task;
		}

		public virtual Task<IOperation[]> Broadcast(Func<INode, IOperation> createOp)
		{
			// create local "copy" of the reference
			// workingNodes is never changed but replaced
			var nodes = workingNodes;
			var tasks = new List<Task<IOperation>>(nodes.Length);

			foreach (var node in nodes)
			{
				if (node.IsAlive)
				{
					var op = createOp(node);
					tasks.Add(node.Enqueue(op));
				}
			}

			if (tasks.Count == 0)
				throw new IOException("All nodes are dead");

			hasWork.Set();
			return Task.WhenAll(tasks);
		}

		private void Worker()
		{
			while (!shutdownToken.IsCancellationRequested)
			{
				hasWork.Reset();

				var pendingIO = false;

				if (RunOnNodes(n => n.Send())) pendingIO = true;
				if (shutdownToken.IsCancellationRequested) break;
				if (RunOnNodes(n => n.Receive())) pendingIO = true;

				if (!pendingIO)
				{
					if (log.IsTraceEnabled) log.Trace("No pending IO, waiting");

					try { hasWork.Wait(shutdownToken.Token); }
					catch (OperationCanceledException) { break; }
				}
			}

			if (log.IsDebugEnabled) log.Debug("shutdownToken was cancelled, finishing work");

			workerIsDone.Set();
		}

		protected virtual bool RunOnNodes(Func<INode, bool> what)
		{
			var pendingIO = false;
			var runOn = workingNodes;

			foreach (var node in runOn)
			{
				try
				{
					if (what(node))
					{
						if (log.IsTraceEnabled) log.Trace(node + " has pending IO.");
						pendingIO = true;
					}
				}
				catch (Exception e)
				{
					FailNode(node, e);
				}
			}

			return pendingIO;
		}

		protected virtual void ScheduleReconnect(INode node)
		{
			if (log.IsInfoEnabled) log.Info("Scheduling reconnect for " + node.EndPoint);

			var when = policy.Schedule(node);
			if (log.IsDebugEnabled) log.Debug("Will reconnect after " + when);

			if (when == TimeSpan.Zero)
			{
				ReconnectNow(node, false);
			}
			else
			{
				Task
					.Delay(when, shutdownToken.Token)
					.ContinueWith(_ => ReconnectNow(node, true),
									TaskContinuationOptions.OnlyOnRanToCompletion);
			}
		}

		protected virtual void ReconnectNow(INode node, bool reset)
		{
			try
			{
				if (shutdownToken.IsCancellationRequested) return;

				node.Connect(reset, shutdownToken.Token);

				ReAddNode(node);
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled)
					log.Error("Failed to reconnect", e);

				ScheduleReconnect(node);
			}
		}

		private void ReAddNode(INode node)
		{
			if (log.IsDebugEnabled) log.Debug("Node {0} was reconnected", node.EndPoint);

			policy.Reset(node);

			var existing = Volatile.Read(ref workingNodes);

			while (true)
			{
				var updated = new INode[existing.Length + 1];
				Array.Copy(existing, 0, updated, 0, existing.Length);
				updated[existing.Length] = node;

				var current = Interlocked.CompareExchange(ref workingNodes, updated, existing);

				if (Object.ReferenceEquals(existing, current))
				{
					locator.Initialize(updated);
					break;
				}

				existing = current;
			}

			locator.Initialize(existing);
			hasWork.Set();
		}

		private void FailNode(INode node, Exception e)
		{
			if (log.IsWarnEnabled) log.Warn("Node {0} failed", node.EndPoint);

			var existing = Volatile.Read(ref workingNodes);

			while (true)
			{
				var updated = existing.Where(n => n != node).ToArray();
				var current = Interlocked.CompareExchange(ref workingNodes, updated, existing);

				if (Object.ReferenceEquals(existing, current))
				{
					locator.Initialize(updated);
					break;
				}

				existing = current;
			}

			ScheduleReconnect(node);
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
