using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace WolfConquer.Core.World;

public enum LegacyNpcScriptOptionResultKind
{
    NoOp = 0,
    Teleport = 1,
    Shop = 2,
    OpenUi = 3,
    Unsupported = 4,
    PendingMutation = 5,
}

public enum LegacyNpcScriptPendingMutationKind
{
    NobilityDonation = 1,
}

public sealed record LegacyNpcScriptPendingMutation(
    LegacyNpcScriptPendingMutationKind Kind,
    uint ConquerPoints,
    ulong Donation,
    string CurrencySource,
    string MutationTarget,
    string SourceEvidence)
{
    public static LegacyNpcScriptPendingMutation NobilityDonation(
        uint conquerPoints,
        string sourceEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);

        return new LegacyNpcScriptPendingMutation(
            LegacyNpcScriptPendingMutationKind.NobilityDonation,
            conquerPoints,
            checked((ulong)conquerPoints * 500UL),
            "client.Player.ConquerPoints",
            "client.Player.Nobility.Donation",
            sourceEvidence);
    }

    public string DescribeForLog()
    {
        return string.Concat(
            "mutationKind=",
            Kind,
            " conquerPoints=",
            ConquerPoints.ToString(CultureInfo.InvariantCulture),
            " donation=",
            Donation.ToString(CultureInfo.InvariantCulture),
            " currencySource=",
            CurrencySource,
            " mutationTarget=",
            MutationTarget,
            " sourceEvidence=",
            SourceEvidence);
    }
}

public sealed record LegacyNpcScriptOptionResult(
    LegacyNpcScriptOptionResultKind Kind,
    uint? MapId,
    uint? X,
    uint? Y,
    uint? ShopId,
    uint? UiId,
    string SourceEvidence)
{
    public LegacyNpcScriptPendingMutation? PendingMutation { get; init; }

    public static LegacyNpcScriptOptionResult NoOp(string sourceEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);

        return new LegacyNpcScriptOptionResult(
            LegacyNpcScriptOptionResultKind.NoOp,
            MapId: null,
            X: null,
            Y: null,
            ShopId: null,
            UiId: null,
            sourceEvidence);
    }

    public static LegacyNpcScriptOptionResult Teleport(uint mapId, uint x, uint y, string sourceEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);

        return new LegacyNpcScriptOptionResult(
            LegacyNpcScriptOptionResultKind.Teleport,
            mapId,
            x,
            y,
            ShopId: null,
            UiId: null,
            sourceEvidence);
    }

    public static LegacyNpcScriptOptionResult Shop(uint shopId, string sourceEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);

        return new LegacyNpcScriptOptionResult(
            LegacyNpcScriptOptionResultKind.Shop,
            MapId: null,
            X: null,
            Y: null,
            shopId,
            UiId: null,
            sourceEvidence);
    }

    public static LegacyNpcScriptOptionResult OpenUi(uint uiId, string sourceEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);

        return new LegacyNpcScriptOptionResult(
            LegacyNpcScriptOptionResultKind.OpenUi,
            MapId: null,
            X: null,
            Y: null,
            ShopId: null,
            uiId,
            sourceEvidence);
    }

    public static LegacyNpcScriptOptionResult FromPendingMutation(
        LegacyNpcScriptPendingMutation pendingMutation,
        string sourceEvidence)
    {
        ArgumentNullException.ThrowIfNull(pendingMutation);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);

        return new LegacyNpcScriptOptionResult(
            LegacyNpcScriptOptionResultKind.PendingMutation,
            MapId: null,
            X: null,
            Y: null,
            ShopId: null,
            UiId: null,
            sourceEvidence)
        {
            PendingMutation = pendingMutation,
        };
    }

    public static LegacyNpcScriptOptionResult Unsupported(string sourceEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);

        return new LegacyNpcScriptOptionResult(
            LegacyNpcScriptOptionResultKind.Unsupported,
            MapId: null,
            X: null,
            Y: null,
            ShopId: null,
            UiId: null,
            sourceEvidence);
    }

    public string DescribeForLog()
    {
        return Kind switch
        {
            LegacyNpcScriptOptionResultKind.Teleport => string.Concat(
                "kind=Teleport map=",
                MapId?.ToString(CultureInfo.InvariantCulture) ?? "none",
                " x=",
                X?.ToString(CultureInfo.InvariantCulture) ?? "none",
                " y=",
                Y?.ToString(CultureInfo.InvariantCulture) ?? "none",
                " sourceEvidence=",
                SourceEvidence),
            LegacyNpcScriptOptionResultKind.Shop => string.Concat(
                "kind=Shop shopId=",
                ShopId?.ToString(CultureInfo.InvariantCulture) ?? "none",
                " sourceEvidence=",
                SourceEvidence),
            LegacyNpcScriptOptionResultKind.OpenUi => string.Concat(
                "kind=OpenUi uiId=",
                UiId?.ToString(CultureInfo.InvariantCulture) ?? "none",
                " sourceEvidence=",
                SourceEvidence),
            LegacyNpcScriptOptionResultKind.PendingMutation => string.Concat(
                "kind=PendingMutation ",
                PendingMutation?.DescribeForLog() ?? "pendingMutation=none",
                " sourceEvidence=",
                SourceEvidence),
            LegacyNpcScriptOptionResultKind.Unsupported => string.Concat(
                "kind=Unsupported sourceEvidence=",
                SourceEvidence),
            _ => string.Concat("kind=NoOp sourceEvidence=", SourceEvidence),
        };
    }
}

public sealed record LegacyNpcScriptOptionDefinition(
    string Text,
    byte OptionId,
    LegacyNpcScriptOptionResult Result)
{
    public bool IsClosedOption => OptionId == LegacyNpcReplyPacket.ClosedOptionId;
}

public sealed record LegacyNpcScriptInputDefinition(
    string Label,
    byte OptionId,
    int MaxLength,
    string SourceEvidence);

public sealed record LegacyNpcScriptDefinition
{
    private const string VipQuestSourceEvidence =
        "NpcHandler.cs search for NPC ids 2084 and 2255 had no matches; selected NpcHandler.VipQuest because it is absent from WorldPacketHandlers and models only option 0 dialog plus option 1 teleport. Evidence: NpcID.cs:301 VipQuest = 52002u; NpcHandler.cs:26123-26136 [Npc(NpcID.VipQuest)], dialog AddText/AddOption/AddAvatar(105), client.Teleport(208, 255, 11127u).";

    private const string BackTcSourceEvidence =
        "NpcID.cs:58 BackTC = 10032u; NpcHandler.cs:56955-56968 [Npc(NpcID.BackTC)] option 0 dialog AddText/AddOption/AddAvatar(154), option 3 client.Teleport(410, 354, 1002u).";

    private const string GlowtownPortal1SourceEvidence =
        "NpcID.cs:109 GlowtownPortal1 = 20571u; NpcHandler.cs:33379-33392 [Npc(NpcID.GlowtownPortal1)] option 0 dialog AddText/AddOption/AddAvatar(156), option 1 client.Teleport(93, 408, 10137u).";

    private const string GlowtownPortal2SourceEvidence =
        "NpcID.cs:110 GlowtownPortal2 = 20572u; NpcHandler.cs:33396-33409 [Npc(NpcID.GlowtownPortal2)] option 0 dialog AddText/AddOption/AddAvatar(156), option 1 client.Teleport(93, 408, 10137u).";

    private const string GlowtownPortal3SourceEvidence =
        "NpcID.cs:111 GlowtownPortal3 = 20573u; NpcHandler.cs:33413-33426 [Npc(NpcID.GlowtownPortal3)] option 0 dialog AddText/AddOption/AddAvatar(156), option 1 client.Teleport(93, 408, 10137u).";

    private const string NobilityDonorSourceEvidence =
        "NpcID.cs:119 NobilityDonor = 2541u; NpcHandler.cs:7854-7905 [Npc(NpcID.NobilityDonor)] option 0 AddText/AddInput(\"\u5929\u77f3\u6350\u732e\u91d1\u989d\", 1, 10)/close; option 1 validates uint input and ConquerPoints before confirmation; option 2 subtracts NobilityCps, adds NobilityCps * 500 to Nobility.Donation, sends NobilityIconCreate, and updates Pool.NobilityRanking.";

    private const string TeleportallSourceEvidence =
        "NpcID.cs:66 Teleportall = 15641u; NpcHandler.cs:4597-4698 [Npc(NpcID.Teleportall)] option 0 emits the travel dialog with AddAvatar(286), options 1/2/3/5/6/7/8/9/10 teleport inside Twin City, and option 4 opens a city submenu with options 15/20/30/40/50.";

    private static readonly LegacyNpcScriptDefinition[] Catalog =
    [
        new LegacyNpcScriptDefinition(
            npcId: 15_641,
            npcEnumName: "Teleportall",
            sourceMethodName: "NpcHandler.Teleportall",
            sourceEvidence: TeleportallSourceEvidence,
            initialDialogLines:
            [
                "We made it easy the matter for you, from here you can move around the city.",
            ],
            initialOptions:
            [
                new LegacyNpcScriptOptionDefinition(
                    "GuildWar.",
                    1,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_002,
                        x: 319,
                        y: 318,
                        "NpcHandler.Teleportall option 1: client.Teleport(319, 318, 1002u) at NpcHandler.cs:4669-4671.")),
                new LegacyNpcScriptOptionDefinition(
                    "SuperGuildwar",
                    2,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_002,
                        x: 328,
                        y: 307,
                        "NpcHandler.Teleportall option 2: client.Teleport(328, 307, 1002u) at NpcHandler.cs:4672-4674.")),
                new LegacyNpcScriptOptionDefinition(
                    "CTF",
                    3,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_002,
                        x: 319,
                        y: 318,
                        "NpcHandler.Teleportall option 3: client.Teleport(319, 318, 1002u) at NpcHandler.cs:4675-4677.")),
                new LegacyNpcScriptOptionDefinition(
                    "ClanWar[Choose-Map]",
                    4,
                    LegacyNpcScriptOptionResult.Unsupported(
                        "NpcHandler.Teleportall option 4 emits a city-selection submenu at NpcHandler.cs:4640-4668; local runtime catalogs the submenu destinations but does not yet send nested dialog continuations.")),
                new LegacyNpcScriptOptionDefinition(
                    "Arena",
                    5,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_002,
                        x: 422,
                        y: 273,
                        "NpcHandler.Teleportall option 5: client.Teleport(422, 273, 1002u) at NpcHandler.cs:4679-4681.")),
                new LegacyNpcScriptOptionDefinition(
                    "SkillTeamPK",
                    6,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_002,
                        x: 448,
                        y: 271,
                        "NpcHandler.Teleportall option 6: client.Teleport(448, 271, 1002u) at NpcHandler.cs:4682-4684.")),
                new LegacyNpcScriptOptionDefinition(
                    "TeamPK",
                    7,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_002,
                        x: 443,
                        y: 272,
                        "NpcHandler.Teleportall option 7: client.Teleport(443, 272, 1002u) at NpcHandler.cs:4685-4687.")),
                new LegacyNpcScriptOptionDefinition(
                    "ElitePK",
                    8,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_002,
                        x: 438,
                        y: 272,
                        "NpcHandler.Teleportall option 8: client.Teleport(438, 272, 1002u) at NpcHandler.cs:4688-4690.")),
                new LegacyNpcScriptOptionDefinition(
                    "Events-Server",
                    9,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_002,
                        x: 372,
                        y: 363,
                        "NpcHandler.Teleportall option 9: client.Teleport(372, 363, 1002u) at NpcHandler.cs:4691-4693.")),
                new LegacyNpcScriptOptionDefinition(
                    "Poker",
                    10,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_002,
                        x: 347,
                        y: 444,
                        "NpcHandler.Teleportall option 10: client.Teleport(347, 444, 1002u) at NpcHandler.cs:4694-4697.")),
                new LegacyNpcScriptOptionDefinition(
                    "No Thx.",
                    LegacyNpcReplyPacket.ClosedOptionId,
                    LegacyNpcScriptOptionResult.NoOp(
                        "NpcHandler.Teleportall option byte.MaxValue is the close dialog option at NpcHandler.cs:4628-4630; no source case branch mutates state.")),
            ],
            avatarId: 286,
            continuationOptions:
            [
                new LegacyNpcScriptOptionDefinition(
                    "T.C.",
                    15,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_002,
                        x: 431,
                        y: 269,
                        "NpcHandler.Teleportall option 15: client.Teleport(431, 269, 1002u) at NpcHandler.cs:4629-4631.")),
                new LegacyNpcScriptOptionDefinition(
                    "Phonix",
                    20,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_011,
                        x: 209,
                        y: 260,
                        "NpcHandler.Teleportall option 20: client.Teleport(209, 260, 1011u) at NpcHandler.cs:4632-4634.")),
                new LegacyNpcScriptOptionDefinition(
                    "ApeCity",
                    30,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_020,
                        x: 565,
                        y: 583,
                        "NpcHandler.Teleportall option 30: client.Teleport(565, 583, 1020u) at NpcHandler.cs:4635-4637.")),
                new LegacyNpcScriptOptionDefinition(
                    "DesertCity",
                    40,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_000,
                        x: 492,
                        y: 667,
                        "NpcHandler.Teleportall option 40: client.Teleport(492, 667, 1000u) at NpcHandler.cs:4638-4640.")),
                new LegacyNpcScriptOptionDefinition(
                    "BirdIsland",
                    50,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_015,
                        x: 706,
                        y: 571,
                        "NpcHandler.Teleportall option 50: client.Teleport(706, 571, 1015u) at NpcHandler.cs:4641-4643.")),
            ]),
        new LegacyNpcScriptDefinition(
            npcId: 52_002,
            npcEnumName: "VipQuest",
            sourceMethodName: "NpcHandler.VipQuest",
            sourceEvidence: VipQuestSourceEvidence,
            initialDialogLines:
            [
                "\u6211\u6562\u80af\u5b9a\u4f60\u6b63\u5728\u5bfb\u627e\u4e00\u79cd\u83b7\u5f97VIP 6\u7684\u65b9\u6cd5\uff0c\u597d\u5728\u6709\u4e00\u79cd\u65b9\u6cd5\uff0c\u4f46\u4e0d\u4f1a\u90a3\u4e48\u5bb9\u6613\u3002",
            ],
            initialOptions:
            [
                new LegacyNpcScriptOptionDefinition(
                    "\u6ca1\u95ee\u9898\u3002",
                    1,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 11_127,
                        x: 208,
                        y: 255,
                        "NpcHandler.VipQuest option 1: client.Teleport(208, 255, 11127u) at NpcHandler.cs:26134-26135.")),
                new LegacyNpcScriptOptionDefinition(
                    "\u6211\u4f1a\u518d\u8003\u8651\u4e00\u4e0b\u3002",
                    LegacyNpcReplyPacket.ClosedOptionId,
                    LegacyNpcScriptOptionResult.NoOp(
                        "NpcHandler.VipQuest option byte.MaxValue is the close dialog option at NpcHandler.cs:26129-26131; no source case branch mutates state.")),
            ],
            avatarId: 105),
        new LegacyNpcScriptDefinition(
            npcId: 10_032,
            npcEnumName: "BackTC",
            sourceMethodName: "NpcHandler.BackTC",
            sourceEvidence: BackTcSourceEvidence,
            initialDialogLines:
            [
                "      \u9601\u4e0b\u662f\u6700\u540e\u5b58\u6d3b\u4e0b\u6765\u7684\u73a9\u5bb6\uff1f\u4f60\u662f\u8981\u8fd4\u56de\u53cc\u9f99\u57ce\u5417\uff1f",
            ],
            initialOptions:
            [
                new LegacyNpcScriptOptionDefinition(
                    "\u8bf7\u5e26\u6211\u56de\u53bb\u3002",
                    3,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 1_002,
                        x: 410,
                        y: 354,
                        "NpcHandler.BackTC option 3: client.Teleport(410, 354, 1002u) at NpcHandler.cs:56966-56967.")),
                new LegacyNpcScriptOptionDefinition(
                    "\u554a\uff0c\u7b49\u7b49",
                    LegacyNpcReplyPacket.ClosedOptionId,
                    LegacyNpcScriptOptionResult.NoOp(
                        "NpcHandler.BackTC option byte.MaxValue is the close dialog option at NpcHandler.cs:56961-56963; no source case branch mutates state.")),
            ],
            avatarId: 154),
        new LegacyNpcScriptDefinition(
            npcId: 2_541,
            npcEnumName: "NobilityDonor",
            sourceMethodName: "NpcHandler.NobilityDonor",
            sourceEvidence: NobilityDonorSourceEvidence,
            initialDialogLines:
            [
                "    \u5728\u6b64\u4f60\u53ef\u4ee5\u5feb\u901f\u8fdb\u884c\u7235\u4f4d\u6350\u732e\u3002\u5929\u77f3\u6bd4\u4f8b\u662f1:50000\u3001\u8fd9\u5c31\u610f\u5473\u77401\u70b9\u5929\u77f3\u62b5\u626350000\u679a\u94f6\u4e24\uff0c\u6700\u4f4e\u9650\u989d\u4e3a1,000,000\u70b9\u5929\u77f3\u3002",
            ],
            initialOptions:
            [
                new LegacyNpcScriptOptionDefinition(
                    "\u53ea\u662f\u8def\u8fc7\u3002",
                    LegacyNpcReplyPacket.ClosedOptionId,
                    LegacyNpcScriptOptionResult.NoOp(
                        "NpcHandler.NobilityDonor option byte.MaxValue is the close dialog option at NpcHandler.cs:7865-7867; no source case branch mutates state.")),
            ],
            avatarId: 6,
            initialInputs:
            [
                new LegacyNpcScriptInputDefinition(
                    "\u5929\u77f3\u6350\u732e\u91d1\u989d",
                    1,
                    10,
                    "NpcHandler.NobilityDonor option 0 emits AddInput(\"\u5929\u77f3\u6350\u732e\u91d1\u989d\", 1, 10) at NpcHandler.cs:7864-7867."),
            ],
            continuationOptions:
            [
                new LegacyNpcScriptOptionDefinition(
                    "\u662f\u7684\u3002",
                    2,
                    LegacyNpcScriptOptionResult.Unsupported(
                        "NpcHandler.NobilityDonor option 2 applies a stateful CP-backed nobility donation: client.Player.ConquerPoints -= client.Player.NobilityCps; num = NobilityCps * 500; client.Player.Nobility.Donation += num; client.Send(stream.NobilityIconCreate(...)); Pool.NobilityRanking.UpdateRank(...) at NpcHandler.cs:7893-7902.")),
                new LegacyNpcScriptOptionDefinition(
                    "\u8def\u8fc7\u800c\u5df2\u3002",
                    LegacyNpcReplyPacket.ClosedOptionId,
                    LegacyNpcScriptOptionResult.NoOp(
                        "NpcHandler.NobilityDonor option 1 confirmation adds close option byte.MaxValue via AddOption(\"\u8def\u8fc7\u800c\u5df2\u3002\", byte.MaxValue) at NpcHandler.cs:7876-7878; source resets client.Player.NobilityCps for non-option-2 requests at NpcHandler.cs:7858-7861.")),
            ]),
        new LegacyNpcScriptDefinition(
            npcId: 20_571,
            npcEnumName: "GlowtownPortal1",
            sourceMethodName: "NpcHandler.GlowtownPortal1",
            sourceEvidence: GlowtownPortal1SourceEvidence,
            initialDialogLines:
            [
                "\u4f60\u786e\u5b9a\u8981\u56de\u5230\u57ce\u9547\u5417\uff1f",
            ],
            initialOptions:
            [
                new LegacyNpcScriptOptionDefinition(
                    "\u662f\u7684\u3002",
                    1,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 10_137,
                        x: 93,
                        y: 408,
                        "NpcHandler.GlowtownPortal1 option 1: client.Teleport(93, 408, 10137u) at NpcHandler.cs:33390-33392.")),
                new LegacyNpcScriptOptionDefinition(
                    "\u518d\u8d70\u8d70\u3002",
                    LegacyNpcReplyPacket.ClosedOptionId,
                    LegacyNpcScriptOptionResult.NoOp(
                        "NpcHandler.GlowtownPortal1 option byte.MaxValue is the close dialog option at NpcHandler.cs:33386-33388; no source case branch mutates state.")),
            ],
            avatarId: 156),
        new LegacyNpcScriptDefinition(
            npcId: 20_572,
            npcEnumName: "GlowtownPortal2",
            sourceMethodName: "NpcHandler.GlowtownPortal2",
            sourceEvidence: GlowtownPortal2SourceEvidence,
            initialDialogLines:
            [
                "\u4f60\u786e\u5b9a\u8981\u56de\u5230\u57ce\u9547\u5417\uff1f",
            ],
            initialOptions:
            [
                new LegacyNpcScriptOptionDefinition(
                    "\u662f\u7684\u3002",
                    1,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 10_137,
                        x: 93,
                        y: 408,
                        "NpcHandler.GlowtownPortal2 option 1: client.Teleport(93, 408, 10137u) at NpcHandler.cs:33407-33409.")),
                new LegacyNpcScriptOptionDefinition(
                    "\u518d\u8d70\u8d70\u3002",
                    LegacyNpcReplyPacket.ClosedOptionId,
                    LegacyNpcScriptOptionResult.NoOp(
                        "NpcHandler.GlowtownPortal2 option byte.MaxValue is the close dialog option at NpcHandler.cs:33403-33405; no source case branch mutates state.")),
            ],
            avatarId: 156),
        new LegacyNpcScriptDefinition(
            npcId: 20_573,
            npcEnumName: "GlowtownPortal3",
            sourceMethodName: "NpcHandler.GlowtownPortal3",
            sourceEvidence: GlowtownPortal3SourceEvidence,
            initialDialogLines:
            [
                "\u4f60\u786e\u5b9a\u8981\u56de\u5230\u57ce\u9547\u5417\uff1f",
            ],
            initialOptions:
            [
                new LegacyNpcScriptOptionDefinition(
                    "\u662f\u7684\u3002",
                    1,
                    LegacyNpcScriptOptionResult.Teleport(
                        mapId: 10_137,
                        x: 93,
                        y: 408,
                        "NpcHandler.GlowtownPortal3 option 1: client.Teleport(93, 408, 10137u) at NpcHandler.cs:33424-33426.")),
                new LegacyNpcScriptOptionDefinition(
                    "\u518d\u8d70\u8d70\u3002",
                    LegacyNpcReplyPacket.ClosedOptionId,
                    LegacyNpcScriptOptionResult.NoOp(
                        "NpcHandler.GlowtownPortal3 option byte.MaxValue is the close dialog option at NpcHandler.cs:33420-33422; no source case branch mutates state.")),
            ],
            avatarId: 156),
    ];

    private static readonly IReadOnlyDictionary<uint, LegacyNpcScriptDefinition> CatalogByNpcId =
        Catalog.ToDictionary(definition => definition.NpcId);

    public LegacyNpcScriptDefinition(
        uint npcId,
        string npcEnumName,
        string sourceMethodName,
        string sourceEvidence,
        IReadOnlyList<string> initialDialogLines,
        IReadOnlyList<LegacyNpcScriptOptionDefinition> initialOptions,
        ushort? avatarId,
        IReadOnlyList<LegacyNpcScriptInputDefinition>? initialInputs = null,
        IReadOnlyList<LegacyNpcScriptOptionDefinition>? continuationOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(npcEnumName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceMethodName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);
        ArgumentNullException.ThrowIfNull(initialDialogLines);
        ArgumentNullException.ThrowIfNull(initialOptions);

        NpcId = npcId;
        NpcEnumName = npcEnumName;
        SourceMethodName = sourceMethodName;
        SourceEvidence = sourceEvidence;
        InitialDialogLines = initialDialogLines.ToArray();
        InitialOptions = initialOptions.ToArray();
        AvatarId = avatarId;
        InitialInputs = initialInputs?.ToArray() ?? [];
        ContinuationOptions = continuationOptions?.ToArray() ?? [];
    }

    public uint NpcId { get; }

    public string NpcEnumName { get; }

    public string SourceMethodName { get; }

    public string SourceEvidence { get; }

    public IReadOnlyList<string> InitialDialogLines { get; }

    public IReadOnlyList<LegacyNpcScriptOptionDefinition> InitialOptions { get; }

    public IReadOnlyList<LegacyNpcScriptInputDefinition> InitialInputs { get; }

    public IReadOnlyList<LegacyNpcScriptOptionDefinition> ContinuationOptions { get; }

    public ushort? AvatarId { get; }

    public static IReadOnlyList<LegacyNpcScriptDefinition> All => Catalog;

    public static bool TryGetByNpcId(
        uint npcId,
        [NotNullWhen(true)] out LegacyNpcScriptDefinition? definition)
    {
        return CatalogByNpcId.TryGetValue(npcId, out definition);
    }

    public bool TryGetInitialOption(
        byte optionId,
        [NotNullWhen(true)] out LegacyNpcScriptOptionDefinition? option)
    {
        option = InitialOptions.FirstOrDefault(candidate => candidate.OptionId == optionId);
        return option is not null;
    }

    public bool TryGetInitialInput(
        byte optionId,
        [NotNullWhen(true)] out LegacyNpcScriptInputDefinition? input)
    {
        input = InitialInputs.FirstOrDefault(candidate => candidate.OptionId == optionId);
        return input is not null;
    }

    public bool TryGetScriptOption(
        byte optionId,
        [NotNullWhen(true)] out LegacyNpcScriptOptionDefinition? option)
    {
        if (TryGetInitialOption(optionId, out option))
        {
            return true;
        }

        option = ContinuationOptions.FirstOrDefault(candidate => candidate.OptionId == optionId);
        return option is not null;
    }

    public string DescribeForLog()
    {
        return string.Concat(
            "legacyScript=",
            SourceMethodName,
            " npcId=",
            NpcId.ToString(CultureInfo.InvariantCulture),
            " npcEnum=",
            NpcEnumName,
            " initialLines=",
            InitialDialogLines.Count.ToString(CultureInfo.InvariantCulture),
            " initialOptions=",
            InitialOptions.Count.ToString(CultureInfo.InvariantCulture),
            " avatar=",
            AvatarId?.ToString(CultureInfo.InvariantCulture) ?? "none",
            " sourceEvidence=",
            SourceEvidence);
    }
}
