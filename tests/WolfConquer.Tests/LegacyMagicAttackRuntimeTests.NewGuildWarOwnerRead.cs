using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public void NewGuildWarOwnerReadPreservesAbsentProducerAndReadsOnlyCurrentCanonicalState()
    {
        var sob = CreateGuildWarCapturePillarSobNpc(LegacySobNpcEventRules.GuildWarTenacityPillarUid, "TenacityPillar");
        var world = CreateMagicCatalogWithSobNpcs(CreateMonsterFamily(), [sob],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var states = new SobNpcRuntimeStateStore();
        Assert.True(states.TryCaptureMagicPrimaryTarget(sob, out var before));
        Assert.NotNull(before);
        Assert.True(states.TryReadMagicPrimaryOwnerGuildId(world, before.Visible, out var owner));
        Assert.Equal(0U, owner);
        // An actual state insertion would invalidate this absent-row observation.
        Assert.True(before.TryEnter(out var lease));
        using (lease) Assert.True(lease!.IsCurrentPlain());
        states.SetOwnerGuildId(sob, 77);
        var current = states.ReadMagicPrimaryVisible(sob);
        Assert.True(states.TryReadMagicPrimaryOwnerGuildId(world, current, out owner));
        Assert.Equal(77U, owner);
        states.ApplyDamage(sob, 1, new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero));
        Assert.False(states.TryReadMagicPrimaryOwnerGuildId(world, current, out owner));
        Assert.Equal(0U, owner);
        Assert.True(states.TryReadMagicPrimaryOwnerGuildId(world, states.ReadMagicPrimaryVisible(sob), out owner));
        Assert.Equal(77U, owner);
    }

    [Fact]
    public void NewGuildWarOwnerReadRefusesForeignBirthAndAmbiguousCanonicalMembers()
    {
        var sob = CreateGuildWarCapturePillarSobNpc(LegacySobNpcEventRules.GuildWarExorcimPillarUid, "ExorcimPillar");
        var world = CreateMagicCatalogWithSobNpcs(CreateMonsterFamily(), [sob],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var states = new SobNpcRuntimeStateStore();
        var copy = sob with { };
        _ = states.GetVisible(copy); // Declared malformed producer, never an accepting authority.
        Assert.False(states.TryCaptureMagicPrimaryTarget(sob, out _));
        Assert.False(states.TryReadMagicPrimaryOwnerGuildId(world, states.ReadMagicPrimaryVisible(sob), out var owner));
        Assert.Equal(0U, owner);
        var duplicateWorld = CreateMagicCatalogWithSobNpcs(CreateMonsterFamily(), [sob, sob],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        Assert.False(new SobNpcRuntimeStateStore().TryReadMagicPrimaryOwnerGuildId(duplicateWorld, sob, out owner));
        Assert.Equal(0U, owner);
    }
}
