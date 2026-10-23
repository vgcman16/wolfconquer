using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeStatusPacket
{
    // Whole native MsgBuilder.StatusCreate2742..2788; no host route or projection.
	public static LegacyNativePacket StatusCreate(this LegacyNativePacket stream, LegacyNativeGameClientRuntime user)
	{
		LegacyNativeMsgStatus status = user.Status;
		stream.InitWriter();
		stream.Write(status.UID);
		stream.Write(status.MaxHitpoints);
		stream.Write(status.MaxMana);
		stream.Write(status.MaxAttack);
		stream.Write(status.MinAttack);
		stream.Write(status.Defence);
		stream.Write(status.MagicAttack);
		stream.Write(status.MagicDefence);
		stream.Write(status.Dodge);
		stream.Write(status.AgilityAtack);
		stream.Write(status.Accuracy);
		stream.Write(status.PhysicalPercent);
		stream.Write(status.MagicPercent);
		stream.Write(status.MDefence);
		stream.Write(status.Damage);
		stream.Write(status.ItemBless);
		stream.Write(status.CriticalStrike);
		stream.Write(status.SkillCStrike);
		stream.Write(status.Immunity);
		stream.Write(status.Penetration);
		stream.Write(status.Block);
		stream.Write(status.Breakthrough);
		stream.Write(status.Counteraction);
		stream.Write(status.Detoxication);
		stream.Write(status.PhysicalDamageIncrease);
		stream.Write(status.MagicDamageIncrease);
		stream.Write(status.PhysicalDamageDecrease);
		stream.Write(status.MagicDamageDecrease);
		stream.Write(status.MetalResistance);
		stream.Write(status.WoodResistance);
		stream.Write(status.WaterResistance);
		stream.Write(status.FireResistance);
		stream.Write(status.EarthResistance);
		stream.Write(status.PrestigeLevel);
		stream.Write(status.RuneScore);
		stream.Write(status.DashRate);
		stream.Write(NativeStatusWireUInt32(user.PerfectionStatus!.LuckyStrike * 100.0));
		stream.Write(NativeStatusWireUInt32(user.PerfectionStatus!.StrikeLock * 100.0));
		stream.Write(0u);
		stream.Write(status.DodgeRate);
		stream.Finalize(2301);
		return stream;
	}

    // Scoped to the two literal PerfectionStatus*100.0 source cast sites above.
    // The actual modern writer is tested against source-shaped Framework/x86
    // default/optimized observations; this is not a general cast replacement.
    private static uint NativeStatusWireUInt32(double value)
    {
        if (double.IsNaN(value) || value < -9223372036854775808.0 || value >= 9223372036854775808.0) return 0;
        return unchecked((uint)(long)value);
    }
}
