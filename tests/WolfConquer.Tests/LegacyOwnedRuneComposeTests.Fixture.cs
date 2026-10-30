using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedRuneComposeTests
{
    private static async Task Run(Func<ComposeFixture, Task> body, string mode = "active", string fault = "")
    {
        var fixture = new ComposeFixture(); var errors = new List<Exception>();
        try { await fixture.InitializeAsync(mode, fault); await body(fixture); } catch (Exception error) { errors.Add(error); }
        try { await fixture.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { fixture.RestoreMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { fixture.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    // All input bytes are seeded before bootstrap. Grants and every positive
    // journal are produced by actual admitted Frame requests, never by this fixture.
    private sealed class ComposeFixture
    {
        internal const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
        internal const uint Target = 1_020_001, Material = Target + 1, Surplus = Target + 2, Invalid = Target + 3;
        internal const uint TargetType = 4_020_517, EssenceType = 4_040_001, OtherType = 4_022_001, SharedType = 4_020_518;
        internal const uint PassiveType = 4_030_102;
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
            get { Assert.True(Store.TryGet(connection!, out var current), "Owned Compose admission/session unavailable:\n" + Log);
                return Assert.IsType<WorldSession>(current); }
        }
        internal LegacyNativeGameClientRuntime Native => Current.NativeGameClient!;
        internal ConquerTcpConnection Connection => connection!;
        internal string? Origins => Current.Character.Attributes.GetValueOrDefault(Marker);

        internal async Task InitializeAsync(string mode, string fault)
        {
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out var encoding)
                .Replace("Class=7051\r\n", "Class=6000\r\n", StringComparison.Ordinal)
                .Replace("MinHitPoints=330\r\n", "MinHitPoints=1\r\n", StringComparison.Ordinal)
                .Replace("KeepBytes=startup-owned-value\r\n", "KeepBytes=startup-owned-value\r\nTailedBeastActivated=true\r\nTailedBeastTotalPoints=0\r\n", StringComparison.Ordinal);
            File.WriteAllBytes(CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            var target = Item(Target, mode == "passive" ? PassiveType : TargetType, 212);
            if (mode == "stored-zero") target = target.WithStackSize(0);
            if (fault == "stacked-target") target = target.WithStackSize(2);
            if (fault == "overflow") target = target.WithRuneExperience(uint.MaxValue);
            var material = Item(Material, mode is "provider" or "permanent" or "rune-material" or "rune-material-zero" ? OtherType : mode == "shared" ? SharedType : EssenceType,
                mode is "provider" or "permanent" or "shared" or "rune-material" or "rune-material-zero" ? (ushort)212 : (ushort)0)
                .WithStackSize(mode is "provider" or "permanent" or "shared" or "rune-material" or "rune-material-zero" ? (ushort)1 : mode == "passive" ? (ushort)100 : (ushort)1500);
            if (mode == "stack-zero") material = material.WithStackSize(0);
            if (mode == "essence-cap") material = material.WithStackSize(10000);
            if (fault == "over-material-cap") material = material.WithStackSize(10001);
            if (mode == "rune-material-zero") material = material.WithStackSize(0);
            if (fault == "stacked-rune-material") material = material.WithStackSize(2);
            if (fault == "locked") material = material.WithLocked(1);
            if (fault == "deposit") material = material.WithDepositedItems([Item(1_020_099, 1_000_000, 0)]);
            var inventory = new[] { material, Item(Surplus, EssenceType, 0), Item(Invalid, 1_000_000, 0) }.Where(row => row.Position == 0).ToArray();
            var runes = new[] { target, material }.Where(row => row.Position == 212).ToArray();
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Uid, new(inventory, [], [], [], runes));
            PlayerSpellSnapshotEntry[] spells = mode == "permanent"
                ? [new(6001, 4, 1111, 2, 3, 2), new(15470, 0, 1234, 2, 3, 1)] : [new(6001, 4, 1111, 2, 3, 2)];
            OriginalSpells = PlayerSpellSnapshotLoader.ToLegacyBytes(spells); OriginalSpells[15] = 0xA5;
            File.WriteAllBytes(SpellsPath, OriginalSpells);
            var beastFields = Enumerable.Repeat("0", 19).ToArray(); beastFields[0] = "1"; beastFields[1] = "1000"; beastFields[^1] = "";
            var beast = BeastParser.ParseRow("owned-compose-beast", 1, string.Join("@@", beastFields));
            priorBeasts = LegacyNativeBeastsAtrribute.Attributes; LegacyNativeBeastsAtrribute.Attributes = [];
            LegacyNativeBeastsCatalogStartup.AddRows([beast]);
            ItemType Metadata(uint id, uint encoded, uint requiredClass = 6000, ushort stack = 0) => OwnedEquipmentBehaviorFixture.Item(id,
                "OwnedCompose" + id, new Dictionary<int, string> { [ItemType.DurabilityFieldIndex] = "10",
                    [ItemType.RequiredClassFieldIndex] = requiredClass.ToString(CultureInfo.InvariantCulture),
                    [ItemType.RuneSpellIdFieldIndex] = encoded.ToString(CultureInfo.InvariantCulture),
                    [ItemType.StackSizeFieldIndex] = stack.ToString(CultureInfo.InvariantCulture) });
            var metadata = new List<ItemType> { Metadata(TargetType, 1416016), Metadata(TargetType + 1, 1416017),
                Metadata(OtherType, 1547000), Metadata(PassiveType, PassiveType, 0),
                Metadata(PassiveType + 1, PassiveType + 1, 0), Metadata(EssenceType, 0, 0, 10000), Metadata(1_000_000, 0, 0, 1) };
            metadata.AddRange(Enumerable.Range(19, 9).Select(level => Metadata(4_020_500U + (uint)level, 1_416_000U + (uint)level - 1)));
            if (fault == "missing-successor") metadata.RemoveAll(row => row.Id == TargetType + 1);
            if (fault == "malformed-successor") ((string[])metadata.Single(row => row.Id == TargetType + 1).Fields)[ItemType.RuneSpellIdFieldIndex] = "invalid";
            if (fault == "malformed-material-cap") ((string[])metadata.Single(row => row.Id == EssenceType).Fields)[ItemType.StackSizeFieldIndex] = "invalid";
            var progress = new List<RuneLevelExperience>
            {
                Progress("2@@402@@1@@100@@"), Progress("18@@402@@17@@94000@@"), Progress("19@@402@@18@@109000@@"),
                Progress("20@@402@@19@@131500@@"), Progress("30@@403@@2@@600@@"), Progress("31@@403@@3@@1600@@"),
                Progress("32@@403@@4@@3100@@"), Progress("38@@404@@1@@10@@"),
            };
            progress.AddRange(new[] { "21@@402@@20@@154000@@", "22@@402@@21@@176500@@", "23@@402@@22@@199000@@",
                "24@@402@@23@@221500@@", "25@@402@@24@@251500@@", "26@@402@@25@@281500@@",
                "27@@402@@26@@311500@@", "28@@402@@27@@341500@@" }.Select(Progress));
            if (fault == "missing-material-progress") progress.RemoveAll(row => row.RuneType == 404);
            if (fault == "nonpositive-progress") progress[2] = Progress("19@@402@@18@@94000@@");
            Catalog = NativeCallerWorldDataFixture.Create(metadata.ToArray(), [], beastAttributes: new BeastAttribute[] { beast },
                magicEffects: MagicEffectParser.Parse("[1416000]\r\nIsRuneSkill=1\r\n[1547000]\r\nIsRuneSkill=1\r\n", "owned-compose-MagicEffect.ini"),
                magic: Enumerable.Range(0, 27).Select(Magic).ToArray(), runeLevelExperience: progress.ToArray());
            Data.Bootstrap(); await ReopenFrameAsync(); Ledger = File.ReadAllBytes(Data.State);
            Assert.True(Native.Beasts!.Activated); Assert.Equal((byte)1, Native.Beasts.Level);
            if (mode is not ("stored" or "stored-zero")) await DispatchAsync(new(Target, mode == "passive" ? 103U : 102U, MsgItemUsageAction.EquipRune, 112233, 44, 55, 66));
            if (mode is "provider" or "permanent" or "shared")
                await DispatchAsync(new(Material, 122, MsgItemUsageAction.EquipRune, 112244, 77, 88, 99));
            Assert.Equal(mode is "stored" or "stored-zero" ? (ushort)212 : mode == "passive" ? (ushort)103 : (ushort)102,
                Assert.Single(Current.PlayerItems.RuneItems, row => row.Uid == Target).Position);
            if (mode is "provider" or "permanent" or "shared")
                Assert.Equal((ushort)122, Assert.Single(Current.PlayerItems.RuneItems, row => row.Uid == Material).Position);
            AssertConverged(); Sent.Clear();
        }

        private static RuneLevelExperience Progress(string row) => RuneParser.ParseLevelExperienceRow("owned-exact-rune_levexp.txt", 1, row);
        private static MagicType Magic(int level)
        {
            var fields = Enumerable.Repeat("0", 53).ToArray(); fields[0] = (1_416_000 + level).ToString(CultureInfo.InvariantCulture);
            fields[1] = "14160"; fields[2] = "6"; fields[3] = "IronShield"; fields[8] = level.ToString(CultureInfo.InvariantCulture);
            fields[14] = "1"; fields[15] = "18"; fields[18] = "100000"; return new(1_416_000 + level, 14160, fields[3], fields);
        }
        internal static PlayerItemSnapshotItem Item(uint uid, uint type, ushort position) => new()
        { Uid = uid, ItemId = type, Position = position, StackSize = 1, Durability = 10, MaximumDurability = 10, Color = 5 };
        internal static byte[] Request(uint count = 1, params uint[] materials)
        {
            if (materials.Length == 0) materials = [Material];
            var writer = new PacketWriter(WorldPacketOpcodes.MsgItemUsage, MsgItemUsagePacket.PayloadLength + materials.Length * sizeof(uint));
            writer.WriteUInt32(0, Target); writer.WriteUInt64(4, 998877); writer.WriteUInt16(12, 63);
            writer.WriteUInt32(14, 511043495); writer.WriteUInt32(18, count); writer.WriteUInt32(22, 112244); writer.WriteUInt32(26, 55);
            for (int index = 0; index < materials.Length; index++) writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + index * sizeof(uint), materials[index]);
            return writer.ToArray();
        }
        internal Task DispatchAsync(MsgItemUsagePacket packet) => SendAsync(packet.ToFrameBytes());
        internal async Task SendAsync(byte[] bytes)
        {
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
            await Handler.HandleFrameAsync(connection!, frame, lifetime!.Token);
            if (observerFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
        }
        internal LegacyOwnedAdmissionRuntime Open()
        {
            bool success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason);
            if (opened is not null) owners.Add(opened); Assert.True(success, reason); return Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
        }
        internal async Task ReopenFrameAsync()
        {
            Runtime = Open(); Handler = new(Data.Loader, null, Catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
                Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            lifetime = new(); var cancellation = lifetime; var native = new LegacyNativeGameClientRuntime();
            connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
            {
                token.ThrowIfCancellationRequested(); var bytes = memory.ToArray();
                try { BeforeSend?.Invoke(bytes); } catch (Exception error) { observerFailure ??= error; throw; }
                Sent.Add(bytes); return ValueTask.CompletedTask;
            }, _ => { cancellation.Cancel(); return ValueTask.CompletedTask; }, native);
            await SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes()); Assert.Same(native, Native);
            Assert.NotNull(native.OrdinarySpellBinding); Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connection, Current));
        }
        internal async Task CloseAsync()
        {
            BeforeSend = null; var errors = new List<Exception>();
            if (connection is not null)
            {
                try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
                try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            }
            connection = null;
            foreach (var owner in owners.AsEnumerable().Reverse()) try { await owner.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            owners.Clear(); lifetime?.Dispose(); lifetime = null;
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
        internal void RestoreMetadata()
        { if (priorBeasts is not null) { LegacyNativeBeastsAtrribute.Attributes = priorBeasts; priorBeasts = null; } Log.Dispose(); }

        internal void AssertConverged()
        {
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(Native.OrdinarySpellBinding);
            Assert.True(Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(Catalog, binding.ItemCatalog);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems), File.ReadAllBytes(ItemsPath));
            Assert.Equal(Current.PlayerSpells.Spells, PlayerSpellSnapshotLoader.Parse(File.ReadAllBytes(SpellsPath)).Spells);
            Assert.Equal(Current.PlayerSpells.Spells.Select(row => row.Id).Order(), Native.MySpells!.ClientSpells.Keys.Order());
            foreach (var row in Current.PlayerSpells.Spells)
            {
                var actual = Native.MySpells.ClientSpells[row.Id]; Assert.Equal(row.Level, actual.Level); Assert.Equal(row.Experience, actual.Experience);
                Assert.Equal(row.PreviousLevel, actual.PreviousLevel); Assert.Equal(row.SoulLevel, actual.SoulLevel); Assert.Equal(row.UseSpellSoul, actual.UseSpellSoul);
            }
            foreach (var row in Current.PlayerItems.RuneItems)
            {
                var actual = Assert.Single(binding.Items.Rune.Objects, item => item.Uid == row.Uid);
                Assert.Equal(row.ItemId, actual.ITEM_ID); Assert.Equal(row.RuneExperience, actual.RuneEXP); Assert.Equal(row.Position, actual.Position);
            }
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out _));
            Assert.Equal(Current.Character.Money, saved.Money); Assert.Equal(Current.Character.ConquerPoints, saved.ConquerPoints);
            Assert.Equal(Current.Character.Money, Native.Player!.Money); Assert.Equal((uint)Current.Character.ConquerPoints, Native.Player.ConquerPoints);
            Assert.Equal(Current.Character.MinHitPoints, (long)Native.Player.HitPoints);
            Assert.Equal(Origins, saved.Attributes.GetValueOrDefault(Marker)); Assert.False(LegacyTradeJournal.HasPending(Data.Users));
            if (Ledger.Length != 0) Assert.Equal(Ledger, File.ReadAllBytes(Data.State));
            Assert.Equal(OriginalSpells.AsSpan(4, 12).ToArray(), File.ReadAllBytes(SpellsPath).AsSpan(4, 12).ToArray());
        }
    }
}
