using System.Globalization;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedBankCpsTransactionTests
{
    [WindowsNpcSaleTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ActualCpBankSharingRefusalPreservesPendingAndSameOwnerCanRetry(bool itemTarget, bool denyRead) =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, deposit: true, 300);
            var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
            AssertBankTuple(f, 1_000, 250, 300, deposit: true);
            using (File.Open(itemTarget ? f.ItemsPath : f.CharacterPath, FileMode.Open, FileAccess.Read,
                denyRead ? FileShare.None : FileShare.Read))
            {
                await ConfirmBank(f, deposit: true);

                Assert.Same(before, f.Current); Assert.Equal(300U, f.Current.PendingBankConquerPoints);
                AssertNoBankSuccess(f.Sent.Skip(first));
                Assert.Equal((1_000U, 250UL), (f.Native.Player.ConquerPoints, f.Native.ConquerPointsSave));
            }
            f.CheckFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Users));
            AssertBankTuple(f, 1_000, 250, 300, deposit: true);
            int retry = f.Sent.Count;

            await ConfirmBank(f, deposit: true);

            AssertBankTuple(f, 700, 550); AssertBankSuccessFrames(f.Sent.Skip(retry).ToArray(), 700, 550);
            Assert.Equal(files[f.ItemsPath], File.ReadAllBytes(f.ItemsPath));
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("negative")]
    [InlineData("overflow")]
    [InlineData("malformed")]
    [InlineData("stale")]
    [InlineData("duplicate")]
    public Task InvalidRawSavedBankCpsRefusesBeforePublicationAndRetainsTheCurrentConfirmation(string fault) =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, deposit: true, 300);
            var before = f.Current; var originalCharacter = File.ReadAllBytes(f.CharacterPath);
            var text = CharacterSnapshotFileEncoding.Decode(originalCharacter, out var encoding);
            const string pattern = "(?m)^BankCps=[^\\r\\n]*(?:\\r?\\n|$)";
            Assert.Single(Regex.Matches(text, pattern).Cast<Match>());
            var replacement = fault switch
            {
                "missing" => "",
                "empty" => "BankCps=\r\n",
                "negative" => "BankCps=-1\r\n",
                "overflow" => "BankCps=9223372036854775808\r\n",
                "malformed" => "BankCps=not-a-balance\r\n",
                "stale" => "BankCps=251\r\n",
                "duplicate" => "BankCps=250\r\nbankcps=250\r\n",
                _ => throw new ArgumentOutOfRangeException(nameof(fault)),
            };
            text = Regex.Replace(text, pattern, _ => replacement);
            File.WriteAllBytes(f.CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            var corruptFiles = f.Files(); int first = f.Sent.Count;

            await ConfirmBank(f, deposit: true);

            Assert.Same(before, f.Current); Assert.Equal(300U, f.Current.PendingBankConquerPoints);
            Assert.True(f.Current.PendingBankConquerPointsIsDeposit);
            Assert.Equal((1_000U, 250UL), (f.Native.Player.ConquerPoints, f.Native.ConquerPointsSave));
            f.CheckFiles(corruptFiles); AssertNoBankSuccess(f.Sent.Skip(first));
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
            File.WriteAllBytes(f.CharacterPath, originalCharacter); int retry = f.Sent.Count;

            await ConfirmBank(f, deposit: true);

            AssertBankTuple(f, 700, 550); AssertBankSuccessFrames(f.Sent.Skip(retry).ToArray(), 700, 550);
        });

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public Task RealCpBankWalInterruptionRetainsPendingAndActualRecoveryAppliesTheSavedTransferOnce(bool withdraw, int replacements) =>
        WithBank(async f =>
        {
            uint amount = withdraw ? 100U : 300U;
            await f.Login(); await StageBank(f, deposit: !withdraw, amount);
            var before = f.Current; var proposal = BankCpsProposal(before, deposit: !withdraw, amount);
            var characterBytes = File.ReadAllBytes(f.CharacterPath); var itemBytes = File.ReadAllBytes(f.ItemsPath);
            var expected = ExpectedCpBankCharacter(characterBytes, proposal.Character.ConquerPoints, proposal.Character.BankConquerPoints);
            var native = (f.Native.Player.ConquerPoints, f.Native.ConquerPointsSave);
            int first = f.Sent.Count;
            using var interruption = LegacyJournalInterruption.StopAfter(replacements);

            Assert.False(LegacyNpcSalePersistence.TryPersistBankCps(before, proposal, f.Items,
                deposit: !withdraw, amount, interruption, out var reason));

            Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
            Assert.True(LegacyTradeJournal.HasPending(f.Users));
            Assert.Equal(replacements == 0 ? characterBytes : expected, File.ReadAllBytes(f.CharacterPath));
            Assert.Equal(itemBytes, File.ReadAllBytes(f.ItemsPath));
            Assert.Same(before, f.Current); Assert.Equal(amount, f.Current.PendingBankConquerPoints);
            Assert.Equal(native, (f.Native.Player.ConquerPoints, f.Native.ConquerPointsSave));
            Assert.Equal(first, f.Sent.Count);
            var pendingPath = Path.Combine(f.Users, ".managed-trade-journal", "pending.json");
            var pendingBytes = File.ReadAllBytes(pendingPath);
            await ConfirmBank(f, deposit: !withdraw);
            Assert.Same(before, f.Current); Assert.True(f.Recovery.IsBlocked);
            AssertNoBankSuccess(f.Sent.Skip(first)); Assert.Equal(pendingBytes, File.ReadAllBytes(pendingPath));

            // Close this actual connection/runtime lease and reopen the same
            // real configured owner. No reseed/bootstrap or fabricated recovery
            // custody is supplied; ordinary startup reconciles the real v2 WAL.
            await f.ReopenAfterRecoveryAsync();
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
            Assert.Equal(expected, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(itemBytes, File.ReadAllBytes(f.ItemsPath));
            var saved = ReadCpBankSaved(f);
            Assert.Equal(proposal.Character.ConquerPoints, saved.ConquerPoints);
            Assert.Equal(proposal.Character.BankConquerPoints, saved.BankConquerPoints);
            AssertBankTuple(f, proposal.Character.ConquerPoints, proposal.Character.BankConquerPoints);
            Assert.True(LegacyTradeJournal.TryRecover(f.Users, f.Items, out var recovered, out reason), reason);
            Assert.Equal(0, recovered);
            Assert.False(LegacyNpcSalePersistence.TryPersistBankCps(before, proposal, f.Items,
                deposit: !withdraw, amount, out reason));
            Assert.Equal(expected, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(itemBytes, File.ReadAllBytes(f.ItemsPath));
            AssertBankTuple(f, proposal.Character.ConquerPoints, proposal.Character.BankConquerPoints);
            Assert.Null(f.Current.PendingBankConquerPoints); Assert.False(f.Recovery.IsBlocked);
        });

    [Theory]
    [InlineData("money")]
    [InlineData("bound")]
    [InlineData("warehouse")]
    [InlineData("item")]
    [InlineData("attribute")]
    [InlineData("pending-retained")]
    [InlineData("pending-direction")]
    [InlineData("pending-missing")]
    [InlineData("wrong-delta")]
    [InlineData("missing-users")]
    [InlineData("missing-items")]
    public Task ClosedCpBankWriterRejectsUnrelatedChangesAndWrongConfirmationWithoutReplacingAnyFile(string fault) =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, deposit: true, 300);
            var holder = f.Current; var before = holder; var proposal = BankCpsProposal(holder, deposit: true, 300);
            string? items = f.Items;
            switch (fault)
            {
                case "money": proposal = proposal with { Character = proposal.Character with { Money = 999 } }; break;
                case "bound": proposal = proposal with { Character = proposal.Character with { BoundConquerPoints = 301 } }; break;
                case "warehouse": proposal = ChangeCpBankAttribute(ChangeCpBankAttribute(proposal, "WHMoney", "556"), "WarehouseMoney", "556"); break;
                case "item": proposal = proposal with { PlayerItems = proposal.PlayerItems with { InventoryItems = [] } }; break;
                case "attribute": proposal = ChangeCpBankAttribute(proposal, "UnrelatedBankProbe", "changed"); break;
                case "pending-retained": proposal = proposal with { PendingBankConquerPoints = 300, PendingBankConquerPointsIsDeposit = true }; break;
                case "pending-direction": before = before with { PendingBankConquerPointsIsDeposit = false }; break;
                case "pending-missing": before = before with { PendingBankConquerPoints = null }; break;
                case "wrong-delta": proposal = ChangeCpBankAttribute(
                    proposal with { Character = proposal.Character with { ConquerPoints = 699 } }, "ConquerPoints", "699"); break;
                case "missing-users": before = before with { UsersRoot = null }; proposal = proposal with { UsersRoot = null }; break;
                case "missing-items": items = null; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            var files = f.Files(); var native = (f.Native.Player.ConquerPoints, f.Native.ConquerPointsSave);
            int first = f.Sent.Count;

            Assert.False(LegacyNpcSalePersistence.TryPersistBankCps(before, proposal, items, deposit: true, 300, out var reason));

            Assert.NotEmpty(reason); Assert.Same(holder, f.Current); Assert.Equal(300U, f.Current.PendingBankConquerPoints);
            Assert.Equal(native, (f.Native.Player.ConquerPoints, f.Native.ConquerPointsSave));
            Assert.Equal(first, f.Sent.Count); f.CheckFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });

    private static WorldSession BankCpsProposal(WorldSession before, bool deposit, uint amount)
    {
        var cps = deposit ? checked(before.Character.ConquerPoints - amount) : checked(before.Character.ConquerPoints + amount);
        var bank = deposit ? checked(before.Character.BankConquerPoints + amount) : checked(before.Character.BankConquerPoints - amount);
        var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = cps.ToString(CultureInfo.InvariantCulture),
            ["BankCps"] = bank.ToString(CultureInfo.InvariantCulture),
        };
        return before with { Character = before.Character with { ConquerPoints = cps, BankConquerPoints = bank, Attributes = attributes },
            PendingBankConquerPoints = null, PendingBankConquerPointsIsDeposit = false };
    }

    private static WorldSession ChangeCpBankAttribute(WorldSession source, string key, string value)
    {
        var attributes = new Dictionary<string, string>(source.Character.Attributes, StringComparer.OrdinalIgnoreCase) { [key] = value };
        return source with { Character = source.Character with { Attributes = attributes } };
    }

    private static CharacterSnapshot ReadCpBankSaved(Fixture f) =>
        CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));

    private static byte[] ExpectedCpBankCharacter(byte[] before, long cps, long bank)
    {
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        foreach (var (key, value) in new[] { ("ConquerPoints", cps), ("BankCps", bank) })
        {
            var pattern = "(?m)^" + Regex.Escape(key) + "=[^\\r\\n]*";
            Assert.Single(Regex.Matches(text, pattern).Cast<Match>());
            text = Regex.Replace(text, pattern, _ => key + "=" + value.ToString(CultureInfo.InvariantCulture));
        }
        return encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
    }
}
