using System.Globalization;
using System.Reflection;
using WolfConquer.Core.World;
using WolfConquer.Core.Data;
using Table = WolfConquer.Core.Data.LegacyNativeGuildSkillTypeTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativePlayerStatCalculationTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly Dictionary<uint, Dictionary<uint, Table.SkillType>> oldRows = Table._syn_skill_type;
        private readonly string oldLocation = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture, oldUiCulture = CultureInfo.CurrentUICulture;
        private readonly TextWriter oldOutput = Console.Out;
        internal string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-stat-" + Guid.NewGuid().ToString("N"));
        internal string FilePath => Path.Combine(DirectoryPath, "syn_skill_type.txt");
        internal StringWriter Output { get; } = new(CultureInfo.InvariantCulture);
        internal LegacyNativeGameClientRuntime Client { get; }

        internal Scope()
        {
            Directory.CreateDirectory(DirectoryPath);
            LegacyNativeServerConfig.DbLocation = DirectoryPath + Path.DirectorySeparatorChar;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            Table._syn_skill_type = new();
            Console.SetOut(Output);
            Client = new LegacyNativeGameClientRuntime();
        }

        public void Dispose()
        {
            Console.SetOut(oldOutput); Output.Dispose(); Table._syn_skill_type = oldRows;
            LegacyNativeServerConfig.DbLocation = oldLocation; LegacyNativePoolRandom.GetRandom = oldRandom;
            CultureInfo.CurrentCulture = oldCulture; CultureInfo.CurrentUICulture = oldUiCulture;
            string target = Path.GetFullPath(DirectoryPath);
            string prefix = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "wolf-native-stat-");
            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing cleanup outside the owned stat fixture.");
            Directory.Delete(target, recursive: true);
        }
    }

    private static void Class(LegacyNativeGameClientRuntime client, uint value) => typeof(LegacyNativePlayerRuntime)
        .GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client.Player, value);
}
