using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task ActualNativeNinjaAlternatePublishesItsPreparedSkillsIntoAdmittedSession()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; var storage = RequireCoherent(f);
            await NativePacketTestThread.Run(() =>
            {
                var bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
                var oldBin = bin.GetValue(null); var oldTypes = LegacyNativeNinjaFile.gouyu_type;
                var oldImmortals = LegacyNativeNinjaFile.gouyu_immortals; var oldRanks = LegacyNativeNinjaRank.Ranks;
                var oldNinja = f.Native.MyNinja;
                var equipment = Assert.IsType<LegacyNativeEquipItemStorage>(f.Native.Equipment);
                var owned = new List<LegacyNativePacket>();
                try
                {
                    var queue = new ConcurrentQueue<LegacyNativePacket>();
                    for (int i = 0; i < 12; i++) { var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE); owned.Add(packet); queue.Enqueue(packet); }
                    bin.SetValue(null, queue);
                    LegacyNativeNinjaRank.Ranks = new(); LegacyNativeNinjaRank.Create();
                    LegacyNativeNinjaFile.gouyu_immortals = new();
                    LegacyNativeNinjaFile.gouyu_type = new() { [7] = new() { ID = 7, ItemID = 100, Level = 2, magic1 = 16310, magic2 = 16340 } };
                    var ninja = new LegacyNativeNinja(f.Native); f.Native.MyNinja = ninja;
                    Assert.True(ninja.Items.TryAdd(100, new LegacyNativeNinja.Item { ItemID = 100, Level = 2, Position = 1 }));
                    int first = f.Sent.Count;
                    ninja.Alternate();
                    Assert.NotSame(before, f.Current);
                    Assert.Equal(2, Assert.Single(f.Current.PlayerSpells.Spells, s => s.Id == 16310).Level);
                    Assert.Equal(2, Assert.Single(f.Current.PlayerSpells.Spells, s => s.Id == 16340).Level);
                    Assert.Equal(1111, Assert.Single(f.Current.PlayerSpells.Spells, s => s.Id == 6001).Experience);
                    Assert.Same(storage, f.Native.MySpells); RequireCoherent(f);
                    Assert.Same(equipment, f.Native.Equipment);
                    Assert.True(f.Sent.Count >= first + 2);
                }
                finally
                {
                    f.Native.MyNinja = oldNinja;
                    LegacyNativeNinjaRank.Ranks = oldRanks; LegacyNativeNinjaFile.gouyu_type = oldTypes!;
                    LegacyNativeNinjaFile.gouyu_immortals = oldImmortals!; bin.SetValue(null, oldBin);
                    foreach (var packet in owned) packet.Dispose();
                }
            });
        });
    }
}
