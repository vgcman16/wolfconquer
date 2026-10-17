using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyParityGapDiagnosticsTests
{
    [Fact]
    public void ClickedNpcDiagnosticUsesStableNpcCategoryAndUnhandledClickMarker()
    {
        var diagnostic = LegacyParityGapDiagnostics.ClickedNpc(10010, mapId: 1002);

        Assert.Equal(LegacyParityGapCategory.Npc, diagnostic.Category);
        Assert.Equal(LegacyParityGapSeverity.Warning, diagnostic.Severity);
        Assert.Equal("clicked-npc", diagnostic.Gap);
        Assert.Equal("npc:clicked-npc:npcId=10010:opcode=2255", diagnostic.DedupeKey);
        Assert.StartsWith(
            "PARITY-GAP category=npc severity=warning gap=clicked-npc dedupe=npc:clicked-npc:npcId=10010:opcode=2255",
            diagnostic.Message);
        Assert.Contains("map=1002", diagnostic.Message);
        Assert.Contains("unhandled-click", diagnostic.Message);
        Assert.Equal(diagnostic.Message, diagnostic.FormatLogMessage());
    }

    [Fact]
    public void ClickedNpcDiagnosticCarriesSourceFactsWithoutWideningDedupe()
    {
        var diagnostic = LegacyParityGapDiagnostics.ClickedNpc(
            15_641,
            opcode: WorldPacketOpcodes.LegacyNpcServer,
            mapId: 1002,
            optionId: 4,
            interactType: 1,
            action: "Script",
            source: "NpcHandler.Teleportall");

        Assert.Equal("npc:clicked-npc:npcId=15641:opcode=2084", diagnostic.DedupeKey);
        Assert.Contains("action=Script", diagnostic.Message);
        Assert.Contains("interactType=1", diagnostic.Message);
        Assert.Contains("optionId=4", diagnostic.Message);
        Assert.Contains("source=NpcHandler.Teleportall", diagnostic.Message);
    }

    [Fact]
    public void MissingNpcScriptDiagnosticUsesRuntimeResolutionFacts()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                20_508,
                optionId: 0,
                LegacyNpcReplyInteractType.Dialog));

        var diagnostic = LegacyParityGapDiagnostics.MissingNpcScript(resolution, mapId: 1002);

        Assert.Equal(LegacyParityGapCategory.Npc, diagnostic.Category);
        Assert.Equal(LegacyParityGapSeverity.Warning, diagnostic.Severity);
        Assert.Equal("missing-npc-script", diagnostic.Gap);
        Assert.Equal(
            "npc:missing-npc-script:action=Script:interactType=1:nextSeam=model-npc-script-definition:npcId=20508:opcode=2084:optionId=0:unsupportedReason=MissingRuntimeDefinition",
            diagnostic.DedupeKey);
        Assert.StartsWith(
            "PARITY-GAP category=npc severity=warning gap=missing-npc-script dedupe=npc:missing-npc-script",
            diagnostic.Message);
        Assert.Contains("actionCategory=KnownSourceScript", diagnostic.Message);
        Assert.Contains("map=1002", diagnostic.Message);
        Assert.Contains("nextSeam=model-npc-script-definition", diagnostic.Message);
        Assert.Contains("npcEnum=DragonIslandConductress", diagnostic.Message);
        Assert.Contains("source=NpcHandler.DragonIslandConductress", diagnostic.Message);
        Assert.Contains("unsupportedReason=MissingRuntimeDefinition", diagnostic.Message);
        Assert.Contains("missing NPC script runtime path is not modeled", diagnostic.Message);
    }

    [Fact]
    public void MissingNpcScriptDiagnosticScrubsReasonAndKeepsSourceOutOfDedupe()
    {
        var first = LegacyParityGapDiagnostics.MissingNpcScript(
            999_001,
            opcode: WorldPacketOpcodes.LegacyNpcReply,
            optionId: 5,
            interactType: (byte)LegacyNpcReplyInteractType.Option,
            action: LegacyNpcServerAction.Script,
            unsupportedReason: LegacyNpcScriptRuntimeUnsupportedReason.MissingSourceScript,
            actionCategory: LegacyNpcScriptRuntimeActionCategory.UnknownSourceScript,
            mapId: 1002,
            npcEnumName: "First Sweep NPC",
            source: "NpcHandler.FirstSweep",
            nextSeam: "source-triage",
            reason: "missing NPC script is not modeled player=111 session=alpha account=secret");
        var second = LegacyParityGapDiagnostics.MissingNpcScript(
            999_001,
            opcode: WorldPacketOpcodes.LegacyNpcReply,
            optionId: 5,
            interactType: (byte)LegacyNpcReplyInteractType.Option,
            action: LegacyNpcServerAction.Script,
            unsupportedReason: LegacyNpcScriptRuntimeUnsupportedReason.MissingSourceScript,
            actionCategory: LegacyNpcScriptRuntimeActionCategory.UnknownSourceScript,
            mapId: 1036,
            npcEnumName: "Second Sweep NPC",
            source: "NpcHandler.SecondSweep",
            nextSeam: "source-triage",
            reason: "missing NPC script is not modeled player=222 session=beta account=other");

        Assert.Equal(
            "npc:missing-npc-script:action=Script:interactType=2:nextSeam=source-triage:npcId=999001:opcode=2255:optionId=5:unsupportedReason=MissingSourceScript",
            first.DedupeKey);
        Assert.Equal(first.DedupeKey, second.DedupeKey);
        Assert.Contains("map=1002", first.Message);
        Assert.Contains("source=NpcHandler.FirstSweep", first.Message);
        Assert.Contains("npcEnum=First-Sweep-NPC", first.Message);
        Assert.Contains("player=#", first.Message);
        Assert.Contains("session=#", first.Message);
        Assert.Contains("account=#", first.Message);
        Assert.DoesNotContain("player=111", first.Message);
        Assert.DoesNotContain("alpha", first.Message);
        Assert.DoesNotContain("secret", first.Message);
    }

    [Fact]
    public void UnsupportedMagicDiagnosticScrubsSensitiveFactsAndKeepsDedupeStable()
    {
        var first = LegacyParityGapDiagnostics.Create(
            LegacyParityGapCategory.Magic,
            "unsupported magic",
            "unsupported magic is not modeled accountId=123 session=alpha",
            facts:
            [
                LegacyParityGapDiagnostics.Fact("spellId", 6010U),
                LegacyParityGapDiagnostics.Fact("sort", 18),
                LegacyParityGapDiagnostics.Fact("accountId", 123U),
                LegacyParityGapDiagnostics.Fact("session", "alpha"),
            ],
            dedupeFacts:
            [
                LegacyParityGapDiagnostics.Fact("accountId", 999U),
                LegacyParityGapDiagnostics.Fact("sort", 18),
                LegacyParityGapDiagnostics.Fact("spellId", 6010U),
            ]);
        var second = LegacyParityGapDiagnostics.Create(
            LegacyParityGapCategory.Magic,
            "unsupported magic",
            "unsupported magic is not modeled accountId=456 session=beta",
            facts:
            [
                LegacyParityGapDiagnostics.Fact("session", "beta"),
                LegacyParityGapDiagnostics.Fact("sort", 18),
                LegacyParityGapDiagnostics.Fact("spellId", 6010U),
                LegacyParityGapDiagnostics.Fact("accountId", 456U),
            ],
            dedupeFacts:
            [
                LegacyParityGapDiagnostics.Fact("spellId", 6010U),
                LegacyParityGapDiagnostics.Fact("sort", 18),
                LegacyParityGapDiagnostics.Fact("accountId", 456U),
            ]);

        Assert.Equal(LegacyParityGapCategory.Magic, first.Category);
        Assert.Equal("unsupported-magic", first.Gap);
        Assert.Equal("magic:unsupported-magic:sort=18:spellId=6010", first.DedupeKey);
        Assert.Equal(first.DedupeKey, second.DedupeKey);
        Assert.Contains("category=magic", first.Message);
        Assert.Contains("sort=18", first.Message);
        Assert.Contains("spellId=6010", first.Message);
        Assert.DoesNotContain("accountId=123", first.Message);
        Assert.DoesNotContain("alpha", first.Message);
        Assert.Contains("accountId=#", first.Message);
        Assert.Contains("session=#", first.Message);
    }

    [Fact]
    public void UnsupportedItemUseDiagnosticUsesItemUseCategoryAndOpcode()
    {
        var diagnostic = LegacyParityGapDiagnostics.UnsupportedItemUse(
            720027,
            action: 3,
            actionName: "Equip",
            itemUid: 123,
            source: "MsgItemUsage.SkillBook");

        Assert.Equal(LegacyParityGapCategory.Item, diagnostic.Category);
        Assert.Equal("item-use", diagnostic.Gap);
        Assert.Equal("item:item-use:action=3:itemId=720027:opcode=2137", diagnostic.DedupeKey);
        Assert.Contains("category=item", diagnostic.Message);
        Assert.Contains("action=3", diagnostic.Message);
        Assert.Contains("actionName=Equip", diagnostic.Message);
        Assert.Contains("itemId=720027", diagnostic.Message);
        Assert.Contains("itemUid=123", diagnostic.Message);
        Assert.Contains("source=MsgItemUsage.SkillBook", diagnostic.Message);
        Assert.Contains("not modeled", diagnostic.Message);
    }

    [Fact]
    public void UnsupportedMagicDiagnosticAddsSkillSearchFactsButKeepsDedupeNarrow()
    {
        var diagnostic = LegacyParityGapDiagnostics.UnsupportedMagic(
            11_110,
            sort: 61,
            skillName: "MoveLine",
            targetKind: "monster",
            resource: "MonsterRuntimeStateStore",
            source: "AttackHandler.MoveLine");

        Assert.Equal(LegacyParityGapCategory.Magic, diagnostic.Category);
        Assert.Equal("unsupported-magic", diagnostic.Gap);
        Assert.Equal("magic:unsupported-magic:opcode=2135:sort=61:spellId=11110", diagnostic.DedupeKey);
        Assert.Contains("skillId=11110", diagnostic.Message);
        Assert.Contains("skillName=MoveLine", diagnostic.Message);
        Assert.Contains("targetKind=monster", diagnostic.Message);
        Assert.Contains("resource=MonsterRuntimeStateStore", diagnostic.Message);
        Assert.Contains("source=AttackHandler.MoveLine", diagnostic.Message);
    }

    [Fact]
    public void UnsupportedSkillDiagnosticUsesSkillGapToken()
    {
        var diagnostic = LegacyParityGapDiagnostics.UnsupportedSkill(
            11_110,
            sort: 61,
            skillName: "MoveLine");

        Assert.Equal(LegacyParityGapCategory.Magic, diagnostic.Category);
        Assert.Equal("unsupported-skill", diagnostic.Gap);
        Assert.Equal("magic:unsupported-skill:opcode=2135:skillId=11110:sort=61", diagnostic.DedupeKey);
        Assert.Contains("skillId=11110", diagnostic.Message);
        Assert.Contains("skillName=MoveLine", diagnostic.Message);
    }

    [Fact]
    public void LayTrapFloorEffectRuntimeDiagnosticIncludesSourceEvidenceAndNextSeam()
    {
        var diagnostic = LegacyParityGapDiagnostics.LayTrapFloorEffectRuntime(
            14_720,
            floorItemId: 1080,
            mapId: 1002,
            spellName: "Bloom of Death",
            targetKind: "floor effect",
            trigger: "client-click-sweep");

        Assert.Equal(LegacyParityGapCategory.Magic, diagnostic.Category);
        Assert.Equal(LegacyParityGapSeverity.Warning, diagnostic.Severity);
        Assert.Equal("lay-trap-floor-effect-runtime", diagnostic.Gap);
        Assert.Equal(
            "magic:lay-trap-floor-effect-runtime:floorItemId=1080:nextSeam=lay-trap-floor-effect-runtime:opcode=2135:sort=21:spellId=14720",
            diagnostic.DedupeKey);
        Assert.StartsWith(
            "PARITY-GAP category=magic severity=warning gap=lay-trap-floor-effect-runtime dedupe=magic:lay-trap-floor-effect-runtime",
            diagnostic.Message);
        Assert.Contains("floorItemId=1080", diagnostic.Message);
        Assert.Contains("map=1002", diagnostic.Message);
        Assert.Contains("nextSeam=lay-trap-floor-effect-runtime", diagnostic.Message);
        Assert.Contains("source=MsgAttackPacket.MagicSort.LayTrap", diagnostic.Message);
        Assert.Contains(
            "sourceEvidence=MsgAttackPacket.LayTrap.Execute-MsgDataPacket.Player.FloorSpells",
            diagnostic.Message);
        Assert.Contains("spellName=Bloom-of-Death", diagnostic.Message);
        Assert.Contains("targetKind=floor-effect", diagnostic.Message);
        Assert.Contains("trigger=client-click-sweep", diagnostic.Message);
        Assert.Contains("LayTrap.Execute", diagnostic.Message);
        Assert.Contains("Player.FloorSpells", diagnostic.Message);
        Assert.Equal(diagnostic.Message, diagnostic.FormatLogMessage());
    }

    [Fact]
    public void LayTrapFloorEffectRuntimeDiagnosticKeepsSweepContextOutOfDedupe()
    {
        var first = LegacyParityGapDiagnostics.LayTrapFloorEffectRuntime(
            14_250,
            floorItemId: 1080,
            mapId: 1002,
            source: "MsgAttackPacket.FirstSweep",
            sourceEvidence: "MsgAttackPacket.LayTrap.Execute",
            spellName: "TideTrap",
            targetKind: "visible monster",
            trigger: "direct-cast");
        var second = LegacyParityGapDiagnostics.LayTrapFloorEffectRuntime(
            14_250,
            floorItemId: 1080,
            mapId: 1036,
            source: "MsgAttackPacket.SecondSweep",
            sourceEvidence: "MsgDataPacket.Player.FloorSpells",
            spellName: "Tide Trap",
            targetKind: "visible player",
            trigger: "client-click");

        Assert.Equal(
            "magic:lay-trap-floor-effect-runtime:floorItemId=1080:nextSeam=lay-trap-floor-effect-runtime:opcode=2135:sort=21:spellId=14250",
            first.DedupeKey);
        Assert.Equal(first.DedupeKey, second.DedupeKey);
        Assert.DoesNotContain("map=", first.DedupeKey);
        Assert.DoesNotContain("source=", first.DedupeKey);
        Assert.DoesNotContain("sourceEvidence=", first.DedupeKey);
        Assert.DoesNotContain("trigger=", first.DedupeKey);
    }

    [Fact]
    public void LayTrapFloorEffectRuntimeDiagnosticScrubsSensitiveReasonValues()
    {
        var diagnostic = LegacyParityGapDiagnostics.LayTrapFloorEffectRuntime(
            14_720,
            floorItemId: 1080,
            reason: "lay trap click froze player=111 session=alpha account=secret remote=10.0.0.1");

        Assert.Contains("player=#", diagnostic.Message);
        Assert.Contains("session=#", diagnostic.Message);
        Assert.Contains("account=#", diagnostic.Message);
        Assert.Contains("remote=#", diagnostic.Message);
        Assert.DoesNotContain("player=111", diagnostic.Message);
        Assert.DoesNotContain("alpha", diagnostic.Message);
        Assert.DoesNotContain("secret", diagnostic.Message);
        Assert.DoesNotContain("10.0.0.1", diagnostic.Message);
    }

    [Fact]
    public void MonsterDropDiagnosticUsesMonsterCategoryAndKeepsMapOutOfDedupe()
    {
        var diagnostic = LegacyParityGapDiagnostics.MonsterDrop(
            17,
            itemId: 1088000,
            mapId: 1002,
            dropKind: "item",
            resource: "WorldDataCatalog",
            source: "MonsterRole.Drop");

        Assert.Equal(LegacyParityGapCategory.Monster, diagnostic.Category);
        Assert.Equal("monster-drop", diagnostic.Gap);
        Assert.Equal("monster:monster-drop:dropKind=item:itemId=1088000:monsterId=17", diagnostic.DedupeKey);
        Assert.Contains("category=monster", diagnostic.Message);
        Assert.Contains("dropKind=item", diagnostic.Message);
        Assert.Contains("itemId=1088000", diagnostic.Message);
        Assert.Contains("map=1002", diagnostic.Message);
        Assert.Contains("monsterId=17", diagnostic.Message);
        Assert.Contains("resource=WorldDataCatalog", diagnostic.Message);
        Assert.Contains("source=MonsterRole.Drop", diagnostic.Message);
        Assert.Contains("not loaded", diagnostic.Message);
    }

    [Fact]
    public void RuntimeStateNotLoadedDiagnosticUsesRuntimeCategoryAndResourceDedupe()
    {
        var diagnostic = LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
            "MonsterRuntimeStateStore",
            "physical-spells-monster-magic",
            facts:
            [
                LegacyParityGapDiagnostics.Fact("spellId", 7010),
                LegacyParityGapDiagnostics.Fact("sort", 55),
                LegacyParityGapDiagnostics.Fact("targetKind", "monster"),
            ],
            dedupeFacts:
            [
                LegacyParityGapDiagnostics.Fact("spellId", 7010),
                LegacyParityGapDiagnostics.Fact("sort", 55),
                LegacyParityGapDiagnostics.Fact("targetKind", "monster"),
            ]);

        Assert.Equal(LegacyParityGapCategory.Runtime, diagnostic.Category);
        Assert.Equal("runtime-state-not-loaded", diagnostic.Gap);
        Assert.Equal(
            "runtime:runtime-state-not-loaded:resource=MonsterRuntimeStateStore:sort=55:spellId=7010:surface=physical-spells-monster-magic:targetKind=monster",
            diagnostic.DedupeKey);
        Assert.Contains("category=runtime", diagnostic.Message);
        Assert.Contains("resource=MonsterRuntimeStateStore", diagnostic.Message);
        Assert.Contains("surface=physical-spells-monster-magic", diagnostic.Message);
        Assert.Contains("targetKind=monster", diagnostic.Message);
    }

    [Fact]
    public void MovementActionDiagnosticUsesActionCategoryAndStableActionType()
    {
        var diagnostic = LegacyParityGapDiagnostics.MovementAction("Jump", actionType: 137);

        Assert.Equal(LegacyParityGapCategory.Action, diagnostic.Category);
        Assert.Equal("movement-action", diagnostic.Gap);
        Assert.Equal("action:movement-action:action=Jump:actionType=137:opcode=2046", diagnostic.DedupeKey);
        Assert.Contains("category=action", diagnostic.Message);
        Assert.Contains("action=Jump", diagnostic.Message);
        Assert.Contains("actionType=137", diagnostic.Message);
        Assert.Contains("not modeled", diagnostic.Message);
    }

    [Fact]
    public void RegisteredMsgActionDiagnosticUsesActionCategoryAndLegacyHandler()
    {
        var diagnostic = LegacyParityGapDiagnostics.RegisteredMsgAction(
            "StartVendor",
            111,
            "MsgDataPacket.StartVendor [Data(111)]");

        Assert.Equal(LegacyParityGapCategory.Action, diagnostic.Category);
        Assert.Equal("registered-msgaction", diagnostic.Gap);
        Assert.Equal(
            "action:registered-msgaction:actionType=111:legacyHandler=MsgDataPacket.StartVendor-Data-111:opcode=2046",
            diagnostic.DedupeKey);
        Assert.Contains("category=action", diagnostic.Message);
        Assert.Contains("action=StartVendor", diagnostic.Message);
        Assert.Contains("actionType=111", diagnostic.Message);
        Assert.Contains("legacyHandler=MsgDataPacket.StartVendor-Data-111", diagnostic.Message);
        Assert.Contains("registered legacy MsgAction handler is not modeled", diagnostic.Message);
    }

    [Fact]
    public void UnhandledGameplayClickActionDiagnosticRecordsClickAndSeamContext()
    {
        var diagnostic = LegacyParityGapDiagnostics.UnhandledGameplayClickAction(
            WorldPacketOpcodes.MsgAction,
            actionType: (uint)MsgActionType.OpenGuiNpc,
            actionSubtype: 2,
            mapId: 1002,
            x: 431,
            y: 378,
            targetUid: 500_012,
            sourceSeam: "WorldPacketHandlers.MsgAction.OpenGuiNpc",
            nextSeam: "npc-dialog-runtime");

        Assert.Equal(LegacyParityGapCategory.Action, diagnostic.Category);
        Assert.Equal(LegacyParityGapSeverity.Warning, diagnostic.Severity);
        Assert.Equal("unhandled-gameplay-click-action", diagnostic.Gap);
        Assert.Equal(
            "action:unhandled-gameplay-click-action:actionSubtype=2:actionType=160:nextSeam=npc-dialog-runtime:opcode=2046:sourceSeam=WorldPacketHandlers.MsgAction.OpenGuiNpc",
            diagnostic.DedupeKey);
        Assert.StartsWith(
            "PARITY-GAP category=action severity=warning gap=unhandled-gameplay-click-action dedupe=action:unhandled-gameplay-click-action",
            diagnostic.Message);
        Assert.Contains("actionSubtype=2", diagnostic.Message);
        Assert.Contains("actionType=160", diagnostic.Message);
        Assert.Contains("map=1002", diagnostic.Message);
        Assert.Contains("nextSeam=npc-dialog-runtime", diagnostic.Message);
        Assert.Contains("opcode=2046", diagnostic.Message);
        Assert.Contains("sourceSeam=WorldPacketHandlers.MsgAction.OpenGuiNpc", diagnostic.Message);
        Assert.Contains("targetUid=500012", diagnostic.Message);
        Assert.Contains("x=431", diagnostic.Message);
        Assert.Contains("y=378", diagnostic.Message);
        Assert.Contains("unhandled gameplay click/action is not modeled", diagnostic.Message);
        Assert.Equal(diagnostic.Message, diagnostic.FormatLogMessage());
    }

    [Fact]
    public void UnhandledGameplayClickActionDiagnosticKeepsDynamicTargetContextOutOfDedupe()
    {
        var first = LegacyParityGapDiagnostics.UnhandledGameplayClickAction(
            WorldPacketOpcodes.MsgAction,
            actionType: (uint)MsgActionType.OpenGuiNpc,
            actionSubtype: 2,
            mapId: 1002,
            x: 431,
            y: 378,
            targetUid: 500_012,
            sourceSeam: "WorldPacketHandlers.MsgAction.OpenGuiNpc",
            nextSeam: "npc-dialog-runtime");
        var second = LegacyParityGapDiagnostics.UnhandledGameplayClickAction(
            WorldPacketOpcodes.MsgAction,
            actionType: (uint)MsgActionType.OpenGuiNpc,
            actionSubtype: 2,
            mapId: 1036,
            x: 109,
            y: 222,
            targetUid: 501_777,
            sourceSeam: "WorldPacketHandlers.MsgAction.OpenGuiNpc",
            nextSeam: "npc-dialog-runtime");

        Assert.Equal(first.DedupeKey, second.DedupeKey);
        Assert.DoesNotContain("map=", first.DedupeKey);
        Assert.DoesNotContain("targetUid=", first.DedupeKey);
        Assert.DoesNotContain("x=", first.DedupeKey);
        Assert.DoesNotContain("y=", first.DedupeKey);
        Assert.Contains("map=1002", first.Message);
        Assert.Contains("targetUid=500012", first.Message);
    }

    [Fact]
    public void UnhandledGameplayClickActionDiagnosticScrubsSensitiveReasonValues()
    {
        var diagnostic = LegacyParityGapDiagnostics.UnhandledGameplayClickAction(
            WorldPacketOpcodes.MsgAttack,
            actionType: 21,
            actionSubtype: 14_720,
            mapId: 1002,
            x: 430,
            y: 377,
            targetUid: 1_293_600,
            sourceSeam: "MsgAttackPacket.LayTrap",
            nextSeam: "lay-trap-floor-effect-runtime",
            reason: "unhandled floor click playerUid=1293600 accountName=alice username=alice session=alpha");

        Assert.Contains("playerUid=#", diagnostic.Message);
        Assert.Contains("accountName=#", diagnostic.Message);
        Assert.Contains("username=#", diagnostic.Message);
        Assert.Contains("session=#", diagnostic.Message);
        Assert.DoesNotContain("playerUid=1293600", diagnostic.Message);
        Assert.DoesNotContain("accountName=alice", diagnostic.Message);
        Assert.DoesNotContain("username=alice", diagnostic.Message);
        Assert.DoesNotContain("session=alpha", diagnostic.Message);
        Assert.DoesNotContain("alice", diagnostic.Message);
        Assert.DoesNotContain("alice", diagnostic.DedupeKey);
    }

    [Fact]
    public void UnhandledUiClickDiagnosticUsesStableUiClickPrefixAndFields()
    {
        var diagnostic = LegacyParityGapDiagnostics.UnhandledUiClick(
            "client click sweep/settings panel",
            opcode: WorldPacketOpcodes.MsgAction,
            controlId: 4_401);

        Assert.Equal(LegacyParityGapCategory.Ui, diagnostic.Category);
        Assert.Equal(LegacyParityGapSeverity.Warning, diagnostic.Severity);
        Assert.Equal("unhandled-ui-click", diagnostic.Gap);
        Assert.Equal(
            "ui:unhandled-ui-click:controlId=4401:opcode=2046:surface=client-click-sweep-settings-panel",
            diagnostic.DedupeKey);
        Assert.StartsWith(
            "PARITY-GAP category=ui severity=warning gap=unhandled-ui-click dedupe=ui:unhandled-ui-click",
            diagnostic.Message);
        Assert.Contains("controlId=4401", diagnostic.Message);
        Assert.Contains("opcode=2046", diagnostic.Message);
        Assert.Contains("surface=client-click-sweep-settings-panel", diagnostic.Message);
        Assert.Contains("unhandled-click UI action is not modeled", diagnostic.Message);
        Assert.Equal(diagnostic.Message, diagnostic.FormatLogMessage());
    }

    public static TheoryData<LegacyParityGapCategory, string, string, string, string, uint> ClickedNotLoadedDiagnostics()
    {
        return new TheoryData<LegacyParityGapCategory, string, string, string, string, uint>
        {
            { LegacyParityGapCategory.Ui, "ui", "clicked-ui-not-loaded", "UiActionTable", "controlId", 4_401 },
            { LegacyParityGapCategory.Npc, "npc", "clicked-npc-not-loaded", "NpcScriptDefinitions", "npcId", 15_641 },
            { LegacyParityGapCategory.Action, "action", "clicked-action-not-loaded", "MsgActionDispatchTable", "actionType", (uint)MsgActionType.OpenGuiNpc },
        };
    }

    [Theory]
    [MemberData(nameof(ClickedNotLoadedDiagnostics))]
    public void NotLoadedDiagnosticForClickedSurfacesUsesCategoryPrefixAndStableFields(
        LegacyParityGapCategory category,
        string categoryToken,
        string gap,
        string resource,
        string stableField,
        uint stableValue)
    {
        var diagnostic = LegacyParityGapDiagnostics.NotLoaded(
            category,
            gap,
            resource,
            facts:
            [
                LegacyParityGapDiagnostics.Fact(stableField, stableValue),
                LegacyParityGapDiagnostics.Fact("map", 1002),
                LegacyParityGapDiagnostics.Fact("surface", "client-click-sweep"),
            ],
            dedupeFacts:
            [
                LegacyParityGapDiagnostics.Fact(stableField, stableValue),
                LegacyParityGapDiagnostics.Fact("resource", resource),
                LegacyParityGapDiagnostics.Fact("surface", "client-click-sweep"),
            ]);
        var expectedDedupe =
            $"{categoryToken}:{gap}:{stableField}={stableValue}:resource={resource}:surface=client-click-sweep";

        Assert.Equal(category, diagnostic.Category);
        Assert.Equal(LegacyParityGapSeverity.Warning, diagnostic.Severity);
        Assert.Equal(gap, diagnostic.Gap);
        Assert.Equal(expectedDedupe, diagnostic.DedupeKey);
        Assert.StartsWith(
            $"PARITY-GAP category={categoryToken} severity=warning gap={gap} dedupe={expectedDedupe}",
            diagnostic.Message);
        Assert.Contains($"{stableField}={stableValue}", diagnostic.Message);
        Assert.Contains("map=1002", diagnostic.Message);
        Assert.Contains($"resource={resource}", diagnostic.Message);
        Assert.Contains("surface=client-click-sweep", diagnostic.Message);
        Assert.Contains($"{resource} is not loaded", diagnostic.Message);
        Assert.DoesNotContain("map=", diagnostic.DedupeKey);
    }

    [Fact]
    public void UnsupportedPacketDiagnosticUsesPacketCategory()
    {
        var diagnostic = LegacyParityGapDiagnostics.UnsupportedPacket(9999, "MsgMystery");

        Assert.Equal(LegacyParityGapCategory.UnsupportedPacket, diagnostic.Category);
        Assert.Equal("unsupported-packet", diagnostic.Gap);
        Assert.Equal("unsupported-packet:unsupported-packet:opcode=9999:packet=MsgMystery", diagnostic.DedupeKey);
        Assert.Contains("category=unsupported-packet", diagnostic.Message);
        Assert.Contains("opcode=9999", diagnostic.Message);
        Assert.Contains("packet=MsgMystery", diagnostic.Message);
        Assert.Contains("unsupported packet", diagnostic.Message);
    }

    [Fact]
    public void RuntimeExceptionDiagnosticUsesRuntimeCategoryAndStableDedupe()
    {
        var diagnostic = LegacyParityGapDiagnostics.RuntimeException(
            "world-packet-handler",
            opcode: WorldPacketOpcodes.MsgAttack,
            payloadLength: 28,
            exceptionType: "InvalidOperationException",
            sourceSeam: "WorldPacketHandlers.Registry",
            nextSeam: "inspect-runtime-exception");

        Assert.Equal(LegacyParityGapCategory.Runtime, diagnostic.Category);
        Assert.Equal(LegacyParityGapSeverity.Error, diagnostic.Severity);
        Assert.Equal("runtime-exception", diagnostic.Gap);
        Assert.Equal(
            "runtime:runtime-exception:exceptionType=InvalidOperationException:opcode=2135:sourceSeam=WorldPacketHandlers.Registry:surface=world-packet-handler",
            diagnostic.DedupeKey);
        Assert.Contains("category=runtime", diagnostic.Message);
        Assert.Contains("severity=error", diagnostic.Message);
        Assert.Contains("exceptionType=InvalidOperationException", diagnostic.Message);
        Assert.Contains("nextSeam=inspect-runtime-exception", diagnostic.Message);
        Assert.Contains("payloadLength=28", diagnostic.Message);
        Assert.Contains("sourceSeam=WorldPacketHandlers.Registry", diagnostic.Message);
    }
}
