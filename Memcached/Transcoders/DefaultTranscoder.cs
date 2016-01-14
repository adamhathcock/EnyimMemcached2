using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
#if NET451
using System.Runtime.Serialization.Formatters.Binary;
#else
using System.Runtime.Serialization.Json;
#endif
using System.Text;
using System.Reflection;

namespace Enyim.Caching.Memcached
{
	public class DefaultTranscoder : ITranscoder
	{
		public const uint RawDataFlag = 0xfa52;

		private readonly IBufferAllocator allocator;

		public DefaultTranscoder(IBufferAllocator allocator)
		{
			this.allocator = allocator;
		}

		public CacheItem Serialize(object value)
		{
			// - or we just received a byte[]. No further processing is needed.
			var tmpByteArray = value as byte[];
			if (tmpByteArray != null)
				return new CacheItem(RawDataFlag, new PooledSegment(tmpByteArray, tmpByteArray.Length));

            // got some real data, serialize it
            var code = value?.GetType().GetTypeCode() ?? TypeCode.Empty;

			PooledSegment data;
			switch (code)
			{
				case TypeCode.Empty: data = PooledSegment.Empty; break;
				case TypeCode.String: data = SerializeString((String)value); break;

				case TypeCode.SByte: data = PooledBitConverter.GetBytes(allocator, (SByte)value); break;
				case TypeCode.Byte: data = PooledBitConverter.GetBytes(allocator, (Byte)value); break;

				case TypeCode.Boolean: data = PooledBitConverter.GetBytes(allocator, (Boolean)value); break;
				case TypeCode.Char: data = PooledBitConverter.GetBytes(allocator, (Char)value); break;

				case TypeCode.Int16: data = PooledBitConverter.GetBytes(allocator, (Int16)value); break;
				case TypeCode.Int32: data = PooledBitConverter.GetBytes(allocator, (Int32)value); break;
				case TypeCode.Int64: data = PooledBitConverter.GetBytes(allocator, (Int64)value); break;
				case TypeCode.UInt16: data = PooledBitConverter.GetBytes(allocator, (UInt16)value); break;
				case TypeCode.UInt32: data = PooledBitConverter.GetBytes(allocator, (UInt32)value); break;
				case TypeCode.UInt64: data = PooledBitConverter.GetBytes(allocator, (UInt64)value); break;
				case TypeCode.DateTime: data = PooledBitConverter.GetBytes(allocator, ((DateTime)value).ToBinary()); break;

				case TypeCode.Single: data = PooledBitConverter.GetBytes(allocator, (Single)value); break;
				case TypeCode.Double: data = PooledBitConverter.GetBytes(allocator, (Double)value); break;
				case TypeCode.Decimal: data = PooledBitConverter.GetBytes(allocator, (Decimal)value); break;

				case TypeCode.Object:
					// raw data is a special case when someone passes in a buffer (byte[] or ArraySegment<byte>)
					// ArraySegment<byte> is only passed in when a part of buffer is being
					// serialized, usually from a MemoryStream (To avoid duplicating arrays
					// the byte[] returned by MemoryStream.GetBuffer is placed into an ArraySegment.)
					if (value is ArraySegment<byte>)
						return new CacheItem(RawDataFlag, PooledSegment.From((ArraySegment<byte>)value));

					data = SerializeObject(value); break;
				default: throw new InvalidOperationException("Unknown TypeCode was returned: " + code);
			}

			return new CacheItem((uint)((int)code | 0x100), data);
		}

		public object Deserialize(Type type, CacheItem item)
		{
			if (item.Segment.Array == null)
				return null;

			if (item.Flags == RawDataFlag || (item.Flags & 0x1ff) != item.Flags)
			{
				var tmp = item.Segment;
				if (tmp.Count == tmp.Array.Length)
					return tmp.Array;

				// TODO improve memcpy
				var retval = new byte[tmp.Count];
				Buffer.BlockCopy(tmp.Array, 0, retval, 0, tmp.Count);

				return retval;
			}

			var code = (TypeCode)(item.Flags & 0xff);
			var data = item.Segment;

			switch (code)
			{
				case TypeCode.Empty: return null;

				// incrementing a non-existing key then getting it
				// returns as a string, but the flag will be 0
				// so treat all 0 flagged items as string
				// this may help inter-client data management as well
				case TypeCode.String: return DeserializeString(data);

				case TypeCode.Byte: return data.Array[0];
				case TypeCode.SByte: return (sbyte)data.Array[0];

				case TypeCode.Boolean: return PooledBitConverter.ToBoolean(data);
				case TypeCode.Char: return PooledBitConverter.ToChar(data);

				case TypeCode.Int16: return PooledBitConverter.ToInt16(data);
				case TypeCode.Int32: return PooledBitConverter.ToInt32(data);
				case TypeCode.Int64: return PooledBitConverter.ToInt64(data);
				case TypeCode.UInt16: return PooledBitConverter.ToUInt16(data);
				case TypeCode.UInt32: return PooledBitConverter.ToUInt32(data);
				case TypeCode.UInt64: return PooledBitConverter.ToUInt64(data);
				case TypeCode.DateTime: return DateTime.FromBinary(PooledBitConverter.ToInt64(data));

				case TypeCode.Single: return PooledBitConverter.ToSingle(data);
				case TypeCode.Double: return PooledBitConverter.ToDouble(data);
				case TypeCode.Decimal: return PooledBitConverter.ToDecimal(data);

				case TypeCode.Object: return DeserializeObject(type, data);
				default: throw new InvalidOperationException("Unknown TypeCode was returned: " + code);
			}
		}

		private PooledSegment SerializeString(string value)
		{
			if (String.IsNullOrEmpty(value))
				return PooledSegment.Empty;

			var utf8 = Encoding.UTF8;
			var buffer = allocator.Take(utf8.GetMaxByteCount(value.Length));
			var count = utf8.GetBytes(value, 0, value.Length, buffer, 0);

			return new PooledSegment(allocator, buffer, count);
		}

		private static string DeserializeString(PooledSegment value)
		{
			return Encoding.UTF8.GetString(value.Array, 0, value.Count);
		}

		private PooledSegment SerializeObject(object value)
		{
			using (var ms = new PooledMemoryStream(allocator))
			{
#if NET451
                new BinaryFormatter().Serialize(ms, value);
#else
                new DataContractJsonSerializer(value.GetType()).WriteObject(ms, value);
#endif

                var retval = new PooledSegment(allocator, (int)ms.Length);
				ms.Position = 0;
				ms.Read(retval.Array, 0, retval.Count);

				return retval;
			}
		}

		private static object DeserializeObject(Type type, PooledSegment value)
		{
			using (var ms = new MemoryStream(value.Array, 0, value.Count))
            {
#if NET451
                return new BinaryFormatter().Deserialize(ms);
#else
                if (type == null)
                {
                    throw new InvalidOperationException("Null type specified for object deserialization");
                }
                return new DataContractJsonSerializer(type).ReadObject(ms);
#endif
            }
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
