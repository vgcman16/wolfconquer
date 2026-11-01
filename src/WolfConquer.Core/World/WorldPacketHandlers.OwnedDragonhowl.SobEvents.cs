using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static bool TryPrepareOwnedDragonhowlSobEvent(WorldSessionStore.OwnedDragonhowlNonPlayer purpose,
        WorldSession calculatedActor, SobNpc original, SobNpc projectedAfter, SobNpcDamageResult projectedResult,
        out PreparedDragonhowlSobEvent? plan) => PreparedDragonhowlSobEvent.TryPrepare(
            purpose, calculatedActor, original, projectedAfter, projectedResult, out plan);

    // Closed concrete event plan. Only the privately registered proc target
    // can issue it, and the actual Store event stage must consume it. CTF has
    // actor-held scores; SuperGuildWar also owns the real global score graph.
    internal sealed partial class PreparedDragonhowlSobEvent
    {
        private readonly WorldSessionStore.OwnedDragonhowlNonPlayer purpose;
        private readonly LegacyNativeAttributeMapProof beforeAttributes, afterAttributes;
        private readonly LegacySuperGuildWarStateStore.PreparedDragonhowlScore? score;
        private readonly LegacyClassPolePlayerScoreStateStore.PreparedDragonhowlScore? classScore;
        private readonly LegacyNobilityPoleScoreStateStore.PreparedDragonhowlScore? nobilityScore;
        private readonly SobEventKind kind;
        private readonly SobNpc original, damageAfter;
        private readonly SobNpcDamageResult result;
        private readonly SobEventFrames[] output;
        private bool scorePublished;
        private int outputStarted;
        internal WorldSession ActorBefore { get; }
        internal WorldSession ActorAfter { get; }
        internal string? ResetName { get; }
        internal uint? ResetGuildId { get; }
        internal SobNpc? ExpectedResetSob { get; }

        private PreparedDragonhowlSobEvent(WorldSessionStore.OwnedDragonhowlNonPlayer purpose,
            WorldSession actor, WorldSession after, SobNpc original, SobNpc damageAfter, SobNpcDamageResult result,
            SobEventKind kind, LegacySuperGuildWarStateStore.PreparedDragonhowlScore? score,
            LegacyClassPolePlayerScoreStateStore.PreparedDragonhowlScore? classScore, string? resetName, uint? resetGuild,
            SobNpc? reset, SobEventFrames[] output, LegacyNobilityPoleScoreStateStore.PreparedDragonhowlScore? nobilityScore = null)
        {
            if (!(kind == SobEventKind.CaptureTheFlag && score is null && classScore is null && nobilityScore is null ||
                kind == SobEventKind.SuperGuildWar && score is not null && classScore is null && nobilityScore is null ||
                kind == SobEventKind.ClassPole && score is null && classScore is not null && nobilityScore is null ||
                kind == SobEventKind.Nobility && score is null && classScore is null && nobilityScore is not null))
                throw new InvalidDataException("ownedDragonhowlConflictingScoreOwners");
            this.purpose = purpose; ActorBefore = actor; ActorAfter = after;
            this.original = original; this.damageAfter = damageAfter; this.result = result;
            this.kind = kind; this.score = score; this.classScore = classScore;
            this.nobilityScore = nobilityScore;
            ResetName = resetName; ResetGuildId = resetGuild; ExpectedResetSob = reset;
            beforeAttributes = LegacyNativeAttributeMapProof.CaptureRaw(actor.Character.Attributes);
            afterAttributes = LegacyNativeAttributeMapProof.CaptureRaw(after.Character.Attributes);
            this.output = output;
        }

        internal static bool TryPrepare(WorldSessionStore.OwnedDragonhowlNonPlayer purpose,
            WorldSession actor, SobNpc original, SobNpc after, SobNpcDamageResult result,
            out PreparedDragonhowlSobEvent? plan)
        {
            plan = null;
            if (!purpose.CoversSobEventPreparation(actor, original, after, result) || result.AlreadyDead ||
                result.DamageApplied == 0 || LegacySobNpcEventRules.IsTrainingSob(original)) return false;
            if (LegacySobNpcEventRules.IsSuperGuildWarPole(original))
                return TryPrepareSuper(purpose, actor, original, after, result, out plan);
            if (LegacySobNpcEventRules.TryGetClassPoleWarPole(original, out _, out _, out _))
                return TryPrepareClassPole(purpose, actor, original, after, result, out plan);
            if (LegacySobNpcEventRules.TryGetNobilityPoleSchedule(original, out _, out _))
                return TryPrepareNobility(purpose, actor, original, after, result, out plan);
            if (!TryPrepareDragonhowlCaptureTheFlag(actor, original, after, result,
                out var projected, out var reset, out var resetName, out var resetGuild)) return false;
            var frames = reset is null ? Array.Empty<SobEventFrames>() : new[]
            {
                new SobEventFrames(SobEventAudience.Visible,
                    [LegacySobNpcSpawnPacket.FromSobNpc(reset).ToFrameBytes()]),
                new SobEventFrames(SobEventAudience.Visible,
                    [new LegacyCaptureTheFlagUpdatePacket(LegacyCaptureTheFlagUpdateMode.OccupiedBase,
                        checked((uint)(original.Id % 100)), Parameter2: 0, Count: 1).ToFrameBytes()]),
            };
            plan = new(purpose, actor, projected, original, after, result, SobEventKind.CaptureTheFlag,
                null, null, resetName, resetGuild, reset, frames);
            return plan.IsCurrentPlain() && (reset is null || purpose.CanPrepareSobEventReset(plan));
        }

        private static bool TryPrepareSuper(WorldSessionStore.OwnedDragonhowlNonPlayer purpose,
            WorldSession actor, SobNpc original, SobNpc after, SobNpcDamageResult result,
            out PreparedDragonhowlSobEvent? plan)
        {
            plan = null;
            var process = ReadOwnedSobEventAttribute(actor.Character, GetLegacySuperGuildWarAttributeNames("Process", "Proces")).Trim();
            if (!(process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
                process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) || process == "1") ||
                !ulong.TryParse(ReadOwnedSobEventAttribute(actor.Character, "GuildID", "GuildId"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var guildId) || guildId == 0) return false;
            var guildName = ReadOwnedSobEventAttribute(actor.Character, "GuildName", "LegacyGuildName").Trim();
            // The file fallback needs a retained real Guilds participant. A
            // missing name is a pre-cost refusal, never a fabricated guild.
            if (guildName.Length == 0 || LegacySuperGuildWarStates.PrepareDragonhowlScore(
                purpose, actor, original, after, result, guildId, guildName) is not { } score) return false;
            var projected = actor with { Character = WithLegacySuperGuildWarScoresAttribute(actor.Character, score.Remaining) };
            var frames = new List<SobEventFrames>
            {
                new(SobEventAudience.SuperGuildWarMap, CreateLegacySuperGuildWarScorePanelFrames(score.Scored).ToArray()),
            };
            SobNpc? reset = null;
            string? resetName = null;
            if (result.Killed)
            {
                var winner = score.Winner;
                resetName = winner.GuildName;
                projected = projected with { Character = WithLegacySuperGuildWarFinishRoundAttributes(
                    projected.Character, winner, resetName) };
                reset = after with { Life = after.MaxLife, Name = resetName };
                var message = $"Congratulations to {winner.GuildName}, they've won the Super Guild War with a score of {winner.Score.ToString(CultureInfo.InvariantCulture)}";
                frames.Add(new(SobEventAudience.All,
                    [LegacyMsgMessagePacket.SystemWhiteTalk(message).ToFrameBytes(), LegacyMsgMessagePacket.SystemRedCenter(message).ToFrameBytes()]));
                frames.Add(new(SobEventAudience.Visible, [LegacySobNpcSpawnPacket.FromSobNpc(reset).ToFrameBytes()]));
            }
            plan = new(purpose, actor, projected, original, after, result, SobEventKind.SuperGuildWar,
                score, null, resetName, null, reset, frames.ToArray());
            return plan.IsCurrentPlain() && (reset is null || purpose.CanPrepareSobEventReset(plan));
        }

        internal bool IsPreparedFor(WorldSessionStore.OwnedDragonhowlNonPlayer token) => ReferenceEquals(purpose, token);
        internal bool UsesScore(LegacySuperGuildWarStateStore.PreparedDragonhowlScore actual) =>
            kind == SobEventKind.SuperGuildWar && ReferenceEquals(score, actual);
        internal bool UsesClassScore(LegacyClassPolePlayerScoreStateStore.PreparedDragonhowlScore actual) =>
            kind == SobEventKind.ClassPole && ReferenceEquals(classScore, actual);
        internal bool UsesNobilityScore(LegacyNobilityPoleScoreStateStore.PreparedDragonhowlScore actual) =>
            kind == SobEventKind.Nobility && ReferenceEquals(nobilityScore, actual);
        internal IDisposable EnterCommitScope() => kind switch
        {
            SobEventKind.SuperGuildWar => score!.EnterCommitScope(),
            SobEventKind.ClassPole => classScore!.EnterCommitScope(),
            SobEventKind.Nobility => nobilityScore!.EnterCommitScope(),
            SobEventKind.CaptureTheFlag => EmptyEventScope.Instance,
            _ => throw new InvalidOperationException("ownedDragonhowlScoreOwnerInvalid"),
        };
        internal bool IsCurrentPlain() => beforeAttributes.IsCurrentPlain() && afterAttributes.IsCurrentPlain() &&
            (kind switch
            {
                SobEventKind.SuperGuildWar => score!.IsCurrentPlain(),
                SobEventKind.ClassPole => classScore!.IsCurrentPlain(),
                SobEventKind.Nobility => nobilityScore!.IsCurrentPlain(),
                SobEventKind.CaptureTheFlag => true,
                _ => false,
            });

        internal bool PublishScorePrepared()
        {
            if (scorePublished || !IsCurrentPlain() || !purpose.CoversSobEventCommit(this)) return false;
            if (!(kind switch
            {
                SobEventKind.SuperGuildWar => score!.PublishPrepared(this),
                SobEventKind.ClassPole => classScore!.PublishPrepared(this),
                SobEventKind.Nobility => nobilityScore!.PublishPrepared(this),
                SobEventKind.CaptureTheFlag => true,
                _ => false,
            })) return false;
            scorePublished = true;
            return true;
        }

        internal async ValueTask<bool> PublishAsync(OwnedDragonhowlPreparation plan,
            WorldSessionStore.OwnedDragonhowlNonPlayer token, CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref outputStarted, 1, 0) != 0 || !TryCurrent(plan, token, out _) ||
                !plan.Store.TryBeginDragonhowlNonPlayerOutput(plan, token)) return false;
            foreach (var group in output)
            {
                if (!TryCurrent(plan, token, out var actor)) return false;
                if (group.Audience == SobEventAudience.Visible &&
                    !await SendGroupAsync(plan, token, plan.Context.Connection, group.Frames, cancellationToken).ConfigureAwait(false)) return false;
                foreach (var peer in plan.Store.Snapshot())
                {
                    if (!plan.Store.TryGet(peer.Connection, out var current) || !ReferenceEquals(current, peer.Session)) continue;
                    if (group.Audience == SobEventAudience.Visible &&
                        (ReferenceEquals(peer.Connection, plan.Context.Connection) || peer.Session.Character.Uid == actor.Character.Uid ||
                         !CanSeePlayer(peer.Session.Character, actor.Character))) continue;
                    if (group.Audience == SobEventAudience.SuperGuildWarMap &&
                        peer.Session.Character.MapId != LegacySobNpcEventRules.SuperGuildWarMapId) continue;
                    if (group.Audience == SobEventAudience.NobilityMap &&
                        peer.Session.Character.MapId != LegacySobNpcEventRules.NobilityPoleMapId) continue;
                    if (group.Audience == SobEventAudience.ClassPole &&
                        (peer.Session.Character.MapId != LegacySobNpcEventRules.ClassPoleWarMapId ||
                         !MatchesDragonhowlClassPoleClass(group.ClassPole, peer.Session.Character.Class))) continue;
                    if (!await SendGroupAsync(plan, token, peer.Connection, group.Frames, cancellationToken).ConfigureAwait(false)) return false;
                }
            }
            return TryCurrent(plan, token, out _);
        }

        private bool TryCurrent(OwnedDragonhowlPreparation plan, WorldSessionStore.OwnedDragonhowlNonPlayer token,
            out WorldSession actor)
        {
            actor = null!;
            return scorePublished && IsPreparedFor(token) && IsCurrentPlain() &&
                plan.Store.TryReadDragonhowlSobEventPublication(plan, token, this, out actor,
                    out var actualOriginal, out var actualAfter, out var actualResult) && actualOriginal == original &&
                actualAfter == (ExpectedResetSob ?? damageAfter) && actualResult == result;
        }

        private async ValueTask<bool> SendGroupAsync(OwnedDragonhowlPreparation plan,
            WorldSessionStore.OwnedDragonhowlNonPlayer token, ConquerTcpConnection recipient,
            byte[][] frames, CancellationToken cancellationToken)
        {
            foreach (var frame in frames)
            {
                if (!TryCurrent(plan, token, out _)) return false;
                await recipient.SendAsync(frame.ToArray(), cancellationToken).ConfigureAwait(false);
                if (!TryCurrent(plan, token, out _)) return false;
            }
            return true;
        }

        private enum SobEventKind { CaptureTheFlag, SuperGuildWar, ClassPole, Nobility }
        private enum SobEventAudience { Visible, SuperGuildWarMap, ClassPole, NobilityMap, All }
        private readonly record struct SobEventFrames(SobEventAudience Audience, byte[][] Frames,
            DragonhowlClassPole ClassPole = DragonhowlClassPole.None);
        private sealed class EmptyEventScope : IDisposable
        {
            internal static readonly EmptyEventScope Instance = new();
            public void Dispose() { }
        }
    }
}
