using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Full native PokerTable14..88 storage,119 auto-property and121..140 constructor.
// Other game methods are absent, not represented by successful no-op implementations.
internal partial class LegacyNativePokerTableRuntime
{
    public LegacyNativePokerKick? Kick;
    public List<uint> ToSend = new List<uint>();
    public uint BigBlind;
    internal LegacyNativePokerCard[] Board = new LegacyNativePokerCard[5];
    internal List<LegacyNativePokerCard> Cards = null!;
    public uint CurrentPlayer;
    public uint Dealer;
    public object dr;
    public ushort MapId = 1858;
    public ConcurrentDictionary<uint, uint> OnScreen = null!;
    public ConcurrentDictionary<uint, LegacyNativePokerPlayer> Players;
    private Dictionary<uint, ulong> TempPlayers;
    public ConcurrentDictionary<uint, LegacyNativePokerPlayer> Watchers;
    public LegacyNativePokerPlayer? PreviousPlayer;
    public ulong RequiredPot;
    public ulong RoundPot;
    public uint SmallBlind;
    public bool TableIsChange;
    public object TableSyncRoot;
    public LegacyNativePokerFlags.TableType TableType;
    public DateTime ThreadTime;
    public DateTime Time;
    public byte RoundState;
    public LegacyNativePokerFlags.TableState PreviousState;
    public byte NumberOfRaise = 0;
    public bool Showhand = false;
    public long ShowhandTotalPot = 0L;
    public bool TableBusy = false;
    public uint Id;
    public ushort X;
    public ushort Y;
    internal uint Mesh;
    public uint Number;
    public bool UnLimited;
    public bool IsCPs;
    public uint MinBet;
    public LegacyNativePokerFlags.TableState State;
    public ulong TotalPot;
    public int ShowHand { get; set; }

    public LegacyNativePokerTableRuntime(uint id)
    {
        TableSyncRoot = new object();
        Id = id;
        Players = new ConcurrentDictionary<uint, LegacyNativePokerPlayer>();
        Watchers = new ConcurrentDictionary<uint, LegacyNativePokerPlayer>();
        TempPlayers = new Dictionary<uint, ulong>();
        RoundState = 0;
        Kick = null;
        PreviousState = LegacyNativePokerFlags.TableState.Unopened;
        RequiredPot = 0uL;
        NumberOfRaise = 0;
        TotalPot = 0uL;
        RoundPot = 0uL;
        CurrentPlayer = 0u;
        PreviousPlayer = null;
        Board = new LegacyNativePokerCard[5];
        ReloadCards();
        dr = new object();
    }

    // Native267..269 is genuinely empty in the reference. Do not invent a deck here.
    private void ReloadCards()
    {
    }
}
