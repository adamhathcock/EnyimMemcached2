#if NET451
using System;
using System.ComponentModel;
using System.Configuration;
using System.Linq;

namespace Enyim.Caching.Configuration
{
	/// <summary>
	/// We need our own, so that type names without assembly spec can be parsed.
	/// </summary>
	internal sealed class ProviderTypeNameConverter : ConfigurationConverterBase
	{
		public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
		{
			var name = (string)value;

			try
			{
				return Type.GetType(name, true);
			}
			catch
			{
				var t = typeof(ICluster).Assembly.GetType(name, false);
				if (t == null) throw;

				return t;
			}
		}

		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
		{
			return ((Type)value).AssemblyQualifiedName;
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
