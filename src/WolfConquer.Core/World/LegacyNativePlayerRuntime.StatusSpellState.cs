using System;

namespace WolfConquer.Core.World;

// Exact native Player status-spell state; existing/incoming owners retain their other fields.
public sealed partial class LegacyNativePlayerRuntime
{
    // Player.cs 353,369,371,413,415,419,425,439,441,495,1011,1277,2854.
    public byte DragonSwingChance;
    public uint SongofPhoenixPower;
    public uint HawksAmbitionPower;
    public uint IronShieldDamage;
    public uint defFineRainPower;
    public uint FuryStrikeHP;
    public uint RiseofTaoismExtraMDamage;
    public byte FuryStrikeAttack;
    public byte DuelPercent;
    public int RevengeTailChange = 0;
    public DateTime IntensifyStamp = default(DateTime);
    public uint Venom;
    public uint BonePulse { get; set; }
}
