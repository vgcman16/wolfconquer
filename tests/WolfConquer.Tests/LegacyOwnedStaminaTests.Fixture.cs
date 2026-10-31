using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedStaminaTests
{
    private const uint Uid = 2_399_700, Peer = Uid + 1, Right = 1_041_001, Left = Right + 1;
    private const ushort Breath = 11960;
    private sealed record Input(string? Stamina = "0", bool Blessed = false, bool Sitting = false,
        bool Peer = false, string Mode = "normal", int Flag = -1, bool Admit = true);

    private static async Task Run(Func<StaminaFixture, Task> body, Input? input = null)
    {
        var f = new StaminaFixture(); var errors = new List<Exception>();
        try { await f.InitializeAsync(input ?? new()); await body(f); } catch (Exception error) { errors.Add(error); }
        try { await f.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { f.DisposeMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { f.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private sealed class StaminaClock : TimeProvider
    {
        private long elapsed, utc;
        private int readsUntilRewind;
        private long rewindTicks;
        internal bool RewindFired { get; private set; }
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => elapsed;
        public override DateTimeOffset GetUtcNow()
        {
            if (readsUntilRewind > 0 && --readsUntilRewind == 0)
            { utc = checked(utc - rewindTicks); RewindFired = true; }
            return new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero).AddTicks(utc);
        }
        internal void RewindOnRelativeUtcRead(int ordinal, TimeSpan value)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ordinal);
            ArgumentOutOfRangeException.ThrowIfNegative(value.Ticks);
            if (readsUntilRewind != 0) throw new InvalidOperationException("clockRewindAlreadyArmed");
            RewindFired = false; rewindTicks = value.Ticks; readsUntilRewind = ordinal;
        }
        internal void Advance(TimeSpan value) { ArgumentOutOfRangeException.ThrowIfNegative(value.Ticks); elapsed = checked(elapsed + value.Ticks); utc = checked(utc + value.Ticks); }
        internal void RewindWallClock(TimeSpan value) { ArgumentOutOfRangeException.ThrowIfNegative(value.Ticks); utc = checked(utc - value.Ticks); }
    }

    private sealed partial class StaminaFixture
    {
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly StaminaClock Clock = new();
        internal readonly StringWriter Log = new();
        internal readonly List<byte[]> Sent = [], PeerSent = [];
        internal readonly List<(uint Recipient, byte[] Bytes)> Order = [];
        internal Action<uint, byte[]>? BeforeSend;
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal WorldDataCatalog Catalog = null!;
        internal Dictionary<string, byte[]> BeforeLogin = [];
        internal byte[] OriginalSpells = [];
        internal Input Options = null!;
        private NativeLoginBeastMetadataScope? beasts;
        internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
        internal WorldSession Current => Assert.IsType<WorldSession>(Store.TryGet(Connection, out var current) ? current : null);
        internal LegacyNativeGameClientRuntime Native => Connection.NativeGameClient!;
        internal string CharacterPath => Data.UserPath;
        internal string ItemsPath => Path.Combine(Data.Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string SpellsPath => Path.Combine(Data.Spells, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");

        internal async Task InitializeAsync(Input input)
        {
            Options = input;
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out var encoding)
                .Replace("Class=7051\r\n", "Class=" + (input.Mode == "monk" ? "6000" : input.Mode.StartsWith("trojan", StringComparison.Ordinal) ? "1000" : "16051") + "\r\n", StringComparison.Ordinal)
                .Replace("MinHitPoints=330\r\n", "MinHitPoints=1\r\n", StringComparison.Ordinal)
                // Explicit complete baseline for the existing HP writer,
                // BEFORE bootstrap. Its established EpicTrojan composite is
                // already present, so the lifetime oracle expects only HP.
                .Replace("MinMana=50\r\n", "MinMana=50\r\nMaxMana=50\r\nExpBallUsed=0\r\n" +
                    "EpicTrojan=0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/\r\n", StringComparison.Ordinal);
            var extra = (input.Stamina is null ? "" : "Stamina=" + input.Stamina + "\r\n") +
                (input.Blessed ? "HeavenBlessing=1\r\n" : "") +
                (input.Flag < 0 ? "" : "StatusFlags=" + FlagText(input.Flag) + "\r\n");
            text = text.Replace("KeepBytes=startup-owned-value\r\n", "KeepBytes=startup-owned-value\r\n" + extra, StringComparison.Ordinal);
            File.WriteAllBytes(CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            var right = Item(Right, input.Mode == "monk" ? 622003U : 626003U, 4);
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Uid, new([Item(Left, 626009, 0)], [], [], [right], []));
            var spells = new List<PlayerSpellSnapshotEntry> { new(6001, 4, 1111, 2, 3, 2) };
            if (input.Mode == "monk") spells.Add(new(12560, 0, 0, 0, 0, 0));
            if (input.Mode == "trojan-learned") spells.Add(new(14190, 0, 0, 0, 0, 0));
            OriginalSpells = PlayerSpellSnapshotLoader.ToLegacyBytes(spells); OriginalSpells[15] = 0xA5;
            File.WriteAllBytes(SpellsPath, OriginalSpells);
            if (input.Peer) SavePeer();
            Catalog = BuildCatalog(); beasts = new(1000); Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var runtime, out var reason), reason);
            Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime); BeforeLogin = Data.Files();
            await CreateAndLoginAsync();
            if (input.Admit)
            {
                Assert.True(Store.TryGet(Connection, out _), Log.ToString());
                if (input.Sitting) { await SitAsync(); Assert.Equal("250", Current.Character.Attributes["Action"]); }
                AssertCoherent(this); Sent.Clear(); PeerSent.Clear(); Order.Clear();
            }
        }

        private static string FlagText(int flag)
        { var words = new ulong[18]; words[flag / 32] = 1UL << (flag % 32); return string.Join('/', words); }
        private static PlayerItemSnapshotItem Item(uint uid, uint id, ushort position) => new()
        { Uid = uid, ItemId = id, Position = position, Durability = 10, MaximumDurability = 10, StackSize = 0, Color = 5 };

        private void SavePeer()
        {
            var character = OwnedEquipmentBehaviorFixture.Character(Peer, "StaminaViewer", mapId: 1002, x: 431, y: 380, minHitPoints: 1);
            var values = new Dictionary<string, string>(CharacterSnapshotScalarPersistence.BuildPersistedValues(character), StringComparer.OrdinalIgnoreCase)
                { ["Name"] = character.Name, ["SecurityPass"] = "0,0,0", ["Stamina"] = "100" };
            File.WriteAllText(Path.Combine(Data.Users, Peer.ToString(CultureInfo.InvariantCulture) + ".ini"),
                "[Character]\r\n" + string.Join("\r\n", values.Select(value => value.Key + "=" + value.Value)) + "\r\n", new System.Text.UTF8Encoding(true));
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Peer, PlayerItemSnapshot.Empty);
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(Peer, PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(Peer, PlayerProficiencySnapshot.Empty);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(Peer, PlayerQuestSnapshot.Empty);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(Peer, CombatGearSnapshot.Empty);
            new LegacyMailboxStore(Data.Root).Save(Peer);
        }

        private static WorldDataCatalog BuildCatalog()
        {
            // Retained DBServer rows, not guessed item names/class classifiers.
            string[] raw = [
                "626003@@PrideFan@@16000@@0@@5@@0@@0@@2@@0@@0@@0@@153@@0@@9@@5@@0@@51@@0@@0@@0@@1599@@1599@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@Handfan@@None@@5@@0@@0@@0@@1340@@0@@0@@0@@0@@0@@0@@",
                "626009@@PrideFan@@16000@@0@@5@@0@@0@@2@@0@@0@@0@@153@@0@@14@@9@@0@@63@@0@@0@@0@@1599@@1599@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@10@@0@@0@@0@@0@@0@@0@@0@@Handfan@@None@@5@@0@@0@@0@@1340@@0@@0@@0@@0@@0@@0@@",
                "622003@@BuddhistOracle`sPillar@@6000@@0@@5@@0@@0@@2@@0@@0@@0@@153@@0@@7@@5@@0@@37@@0@@0@@0@@1599@@1599@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@500@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@EpicWeapon@@None@@5@@0@@0@@0@@1320@@0@@0@@0@@0@@0@@0@@",
            ];
            var items = raw.Select(line => line.Split("@@", StringSplitOptions.None)).Select(fields =>
                new ItemType(int.Parse(fields[0], CultureInfo.InvariantCulture), fields[1], fields)).ToArray();
            return NativeCallerWorldDataFixture.Create(items, [], magic: new MagicType[] { Magic(12560, 6, 10, 0), Magic(14190, 6, 100, 30) });
        }
        private static MagicType Magic(ushort id, byte sort, uint damage, uint duration)
        {
            var fields = Enumerable.Repeat("0", 48).ToArray(); fields[0] = (id * 100).ToString(CultureInfo.InvariantCulture);
            fields[1] = id.ToString(CultureInfo.InvariantCulture); fields[2] = sort.ToString(CultureInfo.InvariantCulture);
            fields[3] = id == 12560 ? "GraceofHeaven" : "Rampage"; fields[10] = damage.ToString(CultureInfo.InvariantCulture);
            fields[13] = duration.ToString(CultureInfo.InvariantCulture); fields[14] = "1"; fields[15] = "18"; fields[20] = "1";
            return new(id * 100, id, fields[3], fields);
        }
    }
}
