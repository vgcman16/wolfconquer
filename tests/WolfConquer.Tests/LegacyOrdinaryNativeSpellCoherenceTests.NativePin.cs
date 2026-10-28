using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("replacement")]
    [InlineData("unattached")]
    public async Task BoundClientCannotFallBackToUnboundMutationAfterSpellSlotReplacement(string fault)
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var current = f.Current; var original = RequireCoherent(f);
            var originalMap = original.ClientSpells; var originalRows = originalMap.ToArray();
            var other = new LegacyNativeSpellStorage(f.Native); var otherMap = other.ClientSpells;
            if (fault == "null") f.Native.MySpells = null;
            if (fault == "replacement") f.Native.MySpells = other;
            var rawSlot = f.Native.MySpells; var files = f.Files(); int sends = f.Sent.Count;
            await NativePacketTestThread.Run(() =>
            {
                using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                if (fault == "unattached")
                {
                    original.Add(packet, 6002, 1);
                    Assert.Contains(f.Current.PlayerSpells.Spells, row => row.Id == 6002);
                    RequireCoherent(f);
                    originalMap = original.ClientSpells; originalRows = originalMap.ToArray();
                    current = f.Current; sends = f.Sent.Count;
                }
                else Assert.Throws<InvalidOperationException>(() => original.Add(packet, 6002, 1));
                Assert.Throws<InvalidOperationException>(() => other.Add(packet, 6003, 1));
                Assert.Same(rawSlot, f.Native.MySpells); Assert.Same(originalMap, original.ClientSpells);
                Assert.Equal(originalRows, originalMap.ToArray()); Assert.Same(otherMap, other.ClientSpells); Assert.Empty(otherMap);
                Assert.Same(current, f.Current); Assert.Equal(sends, f.Sent.Count);
            });
            f.RequireFiles(files);
        });
    }
}
