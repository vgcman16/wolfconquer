using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // All potential branches of ApplyLegacySobNpcBoundedEventSideEffectsAsync
    // are excluded here by their real source target selectors, before debit.
    // The scoreboard/reset owners remain a separate required integration.
    internal static bool CanPrepareOwnedDragonhowlSobOutput(WorldSession actor, SobNpc sob)
    {
        if (LegacySobNpcEventRules.IsTrainingSob(sob)) return true;
        return !(LegacySobNpcEventRules.IsSuperGuildWarPole(sob) ||
            LegacySobNpcEventRules.IsWarOfPlayersPole(sob) ||
            LegacySobNpcEventRules.TryGetGuild6PoleWarPoleSchedule(sob, out _, out _) ||
            LegacySobNpcEventRules.IsGuildWarScorePole(sob) ||
            LegacySobNpcEventRules.IsEmperorWarPole(sob) ||
            LegacySobNpcEventRules.IsTopPole(sob) ||
            LegacySobNpcEventRules.IsWarFightersPole(sob) ||
            LegacySobNpcEventRules.IsTrepilWarPole(sob) ||
            LegacySobNpcEventRules.TryGetNobilityPoleSchedule(sob, out _, out _) ||
            LegacySobNpcEventRules.TryGetClassPoleWarPole(sob, out _, out _, out _) ||
            LegacySobNpcEventRules.IsClanWarScorePole(sob) ||
            LegacySobNpcEventRules.IsClanWarPoleSob(sob) ||
            sob.MapId == LegacySobNpcEventRules.EliteGuildWarMapId && sob.Id == LegacySobNpcEventRules.EliteGuildWarPoleUid ||
            LegacySobNpcEventRules.IsSourceUnionWarPoleSob(sob) ||
            LegacySobNpcEventRules.IsChampionsOfWarrPole(sob) ||
            LegacySobNpcEventRules.TryGetDefaultDemolitionMesh(sob, out _) ||
            LegacySobNpcEventRules.TryGetGuildWarOwnershipChangeDwParam(sob, out _) ||
            actor.Character.MapId == LegacyCaptureTheFlagMapId && sob.Id >= 0 &&
                ReadCaptureTheFlagBases(actor.Character).Any(row => row.Uid == (uint)sob.Id && row.Viable));
    }

    // DATA preparation only. The private real proc Plan retains this object;
    // successful publication still requires its exact registered Store token.
    internal static DragonhowlSobOutput? PrepareOwnedDragonhowlSobOutput(WorldSession actor, SobNpc original,
        SobNpc projectedAfter, SobNpcDamageResult projectedResult) => CanPrepareOwnedDragonhowlSobOutput(actor, original)
            ? DragonhowlSobOutput.Prepare(actor, original, projectedAfter, projectedResult) : null;

    internal sealed class DragonhowlSobOutput
    {
        private readonly long actorUid;
        private readonly LegacyNativeGameClientRuntime? actorClient;
        private readonly SobNpc original, after;
        private readonly SobNpcDamageResult result;
        private readonly byte[][] frames;
        private int started;

        private DragonhowlSobOutput(WorldSession actor, SobNpc original, SobNpc after, SobNpcDamageResult result)
        {
            actorUid = actor.Character.Uid; actorClient = actor.NativeGameClient;
            this.original = original; this.after = after; this.result = result;
            if (result.AlreadyDead || original.Id != after.Id || original.MapId != after.MapId ||
                original.X != after.X || original.Y != after.Y) throw new InvalidDataException("ownedDragonhowlSobOutputProjectionInvalid");
            if (!result.Killed || !LegacySobNpcEventRules.IsTrainingSob(original)) { frames = []; return; }
            var uid = CheckedUInt32(original.Id, nameof(original.Id));
            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: CheckedUInt32(actorUid, nameof(actorUid)), OpponentUid: uid,
                X: CheckedUInt16(original.X, nameof(original.X)), Y: CheckedUInt16(original.Y, nameof(original.Y)),
                AttackType: MsgAttackType.Death, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0));
            frames =
            [
                death.ToFrameBytes(),
                new LegacyMsgUpdatePacket(uid, [new(LegacyMsgUpdateDataType.Hitpoints,
                    CheckedUInt32(after.Life, nameof(after.Life)))]).ToFrameBytes(),
                new LegacyMsgUpdatePacket(uid, [new(LegacyMsgUpdateDataType.MaxHitpoints,
                    CheckedUInt32(after.MaxLife, nameof(after.MaxLife)))]).ToFrameBytes(),
            ];
        }

        internal static DragonhowlSobOutput Prepare(WorldSession actor, SobNpc original, SobNpc after,
            SobNpcDamageResult result) => new(actor, original, after, result);

        internal async ValueTask<bool> PublishAsync(OwnedDragonhowlPreparation plan,
            WorldSessionStore.OwnedDragonhowlNonPlayer token, CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref started, 1, 0) != 0 || !TryCurrent(plan, token, out _) ||
                !plan.Store.TryBeginDragonhowlNonPlayerOutput(plan, token)) return false;
            foreach (var frame in frames)
            {
                if (!TryCurrent(plan, token, out var actor)) return false;
                if (!await SendAsync(plan, token, plan.Context.Connection, frame, cancellationToken).ConfigureAwait(false)) return false;
                foreach (var recipient in plan.Store.Snapshot())
                {
                    if (ReferenceEquals(recipient.Connection, plan.Context.Connection) ||
                        recipient.Session.Character.Uid == actorUid || !CanSeePlayer(recipient.Session.Character, actor.Character) ||
                        !plan.Store.TryGet(recipient.Connection, out var current) || !ReferenceEquals(current, recipient.Session)) continue;
                    if (!await SendAsync(plan, token, recipient.Connection, frame, cancellationToken).ConfigureAwait(false)) return false;
                }
            }
            return TryCurrent(plan, token, out _);
        }

        private bool TryCurrent(OwnedDragonhowlPreparation plan, WorldSessionStore.OwnedDragonhowlNonPlayer token,
            out WorldSession actor) => plan.Store.TryReadDragonhowlSobPublication(plan, token, out actor,
                out var actualOriginal, out var actualAfter, out var actualResult) && actor.Character.Uid == actorUid &&
            ReferenceEquals(actor.NativeGameClient, actorClient) && actualOriginal == original && actualAfter == after && actualResult == result;

        private async ValueTask<bool> SendAsync(OwnedDragonhowlPreparation plan,
            WorldSessionStore.OwnedDragonhowlNonPlayer token, ConquerTcpConnection recipient, byte[] frame, CancellationToken cancellationToken)
        {
            if (!TryCurrent(plan, token, out _)) return false;
            await recipient.SendAsync(frame.ToArray(), cancellationToken).ConfigureAwait(false);
            return TryCurrent(plan, token, out _);
        }
    }
}
