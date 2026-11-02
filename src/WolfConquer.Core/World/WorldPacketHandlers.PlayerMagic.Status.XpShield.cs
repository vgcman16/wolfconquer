using System.Globalization;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class RootXpShieldAdmission { }
    private sealed record RootXpShieldAdmissionData(WorldSessionStore Store, ConquerTcpConnection Connection,
        object Identity, WorldSession Removed, uint Duration);
    private static readonly ConditionalWeakTable<RootXpShieldAdmission, RootXpShieldAdmissionData> RootXpShieldAdmissions = new();

    private static RootXpShieldAdmission IssueRootXpShieldAdmission(WorldSessionStore store, ConquerTcpConnection connection,
        object identity, WorldSession removed, uint duration)
    {
        var admission = new RootXpShieldAdmission();
        RootXpShieldAdmissions.Add(admission, new(store, connection, identity, removed, duration));
        return admission;
    }

    internal static bool ConsumeRootXpShieldAdmission(RootXpShieldAdmission admission, WorldSessionStore store,
        ConquerTcpConnection connection, object identity, WorldSession expected, uint duration) =>
        RootXpShieldAdmissions.TryGetValue(admission, out var data) &&
        ReferenceEquals(data.Store, store) && ReferenceEquals(data.Connection, connection) &&
        ReferenceEquals(data.Identity, identity) && ReferenceEquals(data.Removed, expected) &&
        data.Duration == duration && RootXpShieldAdmissions.Remove(admission);

    // Validate only the current XP-removal fields, plus Shield clock fields
    // when this actual selected path needs a new registration. This is DATA
    // preflight against the real captured admission owner, not write authority.
    private static bool TryPreflightRootXpShield(CharacterSnapshot character, uint duration, TimeProvider provider)
    {
        try
        {
            var proof = LegacyNativeAttributeMapProof.CaptureRaw(character.Attributes);
            // Validate both known representations before selecting XP input;
            // conflicting aliases must not be normalized by a later reducer.
            _ = LegacyWindWalkerStatusProjection.Read(character);
            if (character.MinHitPoints <= 0 ||
                !TryGetLegacyStatusFlagsAttribute(character.Attributes, out var statusKey, out var raw)) return false;
            LegacyCharacterAttributeMap.Validate(character.Attributes, statusKey,
                LegacyStatusFlagDeathRegistration.Key(LegacyRuntimeUpdateTicker.XpListFlag));
            var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || parts.Length > LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength ||
                parts.Any(part => !ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) ||
                !LegacyPlayerDeathState.HasStatusFlag(character.Attributes, LegacyRuntimeUpdateTicker.XpListFlag) ||
                LegacyPlayerDeathState.HasStatusFlag(character.Attributes, LegacyPlayerDeathState.DeadStatusFlag)) return false;
            var fields = LegacyCharacterAttributeMap.Copy(character.Attributes);
            LegacyCharacterAttributeMap.Remove(fields, LegacyStatusFlagDeathRegistration.Key(LegacyRuntimeUpdateTicker.XpListFlag));
            var afterXp = WithoutLegacyStatusFlag(character with { Attributes = fields }, LegacyRuntimeUpdateTicker.XpListFlag);
            _ = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                CheckedUInt32(character.Uid, nameof(character.Uid)), LegacyPlayerDeathState.CreateStatusFlagVector(
                    afterXp.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
            if (!LegacyPlayerDeathState.HasStatusFlag(character.Attributes, LegacyMagicShieldFlag))
            {
                LegacyCharacterAttributeMap.Validate(character.Attributes,
                    LegacyStatusFlagDeathRegistration.Key(LegacyMagicShieldFlag),
                    "LegacyRootItemEffectFlag.8.NativeOwned", RuntimeLegacyMagicShieldExpiresAtUtcAttribute,
                    RuntimeLegacyMagicShieldRemoveOnDeadAttribute);
                if (!LegacyRootItemEffectFlagClock.CanRepresentDuration(duration, provider)) return false;
                var projected = ProjectRootXpShieldFlag(afterXp);
                var prepared = LegacyCharacterAttributeMap.Copy(projected.Attributes);
                LegacyCharacterAttributeMap.Set(prepared, "LegacyRootItemEffectFlag.8.NativeOwned", bool.TrueString);
                LegacyCharacterAttributeMap.Set(prepared, RuntimeLegacyMagicShieldExpiresAtUtcAttribute,
                    provider.GetUtcNow().AddSeconds(duration).ToString("O", CultureInfo.InvariantCulture));
            }
            return proof.IsCurrentPlain();
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or
            ArgumentException or OverflowException or FormatException) { return false; }
    }

    // DATA projection only; the Store below owns the actual flag/clock commit.
    internal static CharacterSnapshot ProjectRootXpShieldFlag(CharacterSnapshot character)
    {
        character = LegacyRootItemEffectFlagClock.ForgetProjection(character, LegacyMagicShieldFlag);
        var fields = LegacyCharacterAttributeMap.Copy(character.Attributes);
        LegacyCharacterAttributeMap.Remove(fields, LegacyStatusFlagDeathRegistration.Key(LegacyMagicShieldFlag));
        character = WithLegacyStatusFlag(character with { Attributes = fields }, LegacyMagicShieldFlag);
        fields = LegacyCharacterAttributeMap.Copy(character.Attributes);
        LegacyCharacterAttributeMap.Set(fields, LegacyStatusFlagDeathRegistration.Key(LegacyMagicShieldFlag), "true");
        LegacyCharacterAttributeMap.Set(fields, RuntimeLegacyMagicShieldRemoveOnDeadAttribute, bool.TrueString);
        return character with { Attributes = fields };
    }

    internal static bool RootXpShieldProjectionMatches(CharacterSnapshot proposed, CharacterSnapshot actual)
    {
        var a = LegacyNativeAttributeMapProof.CaptureRaw(proposed.Attributes);
        var b = LegacyNativeAttributeMapProof.CaptureRaw(actual.Attributes);
        return (proposed with { Attributes = actual.Attributes }) == actual &&
            ReferenceEquals(a.CapturedComparer, b.CapturedComparer) && a.Fields.Count == b.Fields.Count &&
            a.Fields.All(pair => b.Fields.TryGetValue(pair.Key, out var value) && value == pair.Value &&
                b.Fields.Keys.Any(key => string.Equals(key, pair.Key, StringComparison.Ordinal))) &&
            a.IsCurrentPlain() && b.IsCurrentPlain();
    }

    private static bool RootXpShieldIsCurrent(WorldPacketHandlerContext context, object identity, WorldSession expected) =>
        context.Sessions is { } store &&
        LegacyNativeClientPublication.TryCurrent(store, context.Connection, expected.Character.Uid, identity, out var actual) &&
        ReferenceEquals(actual, expected) && actual.Character.MinHitPoints > 0 &&
        !LegacyPlayerDeathState.HasStatusFlag(actual.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag);

    private static async ValueTask<bool> SendRootXpShieldFrameAsync(WorldPacketHandlerContext context,
        object identity, WorldSession expected, byte[] frame, CancellationToken cancellationToken)
    {
        if (!RootXpShieldIsCurrent(context, identity, expected)) return false;
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!RootXpShieldIsCurrent(context, identity, expected)) return false;
        var store = context.Sessions!;
        foreach (var candidate in store.Snapshot())
        {
            if (!RootXpShieldIsCurrent(context, identity, expected)) return false;
            if (ReferenceEquals(candidate.Connection, context.Connection) ||
                !store.TryGet(candidate.Connection, out var viewer) || viewer is null ||
                viewer.Character.Uid != candidate.Session.Character.Uid || viewer.Character.Uid == expected.Character.Uid ||
                !CanSeePlayer(expected.Character, viewer.Character) ||
                ReadCharacterDynamicId(expected.Character) != ReadCharacterDynamicId(viewer.Character)) continue;
            await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!RootXpShieldIsCurrent(context, identity, expected)) return false;
        }
        return true;
    }

    private static byte[] RootXpShieldVector(WorldSession session) =>
        LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            LegacyPlayerDeathState.CreateStatusFlagVector(session.Character.Attributes,
                LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();

    private static async ValueTask<bool> HandleLegacyFreshShieldXpBuffAsync(
        WorldPacketHandlerContext context, MsgAttackQuery query, PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell, WorldSession session, CancellationToken cancellationToken)
    {
        var uid = session.Character.Uid;
        if (context.Sessions is not { } store ||
            !LegacyNativeClientPublication.TryCaptureIdentity(store, context.Connection, uid, out var identity) ||
            !RootXpShieldIsCurrent(context, identity, session)) return true;
        if (!HasLegacyStatusFlag(session.Character, LegacyRuntimeUpdateTicker.XpListFlag))
        {
            await context.Log.WriteLineAsync(
                $"WorldServer skipped legacy Shield XP buff {FormatSession(context with { Session = session })} " +
                $"spellId={spell.Id}; XPList status flag is not active after source resource preparation.").ConfigureAwait(false);
            return true;
        }

        var casterUid = CheckedUInt32(uid, nameof(uid));
        var frame = LegacySpellAnimationPacket.ForPlayerMagic(casterUid, query.X, query.Y, spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue), playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(casterUid, spell.Duration, true, MsgAttackEffect.None)]).ToFrameBytes();
        var xpFields = LegacyCharacterAttributeMap.Copy(session.Character.Attributes);
        LegacyCharacterAttributeMap.Remove(xpFields, LegacyStatusFlagDeathRegistration.Key(LegacyRuntimeUpdateTicker.XpListFlag));
        var withoutXp = session with
        {
            Character = WithoutLegacyStatusFlag(session.Character with { Attributes = xpFields },
                LegacyRuntimeUpdateTicker.XpListFlag),
        };
        if (!store.TryCompareAndSetWithNativeIdentity(context.Connection, session, identity, withoutXp, out var removed) ||
            removed is null || !RootXpShieldIsCurrent(context, identity, removed)) return true;
        var admission = IssueRootXpShieldAdmission(store, context.Connection, identity, removed, spell.Duration);
        await context.Connection.SendAsync(
            LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(casterUid, value: 0).ToFrameBytes(),
            cancellationToken).ConfigureAwait(false);
        // A real reentrant death, revival, disconnect or other holder change
        // retires the entered tail. The committed XPList removal remains.
        if (!RootXpShieldIsCurrent(context, identity, removed)) return true;
        if (!await SendRootXpShieldFrameAsync(context, identity, removed, RootXpShieldVector(removed), cancellationToken)
                .ConfigureAwait(false)) return true;
        if (!store.TryCommitRootXpShield(context.Connection, identity, removed, admission, spell.Duration,
                context.TimeProvider ?? TimeProvider.System, out var committed, out var added) || committed is null ||
            !RootXpShieldIsCurrent(context, identity, committed)) return true;
        if (added && !await SendRootXpShieldFrameAsync(context, identity, committed, RootXpShieldVector(committed), cancellationToken)
                .ConfigureAwait(false)) return true;
        if (!await SendRootXpShieldFrameAsync(context, identity, committed, frame, cancellationToken).ConfigureAwait(false)) return true;
        await context.Log.WriteLineAsync(
            $"WorldServer handled legacy Shield XP buff {FormatSession(context with { Session = committed })} " +
            $"spellId={spell.Id} statusFlag={LegacyMagicShieldFlag} duration={spell.Duration} " +
            $"consumedXpList=True hadStatus={!added} refreshedStatus=False animationTargetDamage={spell.Duration}.")
            .ConfigureAwait(false);
        return true;
    }
}
