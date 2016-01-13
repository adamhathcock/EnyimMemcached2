using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Enyim.Caching
{
	public static class Metrics
	{
		private static readonly object CacheLock = new Object();
		private static readonly SortedList<string, IMetric> Cache = new SortedList<string, IMetric>();

		private static IMetricFactory Factory = new Internal.DefaultMetricFactory();

		public static void AssignFactory(IMetricFactory factory)
		{
			Factory = factory;
		}

		public static IMetric[] GetAll()
		{
			lock (CacheLock)
			{
				var retval = new IMetric[Cache.Count];
				Cache.Values.CopyTo(retval, 0);

				return retval;
			}
		}

		private static IMetric New(string name, string instance, Func<IMetric> create)
		{
			IMetric retval;
			var key = String.IsNullOrEmpty(instance)
						? name
						: name + "\t" + instance;

			lock (CacheLock)
			{
				if (!Cache.TryGetValue(key, out retval))
				{
					retval = create();
					Cache.Add(key, retval);
				}
			}

			return retval;
		}

		public static ICounter Counter(string name, string instance)
		{
			var parent = (ICounter)New(name, null, () => Factory.Counter(name));

			return (ICounter)New(name, instance, () => Factory.Counter(parent, instance));
		}

		public static IMeter Meter(string name, string instance, Interval interval)
		{
			var parent = (IMeter)New(name, null, () => Factory.Meter(name, interval));

			return (IMeter)New(name, instance, () => Factory.Meter(parent, instance, interval));
		}

		public static IGauge Gauge(string name, string instance)
		{
			var parent = (IGauge)New(name, null, () => Factory.Gauge(name));

			return (IGauge)New(name, instance, () => Factory.Gauge(parent, instance));
		}
	}

	public interface IMetricFactory
	{
		ICounter Counter(string name);
		IMeter Meter(string name, Interval interval);
		IGauge Gauge(string name);

		ICounter Counter(ICounter parent, string instance);
		IMeter Meter(IMeter parent, string instance, Interval interval);
		IGauge Gauge(IGauge parent, string instance);
	}

	public interface IMetric
	{
		string Name { get; }
		string Instance { get; }
		IMetric Parent { get; }
	}

	public interface IGauge : IMetric
	{
		void Set(long value);

		long Value { get; }
		long Min { get; }
		long Max { get; }
	}

	public interface ICounter : IMetric
	{
		void Reset();
		void IncrementBy(int value);
		long Count { get; }
	}

	public static class MetricExtensions
	{
		public static void Increment(this ICounter counter)
		{
			counter.IncrementBy(1);
		}

		public static void Decrement(this ICounter counter)
		{
			counter.IncrementBy(-1);
		}

		public static void DecrementBy(this ICounter counter, int value)
		{
			counter.IncrementBy(-value);
		}

	}

	public interface IMeter : ICounter
	{
		double Rate { get; }
		Interval Interval { get; }
	}

	public enum Interval
	{
		Nanoseconds,
		Microseconds,
		Milliseconds,
		Seconds,
		Minutes,
		Hours,
		Days
	}

	#region [ MetricsVisitor               ]

	internal abstract class MetricsVisitor
	{
		public virtual void Visit(IEnumerable<IMetric> metrics)
		{
			foreach (var m in metrics)
				Visit(m);
		}

		protected virtual void Visit(IMetric metric)
		{
			var im = metric as IMeter;
			if (im != null) Visit(im);
			else
			{
				var ic = metric as ICounter;
				if (ic != null) Visit(ic);
				else
				{
					var g = metric as IGauge;
					if (g != null) Visit(g);
				}
			}
		}

		protected abstract void Visit(IMeter meter);
		protected abstract void Visit(ICounter counter);
		protected abstract void Visit(IGauge gauge);
	}

	#endregion
	#region [ StringBuilderVisitor         ]

	internal class StringBuilderVisitor : MetricsVisitor
	{
		private readonly StringBuilder sb;

		public StringBuilderVisitor(StringBuilder sb)
		{
			this.sb = sb;
		}

		public override void Visit(IEnumerable<IMetric> metrics)
		{
			foreach (var m in metrics)
			{
				if (m.Parent == null)
				{
					sb.AppendLine();
					sb.AppendFormat("{0,-22}", m.Name);
				}
				else
				{
					sb.AppendFormat("{0,20}: ", m.Instance);
				}

				Visit(m);
				sb.AppendLine("      ");

				if (m.Parent == null)
					sb.AppendLine("========================================");
			}
		}

		protected override void Visit(IMeter meter)
		{
			sb.AppendFormat("{0:0.##}/{1}", meter.Rate, meter.Interval.ToString().ToLower());
		}

		protected override void Visit(ICounter counter)
		{
			sb.Append(counter.Count);
		}

		protected override void Visit(IGauge gauge)
		{
			sb.AppendFormat("{0} ({1}/{2})", gauge.Value, gauge.Min, gauge.Max);
		}
	}

	#endregion
	#region [ ConsoleReporter              ]

	public class ConsoleReporter
	{
		private StringBuilder sb;
		private StringBuilderVisitor visitor;

		public ConsoleReporter()
		{
			this.sb = new StringBuilder();
			this.visitor = new StringBuilderVisitor(sb);
		}

		public void Report()
		{
			sb.Length = 0;
			visitor.Visit(Metrics.GetAll());
#if NET451
            Console.CursorLeft = 0;
			Console.CursorTop = 0;
#endif
			Console.WriteLine(sb.ToString());
        }

		public void StartPeriodicReporting(int time, Interval interval, CancellationToken cancellationToken)
		{
			var delay = (int)IntervalConverter.Convert(time, interval, Interval.Milliseconds);

			Task.Factory.StartNew(async () =>
			{
				try
				{
					while (!cancellationToken.IsCancellationRequested)
					{
						Report();

						await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
					}
				}
				catch { }
			}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
		}
	}

	#endregion
	#region [ IntervalConverter            ]

	internal static class IntervalConverter
	{
		static readonly long[][] ConversionMatrix = CreateMatrix();

		static long[][] CreateMatrix()
		{
			var multipliers = new long[] { 1000L, 1000L, 1000L, 60L, 60L, 24L };
			var length = multipliers.Length + 1;
			var retval = new long[length][];

			for (var i = 0; i < length; i++)
			{
				retval[i] = new long[i];

				var tmp = 1L;
				for (var j = i - 1; j >= 0; j--)
				{
					tmp *= multipliers[j];
					retval[i][j] = tmp;
				}
			}

			return retval;
		}

		public static long Convert(long value, Interval from, Interval to)
		{
			return from == to
					? value
					: from > to
						? (value * ConversionMatrix[(int)from][(int)to])
						: (value / ConversionMatrix[(int)to][(int)from]);
		}
	}

	#endregion
	#region [ Internal impl.               ]

	namespace Internal
	{
		internal class DefaultMetricFactory : IMetricFactory
		{
			public ICounter Counter(string name)
			{
				return new SharedCounter(name);
			}

			public IMeter Meter(string name, Interval interval)
			{
				return new DefaultMeter(name, interval);
			}

			public IGauge Gauge(string name)
			{
				return new DefaultGauge(name);
			}

			public ICounter Counter(ICounter parent, string instance)
			{
				return new SharedCounter(parent, instance);
			}

			public IMeter Meter(IMeter parent, string instance, Interval interval)
			{
				return new DefaultMeter(parent, instance, interval);
			}

			public IGauge Gauge(IGauge parent, string instance)
			{
				return new DefaultGauge(parent, instance);
			}
		}

		internal abstract class DefaultMetric : IMetric
		{
			private readonly IMetric parent;
			private readonly string name;

			protected DefaultMetric(IMetric parent, string instance)
			{
				this.parent = parent;
				Instance = instance;
			}

			protected DefaultMetric(string name)
			{
				this.name = name;
			}

			public string Name { get { return name ?? parent.Name; } }
			public string Instance { get; private set; }
			IMetric IMetric.Parent { get { return parent; } }
		}

		internal class DefaultGauge : DefaultMetric, IGauge
		{
			public DefaultGauge(string name) : base(name) { }
			public DefaultGauge(IMetric parent, string instance) : base(parent, instance) { }

			public IGauge Parent { get { return ((IMetric)this).Parent as IGauge; } }

			public void Set(long value)
			{
				Value = value;
				if (Min > value) Min = value;
				if (Max < value) Max = value;

				var p = Parent;
				if (p != null) p.Set(value);
			}

			public long Value { get; private set; }
			public long Min { get; private set; }
			public long Max { get; private set; }
		}

		internal class SharedCounter : DefaultMetric, ICounter
		{
			private long global;
			private Entry[] slices;

			public SharedCounter(string name)
				: base(name)
			{
				Initialize();
			}

			public SharedCounter(IMetric parent, string instance)
				: base(parent, instance)
			{
				Initialize();
			}

			public ICounter Parent { get { return ((IMetric)this).Parent as ICounter; } }

			private struct Entry
			{
				public int ThreadId;
				public long Value;
			}

			private void Initialize()
			{
				slices = new Entry[Environment.ProcessorCount + 16];
				global = 0;
#if NET451
				Thread.MemoryBarrier();
#endif
			}

			public virtual void Reset()
			{
				Initialize();

				var p = Parent;
				if (p != null) p.Reset();
			}

			public void Increment()
			{
				ChangeBy(1);
			}

			public void Decrement()
			{
				ChangeBy(-1);
			}

			public void IncrementBy(int value)
			{
				ChangeBy(value);
			}

			public void DecrementBy(int value)
			{
				ChangeBy(-value);
			}

			private void ChangeBy(int by)
			{
				var p = Parent as SharedCounter;
				if (p != null) p.ChangeBy(by);

				var threadId = Thread.CurrentThread.ManagedThreadId;

				for (var i = 0; i < slices.Length; i++)
				{
					var id = slices[i].ThreadId;

					if (id == threadId)
					{
						slices[i].Value = slices[i].Value + by;
						return;
					}

					if (id == 0)
					{
						slices[i] = new Entry { ThreadId = threadId, Value = by };
						return;
					}
				}

				Interlocked.Add(ref global, by);
			}

			public long Count
			{
				get
				{
					long retval = global;
					for (var i = 0; i < slices.Length; i++)
					{
						if (slices[i].ThreadId > 0)
							retval += slices[i].Value;
					}

					return retval;
				}
			}
		}

		internal class DefaultMeter : SharedCounter, IMeter
		{
			private static readonly long NanoTick = 1000 * 1000 * 1000 / Stopwatch.Frequency;
			private readonly Stopwatch stopwatch;
			private readonly Interval interval;

			public DefaultMeter(string name, Interval interval)
				: base(name)
			{
				this.interval = interval;
				this.stopwatch = Stopwatch.StartNew();
			}

			public DefaultMeter(IMetric parent, string instance, Interval interval)
				: base(parent, instance)
			{
				this.interval = interval;
				this.stopwatch = Stopwatch.StartNew();
			}

			public override void Reset()
			{
				base.Reset();
				stopwatch.Restart();
			}

			public Interval Interval { get { return interval; } }

			public double Rate
			{
				get
				{
					var by = IntervalConverter.Convert(stopwatch.ElapsedTicks * NanoTick, Interval.Nanoseconds, interval);
					var retval = by == 0 ? 0 : Count / by;
					Reset();

					return retval;
				}
			}
		}
	}

	#endregion
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
