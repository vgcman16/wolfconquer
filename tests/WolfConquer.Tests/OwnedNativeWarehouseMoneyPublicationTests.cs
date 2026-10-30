using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Spec: docs/protocol/specs/7899-owned-warehouse-money-publication.md.
public sealed partial class OwnedNativeWarehouseMoneyPublicationTests
{
    [Theory]
    [InlineData("0", null, 0L)]
    [InlineData("555", null, 555L)]
    [InlineData("9223372036854775807", null, long.MaxValue)]
    [InlineData(null, "555", 555L)]
    [InlineData(null, "9223372036854775807", long.MaxValue)]
    [InlineData("555", "555", 555L)]
    [InlineData("000555", "+555", 555L)]
    [InlineData(null, null, 0L)]
    public Task AdmissionPublishesTheSavedWarehouseBeforeItsFirstReplyWithoutAliasRewriteOrExtraWarehouseFrame(
        string? canonical, string? alias, long warehouse) =>
        WithWarehouseFrame(canonical, alias, async f =>
        {
            f.Native.Player.WHMoney = 77;
            f.Native.PokerPlayer = new LegacyNativePokerPlayer("OwnedWarehouse", 17) { CurrentMoney = 42 };
            var state = File.ReadAllBytes(f.Data.State);
            int replies = 0; Exception? observed = null;
            f.OnSend = _ =>
            {
                replies++;
                try
                {
                    Assert.Equal((777L, warehouse, 222U, 333, 444UL), WarehouseNativeTuple(f));
                    Assert.Equal(777L, f.Current.Character.Money);
                    Assert.Equal(42L, f.Native.PokerPlayer!.CurrentMoney);
                }
                catch (Exception error) { observed ??= error; }
            };

            await f.LoginAsync(); f.OnSend = null;

            if (observed is not null) ExceptionDispatchInfo.Capture(observed).Throw();
            Assert.True(replies > 0); Assert.NotNull(f.Native.OrdinarySpellBinding);
            Assert.Equal((777L, warehouse, 222U, 333, 444UL), WarehouseNativeTuple(f));
            Assert.Equal(42L, f.Native.PokerPlayer!.CurrentMoney);
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.Data.UserPath), out _));
            Assert.Equal(canonical is not null, saved.Attributes.ContainsKey("WHMoney"));
            Assert.Equal(alias is not null, saved.Attributes.ContainsKey("WarehouseMoney"));
            if (canonical is not null) Assert.Equal(canonical, saved.Attributes["WHMoney"]);
            if (alias is not null) Assert.Equal(alias, saved.Attributes["WarehouseMoney"]);
            Assert.Equal(state, File.ReadAllBytes(f.Data.State));
            Assert.DoesNotContain(f.Sent, bytes => LegacyMsgUpdatePacket.TryParse(WarehouseFrame(bytes), out var update) &&
                update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.WarehouseMoney));
            Assert.DoesNotContain("Frame exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);

            var first = f.Sent.Count; var files = f.Data.Files();
            await f.LoginAsync();

            Assert.Equal(first, f.Sent.Count); f.Data.AssertFiles(files);
            Assert.Equal((777L, warehouse, 222U, 333, 444UL), WarehouseNativeTuple(f));
        });

    [Theory]
    [InlineData("-1", null)]
    [InlineData("9223372036854775808", null)]
    [InlineData("not-a-balance", null)]
    [InlineData("", null)]
    [InlineData(null, "-1")]
    [InlineData(null, "9223372036854775808")]
    [InlineData("555", "556")]
    [InlineData("555", "not-a-balance")]
    [InlineData("not-a-balance", "555")]
    public Task InvalidSavedWarehouseRefusesAdmissionBeforeFilesNativeWalletUidStateOrOutputChange(
        string? canonical, string? alias) =>
        WithWarehouseFrame(canonical, alias, async f =>
        {
            f.Native.Player.WHMoney = 77;
            var files = f.Data.Files(); var native = WarehouseNativeTuple(f);

            await f.LoginAsync();

            Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.Null(f.Native.OrdinarySpellBinding);
            Assert.Equal(native, WarehouseNativeTuple(f)); Assert.Empty(f.Sent);
            f.Data.AssertFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });

    [Fact]
    public Task RawCaseDuplicateWarehouseSavedFieldIsRejectedByTheActualAdmissionImageValidator() =>
        WithWarehouseFrame("555", null, async f =>
        {
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.Data.UserPath), out var encoding);
            Assert.Contains("WHMoney=555\r\n", text, StringComparison.Ordinal);
            text = text.Replace("WHMoney=555\r\n", "WHMoney=555\r\nwhmoney=555\r\n", StringComparison.Ordinal);
            File.WriteAllBytes(f.Data.UserPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            var files = f.Data.Files(); var native = WarehouseNativeTuple(f);

            await f.LoginAsync();

            Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.Null(f.Native.OrdinarySpellBinding);
            Assert.Equal(native, WarehouseNativeTuple(f)); Assert.Empty(f.Sent);
            f.Data.AssertFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });

    [Theory]
    [InlineData("negative")]
    [InlineData("overflow")]
    [InlineData("malformed")]
    [InlineData("alias-negative")]
    [InlineData("alias-overflow")]
    [InlineData("alias-disagrees")]
    [InlineData("case-duplicate")]
    [InlineData("null")]
    public Task InvalidWarehouseProposalCannotReachTheActualBoundStoreWriter(string fault) =>
        WithWarehouseFrame("555", null, async f =>
        {
            await f.LoginAsync(); var before = f.Current;
            var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.Ordinal);
            switch (fault)
            {
                case "negative": attributes["WHMoney"] = "-1"; break;
                case "overflow": attributes["WHMoney"] = "9223372036854775808"; break;
                case "malformed": attributes["WHMoney"] = "invalid"; break;
                case "alias-negative": attributes["WarehouseMoney"] = "-1"; break;
                case "alias-overflow": attributes["WarehouseMoney"] = "9223372036854775808"; break;
                case "alias-disagrees": attributes["WarehouseMoney"] = "556"; break;
                case "case-duplicate": attributes.Add("whmoney", "555"); break;
                case "null": attributes["WHMoney"] = null!; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            var proposal = before with { Character = before.Character with { Attributes = attributes } };
            var files = f.Data.Files(); var native = WarehouseNativeTuple(f); int first = f.Sent.Count, writes = 0;
            bool UnexpectedWriter() { writes++; throw new InvalidOperationException("Invalid warehouse proposal reached persistence."); }

            Assert.Throws<InvalidOperationException>(() =>
                f.Store.TryCommitSession(f.Connection, before, proposal, UnexpectedWriter));

            Assert.Equal(0, writes); Assert.Same(before, f.Current); Assert.Equal(native, WarehouseNativeTuple(f));
            Assert.Equal(first, f.Sent.Count); f.Data.AssertFiles(files);
        });

    [Fact]
    public void WarehouseMoneyRemainsTheActualPublicSigned64FieldForNativeCallsiteCompatibility()
    {
        var field = typeof(LegacyNativePlayerRuntime).GetField("WHMoney");
        Assert.NotNull(field); Assert.True(field.IsPublic); Assert.Equal(typeof(long), field.FieldType);
        Assert.Null(typeof(LegacyNativePlayerRuntime).GetProperty("WHMoney"));
    }
}
