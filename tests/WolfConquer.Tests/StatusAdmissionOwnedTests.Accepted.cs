using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class StatusAdmissionOwnedTests
{
    [Theory]
    [InlineData((ushort)1015, 7, 0U)] [InlineData((ushort)1020, 8, 120U)]
    [InlineData((ushort)8002, 27, 40U)] [InlineData((ushort)8003, 27, 55U)]
    public Task ValidOwnedStatusRetainsPaidResourcesSelfTargetAndSuccessfulWireOrder(ushort id, int flag, uint durationDamage) =>
        F.StatusAdmissionRunAsync(id, id != 8003, async f =>
        {
            var peer = f.Current(Peer); var peerImage = File.ReadAllBytes(f.CharacterPath(Peer));
            var binding = Owner(f); bool observedCost = false;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != Actor || !bytes.SequenceEqual(Convert.FromHexString("0E00800808A0FA4E120408081060"))) return;
                observedCost = true;
                Assert.Equal(493L, f.Current().Character.MinMana); Assert.Equal(493U, binding.Player.Mana);
                Assert.Equal(493L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
                Assert.Equal("96", f.Current().Character.Attributes["Stamina"]);
            };
            try { await Cast(f, id); } finally { f.BeforeSend = null; }
            Assert.True(observedCost, f.Log.ToString()); Assert.Same(binding, Owner(f));
            Assert.Equal(493L, f.Current().Character.MinMana); Assert.Equal("96", f.Current().Character.Attributes["Stamina"]);
            Assert.True(Flag(f.Current().Character, flag)); Assert.False(Flag(f.Current().Character, LegacyRuntimeUpdateTicker.XpListFlag));
            Assert.True(Flag(f.Current().Character, 0x1FF)); Assert.Equal("37", f.Current().Character.Attributes["XPCount"]);
            Assert.Equal(Convert.FromHexString("0E00800808A0FA4E120408081060"), f.ActorFrames[0]);
            Assert.Equal(Convert.FromHexString("0F00800808A0FA4E1205080210ED03"), f.ActorFrames[1]);
            var animation = Assert.Single(Animations(f), packet => packet.SpellId == id);
            Assert.Equal(Actor, animation.AttackerUid); Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal((ushort)832, animation.X); Assert.Equal((ushort)587, animation.Y);
            var target = Assert.Single(animation.Targets); Assert.Equal(Actor, target.Uid);
            Assert.Equal(durationDamage, target.Damage); Assert.True(target.Hit);
            if (id != 8003)
            {
                var scalar = f.ActorFrames.FindIndex(bytes => bytes.SequenceEqual(Convert.FromHexString("0C00800808A0FA4E12020865")));
                var animationIndex = f.ActorFrames.FindIndex(bytes => LegacySpellAnimationPacket.TryParse(
                    OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.SpellId == id);
                Assert.True(scalar >= 2 && scalar < animationIndex);
            }
            Assert.Same(peer, f.Current(Peer)); Assert.Equal(peerImage, File.ReadAllBytes(f.CharacterPath(Peer)));
        });

    [Theory]
    [InlineData((ushort)1015, 7)] [InlineData((ushort)1020, 8)] [InlineData((ushort)8002, 27)]
    public Task ValidOwnedMapCostBypassRetainsResourcesAndSuccessfulStatusWire(ushort id, int flag) =>
        F.StatusAdmissionRunAsync(id, true, async f =>
        {
            var peer = f.Current(Peer);
            var peerImage = File.ReadAllBytes(f.CharacterPath(Peer));
            var binding = Owner(f);
            Assert.Equal((ushort)100, binding.Player.Stamina);
            await Cast(f, id);
            Assert.Same(binding, Owner(f));
            Assert.Equal(500L, f.Current().Character.MinMana); Assert.Equal(500U, binding.Player.Mana);
            Assert.Equal(500L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
            Assert.Equal("100", f.Current().Character.Attributes["Stamina"]);
            Assert.Equal((ushort)100, binding.Player.Stamina);
            Assert.True(Flag(f.Current().Character, flag)); Assert.False(Flag(f.Current().Character, LegacyRuntimeUpdateTicker.XpListFlag));
            Assert.True(Flag(f.Current().Character, 0x1FF)); Assert.Equal("37", f.Current().Character.Attributes["XPCount"]);
            Assert.DoesNotContain(f.ActorFrames, bytes => LegacyMsgUpdatePacket.TryParse(
                OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) &&
                update.Updates.Any(row => row.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana));
            var animation = Assert.Single(Animations(f), packet => packet.SpellId == id);
            Assert.Equal(Actor, animation.AttackerUid); Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal((ushort)832, animation.X); Assert.Equal((ushort)587, animation.Y);
            var target = Assert.Single(animation.Targets); Assert.Equal(Actor, target.Uid); Assert.True(target.Hit);
            Assert.Same(peer, f.Current(Peer)); Assert.Equal(peerImage, File.ReadAllBytes(f.CharacterPath(Peer)));
        }, map: 1039);

    [Fact]
    public Task ShieldStopsStaleContinuationWhenXpScalarCallbackPublishesAnotherHolder() =>
        F.StatusAdmissionRunAsync(1020, true, async f =>
        {
            const string expiry = "2030-01-01T00:00:00.0000000+00:00";
            var binding = Owner(f); bool installed = false;
            WorldSession? successor = null;
            Dictionary<string, byte[]>? filesAfterCallback = null;
            int actorFramesAtCallback = -1, peerFramesAtCallback = -1;
            (int Hitpoints, uint Mana, ushort Stamina) nativeAtCallback = default;
            f.BeforeSend = (uid, bytes) =>
            {
                if (installed || uid != Actor || !bytes.SequenceEqual(Convert.FromHexString("0C00800808A0FA4E12020865"))) return;
                installed = true; Assert.Equal(493U, binding.Player.Mana);
                var current = f.Current(); Assert.False(Flag(current.Character, LegacyRuntimeUpdateTicker.XpListFlag));
                f.Store.Set(f.Connection(Actor), F.StatusAdmissionInstallShield(current, expiry));
                successor = f.Current();
                Assert.NotSame(current, successor);
                nativeAtCallback = (binding.Player.HitPoints, binding.Player.Mana, binding.Player.Stamina);
                filesAfterCallback = f.Data.Files();
                actorFramesAtCallback = f.ActorFrames.Count;
                peerFramesAtCallback = f.Frames(Peer).Count;
            };
            try { await Cast(f, 1020); } finally { f.BeforeSend = null; }
            Assert.True(installed, f.Log.ToString()); Assert.Same(binding, Owner(f));
            Assert.True(Flag(f.Current().Character, 8)); Assert.False(Flag(f.Current().Character, LegacyRuntimeUpdateTicker.XpListFlag));
            Assert.Equal(expiry, f.Current().Character.Attributes["LegacyMagicShieldExpiresAtUtc"]);
            Assert.Equal(bool.FalseString, f.Current().Character.Attributes["LegacyMagicShieldRemoveOnDead"]);
            Assert.Equal("96", f.Current().Character.Attributes["Stamina"]);
            Assert.Equal(493L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
            Assert.Same(successor, f.Current());
            Assert.Equal(nativeAtCallback, (binding.Player.HitPoints, binding.Player.Mana, binding.Player.Stamina));
            Assert.Equal(493U, binding.Player.Mana); Assert.Equal((ushort)96, binding.Player.Stamina);
            Assert.NotNull(filesAfterCallback); f.Data.AssertFiles(filesAfterCallback!);
            Assert.DoesNotContain(Animations(f), packet => packet.SpellId == 1020);
            Assert.Empty(f.ActorFrames.Where(bytes => LegacyMsgUpdatePacket.TryParse(
                OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) &&
                update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.StatusFlag)));
            // The callback runs before its scalar enters the transport. The
            // scalar is the only subsequent actor frame; no stale vector or
            // success animation may follow a different published holder.
            var scalar = Assert.Single(f.ActorFrames.Skip(actorFramesAtCallback));
            Assert.Equal(Convert.FromHexString("0C00800808A0FA4E12020865"), scalar);
            Assert.Equal(peerFramesAtCallback, f.Frames(Peer).Count);
        });
}
