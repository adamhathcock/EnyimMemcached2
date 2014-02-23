﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Enyim.Caching
{
	/// <summary>
	///  A queue which allows to insert items to the beginning, and also supports efficient enqueuing of other AdvQueues
	/// </summary>
	/// <typeparam name="T"></typeparam>
	internal class AdvQueue<T> : IEnumerable<T>
	{
		private const int GrowthFactor = 2;
		private const int DefaultCapacity = 4;

		private T[] data;
		private int head; // points to the first item to be removed
		private int tail; // points to the place where new items will be added
		private int count;
		private int version;

		public AdvQueue()
		{
			this.data = new T[DefaultCapacity];
		}

		public AdvQueue(int capacity)
		{
			if (capacity < 0)
				throw new ArgumentOutOfRangeException();

			this.data = new T[capacity < DefaultCapacity ? DefaultCapacity : capacity];
		}

		public AdvQueue(IEnumerable<T> source)
		{
			this.data = new T[DefaultCapacity];

			foreach (var item in source)
				Enqueue(item);
		}

		public int Count { get { return count; } }

		public void Enqueue(T item)
		{
			if (count == data.Length) Grow();

			data[tail] = item;
			tail = (tail + 1) % data.Length; // move tail (w/ wrap-around)
			count++;
			version++;
		}

		public void Enqueue(AdvQueue<T> other)
		{
			var otherCount = other.count;
			if (otherCount < 1) return;

			// check if we have enough space
			if (data.Length - count < otherCount) Grow(minimum: count + otherCount);

			// these are easier to read than "other.xxx"
			var otherData = other.data;
			var otherHead = other.head;
			var otherTail = other.tail;

			if (count == 0 && head > 0)
			{
				head = tail = 0;
			}
			else if (head < tail && data.Length - tail < otherCount)
			{
				// we do not have enough continous space after tail, so move eveything back a little
				// this only happens when head is in the middle of the array and the data is continous
				var diff = otherCount - (data.Length - tail);
				Array.Copy(data, head, data, head - diff, count);
				head -= diff;
				tail -= diff;
			}

			if (otherHead < otherTail)
			{
				// the source queue is continous
				Array.Copy(otherData, otherHead, this.data, this.tail, otherCount);
			}
			else
			{
				// the source queue is wrapping around
				Array.Copy(otherData, otherHead, this.data, tail, otherData.Length - otherHead);
				Array.Copy(otherData, 0, this.data, otherData.Length - otherHead + tail, otherTail);
			}

			tail = (tail + other.count) % data.Length;
			count += other.count;
			other.Clear();

			version++;
		}

		public void Insert(T item)
		{
			// grow the array if needed but shift the items by 1 so that we put
			// the new item at 0
			if (count == data.Length) Grow(shift: 1);

			// move head back by 1
			head = (data.Length + head - 1) % data.Length;
			data[head] = item;
			count++;
			version++;
		}

		public T Peek()
		{
			if (count == 0) throw new InvalidOperationException("Empty queue");

			return this.data[this.head];
		}

		public T Dequeue()
		{
			if (this.count == 0) throw new InvalidOperationException("Empty queue");

			var retval = data[head];
			data[head] = default(T); // drop reference

			head = (head + 1) % data.Length; // move head (w/ wrap-around)
			count--;
			version++;

			return retval;
		}

		public void Clear()
		{
			if (count == 0) return;

			// same as Grow
			if (head < tail)
			{
				Array.Clear(data, head, count);
			}
			else
			{
				Array.Clear(data, head, data.Length - head);
				Array.Clear(data, 0, tail);
			}

			head = tail = count = 0;
			version++;
		}

		private void Grow(int shift = 0, int minimum = 0)
		{
			var capacity = (int)(data.Length * GrowthFactor);
			if (capacity < minimum) capacity = minimum;

			var newData = new T[capacity];

			if (count > 0)
			{
				if (head < tail)
				{
					// the current queue is continous
					// ....****....
					//     ^  ^
					//     H  T
					Array.Copy(data, head, newData, shift, count);
				}
				else
				{
					// the current queue is wrapping around
					// ****....****
					//    ^    ^
					//    T    H
					Array.Copy(data, head, newData, shift, data.Length - head);
					Array.Copy(data, 0, newData, shift + data.Length - head, tail);
				}
			}

			Debug.Assert(capacity > count);
			data = newData;
			head = shift;
			tail = count + shift;
			version++;
		}

		#region [ Enumeration                  ]

		public IEnumerator<T> GetEnumerator()
		{
			return Enumerate().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return Enumerate().GetEnumerator();
		}

		private IEnumerable<T> Enumerate()
		{
			var initialVersion = version;

			for (var i = 0; i < count; i++)
			{
				if (initialVersion != version)
					throw new InvalidOperationException("Queue has changed during enumeration.");

				yield return data[(head + i) % data.Length];
			}
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
