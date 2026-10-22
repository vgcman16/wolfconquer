using System.Text;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativePacket
{
    public void Write(bool value)
    {
        Write((byte)(value ? 1u : 0u));
    }

    public unsafe void Write(byte value)
    {
        if (Position + 1 < 16384)
        {
            *stream = value;
            stream++;
        }
    }

    public unsafe void Write(ushort value)
    {
        if (Position + 2 < 16384)
        {
            *(ushort*)stream = value;
            stream += 2;
        }
    }

    public unsafe void Write(uint value)
    {
        if (Position + 4 < 16384)
        {
            *(uint*)stream = value;
            stream += 4;
        }
    }

    public unsafe void Write(ulong value)
    {
        if (Position + 8 < 16384)
        {
            *(ulong*)stream = value;
            stream += 8;
        }
    }

    public void Write(sbyte value)
    {
        Write((byte)value);
    }

    public void Write(short value)
    {
        Write((ushort)value);
    }

    public void Write(int value)
    {
        Write((uint)value);
    }

    public void Write(long value)
    {
        Write((ulong)value);
    }

    public void Write(string value, int length)
    {
        int num = Math.Min(value.Length, length);
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        for (int i = 0; i < num; i++)
        {
            Write(bytes[i]);
        }
        ZeroFill(length - num);
    }

    public void Write(params string[] value)
    {
        Write((byte)value.Length);
        foreach (string text in value)
        {
            if (string.IsNullOrEmpty(text))
            {
                Write((ushort)0);
                continue;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            Write((ushort)bytes.Length);
            for (int j = 0; j < bytes.Length; j++)
            {
                Write(bytes[j]);
            }
        }
    }

    public unsafe void WriteUnsafe(void* buf, int length)
    {
        if (Position + length < 16384)
        {
            memcpy(stream, buf, length);
            stream += length;
        }
    }

    public unsafe void ZeroFill(int amount)
    {
        if (Position + amount < 16384)
        {
            if (amount > 0)
            {
                memset(stream, 0, amount);
            }
            stream += amount;
        }
    }

    public string[] ReadStringList()
    {
        string[] array = new string[ReadUInt8()];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = ReadCString(ReadUInt16());
        }
        return array;
    }

    public unsafe void ReadUnsafe(void* buf, int length)
    {
        if (Position + length < 16384)
        {
            memcpy(buf, stream, length);
            stream += length;
        }
    }

    public byte[] ReadBytes(int size)
    {
        byte[] array = new byte[size];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = ReadUInt8();
        }
        return array;
    }

    public string ReadCString(int size)
    {
        if (Position + size >= 16384)
        {
            return "";
        }
        string text = Encoding.UTF8.GetString(ReadBytes(size));
        int num = text.IndexOf('\0');
        return (num > -1) ? text.Substring(0, num) : text;
    }

    public unsafe byte ReadUInt8()
    {
        if (Position + 1 >= 16384)
        {
            return 0;
        }
        byte result = *stream;
        stream++;
        return result;
    }

    public unsafe ushort ReadUInt16()
    {
        if (Position + 2 >= 16384)
        {
            return 0;
        }
        ushort result = *(ushort*)stream;
        stream += 2;
        return result;
    }

    public unsafe uint ReadUInt32()
    {
        if (Position + 4 >= 16384)
        {
            return 0u;
        }
        uint result = *(uint*)stream;
        stream += 4;
        return result;
    }

    public unsafe ulong ReadUInt64()
    {
        if (Position + 8 >= 16384)
        {
            return 0uL;
        }
        ulong result = *(ulong*)stream;
        stream += 8;
        return result;
    }

    public sbyte ReadInt8()
    {
        return (sbyte)ReadUInt8();
    }

    public short ReadInt16()
    {
        return (short)ReadUInt16();
    }

    public int ReadInt32()
    {
        return (int)ReadUInt32();
    }

    public long ReadInt64()
    {
        return (long)ReadUInt64();
    }

    public void CreateProtocolBuffer(params uint[] values)
    {
        Write((byte)8);
        for (int i = 0; i < values.Length; i++)
        {
            uint num;
            for (num = values[i]; num > 127; num >>= 7)
            {
                Write((byte)((num & 0x7F) | 0x80));
            }
            Write((byte)(num & 0x7F));
            Write((byte)(8 * (i + 2)));
            if (i + 1 == values.Length)
            {
                break;
            }
        }
    }
}
