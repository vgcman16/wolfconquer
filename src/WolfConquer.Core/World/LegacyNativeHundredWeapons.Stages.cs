#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHundredWeapons
{
    public bool StageActivated(byte Stage)
    {
        return Stage switch
        {
            1 => TryGetItem(1) != null,
            2 => Objects.Values.Count((LegacyNativeHundredWeaponInfo i) => i.BitVector.Contain(2) || i.BitVector.Contain(3) || i.BitVector.Contain(4)) >= 3,
            3 => Objects.Values.Count((LegacyNativeHundredWeaponInfo i) => i.BitVector.Contain(5) || i.BitVector.Contain(6) || i.BitVector.Contain(7) || i.BitVector.Contain(8) || i.BitVector.Contain(9)) >= 5,
            _ => false,
        };
    }

    public ushort GetNextSpell()
    {
        ushort result = 0;
        if (TODO_Spells.Count > 0)
        {
            result = TODO_Spells.FirstOrDefault();
            TODO_Spells.RemoveAt(0);
        }
        return result;
    }

    public bool CanTrigger(byte Stage)
    {
        return LegacyNativeTime32.Now >= TriggerStamp[Stage].AddSeconds(TriggerSeconds[Stage]);
    }

    public bool Trigger(byte Stage)
    {
        if (CanTrigger(Stage))
        {
            TriggerSeconds[Stage] = GetWeaponRate(Stage).FirstOrDefault().Value;
            TriggerStamp[Stage] = LegacyNativeTime32.Now;
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream = recycledPacket.GetStream();
            Owner.Send(stream.CreateHundredWeaponsOpt(new LegacyNativeMsgHundredWeaponsOpt.MsgHundredWeaponsOptProto
            {
                Type = LegacyNativeMsgHundredWeaponsOpt.ActionID.Trigger,
                TriggeredStage = Stage,
                TriggeringDuration = TriggerSeconds[Stage]
            }));
        }
        return false;
    }
}
