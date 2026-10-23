namespace WolfConquer.Core.World;

// Complete native Role.Instance.Poker.Player owner. No host poker admission is installed.
internal class LegacyNativePokerPlayer
{
    public bool IsPlaying;
    public bool IsPotAllin;
    public bool PotinThisRound;
    public bool Fold;
    public string Name;
    public LegacyNativePokerFlags.PlayerType PlayerType;
    internal LegacyNativePokerCard[] Pocket = null!;
    public long _CurrentMoney;
    public long Lose;
    public ulong RoundPot;
    public byte Seat;
    public LegacyNativePokerTableRuntime Table = null!;
    public ulong TotalPot;
    public uint Uid;
    public ulong TempMoney;

    public long CurrentMoney
    {
        get { return _CurrentMoney; }
        set
        {
            if (value < 0) value = 0L;
            _CurrentMoney = value;
        }
    }

    public LegacyNativePokerPlayer(string name, uint uid)
    {
        Name = name;
        Uid = uid;
    }

    public void Create(LegacyNativePokerFlags.PlayerType pType, byte seat, LegacyNativePokerTableRuntime table, ulong money)
    {
        PotinThisRound = false;
        IsPlaying = false;
        IsPotAllin = false;
        Fold = false;
        PlayerType = pType;
        TotalPot = 0uL;
        RoundPot = 0uL;
        Seat = seat;
        Lose = 0L;
        CurrentMoney = (long)money;
        TempMoney = 0uL;
        Table = table;
        if (Table.TableType == LegacyNativePokerFlags.TableType.ShowHand) Pocket = new LegacyNativePokerCard[5];
        else Pocket = new LegacyNativePokerCard[2];
    }

    public void Increment(ulong money)
    {
        CurrentMoney += (long)money;
    }

    public void Decrement(ulong money)
    {
        CurrentMoney -= (long)money;
        RoundPot += money;
        TotalPot += money;
        Table.TotalPot += money;
        Table.RoundPot += money;
    }

    public override string ToString()
    {
        string text = "";
        LegacyNativePokerCard[] pocket = Pocket;
        LegacyNativePokerCard[] array = pocket;
        foreach (LegacyNativePokerCard pokerCard in array)
        {
            if (pokerCard != null) text = string.IsNullOrEmpty(text) ? pokerCard.ToString() : text + " " + pokerCard;
        }
        return text;
    }
}
