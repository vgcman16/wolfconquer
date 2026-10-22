using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native title metadata row, mutable dictionary and file loader.
// CheckUpUser is intentionally absent; no partial qualification evaluator or
// automatic host/startup metadata-loading caller is installed here.
internal class LegacyNativeTitleStorage
{
    public uint ID = 0u;

    public uint SubID = 0u;

    public string Name = "";

    public uint Score = 0u;

    public static Dictionary<uint, LegacyNativeTitleStorage> Titles =
        new Dictionary<uint, LegacyNativeTitleStorage>();

    public static void LoadDBInformation()
    {
        string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "title_type.txt");
        string[] array2 = array;
        foreach (string text in array2)
        {
            string[] array3 = text.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
            LegacyNativeTitleStorage titleStorage = new LegacyNativeTitleStorage();
            titleStorage.ID = uint.Parse(array3[0]);
            titleStorage.SubID = uint.Parse(array3[1]);
            titleStorage.Name = array3[2];
            titleStorage.Score = uint.Parse(array3[7]);
            Titles.Add(titleStorage.ID, titleStorage);
        }
    }
}
