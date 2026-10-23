namespace WolfConquer.Core.Data;

// Complete native AchievementCollection record. No SaveClient/INI caller adoption.
internal class LegacyNativeAchievementCollection
{
    public int Key;
    public uint[] Value = new uint[14];

    public override string ToString()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
        writeLine.Add(Key);
        uint[] value = Value;
        foreach (uint data in value)
        {
            writeLine.Add(data);
        }
        return writeLine.Close();
    }

    public void Load(string data)
    {
        using LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(data, '/');
        Key = readLine.Read(0);
        for (int i = 0; i < Value.Length; i++)
        {
            Value[i] = readLine.Read(0u);
        }
    }
}
