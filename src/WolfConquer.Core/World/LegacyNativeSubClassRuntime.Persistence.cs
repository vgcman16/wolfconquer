using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSubClassRuntime
{
    public override string ToString()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('#');
        writeLine.Add(StudyPoints).Add((byte)ActiveSubclass).Add(src.Count);
        foreach (LegacyNativeMsgSubClass.SubClases value in src.Values)
        {
            writeLine.Add((byte)value.ID).Add(value.Level).Add(value.Phrase);
        }
        return writeLine.Close();
    }

    public void Load(string? Base_line)
    {
        if (Base_line == "" || Base_line == null)
        {
            return;
        }
        LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(Base_line, '#');
        StudyPoints = readLine.Read((ushort)0);
        ActiveSubclass = (LegacyNativeDbLevExp.Sort)readLine.Read((byte)0);
        int num = readLine.Read(0);
        for (byte b = 0; b < num; b++)
        {
            LegacyNativeMsgSubClass.SubClases subClases = LegacyNativeMsgSubClass.SubClases.Create((LegacyNativeDbLevExp.Sort)readLine.Read((byte)0));
            subClases.Level = readLine.Read((byte)0);
            subClases.Phrase = readLine.Read((byte)0);
            if (!src.ContainsKey(subClases.ID))
            {
                src.TryAdd(subClases.ID, subClases);
            }
        }
    }
}
