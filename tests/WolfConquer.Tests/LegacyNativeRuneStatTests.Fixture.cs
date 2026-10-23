using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using RuneTable = WolfConquer.Core.Data.LegacyNativeRuneTable;
using Stat = WolfConquer.Core.Data.LegacyNativeRuneTable.RuneAttribute;

namespace WolfConquer.Tests;

// Native component proof only. No equipment rebuild, Team birth, login or live client.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeRuneStatTests
{
    private sealed class Scope : IDisposable
    {
        private readonly Dictionary<uint, RuneTable.RuneLevelEXP> previousExp = RuneTable.RuneLevelEXPList;
        private readonly Dictionary<uint, RuneTable.Attribute> previousAttributes = RuneTable.Attributes;
        private readonly List<RuneTable.RandomAttribute> previousRandoms = RuneTable.RandomAttributes;
        private readonly LegacyNativeItemTypeRuneMetadata? previousItems = LegacyNativePoolItems.ItemsBase;
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime previousReset = LegacyNativePoolRandom.ResetRandom;
        private readonly CultureInfo previousCulture = CultureInfo.CurrentCulture;
        internal LegacyNativeGameClientRuntime Client { get; }
        internal LegacyNativeRuneItemStorage Rune { get; }

        internal Scope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            RuneTable.RuneLevelEXPList = new();
            RuneTable.Attributes = new();
            RuneTable.RandomAttributes = new();
            LegacyNativePoolItems.ItemsBase = new();
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(91);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            Client = new LegacyNativeGameClientRuntime();
            Client.Equipment = new LegacyNativeEquipItemStorage(Client);
            Rune = new LegacyNativeRuneItemStorage(Client);
            Client.Rune = Rune;
        }

        internal LegacyNativeGameItemRuntime Add(uint uid, uint id, ushort position)
        {
            var item = Item(uid, id, position);
            Assert.True(Rune.Add(item));
            return item;
        }

        public void Dispose()
        {
            RuneTable.RuneLevelEXPList = previousExp;
            RuneTable.Attributes = previousAttributes;
            RuneTable.RandomAttributes = previousRandoms;
            LegacyNativePoolItems.ItemsBase = previousItems;
            LegacyNativePoolRandom.GetRandom = previousRandom;
            LegacyNativePoolRandom.ResetRandom = previousReset;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private sealed class Files : IDisposable
    {
        private readonly string previousLocation = LegacyNativeServerConfig.DbLocation;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-rune-native-" + Guid.NewGuid().ToString("N"));
        internal string Location => Root + Path.DirectorySeparatorChar;

        internal Files()
        {
            Directory.CreateDirectory(Root);
            LegacyNativeServerConfig.DbLocation = Location;
        }

        internal void Write(string name, string text) => File.WriteAllText(Path.Combine(Root, name), text,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = previousLocation;
            string exact = Path.GetFullPath(Root);
            string prefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar + "wolf-rune-native-";
            if (!exact.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Owned Rune fixture root escaped temporary directory.");
            if (Directory.Exists(exact)) Directory.Delete(exact, recursive: true);
        }
    }

    private static LegacyNativeGameItemRuntime Item(uint uid, uint id, ushort position = 211)
    {
        if (LegacyNativeItemTypeRuneMetadata.IsRune(id))
            LegacyNativePoolItems.ItemsBase![id] = new LegacyNativeItemRuneMetadata { ID = id, RuneSpellID = 1432000, Name = "Rune fixture" };
        return new LegacyNativeGameItemRuntime { Uid = uid, ITEM_ID = id, Position = position };
    }

    private static RuneTable.Attribute Attribute(uint score, params (Stat Type, uint Value)[] values) => new()
    {
        Score = score,
        AttributeValue = values.Select(value => new RuneTable.AttributeValue { Type = value.Type, Value = value.Value }).ToArray(),
    };

    private static RuneTable.RuneLevelEXP Exp(uint type, byte level, uint progress) => new()
    {
        ItemType = type, Level = level, RequiredProgress = progress,
    };
}
