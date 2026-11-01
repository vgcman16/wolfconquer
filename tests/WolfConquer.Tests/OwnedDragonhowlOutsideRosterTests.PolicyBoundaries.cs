using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlOutsideRosterTests
{
    // 10 * 2^59 = 5 * 2^60. Unsigned LEB128 is eight80 bytes then50;
    // the nested2176 update has12 bytes. No production encoder supplies this golden.
    private static readonly byte[] FiniteBoundaryXp =
        [22, 0, 128, 8, 8, 161, 250, 78, 18, 12, 8, 5, 16, 128, 128, 128, 128, 128, 128, 128, 128, 80];

    [Theory]
    [InlineData((ushort)17_490, PolicyInput.SafeFiniteBoundary, 5_764_607_523_034_234_880L)]
    [InlineData((ushort)17_650, PolicyInput.SafeFiniteBoundary, 5_764_607_523_034_234_880L)]
    [InlineData((ushort)17_490, PolicyInput.EligibleZero, 0L)]
    [InlineData((ushort)17_650, PolicyInput.EligibleZero, 0L)]
    [InlineData((ushort)17_490, PolicyInput.IneligibleNaN, 0L)]
    [InlineData((ushort)17_650, PolicyInput.IneligibleNaN, 0L)]
    [InlineData((ushort)17_490, PolicyInput.ValidDistinctPrecedence, 10L)]
    [InlineData((ushort)17_650, PolicyInput.ValidDistinctPrecedence, 10L)]
    public Task SafeFiniteAndZeroGainPoliciesPreserveRealRecipientsAndExistingPrecedence(
        ushort spell, PolicyInput input, long gain) => RunPolicy(spell, input, async c =>
        {
            var f = c.Fixture; var before = f.Current(Peer); var a = Binding(c, Actor); var p = Binding(c, Peer);
            var query = QueryIdentity(p); var stamp = a.Player.AttackStamp;
            var expected = ExpectedPolicyFiles(c, gain);
            await f.SendAsync(Actor, Physical(c));
            Assert.True(f.Store.TryGet(f.Connection(Actor), out _), Log(c));
            Assert.True(f.Store.TryGet(f.Connection(Peer), out var after), Log(c));
            Assert.Same(p, after!.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(a)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(p));
            if (gain == 0) Assert.Same(before, after);
            Assert.Same(query, QueryIdentity(p));
            Assert.Equal(10L, after.Character.Level); Assert.Equal(gain, after.Character.Experience);
            Assert.Equal((ushort)10, p.Player.Level); Assert.Equal((ulong)gain, p.Player.Experience);
            Assert.Equal(30L, after.Character.MinHitPoints); Assert.Equal(30, p.Player.HitPoints);
            Assert.Equal(20L, after.Character.MinMana); Assert.Equal(20U, p.Player.Mana);
            Assert.Same(before.EquipmentHitpointsStatus, after.EquipmentHitpointsStatus);
            Assert.Same(before.PhysicalStatusOverlay, after.PhysicalStatusOverlay);
            Assert.Equal(30U, p.Client.Status.MaxHitpoints); Assert.Equal(300U, p.Client.Status.MaxMana);
            Assert.Equal(493U, a.Player.Mana); Assert.Equal((ushort)95, a.Player.Stamina);
            Assert.Equal(51_000, a.Player.HitPoints); Assert.Equal(639_778_909U, a.Player.ConquerPoints);
            Assert.Equal(0UL, a.Player.Experience); Assert.NotEqual(stamp, a.Player.AttackStamp);
            Assert.False(a.Client.OnAutoAttack); Assert.False(p.Client.OnAutoAttack);
            Assert.False(c.Monsters.TryGetVisibleInstance(c.Targets[0], c.Clock.GetUtcNow(), out _));
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.False(File.Exists(c.SpellPath)); f.Data.AssertFiles(expected);
            var experience = f.Frames(Peer).Where(bytes => LegacyMsgUpdatePacket.TryParse(
                OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.Uid == Peer &&
                packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Experience)).ToArray();
            if (gain == 0) Assert.Empty(experience);
            else Assert.Equal(gain == 10 ? Exp10 : FiniteBoundaryXp, Assert.Single(experience));
            Assert.DoesNotContain(f.Frames(Peer), bytes =>
                OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode == LegacyPlayerStatusPacket.Opcode);
            var animationBytes = Assert.Single(f.ActorFrames.Where(bytes => IsAnimation(bytes, spell)));
            Assert.True(LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(animationBytes), out var animation));
            var target = Assert.Single(animation.Targets);
            Assert.Equal(c.Targets[0].Uid, target.Uid); Assert.Equal(4_200U, target.Damage); Assert.True(target.Hit);
        });

    private static Dictionary<string, byte[]> ExpectedPolicyFiles(Case c, long gain)
    {
        var f = c.Fixture; var files = f.Data.Files();
        void Set(uint uid, string before, string after)
        {
            string path = Path.GetRelativePath(f.Data.Root, f.CharacterPath(uid));
            string text = Encoding.UTF8.GetString(files[path]); string line = "\r\n" + before + "\r\n";
            Assert.Contains(line, text);
            files[path] = Encoding.UTF8.GetBytes(text.Replace(line, "\r\n" + after + "\r\n", StringComparison.Ordinal));
        }
        Set(Actor, "MinMana=500", "MinMana=493"); Set(Actor, "ConquerPoints=639778904", "ConquerPoints=639778909");
        if (gain != 0) Set(Peer, "Experience=0", "Experience=" + gain);
        return files;
    }

    [Fact]
    public void SelectedRateDataRejectsSameKeyCaseAliasesWithoutNormalizingOtherFields()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["UserExpRate"] = "1", ["userexprate"] = "2", ["Other"] = "UP", ["other"] = "low" };
        var before = fields.ToArray();
        Assert.Throws<InvalidDataException>(() => LegacyDragonhowlExperienceRateInputs.RequireSelected(fields));
        Assert.Equal(before, fields.ToArray());
        var empty = new Dictionary<string, string>(StringComparer.Ordinal) { ["Other"] = "UP", ["other"] = "low" };
        var emptyBefore = empty.ToArray();
        LegacyDragonhowlExperienceRateInputs.RequireSelected(empty); // No override: no new default/environment policy.
        Assert.Equal(emptyBefore, empty.ToArray());
    }
}
