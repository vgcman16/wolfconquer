using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyMonsterDeathDeferredMoneyTests
{
    [Fact]
    public void CallbackBackedMoneyProbeCannotReplaceThePublishedPlainSession()
    {
        using var fixture = new Fixture(49, [50, 200, 6_000, 50_000]);
        var before = fixture.Session;
        var fields = before.Character.Attributes.ToArray();
        var items = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
        var spells = PlayerSpellSnapshotLoader.ToLegacyBytes(before.PlayerSpells);
        var proficiencies = PlayerProficiencySnapshotLoader.ToLegacyBytes(before.PlayerProficiencies);
        var native = fixture.Context.Connection.NativeGameClient;
        Assert.True(fixture.Monsters.TryGetVisibleInstance(fixture.Monster, DateTimeOffset.UtcNow, out var monster));
        var floorUid = ReadFloorUidCounter();
        int callbacks = 0;
        fixture.Attributes.BeforeMoneyRead = _ => callbacks++;
        var instrumented = before with
        {
            Character = before.Character with { Attributes = fixture.Attributes }
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            fixture.Sessions.Set(fixture.Context.Connection, instrumented));

        Assert.Equal("experienceContinuationPendingRequiresOwner", error.Message);
        Assert.True(fixture.Sessions.TryGet(fixture.Context.Connection, out var current));
        Assert.Same(before, current);
        Assert.Equal(fields, before.Character.Attributes.ToArray());
        Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(current!.PlayerItems));
        Assert.Equal(spells, PlayerSpellSnapshotLoader.ToLegacyBytes(current.PlayerSpells));
        Assert.Equal(proficiencies, PlayerProficiencySnapshotLoader.ToLegacyBytes(current.PlayerProficiencies));
        Assert.Same(native, fixture.Context.Connection.NativeGameClient);
        Assert.True(fixture.Monsters.TryGetVisibleInstance(fixture.Monster, DateTimeOffset.UtcNow, out var afterMonster));
        Assert.Equal(monster, afterMonster);
        Assert.Equal(0, callbacks);
        Assert.Equal(0, fixture.Attributes.MoneyReads);
        Assert.Equal(0, fixture.Cells.Reads);
        AssertUidDelta(floorUid, 0);
        Assert.Empty(fixture.Sent);
        Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal(string.Empty, fixture.Log.ToString());
    }
}
