using Best.HTTP.Shared.PlatformSupport.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class BinaryProtocol
{

    public const int ByteSize = 1;
    public const int ShortSize = 2;
    public const int IntSize = 4;
    public const int LongSize = 8;
    public const int FloatSize = 4;
    public const int DoubleSize = 8;

    public static ushort SwapEndian(ushort value)
    {
        return (ushort)((value >> 8) | (value << 8));
    }

    public static uint SwapEndian(uint value)
    {
        return (value >> 24) |
               ((value >> 8) & 0xFF00) |
               ((value << 8) & 0xFF0000) |
               (value << 24);
    }

    public static BufferSegment EncodeFloat(float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes); // 转换为大端序
        }
        return new BufferSegment(bytes, 0, bytes.Length);
    }

    public static float DecodeFloat(byte[] payload, ref int offset)
    {
        if (offset + 4 > payload.Length)
        {
            throw new Exception("浮点数读取失败");
        }
        byte[] bytes = new byte[4];
        Array.Copy(payload, offset, bytes, 0, 4);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes); // 转换为小端序
        }
        float value = BitConverter.ToSingle(bytes, 0);
        offset += 4;
        return value;
    }

    public static BufferSegment EncodeVector3(Vector3 vector)
    {
        BufferSegment x = EncodeFloat(vector.x);
        BufferSegment y = EncodeFloat(vector.y);
        BufferSegment z = EncodeFloat(vector.z);
        byte[] buffer = BufferPool.Get(x.Count + y.Count + z.Count, true);
        int offset = 0;
        Array.Copy(x.Data, x.Offset, buffer, offset, x.Count);
        offset += x.Count;
        Array.Copy(y.Data, y.Offset, buffer, offset, y.Count);
        offset += y.Count;
        Array.Copy(z.Data, z.Offset, buffer, offset, z.Count);
        BufferPool.Release(x.Data);
        BufferPool.Release(y.Data);
        BufferPool.Release(z.Data);
        return new BufferSegment(buffer, 0, offset);
    }

    public static Vector3 DecodeVector3(byte[] payload, ref int offset)
    {
        float x = DecodeFloat(payload, ref offset);
        float y = DecodeFloat(payload, ref offset);
        float z = DecodeFloat(payload, ref offset);
        return new Vector3(x, y, z);
    }

    public static BufferSegment EncodeQuaternion(Quaternion quat)
    {
        int totalLength = 16; // 4 * 4 bytes (float)
        byte[] buffer = BufferPool.Get(totalLength, true);
        int offset = 0;
        Array.Copy(BitConverter.GetBytes(quat.x), 0, buffer, offset, 4);
        offset += 4;
        Array.Copy(BitConverter.GetBytes(quat.y), 0, buffer, offset, 4);
        offset += 4;
        Array.Copy(BitConverter.GetBytes(quat.z), 0, buffer, offset, 4);
        offset += 4;
        Array.Copy(BitConverter.GetBytes(quat.w), 0, buffer, offset, 4);
        return new BufferSegment(buffer, 0, totalLength);
    }

    public static Quaternion DecodeQuaternion(byte[] payload, ref int offset)
    {
        float x = BitConverter.ToSingle(payload, offset);
        offset += 4;
        float y = BitConverter.ToSingle(payload, offset);
        offset += 4;
        float z = BitConverter.ToSingle(payload, offset);
        offset += 4;
        float w = BitConverter.ToSingle(payload, offset);
        offset += 4;
        return new Quaternion(x, y, z, w);
    }

    /// <summary>
    /// 编码字符串
    /// </summary>
    /// <param name="value">要编码的字符串</param>
    /// <returns>编码后的二进制段 (2字节长度 + 字符串内容)</returns>
    public static BufferSegment EncodeString(string value)
    {
        byte[] stringBytes = Encoding.UTF8.GetBytes(value);
        byte[] buffer = BufferPool.Get(ShortSize + stringBytes.Length, true);

        // 大端序写入长度
        buffer[0] = (byte)(stringBytes.Length >> 8);
        buffer[1] = (byte)stringBytes.Length;

        Array.Copy(stringBytes, 0, buffer, ShortSize, stringBytes.Length);
        return new BufferSegment(buffer, 0, ShortSize + stringBytes.Length);
    }

    /// <summary>
    /// 解码字符串
    /// </summary>
    /// <param name="payload">二进制数据</param>
    /// <param name="offset">当前读取偏移量(会被修改)</param>
    /// <returns>解码后的字符串</returns>
    /// <exception cref="Exception">如果数据不完整</exception>
    public static string DecodeString(byte[] payload, ref int offset)
    {
        if (offset + ShortSize > payload.Length)
        {
            throw new Exception($"字符串长度读取失败: 需要{ShortSize}字节，剩余{payload.Length - offset}");
        }

        // 大端序读取长度
        ushort length = (ushort)((payload[offset] << 8) | payload[offset + 1]);
        offset += ShortSize;

        if (offset + length > payload.Length)
        {
            throw new Exception($"字符串内容读取失败: 需要{length}字节，剩余{payload.Length - offset}");
        }

        string result = Encoding.UTF8.GetString(payload, offset, length);
        offset += length;
        return result;
    }

    /// <summary>
    /// 编码32位整数
    /// </summary>
    /// <param name="value">要编码的整数</param>
    /// <returns>4字节二进制段</returns>
    public static BufferSegment EncodeInt32(int value)
    {
        byte[] buffer = BufferPool.Get(IntSize, true);
        buffer[0] = (byte)(value >> 24);
        buffer[1] = (byte)(value >> 16);
        buffer[2] = (byte)(value >> 8);
        buffer[3] = (byte)value;
        return new BufferSegment(buffer, 0, IntSize);
    }

    /// <summary>
    /// 解码32位整数
    /// </summary>
    /// <param name="payload">二进制数据</param>
    /// <param name="offset">当前读取偏移量(会被修改)</param>
    /// <returns>解码后的整数</returns>
    /// <exception cref="Exception">如果数据不完整</exception>
    public static int DecodeInt32(byte[] payload, ref int offset)
    {
        if (offset + IntSize > payload.Length)
        {
            throw new Exception($"32位整数读取失败: 需要{IntSize}字节，剩余{payload.Length - offset}");
        }

        int value = (payload[offset] << 24) |
                    (payload[offset + 1] << 16) |
                    (payload[offset + 2] << 8) |
                    payload[offset + 3];
        offset += IntSize;

        return value;
    }

    /// <summary>
    /// 编码状态码(1字节)
    /// </summary>
    /// <param name="status">状态码(0-255)</param>
    /// <returns>1字节二进制段</returns>
    public static BufferSegment EncodeStatus(byte status)
    {
        byte[] buffer = BufferPool.Get(ByteSize, true);
        buffer[0] = status;
        return new BufferSegment(buffer, 0, ByteSize);
    }

    /// <summary>
    /// 解码状态码
    /// </summary>
    /// <param name="payload">二进制数据</param>
    /// <param name="offset">当前读取偏移量(会被修改)</param>
    /// <returns>解码后的状态码</returns>
    /// <exception cref="Exception">如果数据不完整</exception>
    public static byte DecodeStatus(byte[] payload, ref int offset)
    {
        if (offset + ByteSize > payload.Length)
        {
            throw new Exception($"状态码读取失败: 需要{ByteSize}字节，剩余{payload.Length - offset}");
        }

        byte status = payload[offset];
        offset += ByteSize;
        return status;
    }

    /// <summary>
    /// 编码坐标位置(两个32位整数)
    /// </summary>
    /// <param name="position">坐标位置</param>
    /// <returns>8字节二进制段</returns>
    public static BufferSegment EncodePosition(Vector3Int position)
    {
        byte[] buffer = BufferPool.Get(IntSize * 2, true);

        // 编码X坐标
        buffer[0] = (byte)(position.x >> 24);
        buffer[1] = (byte)(position.x >> 16);
        buffer[2] = (byte)(position.x >> 8);
        buffer[3] = (byte)position.x;

        // 编码Y坐标
        buffer[4] = (byte)(position.y >> 24);
        buffer[5] = (byte)(position.y >> 16);
        buffer[6] = (byte)(position.y >> 8);
        buffer[7] = (byte)position.y;

        return new BufferSegment(buffer, 0, IntSize * 2);
    }

    /// <summary>
    /// 解码坐标位置
    /// </summary>
    /// <param name="payload">二进制数据</param>
    /// <param name="offset">当前读取偏移量(会被修改)</param>
    /// <returns>解码后的坐标位置</returns>
    /// <exception cref="Exception">如果数据不完整</exception>
    public static Vector3Int DecodePosition(byte[] payload, ref int offset)
    {
        int x = DecodeInt32(payload, ref offset);
        int y = DecodeInt32(payload, ref offset);
        return new Vector3Int(x, y, 0);
    }

    /// <summary>
    /// 编码字符串数组
    /// </summary>
    /// <param name="strings">字符串数组</param>
    /// <returns>编码后的二进制段</returns>
    public static BufferSegment EncodeStringArray(List<string> strings)
    {
        List<byte> payload = new List<byte>();

        // 编码数量(大端序)
        payload.Add((byte)(strings.Count >> 8));
        payload.Add((byte)strings.Count);

        // 编码每个字符串
        foreach (string str in strings)
        {
            BufferSegment strSegment = EncodeString(str);
            byte[] strBytes = new byte[strSegment.Count];
            Array.Copy(strSegment.Data, strSegment.Offset, strBytes, 0, strSegment.Count);
            payload.AddRange(strBytes);
            BufferPool.Release(strSegment.Data);
        }

        byte[] buffer = BufferPool.Get(payload.Count, true);
        Array.Copy(payload.ToArray(), 0, buffer, 0, payload.Count);
        return new BufferSegment(buffer, 0, payload.Count);
    }

    /// <summary>
    /// 解码字符串数组
    /// </summary>
    /// <param name="payload">二进制数据</param>
    /// <param name="offset">当前读取偏移量(会被修改)</param>
    /// <returns>解码后的字符串列表</returns>
    /// <exception cref="Exception">如果数据不完整</exception>
    public static List<string> DecodeStringArray(byte[] payload, ref int offset)
    {
        if (offset + ShortSize > payload.Length)
        {
            throw new Exception("字符串数组长度读取失败");
        }

        // 大端序读取数量
        int count = (payload[offset] << 8) | payload[offset + 1];
        offset += ShortSize;

        List<string> result = new List<string>();
        for (int i = 0; i < count; i++)
        {
            result.Add(DecodeString(payload, ref offset));
        }
        return result;
    }

    /// <summary>
    /// 玩家信息结构
    /// </summary>
    public struct PlayerInfo
    {
        public int PlayerId;
        public string Name;
        public int Level;
        public int Role;
        public Vector3Int Position;
    }

    public static Dictionary<string, object> DecodeCharacterInfo(byte[] payload, ref int offset)
    {
        return new Dictionary<string, object>
        {
            {"characterId", DecodeInt32(payload, ref offset)},
            {"name", DecodeString(payload, ref offset)},
            {"level", DecodeInt32(payload, ref offset)},
            {"role", DecodeInt32(payload, ref offset)},
            {"skinId", DecodeInt32(payload, ref offset)},
            {"mapId", DecodeInt32(payload, ref offset)},
            {"x", DecodeInt32(payload, ref offset)},
            {"y", DecodeInt32(payload, ref offset)},
        };
    }
}