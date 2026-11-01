using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlWornSuccessorTests
{
    [Fact]
    public Task DefensiveRuneArrayEditDuringActualFlashingKeepsRealCapturedRowsAndOriginal582Hit() => Run(false, async f =>
    {
        var actor = f.Current(Actor).NativeGameClient!.OrdinarySpellBinding!;
        var target = f.Current(Target).NativeGameClient!.OrdinarySpellBinding!;
        var alias = WornAlias(actor);
        var images = f.Data.Files();
        var expected = images.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        var targetPath = Path.GetRelativePath(f.Data.Root, f.CharacterPath(Target));
        var text = CharacterSnapshotFileEncoding.Decode(expected[targetPath], out var encoding);
        const string token = "MinHitPoints=100000\r\n";
        Assert.Equal(1, text.Split(token, StringSplitOptions.None).Length - 1);
        expected[targetPath] = encoding.GetPreamble().Concat(encoding.GetBytes(
            text.Replace(token, "MinHitPoints=99418\r\n", StringComparison.Ordinal))).ToArray();
        var start = f.ActorFrames.Count;
        var observed = false;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != Actor || observed || !Stage(bytes, "flashing")) return;
            Assert.Equal(100_000, f.Current(Target).Character.MinHitPoints);
            var exposed = actor.Items.Rune.Objects;
            Assert.Same(alias, Assert.Single(exposed));
            exposed[0] = null!; // Only the returned array is changed.
            Assert.Same(alias, Assert.Single(actor.Items.Rune.Objects));
            Assert.Equal((ushort)103, alias.Position);
            observed = true;
        };
        try { await f.SendAsync(Actor, Request()); }
        finally { f.BeforeSend = null; }
        Assert.True(observed, f.Log.ToString());
        var bytes = Assert.Single(f.ActorFrames.Skip(start), IsAnimation);
        Assert.Equal((ushort)2239, OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation));
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(Target, hit.Uid); Assert.Equal(582U, hit.Damage);
        Assert.Equal(MsgAttackEffect.Penetration, hit.Effect);
        Assert.Equal(99_418, f.Current(Target).Character.MinHitPoints);
        Assert.Equal(99_418, target.Player.HitPoints);
        Assert.Equal((uint?)66, LegacyPhysicalStatusOverlay.ForOwner(f.Current(Actor)).Breakthrough);
        Assert.True(actor.Client.OnAutoAttack); Assert.False(f.Recovery.IsBlocked);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(actor)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(target));
        f.Data.AssertFiles(expected);
    });
}
