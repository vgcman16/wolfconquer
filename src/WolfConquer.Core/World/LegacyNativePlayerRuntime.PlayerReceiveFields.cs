namespace WolfConquer.Core.World;

// Native physical slots only; this does not admit any shortened receive/HP/event path.
public sealed partial class LegacyNativePlayerRuntime
{
    // Player399/401/405/407/409: implicit zero-valued Time32 fields.
    internal LegacyNativeTime32 CapriceStamp;
    internal LegacyNativeTime32 BloodTideStamp;
    internal LegacyNativeTime32 DuelEndStamp;
    internal LegacyNativeTime32 TidalWaveStamp;
    internal LegacyNativeTime32 QuenchStamp;
    public uint LightningShieldLeft; //431
    public uint SkillTournamentLifes = 0u; //653
    public uint DragonWarHits = 0u; //793
    public uint DragonWarScore = 0u; //795
    public bool EagleEyeCountDown = false; //1027
}
