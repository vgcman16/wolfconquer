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
        if (Class >= 1000 && Class <= 1051)
        {
            return 1005u;
        }
        if (Class >= 2000 && Class <= 2051)
        {
            return 2005u;
        }
        if (Class >= 4000 && Class <= 4051)
        {
            return 4005u;
        }
        if (Class >= 5000 && Class <= 5051)
        {
            return 5005u;
        }
        if (Class >= 6000 && Class <= 6051)
        {
            return 6005u;
        }
        if (Class >= 7000 && Class <= 7051)
        {
            return 7005u;
        }
        if (Class >= 8000 && Class <= 8051)
        {
            return 8005u;
        }
        if (Class >= 9000 && Class <= 9051)
        {
            return 9005u;
        }
        if (Class >= 10000 && Class <= 14051)
        {
            return 10000u;
        }
        if (Class >= 16000 && Class <= 16051)
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
