namespace WolfConquer.Core.World;

// Complete native Role.Instance.Poker.PokerCard owner, including null-faulting comparisons.
internal class LegacyNativePokerCard
{
    internal LegacyNativePokerFlags.CardsType Type = LegacyNativePokerFlags.CardsType.Heart;
    internal byte Value = 0;

    public override string ToString()
    {
        string text = (Value + 2).ToString();
        string? name = Enum.GetName(typeof(LegacyNativePokerFlags.CardsType), Type);
        if (name != null)
        {
            string text2 = name.Substring(0, 1).ToLower();
            if (text == "10") text = "t";
            if (text == "11") text = "j";
            if (text == "12") text = "q";
            if (text == "13") text = "k";
            if (text == "14") text = "a";
            return text + text2;
        }
        return "";
    }

    public static bool operator >(LegacyNativePokerCard pc1, LegacyNativePokerCard pc2)
    {
        if (pc1.Value == pc2.Value) return (int)pc1.Type > (int)pc2.Type;
        return pc1.Value > pc2.Value;
    }

    public static bool operator <(LegacyNativePokerCard pc1, LegacyNativePokerCard pc2)
    {
        if (pc1.Value == pc2.Value) return (int)pc1.Type < (int)pc2.Type;
        return pc1.Value < pc2.Value;
    }
}
