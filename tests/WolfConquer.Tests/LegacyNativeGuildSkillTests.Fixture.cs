using System.Globalization;
using GuildSkill = WolfConquer.Core.World.LegacyNativeGuildSkillRuntime;

namespace WolfConquer.Tests;

// Complete native record/storage and explicit component birth slot only.
// No ServerDatabase profile-save/load caller or full host birth is installed.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeGuildSkillTests
{
    private sealed class Scope : IDisposable
    {
        private readonly CultureInfo culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo ui = CultureInfo.CurrentUICulture;
        internal GuildSkill Storage { get; } = new();

        internal Scope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        internal GuildSkill.Skill Row(uint key, uint id, uint level)
        {
            var row = new GuildSkill.Skill { ID = id, Level = level };
            Assert.True(Storage.Skills.TryAdd(key, row));
            return row;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = ui;
        }
    }

    private static void RowIs(GuildSkill.Skill row, uint id, uint level)
    {
        Assert.Equal(id, row.ID);
        Assert.Equal(level, row.Level);
    }
}
