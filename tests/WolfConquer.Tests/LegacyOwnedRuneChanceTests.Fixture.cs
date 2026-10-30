using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedRuneChanceTests
{
    private static async Task Run(Func<ChanceFixture, Task> body, uint experience = 7500,
        ushort stack = 1, string mode = "active", string fault = "")
    {
        var fixture = new ChanceFixture(); var errors = new List<Exception>();
        try { await fixture.InitializeAsync(experience, stack, mode, fault); await body(fixture); }
        catch (Exception error) { errors.Add(error); }
        try { await fixture.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { fixture.RestoreMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { fixture.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    // Saved item/XP inputs are declared before bootstrap. Every positive origin,
    // chance outcome and journal comes from actual admitted Frame requests.
    private sealed partial class ChanceFixture
    {
        internal const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
        internal const uint Target = 1_025_001, Kept = Target + 1, TargetType = 4_020_517;
        internal const string Marker = "ManagedRuneSpellOrigins";
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly List<byte[]> Sent = [];
        internal readonly StringWriter Log = new();
        internal WorldDataCatalog Catalog = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal byte[] Ledger = [], OriginalSpells = [];
        internal Action<byte[]>? BeforeSend;
        private readonly List<LegacyOwnedAdmissionRuntime> owners = [];
        private Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute>? priorBeasts;
        private ConquerTcpConnection? connection;
        private CancellationTokenSource? lifetime;
        private Exception? observerFailure;
        internal string CharacterPath => Data.UserPath;
        internal string ItemsPath => Path.Combine(Data.Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string SpellsPath => Path.Combine(Data.Spells, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string PendingPath => Path.Combine(Data.Users, ".managed-trade-journal", "pending.json");
        internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Handler));
        internal WorldSession Current
        {
            get { Assert.True(Store.TryGet(connection!, out var current), "Owned Chance admission/session unavailable:\n" + Log);
                return Assert.IsType<WorldSession>(current); }
        }
        internal LegacyNativeGameClientRuntime Native => Current.NativeGameClient!;
        internal ConquerTcpConnection Connection => connection!;
        internal string? Origins => Current.Character.Attributes.GetValueOrDefault(Marker);
        internal LegacyRuneRankingState Rankings => Assert.IsType<LegacyRuneRankingState>(typeof(WorldServerFrameHandler)
            .GetField("_runeRankings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Handler));

        internal async Task InitializeAsync(uint experience, ushort stack, string mode, string fault)
        {
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out var encoding)
                .Replace("Class=7051\r\n", "Class=6000\r\n", StringComparison.Ordinal)
                .Replace("MinHitPoints=330\r\n", "MinHitPoints=1\r\n", StringComparison.Ordinal)
                .Replace("KeepBytes=startup-owned-value\r\n", "KeepBytes=startup-owned-value\r\nTailedBeastActivated=true\r\nTailedBeastTotalPoints=0\r\n", StringComparison.Ordinal);
            File.WriteAllBytes(CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            var rune = Item(Target, mode == "maximum" ? 4_020_527U : TargetType, 212)
                .WithRuneExperience(experience).WithStackSize(stack);
            if (mode == "stored-restricted") rune = rune.WithLocked(1).WithBound(1);
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Uid,
                new([Item(Kept, 1_000_000, 0)], [], [], [], [rune]));
            OriginalSpells = PlayerSpellSnapshotLoader.ToLegacyBytes([new(6001, 4, 1111, 2, 3, 2)]);
            OriginalSpells[15] = 0xA5; File.WriteAllBytes(SpellsPath, OriginalSpells);
            var beastFields = Enumerable.Repeat("0", 19).ToArray(); beastFields[0] = "1"; beastFields[1] = "1000"; beastFields[^1] = "";
            var beast = BeastParser.ParseRow("owned-chance-beast", 1, string.Join("@@", beastFields));
            priorBeasts = LegacyNativeBeastsAtrribute.Attributes; LegacyNativeBeastsAtrribute.Attributes = [];
            LegacyNativeBeastsCatalogStartup.AddRows([beast]);
            var metadata = new List<ItemType> { Metadata(TargetType, 1416016), Metadata(TargetType + 1, 1416017),
                Metadata(4_020_527, 1416026), Metadata(1_000_000, 0, 0, 1) };
            if (fault == "missing-successor") metadata.RemoveAll(row => row.Id == TargetType + 1);
            if (fault == "malformed-successor") ((string[])metadata.Single(row => row.Id == TargetType + 1).Fields)[ItemType.RuneSpellIdFieldIndex] = "invalid";
            var progress = new List<RuneLevelExperience>
            { Progress("18@@402@@17@@94000@@"), Progress("19@@402@@18@@109000@@") };
            if (fault == "missing-current-progress") progress.RemoveAt(0);
            if (fault == "missing-next-progress") progress.RemoveAt(1);
            if (fault == "nonpositive-progress") progress[1] = Progress("19@@402@@18@@94000@@");
            if (fault == "malformed-progress") ((string[])progress[1].Fields)[3] = "invalid";
            Catalog = NativeCallerWorldDataFixture.Create(metadata.ToArray(), [], beastAttributes: new BeastAttribute[] { beast },
                magicEffects: MagicEffectParser.Parse("[1416000]\r\nIsRuneSkill=1\r\n", "owned-chance-MagicEffect.ini"),
                magic: Enumerable.Range(0, 27).Select(Magic).ToArray(), runeLevelExperience: progress.ToArray());
            Data.Bootstrap(); await ReopenFrameAsync(); Ledger = File.ReadAllBytes(Data.State);
            Assert.True(Native.Beasts!.Activated); Assert.Equal((byte)1, Native.Beasts.Level);
            if (mode is not ("stored" or "stored-restricted"))
                await DispatchAsync(new(Target, 102, MsgItemUsageAction.EquipRune, 112233, 44, 55, 66));
            Assert.Equal(mode is "stored" or "stored-restricted" ? (ushort)212 : (ushort)102,
                Assert.Single(Current.PlayerItems.RuneItems, row => row.Uid == Target).Position);
            Assert.Equal(experience, Assert.Single(Current.PlayerItems.RuneItems, row => row.Uid == Target).RuneExperience);
            AssertConverged(); Sent.Clear();
        }

        private static ItemType Metadata(uint id, uint encoded, uint requiredClass = 6000, ushort stack = 0) =>
            OwnedEquipmentBehaviorFixture.Item(id, "OwnedChance" + id, new Dictionary<int, string>
            { [ItemType.DurabilityFieldIndex] = "10", [ItemType.RequiredClassFieldIndex] = requiredClass.ToString(CultureInfo.InvariantCulture),
              [ItemType.RuneSpellIdFieldIndex] = encoded.ToString(CultureInfo.InvariantCulture), [ItemType.StackSizeFieldIndex] = stack.ToString(CultureInfo.InvariantCulture) });
        private static RuneLevelExperience Progress(string row) => RuneParser.ParseLevelExperienceRow("owned-exact-rune_levexp.txt", 1, row);
        private static MagicType Magic(int level)
        {
            var fields = Enumerable.Repeat("0", 53).ToArray(); fields[0] = (1_416_000 + level).ToString(CultureInfo.InvariantCulture);
            fields[1] = "14160"; fields[2] = "6"; fields[3] = "IronShield"; fields[8] = level.ToString(CultureInfo.InvariantCulture);
            fields[14] = "1"; fields[15] = "18"; fields[18] = "100000"; return new(1_416_000 + level, 14160, fields[3], fields);
        }
        internal static PlayerItemSnapshotItem Item(uint uid, uint type, ushort position) => new()
        { Uid = uid, ItemId = type, Position = position, StackSize = 1, Durability = 10, MaximumDurability = 10, Color = 5 };
        internal static MsgItemUsagePacket Request(uint uid = Target) => new(uid, 998877, MsgItemUsageAction.ChanceRune, 511043495, 112244, 55, 66);
    }
}
