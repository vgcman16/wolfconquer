namespace WolfConquer.Core.World;

// Complete native Poker.Kick implicit null/default storage owner.
internal class LegacyNativePokerKick
{
    public DateTime Time;
    public List<uint>? Accept;
    public List<uint>? Refuse;
    public byte Total;
    public byte TotalRefuse;
    public uint Target;
    public uint Starter;
}
