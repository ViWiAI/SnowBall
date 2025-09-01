using Best.HTTP.Shared.PlatformSupport.Memory;
using System;
using System.Collections.Generic;
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
            Array.Reverse(bytes); // 确保大端序
        }
        //Debug.Log($"EncodeFloat: value={value}, bytes={BitConverter.ToString(bytes)}");
        return new BufferSegment(bytes, 0, bytes.Length);
    }

    public static float DecodeFloat(byte[] payload, ref int offset)
    {
        if (offset + FloatSize > payload.Length)
        {
            throw new Exception($"浮点数读取失败: 需要{FloatSize}字节，剩余{payload.Length - offset}");
        }
        byte[] bytes = new byte[4];
        Array.Copy(payload, offset, bytes, 0, 4);
        //Debug.Log($"DecodeFloat: offset={offset}, bytes={BitConverter.ToString(bytes)}");
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes); // 转换为小端序
        }
        float value = BitConverter.ToSingle(bytes, 0);
        offset += 4;
        return value;
    }

    public static BufferSegment EncodePosition(Vector3 position)
    {
        byte[] buffer = new byte[12];
        int offset = 0;
        Array.Copy(EncodeFloat(position.x).Data, 0, buffer, offset, 4);
        offset += 4;
        Array.Copy(EncodeFloat(position.y).Data, 0, buffer, offset, 4);
        offset += 4;
        Array.Copy(EncodeFloat(position.z).Data, 0, buffer, offset, 4);
        //Debug.Log($"EncodePosition: position={position}, bytes={BitConverter.ToString(buffer)}");
        return new BufferSegment(buffer, 0, 12);
    }

    public static BufferSegment EncodeVector3(Vector3 vector)
    {
        byte[] buffer = new byte[12];
        int offset = 0;
        Array.Copy(EncodeFloat(vector.x).Data, 0, buffer, offset, 4);
        offset += 4;
        Array.Copy(EncodeFloat(vector.y).Data, 0, buffer, offset, 4);
        offset += 4;
        Array.Copy(EncodeFloat(vector.z).Data, 0, buffer, offset, 4);
        //Debug.Log($"EncodeVector3: vector={vector}, bytes={BitConverter.ToString(buffer)}");
        return new BufferSegment(buffer, 0, 12);
    }

    public static BufferSegment EncodeQuaternion(Quaternion quaternion)
    {
        byte[] buffer = new byte[16];
        int offset = 0;
        Array.Copy(EncodeFloat(quaternion.x).Data, 0, buffer, offset, 4);
        offset += 4;
        Array.Copy(EncodeFloat(quaternion.y).Data, 0, buffer, offset, 4);
        offset += 4;
        Array.Copy(EncodeFloat(quaternion.z).Data, 0, buffer, offset, 4);
        offset += 4;
        Array.Copy(EncodeFloat(quaternion.w).Data, 0, buffer, offset, 4);
        //Debug.Log($"EncodeQuaternion: quaternion={quaternion}, bytes={BitConverter.ToString(buffer)}");
        return new BufferSegment(buffer, 0, 16);
    }

    public static Vector3 DecodePosition(byte[] payload, ref int offset)
    {
        Vector3 position = new Vector3(
            DecodeFloat(payload, ref offset),
            DecodeFloat(payload, ref offset),
            DecodeFloat(payload, ref offset)
        );
        //Debug.Log($"DecodePosition: position={position}");
        return position;
    }

    public static Vector3 DecodeVector3(byte[] payload, ref int offset)
    {
        Vector3 vector = new Vector3(
            DecodeFloat(payload, ref offset),
            DecodeFloat(payload, ref offset),
            DecodeFloat(payload, ref offset)
        );
        //Debug.Log($"DecodeVector3: vector={vector}");
        return vector;
    }

    public static Quaternion DecodeQuaternion(byte[] payload, ref int offset)
    {
        Quaternion quaternion = new Quaternion(
            DecodeFloat(payload, ref offset),
            DecodeFloat(payload, ref offset),
            DecodeFloat(payload, ref offset),
            DecodeFloat(payload, ref offset)
        );
        //Debug.Log($"DecodeQuaternion: quaternion={quaternion}");
        return quaternion;
    }

    public static BufferSegment EncodeString(string value)
    {
        byte[] stringBytes = Encoding.UTF8.GetBytes(value);
        byte[] buffer = BufferPool.Get(ShortSize + stringBytes.Length, true);

        buffer[0] = (byte)(stringBytes.Length >> 8);
        buffer[1] = (byte)stringBytes.Length;

        Array.Copy(stringBytes, 0, buffer, ShortSize, stringBytes.Length);
        //Debug.Log($"EncodeString: value={value}, bytes={BitConverter.ToString(buffer)}");
        return new BufferSegment(buffer, 0, ShortSize + stringBytes.Length);
    }

    public static string DecodeString(byte[] payload, ref int offset)
    {
        if (offset + ShortSize > payload.Length)
        {
            throw new Exception($"字符串长度读取失败: 需要{ShortSize}字节，剩余{payload.Length - offset}");
        }

        ushort length = (ushort)((payload[offset] << 8) | payload[offset + 1]);
        offset += ShortSize;

        if (offset + length > payload.Length)
        {
            throw new Exception($"字符串内容读取失败: 需要{length}字节，剩余{payload.Length - offset}");
        }

        string result = Encoding.UTF8.GetString(payload, offset, length);
        offset += length;
        //Debug.Log($"DecodeString: result={result}");
        return result;
    }

    public static BufferSegment EncodeInt32(int value)
    {
        byte[] buffer = BufferPool.Get(IntSize, true);
        buffer[0] = (byte)(value >> 24);
        buffer[1] = (byte)(value >> 16);
        buffer[2] = (byte)(value >> 8);
        buffer[3] = (byte)value;
        //Debug.Log($"EncodeInt32: value={value}, bytes={BitConverter.ToString(buffer)}");
        return new BufferSegment(buffer, 0, IntSize);
    }

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
        //Debug.Log($"DecodeInt32: value={value}");
        return value;
    }

    public static BufferSegment EncodeStatus(byte status)
    {
        byte[] buffer = BufferPool.Get(ByteSize, true);
        buffer[0] = status;
        //Debug.Log($"EncodeStatus: status={status}, bytes={BitConverter.ToString(buffer)}");
        return new BufferSegment(buffer, 0, ByteSize);
    }

    public static byte DecodeStatus(byte[] payload, ref int offset)
    {
        if (offset + ByteSize > payload.Length)
        {
            throw new Exception($"状态码读取失败: 需要{ByteSize}字节，剩余{payload.Length - offset}");
        }

        byte status = payload[offset];
        offset += ByteSize;
        //Debug.Log($"DecodeStatus: status={status}");
        return status;
    }

    public static BufferSegment EncodeStringArray(List<string> strings)
    {
        List<byte> payload = new List<byte>();

        payload.Add((byte)(strings.Count >> 8));
        payload.Add((byte)strings.Count);

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
        //Debug.Log($"EncodeStringArray: count={strings.Count}, bytes={BitConverter.ToString(buffer)}");
        return new BufferSegment(buffer, 0, payload.Count);
    }

    public static List<string> DecodeStringArray(byte[] payload, ref int offset)
    {
        if (offset + ShortSize > payload.Length)
        {
            throw new Exception("字符串数组长度读取失败");
        }

        int count = (payload[offset] << 8) | payload[offset + 1];
        offset += ShortSize;

        List<string> result = new List<string>();
        for (int i = 0; i < count; i++)
        {
            result.Add(DecodeString(payload, ref offset));
        }
        //Debug.Log($"DecodeStringArray: count={count}, result={string.Join(",", result)}");
        return result;
    }
}