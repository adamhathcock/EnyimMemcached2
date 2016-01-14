using System;
using System.Runtime.CompilerServices;

namespace Enyim.Caching.Memcached
{
	internal static class PooledBitConverter
	{
		const byte FALSE = 0;
		const byte TRUE = 1;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PooledSegment GetBytes(IBufferAllocator allocator, byte value)
		{
			var retval = new PooledSegment(allocator, 1);
			retval.Array[0] = value;

			return retval;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PooledSegment GetBytes(IBufferAllocator allocator, bool value)
		{
			var retval = new PooledSegment(allocator, 1);
			retval.Array[0] = value ? TRUE : FALSE;

			return retval;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PooledSegment GetBytes(IBufferAllocator allocator, char value)
		{
			return GetBytes(allocator, (short)value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static PooledSegment GetBytes(IBufferAllocator allocator, short value)
		{
			var retval = new PooledSegment(allocator, 2);

			fixed (byte* ptr = retval.Array)
			{
				ptr[0] = (byte)value;
				ptr[1] = (byte)(value >> 8);
			}

			return retval;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static PooledSegment GetBytes(IBufferAllocator allocator, int value)
		{
			var retval = new PooledSegment(allocator, 4);

			fixed (byte* ptr = retval.Array)
			{
				ptr[0] = (byte)value;
				ptr[1] = (byte)(value >> 8);
				ptr[2] = (byte)(value >> 16);
				ptr[3] = (byte)(value >> 24);
			}

			return retval;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static PooledSegment GetBytes(IBufferAllocator allocator, decimal value)
		{
			// should use 'internal static void GetBytes(decimal d, byte[] buffer)'
			int v;
			var tmp = Decimal.GetBits(value);
			var retval = new PooledSegment(allocator, 16);

			const int I_0 = 0;
			const int I_1 = 4;
			const int I_2 = 8;
			const int I_3 = 12;

			fixed (byte* ptr = retval.Array)
			{
				v = tmp[0];
				ptr[0 + I_0] = (byte)v;
				ptr[1 + I_0] = (byte)(v >> 8);
				ptr[2 + I_0] = (byte)(v >> 16);
				ptr[3 + I_0] = (byte)(v >> 24);

				v = tmp[1];
				ptr[0 + I_1] = (byte)v;
				ptr[1 + I_1] = (byte)(v >> 8);
				ptr[2 + I_1] = (byte)(v >> 16);
				ptr[3 + I_1] = (byte)(v >> 24);

				v = tmp[2];
				ptr[0 + I_2] = (byte)v;
				ptr[1 + I_2] = (byte)(v >> 8);
				ptr[2 + I_2] = (byte)(v >> 16);
				ptr[3 + I_2] = (byte)(v >> 24);

				v = tmp[3];
				ptr[0 + I_3] = (byte)v;
				ptr[1 + I_3] = (byte)(v >> 8);
				ptr[2 + I_3] = (byte)(v >> 16);
				ptr[3 + I_3] = (byte)(v >> 24);
			}

			return retval;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static decimal ToDecimal(PooledSegment value)
		{
			if (value.Count != 16)
				throw new ArgumentOutOfRangeException("value.Count", value.Count, "count must be == 16");

			const int I_0 = 0;
			const int I_1 = 4;
			const int I_2 = 8;
			const int I_3 = 12;

			fixed (byte* ptr = value.Array)
			{
				var v0 = ptr[0 + I_0]
							+ (ptr[1 + I_0] << 8)
							+ (ptr[2 + I_0] << 16)
							+ (ptr[3 + I_0] << 24);

				var v1 = ptr[0 + I_1]
							+ (ptr[1 + I_1] << 8)
							+ (ptr[2 + I_1] << 16)
							+ (ptr[3 + I_1] << 24);

				var v2 = ptr[0 + I_2]
							+ (ptr[1 + I_2] << 8)
							+ (ptr[2 + I_2] << 16)
							+ (ptr[3 + I_2] << 24);

				var v3 = ptr[0 + I_3]
						+ (ptr[1 + I_3] << 8)
						+ (ptr[2 + I_3] << 16)
						+ (ptr[3 + I_3] << 24);

				return new decimal(new int[] { v0, v1, v2, v3 });
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static PooledSegment GetBytes(IBufferAllocator allocator, long value)
		{
			var retval = new PooledSegment(allocator, 8);

			fixed (byte* ptr = retval.Array)
			{
				ptr[0] = (byte)value;
				ptr[1] = (byte)(value >> 8);
				ptr[2] = (byte)(value >> 16);
				ptr[3] = (byte)(value >> 24);
				ptr[4] = (byte)(value >> 32);
				ptr[5] = (byte)(value >> 40);
				ptr[6] = (byte)(value >> 48);
				ptr[7] = (byte)(value >> 56);
			}

			return retval;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PooledSegment GetBytes(IBufferAllocator allocator, ushort value)
		{
			return GetBytes(allocator, (short)value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PooledSegment GetBytes(IBufferAllocator allocator, uint value)
		{
			return GetBytes(allocator, (int)value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PooledSegment GetBytes(IBufferAllocator allocator, ulong value)
		{
			return GetBytes(allocator, (long)value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static PooledSegment GetBytes(IBufferAllocator allocator, float value)
		{
			return GetBytes(allocator, *(int*)(&value));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static PooledSegment GetBytes(IBufferAllocator allocator, double value)
		{
			return GetBytes(allocator, *(long*)(&value));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool ToBoolean(PooledSegment value)
		{
			return value.Array[0] == TRUE;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char ToChar(PooledSegment value)
		{
			return (char)ToInt16(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static short ToInt16(PooledSegment value)
		{
			if (value.Count != 2)
				throw new ArgumentOutOfRangeException("value.Count", value.Count, "count must be == 2");

			fixed (byte* ptr = value.Array)
			{
				return (short)(ptr[0] + (ptr[1] << 8));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static int ToInt32(PooledSegment value)
		{
			if (value.Count != 4)
				throw new ArgumentOutOfRangeException("value.Count", value.Count, "count must be == 4");

			fixed (byte* ptr = value.Array)
			{
				return (ptr[0]
							+ (ptr[1] << 8)
							+ (ptr[2] << 16)
							+ (ptr[3] << 24));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static long ToInt64(PooledSegment value)
		{
			if (value.Count < 8)
				throw new ArgumentOutOfRangeException("value.Count", value.Count, "count must be >= 8");

			fixed (byte* ptr = value.Array)
			{
				return (ptr[0]
							+ (ptr[1] << 8)
							+ (ptr[2] << 16)
							+ (ptr[3] << 24)
							+ (ptr[4] << 32)
							+ (ptr[5] << 40)
							+ (ptr[6] << 48)
							+ (ptr[7] << 56));
			}
		}

		public static ushort ToUInt16(PooledSegment value)
		{
			return (ushort)ToInt16(value);
		}

		public static uint ToUInt32(PooledSegment value)
		{
			return (uint)ToInt32(value);
		}

		public static ulong ToUInt64(PooledSegment value)
		{
			return (ulong)ToInt64(value);
		}

		public unsafe static float ToSingle(PooledSegment value)
		{
			var tmp = ToInt32(value);

			return *(&tmp);
		}

		public unsafe static double ToDouble(PooledSegment value)
		{
			var tmp = ToInt64(value);

			return *(&tmp);
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
