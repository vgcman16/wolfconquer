namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    // Count uses native active ranges, not worn fallback; corrupt or unrepresentable state refuses.
    public byte EquippedCount
    {
        get
        {
            var client = constructorOwner;
            if (client is null) return 0;
            lock (client.SpellAdmissionSyncRoot)
            {
                var player = client.Player;
                if (player is null) return 0;
                lock (player.SpellAdmissionSyncRoot)
                lock (StorageSyncRoot)
                {
                    if (!ReferenceEquals(client.Player, player) ||
                        !TryCaptureEquipped(out var context, out var snapshot)) return 0;
                    int count = 0;
                    ushort first = context.Alternate ? (ushort)121 : (ushort)101;
                    ushort last = context.Alternate ? (ushort)135 : (ushort)115;
                    foreach (var row in snapshot)
                    {
                        if (row.Position >= first && row.Position <= last && ++count > byte.MaxValue) return 0;
                    }
                    return IsCurrent(context) ? (byte)count : (byte)0;
                }
            }
        }
    }

    public LegacyNativeGameItemRuntime? EquippedRedRune
    {
        get
        {
            LegacyNativeGameItemRuntime[] wornObjects = WornObjects;
            foreach (LegacyNativeGameItemRuntime msgGameItem in wornObjects)
            {
                if (msgGameItem.Position == 101 || msgGameItem.Position == 121)
                {
                    return msgGameItem;
                }
            }
            return null;
        }
    }

    public LegacyNativeGameItemRuntime? EquippedBlueRune
    {
        get
        {
            LegacyNativeGameItemRuntime[] wornObjects = WornObjects;
            foreach (LegacyNativeGameItemRuntime msgGameItem in wornObjects)
            {
                if (msgGameItem.Position == 102 || msgGameItem.Position == 122)
                {
                    return msgGameItem;
                }
            }
            return null;
        }
    }
}
