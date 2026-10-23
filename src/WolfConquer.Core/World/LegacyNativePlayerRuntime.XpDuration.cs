using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    public void IncreaseXPDuration(short seconds)
    {
        if (!TryIncreaseXPDuration(seconds))
            throw new InvalidDataException("XP duration adjustment requires current ownership and a valid representable status graph.");
    }

    internal bool TryIncreaseXPDuration(short seconds)
    {
        var client = Owner;
        lock (client.SpellAdmissionSyncRoot)
        lock (SpellAdmissionSyncRoot)
        {
            var vector = BitVector;
            uint uid = UID;
            if (vector is null || !ReferenceEquals(client.Player, this)) return false;
            lock (vector.SyncRoot)
                return vector.TryAdjustFirstDuration(XpDurationPriority, (int)LegacyNativeMsgUpdate.Flags.Normal,
                    seconds, this, client, uid);
        }
    }

    // Same complete order as the real OnXPSkill. No new wire values or XP kinds.
    private static ReadOnlySpan<int> XpDurationPriority =>
    [
        (int)LegacyNativeMsgUpdate.Flags.Cyclone, (int)LegacyNativeMsgUpdate.Flags.Superman,
        (int)LegacyNativeMsgUpdate.Flags.Oblivion, (int)LegacyNativeMsgUpdate.Flags.FatalStrike,
        (int)LegacyNativeMsgUpdate.Flags.ShurikenVortex, (int)LegacyNativeMsgUpdate.Flags.ChaintBolt,
        (int)LegacyNativeMsgUpdate.Flags.BlackbeardsRage, (int)LegacyNativeMsgUpdate.Flags.CannonBarrage,
        (int)LegacyNativeMsgUpdate.Flags.BladeFlurry, (int)LegacyNativeMsgUpdate.Flags.SuperCyclone,
        (int)LegacyNativeMsgUpdate.Flags.DragonCyclone, (int)LegacyNativeMsgUpdate.Flags.Omnipotence,
        (int)LegacyNativeMsgUpdate.Flags.ManiacDance, (int)LegacyNativeMsgUpdate.Flags.ThunderRampage
    ];

public void UpdateXpSkill()
	{
		if ((UseXPSpell == LegacyNativeMsgUpdate.Flags.Cyclone || UseXPSpell == LegacyNativeMsgUpdate.Flags.Superman) && ContainFlag(UseXPSpell))
		{
			UpdateFlag(UseXPSpell, 1, SetNewTimer: false, 20);
		}
	}

internal void SendScrennXPSkill(ILegacyNativeMapObj obj)
	{
		if (OnXPSkill() != LegacyNativeMsgUpdate.Flags.Normal)
		{
			using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
			{
				LegacyNativePacket stream = recycledPacket.GetStream();
				LegacyNativeInteractQuery pQuery = new LegacyNativeInteractQuery
				{
					UID = UID,
					KilledMonster = 1,
					X = X,
					Y = Y,
					AtkType = 14,
					SpellLevel = KillCounter
				};
				obj.Send(stream.InteractionCreate(pQuery));
			}
		}
	}
}
