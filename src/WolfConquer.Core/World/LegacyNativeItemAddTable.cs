namespace WolfConquer.Core.World;

// Native ItemType.Loading ItemAdd row phase only (ItemType.cs1102..1116).
// The future caller allocates the real dictionary at1100 BEFORE File.ReadAllLines1101.
// No configured file read, dictionary creation/replacement or global publication occurs here.
internal static class LegacyNativeItemAddTable
{
    internal static void PopulateRows(
        Dictionary<uint, LegacyNativeItemTypePlus[]> dictionary,
        string[] array)
    {
        string[] array2 = array;
        foreach (string text in array2)
        {
            string line = text.Trim();
            LegacyNativeItemTypePlus iTPlus = new LegacyNativeItemTypePlus();
            iTPlus.Parse(line);
            if (dictionary.ContainsKey(iTPlus.ID))
            {
                dictionary[iTPlus.ID][iTPlus.Plus] = iTPlus;
                continue;
            }
            LegacyNativeItemTypePlus[] array3 = new LegacyNativeItemTypePlus[13];
            array3[iTPlus.Plus] = iTPlus;
            dictionary.Add(iTPlus.ID, array3);
        }
    }
}
