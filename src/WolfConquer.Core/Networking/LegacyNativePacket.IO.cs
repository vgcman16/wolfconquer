using System.Text;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativePacket
{
    public void Write(bool value)
    {
        Write((byte)(value ? 1u : 0u));
    }

    public void Write(byte value) => WritePhysicalScalar(value, 1);

    public void Write(ushort value) => WritePhysicalScalar(value, 2);

    public void Write(uint value) => WritePhysicalScalar(value, 4);

    public void Write(ulong value) => WritePhysicalScalar(value, 8);

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
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ValidatePhysicalWrite(length);
        var prepared = new byte[length];
        var copied = Math.Min(value.Length, length);
        if (copied > 0)
        {
            // Retain native character-count truncation of UTF8 bytes. Include
            // the next character so a boundary surrogate keeps its original pair.
            var characters = copied < value.Length ? copied + 1 : copied;
            var encoded = Encoding.UTF8.GetBytes(value[..characters]);
            encoded.AsSpan(0, copied).CopyTo(prepared);
        }
        WritePhysicalBytes(prepared);
    }

    public void Write(params string[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), "String-list count exceeds its byte representation.");
        var position = ValidatePhysicalWrite(1);
        var snapshot = (string[])value.Clone();
        var lengths = new int[snapshot.Length];
        long total = 1;
        for (var index = 0; index < snapshot.Length; index++)
        {
            var text = snapshot[index];
            var length = string.IsNullOrEmpty(text) ? 0 : Encoding.UTF8.GetByteCount(text);
            if (length > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), "String-list entry exceeds its UInt16 byte length.");
            lengths[index] = length;
            total += 2 + length;
            if (total > Capacity - position)
                throw new InvalidOperationException("String list exceeds the packet allocation.");
        }
        var prepared = new byte[(int)total];
        prepared[0] = (byte)snapshot.Length;
        var offset = 1;
        for (var index = 0; index < snapshot.Length; index++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(prepared.AsSpan(offset), (ushort)lengths[index]);
            offset += 2;
            if (lengths[index] > 0)
                Encoding.UTF8.GetBytes(snapshot[index].AsSpan(), prepared.AsSpan(offset, lengths[index]));
            offset += lengths[index];
        }
        WritePhysicalBytes(prepared);
    }

    public unsafe void WriteUnsafe(void* buf, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ValidatePhysicalWrite(length);
        if (buf == null && length > 0) throw new ArgumentNullException(nameof(buf));
        var prepared = new byte[length];
        // Source extent/lifetime remains caller-owned; snapshot before changing
        // the validated destination, including for valid overlapping spans.
        if (length > 0) System.Runtime.InteropServices.Marshal.Copy((nint)buf, prepared, 0, length);
        WritePhysicalBytes(prepared);
    }

    public unsafe void ZeroFill(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var position = ValidatePhysicalWrite(amount);
        new Span<byte>(Memory + position, amount).Clear();
        stream = Memory + position + amount;
    }

    public string[] ReadStringList() => ReadPhysicalStringList();

    public unsafe void ReadUnsafe(void* buf, int length)
    {
        ReadPhysicalInto(buf, length);
    }

    public byte[] ReadBytes(int size) => ReadPhysicalBytes(size);

    public string ReadCString(int size) => ReadPhysicalCString(size);

    public byte ReadUInt8() => (byte)ReadPhysicalScalar(1);

    public ushort ReadUInt16() => (ushort)ReadPhysicalScalar(2);

    public uint ReadUInt32() => (uint)ReadPhysicalScalar(4);

    public ulong ReadUInt64() => ReadPhysicalScalar(8);

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
        ArgumentNullException.ThrowIfNull(values);
        var position = ValidatePhysicalWrite(0);
        var available = Capacity - position;
        if (1L + 2L * values.Length > available)
            throw new InvalidOperationException("Protocol helper output exceeds the packet allocation.");
        var snapshot = (uint[])values.Clone();
        long total = 1;
        foreach (var value in snapshot)
        {
            total += 2;
            for (var remaining = value; remaining > 127; remaining >>= 7) total++;
            if (total > available)
                throw new InvalidOperationException("Protocol helper output exceeds the packet allocation.");
        }
        var prepared = new byte[(int)total];
        prepared[0] = 8;
        var offset = 1;
        for (var index = 0; index < snapshot.Length; index++)
        {
            var remaining = snapshot[index];
            while (remaining > 127)
            {
                prepared[offset++] = (byte)((remaining & 0x7f) | 0x80);
                remaining >>= 7;
            }
            prepared[offset++] = (byte)remaining;
            // Preserve the opaque helper's existing single low-byte next token.
            prepared[offset++] = unchecked((byte)(8 * (index + 2)));
        }
        WritePhysicalBytes(prepared);
    }
}
