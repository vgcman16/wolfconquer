using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WolfConquer.Core.Networking;

// Native ServerSockets.Packet unmanaged storage/lifecycle subset, not a DTO or wire adapter.
// Print/Dump and pipe encryption are absent; genuine protobuf methods are in their partial.
// Native allocation/cursor quirks remain: callers must own and bound their unsafe buffers.
internal partial class LegacyNativePacket : IDisposable
{
    public const int MAX_SIZE = 16384;

    private const int TQ_SEALSIZE = 8;

    private static string? seal;

    private bool IsDisposed = false;

    public unsafe byte* stream;

    public static string SealString
    {
        get
        {
            return seal!;
        }
        set
        {
            if (value.Length != 8)
            {
                throw new ArgumentOutOfRangeException("value", "Packet seal must be " + 8 + " chars long.");
            }
            seal = value;
        }
    }

    public static int SealSize => (seal != null) ? 8 : 0;

    public int Size { get; set; }

    public unsafe byte* Memory { get; private set; }

    public unsafe int Position => (int)(stream - Memory);

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
    private unsafe static extern void* memset(void* dst, int val, int count);

    public unsafe LegacyNativePacket(byte[] buffer)
    {
        Memory = (byte*)(void*)Marshal.AllocHGlobal(1024);
        stream = Memory;
        Marshal.Copy(buffer, 0, (IntPtr)stream, buffer.Length);
        Size = buffer.Length;
        if (new StackTrace().FrameCount > 70)
        {
            throw new Exception("[WARNING]: Stack trace call stack's frame count excceeded 70");
        }
    }

    public unsafe LegacyNativePacket(int size)
    {
        Memory = (byte*)(void*)Marshal.AllocHGlobal(size);
        stream = Memory;
        if (new StackTrace().FrameCount > 70)
        {
            throw new Exception("[WARNING]: Stack trace call stack's frame count excceeded 70");
        }
    }

    ~LegacyNativePacket()
    {
        Dispose();
    }

    public unsafe void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            if (!((IntPtr)Memory == IntPtr.Zero))
            {
                Marshal.FreeHGlobal((IntPtr)Memory);
                Memory = null;
                GC.SuppressFinalize(this);
            }
        }
    }

    public void InitWriter()
    {
        Seek(4);
    }

    public unsafe void Seek(int offset)
    {
        stream = Memory + offset;
    }

    public void SeekForward(int amount)
    {
        Seek(Position + amount);
    }

    public void SeekBackwards(int amount)
    {
        Seek(Position - amount);
    }

    public void Finalize(ushort type)
    {
        if (SealSize > 0)
        {
            WriteSeal();
        }
        Size = Position;
        Seek(0);
        Write((ushort)(Size - SealSize));
        Write(type);
    }

    public void WriteSeal()
    {
        Write(SealString, 8);
    }

    public unsafe void memcpy(void* dest, void* src, int size)
    {
        int num = size / 8;
        for (int i = 0; i < num; i++)
        {
            try
            {
                ((long*)dest)[i] = ((long*)src)[i];
            }
            catch
            {
                break;
            }
        }
        int num2 = size - size % 8;
        for (int j = 0; j < size % 8; j++)
        {
            try
            {
                ((byte*)dest + num2)[j] = ((byte*)src + num2)[j];
            }
            catch
            {
                break;
            }
        }
    }
}
