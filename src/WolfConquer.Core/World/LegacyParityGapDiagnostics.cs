using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.World;

public enum LegacyParityGapCategory
{
    UnsupportedPacket,
    Ui,
    Npc,
    Item,
    Magic,
    Monster,
    Action,
    Runtime,
}

public enum LegacyParityGapSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record LegacyParityGapDiagnostic(
    LegacyParityGapCategory Category,
    LegacyParityGapSeverity Severity,
    string Gap,
    string DedupeKey,
    string Message)
{
    public string FormatLogMessage() => Message;

    public override string ToString() => Message;
}

public static class LegacyParityGapDiagnostics
{
    private const int MaxTokenLength = 80;
    private const int MaxReasonLength = 180;
    private const string LayTrapFloorEffectNextSeam = "lay-trap-floor-effect-runtime";
    private const string LayTrapFloorEffectSource = "MsgAttackPacket.MagicSort.LayTrap";
    private const string LayTrapFloorEffectSourceEvidence = "MsgAttackPacket.LayTrap.Execute+MsgDataPacket.Player.FloorSpells";

    private static readonly string[] SensitiveKeyFragments =
    [
        "account",
        "character",
        "connection",
        "email",
        "endpoint",
        "password",
        "player",
        "remote",
        "session",
        "token",
        "trace",
        "username",
    ];

    public static LegacyParityGapDiagnostic UnsupportedPacket(
        ushort opcode,
        string? packet = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var facts = new[]
        {
            Fact("opcode", opcode),
            Fact("packet", packet),
        };

        return Create(
            LegacyParityGapCategory.UnsupportedPacket,
            "unsupported-packet",
            reason ?? "unsupported packet is not modeled",
            severity,
            facts,
            facts);
    }

    public static LegacyParityGapDiagnostic UnhandledUiClick(
        string surface,
        ushort? opcode = null,
        uint? controlId = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var facts = new[]
        {
            Fact("controlId", controlId),
            Fact("opcode", opcode),
            Fact("surface", surface),
        };

        return Create(
            LegacyParityGapCategory.Ui,
            "unhandled-ui-click",
            reason ?? "unhandled-click UI action is not modeled",
            severity,
            facts,
            facts);
    }

    public static LegacyParityGapDiagnostic ClickedNpc(
        uint npcId,
        ushort opcode = WorldPacketOpcodes.LegacyNpcReply,
        uint? mapId = null,
        uint? optionId = null,
        uint? interactType = null,
        string? action = null,
        string? source = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var facts = new List<KeyValuePair<string, object?>>
        {
            Fact("map", mapId),
            Fact("npcId", npcId),
            Fact("opcode", opcode),
        };
        AddOptionalFact(facts, "action", action);
        AddOptionalFact(facts, "interactType", interactType);
        AddOptionalFact(facts, "optionId", optionId);
        AddOptionalFact(facts, "source", source);
        var dedupeFacts = new[]
        {
            Fact("npcId", npcId),
            Fact("opcode", opcode),
        };

        return Create(
            LegacyParityGapCategory.Npc,
            "clicked-npc",
            reason ?? "unhandled-click NPC click is not modeled",
            severity,
            facts,
            dedupeFacts);
    }

    public static LegacyParityGapDiagnostic MissingNpcScript(
        LegacyNpcScriptRuntimeResolution resolution,
        ushort opcode = WorldPacketOpcodes.LegacyNpcServer,
        uint? mapId = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        return MissingNpcScript(
            resolution.NpcId,
            opcode,
            resolution.OptionId,
            resolution.InteractType,
            resolution.ServerAction,
            resolution.Reason,
            resolution.ActionCategory,
            mapId,
            resolution.NpcEnumName,
            resolution.SourceMethodName,
            resolution.NextSeam,
            reason,
            severity);
    }

    public static LegacyParityGapDiagnostic MissingNpcScript(
        uint npcId,
        ushort opcode = WorldPacketOpcodes.LegacyNpcServer,
        byte? optionId = null,
        byte? interactType = null,
        LegacyNpcServerAction? action = null,
        LegacyNpcScriptRuntimeUnsupportedReason? unsupportedReason = null,
        LegacyNpcScriptRuntimeActionCategory? actionCategory = null,
        uint? mapId = null,
        string? npcEnumName = null,
        string? source = null,
        string? nextSeam = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var facts = new List<KeyValuePair<string, object?>>
        {
            Fact("npcId", npcId),
            Fact("opcode", opcode),
        };
        AddOptionalFact(facts, "action", action);
        AddOptionalFact(facts, "actionCategory", actionCategory);
        AddOptionalFact(facts, "interactType", interactType);
        AddOptionalFact(facts, "map", mapId);
        AddOptionalFact(facts, "nextSeam", nextSeam);
        AddOptionalFact(facts, "npcEnum", npcEnumName);
        AddOptionalFact(facts, "optionId", optionId);
        AddOptionalFact(facts, "source", source);
        AddOptionalFact(facts, "unsupportedReason", unsupportedReason);

        var dedupeFacts = new List<KeyValuePair<string, object?>>
        {
            Fact("npcId", npcId),
            Fact("opcode", opcode),
        };
        AddOptionalFact(dedupeFacts, "action", action);
        AddOptionalFact(dedupeFacts, "interactType", interactType);
        AddOptionalFact(dedupeFacts, "nextSeam", nextSeam);
        AddOptionalFact(dedupeFacts, "optionId", optionId);
        AddOptionalFact(dedupeFacts, "unsupportedReason", unsupportedReason);

        return Create(
            LegacyParityGapCategory.Npc,
            "missing-npc-script",
            reason ?? "missing NPC script runtime path is not modeled",
            severity,
            facts,
            dedupeFacts);
    }

    public static LegacyParityGapDiagnostic UnsupportedItemUse(
        uint itemId,
        uint? action = null,
        ushort opcode = WorldPacketOpcodes.MsgItemUsage,
        string? actionName = null,
        uint? itemUid = null,
        string? source = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var facts = new List<KeyValuePair<string, object?>>
        {
            Fact("action", action),
            Fact("itemId", itemId),
            Fact("opcode", opcode),
        };
        AddOptionalFact(facts, "actionName", actionName);
        AddOptionalFact(facts, "itemUid", itemUid);
        AddOptionalFact(facts, "source", source);
        var dedupeFacts = new[]
        {
            Fact("action", action),
            Fact("itemId", itemId),
            Fact("opcode", opcode),
        };

        return Create(
            LegacyParityGapCategory.Item,
            "item-use",
            reason ?? "item use is not modeled",
            severity,
            facts,
            dedupeFacts);
    }

    public static LegacyParityGapDiagnostic UnsupportedMagic(
        uint spellId,
        int? sort = null,
        ushort opcode = WorldPacketOpcodes.MsgAttack,
        string? skillName = null,
        string? targetKind = null,
        uint? mapId = null,
        string? resource = null,
        string? source = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var facts = new List<KeyValuePair<string, object?>>
        {
            Fact("opcode", opcode),
            Fact("skillId", spellId),
            Fact("sort", sort),
            Fact("spellId", spellId),
        };
        AddOptionalFact(facts, "map", mapId);
        AddOptionalFact(facts, "resource", resource);
        AddOptionalFact(facts, "skillName", skillName);
        AddOptionalFact(facts, "source", source);
        AddOptionalFact(facts, "targetKind", targetKind);
        var dedupeFacts = new[]
        {
            Fact("opcode", opcode),
            Fact("sort", sort),
            Fact("spellId", spellId),
        };

        return Create(
            LegacyParityGapCategory.Magic,
            "unsupported-magic",
            reason ?? "unsupported magic is not modeled",
            severity,
            facts,
            dedupeFacts);
    }

    public static LegacyParityGapDiagnostic UnsupportedSkill(
        uint skillId,
        int? sort = null,
        ushort opcode = WorldPacketOpcodes.MsgAttack,
        string? skillName = null,
        string? targetKind = null,
        uint? mapId = null,
        string? resource = null,
        string? source = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var facts = new List<KeyValuePair<string, object?>>
        {
            Fact("opcode", opcode),
            Fact("skillId", skillId),
            Fact("sort", sort),
        };
        AddOptionalFact(facts, "map", mapId);
        AddOptionalFact(facts, "resource", resource);
        AddOptionalFact(facts, "skillName", skillName);
        AddOptionalFact(facts, "source", source);
        AddOptionalFact(facts, "targetKind", targetKind);
        var dedupeFacts = new[]
        {
            Fact("opcode", opcode),
            Fact("sort", sort),
            Fact("skillId", skillId),
        };

        return Create(
            LegacyParityGapCategory.Magic,
            "unsupported-skill",
            reason ?? "unsupported skill is not modeled",
            severity,
            facts,
            dedupeFacts);
    }

    public static LegacyParityGapDiagnostic LayTrapFloorEffectRuntime(
        uint spellId,
        int sort = (int)LegacyMagicSort.LayTrap,
        ushort opcode = WorldPacketOpcodes.MsgAttack,
        uint? floorItemId = null,
        uint? mapId = null,
        string? nextSeam = null,
        string? source = null,
        string? sourceEvidence = null,
        string? spellName = null,
        string? targetKind = null,
        string? trigger = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var resolvedNextSeam = nextSeam ?? LayTrapFloorEffectNextSeam;
        var facts = new List<KeyValuePair<string, object?>>
        {
            Fact("nextSeam", resolvedNextSeam),
            Fact("opcode", opcode),
            Fact("sort", sort),
            Fact("source", source ?? LayTrapFloorEffectSource),
            Fact("sourceEvidence", sourceEvidence ?? LayTrapFloorEffectSourceEvidence),
            Fact("spellId", spellId),
        };
        AddOptionalFact(facts, "floorItemId", floorItemId);
        AddOptionalFact(facts, "map", mapId);
        AddOptionalFact(facts, "spellName", spellName);
        AddOptionalFact(facts, "targetKind", targetKind);
        AddOptionalFact(facts, "trigger", trigger);

        var dedupeFacts = new List<KeyValuePair<string, object?>>
        {
            Fact("nextSeam", resolvedNextSeam),
            Fact("opcode", opcode),
            Fact("sort", sort),
            Fact("spellId", spellId),
        };
        AddOptionalFact(dedupeFacts, "floorItemId", floorItemId);

        return Create(
            LegacyParityGapCategory.Magic,
            "lay-trap-floor-effect-runtime",
            reason ?? "lay-trap floor-effect runtime is not modeled; source dispatches MagicSort.LayTrap to LayTrap.Execute and MsgDataPacket checks Player.FloorSpells; next seam lay-trap-floor-effect-runtime",
            severity,
            facts,
            dedupeFacts);
    }

    public static LegacyParityGapDiagnostic SobNpcSideEffects(
        uint spellId,
        int? sort = null,
        int? mapId = null,
        int? sobNpcKind = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var facts = new[]
        {
            Fact("kind", sobNpcKind),
            Fact("map", mapId),
            Fact("opcode", WorldPacketOpcodes.MsgAttack),
            Fact("sort", sort),
            Fact("spellId", spellId),
        };
        var dedupeFacts = new[]
        {
            Fact("kind", sobNpcKind),
            Fact("map", mapId),
            Fact("sort", sort),
            Fact("spellId", spellId),
        };

        return Create(
            LegacyParityGapCategory.Magic,
            "sob-side-effects",
            reason ?? "SOB damage is modeled but event side effects are not modeled",
            severity,
            facts,
            dedupeFacts);
    }

    public static LegacyParityGapDiagnostic DirectSobNpcSideEffects(
        MsgAttackType attackType,
        int? mapId = null,
        int? sobNpcKind = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var facts = new[]
        {
            Fact("attackType", attackType.ToString()),
            Fact("kind", sobNpcKind),
            Fact("map", mapId),
            Fact("opcode", WorldPacketOpcodes.MsgAttack),
            Fact("targetKind", "sob-npc"),
        };
        var dedupeFacts = new[]
        {
            Fact("attackType", attackType.ToString()),
            Fact("kind", sobNpcKind),
            Fact("map", mapId),
            Fact("targetKind", "sob-npc"),
        };

        return Create(
            LegacyParityGapCategory.Monster,
            "direct-sob-side-effects",
            reason ?? "Direct SOB damage is modeled but full event side effects are not modeled",
            severity,
            facts,
            dedupeFacts);
    }

    public static LegacyParityGapDiagnostic MonsterDrop(
        uint monsterId,
        uint? itemId = null,
        uint? mapId = null,
        string? dropKind = null,
        string? resource = null,
        string? source = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning,
        IEnumerable<KeyValuePair<string, object?>>? extraFacts = null)
    {
        var facts = new List<KeyValuePair<string, object?>>
        {
            Fact("itemId", itemId),
            Fact("map", mapId),
            Fact("monsterId", monsterId),
        };
        AddOptionalFact(facts, "dropKind", dropKind);
        AddOptionalFact(facts, "resource", resource);
        AddOptionalFact(facts, "source", source);
        if (extraFacts is not null)
        {
            facts.AddRange(extraFacts);
        }

        var dedupeFacts = new List<KeyValuePair<string, object?>>
        {
            Fact("itemId", itemId),
            Fact("monsterId", monsterId),
        };
        AddOptionalFact(dedupeFacts, "dropKind", dropKind);

        return Create(
            LegacyParityGapCategory.Monster,
            "monster-drop",
            reason ?? "monster drop item is not loaded",
            severity,
            facts,
            dedupeFacts);
    }

    public static LegacyParityGapDiagnostic RuntimeStateNotLoaded(
        string resource,
        string surface,
        string? reason = null,
        IEnumerable<KeyValuePair<string, object?>>? facts = null,
        IEnumerable<KeyValuePair<string, object?>>? dedupeFacts = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var allFacts = new List<KeyValuePair<string, object?>>
        {
            Fact("resource", resource),
            Fact("surface", surface),
        };
        if (facts is not null)
        {
            allFacts.AddRange(facts);
        }

        var allDedupeFacts = new List<KeyValuePair<string, object?>>
        {
            Fact("resource", resource),
            Fact("surface", surface),
        };
        if (dedupeFacts is not null)
        {
            allDedupeFacts.AddRange(dedupeFacts);
        }

        return Create(
            LegacyParityGapCategory.Runtime,
            "runtime-state-not-loaded",
            reason ?? $"{resource} runtime state is not loaded",
            severity,
            allFacts,
            allDedupeFacts);
    }

    public static LegacyParityGapDiagnostic MovementAction(
        string action,
        ushort opcode = WorldPacketOpcodes.MsgAction,
        ushort? actionType = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var facts = new[]
        {
            Fact("action", action),
            Fact("actionType", actionType),
            Fact("opcode", opcode),
        };

        return Create(
            LegacyParityGapCategory.Action,
            "movement-action",
            reason ?? "movement action is not modeled",
            severity,
            facts,
            facts);
    }

    public static LegacyParityGapDiagnostic RegisteredMsgAction(
        string action,
        ushort actionType,
        string legacyHandler,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var facts = new[]
        {
            Fact("action", action),
            Fact("actionType", actionType),
            Fact("legacyHandler", legacyHandler),
            Fact("opcode", WorldPacketOpcodes.MsgAction),
        };
        var dedupeFacts = new[]
        {
            Fact("actionType", actionType),
            Fact("legacyHandler", legacyHandler),
            Fact("opcode", WorldPacketOpcodes.MsgAction),
        };

        return Create(
            LegacyParityGapCategory.Action,
            "registered-msgaction",
            reason ?? "registered legacy MsgAction handler is not modeled",
            severity,
            facts,
            dedupeFacts);
    }

    public static LegacyParityGapDiagnostic UnhandledGameplayClickAction(
        ushort opcode,
        uint? actionType = null,
        uint? actionSubtype = null,
        uint? mapId = null,
        uint? x = null,
        uint? y = null,
        uint? targetUid = null,
        string? sourceSeam = null,
        string? nextSeam = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var facts = new List<KeyValuePair<string, object?>>
        {
            Fact("opcode", opcode),
        };
        AddOptionalFact(facts, "actionSubtype", actionSubtype);
        AddOptionalFact(facts, "actionType", actionType);
        AddOptionalFact(facts, "map", mapId);
        AddOptionalFact(facts, "nextSeam", nextSeam);
        AddOptionalFact(facts, "sourceSeam", sourceSeam);
        AddOptionalFact(facts, "targetUid", targetUid);
        AddOptionalFact(facts, "x", x);
        AddOptionalFact(facts, "y", y);

        var dedupeFacts = new List<KeyValuePair<string, object?>>
        {
            Fact("opcode", opcode),
        };
        AddOptionalFact(dedupeFacts, "actionSubtype", actionSubtype);
        AddOptionalFact(dedupeFacts, "actionType", actionType);
        AddOptionalFact(dedupeFacts, "nextSeam", nextSeam);
        AddOptionalFact(dedupeFacts, "sourceSeam", sourceSeam);

        return Create(
            LegacyParityGapCategory.Action,
            "unhandled-gameplay-click-action",
            reason ?? "unhandled gameplay click/action is not modeled",
            severity,
            facts,
            dedupeFacts);
    }

    public static LegacyParityGapDiagnostic NotLoaded(
        LegacyParityGapCategory category,
        string gap,
        string resource,
        IEnumerable<KeyValuePair<string, object?>>? facts = null,
        IEnumerable<KeyValuePair<string, object?>>? dedupeFacts = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning)
    {
        var allFacts = new List<KeyValuePair<string, object?>>
        {
            Fact("resource", resource),
        };
        if (facts is not null)
        {
            allFacts.AddRange(facts);
        }

        return Create(
            category,
            gap,
            $"{resource} is not loaded",
            severity,
            allFacts,
            dedupeFacts ?? allFacts);
    }

    public static LegacyParityGapDiagnostic RuntimeFreeze(
        string surface,
        string reason,
        IEnumerable<KeyValuePair<string, object?>>? facts = null)
    {
        var allFacts = new List<KeyValuePair<string, object?>>
        {
            Fact("surface", surface),
        };
        if (facts is not null)
        {
            allFacts.AddRange(facts);
        }

        return Create(
            LegacyParityGapCategory.Runtime,
            "freeze",
            $"freeze {reason}",
            LegacyParityGapSeverity.Error,
            allFacts,
            allFacts);
    }

    public static LegacyParityGapDiagnostic RuntimeException(
        string surface,
        ushort? opcode = null,
        int? payloadLength = null,
        string? exceptionType = null,
        string? sourceSeam = null,
        string? nextSeam = null,
        string? reason = null,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Error)
    {
        var facts = new List<KeyValuePair<string, object?>>
        {
            Fact("surface", surface),
        };
        AddOptionalFact(facts, "exceptionType", exceptionType);
        AddOptionalFact(facts, "nextSeam", nextSeam);
        AddOptionalFact(facts, "opcode", opcode);
        AddOptionalFact(facts, "payloadLength", payloadLength);
        AddOptionalFact(facts, "sourceSeam", sourceSeam);

        var dedupeFacts = new List<KeyValuePair<string, object?>>
        {
            Fact("surface", surface),
        };
        AddOptionalFact(dedupeFacts, "exceptionType", exceptionType);
        AddOptionalFact(dedupeFacts, "opcode", opcode);
        AddOptionalFact(dedupeFacts, "sourceSeam", sourceSeam);

        return Create(
            LegacyParityGapCategory.Runtime,
            "runtime-exception",
            reason ?? "runtime exception was caught and logged for parity triage",
            severity,
            facts,
            dedupeFacts);
    }

    public static LegacyParityGapDiagnostic Create(
        LegacyParityGapCategory category,
        string gap,
        string? reason,
        LegacyParityGapSeverity severity = LegacyParityGapSeverity.Warning,
        IEnumerable<KeyValuePair<string, object?>>? facts = null,
        IEnumerable<KeyValuePair<string, object?>>? dedupeFacts = null)
    {
        var categoryToken = ToCategoryToken(category);
        var severityToken = ToSeverityToken(severity);
        var gapToken = NormalizeToken(gap);
        var safeFacts = NormalizeFacts(facts).ToArray();
        var safeDedupeFacts = NormalizeFacts(dedupeFacts ?? facts).ToArray();

        var dedupeKey = FormatDedupeKey(categoryToken, gapToken, safeDedupeFacts);
        var message = FormatMessage(categoryToken, severityToken, gapToken, dedupeKey, safeFacts, reason);
        return new LegacyParityGapDiagnostic(category, severity, gapToken, dedupeKey, message);
    }

    public static KeyValuePair<string, object?> Fact(string name, object? value) => new(name, value);

    private static void AddOptionalFact(List<KeyValuePair<string, object?>> facts, string name, object? value)
    {
        if (value is not null)
        {
            facts.Add(Fact(name, value));
        }
    }

    public static string ToCategoryToken(LegacyParityGapCategory category) =>
        category switch
        {
            LegacyParityGapCategory.UnsupportedPacket => "unsupported-packet",
            LegacyParityGapCategory.Ui => "ui",
            LegacyParityGapCategory.Npc => "npc",
            LegacyParityGapCategory.Item => "item",
            LegacyParityGapCategory.Magic => "magic",
            LegacyParityGapCategory.Monster => "monster",
            LegacyParityGapCategory.Action => "action",
            LegacyParityGapCategory.Runtime => "runtime",
            _ => NormalizeToken(category.ToString()),
        };

    public static string ToSeverityToken(LegacyParityGapSeverity severity) =>
        severity switch
        {
            LegacyParityGapSeverity.Info => "info",
            LegacyParityGapSeverity.Warning => "warning",
            LegacyParityGapSeverity.Error => "error",
            _ => NormalizeToken(severity.ToString()),
        };

    private static string FormatDedupeKey(
        string categoryToken,
        string gapToken,
        IReadOnlyList<KeyValuePair<string, string>> facts)
    {
        var builder = new StringBuilder();
        builder.Append(categoryToken);
        builder.Append(':');
        builder.Append(gapToken);

        foreach (var fact in facts)
        {
            builder.Append(':');
            builder.Append(fact.Key);
            builder.Append('=');
            builder.Append(fact.Value);
        }

        return builder.ToString();
    }

    private static string FormatMessage(
        string categoryToken,
        string severityToken,
        string gapToken,
        string dedupeKey,
        IReadOnlyList<KeyValuePair<string, string>> facts,
        string? reason)
    {
        var builder = new StringBuilder();
        builder.Append("PARITY-GAP category=");
        builder.Append(categoryToken);
        builder.Append(" severity=");
        builder.Append(severityToken);
        builder.Append(" gap=");
        builder.Append(gapToken);
        builder.Append(" dedupe=");
        builder.Append(dedupeKey);

        foreach (var fact in facts)
        {
            builder.Append(' ');
            builder.Append(fact.Key);
            builder.Append('=');
            builder.Append(fact.Value);
        }

        builder.Append("; ");
        builder.Append(SanitizeReason(reason));
        return builder.ToString();
    }

    private static IEnumerable<KeyValuePair<string, string>> NormalizeFacts(
        IEnumerable<KeyValuePair<string, object?>>? facts)
    {
        if (facts is null)
        {
            yield break;
        }

        var normalized = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var fact in facts)
        {
            var key = NormalizeFactKey(fact.Key);
            if (key.Length == 0 || IsSensitiveKey(key))
            {
                continue;
            }

            var value = NormalizeValue(fact.Value);
            if (!normalized.TryGetValue(key, out var values))
            {
                values = new SortedSet<string>(StringComparer.Ordinal);
                normalized[key] = values;
            }

            values.Add(value);
        }

        foreach (var pair in normalized)
        {
            foreach (var value in pair.Value)
            {
                yield return new KeyValuePair<string, string>(pair.Key, value);
            }
        }
    }

    private static string NormalizeFactKey(string key)
    {
        key = key.Trim();
        if (key.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(key.Length);
        foreach (var character in key)
        {
            if (char.IsLetterOrDigit(character) || character is '_' or '-')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static bool IsSensitiveKey(string key)
    {
        foreach (var fragment in SensitiveKeyFragments)
        {
            if (key.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return string.Equals(key, "ip", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "addr", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "address", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeValue(object? value)
    {
        return value switch
        {
            null => "none",
            bool boolean => boolean ? "1" : "0",
            byte number => number.ToString(CultureInfo.InvariantCulture),
            sbyte number => number.ToString(CultureInfo.InvariantCulture),
            short number => number.ToString(CultureInfo.InvariantCulture),
            ushort number => number.ToString(CultureInfo.InvariantCulture),
            int number => number.ToString(CultureInfo.InvariantCulture),
            uint number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            ulong number => number.ToString(CultureInfo.InvariantCulture),
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            Enum enumValue => NormalizeToken(enumValue.ToString()),
            IFormattable formattable => NormalizeToken(formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty),
            _ => NormalizeToken(value.ToString() ?? string.Empty),
        };
    }

    private static string NormalizeToken(string value)
    {
        value = CollapseWhitespace(value.Trim());
        if (value.Length == 0)
        {
            return "unknown";
        }

        var builder = new StringBuilder(value.Length);
        var previousWasDash = false;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) || character is '_' or '.' or ':')
            {
                builder.Append(character);
                previousWasDash = false;
            }
            else
            {
                if (!previousWasDash)
                {
                    builder.Append('-');
                    previousWasDash = true;
                }
            }
        }

        var token = builder.ToString().Trim('-');
        if (token.Length == 0)
        {
            return "unknown";
        }

        return token.Length <= MaxTokenLength ? token : token[..MaxTokenLength];
    }

    private static string SanitizeReason(string? reason)
    {
        reason = CollapseWhitespace((reason ?? string.Empty).Trim());
        if (reason.Length == 0)
        {
            return "gap is not modeled";
        }

        foreach (var fragment in SensitiveKeyFragments)
        {
            reason = Regex.Replace(
                reason,
                $@"\b[\w.-]*{Regex.Escape(fragment)}[\w.-]*=\S+",
                match => $"{match.Value[..match.Value.IndexOf('=', StringComparison.Ordinal)]}=#",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return reason.Length <= MaxReasonLength ? reason : reason[..MaxReasonLength].TrimEnd() + "...";
    }

    private static string CollapseWhitespace(string value) =>
        Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant);
}
