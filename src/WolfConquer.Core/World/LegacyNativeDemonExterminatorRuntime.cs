using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Native storage/cache and the non-send item-identity methods only.
// Scalar line persistence is literal; no quest creation, reward, save/login or constructor caller is installed.
internal class LegacyNativeDemonExterminatorRuntime
{
	public uint ItemUID = 0u;

	public byte FinishToday = 0;

	public ushort HuntKills = 0;

	public uint QuestTyp = 0u;

	public bool OnQuest = false;

	private LegacyNativeGameItemRuntime? Jar;

	public virtual void UppdateJar(LegacyNativeGameClientRuntime user, uint MonsterID)
	{
		if (ItemUID == 0)
		{
			return;
		}
		if (Jar == null)
		{
			user.Inventory!.TryGetItem(ItemUID, out Jar);
		}
		if (Jar != null && Jar.Uid != ItemUID)
		{
			user.Inventory!.TryGetItem(ItemUID, out Jar);
		}
		if (Jar != null)
		{
			if (CheckUpKill(Jar.MaximDurability, MonsterID))
			{
				HuntKills++;
			}
		}
		else
		{
			ItemUID = 0u;
		}
	}

	public bool CheckUpKill(ushort itemtype, uint monstertype)
	{
		switch (itemtype)
		{
		case 58:
			if (monstertype == 8219 || monstertype == 8319 || monstertype == 8119 || monstertype == 83)
			{
				return true;
			}
			break;
		case 57:
			if (monstertype == 8218 || monstertype == 8318 || monstertype == 8118 || monstertype == 82)
			{
				return true;
			}
			break;
		case 56:
			if (monstertype == 8217 || monstertype == 8317 || monstertype == 8117 || monstertype == 81)
			{
				return true;
			}
			break;
		case 55:
			if (monstertype == 84 || monstertype == 77 || monstertype == 79)
			{
				return true;
			}
			break;
		case 14:
		case 15:
		case 16:
		case 17:
		case 18:
		case 19:
		case 20:
			if (monstertype == 72 + (itemtype - 13) || monstertype == 8208 + (itemtype - 13) || monstertype == 8308 + (itemtype - 13) || monstertype == 8108 + (itemtype - 13))
			{
				return true;
			}
			break;
		case 13:
			if (monstertype == 72 || monstertype == 8208 || monstertype == 8308 || monstertype == 8108)
			{
				return true;
			}
			break;
		case 12:
			if (monstertype == 71 || monstertype == 8207 || monstertype == 8307 || monstertype == 8107)
			{
				return true;
			}
			break;
		case 11:
			if (monstertype == 70 || monstertype == 8206 || monstertype == 8306 || monstertype == 8106)
			{
				return true;
			}
			break;
		case 10:
			if (monstertype == 69 || monstertype == 8205 || monstertype == 8305 || monstertype == 8105)
			{
				return true;
			}
			break;
		case 8:
			if (monstertype == 67 || monstertype == 8203 || monstertype == 8103 || monstertype == 8303)
			{
				return true;
			}
			break;
		case 7:
			if (monstertype == 8102 || monstertype == 66)
			{
				return true;
			}
			break;
		case 6:
			if (monstertype == 65 || monstertype == 8101 || monstertype == 8301 || monstertype == 8201)
			{
				return true;
			}
			break;
		case 5:
			if (monstertype == 64 || monstertype == 25)
			{
				return true;
			}
			break;
		}
		return itemtype == monstertype;
	}
	public byte GetDemonExterminatorStage(LegacyNativeGameItemRuntime? Jar)
	{
		if (Jar != null)
		{
			if (Jar.Durability == 600)
			{
				return 0;
			}
			if (Jar.Durability == 1200)
			{
				return 1;
			}
			if (Jar.Durability == 1500)
			{
				return 2;
			}
			if (Jar.Durability == 1800)
			{
				return 3;
			}
			return byte.MaxValue;
		}
		return byte.MaxValue;
	}

	public override string ToString()
	{
		LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
		writeLine.Add(ItemUID).Add(FinishToday).Add(HuntKills)
			.Add(QuestTyp);
		return writeLine.Close();
	}

	public void ReadLine(string? Line)
	{
		LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(Line, '/');
		ItemUID = readLine.Read(0u);
		FinishToday = readLine.Read((byte)0);
		HuntKills = readLine.Read((ushort)0);
		QuestTyp = readLine.Read(0u);
	}
}
