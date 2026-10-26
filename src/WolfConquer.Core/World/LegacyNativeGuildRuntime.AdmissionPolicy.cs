namespace WolfConquer.Core.World;

internal partial class LegacyNativeGuildRuntime
{
    public bool Profession(uint profession)
    {
        // These are the genuine source class ranges. This predicate checks
        // recruitment permission only; it does not publish membership.
        var required = profession switch
        {
            >= 1000 and <= 1051 => ClassFlag.Trojan,
            >= 2000 and <= 2051 => ClassFlag.Warrior,
            >= 4000 and <= 4051 => ClassFlag.Archer,
            >= 5000 and <= 5051 => ClassFlag.Ninja,
            >= 6000 and <= 6051 => ClassFlag.Monk,
            >= 7000 and <= 7051 => ClassFlag.Pirate,
            >= 8000 and <= 8051 => ClassFlag.DragonWarrior,
            >= 9000 and <= 9051 => ClassFlag.Thunderstriker,
            >= 13002 and <= 13051 => ClassFlag.Water,
            >= 14002 and <= 14051 => ClassFlag.Fire,
            >= 16000 and <= 16051 => ClassFlag.Windwalker,
            _ => ClassFlag.None,
        };
        return ContainsFlag(required);
    }

    public bool ContainsFlag(ClassFlag flag)
    {
        var info = Info;
        var requested = (uint)flag;
        // Empty or unknown requests are not class permissions. Preserve the
        // genuine class-bit values and require every requested known bit.
        return info is not null && requested != 0 &&
            (requested & ~(uint)ClassFlag.Full) == 0 &&
            ((uint)info.Recruitment_Flag & requested) == requested;
    }

	public static bool AllowToCreate(string Name)
	{
		if (!LegacyNativeBaseFunc.NameStrCheck(Name))
		{
			return false;
		}
		foreach (KeyValuePair<uint, string> changeNameRecord in ChangeNameRecords)
		{
			if (changeNameRecord.Value == Name)
			{
				return false;
			}
		}
		foreach (LegacyNativeGuildRuntime value in GuildPoll.Values)
		{
			if (value.GuildName == Name)
			{
				return false;
			}
		}
		return true;
	}
}
