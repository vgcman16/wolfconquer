using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPlayerDeathStateTests
{
    [Fact]
    public void ApplyDeathRecordsTimestampLocationKillerAndDeathFlags()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var snapshot = CreateSnapshot(
            minHitPoints: 450,
            minMana: 120,
            maxMana: 500,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(0x15),
                ["MaxHitpoints"] = "900",
                ["HitPoints"] = "450",
                ["Unrelated"] = "keep",
            });

        var result = LegacyPlayerDeathState.ApplyDeath(
            snapshot,
            now,
            deadX: 321,
            deadY: 456,
            killerUid: 987_654,
            killerName: "Birdman");

        Assert.Equal(0, result.Character.MinHitPoints);
        Assert.Equal("0", result.Attributes["MinHitPoints"]);
        Assert.Equal("450", result.Attributes["HitPoints"]);
        Assert.True(result.State.HasDeadFlag);
        Assert.False(result.State.HasGhostFlag);
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(result.Attributes, 0x15));
        AssertOnlyStatusFlags(result.Attributes, LegacyPlayerDeathState.DeadStatusFlag);
        Assert.Equal(now, result.State.DeadStamp);
        Assert.Equal(now + TimeSpan.FromSeconds(1), result.State.GhostStamp);
        Assert.Equal(now + LegacyPlayerDeathState.DefaultReviveDelay, result.State.ReviveAllowedAt);
        Assert.Equal(321, result.State.DeadX);
        Assert.Equal(456, result.State.DeadY);
        Assert.Equal(987_654U, result.State.KillerUid);
        Assert.Equal("Birdman", result.State.KillerName);
        Assert.Equal(now, ParseTimestamp(result.Attributes[LegacyPlayerDeathState.DeadStampAttribute]));
        Assert.Equal(now + TimeSpan.FromSeconds(1), ParseTimestamp(result.Attributes[LegacyPlayerDeathState.GhostStampAttribute]));
        Assert.Equal("321", result.Attributes[LegacyPlayerDeathState.DeadXAttribute]);
        Assert.Equal("456", result.Attributes[LegacyPlayerDeathState.DeadYAttribute]);
        Assert.Equal("987654", result.Attributes[LegacyPlayerDeathState.DeadKillerUidAttribute]);
        Assert.Equal("Birdman", result.Attributes[LegacyPlayerDeathState.DeadKillerNameAttribute]);
        Assert.Equal("keep", result.Attributes["Unrelated"]);
    }

    [Fact]
    public void DictionaryDeathUsesSimpleAttributeStateAndPreservesUnrelatedAttributes()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BitVector"] = BuildStatusFlags(0x15),
            ["HitPoints"] = "200",
            ["Unrelated"] = "keep",
        };

        var result = LegacyPlayerDeathState.ApplyDeath(attributes, now, deadX: 10, deadY: 20);

        Assert.Equal("0", result.Attributes["HitPoints"]);
        Assert.True(result.HasDeadFlag);
        Assert.False(result.HasGhostFlag);
        Assert.True(result.Attributes.ContainsKey("BitVector"));
        Assert.False(result.Attributes.ContainsKey("StatusFlags"));
        Assert.Equal("keep", result.Attributes["Unrelated"]);
        Assert.False(result.Attributes.ContainsKey(LegacyPlayerDeathState.DeadKillerUidAttribute));
    }

    [Theory]
    [InlineData(321, 456, false, 321, 456)]
    [InlineData(0, 456, true, 410, 354)]
    [InlineData(321, 0, true, 410, 354)]
    [InlineData(0, 0, true, 410, 354)]
    public void ResolveDeadPositionMatchesLegacyGhostFallback(
        ushort recordedX,
        ushort recordedY,
        bool usesFallback,
        ushort resolvedX,
        ushort resolvedY)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [LegacyPlayerDeathState.DeadXAttribute] = recordedX.ToString(CultureInfo.InvariantCulture),
            [LegacyPlayerDeathState.DeadYAttribute] = recordedY.ToString(CultureInfo.InvariantCulture),
        };

        var result = LegacyPlayerDeathState.ResolveDeadPosition(attributes);

        Assert.Equal(usesFallback, result.UsesFallback);
        Assert.Equal(recordedX, result.RecordedX);
        Assert.Equal(recordedY, result.RecordedY);
        Assert.Equal(resolvedX, result.ResolvedX);
        Assert.Equal(resolvedY, result.ResolvedY);
        Assert.Equal(recordedX, result.InteractionX);
        Assert.Equal(recordedY, result.InteractionY);
    }

    [Fact]
    public void ResolveDeadPositionTreatsMissingOrInvalidAttributesAsZeroAndAllowsCustomFallback()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [LegacyPlayerDeathState.DeadXAttribute] = "not-a-coordinate",
        };

        var result = LegacyPlayerDeathState.ResolveDeadPosition(attributes, fallbackX: 88, fallbackY: 77);

        Assert.True(result.UsesFallback);
        Assert.Equal(0, result.RecordedX);
        Assert.Equal(0, result.RecordedY);
        Assert.Equal(88, result.ResolvedX);
        Assert.Equal(77, result.ResolvedY);
        Assert.Equal(0, result.InteractionX);
        Assert.Equal(0, result.InteractionY);
    }

    [Fact]
    public void ReviveIsBlockedBeforeLegacyDelay()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var dead = LegacyPlayerDeathState.ApplyDeath(
            CreateSnapshot(minHitPoints: 200, minMana: 50, maxMana: 300),
            now,
            deadX: 101,
            deadY: 202);

        var revive = LegacyPlayerDeathState.TryRevive(
            dead.Character,
            now + LegacyPlayerDeathState.DefaultReviveDelay - TimeSpan.FromMilliseconds(1));

        Assert.False(revive.Allowed);
        Assert.Equal(0, revive.Character.MinHitPoints);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(revive.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(revive.Attributes, LegacyPlayerDeathState.GhostStatusFlag));
        Assert.Equal(now + LegacyPlayerDeathState.DefaultReviveDelay, revive.State.ReviveAllowedAt);
        Assert.Equal(now, revive.State.DeadStamp);
        Assert.Equal(LegacyPlayerReviveEligibilityReason.WaitingForDelay, revive.State.Reason);
    }

    [Fact]
    public void ReviveAllowedAfterDelayClearsFlagsAndRestoresBoundedVitals()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var dead = LegacyPlayerDeathState.ApplyDeath(
            CreateSnapshot(
                minHitPoints: 200,
                minMana: 750,
                maxMana: 600,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MaxHitpoints"] = "900",
                    ["Unrelated"] = "keep",
                }),
            now,
            deadX: 101,
            deadY: 202,
            killerUid: 123);

        var revive = LegacyPlayerDeathState.TryRevive(
            dead.Character,
            now + TimeSpan.FromSeconds(21),
            new LegacyPlayerReviveOptions
            {
                MaximumHitpoints = 900,
                MaximumMana = 500,
            });

        Assert.True(revive.Allowed);
        Assert.Equal(900, revive.Character.MinHitPoints);
        Assert.Equal("900", revive.Attributes["MinHitPoints"]);
        Assert.Equal(500, revive.Character.MinMana);
        Assert.Equal("500", revive.Attributes["MinMana"]);
        Assert.Equal("100", revive.Attributes["Stamina"]);
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(revive.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(revive.Attributes, LegacyPlayerDeathState.GhostStatusFlag));
        Assert.False(revive.Attributes.ContainsKey("StatusFlags"));
        Assert.False(revive.Attributes.ContainsKey("BitVector"));
        Assert.True(revive.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
        Assert.True(revive.Attributes.ContainsKey(LegacyPlayerDeathState.GhostStampAttribute));
        Assert.Equal("0", revive.Attributes["TransformationID"]);
        Assert.Equal("keep", revive.Attributes["Unrelated"]);
        Assert.Equal("123", revive.Attributes[LegacyPlayerDeathState.DeadKillerUidAttribute]);
        Assert.Equal(LegacyPlayerReviveEligibilityReason.Ready, revive.State.Reason);
    }

    [Fact]
    public void ReviveEligibilityMetadataWaitsUntilStrictLegacyDelayExpires()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var dead = LegacyPlayerDeathState.ApplyDeath(
            CreateSnapshot(minHitPoints: 200, minMana: 50, maxMana: 300),
            now,
            deadX: 101,
            deadY: 202);

        var atBoundary = LegacyPlayerDeathState.EvaluateReviveEligibility(
            dead.Character,
            now + LegacyPlayerDeathState.DefaultReviveDelay);
        var afterBoundary = LegacyPlayerDeathState.EvaluateReviveEligibility(
            dead.Character,
            now + LegacyPlayerDeathState.DefaultReviveDelay + TimeSpan.FromTicks(1));

        Assert.False(atBoundary.ShouldPrompt);
        Assert.False(atBoundary.CanRequestRevive);
        Assert.True(atBoundary.IsDead);
        Assert.Equal(LegacyPlayerReviveEligibilityReason.WaitingForDelay, atBoundary.Reason);
        Assert.Equal(now, atBoundary.DeadStamp);
        Assert.Equal(now + LegacyPlayerDeathState.DefaultReviveDelay, atBoundary.ReviveAllowedAt);
        Assert.Equal(TimeSpan.Zero, atBoundary.RemainingDelay);

        Assert.True(afterBoundary.ShouldPrompt);
        Assert.True(afterBoundary.CanRequestRevive);
        Assert.True(afterBoundary.IsDead);
        Assert.Equal(LegacyPlayerReviveEligibilityReason.Ready, afterBoundary.Reason);
        Assert.Equal(now, afterBoundary.DeadStamp);
        Assert.Equal(now + LegacyPlayerDeathState.DefaultReviveDelay, afterBoundary.ReviveAllowedAt);
        Assert.Equal(TimeSpan.Zero, afterBoundary.RemainingDelay);
    }

    [Fact]
    public void ReviveEligibilityMetadataDoesNotPromptAlivePlayer()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var snapshot = CreateSnapshot(minHitPoints: 200, minMana: 50, maxMana: 300);

        var eligibility = LegacyPlayerDeathState.EvaluateReviveEligibility(snapshot, now);

        Assert.False(eligibility.ShouldPrompt);
        Assert.False(eligibility.CanRequestRevive);
        Assert.False(eligibility.IsDead);
        Assert.Equal(LegacyPlayerReviveEligibilityReason.Alive, eligibility.Reason);
        Assert.Null(eligibility.DeadStamp);
        Assert.Null(eligibility.ReviveAllowedAt);
        Assert.Equal(TimeSpan.Zero, eligibility.RemainingDelay);
    }

    [Fact]
    public void ReviveEligibilityMetadataAllowsDeadPlayerWithMissingDeadStamp()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var snapshot = CreateSnapshot(minHitPoints: 0, minMana: 50, maxMana: 300);

        var eligibility = LegacyPlayerDeathState.EvaluateReviveEligibility(snapshot, now);

        Assert.True(eligibility.ShouldPrompt);
        Assert.True(eligibility.CanRequestRevive);
        Assert.True(eligibility.IsDead);
        Assert.Equal(LegacyPlayerReviveEligibilityReason.Ready, eligibility.Reason);
        Assert.Null(eligibility.DeadStamp);
        Assert.Null(eligibility.ReviveAllowedAt);
        Assert.Equal(TimeSpan.Zero, eligibility.RemainingDelay);
    }

    [Fact]
    public void DeathPromptReadinessDoesNotPromptAlivePlayer()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var snapshot = CreateSnapshot(minHitPoints: 200, minMana: 50, maxMana: 300);

        var prompt = LegacyPlayerDeathState.EvaluateDeathPromptReadiness(snapshot, now);

        Assert.Equal(LegacyPlayerDeathPromptState.Alive, prompt.State);
        Assert.Equal(LegacyPlayerDeathPromptAction.None, prompt.Action);
        Assert.False(prompt.IsDead);
        Assert.False(prompt.ShouldEmitRevivePrompt);
        Assert.False(prompt.ShouldClearRevivePrompt);
        Assert.False(prompt.ShouldEmitPacket);
        Assert.Equal(LegacyPlayerReviveEligibilityReason.Alive, prompt.Eligibility.Reason);
    }

    [Fact]
    public void DeathPromptReadinessWaitsForReviveDelay()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var dead = LegacyPlayerDeathState.ApplyDeath(
            CreateSnapshot(minHitPoints: 200, minMana: 50, maxMana: 300),
            now,
            deadX: 101,
            deadY: 202);

        var prompt = LegacyPlayerDeathState.EvaluateDeathPromptReadiness(
            dead.Character,
            now + TimeSpan.FromSeconds(5));

        Assert.Equal(LegacyPlayerDeathPromptState.DeadWaitingForReviveDelay, prompt.State);
        Assert.Equal(LegacyPlayerDeathPromptAction.None, prompt.Action);
        Assert.True(prompt.IsDead);
        Assert.False(prompt.ShouldEmitRevivePrompt);
        Assert.False(prompt.ShouldClearRevivePrompt);
        Assert.False(prompt.ShouldEmitPacket);
        Assert.Equal(now, prompt.DeadStamp);
        Assert.Equal(now + LegacyPlayerDeathState.DefaultReviveDelay, prompt.ReviveAllowedAt);
        Assert.Equal(TimeSpan.FromSeconds(15), prompt.RemainingDelay);
        Assert.Equal(LegacyPlayerReviveEligibilityReason.WaitingForDelay, prompt.Eligibility.Reason);
    }

    [Fact]
    public void DeathPromptReadinessPromptsWhenDeadDelayExpired()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var dead = LegacyPlayerDeathState.ApplyDeath(
            CreateSnapshot(minHitPoints: 200, minMana: 50, maxMana: 300),
            now,
            deadX: 101,
            deadY: 202);

        var prompt = LegacyPlayerDeathState.EvaluateDeathPromptReadiness(
            dead.Character,
            now + LegacyPlayerDeathState.DefaultReviveDelay + TimeSpan.FromTicks(1));

        Assert.Equal(LegacyPlayerDeathPromptState.DeadReadyForRevive, prompt.State);
        Assert.Equal(LegacyPlayerDeathPromptAction.ShowRevivePrompt, prompt.Action);
        Assert.True(prompt.IsDead);
        Assert.True(prompt.ShouldEmitRevivePrompt);
        Assert.False(prompt.ShouldClearRevivePrompt);
        Assert.True(prompt.ShouldEmitPacket);
        Assert.Equal(now, prompt.DeadStamp);
        Assert.Equal(now + LegacyPlayerDeathState.DefaultReviveDelay, prompt.ReviveAllowedAt);
        Assert.Equal(TimeSpan.Zero, prompt.RemainingDelay);
        Assert.Equal(LegacyPlayerReviveEligibilityReason.Ready, prompt.Eligibility.Reason);
    }

    [Fact]
    public void DeathPromptReadinessTreatsMissingDeadStampAsReady()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var snapshot = CreateSnapshot(minHitPoints: 0, minMana: 50, maxMana: 300);

        var prompt = LegacyPlayerDeathState.EvaluateDeathPromptReadiness(snapshot, now);

        Assert.Equal(LegacyPlayerDeathPromptState.DeadReadyForRevive, prompt.State);
        Assert.Equal(LegacyPlayerDeathPromptAction.ShowRevivePrompt, prompt.Action);
        Assert.True(prompt.IsDead);
        Assert.True(prompt.ShouldEmitRevivePrompt);
        Assert.False(prompt.ShouldClearRevivePrompt);
        Assert.True(prompt.ShouldEmitPacket);
        Assert.Null(prompt.DeadStamp);
        Assert.Null(prompt.ReviveAllowedAt);
        Assert.Equal(TimeSpan.Zero, prompt.RemainingDelay);
        Assert.Equal(LegacyPlayerReviveEligibilityReason.Ready, prompt.Eligibility.Reason);
    }

    [Fact]
    public void DeathPromptReadinessClearsVisiblePromptAfterRevive()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var dead = LegacyPlayerDeathState.ApplyDeath(
            CreateSnapshot(
                minHitPoints: 200,
                minMana: 50,
                maxMana: 300,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MaxHitpoints"] = "900",
                }),
            now,
            deadX: 101,
            deadY: 202);
        var revived = LegacyPlayerDeathState.TryRevive(
            dead.Character,
            now + TimeSpan.FromSeconds(21),
            new LegacyPlayerReviveOptions
            {
                MaximumHitpoints = 900,
            });

        var prompt = LegacyPlayerDeathState.EvaluateDeathPromptReadiness(
            revived.Character,
            now + TimeSpan.FromSeconds(21),
            revivePromptVisible: true);

        Assert.True(revived.Allowed);
        Assert.Equal(LegacyPlayerDeathPromptState.Alive, prompt.State);
        Assert.Equal(LegacyPlayerDeathPromptAction.ClearRevivePrompt, prompt.Action);
        Assert.False(prompt.IsDead);
        Assert.False(prompt.ShouldEmitRevivePrompt);
        Assert.True(prompt.ShouldClearRevivePrompt);
        Assert.True(prompt.ShouldEmitPacket);
        Assert.Equal(LegacyPlayerReviveEligibilityReason.Alive, prompt.Eligibility.Reason);
    }

    [Theory]
    [InlineData(1003, 98)]
    [InlineData(1007, 99)]
    public void GhostTransitionAppliesAfterLegacyGhostStampAndSetsTransformation(long body, long transformationId)
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var dead = LegacyPlayerDeathState.ApplyDeath(
            CreateSnapshot(minHitPoints: 200, minMana: 50, maxMana: 300) with { Body = body },
            now,
            deadX: 101,
            deadY: 202);

        var before = LegacyPlayerDeathState.TryApplyGhost(dead.Character, now + TimeSpan.FromMilliseconds(999));
        var after = LegacyPlayerDeathState.TryApplyGhost(dead.Character, now + TimeSpan.FromSeconds(2));

        Assert.False(before.Applied);
        Assert.True(after.Applied);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(after.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(after.Attributes, LegacyPlayerDeathState.GhostStatusFlag));
        Assert.Equal(transformationId.ToString(CultureInfo.InvariantCulture), after.Attributes["TransformationID"]);
        Assert.Equal(transformationId, after.TransformationId);
        Assert.Equal(now + TimeSpan.FromSeconds(1), after.GhostStamp);
    }

    [Fact]
    public void ReviveDelayIsConfigurable()
    {
        var now = new DateTimeOffset(2026, 6, 11, 10, 15, 30, TimeSpan.Zero);
        var dead = LegacyPlayerDeathState.ApplyDeath(
            CreateSnapshot(minHitPoints: 200, minMana: 50, maxMana: 300),
            now,
            deadX: 101,
            deadY: 202,
            reviveDelay: TimeSpan.FromSeconds(5));
        var options = new LegacyPlayerReviveOptions
        {
            ReviveDelay = TimeSpan.FromSeconds(5),
            MaximumHitpoints = 300,
        };

        Assert.False(LegacyPlayerDeathState.TryRevive(dead.Character, now + TimeSpan.FromSeconds(5), options).Allowed);
        Assert.True(LegacyPlayerDeathState.TryRevive(dead.Character, now + TimeSpan.FromSeconds(6), options).Allowed);
    }

    private static CharacterSnapshot CreateSnapshot(
        long minHitPoints,
        long minMana,
        long maxMana,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var values = attributes is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase);
        values.TryAdd("MinHitPoints", minHitPoints.ToString(CultureInfo.InvariantCulture));
        values.TryAdd("MinMana", minMana.ToString(CultureInfo.InvariantCulture));

        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "Tester",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 142,
            FirstClass: 142,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            MapId: 1015,
            X: 300,
            Y: 400,
            PreviousMapId: 1015,
            PreviousMapX: 300,
            PreviousMapY: 400,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 300,
            AttributePoints: 0,
            MinHitPoints: minHitPoints,
            MinMana: minMana,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://character",
            Attributes: values,
            PaidNobility: new Dictionary<string, string>())
        {
            MaxMana = maxMana,
        };
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[10];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words) + "/";
    }

    private static void AssertOnlyStatusFlags(IReadOnlyDictionary<string, string> attributes, params int[] expectedFlags)
    {
        var raw = attributes.TryGetValue("StatusFlags", out var statusFlags)
            ? statusFlags
            : attributes["BitVector"];
        var expected = BuildStatusFlags(expectedFlags);

        Assert.Equal(expected, raw);
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
