#if NET451
using System;
using System.Configuration;

namespace Enyim.Caching.Configuration
{
	public sealed class NodeElement : ConfigurationElement
	{
		private System.Net.IPEndPoint endpoint;

		/// <summary>
		/// Gets or sets the ip address of the node.
		/// </summary>
		[ConfigurationProperty("address", IsRequired = true, IsKey = true, DefaultValue = "")]
		[ConfigurationValidator(typeof(AddressValidator))]
		public string Address
		{
			get { return (string)base["address"]; }
			set { base["address"] = value; }
		}

		/// <summary>
		/// Gets the <see cref="T:IPEndPoint"/> representation of this instance.
		/// </summary>
		public System.Net.IPEndPoint EndPoint
		{
			get { return this.endpoint ?? (this.endpoint = ConfigurationHelper.ParseEndPoint(this.Address)); }
		}

		#region [ AddressValidator             ]

		private class AddressValidator : ConfigurationValidatorBase
		{
			public override bool CanValidate(Type type)
			{
				return (type == typeof(string)) || base.CanValidate(type);
			}

			public override void Validate(object value)
			{
				var address = Convert.ToString(value);

				if (!String.IsNullOrEmpty(address) && address.LastIndexOf(':') < 1)
					throw new ConfigurationErrorsException("Invalid address specified: " + value);
			}
		}

		#endregion
	}
}
#endif

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
