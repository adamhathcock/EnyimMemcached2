#if NET451
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;

namespace Enyim.Caching.Configuration
{
	public sealed class ProviderElement<T> : ConfigurationElement
		where T : class
	{
		public ProviderElement()
		{
			Parameters = new Dictionary<string, string>();
		}

		public Dictionary<string, string> Parameters { get; private set; }

		/// <summary>
		/// Gets or sets the type of the provider.
		/// </summary>
		[ConfigurationProperty("type", IsRequired = true), TypeConverter(typeof(ProviderTypeNameConverter))]
		public Type Type
		{
			get { return (Type)base["type"]; }
			set
			{
				ConfigurationHelper.CheckForInterface(value, typeof(T));
				base["type"] = value;
			}
		}

		protected override bool OnDeserializeUnrecognizedAttribute(string name, string value)
		{
			var property = new ConfigurationProperty(name, typeof(string), value);
			base[property] = value;
			Parameters[name] = value;

			return true;
		}

		[ConfigurationProperty("data", IsRequired = false)]
		public TextElement Content
		{
			get { return (TextElement)base["data"]; }
			set { base["data"] = value; }
		}

		protected override void PostDeserialize()
		{
			base.PostDeserialize();

			var c = this.Content;
			if (c != null && !String.IsNullOrEmpty(c.Content))
				Parameters[String.Empty] = c.Content;
		}
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
