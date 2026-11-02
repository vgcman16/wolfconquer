using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinarySpellBinding
{
    private LegacySavedVigorSeed? savedVigor;
    private IReadOnlyDictionary<string, string>? queryVigorMap;
    private LegacyNativeAttributeMapProof? queryVigorProof;
    private uint queryVigor;
    private LegacyNativeMagicQueryClient.CompletedMaximums? queryVigorCompletion;

    internal void InitializePreparedVigorSeed(LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner,
        byte[] image, WorldSession proposed)
    {
        if (savedVigor is not null || IsRetired || !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            !owner.BeforeOwnerIsCurrentPlain(Client, Player) || owner.PreparedUid != Uid ||
            !ReferenceEquals(proposed.NativeGameClient, Client) || proposed.AccountId != AccountId ||
            proposed.Character.Uid != Uid || Client.OrdinarySpellBinding is not null) throw InvalidVigor();
        savedVigor = new LegacySavedVigorSeed(image, proposed.Character);
    }

    internal bool MatchesSavedVigorSeed(byte[] image)
    {
        try { return savedVigor is not null && savedVigor.Matches(image); }
        catch (Exception error) when (error is InvalidDataException or ArgumentException or FormatException) { return false; }
    }

    internal void RecordBoundQueryVigor(WorldSession current, ushort value,
        LegacyNativeMagicQueryClient.CompletedMaximums completion)
    {
        if (savedVigor is null || !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            !RuntimeInspectionPlayerOwnerIsCurrentPlain() || !ReferenceEquals(current.NativeGameClient, Client) ||
            current.AccountId != AccountId || current.Character.Uid != Uid ||
            LegacyEquipmentQueryBounds.ReadVigor(current.Character) != value || !completion.IsCurrentPlain(this, current)) throw InvalidVigor();
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(current.Character.Attributes);
        if (!proof.IsCurrentPlain()) throw InvalidVigor();
        queryVigorMap = current.Character.Attributes; queryVigorProof = proof; queryVigor = value;
        queryVigorCompletion = completion;
    }

    internal bool MatchesAttributeAllocationVigor(CharacterSnapshot disk, WorldSession expected)
    {
        try
        {
            if (LegacySavedVigorSeed.SameValue(disk, expected.Character)) return true;
            return RuntimeInspectionPlayerOwnerIsCurrentPlain() &&
                ReferenceEquals(expected.NativeGameClient, Client) && expected.AccountId == AccountId &&
                expected.Character.Uid == Uid && ReferenceEquals(expected.Character.Attributes, queryVigorMap) &&
                queryVigorProof is not null && queryVigorProof.IsCurrentPlain() &&
                queryVigorCompletion is not null && queryVigorCompletion.IsCurrentPlain(this, expected) &&
                LegacyEquipmentQueryBounds.ReadVigor(expected.Character) == queryVigor;
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or ArgumentException) { return false; }
    }

    private static InvalidOperationException InvalidVigor() => new("boundQueryVigorOriginUnavailable");
}
