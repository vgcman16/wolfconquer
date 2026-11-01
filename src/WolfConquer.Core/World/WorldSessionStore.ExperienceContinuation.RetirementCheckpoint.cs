using System.Globalization;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Cleanup evidence, never a gameplay receipt. Only the concrete committed
    // writers below this partial's private call sites can start/advance it.
    private sealed class ContinuationRetirementCheckpoint
    {
        internal readonly WorldPacketHandlerContext Context;
        internal readonly WorldSession Session;
        internal readonly byte[] Character;
        internal readonly LegacyExperienceContinuation? Pending;
        internal readonly LegacySavedSpellImage Spells;
        internal readonly LegacyNativeAttributeMapProof Attributes, Paid;
        internal readonly LegacyNativeActorScalarState.NativeValueProof Scalars;
        internal readonly LegacyActionState.NativeValueProof Action;
        internal readonly string? ItemsPath;
        internal readonly byte[]? Items;
        internal Exception? RetiredFailure;

        internal ContinuationRetirementCheckpoint(WorldPacketHandlerContext context, WorldSession session,
            byte[] character, LegacySavedSpellImage spells, string? itemsPath, byte[]? items)
        {
            Context = context; Session = session; Character = character.ToArray(); Spells = spells;
            ItemsPath = itemsPath; Items = items?.ToArray();
            Pending = LegacyExperienceContinuationCodec.ReadCharacterImage(Character, session.AccountId, checked((uint)session.Character.Uid));
            Attributes = LegacyNativeAttributeMapProof.CaptureRaw(session.Character.Attributes);
            Paid = LegacyNativeAttributeMapProof.CaptureRaw(session.Character.PaidNobility);
            Scalars = new(session.Character); Action = new(session.Character);
            var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(Character, out _));
            if (!Equals(Pending, LegacyExperienceContinuationCodec.ReadCharacter(session.Character, session.AccountId)) ||
                !LegacyCombatCharacterImageInputs.Matches(disk, session.Character)) throw InvalidRetirementCheckpoint();
        }
    }

    private sealed class ContinuationRetirementSlot { internal ContinuationRetirementCheckpoint? Latest; }
    private readonly ConditionalWeakTable<LegacyOrdinarySpellBinding, ContinuationRetirementSlot> continuationRetirements = new();

    // Called only after the actual credit writer or published admission origin
    // has proved this full image. A pending marker alone never calls this API.
    private void BeginCommittedContinuationRetirement(WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, WorldSession published, byte[] image)
    {
        RequireRetirementPublicationHeld(context, binding, published);
        var checkpoint = new ContinuationRetirementCheckpoint(context, published, image, binding.SavedSpells, null, null);
        if (checkpoint.Pending is null) throw InvalidRetirementCheckpoint();
        continuationRetirements.GetValue(binding, static _ => new()).Latest = checkpoint;
    }

    // Every caller is an actual closed writer after its final persistence
    // postlude, before observers. No caller-supplied projection is accepted.
    private void RecordCommittedContinuationRetirement(WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, WorldSession before, WorldSession published, byte[]? committedImage = null)
    {
        var previous = RequireRetirementPredecessor(context, binding, before, published);
        if (!ReferenceEquals(previous.Spells, binding.SavedSpells)) throw InvalidRetirementCheckpoint();
        continuationRetirements.GetValue(binding, static _ => new()).Latest = new(context, published,
            committedImage ?? previous.Character, previous.Spells, previous.ItemsPath, previous.Items);
    }

    private void RecordDerivedContinuationRetirement(WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, WorldSession before, WorldSession published, LegacyPreparedDerivedSpellQueryWrite writer)
    {
        if (LegacyExperienceContinuationCodec.ReadCharacter(before.Character, before.AccountId) is null) return;
        RequireRetirementPredecessor(context, binding, before, published);
        if (!writer.TryCaptureContinuationRetirement(this, context, binding, before, published,
                out var character, out var itemsPath, out var items)) throw InvalidRetirementCheckpoint();
        continuationRetirements.GetValue(binding, static _ => new()).Latest =
            new(context, published, character, binding.SavedSpells, itemsPath, items);
    }

    private ContinuationRetirementCheckpoint RequireRetirementPredecessor(WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, WorldSession before, WorldSession published)
    {
        RequireRetirementPublicationHeld(context, binding, published);
        if (!continuationRetirements.TryGetValue(binding, out var slot) || slot.Latest is not { RetiredFailure: null } previous ||
            !ReferenceEquals(previous.Session, before) || !SameRetirementOperation(previous.Context, context) ||
            !previous.Attributes.IsCurrentPlain() || !previous.Paid.IsCurrentPlain()) throw InvalidRetirementCheckpoint();
        return previous;
    }

    private void RequireRetirementPublicationHeld(WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, WorldSession published)
    {
        if (framePersistenceOwner is not { } frame || !frame.IsActionLifecycleCurrent(this, context) ||
            !ReferenceEquals(context.Connection, binding.Connection) || !ReferenceEquals(binding.Store, this) ||
            context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) != true ||
            !_connectionGates.TryGetValue(binding.Connection, out var gate) || !Monitor.IsEntered(gate) ||
            !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
            !_sessions.TryGetValue(binding.Connection, out var holder) || !BindingIsCurrentPlain(binding, holder, published))
            throw InvalidRetirementCheckpoint();
    }

    private static bool SameRetirementOperation(WorldPacketHandlerContext first, WorldPacketHandlerContext second) =>
        ReferenceEquals(first.Connection, second.Connection) && ReferenceEquals(first.Sessions, second.Sessions) &&
        ReferenceEquals(first.Trades, second.Trades) && ReferenceEquals(first.TradeOperations, second.TradeOperations) &&
        ReferenceEquals(first.TradeRecovery, second.TradeRecovery) && ReferenceEquals(first.WorldData, second.WorldData) &&
        ReferenceEquals(first.FrameOperationLease, second.FrameOperationLease);

    private static string RetirementCharacterPath(LegacyOrdinarySpellBinding binding) =>
        Path.Combine(binding.UsersRoot, binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
    private static string RetirementSpellsPath(LegacyOrdinarySpellBinding binding) =>
        Path.Combine(binding.SpellsRoot, binding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
    private static InvalidOperationException InvalidRetirementCheckpoint() => new("experienceContinuationRetirementCheckpointInvalid");
}
