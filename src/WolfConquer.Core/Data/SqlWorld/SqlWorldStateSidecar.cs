namespace WolfConquer.Core.Data.SqlWorld;

public enum SqlWorldStateSidecarKind : byte
{
    Spells = 1, Proficiencies = 2, Quests = 3, CombatGear = 4,
}

public enum SqlWorldStateSidecarPresence : byte { Missing = 0, Present = 1 }

public readonly record struct SqlWorldStateSidecarInput(SqlWorldStateSidecarKind Kind,
    SqlWorldStateSidecarPresence Presence, ReadOnlyMemory<byte> Bytes);

public enum SqlWorldStateBundleFailure
{
    None, InvalidCore, SidecarCount, InvalidKind, DuplicateKind, InvalidPresence,
    PresenceBytes, ImageBounds, BundleBounds, InvalidSpells, InvalidProficiencies,
    InvalidQuests, InvalidCombatGear,
}

/// <summary>Captured storage bytes and explicit presence; not a native owner or publication receipt.</summary>
public sealed class SqlWorldStateSidecar
{
    private readonly byte[] bytes;

    private SqlWorldStateSidecar(SqlWorldStateSidecarKind kind, SqlWorldStateSidecarPresence presence, byte[] bytes)
    { Kind = kind; Presence = presence; this.bytes = bytes; }

    public SqlWorldStateSidecarKind Kind { get; }
    public SqlWorldStateSidecarPresence Presence { get; }
    public int ByteCount => bytes.Length;
    public byte[] CopyBytes() => bytes.ToArray();
    internal ReadOnlySpan<byte> Bytes => bytes;

    // Always takes a private copy; even assembly-internal callers cannot keep a
    // mutable alias to this sidecar's storage. The bundle owns shape validation.
    internal static SqlWorldStateSidecar Capture(SqlWorldStateSidecarInput input) =>
        new(input.Kind, input.Presence, input.Bytes.Span.ToArray());
}
