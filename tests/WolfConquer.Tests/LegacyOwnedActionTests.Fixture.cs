using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedActionTests
{
    private const uint Uid = 2_399_700, Peer = Uid + 1, Right = 1_044_001, Left = Right + 1;
    private sealed record Input(string? Action = null, bool Admit = true, bool Neptune = false, bool Blessed = false);
    private static async Task Run(Func<ActionFixture, Task> body, Input? input = null)
    {
        var f = new ActionFixture(); var errors = new List<Exception>();
        try { await f.InitializeAsync(input ?? new()); await body(f); } catch (Exception error) { errors.Add(error); }
        try { await f.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { f.DisposeMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { f.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private sealed class ActionClock : TimeProvider
    {
        private long ticks;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero).AddTicks(ticks);
        internal void Advance(TimeSpan value) { ArgumentOutOfRangeException.ThrowIfNegative(value.Ticks); ticks = checked(ticks + value.Ticks); }
    }

    private sealed partial class ActionFixture
    {
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly ActionClock Clock = new();
        internal readonly StringWriter Log = new();
        internal readonly List<byte[]> Sent = [], PeerSent = [];
        internal readonly List<(uint Recipient, byte[] Bytes)> Order = [];
        internal Action<uint, byte[]>? BeforeSend;
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal WorldDataCatalog Catalog = null!;
        internal Dictionary<string, byte[]> BeforeLogin = [];
        private NativeLoginBeastMetadataScope? beasts;
        internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
        internal WorldSession Current => Get(Uid);
        internal WorldSession Get(uint uid) => Assert.IsType<WorldSession>(Store.TryGet(ConnectionFor(uid), out var session) ? session : null);
        internal LegacyNativeGameClientRuntime Native => Connection.NativeGameClient!;
        internal string CharacterPath(uint uid = Uid) => Path.Combine(Data.Users, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal string ItemsPath(uint uid = Uid) => Path.Combine(Data.Items, uid.ToString(CultureInfo.InvariantCulture) + ".bin");

        internal async Task InitializeAsync(Input input)
        {
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(Data.UserPath), out var encoding)
                .Replace("Class=7051\r\n", "Class=16051\r\n", StringComparison.Ordinal)
                .Replace("MinHitPoints=330\r\n", input.Neptune ? "MinHitPoints=0\r\n" : "MinHitPoints=1\r\n", StringComparison.Ordinal)
                .Replace("MinMana=50\r\n", "MinMana=50\r\nMaxMana=50\r\nExpBallUsed=0\r\n" +
                    "EpicTrojan=0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/\r\n", StringComparison.Ordinal);
            string extra = "Stamina=0\r\nAnimaSkin=3/3/1/2/3/\r\n" + (input.Action is null ? "" : "Action=" + input.Action + "\r\n");
            if (input.Blessed) extra += "HeavenBlessing=1\r\n";
            if (input.Neptune)
            {
                // DECLARED defensive saved input, not a positive curse grant.
                // No test/native registration or accepting status producer.
                var words = new ulong[18]; words[222 / 32] = 1UL << (222 % 32);
                extra += "StatusFlags=" + string.Join('/', words) + "\r\nDead_X=420\r\nDead_Y=370\r\n";
            }
            text = text.Replace("KeepBytes=startup-owned-value\r\n", "KeepBytes=startup-owned-value\r\n" + extra, StringComparison.Ordinal);
            File.WriteAllBytes(Data.UserPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Uid,
                new([Item(Left, 626009, 0)], [], [], [Item(Right, 626003, 4)], []));
            var spells = PlayerSpellSnapshotLoader.ToLegacyBytes([new(6001, 4, 1111, 2, 3, 2)]); spells[15] = 0xA5;
            File.WriteAllBytes(Path.Combine(Data.Spells, Uid + ".bin"), spells);
            SavePeer(); Catalog = BuildCatalog(); beasts = new(1000); Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var runtime, out var reason), reason);
            Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime); BeforeLogin = Data.Files();
            await CreateAndLoginAsync(input.Admit);
            if (input.Admit) { AssertCoherent(this); ClearOutput(this); }
        }

        private void SavePeer()
        {
            var character = OwnedEquipmentBehaviorFixture.Character(Peer, "ActionViewer", mapId: 1002, x: 430, y: 380, minHitPoints: 1);
            var fields = new Dictionary<string, string>(CharacterSnapshotScalarPersistence.BuildPersistedValues(character), StringComparer.OrdinalIgnoreCase)
            { ["Name"] = character.Name, ["SecurityPass"] = "0,0,0", ["Stamina"] = "100",
              ["EpicTrojan"] = "0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/" };
            File.WriteAllText(CharacterPath(Peer), "[Character]\r\n" + string.Join("\r\n", fields.Select(row => row.Key + "=" + row.Value)) + "\r\n", new UTF8Encoding(true));
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Peer, PlayerItemSnapshot.Empty);
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(Peer, PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(Peer, PlayerProficiencySnapshot.Empty);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(Peer, PlayerQuestSnapshot.Empty);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(Peer, CombatGearSnapshot.Empty);
            new LegacyMailboxStore(Data.Root).Save(Peer);
        }

        private static PlayerItemSnapshotItem Item(uint uid, uint id, ushort position) => new()
        { Uid = uid, ItemId = id, Position = position, Durability = 10, MaximumDurability = 10, StackSize = 0, Color = 5 };
        private static WorldDataCatalog BuildCatalog()
        {
            // Actual retained DBServer fan rows, explicit trusted BCL backing.
            string[] raw = [
                "626003@@PrideFan@@16000@@0@@5@@0@@0@@2@@0@@0@@0@@153@@0@@9@@5@@0@@51@@0@@0@@0@@1599@@1599@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@Handfan@@None@@5@@0@@0@@0@@1340@@0@@0@@0@@0@@0@@0@@",
                "626009@@PrideFan@@16000@@0@@5@@0@@0@@2@@0@@0@@0@@153@@0@@14@@9@@0@@63@@0@@0@@0@@1599@@1599@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@10@@0@@0@@0@@0@@0@@0@@0@@Handfan@@None@@5@@0@@0@@0@@1340@@0@@0@@0@@0@@0@@0@@",
            ];
            return NativeCallerWorldDataFixture.Create(raw.Select(line => line.Split("@@", StringSplitOptions.None))
                .Select(fields => new ItemType(int.Parse(fields[0], CultureInfo.InvariantCulture), fields[1], fields)).ToArray());
        }
        internal void DisposeMetadata() { beasts?.Dispose(); beasts = null; Log.Dispose(); }
    }
}
