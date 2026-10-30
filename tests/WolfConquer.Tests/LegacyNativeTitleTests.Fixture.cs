using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Metadata = WolfConquer.Core.Data.LegacyNativeTitleStorage;

namespace WolfConquer.Tests;

// Native title leaf methods only; no TitleStorage.CheckUpUser, Process handler,
// appearance stand-in, view callback, worker, socket pump or live server is used.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeTitleTests
{
    private sealed class Scope : IDisposable
    {
        private readonly Dictionary<uint, Metadata> priorTitles = Metadata.Titles;
        private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo priorUiCulture = CultureInfo.CurrentUICulture;
        internal Dictionary<uint, Metadata> Titles { get; } = new();
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativePacket Packet => Native.Packet;
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal Queue<byte[]> Queue => Native.Queue;

        internal Scope()
        {
            Metadata.Titles = Titles;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Reset();
        }

        internal Metadata Row(uint key, uint id, uint sub = 2, uint score = 9)
        {
            var value = new Metadata { ID = id, SubID = sub, Score = score, Name = "Owned" };
            Titles.Add(key, value);
            return value;
        }

        internal unsafe void Reset()
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Packet.Size = 321;
            Packet.Seek(237);
        }

        internal unsafe void Input(byte[] bytes)
        {
            Reset();
            Marshal.Copy(bytes, 0, (IntPtr)Packet.Memory, bytes.Length);
            Packet.Size = bytes.Length;
        }

        internal unsafe byte[] Memory(int length)
        {
            var bytes = new byte[length];
            Marshal.Copy((IntPtr)Packet.Memory, bytes, 0, length);
            return bytes;
        }

        public void Dispose()
        {
            Metadata.Titles = priorTitles;
            Native.Dispose();
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUiCulture;
        }
    }

    private sealed class MetadataFile : IDisposable
    {
        private readonly string previousLocation = LegacyNativeServerConfig.DbLocation;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(),
            "wolf-native-title-db-" + Guid.NewGuid().ToString("N"));
        internal string PathName => Path.Combine(Root, "title_type.txt");

        internal MetadataFile()
        {
            CheckChain(Path.GetTempPath());
            if (Directory.Exists(Root) || File.Exists(Root))
                throw new IOException("Owned title fixture leaf already exists.");
            Directory.CreateDirectory(Root);
            LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar;
        }

        internal void Text(string text) => File.WriteAllText(PathName, text, new UTF8Encoding(false));

        private static void CheckChain(string path)
        {
            for (string? cursor = Path.GetFullPath(path); cursor is not null; cursor = Path.GetDirectoryName(cursor))
                if (Directory.Exists(cursor) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked title fixture rejected.");
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = previousLocation;
            string full = Path.GetFullPath(Root);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full),
                    Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)) ||
                !Regex.IsMatch(Path.GetFileName(full), "^wolf-native-title-db-[0-9a-f]{32}$"))
                throw new IOException("Title fixture cleanup containment failed.");
            CheckChain(full);
            foreach (string entry in Directory.GetFileSystemEntries(full))
                if (!StringComparer.OrdinalIgnoreCase.Equals(entry, PathName) ||
                    (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Unexpected title fixture entry; preserving files.");
            if (File.Exists(PathName)) File.Delete(PathName);
            Directory.Delete(full, recursive: false);
        }
    }

    // Independent writer for known positive Action values and protobuf uint fields,
    // anchored by Root's separate literal pinned controls. No native encoder is used.
    private static byte[] Frame(uint action, uint score, uint id, uint sub,
        bool title = true, uint titleValue = 0)
    {
        var payload = new List<byte>();
        UInt(payload, 1, action);
        UInt(payload, 2, score);
        UInt(payload, 3, id);
        UInt(payload, 4, sub);
        if (title)
        {
            var nested = new List<byte>();
            UInt(nested, 1, id);
            UInt(nested, 2, sub);
            UInt(nested, 3, titleValue);
            UInt(nested, 4, 0);
            payload.Add(0x2A);
            Varint(payload, (uint)nested.Count);
            payload.AddRange(nested);
        }
        return Wrap(payload.ToArray());
    }

    private static void UInt(List<byte> bytes, int tag, uint value)
    {
        bytes.Add((byte)(tag << 3));
        Varint(bytes, value);
    }

    private static void Varint(List<byte> bytes, uint value)
    {
        while (value > 127)
        {
            bytes.Add((byte)((value & 127) | 128));
            value >>= 7;
        }
        bytes.Add((byte)value);
    }

    private static byte[] Wrap(byte[] payload)
    {
        var frame = new byte[payload.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2313);
        payload.CopyTo(frame, 4);
        return frame;
    }

    private static void ListIs(Scope scope, params uint[] values) =>
        Assert.Equal(values, scope.Player.SpecialTitles.Select(value => (uint)value).ToArray());

    private static void Frames(Scope scope, params byte[][] expected)
    {
        byte[][] actual = scope.Queue.ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }

    private static void Untouched(Scope scope)
    {
        Assert.Empty(scope.Queue);
        Assert.Equal(237, scope.Packet.Position);
        Assert.Equal(321, scope.Packet.Size);
        Assert.All(scope.Memory(32), value => Assert.Equal((byte)0xA5, value));
    }
}
