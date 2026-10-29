using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPlayerBoothItemClosureTests
{
    private sealed record BoothRecoveryImages(Snapshot Before, Snapshot After,
        LegacyTradeJournalChange[] Changes);

    private sealed partial class BoothFixture
    {
        private AccountHandoffService? recoveryHandoff;

        internal async Task<BoothRecoveryImages> PrepareRegisteredBoothRecoveryImagesAsync()
        {
            Assert.Null(settlementFrameOwner);
            // Equal IDs are the ACTUAL auth-loader contract, not a restriction on
            // the registered booth component or its separate long-account controls.
            var seller = Harness.CreateSeller(300, 50,
                SevenStores(new[] { BoothItem(10, 1_100_003), BoothItem(11) }, 40_000));
            var buyer = Harness.CreateBuyer(1_000, 200, SevenStores(new[] { BoothItem(50) }, 30_000));
            SeedHealthyBoothRecoveryParticipant(SellerConnection, seller with { AccountId = seller.Character.Uid });
            SeedHealthyBoothRecoveryParticipant(BuyerConnection, buyer with { AccountId = buyer.Character.Uid });
            InitializeSettlementOwners();
            await DepositAsync(11); // Genuine registered Sash producer + real owned setup save.
            await StartAsync();
            await ListAsync();
            var before = Capture();

            await BuyAsync();

            AssertOneSettledBoothPurchase(this, before);
            var after = Capture();
            AssertNoBoothRecoveryClaims(after.Buyer);
            AssertNoBoothRecoveryClaims(after.Seller);
            var buyerCharacter = Path.GetRelativePath(Root, CharacterPath(before.Buyer));
            var sellerCharacter = Path.GetRelativePath(Root, CharacterPath(before.Seller));
            var buyerItem = Path.GetRelativePath(Root, ItemPath(before.Buyer));
            var sellerItem = Path.GetRelativePath(Root, ItemPath(before.Seller));
            // Exact real vendor v1 order: both wallets BEFORE both item images.
            LegacyTradeJournalChange Change(LegacyTradeJournalFileKind kind, long id, string relative) =>
                new(kind, id, before.Files[relative], after.Files[relative]);
            return new(before, after,
            [
                Change(LegacyTradeJournalFileKind.Character, before.Buyer.AccountId, buyerCharacter),
                Change(LegacyTradeJournalFileKind.Character, before.Seller.AccountId, sellerCharacter),
                Change(LegacyTradeJournalFileKind.Items, before.Buyer.Character.Uid, buyerItem),
                Change(LegacyTradeJournalFileKind.Items, before.Seller.Character.Uid, sellerItem),
            ]);
        }

        private void SeedHealthyBoothRecoveryParticipant(
            WolfConquer.Core.Networking.ConquerTcpConnection connection, WorldSession session)
        {
            SeedOne(connection, session);
            var current = Harness.StoredSession(connection);
            // Class10 attributes contribute 3 HP per Strength. The original
            // business fixture's all-zero stats/HP1 would legitimately clamp
            // HP to0 on first real Frame startup, outside this recovery seam.
            // Seed a healthy real parsed file BEFORE taking any WAL images;
            // do not bypass startup or relax a single subsequent byte assertion.
            Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(CharacterPath(current),
                current.Character.Uid, new Dictionary<string, string> { ["Strength"] = "1" },
                detailedIoFailures: true, out var reason), reason);
            var character = CharacterSnapshotParser.ParseText(
                File.ReadAllText(CharacterPath(current)), CharacterPath(current));
            Assert.Equal(1, character.Strength);
            Assert.Equal(1, character.MinHitPoints);
            Harness.Sessions.Set(connection, current with { Character = character });
        }

        internal void PublishInterruptedBoothRecoveryImages(BoothRecoveryImages images, int prefix)
        {
            Assert.InRange(prefix, 0, 4);
            Assert.All(Calls, task => Assert.True(task.IsCompleted));
            Assert.False(LegacyTradeJournal.HasPending(Harness.UsersRoot));
            // Owned interrupted-write simulation after the genuine operation has
            // ended. No live callback, fake persistence, or fabricated afterimage.
            foreach (var (relative, bytes) in images.Before.Files)
                File.WriteAllBytes(Path.Combine(Root, relative), bytes);
            AssertFiles(images.Before.Files);
            Assert.False(LegacyTradeJournal.TryCommit(Harness.UsersRoot, Harness.PlayerItemsRoot,
                images.Changes, LegacyJournalInterruption.StopAfter(prefix), out var reason));
            Assert.StartsWith("tradeJournalRecoveryRequired", reason, StringComparison.Ordinal);
            Assert.True(LegacyTradeJournal.HasPending(Harness.UsersRoot));
            for (var index = 0; index < images.Changes.Length; index++)
            {
                var change = images.Changes[index];
                var root = change.Kind == LegacyTradeJournalFileKind.Character ? Harness.UsersRoot : Harness.PlayerItemsRoot;
                var suffix = change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin";
                Assert.Equal(index < prefix ? change.After : change.Before,
                    File.ReadAllBytes(Path.Combine(root, change.FileId.ToString(CultureInfo.InvariantCulture) + suffix)));
            }
        }

        internal void CreateBoothRecoveryFrameOwner()
        {
            Assert.Null(settlementFrameOwner);
            recoveryHandoff = new(new InMemoryAccountHandoffTicketStore());
            var loader = new CharacterSnapshotLoader(Harness.UsersRoot, Harness.PlayerItemsRoot,
                playerSpellsRoot: null, playerProficienciesRoot: null,
                playersCombatGearRoot: null, playerQuestsRoot: null);
            settlementFrameOwner = new WorldServerFrameHandler(loader, recoveryHandoff, worldData: null,
                runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: Log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        internal Task RequestBoothRecoveryAdmissionAsync(bool buyer)
        {
            Assert.NotNull(recoveryHandoff);
            var uid = buyer ? D13VendorTestHarness.BuyerCharacterUid : D13VendorTestHarness.SellerCharacterUid;
            var ticket = recoveryHandoff.Issue(uid, buyer ? "buyer" : "seller", ConquerRuntimeDefaults.WorldServer);
            return SendSettlementFrameAsync(buyer ? BuyerConnection : SellerConnection,
                new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes());
        }

        internal WorldSessionStore ActualBoothRecoverySessions()
        {
            Assert.NotNull(settlementFrameOwner);
            // Read-only assertions on the genuine Frame owner, never field injection.
            var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return Assert.IsType<WorldSessionStore>(field.GetValue(settlementFrameOwner));
        }
    }

    private static void AssertNoBoothRecoveryClaims(WorldSession session)
    {
        Assert.True(LegacyTradeEscrowClaims.TryRead(session.Character.Attributes, out var claim, out var reason), reason);
        Assert.Null(claim);
        Assert.DoesNotContain(session.Character.Attributes.Keys, key =>
            key.Equals(LegacyTradeEscrowClaims.IdKey, StringComparison.OrdinalIgnoreCase) ||
            key.Equals(LegacyTradeEscrowClaims.MoneyKey, StringComparison.OrdinalIgnoreCase) ||
            key.Equals(LegacyTradeEscrowClaims.CpKey, StringComparison.OrdinalIgnoreCase));
    }
}
