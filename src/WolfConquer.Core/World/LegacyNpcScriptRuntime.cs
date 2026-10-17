using System.Globalization;

namespace WolfConquer.Core.World;

public enum LegacyNpcScriptRuntimeStatus
{
    Modeled = 0,
    Ignored = 1,
    Unsupported = 2,
}

public enum LegacyNpcScriptRuntimeActionCategory
{
    None = 0,
    Dialog = 1,
    Teleport = 2,
    Shop = 3,
    OpenUi = 4,
    NoOp = 5,
    CloseDialog = 6,
    FurniturePickup = 7,
    StatueRemoval = 8,
    CursorAction = 9,
    MessageBoxCallback = 10,
    KnownSourceScript = 11,
    UnknownSourceScript = 12,
    UnknownOption = 13,
    InvalidRequest = 14,
    StatefulNpcScript = 15,
}

public enum LegacyNpcScriptRuntimeUnsupportedReason
{
    None = 0,
    ClosedOptionIgnored = 1,
    ZeroReplyOptionIgnored = 2,
    UnsupportedServerAction = 3,
    InvalidInteractType = 4,
    UnsupportedInteractType = 5,
    MissingNpcId = 6,
    MissingRuntimeDefinition = 7,
    MissingSourceScript = 8,
    MissingOption = 9,
    StatefulSourceAction = 10,
}

public sealed record LegacyNpcScriptRuntimeDialog(
    IReadOnlyList<string> Lines,
    IReadOnlyList<LegacyNpcScriptOptionDefinition> Options,
    ushort? AvatarId,
    string SourceEvidence)
{
    public IReadOnlyList<LegacyNpcScriptInputDefinition> Inputs { get; init; } =
        Array.Empty<LegacyNpcScriptInputDefinition>();

    public string DescribeForLog()
    {
        var parts = new List<string>
        {
            string.Concat("dialogLines=", Lines.Count.ToString(CultureInfo.InvariantCulture)),
            string.Concat("dialogOptions=", Options.Count.ToString(CultureInfo.InvariantCulture)),
            string.Concat("avatar=", AvatarId?.ToString(CultureInfo.InvariantCulture) ?? "none"),
        };

        if (Inputs.Count > 0)
        {
            parts.Add(string.Concat("dialogInputs=", Inputs.Count.ToString(CultureInfo.InvariantCulture)));
        }

        parts.Add(string.Concat("sourceEvidence=", SourceEvidence));
        return string.Join(" ", parts);
    }
}

public enum LegacyNpcScriptStatefulActionStep
{
    None = 0,
    Prompt = 1,
    AmountInput = 2,
    Confirmation = 3,
    CancelClose = 4,
    PendingMutation = 5,
    UnsupportedMutationPersistence = 6,
}

public sealed record LegacyNpcScriptStatefulActionState(
    string ActionName,
    LegacyNpcScriptStatefulActionStep Step,
    uint? PendingConquerPoints)
{
    public static LegacyNpcScriptStatefulActionState None { get; } =
        new(string.Empty, LegacyNpcScriptStatefulActionStep.None, PendingConquerPoints: null);

    public string DescribeForLog()
    {
        var parts = new List<string>
        {
            string.Concat("actionName=", string.IsNullOrWhiteSpace(ActionName) ? "none" : ActionName),
            string.Concat("stateStep=", FormatStep(Step)),
        };

        if (PendingConquerPoints.HasValue)
        {
            parts.Add(string.Concat(
                "pendingConquerPoints=",
                PendingConquerPoints.Value.ToString(CultureInfo.InvariantCulture)));
        }

        return string.Join(" ", parts);
    }

    internal static string FormatStep(LegacyNpcScriptStatefulActionStep step)
    {
        return step switch
        {
            LegacyNpcScriptStatefulActionStep.Prompt => "prompt",
            LegacyNpcScriptStatefulActionStep.AmountInput => "amountInput",
            LegacyNpcScriptStatefulActionStep.Confirmation => "confirmation",
            LegacyNpcScriptStatefulActionStep.CancelClose => "cancelClose",
            LegacyNpcScriptStatefulActionStep.PendingMutation => "pendingMutation",
            LegacyNpcScriptStatefulActionStep.UnsupportedMutationPersistence => "unsupportedMutationPersistence",
            _ => "none",
        };
    }
}

public sealed record LegacyNpcScriptStatefulAction(
    string ActionName,
    LegacyNpcScriptStatefulActionStep Step,
    LegacyNpcScriptStatefulActionState NextState,
    LegacyNpcScriptRuntimeDialog? Dialog,
    LegacyNpcScriptOptionResult? Result,
    string SourceEvidence,
    string NextSeam)
{
    public LegacyNpcScriptPendingMutation? PendingMutation => Result?.PendingMutation;

    public string DescribeForLog()
    {
        var parts = new List<string>
        {
            string.Concat("statefulAction=", ActionName),
            string.Concat("statefulStep=", LegacyNpcScriptStatefulActionState.FormatStep(Step)),
            string.Concat("nextState={", NextState.DescribeForLog(), "}"),
        };

        if (Dialog is not null)
        {
            parts.Add(Dialog.DescribeForLog());
        }

        if (Result is not null)
        {
            parts.Add(Result.DescribeForLog());
        }

        parts.Add(string.Concat("nextSeam=", NextSeam));
        parts.Add(string.Concat("sourceEvidence=", SourceEvidence));
        return string.Join(" ", parts);
    }
}

public sealed record LegacyNpcScriptRuntimeResolution(
    LegacyNpcScriptRuntimeStatus Status,
    LegacyNpcScriptRuntimeActionCategory ActionCategory,
    LegacyNpcScriptRuntimeUnsupportedReason Reason,
    uint NpcId,
    byte OptionId,
    byte InteractType,
    LegacyNpcServerAction ServerAction,
    string? NpcEnumName,
    string? SourceMethodName,
    LegacyNpcScriptRuntimeDialog? Dialog,
    LegacyNpcScriptOptionResult? Result,
    string SourceEvidence,
    string NextSeam)
{
    public LegacyNpcScriptStatefulAction? StatefulAction { get; init; }

    public bool IsModeled => Status == LegacyNpcScriptRuntimeStatus.Modeled;

    public bool IsUnsupported => Status == LegacyNpcScriptRuntimeStatus.Unsupported;

    public bool IsIgnored => Status == LegacyNpcScriptRuntimeStatus.Ignored;

    public string DescribeForLog()
    {
        var parts = new List<string>
        {
            string.Concat("status=", FormatStatus(Status)),
            string.Concat("category=", FormatActionCategory(ActionCategory)),
            string.Concat("reason=", FormatReason(Reason)),
            string.Concat("npcId=", NpcId.ToString(CultureInfo.InvariantCulture)),
            string.Concat("optionId=", OptionId.ToString(CultureInfo.InvariantCulture)),
            string.Concat("interactType=", LegacyNpcServerRequestPacket.FormatInteractTypeForLog(InteractType)),
            string.Concat("serverAction=", ServerAction),
        };

        if (!string.IsNullOrWhiteSpace(NpcEnumName))
        {
            parts.Add(string.Concat("npcEnum=", NpcEnumName));
        }

        if (!string.IsNullOrWhiteSpace(SourceMethodName))
        {
            parts.Add(string.Concat("legacyScript=", SourceMethodName));
        }

        if (Dialog is not null)
        {
            parts.Add(Dialog.DescribeForLog());
        }

        if (Result is not null)
        {
            parts.Add(Result.DescribeForLog());
        }

        if (StatefulAction is not null)
        {
            parts.Add(StatefulAction.DescribeForLog());
        }

        parts.Add(string.Concat("nextSeam=", NextSeam));
        parts.Add(string.Concat("sourceEvidence=", SourceEvidence));

        return string.Join(" ", parts);
    }

    private static string FormatStatus(LegacyNpcScriptRuntimeStatus status)
    {
        return status switch
        {
            LegacyNpcScriptRuntimeStatus.Modeled => "modeled",
            LegacyNpcScriptRuntimeStatus.Ignored => "ignored",
            LegacyNpcScriptRuntimeStatus.Unsupported => "unsupported",
            _ => "unsupported",
        };
    }

    private static string FormatActionCategory(LegacyNpcScriptRuntimeActionCategory category)
    {
        return category switch
        {
            LegacyNpcScriptRuntimeActionCategory.None => "none",
            LegacyNpcScriptRuntimeActionCategory.Dialog => "dialog",
            LegacyNpcScriptRuntimeActionCategory.Teleport => "teleport",
            LegacyNpcScriptRuntimeActionCategory.Shop => "shop",
            LegacyNpcScriptRuntimeActionCategory.OpenUi => "openUi",
            LegacyNpcScriptRuntimeActionCategory.NoOp => "noOp",
            LegacyNpcScriptRuntimeActionCategory.CloseDialog => "closeDialog",
            LegacyNpcScriptRuntimeActionCategory.FurniturePickup => "furniturePickup",
            LegacyNpcScriptRuntimeActionCategory.StatueRemoval => "statueRemoval",
            LegacyNpcScriptRuntimeActionCategory.CursorAction => "cursorAction",
            LegacyNpcScriptRuntimeActionCategory.MessageBoxCallback => "messageBoxCallback",
            LegacyNpcScriptRuntimeActionCategory.KnownSourceScript => "knownSourceScript",
            LegacyNpcScriptRuntimeActionCategory.UnknownSourceScript => "unknownSourceScript",
            LegacyNpcScriptRuntimeActionCategory.UnknownOption => "unknownOption",
            LegacyNpcScriptRuntimeActionCategory.InvalidRequest => "invalidRequest",
            LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript => "statefulNpcScript",
            _ => "invalidRequest",
        };
    }

    private static string FormatReason(LegacyNpcScriptRuntimeUnsupportedReason reason)
    {
        return reason switch
        {
            LegacyNpcScriptRuntimeUnsupportedReason.None => "none",
            LegacyNpcScriptRuntimeUnsupportedReason.ClosedOptionIgnored => "closedOptionIgnored",
            LegacyNpcScriptRuntimeUnsupportedReason.ZeroReplyOptionIgnored => "zeroReplyOptionIgnored",
            LegacyNpcScriptRuntimeUnsupportedReason.UnsupportedServerAction => "unsupportedServerAction",
            LegacyNpcScriptRuntimeUnsupportedReason.InvalidInteractType => "invalidInteractType",
            LegacyNpcScriptRuntimeUnsupportedReason.UnsupportedInteractType => "unsupportedInteractType",
            LegacyNpcScriptRuntimeUnsupportedReason.MissingNpcId => "missingNpcId",
            LegacyNpcScriptRuntimeUnsupportedReason.MissingRuntimeDefinition => "missingRuntimeDefinition",
            LegacyNpcScriptRuntimeUnsupportedReason.MissingSourceScript => "missingSourceScript",
            LegacyNpcScriptRuntimeUnsupportedReason.MissingOption => "missingOption",
            LegacyNpcScriptRuntimeUnsupportedReason.StatefulSourceAction => "statefulSourceAction",
            _ => "unsupportedInteractType",
        };
    }
}

public static partial class LegacyNpcScriptRuntime
{
    private const string ModeledNextSeam = "none";
    private const string ModelDefinitionNextSeam = "model-npc-script-definition";
    private const string SourceTriageNextSeam = "source-triage";
    private const string StatefulSourceActionNextSeam = "wire-stateful-npc-script-action";

    private const string Packet2084ScriptEvidence =
        "Procesor.cs:323-381 [Packet(2084)] reads NpcReply.NpcRequestProto and, for default action, stores ActiveNpc then enqueues InvokerClient when option != byte.MaxValue.";

    private const string Packet2255ReplyEvidence =
        "Procesor.cs:386-427 [Packet(2255)] reuses user.ActiveNpc and enqueues InvokerClient only when option != byte.MaxValue and option != 0.";

    private const string MessageBoxEvidence =
        "Procesor.cs:394-421 handles type 6 as message-box OK/cancel callbacks, not NPC script execution.";

    public static LegacyNpcScriptRuntimeResolution Resolve(LegacyNpcServerRequestPacket request)
    {
        return Resolve(request, statefulActionState: null);
    }

    public static LegacyNpcScriptRuntimeResolution Resolve(
        LegacyNpcServerRequestPacket request,
        LegacyNpcScriptStatefulActionState? statefulActionState)
    {
        return ResolveScriptRequest(
            request.NpcId,
            request.OptionId,
            request.InteractType,
            request.Action,
            request.Input ?? string.Empty,
            Packet2084ScriptEvidence,
            statefulActionState);
    }

    public static LegacyNpcScriptRuntimeResolution Resolve(
        LegacyNpcDialogRequestPacket reply,
        uint activeNpcId)
    {
        return Resolve(reply, activeNpcId, statefulActionState: null);
    }

    public static LegacyNpcScriptRuntimeResolution Resolve(
        LegacyNpcDialogRequestPacket reply,
        uint activeNpcId,
        LegacyNpcScriptStatefulActionState? statefulActionState)
    {
        var npcId = reply.ResolveNpcId(activeNpcId);

        if (reply.IsClosedOption)
        {
            var statefulAction = CreateNobilityDonorCancelCloseActionIfMatched(
                npcId,
                Packet2255ReplyEvidence,
                statefulActionState);
            return Create(
                LegacyNpcScriptRuntimeStatus.Ignored,
                LegacyNpcScriptRuntimeActionCategory.CloseDialog,
                LegacyNpcScriptRuntimeUnsupportedReason.ClosedOptionIgnored,
                npcId,
                reply.OptionId,
                reply.InteractType,
                LegacyNpcServerAction.Script,
                sourceEvidence: Packet2255ReplyEvidence,
                nextSeam: ModeledNextSeam,
                statefulAction: statefulAction);
        }

        if (reply.OptionId == 0)
        {
            return Create(
                LegacyNpcScriptRuntimeStatus.Ignored,
                LegacyNpcScriptRuntimeActionCategory.None,
                LegacyNpcScriptRuntimeUnsupportedReason.ZeroReplyOptionIgnored,
                npcId,
                reply.OptionId,
                reply.InteractType,
                LegacyNpcServerAction.Script,
                sourceEvidence: Packet2255ReplyEvidence,
                nextSeam: ModeledNextSeam);
        }

        if (npcId == 0)
        {
            return Create(
                LegacyNpcScriptRuntimeStatus.Unsupported,
                LegacyNpcScriptRuntimeActionCategory.InvalidRequest,
                LegacyNpcScriptRuntimeUnsupportedReason.MissingNpcId,
                npcId,
                reply.OptionId,
                reply.InteractType,
                LegacyNpcServerAction.Script,
                sourceEvidence: string.Concat(Packet2255ReplyEvidence, " Local active NPC fallback also resolved to 0."),
                nextSeam: SourceTriageNextSeam);
        }

        return ResolveScriptRequest(
            npcId,
            reply.OptionId,
            reply.InteractType,
            LegacyNpcServerAction.Script,
            reply.Input ?? string.Empty,
            Packet2255ReplyEvidence,
            statefulActionState);
    }

    private static LegacyNpcScriptRuntimeResolution ResolveScriptRequest(
        uint npcId,
        byte optionId,
        byte interactType,
        LegacyNpcServerAction serverAction,
        string input,
        string sourceEvidencePrefix,
        LegacyNpcScriptStatefulActionState? statefulActionState)
    {
        if (serverAction != LegacyNpcServerAction.Script)
        {
            return ResolveUnsupportedServerAction(npcId, optionId, interactType, serverAction);
        }

        if (optionId == LegacyNpcReplyPacket.ClosedOptionId)
        {
            var statefulAction = CreateNobilityDonorCancelCloseActionIfMatched(
                npcId,
                sourceEvidencePrefix,
                statefulActionState);
            return Create(
                LegacyNpcScriptRuntimeStatus.Ignored,
                LegacyNpcScriptRuntimeActionCategory.CloseDialog,
                LegacyNpcScriptRuntimeUnsupportedReason.ClosedOptionIgnored,
                npcId,
                optionId,
                interactType,
                serverAction,
                sourceEvidence: sourceEvidencePrefix,
                nextSeam: ModeledNextSeam,
                statefulAction: statefulAction);
        }

        if (!TryValidateScriptInteractType(
            npcId,
            optionId,
            interactType,
            serverAction,
            sourceEvidencePrefix,
            out var invalidResolution))
        {
            return invalidResolution;
        }

        if (npcId == 0)
        {
            return Create(
                LegacyNpcScriptRuntimeStatus.Unsupported,
                LegacyNpcScriptRuntimeActionCategory.InvalidRequest,
                LegacyNpcScriptRuntimeUnsupportedReason.MissingNpcId,
                npcId,
                optionId,
                interactType,
                serverAction,
                sourceEvidence: string.Concat(sourceEvidencePrefix, " NpcID 0 cannot be resolved to a legacy handler."),
                nextSeam: SourceTriageNextSeam);
        }

        if (TryResolveIronTong(
            npcId,
            optionId,
            interactType,
            serverAction,
            sourceEvidencePrefix,
            out var modeledSourceHintResolution))
        {
            return modeledSourceHintResolution;
        }

        if (TryResolveBlackSmith(
            npcId,
            optionId,
            interactType,
            serverAction,
            sourceEvidencePrefix,
            out modeledSourceHintResolution))
        {
            return modeledSourceHintResolution;
        }

        if (TryResolveWarehouse(
            npcId,
            optionId,
            interactType,
            serverAction,
            sourceEvidencePrefix,
            out modeledSourceHintResolution))
        {
            return modeledSourceHintResolution;
        }

        if (!LegacyNpcScriptDefinition.TryGetByNpcId(npcId, out var definition))
        {
            return ResolveMissingDefinition(npcId, optionId, interactType, serverAction, sourceEvidencePrefix);
        }

        if (optionId == 0)
        {
            var dialog = new LegacyNpcScriptRuntimeDialog(
                definition.InitialDialogLines,
                definition.InitialOptions,
                definition.AvatarId,
                string.Concat(
                    definition.SourceMethodName,
                    " option 0 emits AddText/AddOption/AddAvatar/FinalizeDialog. ",
                    definition.SourceEvidence))
            {
                Inputs = definition.InitialInputs,
            };
            var statefulAction = CreateNobilityDonorPromptActionIfMatched(
                definition,
                dialog,
                sourceEvidencePrefix);

            return Create(
                LegacyNpcScriptRuntimeStatus.Modeled,
                LegacyNpcScriptRuntimeActionCategory.Dialog,
                LegacyNpcScriptRuntimeUnsupportedReason.None,
                definition.NpcId,
                optionId,
                interactType,
                serverAction,
                definition.NpcEnumName,
                definition.SourceMethodName,
                dialog,
                result: null,
                sourceEvidence: string.Concat(sourceEvidencePrefix, " ", definition.SourceEvidence),
                nextSeam: ModeledNextSeam,
                statefulAction: statefulAction);
        }

        if (definition.TryGetInitialInput(optionId, out var inputDefinition))
        {
            if (definition.NpcId == NobilityDonorNpcId)
            {
                return CreateNobilityDonorAmountInputResolution(
                    definition,
                    optionId,
                    interactType,
                    serverAction,
                    input,
                    sourceEvidencePrefix,
                    statefulActionState);
            }

            return Create(
                LegacyNpcScriptRuntimeStatus.Unsupported,
                LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript,
                LegacyNpcScriptRuntimeUnsupportedReason.StatefulSourceAction,
                definition.NpcId,
                optionId,
                interactType,
                serverAction,
                definition.NpcEnumName,
                definition.SourceMethodName,
                dialog: null,
                result: null,
                sourceEvidence: string.Concat(
                    sourceEvidencePrefix,
                    " ",
                    inputDefinition.SourceEvidence,
                    " The follow-up source branch parses Input, checks client.Player.ConquerPoints, stores client.Player.NobilityCps, and may emit a confirmation dialog; local runtime does not execute that stateful branch."),
                nextSeam: StatefulSourceActionNextSeam);
        }

        if (!definition.TryGetScriptOption(optionId, out var option))
        {
            return Create(
                LegacyNpcScriptRuntimeStatus.Unsupported,
                LegacyNpcScriptRuntimeActionCategory.UnknownOption,
                LegacyNpcScriptRuntimeUnsupportedReason.MissingOption,
                definition.NpcId,
                optionId,
                interactType,
                serverAction,
                definition.NpcEnumName,
                definition.SourceMethodName,
                dialog: null,
                result: null,
                sourceEvidence: string.Concat(
                    sourceEvidencePrefix,
                    " ",
                    definition.SourceMethodName,
                    " is in the helper catalog, but option ",
                    optionId.ToString(CultureInfo.InvariantCulture),
                    " is not modeled. ",
                    definition.SourceEvidence),
                nextSeam: ModelDefinitionNextSeam);
        }

        if (option.Result.Kind == LegacyNpcScriptOptionResultKind.Unsupported)
        {
            if (definition.NpcId == NobilityDonorNpcId &&
                optionId == 2)
            {
                return CreateNobilityDonorConfirmationResolution(
                    definition,
                    option,
                    optionId,
                    interactType,
                    serverAction,
                    sourceEvidencePrefix,
                    statefulActionState);
            }

            return Create(
                LegacyNpcScriptRuntimeStatus.Unsupported,
                LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript,
                LegacyNpcScriptRuntimeUnsupportedReason.StatefulSourceAction,
                definition.NpcId,
                optionId,
                interactType,
                serverAction,
                definition.NpcEnumName,
                definition.SourceMethodName,
                dialog: null,
                result: option.Result,
                sourceEvidence: string.Concat(sourceEvidencePrefix, " ", option.Result.SourceEvidence),
                nextSeam: StatefulSourceActionNextSeam);
        }

        return Create(
            LegacyNpcScriptRuntimeStatus.Modeled,
            MapResultKind(option.Result.Kind),
            LegacyNpcScriptRuntimeUnsupportedReason.None,
            definition.NpcId,
            optionId,
            interactType,
            serverAction,
            definition.NpcEnumName,
            definition.SourceMethodName,
            dialog: null,
            result: option.Result,
            sourceEvidence: string.Concat(sourceEvidencePrefix, " ", option.Result.SourceEvidence),
            nextSeam: ModeledNextSeam);
    }

    private static LegacyNpcScriptRuntimeResolution ResolveUnsupportedServerAction(
        uint npcId,
        byte optionId,
        byte interactType,
        LegacyNpcServerAction serverAction)
    {
        var category = serverAction switch
        {
            LegacyNpcServerAction.PlaceFurniture => LegacyNpcScriptRuntimeActionCategory.FurniturePickup,
            LegacyNpcServerAction.Statue => LegacyNpcScriptRuntimeActionCategory.StatueRemoval,
            LegacyNpcServerAction.Cursor => LegacyNpcScriptRuntimeActionCategory.CursorAction,
            _ => LegacyNpcScriptRuntimeActionCategory.InvalidRequest,
        };

        var sourceEvidence = serverAction switch
        {
            LegacyNpcServerAction.PlaceFurniture =>
                "Procesor.cs:333-360 [Packet(2084)] handles Mode.PlaceFurniture as a house-furniture pickup/inventory path, not a script option.",
            LegacyNpcServerAction.Statue =>
                "Procesor.cs:362-373 [Packet(2084)] handles Mode.Statue by removing house furniture/statue entities, not a script option.",
            LegacyNpcServerAction.Cursor =>
                "NpcServerReplay.cs:7-12 defines Mode.Cursor = 5; local script runtime does not execute non-Script actions yet.",
            _ =>
                "Procesor.cs:323-381 [Packet(2084)] only has source-mode branches for PlaceFurniture and Statue before default script enqueue.",
        };

        return Create(
            LegacyNpcScriptRuntimeStatus.Unsupported,
            category,
            LegacyNpcScriptRuntimeUnsupportedReason.UnsupportedServerAction,
            npcId,
            optionId,
            interactType,
            serverAction,
            sourceEvidence: sourceEvidence,
            nextSeam: "wire-non-script-npc-action");
    }

    private static bool TryValidateScriptInteractType(
        uint npcId,
        byte optionId,
        byte interactType,
        LegacyNpcServerAction serverAction,
        string sourceEvidencePrefix,
        out LegacyNpcScriptRuntimeResolution resolution)
    {
        resolution = default!;

        if (!Enum.IsDefined(typeof(LegacyNpcReplyInteractType), interactType))
        {
            resolution = Create(
                LegacyNpcScriptRuntimeStatus.Unsupported,
                LegacyNpcScriptRuntimeActionCategory.InvalidRequest,
                LegacyNpcScriptRuntimeUnsupportedReason.InvalidInteractType,
                npcId,
                optionId,
                interactType,
                serverAction,
                sourceEvidence: string.Concat(sourceEvidencePrefix, " InteractType is not a known legacy reply type."),
                nextSeam: SourceTriageNextSeam);
            return false;
        }

        var replyType = (LegacyNpcReplyInteractType)interactType;
        if (replyType == LegacyNpcReplyInteractType.MessageBox)
        {
            resolution = Create(
                LegacyNpcScriptRuntimeStatus.Unsupported,
                LegacyNpcScriptRuntimeActionCategory.MessageBoxCallback,
                LegacyNpcScriptRuntimeUnsupportedReason.UnsupportedInteractType,
                npcId,
                optionId,
                interactType,
                serverAction,
                sourceEvidence: string.Concat(sourceEvidencePrefix, " ", MessageBoxEvidence),
                nextSeam: "wire-message-box-callback");
            return false;
        }

        if (replyType is not LegacyNpcReplyInteractType.Dialog and
            not LegacyNpcReplyInteractType.Option and
            not LegacyNpcReplyInteractType.Input)
        {
            resolution = Create(
                LegacyNpcScriptRuntimeStatus.Unsupported,
                LegacyNpcScriptRuntimeActionCategory.InvalidRequest,
                LegacyNpcScriptRuntimeUnsupportedReason.UnsupportedInteractType,
                npcId,
                optionId,
                interactType,
                serverAction,
                sourceEvidence: string.Concat(
                    sourceEvidencePrefix,
                    " Local script requests only forward Dialog, Option, or Input reply types."),
                nextSeam: SourceTriageNextSeam);
            return false;
        }

        return true;
    }

    private static LegacyNpcScriptRuntimeResolution Create(
        LegacyNpcScriptRuntimeStatus status,
        LegacyNpcScriptRuntimeActionCategory category,
        LegacyNpcScriptRuntimeUnsupportedReason reason,
        uint npcId,
        byte optionId,
        byte interactType,
        LegacyNpcServerAction serverAction,
        string sourceEvidence,
        string nextSeam,
        LegacyNpcScriptStatefulAction? statefulAction = null)
    {
        return Create(
            status,
            category,
            reason,
            npcId,
            optionId,
            interactType,
            serverAction,
            npcEnumName: null,
            sourceMethodName: null,
            dialog: null,
            result: null,
            sourceEvidence,
            nextSeam,
            statefulAction);
    }

    private static LegacyNpcScriptRuntimeResolution Create(
        LegacyNpcScriptRuntimeStatus status,
        LegacyNpcScriptRuntimeActionCategory category,
        LegacyNpcScriptRuntimeUnsupportedReason reason,
        uint npcId,
        byte optionId,
        byte interactType,
        LegacyNpcServerAction serverAction,
        string? npcEnumName,
        string? sourceMethodName,
        LegacyNpcScriptRuntimeDialog? dialog,
        LegacyNpcScriptOptionResult? result,
        string sourceEvidence,
        string nextSeam,
        LegacyNpcScriptStatefulAction? statefulAction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(nextSeam);

        return new LegacyNpcScriptRuntimeResolution(
            status,
            category,
            reason,
            npcId,
            optionId,
            interactType,
            serverAction,
            npcEnumName,
            sourceMethodName,
            dialog,
            result,
            sourceEvidence,
            nextSeam)
        {
            StatefulAction = statefulAction,
        };
    }

    private static LegacyNpcScriptRuntimeActionCategory MapResultKind(
        LegacyNpcScriptOptionResultKind resultKind)
    {
        return resultKind switch
        {
            LegacyNpcScriptOptionResultKind.Teleport => LegacyNpcScriptRuntimeActionCategory.Teleport,
            LegacyNpcScriptOptionResultKind.Shop => LegacyNpcScriptRuntimeActionCategory.Shop,
            LegacyNpcScriptOptionResultKind.OpenUi => LegacyNpcScriptRuntimeActionCategory.OpenUi,
            LegacyNpcScriptOptionResultKind.PendingMutation => LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript,
            LegacyNpcScriptOptionResultKind.Unsupported => LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript,
            LegacyNpcScriptOptionResultKind.NoOp => LegacyNpcScriptRuntimeActionCategory.NoOp,
            _ => LegacyNpcScriptRuntimeActionCategory.InvalidRequest,
        };
    }

}
