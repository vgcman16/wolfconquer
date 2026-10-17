using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTransformStateTests
{
    private static readonly DateTimeOffset TransformExpiresAt =
        new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TransformSortIsClassifiedAsFullyModeledStateRuntime()
    {
        Assert.Equal(19, (int)LegacyMagicSort.Transform);

        var info = LegacyMagicSortCapabilities.Classify(
            LegacyMagicSort.Transform,
            spellId: 1_350);

        Assert.Equal(LegacyMagicSortCapability.StateAndBuffRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("transform-runtime", info.NextSeam);
        Assert.Contains("Transform.cs", info.SourceEvidence);
        Assert.Contains("Exact D13 packet builders", info.SourceEvidence);
        Assert.Contains("no Source7770", info.SourceEvidence);
    }

    [Fact]
    public void ApplyExposesTransformMaximumAndTryClearRemovesTransformMetadata()
    {
        var original = CreateCharacter(
            minHitPoints: 500,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "720",
                ["Unrelated"] = "preserved",
            });

        var transformed = LegacyTransformState.Apply(
            original,
            lookface: 207,
            maxHitpoints: 3_000,
            TransformExpiresAt);

        Assert.Equal(500, original.MinHitPoints);
        Assert.Equal(3_000, transformed.MinHitPoints);
        Assert.Equal("207", transformed.Attributes[LegacyTransformState.TransformationIdAttribute]);
        Assert.Equal("207", transformed.Attributes[LegacyTransformState.TransformationIdAliasAttribute]);
        Assert.Equal("1", transformed.Attributes[LegacyTransformState.ActiveAttribute]);
        Assert.Equal("1", transformed.Attributes[LegacyTransformState.ActiveAliasAttribute]);
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(transformed, out var activeMaximum));
        Assert.Equal(3_000U, activeMaximum);

        Assert.True(LegacyTransformState.TryClear(transformed, out var cleared));

        Assert.Equal("0", cleared.Attributes[LegacyTransformState.TransformationIdAttribute]);
        Assert.Equal("0", cleared.Attributes[LegacyTransformState.TransformationIdAliasAttribute]);
        Assert.Equal("preserved", cleared.Attributes["Unrelated"]);
        Assert.Equal("720", cleared.Attributes["MaxHitpoints"]);
        Assert.False(cleared.Attributes.ContainsKey(LegacyTransformState.ActiveAttribute));
        Assert.False(cleared.Attributes.ContainsKey(LegacyTransformState.ActiveAliasAttribute));
        Assert.False(cleared.Attributes.ContainsKey(LegacyTransformState.ExpiresAtUtcAttribute));
        Assert.False(cleared.Attributes.ContainsKey(LegacyTransformState.ExpiresAtUtcAliasAttribute));
        Assert.False(cleared.Attributes.ContainsKey(
            LegacyTransformState.TransformationExpiresAtUtcAliasAttribute));
        Assert.False(cleared.Attributes.ContainsKey(LegacyTransformState.MaxHitpointsAttribute));
        Assert.False(cleared.Attributes.ContainsKey(LegacyTransformState.MaxHitpointsAliasAttribute));
        Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(cleared, out _));
        Assert.False(LegacyTransformState.TryClear(cleared, out _));
    }

    [Fact]
    public void RuntimeMaxHitpointsHonorsActiveTransformMaximumExactly()
    {
        var transformed = LegacyTransformState.Apply(
            CreateCharacter(
                minHitPoints: 40_000,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MaxHitpoints"] = "50_000",
                }),
            lookface: 214,
            maxHitpoints: 3_000,
            TransformExpiresAt);

        var maximum = LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(
            transformed,
            currentHitpoints: 40_000,
            PlayerItemSnapshot.Empty,
            CombatGearSnapshot.Empty,
            catalog: null);

        Assert.Equal(3_000U, maximum);
    }

    [Fact]
    public async Task TransformExpiryIsStrictAndClampsToNormalMaximumAfterActualQuery()
    {
        var transformed = LegacyTransformState.Apply(
            CreateCharacter(
                minHitPoints: 500,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MaxHitpoints"] = "720",
                    ["Unrelated"] = "preserved",
                }),
            lookface: 207,
            maxHitpoints: 3_000,
            TransformExpiresAt);
        var session = CreateSession(transformed);

        Assert.False(WorldServerFrameHandler.TryPrepareLegacyTransformExpiration(
            session,
            worldData: null,
            TransformExpiresAt,
            LegacySignedMillisecondClock.System,
            out var equalTimestampQuery,
            out var equalTimestampMaximum));
        var equalTimestampSession = equalTimestampQuery.Session;
        Assert.Same(session, equalTimestampSession);
        Assert.Equal(0U, equalTimestampMaximum);

        Assert.True(WorldServerFrameHandler.TryPrepareLegacyTransformExpiration(
            session,
            worldData: null,
            TransformExpiresAt.AddTicks(1),
            LegacySignedMillisecondClock.System,
            out var stagedFinish,
            out var stagedMaximum));
        // Pure CAS preparation consumes only metadata: source query is later.
        Assert.Equal(3_000U, stagedMaximum);
        Assert.Equal(3_000, stagedFinish.Session.Character.MinHitPoints);
        var connection = new ConquerTcpConnection(null,
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 9958),
            sendAsync: (bytes, token) => ValueTask.CompletedTask);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, stagedFinish.Session);
        var context = new WorldPacketHandlerContext(connection, TextWriter.Null, stagedFinish.Session,
            Sessions: sessions, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var finished = await WorldPacketHandlers.PublishLegacyPlayerTransformFinishAsync(
            context, stagedFinish, CancellationToken.None);
        Assert.NotNull(finished.Query);
        Assert.True(finished.Query!.Value.Admitted);
        Assert.False(finished.TargetLost);
        var expiredSession = Assert.IsType<WorldSession>(finished.Session);
        Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(expiredSession, out var normalMaximum));

        Assert.Equal(720U, normalMaximum);
        Assert.Equal(720, expiredSession.Character.MinHitPoints);
        Assert.Equal("0", expiredSession.Character.Attributes[
            LegacyTransformState.TransformationIdAttribute]);
        Assert.Equal("preserved", expiredSession.Character.Attributes["Unrelated"]);
        Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(expiredSession.Character, out _));
    }

    private static CharacterSnapshot CreateCharacter(
        long minHitPoints,
        IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "TransformFixture",
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: 0,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: 1,
            Experience: 0,
            MapId: 1_002,
            X: 430,
            Y: 380,
            PreviousMapId: 1_002,
            PreviousMapX: 430,
            PreviousMapY: 380,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: minHitPoints,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://legacy-transform-state",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }

    private static WorldSession CreateSession(CharacterSnapshot character)
    {
        return new WorldSession(
            AccountId: character.Uid,
            Username: character.Name,
            character,
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }
}
