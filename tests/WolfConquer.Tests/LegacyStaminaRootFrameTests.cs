using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData("-1")] [InlineData("not-a-number")]
    public async Task OwnedRootFrameMalformedStaminaStopsBeforeManualPrefixAuthorizationOrAnyOutput(string raw)
    {
        var inputs = OwnedRootMagicInputs("manual");
        var actor = inputs.Owner with { Character = inputs.Owner.Character with
        {
            Attributes = new Dictionary<string, string>(inputs.Owner.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["Stamina"] = raw },
        } };
        await OwnedEquipmentBehaviorFixture.RunAsync(actor, inputs.World, async f =>
        {
            // Actual typed251 earns the real equipment effects from the saved
            // issued rows/catalog. It never seeds cached effects or cast results.
            await f.SendAsync(PlayerUid, new MsgActionPacket { ObjId = PlayerUid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
            var owner = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid);
            Assert.Equal(202U, LegacyPhysicalStatusOverlay.ForOwner(owner).RightWeaponItemEffect);
            Assert.Equal(1U, LegacyPhysicalStatusOverlay.ForOwner(owner).RingItemEffect);
            Assert.Equal(200U, LegacyPhysicalStatusOverlay.ForOwner(owner).NecklaceItemEffect);
            Assert.Equal(raw, owner.Character.Attributes["Stamina"]);
            Assert.Equal(raw, f.Data.Loader.LoadByAccountId(PlayerUid)!.Attributes["Stamina"]);
            Assert.False(HasLegacyStatusFlag(owner.Character.Attributes, 9)); Assert.False(HasLegacyStatusFlag(owner.Character.Attributes, 8));
            Assert.Equal((ushort)0, owner.LegacyRandomSpell);
            var files = ProcFiles(f); int ownFrames = f.Frames(PlayerUid).Count, peerFrames = f.Frames(TargetPlayerUid).Count;
            var nativeStamina = owner.NativePlayer!.Stamina;
            await f.SendAsync(PlayerUid, new MsgAttackPacket(new MsgAttackQuery(PlayerUid, TargetPlayerUid, 832, 584,
                MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0, SpellId: 1000)).ToFrameBytes());
            Assert.Same(owner, f.Current(PlayerUid)); Assert.Same(peer, f.Current(TargetPlayerUid));
            Assert.Equal(ownFrames, f.Frames(PlayerUid).Count); Assert.Equal(peerFrames, f.Frames(TargetPlayerUid).Count);
            Assert.Equal(owner.Character.MinMana, f.Data.Loader.LoadByAccountId(PlayerUid)!.MinMana);
            Assert.Equal(checked((uint)owner.Character.MinMana), owner.NativePlayer.Mana);
            Assert.Equal(nativeStamina, owner.NativePlayer.Stamina); Assert.Null(owner.MagicCooldowns);
            Assert.Equal((ushort)0, owner.LegacyRandomSpell); ProcFilesUnchanged(f, files);
            Assert.False(f.Recovery.IsBlocked); Assert.True(f.Store.IsBoundSpellOwnerCurrent(owner.NativeGameClient!.OrdinarySpellBinding!));
        }, inputs.Peer);
    }
}
