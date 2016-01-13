using System;
using System.Runtime.CompilerServices;

namespace Enyim.Caching
{
	public static class LogManager
	{
		private static ILogFactory factory = new NullLoggerFactory();

		public static void AssignFactory(ILogFactory factory)
		{
			LogManager.factory = factory;
		}

		public static ILog GetLogger(string name)
		{
			return factory.GetLogger(name);
		}

		public static ILog GetLogger(Type type)
		{
			return factory.GetLogger(type);
        }

        public static ILog GetLogger<T>()
        {
            return factory.GetLogger(typeof(T));
        }

#if NET451
        [MethodImpl(MethodImplOptions.NoInlining)]
		public static ILog GetCurrentClassLogger()
		{
			var frame = new System.Diagnostics.StackFrame(1, false);

			return GetLogger(frame.GetMethod().DeclaringType.FullName);
		}
#endif

        #region [ NullLoggerFactory            ]

        private class NullLoggerFactory : ILogFactory
		{
			private static readonly ILog Instance = new Ω();

			public ILog GetLogger(string name)
			{
				return Instance;
			}

			public ILog GetLogger(Type type)
			{
				return Instance;
			}

			#region [ Logger                       ]

			private class Ω : ILog
			{
				public bool IsTraceEnabled { get { return false; } }
				public bool IsDebugEnabled { get { return false; } }
				public bool IsInfoEnabled { get { return false; } }
				public bool IsWarnEnabled { get { return false; } }
				public bool IsErrorEnabled { get { return false; } }
				public bool IsFatalEnabled { get { return false; } }

				public void Trace(object message) { }
				public void Trace(string message, Exception exception) { }
				public void Trace(string format, params object[] args) { }
				public void Trace(IFormatProvider provider, string format, params object[] args) { }

				public void Debug(object message) { }
				public void Debug(string message, Exception exception) { }
				public void Debug(string format, params object[] args) { }
				public void Debug(IFormatProvider provider, string format, params object[] args) { }

				public void Info(object message) { }
				public void Info(string message, Exception exception) { }
				public void Info(string format, params object[] args) { }
				public void Info(IFormatProvider provider, string format, params object[] args) { }

				public void Warn(object message) { }
				public void Warn(string message, Exception exception) { }
				public void Warn(string format, params object[] args) { }
				public void Warn(IFormatProvider provider, string format, params object[] args) { }

				public void Error(object message) { }
				public void Error(string message, Exception exception) { }
				public void Error(string format, params object[] args) { }
				public void Error(IFormatProvider provider, string format, params object[] args) { }

				public void Fatal(object message) { }
				public void Fatal(string message, Exception exception) { }
				public void Fatal(string format, params object[] args) { }
				public void Fatal(IFormatProvider provider, string format, params object[] args) { }
			}

			#endregion
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
