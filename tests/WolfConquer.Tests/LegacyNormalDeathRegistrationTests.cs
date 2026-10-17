using System.Collections.Concurrent;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData((ushort)10_420, 0x66, "LegacyAuraExpiresAtUtc")]
    [InlineData((ushort)11_200, 0x80, "LegacyAuraMagicDefenderExpiresAtUtc")]
    [InlineData((ushort)1_025, 0x12, "LegacySupermanExpiresAtUtc")]
    [InlineData((ushort)1_110, 0x17, "LegacyCycloneExpiresAtUtc")]
    [InlineData((ushort)6_001, 0x01, "LegacyPoisonedExpiresAtUtc")]
    [InlineData((ushort)10_470, 0x71, "LegacyShieldBlockExpiresAtUtc")]
    public async Task DirectNormalDeathRealRegistrationsClearBeforeQueryWithoutErasingIndependentFields(
        ushort spellId, int flag, string deadlineKey)
    {
        var armed = false;
        var queries = 0;
        Dictionary<string, string>? persistentAura = null;
        string? poisonLevel = null;
        using var fixture = new DirectNormalDeathFixture(targetHitpoints: 1,
            xpListReady: spellId is 1_025 or 1_110, onSend: (current, recipient, bytes) =>
            {
                if (!armed || recipient != TargetPlayerUid ||
                    !LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _) ||
                    Interlocked.Increment(ref queries) != 1)
                    return ValueTask.CompletedTask;
                Assert.Equal(1, current.Target.Character.MinHitPoints);
                Assert.False(HasLegacyStatusFlag(current.Target.Character.Attributes, 5));
                Assert.False(HasLegacyStatusFlag(current.Target.Character.Attributes, flag));
                Assert.False(current.Target.Character.Attributes.ContainsKey(deadlineKey));
                if (persistentAura is not null)
                    Assert.All(persistentAura, field => Assert.Equal(field.Value, current.Target.Character.Attributes[field.Key]));
                return ValueTask.CompletedTask;
            });
        // Real handler owns active flag/lifetime; only XPList is an entry prerequisite.
        if (spellId == 6_001) await fixture.CastPoisonAsync();
        else await fixture.CastStatusAsync(spellId);
        if (spellId == 6_001) poisonLevel = fixture.Target.Character.Attributes["PoisonLevel"];
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, flag));
        Assert.True(fixture.Target.Character.Attributes.ContainsKey(deadlineKey));
        if (spellId == 10_420)
            persistentAura = new[] { "LegacyAuraStatusFlag", "LegacyAuraSpellId", "LegacyAuraLevel",
                    "LegacyAuraDamage", "LegacyAuraGDamage" }
                .ToDictionary(key => key, key => fixture.Target.Character.Attributes[key]);
        armed = true;
        await fixture.KillAsync();
        Assert.Equal(1, queries);
        Assert.Equal(0, fixture.Target.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, flag));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey(deadlineKey));
        if (persistentAura is not null)
            Assert.All(persistentAura, field => Assert.Equal(field.Value, fixture.Target.Character.Attributes[field.Key]));
        if (poisonLevel is not null)
            Assert.Equal(poisonLevel, fixture.Target.Character.Attributes["PoisonLevel"]);
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectNormalDeathDragonSwingRetainsFalseCastButConsumesTrueDragonWarRegistration(bool dragonWar)
    {
        var armed = false;
        var queries = 0;
        string? deadline = null;
        using var fixture = new DirectNormalDeathFixture(targetHitpoints: 1, onSend: (current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid ||
                !LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _) ||
                Interlocked.Increment(ref queries) != 1)
                return ValueTask.CompletedTask;
            Assert.Equal(1, current.Target.Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(current.Target.Character.Attributes, 5));
            Assert.Equal(!dragonWar, HasLegacyStatusFlag(current.Target.Character.Attributes, 0xA0));
            Assert.Equal(!dragonWar, current.Target.Character.Attributes.ContainsKey("LegacyDragonSwingExpiresAtUtc"));
            return ValueTask.CompletedTask;
        });
        if (dragonWar)
        {
            var tournament = new LegacyDragonWarRuntimeStateStore();
            tournament.Open(fixture.UtcNow);
            Assert.True(tournament.TryJoin(fixture.Target, out var joined));
            var started = tournament.CreateDueRuntimeUpdates(fixture.UtcNow.AddSeconds(61), [joined]);
            var earned = Assert.Single(started.SessionUpdates).Session;
            Assert.True(HasLegacyStatusFlag(earned.Character.Attributes, 0xA0));
            fixture.KeepOrdinaryDeathPosition(earned);
            Assert.Equal("5", fixture.Target.Character.Attributes["DragonWarHits"]);
        }
        else
        {
            await fixture.CastStatusAsync(12_200);
            Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0xA0));
        }
        Assert.True(LegacyStatusFlagDeathRegistration.TryRead(fixture.Target.Character, 0xA0, out var remove));
        Assert.Equal(dragonWar, remove);
        deadline = fixture.Target.Character.Attributes["LegacyDragonSwingExpiresAtUtc"];
        armed = true;
        await fixture.KillAsync();
        Assert.Equal(1, queries);
        Assert.Equal(0, fixture.Target.Character.MinHitPoints);
        Assert.Equal(!dragonWar, HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0xA0));
        if (dragonWar)
        {
            Assert.False(fixture.Target.Character.Attributes.ContainsKey(LegacyStatusFlagDeathRegistration.Key(0xA0)));
            Assert.False(fixture.Target.Character.Attributes.ContainsKey("LegacyDragonSwingExpiresAtUtc"));
            Assert.Equal("5", fixture.Target.Character.Attributes["DragonWarHits"]); // Independent source field survives.
        }
        else
        {
            Assert.Equal(deadline, fixture.Target.Character.Attributes["LegacyDragonSwingExpiresAtUtc"]);
            Assert.Equal("false", fixture.Target.Character.Attributes[LegacyStatusFlagDeathRegistration.Key(0xA0)]);
        }
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectNormalDeathFreezeFalseRoundPolicySurvivesQueryButBothPoliciesRemoveAfterDead(bool joinedPolicy)
    {
        var armed = false;
        var queries = 0;
        var flagsAtDead = new ConcurrentQueue<bool>();
        using var fixture = new DirectNormalDeathFixture(targetHitpoints: 1, onSend: (current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid) return ValueTask.CompletedTask;
            if (LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _) &&
                Interlocked.Increment(ref queries) == 1)
            {
                Assert.Equal(1, current.Target.Character.MinHitPoints);
                Assert.Equal(!joinedPolicy, HasLegacyStatusFlag(current.Target.Character.Attributes, 0x3B));
                Assert.False(HasLegacyStatusFlag(current.Target.Character.Attributes, 5));
            }
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var vector) &&
                vector.Uid == TargetPlayerUid && vector.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { } values &&
                    IsLegacyStatusFlagSet(values, 5)))
                flagsAtDead.Enqueue(HasLegacyStatusFlag(current.Target.Character.Attributes, 0x3B));
            return ValueTask.CompletedTask;
        });
        if (joinedPolicy)
        {
            var tournament = new LegacyFreezeWarRuntimeStateStore();
            tournament.Open(fixture.UtcNow);
            Assert.True(tournament.TryJoin(fixture.Target, out var joined, out _));
            fixture.KeepOrdinaryDeathPosition(joined);
        }
        else
        {
            fixture.BindTarget(fixture.Target with
            {
                Character = LegacyFreezeWarRuntimeStateStore.MarkRoundFrozen(fixture.Target.Character),
            });
        }
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0x3B));
        Assert.True(LegacyStatusFlagDeathRegistration.TryRead(fixture.Target.Character, 0x3B, out var remove));
        Assert.Equal(joinedPolicy, remove);
        armed = true;
        await fixture.KillAsync();
        Assert.Equal(1, queries);
        Assert.NotEmpty(flagsAtDead);
        var deadSnapshots = flagsAtDead.ToArray();
        Assert.Equal(joinedPolicy ? 1 : 2, deadSnapshots.Length);
        Assert.Equal(!joinedPolicy, deadSnapshots[0]);
        Assert.False(deadSnapshots[^1]);
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0x3B));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey(LegacyStatusFlagDeathRegistration.Key(0x3B)));
        Assert.Equal(0, fixture.Target.Character.MinHitPoints);
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    [Fact]
    public async Task DirectNormalDeathRealDynamicFlowKnackRegistrationRoundTripsIntoNormalDeath()
    {
        const int flag = 0x29 | 0x40 | 0x100;
        var procs = 0;
        using var fixture = new DirectNormalDeathFixture(targetHitpoints: 1, passiveFlowKnack: true,
            onSend: (current, recipient, bytes) =>
            {
                if (recipient == TargetPlayerUid &&
                    LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                    animation.SpellId == 20_210 && animation.AttackerUid == TargetPlayerUid)
                {
                    Interlocked.Increment(ref procs);
                    Assert.Equal(1, current.Target.Character.MinHitPoints);
                    Assert.True(HasLegacyStatusFlag(current.Target.Character.Attributes, flag));
                    Assert.True(LegacyStatusFlagDeathRegistration.TryRead(current.Target.Character, flag, out var remove) && remove);
                }
                return ValueTask.CompletedTask;
            });
        await fixture.KillAsync(); // Receive opening proc precedes the same real lethal receive.
        Assert.True(procs > 0);
        Assert.Equal(0, fixture.Target.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, flag));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey(LegacyStatusFlagDeathRegistration.Key(flag)));
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DirectNormalDeathAddFlagDuplicatePreservesFirstSourcePolicy(bool initial, bool duplicate)
    {
        var key = LegacyStatusFlagDeathRegistration.Key(0xA0);
        var before = CreateSession(attributes: new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(0xA0),
            [key] = initial ? "true" : "false",
        }).Character;
        var attempted = before with
        {
            Attributes = new Dictionary<string, string>(before.Attributes)
            {
                ["IndependentPower"] = "42", [key] = duplicate ? "true" : "false",
            },
        };
        var after = LegacyStatusFlagDeathRegistration.CaptureAddFlag(before, attempted, 0xA0, duplicate);
        Assert.True(LegacyStatusFlagDeathRegistration.TryRead(after, 0xA0, out var retained));
        Assert.Equal(initial, retained);
        Assert.Equal(attempted with { Attributes = after.Attributes }, after);
        Assert.Equal(attempted.Attributes.Where(entry => entry.Key != key).OrderBy(entry => entry.Key),
            after.Attributes.Where(entry => entry.Key != key).OrderBy(entry => entry.Key));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectNormalDeathAddFlagDuplicateWithUnknownPreimageDoesNotFabricatePolicy(bool attemptedPolicy)
    {
        var key = LegacyStatusFlagDeathRegistration.Key(0xA0);
        var before = CreateSession(attributes: new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(0xA0), ["IndependentPower"] = "19",
        }).Character;
        var attempted = before with
        {
            Attributes = new Dictionary<string, string>(before.Attributes)
            {
                ["IndependentPower"] = "42", [key] = attemptedPolicy ? "true" : "false",
            },
        };
        var after = LegacyStatusFlagDeathRegistration.CaptureAddFlag(before, attempted, 0xA0, attemptedPolicy);
        Assert.False(LegacyStatusFlagDeathRegistration.TryRead(after, 0xA0, out _));
        Assert.False(after.Attributes.ContainsKey(key));
        Assert.Equal(attempted with { Attributes = after.Attributes }, after);
        Assert.Equal(attempted.Attributes.Where(entry => entry.Key != key).OrderBy(entry => entry.Key),
            after.Attributes.Where(entry => entry.Key != key).OrderBy(entry => entry.Key));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DirectNormalDeathAddSpellRegistrationReplacesPriorPolicy(bool initial, bool replacement)
    {
        var key = LegacyStatusFlagDeathRegistration.Key(0xA0);
        var before = CreateSession(attributes: new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(0xA0), [key] = initial ? "true" : "false",
        }).Character;
        var after = LegacyStatusFlagDeathRegistration.CaptureAddSpellFlag(before, 0xA0, replacement);
        Assert.True(LegacyStatusFlagDeathRegistration.TryRead(after, 0xA0, out var stored));
        Assert.Equal(replacement, stored);
    }

    [Fact]
    public async Task DirectNormalDeathTrueDragonWarRemovalThenRealFalseCastDoesNotReuseOldPolicy()
    {
        using var fixture = new DirectNormalDeathFixture(targetHitpoints: 1);
        var tournament = new LegacyDragonWarRuntimeStateStore();
        tournament.Open(fixture.UtcNow);
        Assert.True(tournament.TryJoin(fixture.Target, out var joined));
        var earned = Assert.Single(tournament.CreateDueRuntimeUpdates(
            fixture.UtcNow.AddSeconds(61), [joined]).SessionUpdates).Session;
        fixture.KeepOrdinaryDeathPosition(earned);
        Assert.True(LegacyStatusFlagDeathRegistration.TryRead(fixture.Target.Character, 0xA0, out var first) && first);
        await fixture.CastStatusAsync(12_200); // Existing flag toggles OFF; source does not replace it here.
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0xA0));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey(LegacyStatusFlagDeathRegistration.Key(0xA0)));
        await fixture.CastStatusAsync(12_200); // Now absent: AddSpellFlag(false).
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0xA0));
        Assert.True(LegacyStatusFlagDeathRegistration.TryRead(fixture.Target.Character, 0xA0, out var second) && !second);
        var deadline = fixture.Target.Character.Attributes["LegacyDragonSwingExpiresAtUtc"];
        await fixture.KillAsync();
        Assert.Equal(0, fixture.Target.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0xA0));
        Assert.Equal(deadline, fixture.Target.Character.Attributes["LegacyDragonSwingExpiresAtUtc"]);
        Assert.Equal("false", fixture.Target.Character.Attributes[LegacyStatusFlagDeathRegistration.Key(0xA0)]);
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    [Fact]
    public async Task DirectNormalDeathIdleFalseCastThenDragonWarAddFlagRetainsPolicyAndDeadline()
    {
        using var fixture = new DirectNormalDeathFixture(targetHitpoints: 1);
        var tournament = new LegacyDragonWarRuntimeStateStore();
        tournament.Open(fixture.UtcNow);
        Assert.True(tournament.TryJoin(fixture.Target, out var joined));
        fixture.BindTarget(joined); // Idle: source normal12200 guard permits the real cast.
        await fixture.CastStatusAsync(12_200);
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0xA0));
        Assert.True(LegacyStatusFlagDeathRegistration.TryRead(fixture.Target.Character, 0xA0, out var first) && !first);
        var deadline = fixture.Target.Character.Attributes["LegacyDragonSwingExpiresAtUtc"];
        var started = tournament.CreateDueRuntimeUpdates(fixture.UtcNow.AddSeconds(61), [fixture.Target]);
        var retained = Assert.Single(started.SessionUpdates).Session;
        Assert.True(LegacyStatusFlagDeathRegistration.TryRead(retained.Character, 0xA0, out var afterStart) && !afterStart);
        Assert.Equal(deadline, retained.Character.Attributes["LegacyDragonSwingExpiresAtUtc"]);
        fixture.KeepOrdinaryDeathPosition(retained);
        await fixture.KillAsync();
        Assert.Equal(0, fixture.Target.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0xA0));
        Assert.Equal(deadline, fixture.Target.Character.Attributes["LegacyDragonSwingExpiresAtUtc"]);
        Assert.Equal(0, fixture.ScheduledTimers);
    }
}
