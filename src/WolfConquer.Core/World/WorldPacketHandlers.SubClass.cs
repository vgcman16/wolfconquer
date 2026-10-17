using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacySubClass)]
    private static async ValueTask HandleLegacySubClassAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacySubClassPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacySubClass packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacySubClass " +
                    $"{FormatSession(context)} action={packet.Action} subclassId={packet.SubClassId}; " +
                    "no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacySubClassAction.Open)
        {
            var response = CreateLegacySubClassGuiPacket(session.Character);
            await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacySubClass " +
                    $"{FormatSession(context)} action={packet.Action} subclassId={packet.SubClassId} " +
                    $"entries={response.Entries.Count} studyPoints={response.StudyPoints}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacySubClassAction.Pro)
        {
            if (!TryPromoteLegacySubClass(
                    session.Character,
                    packet.SubClassId,
                    context.WorldData,
                    out var updatedCharacter,
                    out var update,
                    out var skipReason))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacySubClass " +
                        $"{FormatSession(context)} action={packet.Action} subclassId={packet.SubClassId}; " +
                        $"source=MsgSubClass.Pro requires a learned subclass with phrase < 9, phrase < level, " +
                        $"and passing SubProfessionInfo.AllowUpdate. {skipReason}")
                    .ConfigureAwait(false);
                return;
            }

            var updatedSession = session with { Character = updatedCharacter };
            context.Sessions?.Set(context.Connection, updatedSession);
            var updatedContext = context with { Session = updatedSession };

            await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                        updatedSession.PlayerItems,
                        updatedSession.AlternateEquipmentActive).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacySubClass " +
                    $"{FormatSession(updatedContext)} action={packet.Action} subclassId={packet.SubClassId} " +
                    $"phrase={update.Level}; source=MsgSubClass.Pro increments Phrase, sends MartialPromoted, " +
                    "refreshes equipment, and refreshes the derived subclass hash.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacySubClassAction.Upgrade)
        {
            if (!TryUpgradeLegacySubClass(
                    session.Character,
                    packet.SubClassId,
                    context.WorldData,
                    out var updatedCharacter,
                    out var studyAnimation,
                    out var update,
                    out var cost,
                    out var newLevel,
                    out var remainingStudyPoints,
                    out var skipReason))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacySubClass " +
                        $"{FormatSession(context)} action={packet.Action} subclassId={packet.SubClassId}; " +
                        "source=MsgSubClass.Upgrade requires a learned subclass with level < 9, " +
                        "a matching LevelInfo cost row, and enough StudyPoints. " +
                        $"{skipReason}")
                    .ConfigureAwait(false);
                return;
            }

            var updatedSession = session with { Character = updatedCharacter };
            context.Sessions?.Set(context.Connection, updatedSession);
            var updatedContext = context with { Session = updatedSession };

            if (studyAnimation is not null)
            {
                await context.Connection.SendAsync(
                        LegacyStringPacket.PlayerEffect(
                            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                            "zf2-e300").ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Connection.SendAsync(studyAnimation.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }

            await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacySubClass " +
                    $"{FormatSession(updatedContext)} action={packet.Action} subclassId={packet.SubClassId} " +
                    $"newLevel={newLevel} cost={cost} studyPoints={remainingStudyPoints}; " +
                    "source=MsgSubClass.Upgrade applies RemoveStudy semantics, sends SendUpdate, " +
                    "and persists the incremented subclass level.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacySubClassAction.SwitchSubClass)
        {
            if (!TryActivateLegacySubClass(session.Character, packet.SubClassId, out var updatedCharacter, out var update))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacySubClass " +
                        $"{FormatSession(context)} action={packet.Action} subclassId={packet.SubClassId}; " +
                        "source=MsgSubClass.SwitchSubClass requires a learned subclass or User subclass 0.")
                    .ConfigureAwait(false);
                return;
            }

            var updatedSession = session with { Character = updatedCharacter };
            context.Sessions?.Set(context.Connection, updatedSession);
            var updatedContext = context with { Session = updatedSession };

            await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacySubClass " +
                    $"{FormatSession(updatedContext)} action={packet.Action} subclassId={packet.SubClassId} " +
                    $"level={update.Level}; source=MsgSubClass.SwitchSubClass sends ActivateSubClass, " +
                    "sets ActiveSubclass, and refreshes subclass spawn fields.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacySubClass " +
                $"{FormatSession(context)} action={packet.Action} subclassId={packet.SubClassId}; " +
                "subclass mutation is not modeled.")
            .ConfigureAwait(false);
    }

    private static LegacySubClassGuiPacket CreateLegacySubClassGuiPacket(CharacterSnapshot character)
    {
        var state = character.Attributes.TryGetValue("SubProfInfo", out var rawState) &&
            LegacySubClassHitpointsCalculator.TryParseLegacyState(rawState, out var parsed)
                ? parsed
                : new LegacySubClassState(StudyPoints: 0, ActiveSubClass: 0, Entries: []);
        var entries = state.Entries
            .Select(entry => new LegacySubClassGuiEntry(entry.Id, entry.Phrase, entry.Level))
            .ToArray();

        return new LegacySubClassGuiPacket(
            LegacySubClassAction.ShowGUI,
            state.StudyPoints,
            StudyReceive: 0,
            entries);
    }

    private static bool TryPromoteLegacySubClass(
        CharacterSnapshot character,
        sbyte requestedSubClassId,
        WorldDataCatalog? worldData,
        out CharacterSnapshot updatedCharacter,
        out LegacySubClassUpdatePacket update,
        out string skipReason)
    {
        updatedCharacter = character;
        update = default;
        skipReason = string.Empty;

        if (requestedSubClassId < 0)
        {
            skipReason = "Requested subclass id is negative.";
            return false;
        }

        var subClassId = (byte)requestedSubClassId;
        var state = character.Attributes.TryGetValue("SubProfInfo", out var rawState) &&
            LegacySubClassHitpointsCalculator.TryParseLegacyState(rawState, out var parsed)
                ? parsed
                : new LegacySubClassState(StudyPoints: 0, ActiveSubClass: 0, Entries: []);
        var entries = state.Entries.ToArray();
        var entryIndex = Array.FindIndex(entries, entry => entry.Id == subClassId);
        if (entryIndex < 0)
        {
            skipReason = "Subclass is not learned.";
            return false;
        }

        var entry = entries[entryIndex];
        if (entry.Phrase >= 9)
        {
            skipReason = "Subclass phrase is already at the source cap.";
            return false;
        }

        if (entry.Phrase >= entry.Level)
        {
            skipReason = "Subclass phrase must be below subclass level.";
            return false;
        }

        var nextPhrase = checked((byte)(entry.Phrase + 1));
        if (!AllowsLegacySubClassUpdate(character, worldData, subClassId, nextPhrase, out skipReason))
        {
            return false;
        }

        entries[entryIndex] = entry with { Phrase = nextPhrase };
        var promotedState = state with { Entries = entries };
        updatedCharacter = WithCharacterAttribute(character, "SubProfInfo", FormatLegacySubClassState(promotedState));
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "SubClassHashPoint",
            CalculateLegacySubClassHashPoint(promotedState).ToString(CultureInfo.InvariantCulture));
        update = new LegacySubClassUpdatePacket(
            LegacySubClassAction.MartialPromoted,
            subClassId,
            nextPhrase,
            StudyReceive: 0);
        return true;
    }

    private static bool TryUpgradeLegacySubClass(
        CharacterSnapshot character,
        sbyte requestedSubClassId,
        WorldDataCatalog? worldData,
        out CharacterSnapshot updatedCharacter,
        out LegacySubClassGuiPacket? studyAnimation,
        out LegacySubClassUpdatePacket update,
        out ushort cost,
        out byte newLevel,
        out ushort remainingStudyPoints,
        out string skipReason)
    {
        updatedCharacter = character;
        studyAnimation = null;
        update = default;
        cost = 0;
        newLevel = 0;
        remainingStudyPoints = 0;
        skipReason = string.Empty;

        if (requestedSubClassId < 0)
        {
            skipReason = "Requested subclass id is negative.";
            return false;
        }

        var subClassId = (byte)requestedSubClassId;
        var state = character.Attributes.TryGetValue("SubProfInfo", out var rawState) &&
            LegacySubClassHitpointsCalculator.TryParseLegacyState(rawState, out var parsed)
                ? parsed
                : new LegacySubClassState(StudyPoints: 0, ActiveSubClass: 0, Entries: []);
        var entries = state.Entries.ToArray();
        var entryIndex = Array.FindIndex(entries, entry => entry.Id == subClassId);
        if (entryIndex < 0)
        {
            skipReason = "Subclass is not learned.";
            return false;
        }

        var entry = entries[entryIndex];
        if (entry.Level >= 9)
        {
            skipReason = "Subclass level is already at the source cap.";
            return false;
        }

        if (worldData is null)
        {
            skipReason = "WorldDataCatalog.LevelExperience is not available.";
            return false;
        }

        if (!worldData.LevelExperienceByTypeAndLevel.TryGetValue((subClassId, entry.Level), out var levelInfo))
        {
            skipReason =
                $"levexp.txt row for subclass {subClassId.ToString(CultureInfo.InvariantCulture)} " +
                $"level {entry.Level.ToString(CultureInfo.InvariantCulture)} is not available.";
            return false;
        }

        if (levelInfo.Experience is < 0 or > ushort.MaxValue)
        {
            skipReason =
                $"levexp.txt cost {levelInfo.Experience.ToString(CultureInfo.InvariantCulture)} " +
                "is outside the source ushort StudyPoints range.";
            return false;
        }

        cost = (ushort)levelInfo.Experience;
        if (state.StudyPoints < cost)
        {
            skipReason =
                $"study points {state.StudyPoints.ToString(CultureInfo.InvariantCulture)} are below required cost " +
                $"{cost.ToString(CultureInfo.InvariantCulture)}.";
            return false;
        }

        remainingStudyPoints = state.StudyPoints;
        if (state.StudyPoints > cost)
        {
            remainingStudyPoints = checked((ushort)(state.StudyPoints - cost));
            studyAnimation = new LegacySubClassGuiPacket(
                LegacySubClassAction.Animation,
                remainingStudyPoints,
                StudyReceive: 0,
                Entries: []);
        }

        newLevel = checked((byte)(entry.Level + 1));
        entries[entryIndex] = entry with { Level = newLevel };
        var upgradedState = state with
        {
            StudyPoints = remainingStudyPoints,
            Entries = entries,
        };

        updatedCharacter = WithCharacterAttribute(character, "SubProfInfo", FormatLegacySubClassState(upgradedState));
        update = new LegacySubClassUpdatePacket(
            LegacySubClassAction.SendUpdate,
            subClassId,
            Level: 0,
            StudyReceive: 0);
        return true;
    }

    private static bool TryActivateLegacySubClass(
        CharacterSnapshot character,
        sbyte requestedSubClassId,
        out CharacterSnapshot updatedCharacter,
        out LegacySubClassUpdatePacket update)
    {
        updatedCharacter = character;
        update = default;

        if (requestedSubClassId < 0)
        {
            return false;
        }

        var subClassId = (byte)requestedSubClassId;
        var state = character.Attributes.TryGetValue("SubProfInfo", out var rawState) &&
            LegacySubClassHitpointsCalculator.TryParseLegacyState(rawState, out var parsed)
                ? parsed
                : new LegacySubClassState(StudyPoints: 0, ActiveSubClass: 0, Entries: []);
        var entry = state.Entries.FirstOrDefault(candidate => candidate.Id == subClassId);
        if (entry is null && subClassId != 0)
        {
            return false;
        }

        var level = entry?.Level ?? (byte)0;
        var activatedState = state with { ActiveSubClass = subClassId };
        updatedCharacter = WithCharacterAttribute(character, "SubProfInfo", FormatLegacySubClassState(activatedState));
        updatedCharacter = WithCharacterAttribute(updatedCharacter, "ActiveSublass", subClassId.ToString(CultureInfo.InvariantCulture));
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "SubClassHashPoint",
            CalculateLegacySubClassHashPoint(activatedState).ToString(CultureInfo.InvariantCulture));
        update = new LegacySubClassUpdatePacket(
            LegacySubClassAction.ActivateSubClass,
            subClassId,
            level,
            StudyReceive: 0);
        return true;
    }

    private static bool AllowsLegacySubClassUpdate(
        CharacterSnapshot character,
        WorldDataCatalog? worldData,
        byte subClassId,
        byte phrase,
        out string skipReason)
    {
        skipReason = string.Empty;
        if (worldData is null)
        {
            skipReason = "WorldDataCatalog.SubProfessions is not available.";
            return false;
        }

        var sectionId = GetLegacySubClassSubProfessionSectionId(subClassId, phrase);
        if (!worldData.SubProfessionsById.TryGetValue(sectionId, out var profession))
        {
            skipReason = $"SubProfessionInfo.ini section [{sectionId}] is not available.";
            return false;
        }

        if (character.Level < profession.RequiredLevel)
        {
            skipReason =
                $"character level {character.Level.ToString(CultureInfo.InvariantCulture)} is below required level " +
                $"{profession.RequiredLevel.ToString(CultureInfo.InvariantCulture)}.";
            return false;
        }

        if (character.Reborn < profession.RequiredMetempsychosis)
        {
            skipReason =
                $"character metempsychosis {character.Reborn.ToString(CultureInfo.InvariantCulture)} is below required metempsychosis " +
                $"{profession.RequiredMetempsychosis.ToString(CultureInfo.InvariantCulture)}.";
            return false;
        }

        return true;
    }

    private static string FormatLegacySubClassState(LegacySubClassState state)
    {
        var builder = new StringBuilder();
        builder.Append(state.StudyPoints.ToString(CultureInfo.InvariantCulture));
        builder.Append('#');
        builder.Append(state.ActiveSubClass.ToString(CultureInfo.InvariantCulture));
        builder.Append('#');
        builder.Append(state.Entries.Count.ToString(CultureInfo.InvariantCulture));
        builder.Append('#');

        foreach (var entry in state.Entries)
        {
            builder.Append(entry.Id.ToString(CultureInfo.InvariantCulture));
            builder.Append('#');
            builder.Append(entry.Level.ToString(CultureInfo.InvariantCulture));
            builder.Append('#');
            builder.Append(entry.Phrase.ToString(CultureInfo.InvariantCulture));
            builder.Append('#');
        }

        return builder.ToString();
    }

    private static uint CalculateLegacySubClassHashPoint(LegacySubClassState state)
    {
        var total = 0u;
        foreach (var entry in state.Entries)
        {
            if (entry.Id is < 1 or > 9)
            {
                continue;
            }

            var place = 1u;
            for (var index = 1; index < entry.Id; index++)
            {
                place *= 10;
            }

            total += place * entry.Phrase;
        }

        return total;
    }

    private static int GetLegacySubClassSubProfessionSectionId(byte subClassId, byte phrase)
    {
        return phrase == 0
            ? subClassId
            : checked((subClassId * 10) + phrase);
    }

}
