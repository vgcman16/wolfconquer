using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public Task HandlerRespondsToLegacyPlayerStatusQueryAfterWorldEnter() =>
        OwnedEquipmentBehaviorFixture.RunAsync(StatusQueryInput(), StatusQueryCatalog(), async owner =>
        {
            const uint uid = OwnedEquipmentBehaviorFixture.Actor;
            var session = owner.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(session.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(0, session.Character.Spirit);
            Assert.Equal(5_000U, LegacyPhysicalStatusOverlay.ForOwner(session).MaxMana);
            Assert.Equal(5_000U, LegacyEquipmentNativeMana.Item(Assert.Single(session.PlayerItems.EquipmentItems), StatusQueryCatalog()));
            Assert.Contains(owner.ActorFrames, bytes =>
                LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var startup) && startup.Uid == uid && startup.MaxMana == 5_000U);
            var before = owner.Data.Files();
            foreach (uint requested in new[] { uid, 0U, uid })
            {
                var start = owner.ActorFrames.Count;
                await owner.SendAsync(uid, new LegacyPlayerStatusQueryPacket(requested).ToFrameBytes());
                var bytes = Assert.Single(owner.ActorFrames.Skip(start));
                Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
                Assert.Equal(uid, status.Uid);
                Assert.Equal(61_512U, status.MaxHP);
                Assert.Equal(5_000U, status.MaxMana);
                Assert.Equal(0U, status.PrestigeLevel);
                Assert.Equal(5_000U, LegacyPhysicalStatusOverlay.ForOwner(owner.Current()).MaxMana);
                Assert.Equal(0U, status.ReservedZero);
                Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
            }
            var log = owner.Log.ToString();
            Assert.Contains("WorldServer handled LegacyPlayerStatus", log);
            Assert.Contains("uid=1293600", log);
            Assert.DoesNotContain("WorldServer received opcode 2301", log);
            owner.Data.AssertFiles(before);
        });

    private static WorldDataCatalog StatusQueryCatalog() => OwnedEquipmentBehaviorFixture.Catalog(
        OwnedEquipmentBehaviorFixture.Item(500_109, fields: new Dictionary<int, string>
        { [18] = "31676", [19] = "5000" }));

    private static OwnedEquipmentBehaviorInput StatusQueryInput()
    {
        var item = new PlayerItemSnapshotItem
        { Uid = 2001, ItemId = 500_109, Position = 4, Durability = 100, MaximumDurability = 100 };
        var chi = "1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/";
        var character = OwnedEquipmentBehaviorFixture.Character(minHitPoints: 61_229) with
        {
            Spirit = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["Dragon"] = chi, ["Pheonix"] = chi, ["Tiger"] = chi, ["Turtle"] = chi, ["ChiPoints"] = "153850" },
        };
        return new(character,
            new PlayerItemSnapshot([], [], [], [item], []));
    }
}
