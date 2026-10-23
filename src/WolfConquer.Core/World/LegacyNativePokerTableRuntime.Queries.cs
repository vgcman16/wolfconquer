namespace WolfConquer.Core.World;

internal partial class LegacyNativePokerTableRuntime
{
    // Whole native PokerTable90..108,142..149,151..169,190..201.
    public long LowestMoney
    {
        get
        {
            long num = 0L;
            if (!Showhand)
            {
                foreach (LegacyNativePokerPlayer value in Players.Values)
                {
                    if (value.IsPlaying && !value.Fold)
                    {
                        if (num == 0) num = value.CurrentMoney;
                        else if (value.CurrentMoney < num) num = value.CurrentMoney;
                    }
                }
            }
            else num = ShowhandTotalPot;
            return num;
        }
    }

    public bool CanInterface(uint UID)
    {
        if (OnScreen.ContainsKey(UID)) return true;
        return false;
    }

    public bool IsSeatAvailable(byte seat)
    {
        try
        {
            foreach (LegacyNativePokerPlayer value in Players.Values)
            {
                if (seat == value.Seat) return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return false;
        }
    }

    public bool InGame()
    {
        if (State == LegacyNativePokerFlags.TableState.Unopened) return false;
        if (State == LegacyNativePokerFlags.TableState.ShowDown) return false;
        return true;
    }
}
