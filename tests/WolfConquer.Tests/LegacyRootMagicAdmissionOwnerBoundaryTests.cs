using System.Reflection;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public void RootAdmissionCapturedNullExpectedIdentityCannotCommitAcrossRemoveAndSameRecordReinsert()
    {
        var (before, world, spell) = CreateRootOwnerBoundaryInput();
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, before);
        var context = new WorldPacketHandlerContext(connection, log, before, WorldData: world,
            Sessions: sessions, TimeProvider: TimeProvider.System,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var beforeImage = JsonSerializer.SerializeToUtf8Bytes(before);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(sessions, connection, PlayerUid, out var originalIdentity));
        var captured = CaptureRootOwnerBoundary(context, before); // Caller explicitly supplies null identity.
        var candidate = PrepareRootOwnerBoundaryCandidate(context, before, spell);
        Assert.NotSame(before, candidate);
        Assert.Equal("89", candidate.Character.Attributes["Stamina"]);
        Assert.Equal(463L, candidate.Character.MinMana);
        Assert.NotNull(candidate.MagicCooldowns);
        Assert.True(candidate.MagicCooldowns.ContainsKey(spell.Id));
        Assert.Same(before, sessions.TryGet(connection, out var stillBefore) ? stillBefore : null);
        Assert.Equal(beforeImage, JsonSerializer.SerializeToUtf8Bytes(before));
        Assert.Empty(sent);

        Assert.True(sessions.TryRemove(connection, out var removed));
        Assert.Same(before, removed);
        sessions.Set(connection, before); // EXACT same immutable record, but a genuinely new Store generation.
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(sessions, connection, PlayerUid, out var replacementIdentity));
        Assert.NotSame(originalIdentity, replacementIdentity);
        Assert.Same(before, sessions.TryGet(connection, out var reinserted) ? reinserted : null);

        object?[] commit = [captured, candidate, null];
        Assert.False(Assert.IsType<bool>(InvokeRootOwnerBoundary("TryCommitLegacyRootAdmission", commit)));
        Assert.Same(before, commit[2]);
        Assert.Same(before, sessions.TryGet(connection, out var rejectedCurrent) ? rejectedCurrent : null);
        Assert.Equal(beforeImage, JsonSerializer.SerializeToUtf8Bytes(before));
        Assert.Empty(sent);

        // Positive component control: recapturing the REAL new generation can
        // commit this same pure plan because the exact record input is unchanged.
        commit = [CaptureRootOwnerBoundary(context, before), candidate, null];
        Assert.True(Assert.IsType<bool>(InvokeRootOwnerBoundary("TryCommitLegacyRootAdmission", commit)));
        Assert.Same(candidate, commit[2]);
        Assert.True(LegacyNativeClientPublication.TryCurrent(sessions, connection, PlayerUid,
            replacementIdentity, out var admitted));
        Assert.Same(candidate, admitted);
        Assert.Equal(beforeImage, JsonSerializer.SerializeToUtf8Bytes(before));
        Assert.Empty(sent); // Commit component only; no publication is requested here.
    }

    [Fact]
    public async Task RootAdmissionStoreObserverFormatFaultPropagatesAfterFullResourceCommitWithoutRefund()
    {
        var (before, world, spell) = CreateRootOwnerBoundaryInput();
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var fault = new FormatException("owned-store-observer-after-publication");
        var armed = false;
        var calls = 0;
        var publishedWasCurrent = false;
        WorldSession? observedPrevious = null;
        WorldSession? observedPublished = null;
        WorldSessionStore? sessions = null;
        sessions = new WorldSessionStore((actualConnection, previous, published) =>
        {
            if (!armed) return;
            calls++;
            observedPrevious = previous;
            observedPublished = published;
            publishedWasCurrent = ReferenceEquals(actualConnection, connection) &&
                sessions!.TryGet(actualConnection, out var current) && ReferenceEquals(current, published);
            throw fault; // Ordinary real Store observer; never grants or substitutes a commit.
        });
        sessions.Set(connection, before);
        var context = new WorldPacketHandlerContext(connection, log, before, WorldData: world,
            Sessions: sessions, TimeProvider: TimeProvider.System,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var beforeImage = JsonSerializer.SerializeToUtf8Bytes(before);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(sessions, connection, PlayerUid, out var identity));
        armed = true;
        var earliestDeadline = TimeProvider.System.GetUtcNow().AddMilliseconds(spell.ColdTime);
        var operation = Assert.IsType<ValueTask<WorldSession?>>(InvokeRootOwnerBoundary(
            "TryPrepareLegacyRootFirstFamilyCanUseAsync", [context, before, spell, CancellationToken.None, null]));
        var thrown = await Assert.ThrowsAsync<WorldSessionPublicationException>(async () => { await operation; });
        var latestDeadline = TimeProvider.System.GetUtcNow().AddMilliseconds(spell.ColdTime);
        armed = false;

        Assert.Same(fault, thrown.InnerException);
        Assert.Equal(WorldSessionPublicationStage.Observer, thrown.Stage);
        Assert.Null(thrown.FencingFailure);
        Assert.NotEmpty(fault.StackTrace!);
        Assert.Equal(1, calls);
        Assert.True(publishedWasCurrent);
        Assert.Same(before, observedPrevious);
        var committed = Assert.IsType<WorldSession>(observedPublished);
        Assert.NotSame(before, committed);
        Assert.Same(committed, thrown.PublishedSession);
        Assert.False(sessions.TryGet(connection, out _));
        Assert.False(LegacyNativeClientPublication.TryCurrent(sessions, connection, PlayerUid, identity, out _));
        Assert.Equal(463L, committed.Character.MinMana);
        var expectedAttributes = new Dictionary<string, string>(before.Character.Attributes) { ["Stamina"] = "89" };
        Assert.Equal(expectedAttributes.OrderBy(pair => pair.Key), committed.Character.Attributes.OrderBy(pair => pair.Key));
        Assert.Equal(before.Character, committed.Character with
            { MinMana = before.Character.MinMana, Attributes = before.Character.Attributes });
        Assert.Equal(before, committed with { Character = before.Character, MagicCooldowns = before.MagicCooldowns });
        var cooldown = Assert.Single(Assert.IsAssignableFrom<IReadOnlyDictionary<ushort, DateTimeOffset>>(committed.MagicCooldowns));
        Assert.Equal(spell.Id, cooldown.Key);
        Assert.InRange(cooldown.Value, earliestDeadline, latestDeadline);
        Assert.Equal(beforeImage, JsonSerializer.SerializeToUtf8Bytes(before));
        Assert.Empty(sent); // No success packet follows the observer fault; the resource commit remains.
        Assert.Contains("root magic admission commit-publication-unproven", log.ToString());
        Assert.Contains("no rollback or refund attempted", log.ToString());
        Assert.DoesNotContain("WorldServer skipped legacy player magic", log.ToString());
    }

    private static (WorldSession Owner, WorldDataCatalog World, LegacyMagicRuntimeSpell Spell) CreateRootOwnerBoundaryInput()
    {
        var owner = CreateSession(minMana: 500, stamina: 100, spellIds: [15_440], playerItems: PlayerItemSnapshot.Empty);
        var world = CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateMagicRow(15_440, sort: 6, useMana: 37, useStamina: 11, coldTime: 4_000));
        Assert.True(LegacyMagicRuntime.TryResolveNativeLearnedSpell(
            owner.PlayerSpells, world, 15_440, out _, out var spell, out _));
        return (owner, world, spell);
    }

    private static object CaptureRootOwnerBoundary(WorldPacketHandlerContext context, WorldSession before)
    {
        object?[] arguments = [context, before, null, null];
        Assert.True(Assert.IsType<bool>(InvokeRootOwnerBoundary("TryCaptureLegacyRootAdmissionOwner", arguments)));
        return Assert.IsAssignableFrom<object>(arguments[3]);
    }

    private static WorldSession PrepareRootOwnerBoundaryCandidate(
        WorldPacketHandlerContext context, WorldSession before, LegacyMagicRuntimeSpell spell)
    {
        object?[] cooldown = [context, before, spell, TimeProvider.System.GetUtcNow(), null, null];
        Assert.True(Assert.IsType<bool>(InvokeRootOwnerBoundary("TryPrepareLegacyRootCooldown", cooldown)));
        var mana = Assert.IsType<ushort>(InvokeRootOwnerBoundary("ReadLegacyRootRequiredMana", [context, before, spell]));
        object?[] costs = [context, before, spell, cooldown[4], mana, null, null, null];
        Assert.True(Assert.IsType<bool>(InvokeRootOwnerBoundary("TryPrepareLegacyRootCosts", costs)));
        Assert.Equal(string.Empty, costs[7]);
        Assert.NotNull(costs[6]);
        return Assert.IsType<WorldSession>(costs[6]!.GetType().GetProperty("Candidate")!.GetValue(costs[6]));
    }

    private static object? InvokeRootOwnerBoundary(string name, object?[] arguments)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return method.Invoke(null, arguments);
    }
}
