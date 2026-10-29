using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    internal static async Task WithOwnedManagedManaConsumableAsync(ushort stack, Func<ManagedManaConsumableFixture, Task> body,
        bool metadata = true, bool protectedPin = false, string amount = "70", long initialMana = 10, long maximumMana = 5000)
    {
        var fixture = new ManagedManaConsumableFixture(); Exception? primary = null;
        try { fixture.Initialize(stack, metadata, protectedPin, amount, initialMana, maximumMana); await body(fixture); }
        catch (Exception error) { primary = error; }
        try { await fixture.DisposeAsync(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException("Owned managed Mana fixture and cleanup failed.", primary, cleanup);
            ExceptionDispatchInfo.Capture(cleanup).Throw();
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }

    internal sealed class ManagedManaConsumableFixture : IAsyncDisposable
    {
        internal const uint Uid = 1293600, PotionUid = 900001, OtherUid = 900002, PotionType = 1001000;
        internal const string OpaqueComment = "; owned managed Mana opaque comment retained";
        internal const string OpaqueAttribute = "OwnedManagedManaOpaque=preserve-uninterpreted-value";
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-world-handler-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(Root, "Users");
        internal string Items => Path.Combine(Root, "PlayersItems");
        internal string Spells => Path.Combine(Root, "PlayersSpells");
        internal string CharacterPath => Path.Combine(Users, Uid + ".ini");
        internal string ItemsPath => Path.Combine(Items, Uid + ".bin");
        internal List<byte[]> Sent { get; } = [];
        internal StringWriter Log { get; } = new();
        internal Action<byte[]>? OnSend { get; set; }
        internal WorldServerFrameHandler Handler { get; private set; } = null!;
        internal ConquerTcpConnection Connection { get; private set; } = null!;
        internal LegacyNativeGameClientRuntime Native { get; private set; } = null!;
        internal WorldSessionStore Store => Actual<WorldSessionStore>("_sessions");
        internal LegacyTradeRecoveryState Recovery => Actual<LegacyTradeRecoveryState>("_tradeRecovery");
        internal LegacyTradeStateStore Trades => Actual<LegacyTradeStateStore>("_tradeStates");
        internal WorldSession Current => Assert.IsType<WorldSession>(Store.TryGet(Connection, out var session) ? session : null);
        internal LegacyNativePlayerRuntime Player => Assert.IsType<LegacyNativePlayerRuntime>(Current.NativePlayer);
        private OrdinaryLoginFixture? admission;
        private LegacyOwnedAdmissionRuntime? reopened;
        private WorldDataCatalog catalog = null!;
        private NativeLoginBeastMetadataScope? beasts;
        private long initialMana, maximumMana;
        private bool initialized;
        private T Actual<T>(string name) where T : class => Assert.IsType<T>(typeof(WorldServerFrameHandler)
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(Handler));

        internal void Initialize(ushort stack, bool metadata, bool protectedPin, string amount, long initialMana, long maximumMana = 5000)
        {
            this.initialMana = initialMana;
            this.maximumMana = maximumMana;
            Guard(); Assert.False(Directory.Exists(Root)); Directory.CreateDirectory(Users); initialized = true;
            Directory.CreateDirectory(Items); Directory.CreateDirectory(Spells);
            WriteSnapshot(Users, Uid, uid: Uid, classCode: 7051, level: 140, minHitPoints: 10,
                money: 1000, conquerPoints: 200, boundConquerPoints: 300, reborn: 0, secondClass: 0,
                name: "OwnedManagedManaOwner", securityPass: protectedPin ? "1234,0,0" : null,
                extraCharacterLines: OpaqueComment + "\r\n" + OpaqueAttribute);
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out var encoding);
            Assert.Contains("MinMana=5000", text, StringComparison.Ordinal);
            text = text.Replace("MinMana=5000", "MinMana=" + initialMana.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "\r\nMaxMana=" + maximumMana.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
            File.WriteAllBytes(CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            var potion = new PlayerItemSnapshotItem { Uid = PotionUid, ItemId = PotionType, StackSize = stack,
                Durability = 1, MaximumDurability = 1 };
            var other = new PlayerItemSnapshotItem { Uid = OtherUid, ItemId = PotionType, StackSize = 1,
                Durability = 1, MaximumDurability = 1 };
            var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(new([potion, other], [], [], [], []));
            bytes[4 + 14] = 0xA5; bytes[4 + PlayerItemSnapshotLoader.LegacyDbItemSize + 14] = 0x5A;
            File.WriteAllBytes(ItemsPath, bytes);
            var baseline = OrdinaryLoginFixture.Catalog();
            var rows = baseline.Items.ToList();
            if (metadata)
            {
                var fields = Enumerable.Repeat("0", 66).ToArray(); fields[0] = PotionType.ToString(); fields[1] = "OwnedVanilla";
                fields[ItemType.ItemMPFieldIndex] = amount; fields[ItemType.StackSizeFieldIndex] = "10";
                fields[ItemType.DurabilityFieldIndex] = "1";
                rows.Add(new(checked((int)PotionType), fields[1], fields));
            }
            catalog = NativeCallerWorldDataFixture.Create(rows, [], classLevelStats: baseline.ClassLevelStats,
                magicTypeOperations: baseline.MagicTypeOperations);
            // All opaque inputs precede the fixture's actual original-byte capture.
            admission = new OrdinaryLoginFixture(new CharacterSnapshotLoader(Users), catalog);
            beasts = new NativeLoginBeastMetadataScope(1000); Handler = admission.CreateHandler(Log);
            Native = new LegacyNativeGameClientRuntime();
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (frame, _) => { var sentBytes = frame.ToArray(); OnSend?.Invoke(sentBytes); Sent.Add(sentBytes); return ValueTask.CompletedTask; },
                _ => ValueTask.CompletedTask, Native);
        }

        internal async Task Login()
        {
            await Handler.HandleFrameAsync(Connection, ReadFrame(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes()));
            admission!.AssertCommitObserved();
            Assert.DoesNotContain("runtime exception", Log.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PARITY-BUG", Log.ToString(), StringComparison.Ordinal);
            Assert.Equal(10L, Current.Character.MinHitPoints); Assert.Equal(10, Player.HitPoints);
            Assert.Equal(initialMana, Current.Character.MinMana); Assert.Equal(maximumMana, Current.Character.MaxMana);
            Assert.False(Recovery.IsBlocked);
        }
        internal async Task Use() => await Handler.HandleFrameAsync(Connection, ReadFrame(
            new MsgItemUsagePacket(PotionUid, 0, MsgItemUsageAction.Equip, 1234, 0, 0, 0).ToFrameBytes()));
        internal async Task CloseOwnerForRecovery()
        {
            await Connection.DisconnectAsync();
            await Connection.CompleteTransportCloseAsync();
            admission?.Dispose(); admission = null;
            if (reopened is not null) { await reopened.DisposeAsync(); reopened = null; }
            beasts?.Dispose(); beasts = null;
        }
        internal async Task Reload()
        {
            await CloseOwnerForRecovery();
            var loader = new CharacterSnapshotLoader(Users); var roots = LegacyOrdinaryAdmissionRoots.FromLoader(loader);
            var state = Path.Combine(Items, ".managed-item-uids", "state.json");
            var domain = Guid.ParseExact(JsonNode.Parse(File.ReadAllBytes(state))!["Domain"]!.GetValue<string>(), "N");
            var options = new LegacyOwnedAdmissionOptions(domain, roots.Users, roots.Items!, roots.Spells!, roots.Proficiencies!,
                roots.Quests!, roots.CombatGear!, Path.Combine(Root, "PlayersMailList"), Path.Combine(Root, "RedeemContainer.bin"),
                Path.Combine(Root, "ClaimContainer.bin"), 2_000_000, "owned synthetic fixture: quiescent historical floor");
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(loader, options, out reopened, out var reason), reason);
            Assert.NotNull(reopened);
            beasts = new NativeLoginBeastMetadataScope(1000);
            Handler = new WorldServerFrameHandler(loader, null, catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
                Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: reopened);
            Native = new LegacyNativeGameClientRuntime();
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (frame, _) => { var bytes = frame.ToArray(); OnSend?.Invoke(bytes); Sent.Add(bytes); return ValueTask.CompletedTask; },
                _ => ValueTask.CompletedTask, Native);
            await Handler.HandleFrameAsync(Connection, ReadFrame(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes()));
            Assert.NotNull(Native.OrdinarySpellBinding); Assert.False(Recovery.IsBlocked);
            Assert.DoesNotContain("PARITY-BUG", Log.ToString(), StringComparison.Ordinal);
        }
        internal Dictionary<string, byte[]> Files() => Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != "owner.lock").ToDictionary(path => path, File.ReadAllBytes);
        internal void CheckFiles(Dictionary<string, byte[]> expected)
        {
            var actual = Files(); Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
            foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
        }
        private void Guard()
        {
            var full = Path.GetFullPath(Root); const string prefix = "wolf-world-handler-";
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(full));
            Assert.StartsWith(prefix, Path.GetFileName(full));
            Assert.True(Guid.TryParseExact(Path.GetFileName(full)[prefix.Length..], "N", out var id) && id != Guid.Empty);
        }
        public async ValueTask DisposeAsync()
        {
            var errors = new List<Exception>();
            try { if (Connection is not null) await Connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
            try { if (Connection is not null) await Connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            try { admission?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { if (reopened is not null) await reopened.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            try { beasts?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { Log.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (initialized && errors.Count == 0)
                try
                {
                    Guard(); var pending = new Stack<string>(); pending.Push(Root);
                    while (pending.TryPop(out var path))
                    {
                        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Owned managed Mana cleanup refuses reparse entries.");
                        if (Directory.Exists(path)) foreach (var child in Directory.GetFileSystemEntries(path)) pending.Push(child);
                    }
                    Directory.Delete(Root, recursive: true);
                }
                catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("Owned managed Mana fixture retained: " + Root, errors);
        }
    }
}
