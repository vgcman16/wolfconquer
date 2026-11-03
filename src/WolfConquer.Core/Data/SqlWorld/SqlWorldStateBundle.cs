namespace WolfConquer.Core.Data.SqlWorld;

/// <summary>Complete immutable storage DATA, deliberately distinct from the v1 provider's image.</summary>
public sealed partial class SqlWorldStateBundle
{
    public const int MaximumImageBytes = SqlWorldStateImage.MaximumImageBytes;
    public const int MaximumBundleBytes = 32 * 1024 * 1024;
    private readonly SqlWorldStateSidecar[] sidecars;

    private SqlWorldStateBundle(SqlWorldStateImage core, SqlWorldStateSidecar[] sidecars, int totalImageBytes)
    { Core = core; this.sidecars = sidecars; TotalImageBytes = totalImageBytes; }

    public SqlWorldStateImage Core { get; }
    public long AccountId => Core.AccountId;
    public uint CharacterUid => Core.CharacterUid;
    public ReadOnlySpan<uint> ItemUids => Core.ItemUids;
    public ReadOnlySpan<SqlWorldStateSidecar> Sidecars => sidecars;
    public int TotalImageBytes { get; }

    public static bool TryCreate(SqlWorldStateImage? core, ReadOnlySpan<SqlWorldStateSidecarInput> sidecars,
        out SqlWorldStateBundle? bundle, out SqlWorldStateBundleFailure failure)
    {
        bundle = null;
        if (core is null) { failure = SqlWorldStateBundleFailure.InvalidCore; return false; }
        if (sidecars.Length != 4) { failure = SqlWorldStateBundleFailure.SidecarCount; return false; }

        // Capture all four descriptors without allocating an input array. Never
        // measure caller entries and then re-read their possibly changed bytes.
        var first = sidecars[0]; var second = sidecars[1];
        var third = sidecars[2]; var fourth = sidecars[3];
        long total = (long)core.CharacterBytes.Length + core.ItemBytes.Length;
        int kinds = 0;
        if (!TryMeasure(first, ref kinds, ref total, out failure) ||
            !TryMeasure(second, ref kinds, ref total, out failure) ||
            !TryMeasure(third, ref kinds, ref total, out failure) ||
            !TryMeasure(fourth, ref kinds, ref total, out failure)) return false;

        SqlWorldStateSidecarInput[] inputs = [first, second, third, fourth];
        var captured = new SqlWorldStateSidecar[inputs.Length];
        try
        {
            for (int index = 0; index < inputs.Length; index++)
            {
                failure = InvalidPayload(inputs[index].Kind);
                captured[index] = SqlWorldStateSidecar.Capture(inputs[index]);
            }
        }
        catch (Exception error) when (InvalidSidecar(error)) { return false; }

        Array.Sort(captured, static (left, right) => ((byte)left.Kind).CompareTo((byte)right.Kind));
        foreach (var sidecar in captured)
            if (!TryValidateSidecar(sidecar, out failure)) return false;
        bundle = new(core, captured, checked((int)total));
        failure = SqlWorldStateBundleFailure.None;
        return true;
    }

    private static bool TryMeasure(SqlWorldStateSidecarInput input, ref int kinds, ref long total,
        out SqlWorldStateBundleFailure failure)
    {
        failure = SqlWorldStateBundleFailure.InvalidKind;
        if (input.Kind is not (SqlWorldStateSidecarKind.Spells or SqlWorldStateSidecarKind.Proficiencies or
            SqlWorldStateSidecarKind.Quests or SqlWorldStateSidecarKind.CombatGear)) return false;
        int mask = 1 << ((byte)input.Kind - 1);
        if ((kinds & mask) != 0) { failure = SqlWorldStateBundleFailure.DuplicateKind; return false; }
        kinds |= mask;
        if (input.Presence is not (SqlWorldStateSidecarPresence.Missing or SqlWorldStateSidecarPresence.Present))
        { failure = SqlWorldStateBundleFailure.InvalidPresence; return false; }
        if (input.Presence == SqlWorldStateSidecarPresence.Missing ? input.Bytes.Length != 0 : input.Bytes.Length == 0)
        { failure = SqlWorldStateBundleFailure.PresenceBytes; return false; }
        if (input.Bytes.Length > MaximumImageBytes)
        { failure = SqlWorldStateBundleFailure.ImageBounds; return false; }
        total += input.Bytes.Length;
        if (total > MaximumBundleBytes)
        { failure = SqlWorldStateBundleFailure.BundleBounds; return false; }
        failure = SqlWorldStateBundleFailure.None;
        return true;
    }
}
