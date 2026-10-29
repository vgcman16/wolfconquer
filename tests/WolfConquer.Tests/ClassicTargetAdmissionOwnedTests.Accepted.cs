using WolfConquer.Core.World;
using WolfConquer.Core.Packets;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class ClassicTargetAdmissionOwnedTests
{
    [Theory]
    [InlineData((ushort)1075, 0x16)] [InlineData((ushort)1085, 7)]
    [InlineData((ushort)1090, 8)] [InlineData((ushort)1095, 9)]
    public async Task GenuineSelfPeerAndMonsterKeepCostsAndStatusProgressAnimationOrder(ushort id, int flag)
    {
        foreach (var kind in new[] { "self", "peer", "monster" })
            await F.ClassicTargetRunAsync(id, async (f, monster) =>
            {
                var target = kind == "self" ? Actor : kind == "peer" ? Peer : monster.Uid;
                var binding = Owner(f); bool observed = false;
                f.BeforeSend = (uid, bytes) =>
                {
                    if (uid != Actor || !bytes.SequenceEqual(Convert.FromHexString("0E00800808A0FA4E120408081060"))) return;
                    observed = true; Assert.Equal(493U, binding.Player.Mana);
                    Assert.Equal(493L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
                    Assert.Equal("96", f.Current().Character.Attributes["Stamina"]);
                };
                try { await Cast(f, id, target); } finally { f.BeforeSend = null; }
                Assert.True(observed, f.Log.ToString()); Owner(f);
                Assert.Equal(Convert.FromHexString("0E00800808A0FA4E120408081060"), f.ActorFrames[0]);
                Assert.Equal(Convert.FromHexString("0F00800808A0FA4E1205080210ED03"), f.ActorFrames[1]);
                if (kind == "monster") Assert.True(States(f).TryGetTimedStatusFlag(monster, flag, DateTimeOffset.UtcNow, out _));
                else Assert.True(Flag(f.Current(kind == "self" ? Actor : Peer).Character, flag));
                var status = f.ActorFrames.FindIndex(bytes => LegacyMsgUpdatePacket.TryParse(
                    OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.Uid == target &&
                    packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.StatusFlag));
                var progress = f.ActorFrames.FindIndex(bytes => LegacySpellPacket.TryParse(
                    OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.Id == id);
                var animation = f.ActorFrames.FindIndex(bytes => LegacySpellAnimationPacket.TryParse(
                    OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.SpellId == id);
                Assert.True(status >= 2 && status < progress && progress < animation, f.Log.ToString());
                Assert.Equal(45, Assert.Single(f.Current().PlayerSpells.Spells, row => row.Id == id).Experience);
                Assert.Equal(target, Assert.Single(Assert.Single(Animations(f)).Targets).Uid);
            });
    }

    [Fact]
    public Task ActiveMonsterShieldRetainsActualExpiryAndAcceptedEmptyAnimation() => F.ClassicTargetRunAsync(1090, async (f, monster) =>
    {
        var states = States(f); var now = DateTimeOffset.UtcNow;
        var seeded = states.ApplyTimedStatusFlag(monster, 8, TimeSpan.FromMinutes(2), now, false);
        Assert.True(seeded.Applied); await Cast(f, 1090, monster.Uid); Owner(f);
        Assert.True(states.TryGetTimedStatusFlag(monster, 8, DateTimeOffset.UtcNow, out var current));
        Assert.Equal(seeded.Status, current); Assert.Empty(Assert.Single(Animations(f)).Targets);
        Assert.Equal(493L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
        Assert.Equal(45, Assert.Single(f.Current().PlayerSpells.Spells).Experience);
        Assert.DoesNotContain(f.ActorFrames, bytes => LegacyMsgUpdatePacket.TryParse(
            OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.Uid == monster.Uid &&
            packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.StatusFlag));
    });
}
