using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Complete native MrRayzo.ElTabakh/GuildSkill.cs7..42. The real save/load
// callers are ServerDatabase563/859; no caller or host route is installed here.
internal class LegacyNativeGuildSkillRuntime
{
    public class Skill
    {
        public uint ID;

        public uint Level;
    }

    public ConcurrentDictionary<uint, Skill> Skills = new ConcurrentDictionary<uint, Skill>();

    public override string ToString()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
        writeLine.Add(Skills.Values.Count);
        foreach (Skill value in Skills.Values)
        {
            writeLine.Add(value.ID);
            writeLine.Add(value.Level);
        }
        return writeLine.Close();
    }

    public void Load(string? Line)
    {
        LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(Line, '/');
        int num = readLine.Read(0);
        for (int i = 0; i < num; i++)
        {
            Skill skill = new Skill();
            skill.ID = readLine.Read(0u);
            skill.Level = readLine.Read(0u);
            Skills.Add(skill.ID, skill);
        }
    }
}
