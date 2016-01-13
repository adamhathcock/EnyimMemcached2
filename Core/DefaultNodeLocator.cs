using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Enyim.Caching
{
	public class DefaultNodeLocator : INodeLocator
	{
		private const int ServerAddressMutations = 160;
        //TODO: check this
	    private static readonly Encoding NoPreambleUtf8 = Encoding.UTF8;

		private INode[] nodes;
		private uint[] keyRing;
		private int keyRingLengthComplement;
		private Dictionary<uint, INode> keyToServer;

		public void Initialize(IEnumerable<INode> currentNodes)
		{
			// quit if we've been initialized because we can handle dead nodes
			if (keyRing != null) return;

			nodes = currentNodes.ToArray();
			keyRing = new uint[this.nodes.Length * ServerAddressMutations];
			keyToServer = new Dictionary<uint, INode>(keyRing.Length);
			keyRingLengthComplement = ~keyRing.Length;

			var i = 0;

			foreach (var node in nodes)
			{
				for (var mutation = 0; mutation < ServerAddressMutations; mutation++)
				{
					var address = node.EndPoint.ToString();
					var hash = GetKeyHash(address + "-" + mutation);

					keyRing[i++] = hash;
					keyToServer[hash] = node;
				}
			}

			Array.Sort<uint>(keyRing);
		}

		private static uint GetKeyHash(string key)
		{
			return Murmur32.ComputeHash(NoPreambleUtf8.GetBytes(key));
		}

		private static uint GetKeyHash(byte[] key, int count)
		{
			return Murmur32.ComputeHash(key, 0, count);
		}

		public INode Locate(Key key)
		{
			var keyArray = key.Array;
			if (keyArray == null) throw new ArgumentNullException("key");

			switch (nodes.Length)
			{
				case 0: return AlreadyFailedNode.Instance;
				case 1: return nodes[0];
				default:

					var retval = LocateNode(GetKeyHash(keyArray, key.Length));

					// if the result is not alive then try to mutate the item key and find another node
					// this way we do not have to reinitialize every time a node dies/comes back
					// (DefaultServerPool will resurrect the nodes in the background without affecting the hashring)
					//
					// Key mutation logic is taken from spymemcached (https://code.google.com/p/spymemcached/)
					if (!retval.IsAlive)
					{
						var alteredKey = new byte[key.Length + 1];
						Buffer.BlockCopy(keyArray, 0, alteredKey, 1, key.Length);

						for (var i = (byte)'0'; i < (byte)'7'; i++)
						{
							// -- this is from spymemcached
							alteredKey[0] = i;
							var tmpKey = (ulong)GetKeyHash(alteredKey, alteredKey.Length);
							tmpKey += (uint)(tmpKey ^ (tmpKey >> 32));
							tmpKey &= 0xffffffffL; /* truncate to 32-bits */
							retval = LocateNode((uint)tmpKey);
							// -- end

							if (retval.IsAlive) return retval;
						}
					}

					return retval;
			}
		}

		private INode LocateNode(uint itemKeyHash)
		{
			// get the index of the server assigned to this hash
			var foundIndex = Array.BinarySearch<uint>(keyRing, itemKeyHash);

			if (foundIndex == keyRingLengthComplement) foundIndex = 0;
			else if (foundIndex == ~0) foundIndex = keyRing.Length - 1;
			else if (foundIndex < 0) foundIndex = ~foundIndex;

			return keyToServer[keyRing[foundIndex]];
		}

		#region [ AlreadyFailedNode            ]

		private class AlreadyFailedNode : INode
		{
			public static readonly INode Instance;
			private static readonly TaskCompletionSource<IOperation> FailedTask;

			static AlreadyFailedNode()
			{
				FailedTask = new TaskCompletionSource<IOperation>();
				FailedTask.SetException(new IOException("AlwaysDead"));
				Instance = new AlreadyFailedNode();
			}

			public override string ToString()
			{
				return "FailedNode";
			}

			public bool IsAlive { get { return false; } }
			public System.Net.IPEndPoint EndPoint { get { throw new InvalidOperationException("node failed"); } }

			public void Run() { }
			public Task<IOperation> Enqueue(IOperation op) { return FailedTask.Task; }

			public void Connect(CancellationToken token) { }
			public void Shutdown() { }
		}

		#endregion
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
