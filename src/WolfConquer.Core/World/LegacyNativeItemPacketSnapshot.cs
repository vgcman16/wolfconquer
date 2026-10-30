using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

// A wire image of one actual item; never a replacement domain item/owner.
// Public-field rechecks are bounded witnesses, not a shared mutation transaction.
internal sealed class LegacyNativeItemPacketSnapshot
{
    internal LegacyNativeGameItemRuntime Source { get; }
    private readonly LegacyNativeRelicAttribute[] sourceRelics;
    private readonly LegacyNativeRelicAttribute[] relics;
    internal uint Uid { get; }
    internal uint ITEM_ID { get; }
    internal ushort Durability { get; }
    internal ushort MaximDurability { get; }
    internal Flags.ItemMode Mode { get; }
    internal ushort Position { get; }
    internal uint SocketProgress { get; }
    internal Flags.Gem SocketOne { get; }
    internal Flags.Gem SocketTwo { get; }
    internal uint SpellID { get; }
    internal Flags.ItemEffect Effect { get; }
    internal byte Plus { get; }
    internal byte Bless { get; }
    internal byte Bound { get; }
    internal byte Enchant { get; }
    internal uint ProgresGreen { get; }
    internal byte Suspicious { get; }
    internal byte Locked { get; }
    internal uint PlusProgress { get; }
    internal uint Activate { get; }
    internal uint TimeLeftInMinutes { get; }
    internal DateTime EndDate { get; }
    internal ushort StackSize { get; }
    internal uint PerfectionLevel { get; }
    internal uint PerfectionProgress { get; }
    internal uint OwnerUID { get; }
    internal string OwnerName { get; }
    internal string Signature { get; }
    internal uint RuneEXP { get; }
    internal uint AnimaItemID { get; }
    internal uint MythSoul { get; }
    internal uint MythSoulPoints { get; }
    internal uint WH_ID { get; }
    internal int UnLockTimer { get; }
    internal LegacyNativeItemExtra.Refinery Refinary { get; }
    internal LegacyNativeItemExtra.Purification Purification { get; }

    internal int RelicCount => relics.Length;
    internal uint RelicWord(int index) => relics[index];

    internal LegacyNativeItemPacketSnapshot(LegacyNativeGameItemRuntime item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(item.OwnerName);
        ArgumentNullException.ThrowIfNull(item.Signature);
        ArgumentNullException.ThrowIfNull(item.RelicAttributes);
        Source = item;
        sourceRelics = item.RelicAttributes;
        relics = (LegacyNativeRelicAttribute[])sourceRelics.Clone();
        Uid = item.Uid;
        ITEM_ID = item.ITEM_ID;
        Durability = item.Durability;
        MaximDurability = item.MaximDurability;
        Mode = item.Mode;
        Position = item.Position;
        SocketProgress = item.SocketProgress;
        SocketOne = item.SocketOne;
        SocketTwo = item.SocketTwo;
        SpellID = item.SpellID;
        Effect = item.Effect;
        Plus = item.Plus;
        Bless = item.Bless;
        Bound = item.Bound;
        Enchant = item.Enchant;
        ProgresGreen = item.ProgresGreen;
        Suspicious = item.Suspicious;
        Locked = item.Locked;
        PlusProgress = item.PlusProgress;
        Activate = item.Activate;
        TimeLeftInMinutes = item.TimeLeftInMinutes;
        EndDate = item.EndDate;
        StackSize = item.StackSize;
        PerfectionLevel = item.PerfectionLevel;
        PerfectionProgress = item.PerfectionProgress;
        OwnerUID = item.OwnerUID;
        OwnerName = item.OwnerName;
        Signature = item.Signature;
        RuneEXP = item.RuneEXP;
        AnimaItemID = item.AnimaItemID;
        MythSoul = item.MythSoul;
        MythSoulPoints = item.MythSoulPoints;
        WH_ID = item.WH_ID;
        UnLockTimer = item.UnLockTimer;
        Refinary = item.Refinary;
        Purification = item.Purification;
        if (!IsCurrent()) throw new InvalidOperationException("Item changed while capturing its wire image.");
    }

    internal bool IsCurrent() =>
        IdentityAndPlacementAreCurrent() && SocketAndSpellFieldsAreCurrent() &&
        QualityFieldsAreCurrent() && TimeAndPerfectionFieldsAreCurrent() &&
        TextAndOwnerFieldsAreCurrent() && WarehouseAndExtraFieldsAreCurrent() &&
        ReferenceEquals(Source.RelicAttributes, sourceRelics) && sourceRelics.SequenceEqual(relics);

    private bool IdentityAndPlacementAreCurrent() =>
        Source.Uid == Uid &&
        Source.ITEM_ID == ITEM_ID &&
        Source.Durability == Durability &&
        Source.MaximDurability == MaximDurability &&
        Source.Mode == Mode &&
        Source.Position == Position;

    private bool SocketAndSpellFieldsAreCurrent() =>
        Source.SocketProgress == SocketProgress &&
        Source.SocketOne == SocketOne &&
        Source.SocketTwo == SocketTwo &&
        Source.SpellID == SpellID &&
        Source.Effect == Effect &&
        Source.Plus == Plus;

    private bool QualityFieldsAreCurrent() =>
        Source.Bless == Bless &&
        Source.Bound == Bound &&
        Source.Enchant == Enchant &&
        Source.ProgresGreen == ProgresGreen &&
        Source.Suspicious == Suspicious &&
        Source.Locked == Locked;

    private bool TimeAndPerfectionFieldsAreCurrent() =>
        Source.PlusProgress == PlusProgress &&
        Source.Activate == Activate &&
        Source.TimeLeftInMinutes == TimeLeftInMinutes &&
        Source.EndDate == EndDate &&
        Source.StackSize == StackSize &&
        Source.PerfectionLevel == PerfectionLevel;

    private bool TextAndOwnerFieldsAreCurrent() =>
        Source.PerfectionProgress == PerfectionProgress &&
        Source.OwnerUID == OwnerUID &&
        Source.OwnerName == OwnerName &&
        Source.Signature == Signature &&
        Source.RuneEXP == RuneEXP &&
        Source.AnimaItemID == AnimaItemID;

    private bool WarehouseAndExtraFieldsAreCurrent() =>
        Source.MythSoul == MythSoul &&
        Source.MythSoulPoints == MythSoulPoints &&
        Source.WH_ID == WH_ID &&
        Source.UnLockTimer == UnLockTimer &&
        Source.Refinary.Equals(Refinary) &&
        Source.Purification.Equals(Purification);
}
