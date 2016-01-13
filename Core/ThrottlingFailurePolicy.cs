using System;
using System.Collections.Generic;

namespace Enyim.Caching
{
	/// <summary>
	/// Fails a node when the specified number of failures happen in a specified time window.
	/// </summary>
	public class ThrottlingFailurePolicy : IFailurePolicy
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(ThrottlingFailurePolicy));

		private DateTime lastFailed;
		private int counter;

		/// <summary>
		/// Creates a new instance of <see cref="T:ThrottlingFailurePolicy"/>.
		/// </summary>
		/// <param name="resetAfter">Specifies the time in milliseconds how long a node should function properly to reset its failure counter.</param>
		/// <param name="failureThreshold">Specifies the number of failures that must occur in the specified time window to fail a node.</param>
		public ThrottlingFailurePolicy()
		{
			ResetAfter = TimeSpan.FromSeconds(10);
			Threshold = 2;
		}

		public TimeSpan ResetAfter { get; set; }
		public int Threshold { get; set; }

		public void Reset(INode node)
		{
		}

		public bool ShouldFail(INode node)
		{
			var now = DateTime.UtcNow;

			if (counter == 0)
			{
				if (log.IsDebugEnabled) log.Debug("Never failed before, setting counter to 1.");

				counter = 1;
			}
			else
			{
				var diff = now - lastFailed;
				if (log.IsDebugEnabled) log.Debug("Last fail was {0} ago with counter {1}.", diff, counter);

				counter = diff <= ResetAfter ? (counter + 1) : 1;
			}

			lastFailed = now;

			if (counter == Threshold)
			{
				if (log.IsDebugEnabled) log.Debug("Threshold reached, failing node.");
				counter = 0;

				return true;
			}

			if (log.IsDebugEnabled) log.Debug("Threshold not reached, current value is {0}.", counter);

			return false;
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
