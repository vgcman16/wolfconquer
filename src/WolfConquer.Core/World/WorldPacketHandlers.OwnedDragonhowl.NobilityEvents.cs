using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static ulong ReadDragonhowlNobilityRank(CharacterSnapshot character)
    {
        var text = ReadOwnedSobEventAttribute(character, "NobilityRank", "Nobility");
        if (text.Length != 0)
        {
            if (!ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank))
                throw new InvalidDataException("ownedDragonhowlNobilityRankInvalid");
            if (rank != 0) return rank;
        }
        var paid = LegacyNativeAttributeMapProof.CaptureRaw(character.PaidNobility);
        var rows = paid.Fields.Where(row => StringComparer.OrdinalIgnoreCase.Equals(row.Key, "Rank")).ToArray();
        if (rows.Length > 1) throw new InvalidDataException("ownedDragonhowlNobilityRankAmbiguous");
        return (rows.Length == 0 ? string.Empty : rows[0].Value.Trim()) switch
        {
            "Knight" => 1, "Baron" => 3, "Earl" => 5, "Duke" => 7, "Prince" => 9, "King" => 12, _ => 0,
        };
    }

    private static string ReadDragonhowlNobilityProcess(CharacterSnapshot character, string schedule)
    {
        string[] selected = ["Legacy" + schedule + "Process", schedule + "Process", "Runtime" + schedule + "Process",
            "Legacy" + schedule + "Proces", schedule + "Proces", "Runtime" + schedule + "Proces"];
        var fields = LegacyNativeAttributeMapProof.CaptureRaw(character.Attributes).Fields;
        // The source's schedule-specific field overrides the general fallback,
        // including an explicitly empty value. Do not require an unused
        // general process field to agree with the selected schedule.
        if (fields.Keys.Any(key => selected.Contains(key, StringComparer.OrdinalIgnoreCase)))
            return ReadOwnedSobEventAttribute(character, selected);
        return ReadOwnedSobEventAttribute(character, "LegacyNobilityPoleProcess", "NobilityPoleProcess",
            "RuntimeNobilityPoleProcess", "LegacyNobilityPoleProces", "NobilityPoleProces", "RuntimeNobilityPoleProces");
    }

    internal sealed partial class PreparedDragonhowlSobEvent
    {
        private static bool TryPrepareNobility(WorldSessionStore.OwnedDragonhowlNonPlayer purpose,
            WorldSession actor, SobNpc original, SobNpc after, SobNpcDamageResult result,
            out PreparedDragonhowlSobEvent? plan)
        {
            plan = null;
            if (!LegacySobNpcEventRules.TryGetNobilityPoleSchedule(original, out var schedule, out var requiredRank) ||
                ReadDragonhowlNobilityRank(actor.Character) != requiredRank ||
                !ReadDragonhowlNobilityProcess(actor.Character, schedule).Trim()
                    .Equals("Alive", StringComparison.OrdinalIgnoreCase)) return false;
            // A UID-based Nobility pole can also be listed as a CTF base in
            // malformed/custom catalog state. That requires both real effects,
            // not silently substituting this one event participant.
            if (actor.Character.MapId == LegacyCaptureTheFlagMapId &&
                ReadCaptureTheFlagBases(ValidateDragonhowlCtfInputs(actor.Character))
                    .Any(row => row.Viable && row.Uid == (uint)original.Id)) return false;
            var displayRank = GetLegacyNobilityPoleDisplayRank(schedule);
            if (displayRank.Length == 0 || LegacyNobilityPoleStates.PrepareDragonhowlScore(
                purpose, actor, original, after, result, schedule, displayRank) is not { } score) return false;
            var projected = actor with { Character = WithLegacyNobilityPoleScoresAttribute(actor.Character, score.RemainingAll) };
            var frames = new List<SobEventFrames>
            {
                new(SobEventAudience.NobilityMap, CreateLegacyNobilityPoleScorePanelFrames(score.ScoredPartition).ToArray()),
            };
            string? resetName = null;
            SobNpc? reset = null;
            if (result.Killed)
            {
                var winner = score.Winner;
                resetName = winner.PlayerName;
                projected = projected with { Character = WithLegacyNobilityPoleFinishRoundAttributes(
                    projected.Character, winner, resetName) };
                reset = after with { Life = after.MaxLife, Name = resetName };
                frames.Add(new(SobEventAudience.All, [LegacyMsgMessagePacket.SystemRedCenter(
                    GetLegacyNobilityPoleFinishRoundMessage(schedule, winner.PlayerName)).ToFrameBytes()]));
                frames.Add(new(SobEventAudience.Visible, [LegacySobNpcSpawnPacket.FromSobNpc(reset).ToFrameBytes()]));
            }
            plan = new(purpose, actor, projected, original, after, result, SobEventKind.Nobility,
                null, null, resetName, null, reset, frames.ToArray(), score);
            return plan.IsCurrentPlain() && (reset is null || purpose.CanPrepareSobEventReset(plan));
        }
    }
}
