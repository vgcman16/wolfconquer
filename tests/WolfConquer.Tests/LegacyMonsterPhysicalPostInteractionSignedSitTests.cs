using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task MonsterPhysicalEnteredSitAssignsSignedCurrentHpWithoutNewDeadDecision()
    {
        var armed = false;
        var callbacks = 0;
        using var fixture = new MonsterPostInteractionFixture(1_000, maximum: 5_000,
            onSend: (current, recipient, bytes) =>
            {
                if (armed && recipient == TargetPlayerUid && LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
                    packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina))
                {
                    callbacks++;
                    Assert.Equal("80", current.Owner.Character.Attributes["Stamina"]);
                    Assert.Equal("250", current.Owner.Character.Attributes["Action"]);
                    Assert.Equal(1_000, current.Owner.Character.MinHitPoints);
                    current.ChangeVitals(100);
                }
                return ValueTask.CompletedTask;
            });
        await fixture.PrimeAsync(5_000);
        await fixture.JoinAsync();
        await fixture.SitAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        Assert.Equal(1, callbacks);
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        var owner = fixture.OwnerFramesSince(start);
        Assert.Equal(400, AssertSinglePhysicalAttack(owner, TargetPlayerUid).Query.Damage);
        Assert.Equal(-300, fixture.Owner.Character.MinHitPoints); // Source CURRENT signed int100 -=400 after the entered Sit await.
        Assert.Equal(77, fixture.Owner.Character.MinMana);
        Assert.Equal("0", fixture.Owner.Character.Attributes["Action"]);
        MonsterPostAssertNoDeath(fixture.Owner); // No new lethal choice/clamp after source already chose the Sit branch.
        Assert.Empty(ParseDeathAttacks(owner));
        Assert.DoesNotContain(owner, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        var hp = ParseMsgUpdates(owner).Where(packet => packet.Uid == TargetPlayerUid &&
            packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(2, hp.Length);
        var first = Assert.Single(hp[0].Updates);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, first.Type);
        Assert.Equal(5_000UL, first.Value);
        Assert.Collection(hp[1].Updates,
            max => { Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, max.Type); Assert.Equal(5_000UL, max.Value); },
            current => { Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, current.Type); Assert.Equal(18_446_744_073_709_551_316UL, current.Value); });
        // Source HitPoints setter uses unchecked ushort projection for team, then signed-long->ulong MsgUpdate.
        var teamFrames = frames.Select((frame, index) => (frame, index)).Where(item =>
            LegacyTeamMemberInfoPacket.TryParse(ReadFrame(item.frame.Bytes), out var team) &&
            team.Count == 1 && team.Entries.Count == 1 && team.Entries[0].Uid == TargetPlayerUid &&
            team.Entries[0].MaxHitpoints == 5_000 && team.Entries[0].MinHitpoints == 65_236).ToArray();
        Assert.Equal(2, teamFrames.Length);
        Assert.Equal(new[] { PlayerUid, TargetPlayerUid }.Order(), teamFrames.Select(item => item.frame.Recipient).Order());
        var firstHp = Array.FindIndex(frames, frame => frame.Recipient == TargetPlayerUid && DirectNormalDeathIsHp(frame.Bytes));
        Assert.True(firstHp >= 0);
        Assert.All(teamFrames, item => Assert.True(item.index < firstHp));
        Assert.Equal(0, fixture.ScheduledTimers);
    }
}
