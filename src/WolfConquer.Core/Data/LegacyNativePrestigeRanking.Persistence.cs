using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    public static void Save()
    {
        lock (SynRoot)
        {
            var image = PreparePrestigeSaveImage();
            var path = LegacyNativeServerConfig.DbLocation + "PrestigeRanking.txt";
            WritePrestigeSaveImage(path, image);
        }
    }
}
