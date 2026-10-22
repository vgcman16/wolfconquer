using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Complete native Server.LoadExpInfo. Explicit component only: no startup,
// GainExpBall, UpdateLevel, equipment, mentor or team callers are adopted.
internal static class LegacyNativeServerExperience
{
    public static void LoadExpInfo()
    {
        if (File.Exists(LegacyNativeServerConfig.DbLocation + "levexp.txt"))
        {
            using (StreamReader streamReader = File.OpenText(LegacyNativeServerConfig.DbLocation + "levexp.txt"))
            {
                while (true)
                {
                    string? text = streamReader.ReadLine();
                    if (text == null)
                    {
                        break;
                    }
                    string[] array = text.Split(' ');
                    LegacyNativeDbLevExp dBLevExp = new LegacyNativeDbLevExp();
                    dBLevExp.Action = (LegacyNativeDbLevExp.Sort)byte.Parse(array[0]);
                    dBLevExp.Level = byte.Parse(array[1]);
                    dBLevExp.Experience = ulong.Parse(array[2]);
                    dBLevExp.UpLevTime = int.Parse(array[3]);
                    dBLevExp.MentorUpLevTime = int.Parse(array[4]);
                    if (!LegacyNativePoolLevelInfo.LevelInfo.ContainsKey(dBLevExp.Action))
                    {
                        LegacyNativePoolLevelInfo.LevelInfo.Add(dBLevExp.Action, new Dictionary<byte, LegacyNativeDbLevExp>());
                    }
                    LegacyNativePoolLevelInfo.LevelInfo[dBLevExp.Action].Add(dBLevExp.Level, dBLevExp);
                }
                return;
            }
        }
        GC.Collect();
    }
}
