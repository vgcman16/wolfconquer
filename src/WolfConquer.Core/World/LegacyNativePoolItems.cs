namespace WolfConquer.Core.World;

// Native Pool item prerequisites only; no session allocator or host publication.
public static class LegacyNativePoolItems
{
    // Native Pool.cs:86: one shared mutable reference, born at 1 before Server.Initialize.
    public static LegacyNativeCounter ItemCounter = new(1u);

    // Native Pool.cs:88 and94: distinct initially-null replaceable owners, not lazy factories.
    internal static LegacyNativeRifinery? RifineryItems = null;

    internal static LegacyNativeRefinaryBoxes? DBRerinaryBoxes = null;

    // Native Pool.ItemsBase is initially null and remains a replaceable reference.
    // Real native ItemType owner; no Pool publication or startup is wired here.
    internal static LegacyNativeItemTypeRuneMetadata? ItemsBase = null;

    // Call only at the native-equivalent host initialization phase, with the uint
    // already read from shell.ini Database/ItemUID. No read/default/lazy fallback here.
    // No target host invokes this prerequisite yet; unrelated startup order is NOT proven.
    public static void InitializeFromShellItemUid(uint persistedItemUid)
    {
        ItemCounter.Set(persistedItemUid);
        _ = ItemCounter.Next;
    }

    // Native Server.SaveDatabase writes Count, not Next, to Database/ItemUID.
    // The actual shell.ini writer and save phase are NOT integrated by this component.
    public static uint CaptureItemUidForShellSave() => ItemCounter.Count;
}
