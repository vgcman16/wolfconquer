using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Actual Frame/native/Store, existing ordinary admission owner and owned files.
// No accepting saver or catalog is substituted for the bank transaction.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedBankCpsTransactionTests
{
    private const uint BankNpcUid = 102_177;
    private const long BankWarehouseGold = 555;

    private static async Task WithBank(Func<Fixture, Task> body, long cps = 1_000, long bank = 250)
    {
        Fixture? fixture = null; Exception? primary = null;
        try { fixture = new(); fixture.Initialize(cps, bank); await body(fixture); }
        catch (Exception error) { primary = error; }
        var cleanup = fixture is null ? new List<Exception>() : await fixture.CloseAsync();
        if (primary is null && cleanup.Count == 0 && fixture is not null)
            try { fixture.Data.Dispose(); } catch (Exception error) { cleanup.Add(error); }
        if (primary is not null) cleanup.Insert(0, primary);
        if (cleanup.Count == 1) ExceptionDispatchInfo.Capture(cleanup[0]).Throw();
        if (cleanup.Count > 1) throw new AggregateException("Owned CP-bank assertion and/or cleanup failed.", cleanup);
    }

    // Saved fixture inputs are established before its actual leased domain is
    // bootstrapped/opened. No captured admission observer baseline is rewritten.
    private sealed class Fixture
    {
        internal const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly List<byte[]> Sent = [];
        internal readonly StringWriter Log = new();
        internal Action<byte[]>? OnSend { get; set; }
        internal LegacyOwnedAdmissionRuntime? Runtime;
        internal LegacyNativeGameClientRuntime Native = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal ConquerTcpConnection Connection = null!;
        private NativeLoginBeastMetadataScope? beasts;
        internal string Root => Data.Root;
        internal string Users => Data.Users;
        internal string Items => Data.Items;
        internal string CharacterPath => Data.UserPath;
        internal string ItemsPath => Path.Combine(Data.Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string SpellsPath => Path.Combine(Data.Spells, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal WorldSessionStore Store => Actual<WorldSessionStore>("_sessions");
        internal LegacyTradeStateStore Trades => Actual<LegacyTradeStateStore>("_tradeStates");
        internal WorldOperationBarrier Operations => Actual<WorldOperationBarrier>("_worldOperations");
        internal LegacyTradeRecoveryState Recovery => Actual<LegacyTradeRecoveryState>("_tradeRecovery");
        internal WorldSession Current => Assert.IsType<WorldSession>(Store.TryGet(Connection, out var current) ? current : null);
        private T Actual<T>(string name) where T : class => Assert.IsType<T>(typeof(WorldServerFrameHandler)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));

        internal void Initialize(long cps, long bank)
        {
            RewriteBankField(this, "ConquerPoints", cps.ToString(CultureInfo.InvariantCulture));
            RewriteBankField(this, "BankCps", bank.ToString(CultureInfo.InvariantCulture));
            RewriteBankField(this, "Money", "1000"); RewriteBankField(this, "BoundConquerPoints", "300");
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out var encoding);
            Assert.Contains("WHMoney=555\r\n", text, StringComparison.Ordinal);
            text = text.Replace("WHMoney=555\r\n", "WHMoney=555\r\nWarehouseMoney=555\r\n", StringComparison.Ordinal);
            File.WriteAllBytes(CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            Data.Bootstrap(); beasts = new NativeLoginBeastMetadataScope(1000);
            OpenActualFrame();
        }

        private void OpenActualFrame()
        {
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out Runtime, out var reason), reason);
            Assert.NotNull(Runtime);
            var fields = Enumerable.Repeat("0", 66).ToArray();
            fields[0] = "1000000"; fields[1] = "OwnedBankSavedItem";
            fields[ItemType.DurabilityFieldIndex] = "1"; fields[ItemType.StackSizeFieldIndex] = "1";
            var catalog = LegacyItemWorldDataFixture.Create([new ItemType(1000000, fields[1], fields)], [],
                npcs: [new Npc((int)BankNpcUid, Kind: 0, Lookface: 0, MapId: 1002, X: 430, Y: 378, OptionalFlag: null)]);
            Handler = new WorldServerFrameHandler(Data.Loader, null, catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
                Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime!);
            Native = new LegacyNativeGameClientRuntime();
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (frame, _) => { var bytes = frame.ToArray(); OnSend?.Invoke(bytes); Sent.Add(bytes); return ValueTask.CompletedTask; },
                _ => ValueTask.CompletedTask, Native);
        }

        internal async Task Login() => await Handler.HandleFrameAsync(Connection,
            BankFrame(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes()));
        internal async Task Send(byte[] bytes) => await Handler.HandleFrameAsync(Connection, BankFrame(bytes));
        internal Dictionary<string, byte[]> Files() => Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .Where(path => !StringComparer.OrdinalIgnoreCase.Equals(path, Data.OwnerLease))
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        internal void CheckFiles(Dictionary<string, byte[]> expected)
        {
            var actual = Files(); Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
            foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
        }

        internal async Task ReopenAfterRecoveryAsync()
        {
            OnSend = null;
            await Connection.CompleteTransportCloseAsync();
            if (Runtime is not null) await Runtime.DisposeAsync();
            Runtime = null; OpenActualFrame(); await Login();
        }

        internal async Task<List<Exception>> CloseAsync()
        {
            var errors = new List<Exception>();
            if (Connection is not null) try { await Connection.CompleteTransportCloseAsync(); } catch (Exception e) { errors.Add(e); }
            if (Runtime is not null) try { await Runtime.DisposeAsync(); } catch (Exception e) { errors.Add(e); }
            try { beasts?.Dispose(); } catch (Exception e) { errors.Add(e); }
            try { Log.Dispose(); } catch (Exception e) { errors.Add(e); }
            return errors;
        }
    }

    private static void RewriteBankField(Fixture f, string key, string value)
    {
        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out var encoding);
        var pattern = "(?m)^" + Regex.Escape(key) + "=[^\\r\\n]*";
        Assert.Single(Regex.Matches(text, pattern).Cast<Match>());
        text = Regex.Replace(text, pattern, _ => key + "=" + value);
        File.WriteAllBytes(f.CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
    }

    private static Task SendBank(Fixture f, byte option, string input = "") =>
        f.Send(LegacyNpcServerRequestPacket.Script(BankNpcUid, option,
            option is 10 or 20 ? LegacyNpcReplyInteractType.Input : LegacyNpcReplyInteractType.Option, input).ToFrameBytes());

    private static Task StageBank(Fixture f, bool deposit, uint amount) =>
        SendBank(f, deposit ? (byte)10 : (byte)20, amount.ToString(CultureInfo.InvariantCulture));

    private static Task ConfirmBank(Fixture f, bool deposit) => SendBank(f, deposit ? (byte)2 : (byte)3);

    private static PacketFrame BankFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertBankTuple(Fixture f, long cps, long bank, uint? pending = null, bool deposit = false)
    {
        var current = f.Current;
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
        Assert.NotNull(f.Native.OrdinarySpellBinding);
        Assert.Equal((1_000L, cps, 300L, bank),
            (current.Character.Money, current.Character.ConquerPoints, current.Character.BoundConquerPoints,
                current.Character.BankConquerPoints));
        Assert.Equal((1_000L, cps, 300L, bank),
            (saved.Money, saved.ConquerPoints, saved.BoundConquerPoints, saved.BankConquerPoints));
        Assert.Equal((1_000L, checked((uint)cps), 300, checked((ulong)bank), BankWarehouseGold),
            (f.Native.Player.Money, f.Native.Player.ConquerPoints, f.Native.Player.BoundConquerPoints,
                f.Native.ConquerPointsSave, f.Native.Player.WHMoney));
        Assert.Equal(pending, current.PendingBankConquerPoints);
        Assert.Equal(deposit, current.PendingBankConquerPointsIsDeposit);
        foreach (var attributes in new[] { current.Character.Attributes, saved.Attributes })
        {
            Assert.Equal("555", attributes["WHMoney"]);
            Assert.Equal("555", attributes["WarehouseMoney"]);
        }
    }

    private static void AssertBankSuccessFrames(byte[][] actual, long cps, long bank)
    {
        byte[][] expected =
        [
            new LegacyMsgUpdatePacket(Fixture.Uid,
                [new(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)cps))]).ToFrameBytes(),
            LegacyNpcReplyPacket.ForDialog("Done. You now have " + cps.ToString(CultureInfo.InvariantCulture) +
                " CPs with you and " + bank.ToString(CultureInfo.InvariantCulture) + " CPs in the bank.").ToFrameBytes(),
            LegacyNpcReplyPacket.ForClosedOption("Thanks.").ToFrameBytes(),
            LegacyNpcReplyPacket.ForAvatar(183).ToFrameBytes(),
            LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
        ];
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }

    private static void AssertBankStageFrames(byte[][] actual, bool deposit, uint amount)
    {
        byte[][] expected =
        [
            LegacyNpcReplyPacket.ForDialog("Are you sure you want to " + (deposit ? "deposit " : "withdraw ") +
                amount.ToString(CultureInfo.InvariantCulture) + " CPs" +
                (deposit ? " into your bank?" : " from your bank?")).ToFrameBytes(),
            LegacyNpcReplyPacket.ForOption("Yes.", deposit ? (byte)2 : (byte)3).ToFrameBytes(),
            LegacyNpcReplyPacket.ForClosedOption("No thanks.").ToFrameBytes(),
            LegacyNpcReplyPacket.ForAvatar(48).ToFrameBytes(),
            LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
        ];
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }

    private static void AssertNoBankSuccess(IEnumerable<byte[]> frames)
    {
        foreach (var bytes in frames)
        {
            var frame = BankFrame(bytes);
            Assert.False(LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ConquerPoints));
            Assert.False(LegacyNpcReplyPacket.TryParse(frame, out var reply) && reply.Text.StartsWith("Done.", StringComparison.Ordinal));
        }
    }
}
