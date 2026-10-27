namespace WolfConquer.Core.World;

internal partial class LegacyNativeGuildRuntime
{
    public bool Profession(uint profession)
    {
        // Existing families continue through suffix 57 in the target primary.
        // This checks recruitment permission only; it does not publish membership.
        var required = profession switch
        {
            >= 1000 and <= 1057 => ClassFlag.Trojan,
            >= 2000 and <= 2057 => ClassFlag.Warrior,
            >= 4000 and <= 4057 => ClassFlag.Archer,
            >= 5000 and <= 5057 => ClassFlag.Ninja,
            >= 6000 and <= 6057 => ClassFlag.Monk,
            >= 7000 and <= 7057 => ClassFlag.Pirate,
            >= 8000 and <= 8057 => ClassFlag.DragonWarrior,
            >= 9000 and <= 9057 => ClassFlag.Thunderstriker,
            >= 13002 and <= 13057 => ClassFlag.Water,
            >= 14002 and <= 14057 => ClassFlag.Fire,
            >= 16000 and <= 16057 => ClassFlag.Windwalker,
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
