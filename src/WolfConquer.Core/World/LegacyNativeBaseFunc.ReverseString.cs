namespace WolfConquer.Core.World;

public static partial class LegacyNativeBaseFunc
{
public static string ReverseString(string text)
	{
		char[] array = text.ToCharArray();
		string text2 = "";
		for (int num = array.Length - 1; num > -1; num--)
		{
			text2 += array[num];
		}
		return text2;
	}
}
