using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBossStatusEffectLifecycleTests
{
    private const uint OwnerUid = 1_293_700;
    private const uint ViewerUid = 1_293_701;
    private const uint RecipientUid = 1_293_702;
    private const long MapId = 1_015;
    private const ushort OwnerX = 830;
    private const ushort OwnerY = 584;
    private const int FixtureNinjaClass = 5_051;
    private const int UnrelatedStatusFlag = 0x15;
    private const uint AcalanathaRuneItemId = 4_037_101;
    private const string StatusRollAttribute = "LegacyBossStatusRoll";
    private const string SecondaryStatusRollAttribute = "LegacyBossStatusSecondaryRoll";
    private const string AcalanathaRollAttribute = "LegacyBossAcalanathaRoll";
    private const string SentinelAttribute = "BossStatusSentinel";
    private const string ConcurrentMutationAttribute = "BossStatusConcurrentMutation";

    private static readonly DateTimeOffset RuntimeNow =
        new(2026, 7, 13, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ControlStatusLifetimeUsesFrightenedAndCanonicalDustDizzyKeys()
    {
        Assert.Equal(
            "LegacyFrightenedExpiresAtUtc",
            LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute);
        Assert.Equal(
            "LegacyFrightenedRemoveOnDead",
            LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute);
        Assert.Equal(
            "LegacyDustSigilStunnExpiresAtUtc",
            LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute);
        Assert.Equal(
            "LegacyDustSigilStunnRemoveOnDead",
            LegacyControlStatusLifetime.DizzyRemoveOnDeadAttribute);

        Assert.Equal(
            LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute,
            LegacyControlStatusLifetime.GetExpiresAtUtcAttribute(
                LegacyBossStatusEffect.Frightened(durationSeconds: 6)));
        Assert.Equal(
            LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute,
            LegacyControlStatusLifetime.GetRemoveOnDeadAttribute(
                LegacyBossStatusEffect.Frightened(durationSeconds: 6)));
        Assert.Equal(
            LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute,
            LegacyControlStatusLifetime.GetExpiresAtUtcAttribute(
                LegacyBossStatusEffect.Dizzy(durationSeconds: 3)));
        Assert.Equal(
            LegacyControlStatusLifetime.DizzyRemoveOnDeadAttribute,
            LegacyControlStatusLifetime.GetRemoveOnDeadAttribute(
                LegacyBossStatusEffect.Dizzy(durationSeconds: 3)));
    }

    [Theory]
    [InlineData(true, 6U)]
    [InlineData(false, 3U)]
    public void TryStartBossStatusLifetimeUsesSuppliedNowAndPreservesUnrelatedState(
        bool frightened,
        uint durationSeconds)
    {
        var now = new DateTimeOffset(2026, 7, 14, 1, 2, 3, TimeSpan.FromHours(5.5));
        var effect = frightened
            ? LegacyBossStatusEffect.Frightened(durationSeconds)
            : LegacyBossStatusEffect.Dizzy(durationSeconds);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(UnrelatedStatusFlag),
            [SentinelAttribute] = "preserve-me",
        };
        var character = CreateSession(OwnerUid, "Owner", OwnerX, OwnerY, attributes: attributes).Character;

        var started = WorldPacketHandlers.TryStartLegacyBossStatusLifetime(
            character,
            effect,
            now,
            out var updatedCharacter);

        Assert.True(started);
        Assert.True(HasLegacyStatusFlag(updatedCharacter.Attributes, effect.StatusFlag));
        Assert.True(HasLegacyStatusFlag(updatedCharacter.Attributes, UnrelatedStatusFlag));
        Assert.Equal("preserve-me", updatedCharacter.Attributes[SentinelAttribute]);
        Assert.Equal(
            now.ToUniversalTime().AddSeconds(durationSeconds),
            ReadExpiry(
                updatedCharacter.Attributes,
                LegacyControlStatusLifetime.GetExpiresAtUtcAttribute(effect)));
        Assert.Equal(
            bool.TrueString,
            updatedCharacter.Attributes[LegacyControlStatusLifetime.GetRemoveOnDeadAttribute(effect)]);
    }

    [Theory]
    [InlineData(true, 6U)]
    [InlineData(false, 3U)]
    public void TryStartBossStatusLifetimeDoesNotRefreshAnActiveSameFlag(
        bool frightened,
        uint durationSeconds)
    {
        var effect = frightened
            ? LegacyBossStatusEffect.Frightened(durationSeconds)
            : LegacyBossStatusEffect.Dizzy(durationSeconds);
        var expiresAtAttribute = LegacyControlStatusLifetime.GetExpiresAtUtcAttribute(effect);
        var removeOnDeadAttribute = LegacyControlStatusLifetime.GetRemoveOnDeadAttribute(effect);
        var originalExpiresAt = RuntimeNow.AddMinutes(5);
        var attributes = CreateTimedStatusAttributes(
            effect.StatusFlag,
            expiresAtAttribute,
            removeOnDeadAttribute,
            originalExpiresAt,
            includeStatusFlag: true);
        var character = CreateSession(OwnerUid, "Owner", OwnerX, OwnerY, attributes: attributes).Character;

        var started = WorldPacketHandlers.TryStartLegacyBossStatusLifetime(
            character,
            effect,
            RuntimeNow,
            out var updatedCharacter);

        Assert.False(started);
        Assert.Same(character, updatedCharacter);
        Assert.Equal(originalExpiresAt, ReadExpiry(updatedCharacter.Attributes, expiresAtAttribute));
        Assert.Equal(bool.TrueString, updatedCharacter.Attributes[removeOnDeadAttribute]);
        Assert.Equal("preserve-me", updatedCharacter.Attributes[SentinelAttribute]);
    }

    [Fact]
    public async Task FrightenedExpiryHonorsExactBoundaryAndRoutesOwnerViewerThenOwnerOnlyType54()
    {
        Assert.Equal(54U, (uint)LegacyGameUpdateDataType.Flustered);
        var expiresAt = RuntimeNow.AddSeconds(6);
        using var log = new StringWriter();
        var sent = new List<SentFrame>();
        var ownerConnection = CreateTracingConnection("owner", sent);
        var viewerConnection = CreateTracingConnection("viewer", sent);
        var farConnection = CreateTracingConnection("far", sent);
        var handler = CreateHandler(log);
        var owner = CreateSession(
            OwnerUid,
            "Owner",
            OwnerX,
            OwnerY,
            attributes: CreateTimedStatusAttributes(
                LegacyBossStatusEffect.FrightenedFlag,
                LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute,
                LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute,
                expiresAt,
                includeStatusFlag: true));
        var viewer = CreateSession(
            ViewerUid,
            "Viewer",
            checked((ushort)(OwnerX - 1)),
            OwnerY);
        var far = CreateSession(
            ViewerUid + 1,
            "Far",
            checked((ushort)(OwnerX + 100)),
            OwnerY);
        SetHandlerSession(handler, ownerConnection, owner);
        SetHandlerSession(handler, viewerConnection, viewer);
        SetHandlerSession(handler, farConnection, far);

        await InvokeExpirationAsync(
            handler,
            "SendDueLegacyBossFrightenedExpirationsAsync",
            expiresAt.AddTicks(-1));

        var beforeExpiry = AssertHandlerSession(handler, ownerConnection);
        Assert.True(HasLegacyStatusFlag(
            beforeExpiry.Character.Attributes,
            LegacyBossStatusEffect.FrightenedFlag));
        Assert.Equal(
            expiresAt,
            ReadExpiry(
                beforeExpiry.Character.Attributes,
                LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute));
        Assert.Empty(SnapshotSentFrames(sent));

        await InvokeExpirationAsync(
            handler,
            "SendDueLegacyBossFrightenedExpirationsAsync",
            expiresAt);

        var expired = AssertHandlerSession(handler, ownerConnection);
        Assert.False(HasLegacyStatusFlag(
            expired.Character.Attributes,
            LegacyBossStatusEffect.FrightenedFlag));
        Assert.True(HasLegacyStatusFlag(expired.Character.Attributes, UnrelatedStatusFlag));
        Assert.False(expired.Character.Attributes.ContainsKey(
            LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute));
        Assert.False(expired.Character.Attributes.ContainsKey(
            LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute));
        Assert.Equal("preserve-me", expired.Character.Attributes[SentinelAttribute]);

        Assert.Collection(
            SnapshotSentFrames(sent),
            frame =>
            {
                Assert.Equal("owner", frame.Recipient);
                AssertStatusVectorFrame(
                    frame.Bytes,
                    OwnerUid,
                    expectedSet: [UnrelatedStatusFlag],
                    expectedClear: [LegacyBossStatusEffect.FrightenedFlag]);
            },
            frame =>
            {
                Assert.Equal("viewer", frame.Recipient);
                AssertStatusVectorFrame(
                    frame.Bytes,
                    OwnerUid,
                    expectedSet: [UnrelatedStatusFlag],
                    expectedClear: [LegacyBossStatusEffect.FrightenedFlag]);
            },
            frame =>
            {
                Assert.Equal("owner", frame.Recipient);
                AssertGameUpdateFrame(
                    frame.Bytes,
                    OwnerUid,
                    LegacyGameUpdateDataType.Flustered,
                    expectedActive: false,
                    expectedTime: 0);
            });
        Assert.Contains("WorldServer expired legacy boss Frightened flags", log.ToString());
    }

    [Fact]
    public async Task FrightenedMetadataOnlyExpiryCleansStaleStateSilently()
    {
        var expiresAt = RuntimeNow.AddSeconds(1);
        using var log = new StringWriter();
        var sent = new List<SentFrame>();
        var ownerConnection = CreateTracingConnection("owner", sent);
        var viewerConnection = CreateTracingConnection("viewer", sent);
        var handler = CreateHandler(log);
        var owner = CreateSession(
            OwnerUid,
            "Owner",
            OwnerX,
            OwnerY,
            attributes: CreateTimedStatusAttributes(
                LegacyBossStatusEffect.FrightenedFlag,
                LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute,
                LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute,
                expiresAt,
                includeStatusFlag: false));
        var viewer = CreateSession(
            ViewerUid,
            "Viewer",
            checked((ushort)(OwnerX - 1)),
            OwnerY);
        SetHandlerSession(handler, ownerConnection, owner);
        SetHandlerSession(handler, viewerConnection, viewer);

        await InvokeExpirationAsync(
            handler,
            "SendDueLegacyBossFrightenedExpirationsAsync",
            expiresAt);

        var cleaned = AssertHandlerSession(handler, ownerConnection);
        Assert.False(HasLegacyStatusFlag(
            cleaned.Character.Attributes,
            LegacyBossStatusEffect.FrightenedFlag));
        Assert.True(HasLegacyStatusFlag(cleaned.Character.Attributes, UnrelatedStatusFlag));
        Assert.False(cleaned.Character.Attributes.ContainsKey(
            LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute));
        Assert.False(cleaned.Character.Attributes.ContainsKey(
            LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute));
        Assert.Equal("preserve-me", cleaned.Character.Attributes[SentinelAttribute]);
        Assert.Empty(SnapshotSentFrames(sent));
        Assert.DoesNotContain("expired legacy boss Frightened flags", log.ToString());
    }

    [Fact]
    public async Task SharedDizzyExpiryUsesCanonicalDustTimerAndEquivalentOwnerOnlyType58()
    {
        Assert.Equal(58U, (uint)LegacyGameUpdateDataType.Stun);
        var expiresAt = RuntimeNow.AddSeconds(3);
        using var log = new StringWriter();
        var sent = new List<SentFrame>();
        var ownerConnection = CreateTracingConnection("owner", sent);
        var viewerConnection = CreateTracingConnection("viewer", sent);
        var handler = CreateHandler(log);
        var owner = CreateSession(
            OwnerUid,
            "Owner",
            OwnerX,
            OwnerY,
            attributes: CreateTimedStatusAttributes(
                LegacyBossStatusEffect.DizzyFlag,
                LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute,
                LegacyControlStatusLifetime.DizzyRemoveOnDeadAttribute,
                expiresAt,
                includeStatusFlag: true));
        var viewer = CreateSession(
            ViewerUid,
            "Viewer",
            checked((ushort)(OwnerX - 1)),
            OwnerY);
        SetHandlerSession(handler, ownerConnection, owner);
        SetHandlerSession(handler, viewerConnection, viewer);

        await InvokeExpirationAsync(
            handler,
            "SendDueLegacyDustSigilStunnExpirationsAsync",
            expiresAt);

        var expired = AssertHandlerSession(handler, ownerConnection);
        Assert.False(HasLegacyStatusFlag(expired.Character.Attributes, LegacyBossStatusEffect.DizzyFlag));
        Assert.True(HasLegacyStatusFlag(expired.Character.Attributes, UnrelatedStatusFlag));
        Assert.False(expired.Character.Attributes.ContainsKey(
            LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute));
        Assert.False(expired.Character.Attributes.ContainsKey(
            LegacyControlStatusLifetime.DizzyRemoveOnDeadAttribute));
        Assert.Equal("preserve-me", expired.Character.Attributes[SentinelAttribute]);

        Assert.Collection(
            SnapshotSentFrames(sent),
            frame =>
            {
                Assert.Equal("owner", frame.Recipient);
                AssertStatusVectorFrame(
                    frame.Bytes,
                    OwnerUid,
                    expectedSet: [UnrelatedStatusFlag],
                    expectedClear: [LegacyBossStatusEffect.DizzyFlag]);
            },
            frame =>
            {
                Assert.Equal("viewer", frame.Recipient);
                AssertStatusVectorFrame(
                    frame.Bytes,
                    OwnerUid,
                    expectedSet: [UnrelatedStatusFlag],
                    expectedClear: [LegacyBossStatusEffect.DizzyFlag]);
            },
            frame =>
            {
                Assert.Equal("owner", frame.Recipient);
                AssertGameUpdateFrame(
                    frame.Bytes,
                    OwnerUid,
                    LegacyGameUpdateDataType.Stun,
                    expectedActive: false,
                    expectedTime: 0);
            });
        Assert.Contains("WorldServer expired legacy DustSigilStunn flags", log.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControlStatusExpirySuppressesLifecycleFramesWhenCallbackPublishesNewerStatus(
        bool frightened)
    {
        var statusFlag = frightened
            ? LegacyBossStatusEffect.FrightenedFlag
            : LegacyBossStatusEffect.DizzyFlag;
        var expiresAtAttribute = frightened
            ? LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute
            : LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute;
        var removeOnDeadAttribute = frightened
            ? LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute
            : LegacyControlStatusLifetime.DizzyRemoveOnDeadAttribute;
        var expirationMethod = frightened
            ? "SendDueLegacyBossFrightenedExpirationsAsync"
            : "SendDueLegacyDustSigilStunnExpirationsAsync";
        var futureExpiresAt = RuntimeNow.AddMinutes(1);
        using var log = new StringWriter();
        var sent = new List<SentFrame>();
        var ownerConnection = CreateTracingConnection("owner", sent);
        var viewerConnection = CreateTracingConnection("viewer", sent);
        var handler = CreateHandler(log);
        var callbackArmed = false;
        var supersessionCount = 0;
        WorldSessionStore callbackSessions = null!;
        callbackSessions = new WorldSessionStore((connection, previous, current) =>
        {
            if (!callbackArmed ||
                supersessionCount != 0 ||
                !ReferenceEquals(connection, ownerConnection) ||
                previous is null ||
                !HasLegacyStatusFlag(previous.Character.Attributes, statusFlag) ||
                HasLegacyStatusFlag(current.Character.Attributes, statusFlag))
            {
                return;
            }

            supersessionCount++;
            var attributes = new Dictionary<string, string>(
                current.Character.Attributes,
                StringComparer.OrdinalIgnoreCase);
            AddLegacyStatusFlag(attributes, statusFlag);
            attributes[expiresAtAttribute] = FormatTimestamp(futureExpiresAt);
            attributes[removeOnDeadAttribute] = bool.TrueString;
            callbackSessions.Set(
                connection,
                current with
                {
                    Character = current.Character with { Attributes = attributes },
                });
        });
        SetHandlerSessions(handler, callbackSessions);
        callbackSessions.Set(
            ownerConnection,
            CreateSession(
                OwnerUid,
                "Owner",
                OwnerX,
                OwnerY,
                attributes: CreateTimedStatusAttributes(
                    statusFlag,
                    expiresAtAttribute,
                    removeOnDeadAttribute,
                    RuntimeNow,
                    includeStatusFlag: true)));
        callbackSessions.Set(
            viewerConnection,
            CreateSession(
                ViewerUid,
                "Viewer",
                checked((ushort)(OwnerX - 1)),
                OwnerY));
        callbackArmed = true;

        await InvokeExpirationAsync(handler, expirationMethod, RuntimeNow);

        Assert.Equal(1, supersessionCount);
        var supersedingSession = AssertSession(callbackSessions, ownerConnection);
        Assert.True(HasLegacyStatusFlag(supersedingSession.Character.Attributes, statusFlag));
        Assert.True(HasLegacyStatusFlag(
            supersedingSession.Character.Attributes,
            UnrelatedStatusFlag));
        Assert.Equal(
            futureExpiresAt,
            ReadExpiry(supersedingSession.Character.Attributes, expiresAtAttribute));
        Assert.Equal(
            bool.TrueString,
            supersedingSession.Character.Attributes[removeOnDeadAttribute]);
        Assert.Equal(
            "preserve-me",
            supersedingSession.Character.Attributes[SentinelAttribute]);
        Assert.Empty(SnapshotSentFrames(sent));
        Assert.DoesNotContain("expired legacy", log.ToString());
    }

    [Fact]
    public void PlayerDeathClearsBothBossControlStatusesAndMetadataWhileDeadRemains()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(
                LegacyBossStatusEffect.FrightenedFlag,
                LegacyBossStatusEffect.DizzyFlag,
                UnrelatedStatusFlag),
            [LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute] =
                FormatTimestamp(RuntimeNow.AddSeconds(6)),
            [LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute] = bool.TrueString,
            [LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute] =
                FormatTimestamp(RuntimeNow.AddSeconds(3)),
            [LegacyControlStatusLifetime.DizzyRemoveOnDeadAttribute] = bool.TrueString,
            [SentinelAttribute] = "preserve-me",
        };
        var session = CreateSession(
            OwnerUid,
            "Owner",
            OwnerX,
            OwnerY,
            attributes: attributes);

        var death = LegacyPlayerDeathState.ApplyDeath(
            session.Character,
            RuntimeNow,
            OwnerX,
            OwnerY);

        Assert.Equal(0, death.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(
            death.Character.Attributes,
            LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(HasLegacyStatusFlag(
            death.Character.Attributes,
            LegacyBossStatusEffect.FrightenedFlag));
        Assert.False(HasLegacyStatusFlag(
            death.Character.Attributes,
            LegacyBossStatusEffect.DizzyFlag));
        Assert.False(death.Character.Attributes.ContainsKey(
            LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute));
        Assert.False(death.Character.Attributes.ContainsKey(
            LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute));
        Assert.False(death.Character.Attributes.ContainsKey(
            LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute));
        Assert.False(death.Character.Attributes.ContainsKey(
            LegacyControlStatusLifetime.DizzyRemoveOnDeadAttribute));
        Assert.Equal("preserve-me", death.Character.Attributes[SentinelAttribute]);
    }

    [Fact]
    public async Task BossDizzyAfterSuccessfulRollDoesNotRefreshOrResendSharedDustStatus()
    {
        var originalExpiresAt = RuntimeNow.AddMinutes(1);
        var attributes = CreateTimedStatusAttributes(
            LegacyBossStatusEffect.DizzyFlag,
            LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute,
            LegacyControlStatusLifetime.DizzyRemoveOnDeadAttribute,
            originalExpiresAt,
            includeStatusFlag: true);
        attributes[StatusRollAttribute] = "0";
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(OwnerUid, "Owner", OwnerX, OwnerY, attributes: attributes);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions);

        var result = await InvokeBossStatusEffectAsync(
            context,
            connection,
            session,
            CreateBossMonster(),
            LegacyBossSpellBranch.DizzyAreaSpellId,
            session,
            RuntimeNow);

        Assert.False(result.Applied);
        Assert.Same(session, result.Session);
        Assert.Equal(
            originalExpiresAt,
            ReadExpiry(
                result.Session.Character.Attributes,
                LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute));
        Assert.True(HasLegacyStatusFlag(
            result.Session.Character.Attributes,
            LegacyBossStatusEffect.DizzyFlag));
        Assert.Equal("preserve-me", result.Session.Character.Attributes[SentinelAttribute]);
        Assert.Empty(SnapshotFrames(sent));
        var stored = AssertSession(sessions, connection);
        Assert.Equal(
            originalExpiresAt,
            ReadExpiry(
                stored.Character.Attributes,
                LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute));
    }

    [Fact]
    public async Task StaleSessionCasRetriesWithoutRerollAndPreservesConcurrentMutation()
    {
        var staleAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [StatusRollAttribute] = "0",
            [SentinelAttribute] = "stale-value",
        };
        var staleSession = CreateSession(
            OwnerUid,
            "Owner",
            OwnerX,
            OwnerY,
            attributes: staleAttributes);
        var currentAttributes = new Dictionary<string, string>(
            staleSession.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            [StatusRollAttribute] = "99",
            [SentinelAttribute] = "current-value",
            [ConcurrentMutationAttribute] = "preserve-me",
        };
        var currentSession = staleSession with
        {
            Username = "concurrent-owner",
            Character = staleSession.Character with { Attributes = currentAttributes },
        };
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, currentSession);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            staleSession,
            Sessions: sessions);

        var result = await InvokeBossStatusEffectAsync(
            context,
            connection,
            staleSession,
            CreateBossMonster(),
            LegacyBossSpellBranch.FrightenedSpellId,
            staleSession,
            RuntimeNow);

        Assert.True(result.Applied);
        var stored = AssertSession(sessions, connection);
        Assert.Same(stored, result.Session);
        Assert.Equal("concurrent-owner", stored.Username);
        Assert.Equal("99", stored.Character.Attributes[StatusRollAttribute]);
        Assert.Equal("current-value", stored.Character.Attributes[SentinelAttribute]);
        Assert.Equal("preserve-me", stored.Character.Attributes[ConcurrentMutationAttribute]);
        Assert.True(HasLegacyStatusFlag(
            stored.Character.Attributes,
            LegacyBossStatusEffect.FrightenedFlag));
        Assert.Equal(
            RuntimeNow.AddSeconds(6),
            ReadExpiry(
                stored.Character.Attributes,
                LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute));
        Assert.Equal(2, SnapshotFrames(sent).Count);
    }

    [Fact]
    public async Task SameFlagCasRaceSendsNoDuplicateFramesOrTimerRefresh()
    {
        var staleAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [StatusRollAttribute] = "0",
        };
        var staleSession = CreateSession(
            OwnerUid,
            "Owner",
            OwnerX,
            OwnerY,
            attributes: staleAttributes);
        var originalExpiresAt = RuntimeNow.AddMinutes(2);
        var winningAttributes = CreateTimedStatusAttributes(
            LegacyBossStatusEffect.FrightenedFlag,
            LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute,
            LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute,
            originalExpiresAt,
            includeStatusFlag: true);
        winningAttributes[StatusRollAttribute] = "99";
        winningAttributes[ConcurrentMutationAttribute] = "winning-writer";
        var winningSession = staleSession with
        {
            Character = staleSession.Character with { Attributes = winningAttributes },
        };
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, winningSession);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            staleSession,
            Sessions: sessions);

        var result = await InvokeBossStatusEffectAsync(
            context,
            connection,
            staleSession,
            CreateBossMonster(),
            LegacyBossSpellBranch.FrightenedSpellId,
            staleSession,
            RuntimeNow);

        Assert.False(result.Applied);
        Assert.Same(winningSession, result.Session);
        Assert.Same(winningSession, AssertSession(sessions, connection));
        Assert.Equal(
            originalExpiresAt,
            ReadExpiry(
                result.Session.Character.Attributes,
                LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute));
        Assert.Equal("winning-writer", result.Session.Character.Attributes[ConcurrentMutationAttribute]);
        Assert.Empty(SnapshotFrames(sent));
    }

    [Fact]
    public async Task LiveBossStatusWrapperUsesContextTimeProvider()
    {
        var providerNow = RuntimeNow.AddDays(2).AddMinutes(17);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [StatusRollAttribute] = "0",
        };
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(OwnerUid, "Owner", OwnerX, OwnerY, attributes: attributes);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions,
            TimeProvider: new FixedUtcTimeProvider(providerNow));

        var result = await InvokeLiveBossStatusEffectAsync(
            context,
            connection,
            session,
            CreateBossMonster(),
            LegacyBossSpellBranch.FrightenedSpellId);

        Assert.True(result.Applied);
        Assert.Equal(
            providerNow.AddSeconds(6),
            ReadExpiry(
                result.Session.Character.Attributes,
                LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute));
        Assert.Equal(2, SnapshotFrames(sent).Count);
    }

    [Theory]
    [InlineData(54, false)]
    [InlineData(55, true)]
    public async Task NormalBossStatusHonorsAcalanathaForcedRollBoundary(
        int forcedRoll,
        bool expectedApplied)
    {
        var worldData = CreateWorldData([CreateAcalanathaItemType()]);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [StatusRollAttribute] = "0",
            [AcalanathaRollAttribute] = forcedRoll.ToString(CultureInfo.InvariantCulture),
        };
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            OwnerUid,
            "Owner",
            OwnerX,
            OwnerY,
            attributes: attributes,
            playerItems: CreateAcalanathaPlayerItems());
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        var result = await InvokeBossStatusEffectAsync(
            context,
            connection,
            session,
            CreateBossMonster(),
            LegacyBossSpellBranch.FrightenedSpellId,
            session,
            RuntimeNow);

        Assert.Equal(expectedApplied, result.Applied);
        Assert.Equal(
            expectedApplied,
            HasLegacyStatusFlag(
                result.Session.Character.Attributes,
                LegacyBossStatusEffect.FrightenedFlag));
        if (!expectedApplied)
        {
            Assert.False(result.Session.Character.Attributes.ContainsKey(
                LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute));
            Assert.Empty(SnapshotFrames(sent));
            return;
        }

        Assert.Equal(
            RuntimeNow.AddSeconds(6),
            ReadExpiry(
                result.Session.Character.Attributes,
                LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute));
        Assert.Collection(
            SnapshotFrames(sent),
            frame => AssertStatusVectorFrame(
                frame,
                OwnerUid,
                expectedSet: [LegacyBossStatusEffect.FrightenedFlag],
                expectedClear: []),
            frame => AssertGameUpdateFrame(
                frame,
                OwnerUid,
                LegacyGameUpdateDataType.Flustered,
                expectedActive: true,
                expectedTime: 6));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task AlternateAcalanathaOnlyGuardsWhileAlternateLoadoutIsActive(
        bool alternateEquipmentActive,
        bool expectedApplied)
    {
        var worldData = CreateWorldData([CreateAcalanathaItemType()]);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [StatusRollAttribute] = "0",
            [AcalanathaRollAttribute] = "54",
        };
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            OwnerUid,
            "Owner",
            OwnerX,
            OwnerY,
            attributes: attributes,
            playerItems: CreateAcalanathaPlayerItems(position: 121),
            alternateEquipmentActive: alternateEquipmentActive);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        var result = await InvokeBossStatusEffectAsync(
            context,
            connection,
            session,
            CreateBossMonster(),
            LegacyBossSpellBranch.FrightenedSpellId,
            session,
            RuntimeNow);

        Assert.Equal(expectedApplied, result.Applied);
        Assert.Equal(expectedApplied ? 2 : 0, SnapshotFrames(sent).Count);
        Assert.Equal(
            expectedApplied,
            HasLegacyStatusFlag(
                result.Session.Character.Attributes,
                LegacyBossStatusEffect.FrightenedFlag));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public async Task AreaRecipientUsesSuppliedPrimaryRuneOwnerForAcalanatha(
        bool recipientHasRune,
        bool primaryHasRune,
        bool expectedApplied)
    {
        var worldData = CreateWorldData([CreateAcalanathaItemType()]);
        var primaryAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AcalanathaRollAttribute] = "54",
        };
        var recipientAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [StatusRollAttribute] = "0",
            [AcalanathaRollAttribute] = "54",
        };
        var primarySent = new List<byte[]>();
        var recipientSent = new List<byte[]>();
        var primaryConnection = CreateSendingConnection(primarySent);
        var recipientConnection = CreateSendingConnection(recipientSent);
        var primary = CreateSession(
            OwnerUid,
            "Primary",
            OwnerX,
            OwnerY,
            attributes: primaryAttributes,
            playerItems: primaryHasRune ? CreateAcalanathaPlayerItems() : null);
        var recipient = CreateSession(
            RecipientUid,
            "Recipient",
            checked((ushort)(OwnerX + 1)),
            OwnerY,
            attributes: recipientAttributes,
            playerItems: recipientHasRune ? CreateAcalanathaPlayerItems() : null);
        var sessions = new WorldSessionStore();
        sessions.Set(primaryConnection, primary);
        sessions.Set(recipientConnection, recipient);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            primaryConnection,
            log,
            primary,
            WorldData: worldData,
            Sessions: sessions);

        var result = await InvokeBossStatusEffectAsync(
            context,
            recipientConnection,
            recipient,
            CreateBossMonster(),
            LegacyBossSpellBranch.FrightenedSpellId,
            primary,
            RuntimeNow);

        Assert.Equal(expectedApplied, result.Applied);
        Assert.Equal(
            expectedApplied,
            HasLegacyStatusFlag(
                result.Session.Character.Attributes,
                LegacyBossStatusEffect.FrightenedFlag));
        if (expectedApplied)
        {
            Assert.Equal(2, SnapshotFrames(recipientSent).Count);
            Assert.Equal(2, SnapshotFrames(primarySent).Count);
            Assert.True(HasLegacyStatusFlag(
                AssertSession(sessions, recipientConnection).Character.Attributes,
                LegacyBossStatusEffect.FrightenedFlag));
        }
        else
        {
            Assert.Empty(SnapshotFrames(recipientSent));
            Assert.Empty(SnapshotFrames(primarySent));
        }
    }

    [Fact]
    public async Task Monster6643PrimaryFrightenedBypassesOppositeStatusAndAcalanatha()
    {
        var originalDizzyExpiry = RuntimeNow.AddMinutes(1);
        var attributes = CreateTimedStatusAttributes(
            LegacyBossStatusEffect.DizzyFlag,
            LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute,
            LegacyControlStatusLifetime.DizzyRemoveOnDeadAttribute,
            originalDizzyExpiry,
            includeStatusFlag: true);
        attributes[StatusRollAttribute] = "0";
        attributes[SecondaryStatusRollAttribute] = "99";
        attributes[AcalanathaRollAttribute] = "54";
        var worldData = CreateWorldData([CreateAcalanathaItemType()]);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            OwnerUid,
            "Owner",
            OwnerX,
            OwnerY,
            attributes: attributes,
            playerItems: CreateAcalanathaPlayerItems());
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        var result = await InvokeBossStatusEffectAsync(
            context,
            connection,
            session,
            CreateBossMonster(monsterId: 6_643),
            spellId: 10_500,
            session,
            RuntimeNow);

        Assert.True(result.Applied);
        Assert.True(HasLegacyStatusFlag(
            result.Session.Character.Attributes,
            LegacyBossStatusEffect.FrightenedFlag));
        Assert.True(HasLegacyStatusFlag(
            result.Session.Character.Attributes,
            LegacyBossStatusEffect.DizzyFlag));
        Assert.True(HasLegacyStatusFlag(
            result.Session.Character.Attributes,
            UnrelatedStatusFlag));
        Assert.Equal(
            RuntimeNow.AddSeconds(6),
            ReadExpiry(
                result.Session.Character.Attributes,
                LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute));
        Assert.Equal(
            originalDizzyExpiry,
            ReadExpiry(
                result.Session.Character.Attributes,
                LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute));
        Assert.Collection(
            SnapshotFrames(sent),
            frame => AssertStatusVectorFrame(
                frame,
                OwnerUid,
                expectedSet:
                [
                    LegacyBossStatusEffect.FrightenedFlag,
                    LegacyBossStatusEffect.DizzyFlag,
                    UnrelatedStatusFlag,
                ],
                expectedClear: []),
            frame => AssertGameUpdateFrame(
                frame,
                OwnerUid,
                LegacyGameUpdateDataType.Flustered,
                expectedActive: true,
                expectedTime: 6));
    }

    [Fact]
    public async Task ApplyingBossStatusToDeadSnapshotReaddsStatusAndTimerWhileDeadRemains()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(LegacyPlayerDeathState.DeadStatusFlag),
            [StatusRollAttribute] = "0",
            [SentinelAttribute] = "preserve-me",
        };
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            OwnerUid,
            "Owner",
            OwnerX,
            OwnerY,
            minHitPoints: 0,
            attributes: attributes);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions);

        var result = await InvokeBossStatusEffectAsync(
            context,
            connection,
            session,
            CreateBossMonster(),
            LegacyBossSpellBranch.FrightenedSpellId,
            session,
            RuntimeNow);

        Assert.True(result.Applied);
        Assert.Equal(0, result.Session.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(
            result.Session.Character.Attributes,
            LegacyPlayerDeathState.DeadStatusFlag));
        Assert.True(HasLegacyStatusFlag(
            result.Session.Character.Attributes,
            LegacyBossStatusEffect.FrightenedFlag));
        Assert.Equal(
            RuntimeNow.AddSeconds(6),
            ReadExpiry(
                result.Session.Character.Attributes,
                LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute));
        Assert.Equal("preserve-me", result.Session.Character.Attributes[SentinelAttribute]);
        Assert.Collection(
            SnapshotFrames(sent),
            frame => AssertStatusVectorFrame(
                frame,
                OwnerUid,
                expectedSet:
                [
                    LegacyPlayerDeathState.DeadStatusFlag,
                    LegacyBossStatusEffect.FrightenedFlag,
                ],
                expectedClear: []),
            frame => AssertGameUpdateFrame(
                frame,
                OwnerUid,
                LegacyGameUpdateDataType.Flustered,
                expectedActive: true,
                expectedTime: 6));
    }

    private static Dictionary<string, string> CreateTimedStatusAttributes(
        int statusFlag,
        string expiresAtAttribute,
        string removeOnDeadAttribute,
        DateTimeOffset expiresAt,
        bool includeStatusFlag)
    {
        var flags = includeStatusFlag
            ? new[] { statusFlag, UnrelatedStatusFlag }
            : new[] { UnrelatedStatusFlag };
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(flags),
            [expiresAtAttribute] = FormatTimestamp(expiresAt),
            [removeOnDeadAttribute] = bool.TrueString,
            [SentinelAttribute] = "preserve-me",
        };
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var values = new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength];
        foreach (var flag in flags)
        {
            values[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', values.Select(value => value.ToString(CultureInfo.InvariantCulture))) + "/";
    }

    private static void AddLegacyStatusFlag(IDictionary<string, string> attributes, int flag)
    {
        var values = LegacyPlayerDeathState.CreateStatusFlagVector(
            (IReadOnlyDictionary<string, string>)attributes,
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
        values[flag / 32] |= 1UL << (flag % 32);
        attributes["StatusFlags"] =
            string.Join('/', values.Select(value => value.ToString(CultureInfo.InvariantCulture))) + "/";
    }

    private static bool HasLegacyStatusFlag(
        IReadOnlyDictionary<string, string> attributes,
        int flag)
    {
        return attributes.TryGetValue("StatusFlags", out var raw) &&
            IsLegacyStatusFlagSet(
                raw
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(part => ulong.TryParse(
                        part,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var value)
                            ? value
                            : 0)
                    .ToArray(),
                flag);
    }

    private static bool IsLegacyStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }

    private static DateTimeOffset ReadExpiry(
        IReadOnlyDictionary<string, string> attributes,
        string attribute)
    {
        Assert.True(attributes.TryGetValue(attribute, out var rawExpiresAt));
        Assert.True(DateTimeOffset.TryParse(
            rawExpiresAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var expiresAt));
        return expiresAt;
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static WorldServerFrameHandler CreateHandler(StringWriter log)
    {
        return new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled,
            log: log);
    }

    private static ConquerTcpConnection CreateTracingConnection(
        string recipient,
        List<SentFrame> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                lock (sent)
                {
                    sent.Add(new SentFrame(recipient, frame.ToArray()));
                }

                return ValueTask.CompletedTask;
            });
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                lock (sent)
                {
                    sent.Add(frame.ToArray());
                }

                return ValueTask.CompletedTask;
            });
    }

    private static IReadOnlyList<SentFrame> SnapshotSentFrames(List<SentFrame> sent)
    {
        lock (sent)
        {
            return sent.Select(frame => new SentFrame(frame.Recipient, frame.Bytes.ToArray())).ToArray();
        }
    }

    private static IReadOnlyList<byte[]> SnapshotFrames(List<byte[]> sent)
    {
        lock (sent)
        {
            return sent.Select(frame => frame.ToArray()).ToArray();
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static void AssertStatusVectorFrame(
        byte[] bytes,
        uint uid,
        IReadOnlyCollection<int> expectedSet,
        IReadOnlyCollection<int> expectedClear)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        Assert.Equal(uid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, entry.Type);
        Assert.NotNull(entry.Values);
        Assert.Equal(LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength, entry.Values!.Count);
        foreach (var flag in expectedSet)
        {
            Assert.True(IsLegacyStatusFlagSet(entry.Values, flag));
        }

        foreach (var flag in expectedClear)
        {
            Assert.False(IsLegacyStatusFlagSet(entry.Values, flag));
        }
    }

    private static void AssertGameUpdateFrame(
        byte[] bytes,
        uint uid,
        LegacyGameUpdateDataType expectedType,
        bool expectedActive,
        uint expectedTime)
    {
        Assert.True(LegacyGameUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        Assert.Equal(uid, update.Uid);
        Assert.Equal(expectedType, update.Type);
        Assert.Equal(expectedActive, update.Active);
        Assert.Equal(0U, update.ShownAmount);
        Assert.Equal(expectedTime, update.Time);
        Assert.Equal(0U, update.Amount);
    }

    private static void SetHandlerSession(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        WorldSession session)
    {
        GetHandlerSessions(handler).Set(connection, session);
    }

    private static void SetHandlerSessions(
        WorldServerFrameHandler handler,
        WorldSessionStore sessions)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(handler, sessions);
        Assert.Same(sessions, field.GetValue(handler));
    }

    private static WorldSessionStore GetHandlerSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static WorldSession AssertHandlerSession(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection)
    {
        return AssertSession(GetHandlerSessions(handler), connection);
    }

    private static WorldSession AssertSession(
        WorldSessionStore sessions,
        ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        Assert.NotNull(session);
        return session;
    }

    private static async ValueTask InvokeExpirationAsync(
        WorldServerFrameHandler handler,
        string methodName,
        DateTimeOffset now)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var invocation = Assert.IsType<ValueTask>(
            method.Invoke(handler, [now, CancellationToken.None]));
        await invocation;
    }

    private static async Task<BossStatusInvocationResult> InvokeBossStatusEffectAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection subjectConnection,
        WorldSession subjectSession,
        LegacyMonsterInstance monster,
        ushort spellId,
        WorldSession acalanathaRuneOwnerSession,
        DateTimeOffset now)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "TryApplyLegacyBossStatusEffectAsync",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(WorldPacketHandlerContext),
                typeof(ConquerTcpConnection),
                typeof(WorldSession),
                typeof(LegacyMonsterInstance),
                typeof(ushort),
                typeof(WorldSession),
                typeof(DateTimeOffset),
                typeof(CancellationToken),
            ],
            modifiers: null);
        Assert.NotNull(method);
        return await AwaitBossStatusInvocationAsync(method.Invoke(
            null,
            [
                context,
                subjectConnection,
                subjectSession,
                monster,
                spellId,
                acalanathaRuneOwnerSession,
                now,
                CancellationToken.None,
            ])).ConfigureAwait(false);
    }

    private static async Task<BossStatusInvocationResult> InvokeLiveBossStatusEffectAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection subjectConnection,
        WorldSession subjectSession,
        LegacyMonsterInstance monster,
        ushort spellId)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "TryApplyLegacyBossStatusEffectAsync",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(WorldPacketHandlerContext),
                typeof(ConquerTcpConnection),
                typeof(WorldSession),
                typeof(LegacyMonsterInstance),
                typeof(ushort),
                typeof(CancellationToken),
            ],
            modifiers: null);
        Assert.NotNull(method);
        return await AwaitBossStatusInvocationAsync(method.Invoke(
            null,
            [
                context,
                subjectConnection,
                subjectSession,
                monster,
                spellId,
                CancellationToken.None,
            ])).ConfigureAwait(false);
    }

    private static async Task<BossStatusInvocationResult> AwaitBossStatusInvocationAsync(object? invocation)
    {
        Assert.NotNull(invocation);
        var asTaskMethod = invocation.GetType().GetMethod(
            "AsTask",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(asTaskMethod);
        var task = Assert.IsAssignableFrom<Task>(asTaskMethod.Invoke(invocation, null));
        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(resultProperty);
        var result = resultProperty.GetValue(task);
        Assert.NotNull(result);
        var sessionProperty = result.GetType().GetProperty("Session", BindingFlags.Instance | BindingFlags.Public);
        var appliedProperty = result.GetType().GetProperty("Applied", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(sessionProperty);
        Assert.NotNull(appliedProperty);
        return new BossStatusInvocationResult(
            Assert.IsType<WorldSession>(sessionProperty.GetValue(result)),
            Assert.IsType<bool>(appliedProperty.GetValue(result)));
    }

    private static WorldSession CreateSession(
        uint uid,
        string name,
        ushort x,
        ushort y,
        int minHitPoints = 500_000,
        Dictionary<string, string>? attributes = null,
        PlayerItemSnapshot? playerItems = null,
        bool alternateEquipmentActive = false)
    {
        attributes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return new WorldSession(
            AccountId: uid,
            Username: name.ToLowerInvariant(),
            new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1_003,
                Face: 0,
                Hair: 0,
                Class: FixtureNinjaClass,
                FirstClass: FixtureNinjaClass,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: MapId,
                X: x,
                Y: y,
                PreviousMapId: MapId,
                PreviousMapX: x,
                PreviousMapY: y,
                Strength: 300,
                Agility: 0,
                Vitality: 300,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: minHitPoints,
                MinMana: 500,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture://boss-status-lifecycle",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>()),
            playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            AlternateEquipmentActive: alternateEquipmentActive);
    }

    private static LegacyMonsterInstance CreateBossMonster(uint monsterId = 7_981)
    {
        return new LegacyMonsterInstance(
            Uid: 900_001,
            MapId: checked((int)MapId),
            X: OwnerX,
            Y: OwnerY,
            Mesh: 100,
            Level: 120,
            Hitpoints: 100_000,
            MaxLife: 100_000,
            MonsterId: monsterId,
            Name: "FixtureBoss",
            Boss: true,
            GeneratorId: 1,
            SpawnIndex: 0);
    }

    private static PlayerItemSnapshot CreateAcalanathaPlayerItems(ushort position = 101)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: [],
            RuneItems:
            [
                new PlayerItemSnapshotItem
                {
                    Uid = AcalanathaRuneItemId,
                    ItemId = AcalanathaRuneItemId,
                    Position = position,
                },
            ]);
    }

    private static ItemType CreateAcalanathaItemType()
    {
        return new ItemType(
            checked((int)AcalanathaRuneItemId),
            "Acalanatha",
            [AcalanathaRuneItemId.ToString(CultureInfo.InvariantCulture), "Acalanatha"]);
    }

    private static WorldDataCatalog CreateWorldData(IReadOnlyList<ItemType> items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-13T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://boss-status-lifecycle",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(
                new ActivityTaskMetadata(0, "", 0, []),
                [],
                "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(
                0,
                [],
                "fixture://neigong",
                0,
                "",
                new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed class FixedUtcTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedUtcTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow.ToUniversalTime();
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed record SentFrame(string Recipient, byte[] Bytes);

    private sealed record BossStatusInvocationResult(WorldSession Session, bool Applied);
}
