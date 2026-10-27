using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientSpellPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualFileAdmissionComparersCannotPublishNestedSpellMutations(bool metadata) => NativePacketTestThread.Run(() =>
    {
        using var files = new OwnedSpells();
        var image = Image(1, Row(77, 0, 10));
        files.Seed(image);
        var client = Client();
        using var packet = new LegacyNativePacket(128);
        var originalBytes = Enumerable.Repeat((byte)0xA5, packet.Capacity).ToArray();
        unsafe { Marshal.Copy(originalBytes, 0, (nint)packet.Memory, originalBytes.Length); }
        packet.Size = 123;
        packet.Seek(0);
        var comparer = new FileAdmissionComparer(metadata ? (ushort)0 : (ushort)77,
            () => client.MySpells!.Add(packet, 800));
        if (metadata)
        {
            files.Magic[77] = new Dictionary<ushort, LegacyNativeMagicType.Magic>(comparer)
                { [0] = new() };
        }
        else client.MySpells!.ClientSpells = new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>(comparer);
        var existing = SeedFileAdmissionSentinel(client);
        var map = client.MySpells!.ClientSpells;
        comparer.Armed = true;
        var file = files.Open(FileMode.Open);
        try
        {
            var error = Record.Exception(() => LegacyNativeServerDatabasePersistence.LoadClientSpellsOpenedFile(file, client));

            AssertFileAdmissionUnchanged(client, map, existing);
            Assert.True(comparer.Triggered);
            Assert.Equal(123, packet.Size);
            Assert.Equal(0, packet.Position);
            unsafe { Assert.Equal(originalBytes, new ReadOnlySpan<byte>(packet.Memory, packet.Capacity).ToArray()); }
            AssertClosed(files.FilePath);
            Assert.Equal(image, File.ReadAllBytes(files.FilePath));
            Assert.IsType<InvalidOperationException>(error);
        }
        finally { file.Close(); }
    });

    private sealed class FileAdmissionComparer(ushort triggerKey, Action callback) : IEqualityComparer<ushort>
    {
        internal bool Armed { get; set; }
        internal bool Triggered { get; private set; }
        public bool Equals(ushort left, ushort right) => left == right;
        public int GetHashCode(ushort key)
        {
            if (Armed && key == triggerKey)
            {
                Armed = false;
                Triggered = true;
                callback();
            }
            return key.GetHashCode();
        }
    }
}
