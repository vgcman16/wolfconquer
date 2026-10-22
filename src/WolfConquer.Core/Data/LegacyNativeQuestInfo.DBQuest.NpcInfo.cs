namespace WolfConquer.Core.Data;

internal partial class LegacyNativeQuestInfo
{
    public partial class DBQuest
    {
        public class NpcInfo
        {
            public uint ID;

            public ushort Map;

            public ushort X;

            public ushort Y;

            public string Name = null!;

            public string MapName = null!;

            public NpcInfo(string line)
            {
                if (line == "")
                {
                    return;
                }
                try
                {
                    string[] array = line.Split(',');
                    ID = uint.Parse(array[0]);
                    Map = ushort.Parse(array[1]);
                    X = ushort.Parse(array[2]);
                    Y = ushort.Parse(array[3]);
                    Name = array[4];
                    MapName = array[5];
                }
                catch
                {
                }
            }
        }
    }
}
