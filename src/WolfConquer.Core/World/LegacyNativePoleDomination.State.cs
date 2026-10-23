using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Entire native MsgPoleDomination physical state14..39 and constructor41..52.
internal partial class LegacyNativePoleDomination
{
    public class PoleDomination
    {
        public uint GuildID;
        public string? Name;
        public uint Score;
        public int LeaderReward = 1;
    }

    public bool SendInvitation = false;
    public List<uint> RewardLeader = new List<uint>();
    public DateTime StampRound = default(DateTime);
    public DateTime StampShuffleScore = default(DateTime);
    public ConcurrentDictionary<uint, PoleDomination> ScoreList;
    public PoleDomination Winner;
    public LegacyNativeProcesType Proces { get; set; }
    public Dictionary<LegacyNativeSobNpcRuntime.StaticMesh, LegacyNativeSobNpcRuntime> Furnitures { get; set; }

    public LegacyNativePoleDomination()
    {
        Proces = LegacyNativeProcesType.Dead;
        Furnitures = new Dictionary<LegacyNativeSobNpcRuntime.StaticMesh, LegacyNativeSobNpcRuntime>();
        ScoreList = new ConcurrentDictionary<uint, PoleDomination>();
        Winner = new PoleDomination
        {
            Name = "None",
            Score = 100u,
            GuildID = 0u
        };
    }
}
