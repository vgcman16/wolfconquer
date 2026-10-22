namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Complete native GameClient922..937. This predicate does not remove Ride;
    // the unported attack caller owns that later mutation/early-return branch.
    internal bool AllowUseSpellOnSteed(ushort Spell)
    {
        if (!Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Ride))
        {
            return true;
        }
        if (Equipment!.RidingCrop != 0)
        {
            return true;
        }
        if (Spell == 7002 || Spell == 7003 || Spell == 7001)
        {
            return true;
        }
        return false;
    }
}
