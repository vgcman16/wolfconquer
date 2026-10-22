namespace WolfConquer.Core.Data;

// Complete native DBLevExp declaration; no level-table startup loader is added.
internal class LegacyNativeDbLevExp
{
    public enum Sort : byte
    {
        User = 0,
        MartialArtist = 1,
        Warlock = 2,
        ChiMaster = 3,
        Sage = 4,
        Apothecary = 5,
        Performer = 6,
        Wrangler = 9
    }

    public Sort Action;

    public byte Level;

    public ulong Experience;

    public int UpLevTime;

    public int MentorUpLevTime;
}
