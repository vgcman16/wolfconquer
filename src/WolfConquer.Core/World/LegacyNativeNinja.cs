#nullable disable
using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Native Ninja state, constructors, DBItem, queries, scores, Levels and profile record.
// Item.UPDATE and other gameplay mutation/wire/rank methods remain explicitly absent.
// No DataCore, login/save caller or host adoption is installed.
internal partial class LegacyNativeNinja
{
    public enum ItemType : ulong
    {
        WildSigil = 100uL,
        WildSigilBurning = 101uL,
        WildSigilRapid = 102uL,
        FlameSigil = 103uL,
        FlameSigilRapid = 104uL,
        FlameSigilScorch = 105uL,
        PrisonSigil = 200uL,
        PrisonSigilFatigue = 201uL,
        PrisonSigilFutility = 202uL,
        DragonSigil = 203uL,
        DragonSigilBillow = 204uL,
        DragonSigilFading = 205uL,
        MudSigil = 300uL,
        MudSigilImpeccable = 301uL,
        MudSigilSolidity = 302uL,
        DustSigil = 303uL,
        DustSigilExtinction = 304uL,
        DustSigilStunn = 305uL,
        SickleSigil = 400uL,
        SickleSigilFlurry = 401uL,
        SickleSigilGallop = 402uL,
        WhirlSigil = 403uL,
        WhirlSigilSage = 404uL,
        WhirlSigilAura = 405uL,
        KylinSigil = 500uL,
        KylinSigilBurial = 501uL,
        KylinSigilSeal = 502uL,
        SlashSeal = 503uL,
        SlashSealFlash = 504uL,
        SlashSealProwess = 505uL,
        PaperDance = 600uL,
        HellVortex = 601uL,
        InfiniteMist = 602uL,
        HeavenSWonder = 603uL,
        BonePulse = 604uL
    }

    public enum Flags : ulong
    {
        Gate_of_Dawn = 1uL,
        Gate_of_Rest = 2uL,
        Gate_of_Life = 4uL,
        Gate_of_Pain = 8uL,
        Gate_of_Limlt = 16uL,
        Gate_of_View = 32uL,
        Gate_of_Shock = 64uL,
        Gate_of_Death = 128uL,
        Gate_of_Dawn_2 = 256uL,
        Gate_of_Rest_2 = 512uL,
        Gate_of_Life_2 = 1024uL,
        Gate_of_Pain_2 = 2048uL,
        Gate_of_Limlt_2 = 4096uL,
        Gate_of_View_2 = 8192uL,
        Gate_of_Shock_2 = 16384uL,
        Gate_of_Death_2 = 32768uL,
        Full = 65535uL
    }

    public enum UnlockMasterys : ulong
    {
        FireMastery = 1uL,
        WaterMastery = 2uL,
        EarthMastery = 4uL,
        WindMastery = 8uL,
        LightningMastery = 0x10uL
    }

    public class Item
    {
        public uint ItemID;
        public uint Level;
        public uint Position;
        public uint Exp;
        public uint UK;

        public LegacyNativeNinjaFile.NinjaSprintItem DBItem
        {
            get
            {
                LegacyNativeNinjaFile.NinjaSprintItem ninjaSprintItem = LegacyNativeNinjaFile.gouyu_type.Values
                    .Where((LegacyNativeNinjaFile.NinjaSprintItem p) => p.ItemID == ItemID && p.Level == Level).FirstOrDefault();
                if (ninjaSprintItem != null)
                {
                    return ninjaSprintItem;
                }
                return new LegacyNativeNinjaFile.NinjaSprintItem();
            }
        }
    }

    public ConcurrentDictionary<uint, Item> Items;
    public LegacyNativeGameClientRuntime user;
    public ulong Flag;
    public uint Exp;
    public uint Fire;
    public uint Water;
    public uint Earth;
    public uint Wind;
    public uint Lightning;
    public uint tmp_Fire;
    public uint tmp_Water;
    public uint tmp_Earth;
    public uint tmp_Wind;
    public uint tmp_Lightning;
    public uint FireMastery = 0u;
    public uint WaterMastery = 0u;
    public uint EarthMastery = 0u;
    public uint WindMastery = 0u;
    public uint LightningMastery = 0u;
    public uint UnlockMastery = 0u;

    public LegacyNativeNinja()
    {
        Items = new ConcurrentDictionary<uint, Item>();
    }

    public LegacyNativeNinja(LegacyNativeGameClientRuntime _user)
    {
        user = _user;
        Items = new ConcurrentDictionary<uint, Item>();
    }

    public bool TryGetValueEquip(ItemType id, out Item obj)
    {
        obj = null;
        if (!LegacyNativeAtributesStatus.IsNinja(user.Player.Class))
        {
            return false;
        }
        if (user.Equipment.Alternante)
        {
            int i;
            for (i = 9; i < 17; i++)
            {
                Item item = Items.Values.Where((Item p) => p.Position == i && p.ItemID == (uint)id).FirstOrDefault();
                if (item != null)
                {
                    obj = item;
                    continue;
                }
                item = Items.Values.Where((Item p) => p.Position == i - 8 && p.ItemID == (uint)id).FirstOrDefault();
                if (item != null)
                {
                    obj = item;
                }
            }
        }
        else
        {
            int i2;
            for (i2 = 1; i2 < 9; i2++)
            {
                Item item2 = Items.Values.Where((Item p) => p.Position == i2 && p.ItemID == (uint)id).FirstOrDefault();
                if (item2 != null)
                {
                    obj = item2;
                }
            }
        }
        if (obj != null)
        {
            return true;
        }
        return false;
    }

    public bool TryGetValue(uint id, out Item obj)
    {
        obj = Items.Values.Where((Item p) => p.ItemID == id).FirstOrDefault();
        if (obj != null)
        {
            return true;
        }
        return false;
    }
}
