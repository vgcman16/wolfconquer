using System.Net;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    internal static async Task WithNativeItemSpellCallerAsync(bool rune, Func<NativeItemSpellCallerFixture, Task> body,
        IReadOnlyList<TransformInfo>? transformInfo = null, IReadOnlyList<string>? characterLines = null)
    {
        var f = new NativeItemSpellCallerFixture(); Exception? primary = null;
        try { f.Initialize(rune, transformInfo, characterLines); await body(f); } catch (Exception error) { primary = error; }
        try { await f.DisposeAsync(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException($"Owned item/spell fixture {f.Root} failed and cleanup failed.", primary, cleanup);
            ExceptionDispatchInfo.Capture(cleanup).Throw();
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }

    internal sealed class NativeItemSpellCallerFixture : IAsyncDisposable
    {
        internal const uint Uid = 1293600, ItemUid = 900001;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-world-handler-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(Root, "Users");
        internal string Items => Path.Combine(Root, "PlayersItems");
        internal string Spells => Path.Combine(Root, "PlayersSpells");
        internal string CharacterPath => Path.Combine(Users, Uid + ".ini");
        internal string ItemsPath => Path.Combine(Items, Uid + ".bin");
        internal string SpellsPath => Path.Combine(Spells, Uid + ".bin");
        internal readonly List<byte[]> Sent = []; internal readonly StringWriter Log = new();
        internal Action<byte[]>? OnSend { get; set; }
        internal LegacyNativeGameClientRuntime Native { get; private set; } = null!;
        internal ConquerTcpConnection Connection { get; private set; } = null!;
        internal WorldServerFrameHandler Handler { get; private set; } = null!;
        internal WorldSessionStore Store => Actual<WorldSessionStore>("_sessions");
        internal LegacyTradeStateStore Trades => Actual<LegacyTradeStateStore>("_tradeStates");
        internal WorldOperationBarrier Operations => Actual<WorldOperationBarrier>("_worldOperations");
        internal WorldDataCatalog Catalog { get; private set; } = null!;
        internal LegacyTradeRecoveryState Recovery => Actual<LegacyTradeRecoveryState>("_tradeRecovery");
        internal WorldSession Current => Assert.IsType<WorldSession>(Store.TryGet(Connection, out var current) ? current : null);
        private OrdinaryLoginFixture? admission; private NativeLoginBeastMetadataScope? beasts; private bool owned;
        private T Actual<T>(string name) where T : class => Assert.IsType<T>(typeof(WorldServerFrameHandler)
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(Handler));
        internal void Initialize(bool rune, IReadOnlyList<TransformInfo>? transformInfo = null, IReadOnlyList<string>? characterLines = null)
        {
            Guard(); Assert.False(Directory.Exists(Root)); Directory.CreateDirectory(Users); owned = true;
            Directory.CreateDirectory(Items); Directory.CreateDirectory(Spells);
            WriteSnapshot(Users, Uid, uid: Uid, classCode: rune ? 6000 : 7051, level: 140,
                reborn: rune ? 2 : 0, secondClass: 0, money: 1000, conquerPoints: 200, boundConquerPoints: 300,
                minHitPoints: 1000, name: "OwnedItemSpellOwner");
            // Pure input preparation precedes the real issuer's baseline capture.
            if (characterLines is { Count: > 0 })
            {
                Assert.All(characterLines, line =>
                {
                    Assert.DoesNotContain('\n', line); Assert.DoesNotContain('\r', line);
                });
                var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out var encoding);
                text = text.Replace("[Character]", "[Character]\r\n" + string.Join("\r\n", characterLines), StringComparison.Ordinal);
                File.WriteAllBytes(CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            }
            var item = new PlayerItemSnapshotItem { Uid = ItemUid, ItemId = rune ? 4020517U : 725016U,
                Position = rune ? (ushort)102 : (ushort)0, Durability = 1, MaximumDurability = 1, StackSize = 1 };
            var rankKeeper = new PlayerItemSnapshotItem { Uid = ItemUid + 1, ItemId = 4020515, Position = 212, Durability = 1, MaximumDurability = 1, StackSize = 1 };
            var items = rune ? new PlayerItemSnapshot([], [], [], [], [item, rankKeeper]) : new PlayerItemSnapshot([item], [], [], [], []);
            File.WriteAllBytes(ItemsPath, PlayerItemSnapshotLoader.ToLegacyBytes(items));
            var rows = rune ? new[] { new PlayerSpellSnapshotEntry(14160, 16, 0, 0, 0, 0), new PlayerSpellSnapshotEntry(6001, 4, 1111, 2, 3, 2) }
                : new[] { new PlayerSpellSnapshotEntry(6001, 4, 1111, 2, 3, 2) };
            var spellBytes = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(rows)); spellBytes[^1] = 0xA5;
            File.WriteAllBytes(SpellsPath, spellBytes);
            var baseline = OrdinaryLoginFixture.Catalog();
            var fields = Enumerable.Repeat("0", 66).ToArray(); fields[0] = (rune ? 4020517 : 725016).ToString();
            fields[1] = "OwnedFixtureItem"; fields[ItemType.DurabilityFieldIndex] = "1"; fields[ItemType.StackSizeFieldIndex] = "1";
            if (rune) { fields[ItemType.RequiredClassFieldIndex] = "6000"; fields[ItemType.RuneSpellIdFieldIndex] = "1416016"; }
            var itemTypes = baseline.Items.Append(new ItemType(rune ? 4020517 : 725016, fields[1], fields)).ToList();
            if (rune) { var extra = (string[])fields.Clone(); extra[0] = "4020515"; extra[1] = "OwnedRankKeeper"; extra[ItemType.RuneSpellIdFieldIndex] = "0"; itemTypes.Add(new ItemType(4020515, extra[1], extra)); }
            var catalog = NativeCallerWorldDataFixture.Create(itemTypes, [],
                classLevelStats: baseline.ClassLevelStats, magicTypeOperations: baseline.MagicTypeOperations,
                magic: [new MagicType(1360, 1360, "OwnedBookSpell", [])], transformInfo: transformInfo);
            Catalog = catalog;
            beasts = new NativeLoginBeastMetadataScope(1000);
            admission = new OrdinaryLoginFixture(new CharacterSnapshotLoader(Users), catalog);
            Handler = admission.CreateHandler(Log); Native = new LegacyNativeGameClientRuntime();
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (frame, _) => { byte[] bytes = frame.ToArray(); OnSend?.Invoke(bytes); Sent.Add(bytes); return ValueTask.CompletedTask; }, _ => ValueTask.CompletedTask, Native);
        }
        internal async Task Login() => await Handler.HandleFrameAsync(Connection, ReadFrame(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes()));
        internal async Task Send(byte[] frame) => await Handler.HandleFrameAsync(Connection, ReadFrame(frame));
        internal Dictionary<string, byte[]> Files()
        {
            var lease = Path.Combine(Items, ".managed-item-uids", "owner.lock");
            return Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Where(p => !StringComparer.OrdinalIgnoreCase.Equals(p, lease))
                .ToDictionary(p => p, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        }
        internal void CheckFiles(Dictionary<string, byte[]> expected)
        {
            var actual = Files(); Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
            foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
        }
        private void Guard()
        {
            var root = Path.GetFullPath(Root); Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(root));
            const string prefix = "wolf-world-handler-"; string name = Path.GetFileName(root); Assert.StartsWith(prefix, name);
            Assert.True(Guid.TryParseExact(name[prefix.Length..], "N", out _)); LegacyNativeDbReadOwnedFiles.OrdinaryChain(root);
        }
        public async ValueTask DisposeAsync()
        {
            var errors = new List<Exception>();
            try { if (Connection is not null) await Connection.CompleteTransportCloseAsync(); } catch (Exception e) { errors.Add(e); }
            try { admission?.Dispose(); } catch (Exception e) { errors.Add(e); }
            try { beasts?.Dispose(); } catch (Exception e) { errors.Add(e); }
            try { Log.Dispose(); } catch (Exception e) { errors.Add(e); }
            if (owned && errors.Count == 0)
            {
                try
                {
                    Guard(); var stack = new Stack<string>(); stack.Push(Root);
                    while (stack.TryPop(out var path)) { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Owned cleanup refuses reparse entries.");
                        if (Directory.Exists(path)) foreach (var child in Directory.GetFileSystemEntries(path)) stack.Push(child); }
                    Directory.Delete(Root, recursive: true);
                }
                catch (Exception e) { errors.Add(e); }
            }
            if (errors.Count != 0) throw new AggregateException($"Owned fixture retained: {Root}", errors);
        }
    }
}
