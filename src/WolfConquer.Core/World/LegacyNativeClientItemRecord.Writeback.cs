namespace WolfConquer.Core.World;

internal partial struct LegacyNativeClientItemRecord
{
    // Native ClientItems.DBItem.GetDBItem177..227 mutates THIS reused record.
    // Do not replace it with a fresh record: test3/test4 and padding are untouched.
    // This conversion alone does not install a file/SQL writer or saved-item owner.
    internal LegacyNativeClientItemRecord GetDBItem(LegacyNativeGameItemRuntime item)
    {
        UID = item.Uid;
        ITEM_ID = item.ITEM_ID;
        Durability = item.Durability;
        MaximDurability = item.MaximDurability;
        Position = item.Position;
        SocketProgress = item.SocketProgress;
        SocketOne = item.SocketOne;
        SocketTwo = item.SocketTwo;
        Effect = item.Effect;
        Plus = item.Plus;
        Bless = item.Bless;
        Bound = item.Bound;
        Enchant = item.Enchant;
        Suspicious = item.Suspicious;
        Locked = item.Locked;
        PlusProgress = item.PlusProgress;
        Inscribed = item.Inscribed;
        Activate = item.Activate;
        TimeLeftInMinutes = item.TimeLeftInMinutes;
        StackSize = item.StackSize;
        WH_ID = item.WH_ID;
        Color = item.Color;
        ItemPoints = item.ItemPoints;
        PurificationItemID = item.Purification.PurificationItemID;
        PurificationAddedOn = item.Purification.AddedOn.Ticks;
        PurificationDuration = item.Purification.PurificationDuration;
        PurificationLevel = item.Purification.PurificationLevel;
        EffectAddedOn = item.Refinary.AddedOn.Ticks;
        EffectDuration = item.Refinary.EffectDuration;
        EffectID = item.Refinary.EffectID;
        EffectLevel = item.Refinary.EffectLevel;
        EffectPercent = item.Refinary.EffectPercent;
        EffectPercent2 = item.Refinary.EffectPercent2;
        DepositeCount = (uint)item.Deposite.Count;
        UnLockTimer = item.UnLockTimer;
        RemainingTime = item.RemainingTime;
        AnimaItemID = item.AnimaItemID;
        RuneEXP = item.RuneEXP;
        Attribute1 = item.RelicAttributes[0];
        Attribute2 = item.RelicAttributes[1];
        Attribute3 = item.RelicAttributes[2];
        Attribute4 = item.RelicAttributes[3];
        Attribute5 = item.RelicAttributes[4];
        Expiration = item.EndDate == DateTime.FromBinary(0L) ? 0 : item.EndDate.ToBinary();
        MythSoul = item.MythSoul;
        MythSoulPoints = item.MythSoulPoints;
        return this;
    }
}
