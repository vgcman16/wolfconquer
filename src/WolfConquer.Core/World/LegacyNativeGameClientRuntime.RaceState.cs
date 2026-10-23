namespace WolfConquer.Core.World;
public sealed partial class LegacyNativeGameClientRuntime
{
    // Exact native GameClient133/231 and complete race-state301..319.
    public byte BanCount = 0;
    public uint Vigor;
	public bool RaceDizzy;

	public bool RaceFrightened;

	public DateTime RaceExcitementStamp;

	public DateTime GuardStamp;

	public DateTime DizzyStamp;

	public DateTime FrightenStamp;

	public DateTime ExtraVigorStamp;

	public DateTime DecelerateStamp;

	public uint RaceExcitementAmount;

	public uint RaceExtraVigor;
    // Whole native469..473.
    public bool RaceGuard => Player.ContainFlag(LegacyNativeMsgUpdate.Flags.GodlyShield);
    public bool RaceDecelerated => Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Deceleration);
    public bool RaceExcitement => Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Accelerated);
}
