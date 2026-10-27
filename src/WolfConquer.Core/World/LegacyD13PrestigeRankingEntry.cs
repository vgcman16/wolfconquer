namespace WolfConquer.Core.World;

/// <summary>
/// Raw VisibleDx8D13 action-6 row. Opaque bytes require explicit caller input;
/// this type does not obtain missing values from a prestige catalog.
/// </summary>
public sealed class LegacyD13PrestigeRankingEntry
{
    public const int FixedNameLength = 32;
    public const int OpaqueTailLength = 16;

    private readonly byte[] _nameBytes;
    private readonly byte[] _secondaryNameBytes;
    private readonly byte[] _opaqueTailBytes;

    public LegacyD13PrestigeRankingEntry(
        byte rank,
        long amount,
        uint categoryCode,
        uint characterUid,
        ReadOnlySpan<byte> nameBytes,
        ReadOnlySpan<byte> secondaryNameBytes,
        uint level,
        uint @class,
        uint mesh,
        ReadOnlySpan<byte> opaqueTailBytes,
        uint opaqueValue)
    {
        RequireWidth(nameBytes, FixedNameLength, nameof(nameBytes));
        RequireWidth(secondaryNameBytes, FixedNameLength, nameof(secondaryNameBytes));
        RequireWidth(opaqueTailBytes, OpaqueTailLength, nameof(opaqueTailBytes));

        Rank = rank;
        Amount = amount;
        CategoryCode = categoryCode;
        CharacterUid = characterUid;
        _nameBytes = nameBytes.ToArray();
        _secondaryNameBytes = secondaryNameBytes.ToArray();
        Level = level;
        Class = @class;
        Mesh = mesh;
        _opaqueTailBytes = opaqueTailBytes.ToArray();
        OpaqueValue = opaqueValue;
    }

    public byte Rank { get; }
    public long Amount { get; }
    public uint CategoryCode { get; }
    public uint CharacterUid { get; }
    public ReadOnlySpan<byte> NameBytes => _nameBytes;
    public ReadOnlySpan<byte> SecondaryNameBytes => _secondaryNameBytes;
    public uint Level { get; }
    public uint Class { get; }
    public uint Mesh { get; }
    public ReadOnlySpan<byte> OpaqueTailBytes => _opaqueTailBytes;
    public uint OpaqueValue { get; }

    private static void RequireWidth(ReadOnlySpan<byte> bytes, int width, string name)
    {
        if (bytes.Length != width)
        {
            throw new ArgumentException($"Raw prestige field must contain exactly {width} bytes.", name);
        }
    }
}
