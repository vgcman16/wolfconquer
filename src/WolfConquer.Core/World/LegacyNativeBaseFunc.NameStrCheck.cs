using System.Text.RegularExpressions;

namespace WolfConquer.Core.World;

public static partial class LegacyNativeBaseFunc
{
public static bool NameStrCheck(string? name, bool ExceptedSize = true)
	{
		if (name == null)
		{
			return false;
		}
		if (name == "")
		{
			return false;
		}
		// Deny an invalid character anywhere, not only in the final position.
		// The existing allowed ranges/punctuation and later policy stay unchanged.
		string pattern = "[^A-Za-z0-9ء-ي*~.&.$一-龥]";
		Regex regex = new Regex(pattern);
		if (regex.IsMatch(name))
		{
			return false;
		}
		if (name.Contains('/'))
		{
			return false;
		}
		if (name.Contains('\\'))
		{
			return false;
		}
		if (name.Contains('\''))
		{
			return false;
		}
		if (name.Contains("GM") || name.Contains("PM") || name.Contains("MrRayzo") || name.Contains("Rayzo") || name.Contains("SYSTEM") || name.Contains("None") || name.Contains("Clicker") || name.Contains("SpeedHack") || name.Contains("Aimbot") || name.Contains("CheatEngine") || name.Contains("Hack") || name.Contains("Engine") || name.Contains("Cheat") || name.Contains("clicker") || name.Contains("speed") || name.Contains("GM") || name.Contains("PM") || name.Contains("GMPM") || name.Contains("Moksha") || name.Contains("TQ") || name.Contains("TQGMPM") || name.Contains("TQPM") || name.Contains("TQGM") || name.Contains("guard1") || name.Contains("Guard") || name.Contains("Guard1") || name.Contains("Guard") || name.Contains("Kosamk") || name.Contains("kosamk") || name.Contains('{') || name.Contains('}') || name.Contains('[') || name.Contains(']'))
		{
			return false;
		}
		if (name.Length > 16 && ExceptedSize)
		{
			return false;
		}
		for (int i = 0; i < name.Length; i++)
		{
			if (name[i] == '\u0019')
			{
				return false;
			}
		}
		return true;
	}
}
