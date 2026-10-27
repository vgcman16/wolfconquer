namespace WolfConquer.Core.World;

// Complete native AtributesStatus table state and stat-query/reset methods.
// Class/Level getter prestates remain actual Player slots; no setter or host adoption is added.
internal partial class LegacyNativeAtributesStatus : Dictionary<uint, Dictionary<ushort, LegacyNativeAtributesStatus.Instant>>
{
    public class Instant
    {
        public ushort Strenght = 0;
        public ushort Vitality = 0;
        public ushort Agility = 0;
        public ushort Spirit;
    }

    private uint PositionArray(uint Class)
    {
        if (IsTrojan(Class))
        {
            return 1005u;
        }
        if (IsWarrior(Class))
        {
            return 2005u;
        }
        if (IsArcher(Class))
        {
            return 4005u;
        }
        if (IsNinja(Class))
        {
            return 5005u;
        }
        if (IsMonk(Class))
        {
            return 6005u;
        }
        if (IsPirate(Class))
        {
            return 7005u;
        }
        if (IsLee(Class))
        {
            return 8005u;
        }
        if (IsThunderStriker(Class))
        {
            return 9005u;
        }
        if (IsTaoist(Class))
        {
            return 10000u;
        }
        if (IsWindWalker(Class))
        {
            return 16005u;
        }
        return 0u;
    }

    public void ResetStatsNonReborn(LegacyNativePlayerRuntime player)
    {
        Dictionary<ushort, Instant> dictionary = base[PositionArray(player.Class)];
        Instant instant = dictionary[(byte)Math.Min(120, (int)player.Level)];
        player.Strength = instant.Strenght;
        player.Vitality = instant.Vitality;
        player.Spirit = instant.Spirit;
        player.Agility = instant.Agility;
    }

    public bool CheckStatus(LegacyNativePlayerRuntime player)
    {
        Dictionary<ushort, Instant> dictionary = base[PositionArray(player.Class)];
        Instant instant = dictionary[(byte)Math.Min(120, (int)player.Level)];
        return player.Strength >= instant.Strenght && player.Agility >= instant.Agility &&
            player.Spirit >= instant.Spirit && player.Vitality >= instant.Vitality;
    }

    public string InfoStr(byte Class, byte Level)
    {
        Dictionary<ushort, Instant> dictionary = base[PositionArray(Class)];
        Instant instant = dictionary[(byte)Math.Min(120, (int)Level)];
        return "You need to have Strenght : " + instant.Strenght + ", Agility : " + instant.Agility +
            ", Vitality: " + instant.Vitality + ", Spirit: " + instant.Spirit;
    }

    public void GetStatus(LegacyNativePlayerRuntime player, bool Atreborn = false)
    {
        if (Atreborn || (player.Level <= 120 && player.Reborn <= 0))
        {
            Dictionary<ushort, Instant> dictionary = base[PositionArray(player.Class)];
            Instant instant = dictionary[player.Level];
            player.Strength = instant.Strenght;
            player.Agility = instant.Agility;
            player.Spirit = instant.Spirit;
            player.Vitality = instant.Vitality;
        }
    }
}
