namespace WolfConquer.Core.Data.SqlWorld;

/// <summary>Owned complete storage images; identities and validation grant no authenticated or native custody.</summary>
public sealed partial class SqlWorldStateImage
{
    public const int MaximumImageBytes = 16 * 1024 * 1024;
    private readonly byte[] characterImage;
    private readonly byte[] itemImage;
    private readonly uint[] itemUids;

    private SqlWorldStateImage(long accountId, uint characterUid, byte[] characterImage, byte[] itemImage, uint[] itemUids)
    { AccountId = accountId; CharacterUid = characterUid; this.characterImage = characterImage; this.itemImage = itemImage; this.itemUids = itemUids; }

    public long AccountId { get; }
    public uint CharacterUid { get; }
    public ReadOnlySpan<uint> ItemUids => itemUids;
    internal ReadOnlySpan<byte> CharacterBytes => characterImage;
    internal ReadOnlySpan<byte> ItemBytes => itemImage;
    public byte[] CopyCharacterBytes() => characterImage.ToArray();
    public byte[] CopyItemBytes() => itemImage.ToArray();

    public static bool TryCreate(long accountId, uint characterUid, ReadOnlySpan<byte> characterImage,
        ReadOnlySpan<byte> itemImage, out SqlWorldStateImage? image, out SqlWorldStateFailure failure)
    {
        image = null;
        if (accountId <= 0) { failure = SqlWorldStateFailure.InvalidAccountId; return false; }
        if (characterUid == 0) { failure = SqlWorldStateFailure.InvalidCharacterUid; return false; }
        if (characterImage.Length is <= 0 or > MaximumImageBytes || itemImage.Length is <= 0 or > MaximumImageBytes)
        { failure = SqlWorldStateFailure.ImageBounds; return false; }
        var character = characterImage.ToArray();
        var items = itemImage.ToArray();
        if (!TryValidateCharacter(character, characterUid, out failure) || !TryValidateItems(items, out var uids, out failure)) return false;
        image = new(accountId, characterUid, character, items, uids);
        failure = SqlWorldStateFailure.None;
        return true;
    }
}
