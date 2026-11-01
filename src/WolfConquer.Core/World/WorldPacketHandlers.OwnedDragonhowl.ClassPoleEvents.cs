using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private enum DragonhowlClassPole { None, Warrior, ClassPola, Ninja, Monk, Taoist, Fire, Pirate, Kungfu, Wind, Thunder }
    private sealed record DragonhowlClassPoleDefinition(DragonhowlClassPole Kind, string Schedule,
        int RequiredFamily, string ScoresAttribute, LegacyClassPolePlayerScoreStateStore Store);

    // The selector owns the mapping to the ten existing private store
    // instances. No caller supplies a score store, predicate or updater.
    private static DragonhowlClassPoleDefinition? SelectDragonhowlClassPole(SobNpc original)
    {
        if (!LegacySobNpcEventRules.TryGetClassPoleWarPole(original, out var schedule, out var family, out _)) return null;
        DragonhowlClassPoleDefinition? definition = schedule switch
        {
            "MsgWarriorGod" => new(DragonhowlClassPole.Warrior, schedule, 2, LegacyWarriorGodScoresAttribute, LegacyWarriorGodStates),
            "MsgClassPolaWar" => new(DragonhowlClassPole.ClassPola, schedule, 0, LegacyClassPolaWarScoresAttribute, LegacyClassPolaWarStates),
            "MsgNinjaMaster" => new(DragonhowlClassPole.Ninja, schedule, 5, LegacyNinjaMasterScoresAttribute, LegacyNinjaMasterStates),
            "MsgMonkMaster" => new(DragonhowlClassPole.Monk, schedule, 6, LegacyMonkMasterScoresAttribute, LegacyMonkMasterStates),
            LegacyTaoistMasterScheduleName => new(DragonhowlClassPole.Taoist, schedule, LegacyTaoistMasterClassFamily,
                LegacyTaoistMasterScoresAttribute, LegacyTaoistMasterStates),
            LegacyFireMasterScheduleName => new(DragonhowlClassPole.Fire, schedule, LegacyFireMasterClassFamily,
                LegacyFireMasterScoresAttribute, LegacyFireMasterStates),
            "MsgPirateMaster" => new(DragonhowlClassPole.Pirate, schedule, 7, LegacyPirateMasterScoresAttribute, LegacyPirateMasterStates),
            "MsgKungfuKing" => new(DragonhowlClassPole.Kungfu, schedule, 8, LegacyKungfuKingScoresAttribute, LegacyKungfuKingStates),
            LegacyWindWalkerScheduleName => new(DragonhowlClassPole.Wind, schedule, LegacyWindWalkerClassFamily,
                LegacyWindWalkerScoresAttribute, LegacyWindWalkerStates),
            LegacyThnderStrikeScheduleName => new(DragonhowlClassPole.Thunder, schedule, LegacyThnderStrikeClassFamily,
                LegacyThnderStrikeScoresAttribute, LegacyThnderStrikeStates),
            _ => null,
        };
        return definition?.RequiredFamily == family ? definition : null;
    }

    private static bool MatchesDragonhowlClassPoleClass(DragonhowlClassPole kind, long classCode) => kind switch
    {
        DragonhowlClassPole.Warrior => IsLegacyWarriorClass(classCode),
        DragonhowlClassPole.Ninja => IsLegacyNinjaClass(classCode),
        DragonhowlClassPole.Monk => IsLegacyMonkClass(classCode),
        DragonhowlClassPole.Taoist => HasDragonhowlClassPoleFamily(classCode, LegacyTaoistMasterClassFamily),
        DragonhowlClassPole.Fire => HasDragonhowlClassPoleFamily(classCode, LegacyFireMasterClassFamily),
        DragonhowlClassPole.Pirate => HasDragonhowlClassPoleFamily(classCode, 7),
        DragonhowlClassPole.Kungfu => HasDragonhowlClassPoleFamily(classCode, 8),
        DragonhowlClassPole.Wind => HasDragonhowlClassPoleFamily(classCode, LegacyWindWalkerClassFamily),
        DragonhowlClassPole.Thunder => HasDragonhowlClassPoleFamily(classCode, LegacyThnderStrikeClassFamily),
        DragonhowlClassPole.ClassPola => true,
        _ => false,
    };

    private static bool HasDragonhowlClassPoleFamily(long classCode, int family) =>
        classCode <= (long)int.MaxValue * 1_000 + 999 && NormalizeLegacyClassPoleWarClassFamily(classCode) == family;

    internal sealed partial class PreparedDragonhowlSobEvent
    {
        private static bool TryPrepareClassPole(WorldSessionStore.OwnedDragonhowlNonPlayer purpose,
            WorldSession actor, SobNpc original, SobNpc after, SobNpcDamageResult result,
            out PreparedDragonhowlSobEvent? plan)
        {
            plan = null;
            if (SelectDragonhowlClassPole(original) is not { } selected ||
                !MatchesDragonhowlClassPoleClass(selected.Kind, actor.Character.Class)) return false;
            var process = ReadOwnedSobEventAttribute(actor.Character,
                GetLegacyClassPoleWarAttributeNames(selected.Schedule, "Process")).Trim();
            if (!(process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
                process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) || process == "1") ||
                selected.Store.PrepareDragonhowlScore(purpose, actor, original, after, result) is not { } score) return false;
            var projected = actor with { Character = WithLegacyClassPolePlayerScoresAttribute(
                actor.Character, selected.ScoresAttribute, score.Remaining) };
            var frames = new List<SobEventFrames>
            {
                new(SobEventAudience.ClassPole, CreateLegacyClassPolePlayerScorePanelFrames(score.Scored).ToArray(), selected.Kind),
            };
            string? resetName = null;
            SobNpc? reset = null;
            if (result.Killed)
            {
                var winner = score.Winner;
                resetName = winner.PlayerName;
                projected = projected with { Character = WithLegacyClassPolePlayerFinishRoundAttributes(
                    projected.Character, selected.Schedule, winner, resetName) };
                reset = after with { Life = after.MaxLife, Name = resetName };
                frames.Add(new(SobEventAudience.All,
                    [LegacyMsgMessagePacket.SystemRedCenter($"The Round Ownered by {winner.PlayerName}.").ToFrameBytes()]));
                frames.Add(new(SobEventAudience.Visible, [LegacySobNpcSpawnPacket.FromSobNpc(reset).ToFrameBytes()]));
            }
            plan = new(purpose, actor, projected, original, after, result, SobEventKind.ClassPole,
                null, score, resetName, null, reset, frames.ToArray());
            return plan.IsCurrentPlain() && (reset is null || purpose.CanPrepareSobEventReset(plan));
        }
    }
}
