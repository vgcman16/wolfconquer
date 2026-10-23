namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
	internal LegacyNativeMsgUpdate.Flags OnXPSkill()
	{
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.Cyclone))
		{
			return LegacyNativeMsgUpdate.Flags.Cyclone;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.Superman))
		{
			return LegacyNativeMsgUpdate.Flags.Superman;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.Oblivion))
		{
			return LegacyNativeMsgUpdate.Flags.Oblivion;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.FatalStrike))
		{
			return LegacyNativeMsgUpdate.Flags.FatalStrike;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.ShurikenVortex))
		{
			return LegacyNativeMsgUpdate.Flags.ShurikenVortex;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.ChaintBolt))
		{
			return LegacyNativeMsgUpdate.Flags.ChaintBolt;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.BlackbeardsRage))
		{
			return LegacyNativeMsgUpdate.Flags.BlackbeardsRage;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.CannonBarrage))
		{
			return LegacyNativeMsgUpdate.Flags.CannonBarrage;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.BladeFlurry))
		{
			return LegacyNativeMsgUpdate.Flags.BladeFlurry;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.SuperCyclone))
		{
			return LegacyNativeMsgUpdate.Flags.SuperCyclone;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.DragonCyclone))
		{
			return LegacyNativeMsgUpdate.Flags.DragonCyclone;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.Omnipotence))
		{
			return LegacyNativeMsgUpdate.Flags.Omnipotence;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.ManiacDance))
		{
			return LegacyNativeMsgUpdate.Flags.ManiacDance;
		}
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.ThunderRampage))
		{
			return LegacyNativeMsgUpdate.Flags.ThunderRampage;
		}
		return LegacyNativeMsgUpdate.Flags.Normal;
	}

    // Native Player1207 physical stamp; no active guard/proc is fabricated at birth.
    public DateTime LastDivineGuard = default(DateTime);
}
