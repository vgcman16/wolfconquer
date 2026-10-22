using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Genuine native field initializers, including the thirteen-slot array.
    public ushort[] Gems = new ushort[13];
    public byte TortoiseGems = 0;
    internal LegacyNativeMsgStatus Status = new LegacyNativeMsgStatus();

    internal void AddGem(Flags.Gem gem, ushort value)
    {
        if (value == 15 || value == 10 || value == 5)
        {
            if (gem == Flags.Gem.NormalDragonGem || gem == Flags.Gem.RefinedDragonGem || gem == Flags.Gem.SuperDragonGem)
            {
                Status.PhysicalPercent += value;
            }
            else
            {
                Status.MagicPercent += value;
            }
        }
        if ((int)gem >= 71 && (int)gem <= 73)
        {
            TortoiseGems++;
        }
        if (TortoiseGems <= 8)
        {
            Gems[(byte)((int)gem / 10)] += value;
        }
    }

    internal uint GemValues(Flags.Gem gem)
    {
        return Gems[(byte)((int)gem / 10)];
    }
}
