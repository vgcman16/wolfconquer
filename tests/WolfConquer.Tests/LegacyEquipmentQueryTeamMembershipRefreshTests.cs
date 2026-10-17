using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task DirectQueryTeamHpThirdRealJoinCannotReplayExistingMembersPreWoundConstructorHitpoints()
    {
        using var fixture = new DirectQueryTeamHpFixture(includeThirdJoin: true);
        var original = await fixture.JoinTeamAsync();
        Assert.Equal(300, fixture.Owner.Character.MinHitPoints);
        var woundStart = fixture.Order.Count;
        await fixture.WoundOwnerAsync(); // No equipment query between the actual wound and third join.
        Assert.Equal(180, fixture.Owner.Character.MinHitPoints);
        var wound = fixture.Order.ToArray().Skip(woundStart).Where(frame => frame.Recipient == PlayerUid)
            .Select(frame => frame.Bytes).ToArray();
        var animation = Assert.Single(ParseSpellAnimations(wound), packet => packet.SpellId == ThunderSpellId);
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, hit.Uid);
        Assert.Equal(120U, hit.Damage); // Source MagicAttack1 + raw119.
        var joinStart = fixture.Order.Count;
        await fixture.JoinThirdMemberAsync();
        var frame = Assert.Single(fixture.Order.ToArray().Skip(joinStart), entry => entry.Recipient == PlayerUid &&
            LegacyTeamMemberInfoPacket.TryParse(ReadFrame(entry.Bytes), out var packet) && packet.Count == 3);
        Assert.True(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(frame.Bytes), out var joined));
        var wounded = Assert.Single(joined.Entries, entry => entry.Uid == PlayerUid);
        Assert.Equal(180U, wounded.MinHitpoints); // Never stale constructor300 after a real receive.
        Assert.Equal(original.Name, wounded.Name);
        Assert.Equal(original.Mesh, wounded.Mesh);
        Assert.Equal(PlayerUid, wounded.Uid);
        Assert.Equal(180, fixture.Owner.Character.MinHitPoints);
        // Existing-member Max presentation remains the explicit prior-compatibility
        // projection until all real HP setter owners maintain source MemberInfo.
        // Do not seed a Max attribute or assert a fake source500 proof here.
    }
}
