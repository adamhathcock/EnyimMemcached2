#if NET451
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Enyim.Caching.Configuration
{
	public static class ConfigurationHelper
	{
		public static bool TryGetAndRemove(IDictionary<string, string> dict, string name, out int value, bool required)
		{
			string tmp;
			if (TryGetAndRemove(dict, name, out tmp, required)
				&& Int32.TryParse(tmp, out value))
			{
				return true;
			}

			if (required)
				throw new System.Configuration.ConfigurationErrorsException("Missing or invalid parameter: " + (String.IsNullOrEmpty(name) ? "element content" : name));

			value = 0;

			return false;
		}

		public static TimeSpan GetAndRemove(IDictionary<string, string> dict, string name, bool required, TimeSpan @default)
		{
			TimeSpan tmp;

			return TryGetAndRemove(dict, name, out tmp, required) ? tmp : @default;
		}

		public static int GetAndRemove(IDictionary<string, string> dict, string name, bool required, int @default)
		{
			int tmp;

			return TryGetAndRemove(dict, name, out tmp, required) ? tmp : @default;
		}

		public static bool TryGetAndRemove(IDictionary<string, string> dict, string name, out TimeSpan value, bool required)
		{
			string tmp;
			if (TryGetAndRemove(dict, name, out tmp, required)
				&& TimeSpan.TryParse(tmp, out value))
			{
				return true;
			}

			if (required)
				throw new System.Configuration.ConfigurationErrorsException("Missing or invalid parameter: " + (String.IsNullOrEmpty(name) ? "element content" : name));

			value = TimeSpan.Zero;

			return false;
		}

		public static bool TryGetAndRemove(IDictionary<string, string> dict, string name, out string value, bool required)
		{
			if (dict.TryGetValue(name, out value))
			{
				dict.Remove(name);

				if (!String.IsNullOrEmpty(value))
					return true;
			}

			if (required)
				throw new System.Configuration.ConfigurationErrorsException("Missing parameter: " + (String.IsNullOrEmpty(name) ? "element content" : name));

			return false;
		}

		public static void CheckForUnknownAttributes(IDictionary<string, string> dict)
		{
			if (dict.Count > 0)
				throw new System.Configuration.ConfigurationErrorsException("Unrecognized parameter: " + dict.Keys.First());
		}

		public static void CheckForInterface(Type type, Type interfaceType)
		{
			if (type == null || interfaceType == null) return;

			if (Array.IndexOf<Type>(type.GetInterfaces(), interfaceType) == -1)
				throw new System.Configuration.ConfigurationErrorsException("The type " + type.AssemblyQualifiedName + " must implement " + interfaceType.AssemblyQualifiedName);
		}

		public static IPEndPoint ParseEndPoint(string value, int defaultPort = 0)
		{
			if (String.IsNullOrEmpty(value)) throw new ArgumentNullException("value");

			var index = value.LastIndexOf(':');
			if (index == -1 && defaultPort == 0) throw new ArgumentException("host:port is expected", "value");

			var addressPart = index == -1 ? value : value.Remove(index);
			int port;

			if (index == -1)
			{
				port = defaultPort;
			}
			else
			{
				var portPart = value.Substring(index + 1);
				if (!Int32.TryParse(portPart, out port))
					throw new ArgumentException("Cannot parse " + value, "value");
			}

			return ResolveToEndPoint(addressPart, port);
		}

		public static IPEndPoint ResolveToEndPoint(string host, int port)
		{
			if (String.IsNullOrEmpty(host)) throw new ArgumentNullException("host");
			if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException("port");

			IPAddress address;

			// parse as an IP address
			if (!IPAddress.TryParse(host, out address))
			{
				// not an ip, resolve from dns
				// TODO we need to find a way to specify whihc ip should be used when the host has several
				var entry = Dns.GetHostEntry(host);
				address = entry.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork); // TODO ipv6

				if (address == null)
					throw new ArgumentException(String.Format("Could not resolve host '{0}'.", host));
			}

			return new IPEndPoint(address, port);
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
