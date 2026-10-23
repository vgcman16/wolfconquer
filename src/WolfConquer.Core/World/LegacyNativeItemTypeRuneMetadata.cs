namespace WolfConquer.Core.World;

// Exact Dictionary/default-null owner shape needed by native ITEM_ID's Rune branch.
// Complete native DBItem storage, Parse and Loading component; live startup and Class/job policy remain unwired.
// No parser adapter, fallback0 lookup, custom indexer, callback or host registration.
internal sealed partial class LegacyNativeItemTypeRuneMetadata : Dictionary<uint, LegacyNativeItemRuneMetadata>
{
    public static Dictionary<ushort, Dictionary<uint, LegacyNativeItemRuneMetadata>> PurificationItems = new Dictionary<ushort, Dictionary<uint, LegacyNativeItemRuneMetadata>>();

    public static Dictionary<uint, LegacyNativeItemRuneMetadata> Accessorys = new Dictionary<uint, LegacyNativeItemRuneMetadata>();

    public static Dictionary<uint, Dictionary<uint, LegacyNativeRifinery.Item>> Refinary = new Dictionary<uint, Dictionary<uint, LegacyNativeRifinery.Item>>();

    public static Dictionary<uint, LegacyNativeItemRuneMetadata> Garments = new Dictionary<uint, LegacyNativeItemRuneMetadata>();

    public static Dictionary<uint, LegacyNativeItemRuneMetadata> SteedMounts = new Dictionary<uint, LegacyNativeItemRuneMetadata>();

    // Entire native ItemType328..333, following the five caches in source order.
    // This field is genuinely replaceable/mutable, not the host's fixed predicate.
    public static List<uint> unabletradeitem = new List<uint>
    {
        750000u, 722700u, 3306885u, 3307142u, 3600025u, 3200000u, 729549u, 3008994u, 3200005u, 3600029u,
        3007108u, 3007109u, 3007110u, 3200004u, 3600031u, 3600027u, 3600024u
    };

	private static ushort[] purifyStabilizationPoints = new ushort[6] { 10, 30, 60, 100, 150, 200 };

	private static ushort[] refineryStabilizationPoints = new ushort[5] { 10, 30, 70, 150, 270 };

	private static ushort[] StonePoints = new ushort[9] { 1, 10, 40, 120, 360, 1080, 3240, 9720, 29160 };

	private static ushort[] ComposePoints = new ushort[13]
	{
		20, 20, 80, 240, 720, 2160, 6480, 19440, 58320, 2700,
		5500, 9000, 0
	};

	public static ushort PurifyStabilizationPoints(byte plevel)
	{
		return purifyStabilizationPoints[Math.Min(plevel - 1, 5)];
	}

	public static ushort RefineryStabilizationPoints(byte elevel)
	{
		return refineryStabilizationPoints[Math.Min(elevel - 1, 4)];
	}

	public string? GetItemName(uint ID)
	{
		if (LegacyNativePoolItems.ItemsBase!.TryGetValue(ID, out var value))
		{
			return value.Name;
		}
		return "";
	}

	public static uint ComposePlusPoints(byte plus)
	{
		return ComposePoints[Math.Min(plus, (byte)12)];
	}

	public static uint StonePlusPoints(byte plus)
	{
		return StonePoints[Math.Min((int)plus, 8)];
	}

    internal static bool IsRune(uint itemid)
    {
        if (itemid / 10000 >= 401 && itemid / 10000 <= 403)
        {
            return true;
        }
        return false;
    }

	public static uint GetBaseID(uint ID)
	{
		int num = (int)(ID / 1000);
		if (ID == 300000)
		{
			return ID;
		}
		byte b = (byte)(ID / 10000);
		byte b2 = b;
		if ((uint)b2 <= 42u)
		{
			if ((uint)(b2 - 11) <= 5u || b2 == 20 || b2 == 42)
			{
				goto IL_005e;
			}
		}
		else
		{
			if ((uint)(b2 - 60) <= 2u || (uint)(b2 - 67) <= 1u)
			{
				ID -= ID % 10;
				goto IL_00bb;
			}
			if (b2 == 90)
			{
				goto IL_005e;
			}
		}
		goto IL_0082;
		IL_00bb:
		return ID;
		IL_005e:
		if (num == 420)
		{
			goto IL_0082;
		}
		ID -= ID % 10;
		goto IL_00bb;
		IL_0082:
		byte b3 = (byte)(ID / 100000);
		ID = (uint)(b3 * 100000 + b3 * 10000 + b3 * 1000 + (ID % 1000 - ID % 10));
		goto IL_00bb;
	}

	public void Loading()
	{
		Dictionary<uint, LegacyNativeItemTypePlus[]>? dictionary = new Dictionary<uint, LegacyNativeItemTypePlus[]>();
		string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "ItemAdd.ini");
		LegacyNativeItemAddTable.PopulateRows(dictionary, array);
		string[] array4 = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "itemtype.txt");
		string[] array5 = array4;
		foreach (string text2 in array5)
		{
			string text3 = text2.Trim();
			if (text3.Length <= 11 || text3.IndexOf("//", 0, 2) == 0)
			{
				continue;
			}
			LegacyNativeItemRuneMetadata dBItem = new LegacyNativeItemRuneMetadata();
			dBItem.Parse(text2);
			if (dictionary.ContainsKey(GetBaseID(dBItem.ID)) || dictionary.ContainsKey(GetBaseID(dBItem.ID) + 10) || dictionary.ContainsKey(GetBaseID(dBItem.ID) + 20))
			{
				dBItem.AllowUpgradePlus = true;
				if (!dictionary.TryGetValue(GetBaseID(dBItem.ID), out dBItem.Plus) && !dictionary.TryGetValue(GetBaseID(dBItem.ID) + 10, out dBItem.Plus) && !dictionary.TryGetValue(GetBaseID(dBItem.ID) + 20, out dBItem.Plus))
				{
					int num = LegacyNativeItemPosition.ItemPosition(dBItem.ID);
					if (num < 6)
					{
						Console.WriteLine("Eroror item " + dBItem.ID + " " + dBItem.Name);
					}
				}
			}
			if (!ContainsKey(dBItem.ID))
			{
				Add(dBItem.ID, dBItem);
			}
		}
		array4 = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "ItemtypeSub.txt");
		string[] array6 = array4;
		foreach (string text4 in array6)
		{
			string text5 = text4.Trim();
			if (text5.Length <= 11 || text5.IndexOf("//", 0, 2) == 0)
			{
				continue;
			}
			LegacyNativeItemRuneMetadata dBItem2 = new LegacyNativeItemRuneMetadata();
			dBItem2.Parse(text4);
			if (dictionary.ContainsKey(GetBaseID(dBItem2.ID)) || dictionary.ContainsKey(GetBaseID(dBItem2.ID) + 10) || dictionary.ContainsKey(GetBaseID(dBItem2.ID) + 20))
			{
				dBItem2.AllowUpgradePlus = true;
				if (!dictionary.TryGetValue(GetBaseID(dBItem2.ID), out dBItem2.Plus) && !dictionary.TryGetValue(GetBaseID(dBItem2.ID) + 10, out dBItem2.Plus) && !dictionary.TryGetValue(GetBaseID(dBItem2.ID) + 20, out dBItem2.Plus))
				{
					int num2 = LegacyNativeItemPosition.ItemPosition(dBItem2.ID);
					if (num2 < 6)
					{
						Console.WriteLine("Eroror item " + dBItem2.ID + " " + dBItem2.Name);
					}
				}
			}
			if (!ContainsKey(dBItem2.ID))
			{
				Add(dBItem2.ID, dBItem2);
			}
		}
		dictionary = null;
		GC.Collect();
	}
}

internal class LegacyNativeItemRuneMetadata
{
	public enum ItemType : byte
	{
		Dropable,
		Others
	}

	public uint ID;

	public LegacyNativeItemTypePlus[]? Plus;

	public bool AllowUpgradePlus = false;

	public string? Name;

	public uint Class;

	public byte Proficiency;

	public byte Level;

	public byte Gender;

	public ushort Strength;

	public ushort Agility;

	public uint GoldWorth;

	public ushort MinAttack;

	public ushort MaxAttack;

	public ushort PhysicalDefence;

	public ushort PhysicalDamageIncrease;

	public ushort PhysicalDamageDecrease;

	public ushort MagicDefence;

	public ushort MagicAttack;

	public ushort MagicDamageIncrease;

	public ushort MagicDamageDecrease;

	public uint RuneSpellID;

	public uint Sort;

	public byte BonusAttribute;

	public byte Dodge;

	public ushort Frequency;

	public uint ConquerPointsWorth;

	public ushort Durability;

	public ushort StackSize;

	public ushort ItemHP;

	public ushort ItemMP;

	public ushort AttackRange;

	public ItemType Type;

	public string? Description;

	public int GradeKey;

	public uint Crytical;

	public uint SCrytical;

	public uint Imunity;

	public uint Penetration;

	public uint Block;

	public uint BreackTrough;

	public uint ConterAction;

	public uint Detoxication;

	public uint MetalResistance = 0u;

	public uint WoodResistance = 0u;

	public uint WaterResistance = 0u;

	public uint FireResistance = 0u;

	public uint EarthResistance = 0u;

	public ushort PurificationLevel;

	public ushort PurificationMeteorNeed;

	public uint Time;

	public void Parse(string Line)
	{
		Plus = new LegacyNativeItemTypePlus[13];
		string[] array = Line.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
		if (array[2] == "")
		{
			for (int i = 2; i < array.Length - 1; i++)
			{
				array[i] = array[i + 1];
			}
		}
		try
		{
			if (array.Length <= 52 || !(array[0] != "\0"))
			{
				return;
			}
			ID = Convert.ToUInt32(array[0]);
			Name = array[1].Trim();
			if (string.IsNullOrEmpty(Name) || Name == "0")
			{
				return;
			}
			Class = Convert.ToUInt32(array[2]);
			Proficiency = Convert.ToByte(array[3]);
			Level = Convert.ToByte(array[4]);
			Gender = Convert.ToByte(array[5]);
			Strength = Convert.ToUInt16(array[6]);
			Agility = Convert.ToUInt16(array[7]);
			Type = ((Convert.ToUInt32(array[10]) != 0) ? ItemType.Others : ItemType.Dropable);
			GoldWorth = Convert.ToUInt32(array[11]);
			MaxAttack = Convert.ToUInt16(array[13]);
			MinAttack = Convert.ToUInt16(array[14]);
			PhysicalDefence = Convert.ToUInt16(array[15]);
			Frequency = Convert.ToUInt16(array[16]);
			Dodge = Convert.ToByte(array[17]);
			ItemHP = Convert.ToUInt16(array[18]);
			ItemMP = Convert.ToUInt16(array[19]);
			Durability = Convert.ToUInt16(array[21]);
			MagicAttack = Convert.ToUInt16(array[29]);
			MagicDefence = Convert.ToUInt16(array[30]);
			AttackRange = Convert.ToUInt16(array[31]);
			ConquerPointsWorth = Convert.ToUInt32(array[36]);
			Time = Convert.ToUInt32(array[38]);
			Crytical = Convert.ToUInt32(array[39]);
			SCrytical = Convert.ToUInt32(array[40]);
			Imunity = Convert.ToUInt32(array[41]);
			Penetration = Convert.ToUInt32(array[42]);
			Block = Convert.ToUInt32(array[43]);
			BreackTrough = Convert.ToUInt32(array[44]);
			ConterAction = Convert.ToUInt32(array[45]);
			MetalResistance = Convert.ToUInt16(array[47]);
			WoodResistance = Convert.ToUInt16(array[48]);
			WaterResistance = Convert.ToUInt16(array[49]);
			FireResistance = Convert.ToUInt16(array[50]);
			EarthResistance = Convert.ToUInt32(array[51]);
			ushort.TryParse(array[46].ToString(), out StackSize);
			Description = array[52].Replace("`s", "");
			if (Description == "NinjaKatana")
			{
				Description = "NinjaWeapon";
			}
			if (Description == "Earrings")
			{
				Description = "Earring";
			}
			if (Description == "Bow")
			{
				Description = "ArcherBow";
			}
			if (Description == "Backsword")
			{
				Description = "TaoistBackSword";
			}
			Description = Description.ToLower();
			if (ID >= 730001 && ID <= 730009)
			{
				Name = Name + "(+" + ID % 10 + ")";
			}
			if (array.Length >= 56)
			{
				ushort.TryParse(array[55].ToString(), out PurificationLevel);
				ushort.TryParse(array[56].ToString(), out PurificationMeteorNeed);
				if (PurificationLevel != 0 && ID != 729305 && ID != 727465)
				{
					if (!LegacyNativeItemTypeRuneMetadata.PurificationItems.ContainsKey(PurificationLevel))
					{
						LegacyNativeItemTypeRuneMetadata.PurificationItems.Add(PurificationLevel, new Dictionary<uint, LegacyNativeItemRuneMetadata>());
					}
					if (!LegacyNativeItemTypeRuneMetadata.PurificationItems[PurificationLevel].ContainsKey(ID))
					{
						LegacyNativeItemTypeRuneMetadata.PurificationItems[PurificationLevel].Add(ID, this);
					}
				}
			}
			if (LegacyNativeItemPosition.ItemPosition(ID) == 16 || LegacyNativeItemPosition.ItemPosition(ID) == 15)
			{
				if (!LegacyNativeItemTypeRuneMetadata.Accessorys.ContainsKey(ID))
				{
					LegacyNativeItemTypeRuneMetadata.Accessorys.Add(ID, this);
				}
				StackSize = 1;
			}
			if (LegacyNativeItemPosition.ItemPosition(ID) == 17 && !LegacyNativeItemTypeRuneMetadata.SteedMounts.ContainsKey(ID))
			{
				LegacyNativeItemTypeRuneMetadata.SteedMounts.Add(ID, this);
			}
			if (LegacyNativeItemPosition.ItemPosition(ID) == 9 && !LegacyNativeItemTypeRuneMetadata.Garments.ContainsKey(ID))
			{
				LegacyNativeItemTypeRuneMetadata.Garments.Add(ID, this);
			}
			if (ID == 754099)
			{
				ConquerPointsWorth = 299u;
			}
			if (ID == 754999 || ID == 753999 || ID == 751999)
			{
				ConquerPointsWorth = 1699u;
			}
			if (ID == 619028)
			{
				ConquerPointsWorth = 489u;
			}
			if (ID == 723723)
			{
				ConquerPointsWorth = 7100u;
			}
			if (ID == 3005945)
			{
				ConquerPointsWorth = 3100u;
			}
			RuneSpellID = Convert.ToUInt32(array[25]);
			BonusAttribute = (byte)((array.Length > 54) ? Convert.ToByte(array[54]) : 0);
			Sort = Convert.ToUInt32(array[58]);
			PhysicalDamageIncrease = (ushort)((array.Length > 60) ? Convert.ToUInt16(array[60]) : 0);
			MagicDamageIncrease = (ushort)((array.Length > 61) ? Convert.ToUInt16(array[61]) : 0);
			PhysicalDamageDecrease = (ushort)((array.Length > 62) ? Convert.ToUInt16(array[62]) : 0);
			MagicDamageDecrease = (ushort)((array.Length > 63) ? Convert.ToUInt16(array[63]) : 0);
		}
		catch (Exception ex)
		{
			WolfConquer.Core.Runtime.LegacyNativeConsole.WriteLine(ex.ToString());
		}
	}
}
