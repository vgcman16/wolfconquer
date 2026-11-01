using System.Globalization;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task RealSignedCompletedCreatorAndNewbornBankCommitSurvivePairedMovementRecoveryWithoutStarterReplay() => Run(async f =>
    {
        await CreateAndAssert(f); await DepositNewbornMoney(f);
        var completed = CompletedIntent(f); var payload = completed.CopyPayload(); var recipe = Recipe(f, completed);
        var originalStarter = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(recipe.Projection.CopyCharacterImage(), out _));
        Assert.Equal(100_000, originalStarter.Money);
        Assert.NotEqual(recipe.Projection.CopyCharacterImage(), File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(99_300, f.Current.Character.Money); Assert.Equal("700", f.Current.Character.Attributes["WHMoney"]);
        Assert.Equal((1L, 0L), (f.Current.Character.Level, f.Current.Character.Reborn));
        await f.ConnectRuneActorAsync();
        var partnerConnection = f.RuneConnection!;
        Assert.Equal((410L, 350L), (f.Current.Character.X, f.Current.Character.Y));
        Assert.Equal((410L, 350L), (f.CurrentFor(partnerConnection).Character.X, f.CurrentFor(partnerConnection).Character.Y));
        await f.SendAsync(PairHistoryInteraction(CreationFixture.Account, CreationFixture.RuneActor, MsgAttackType.InteractionRequest));
        await f.SendOnAsync(partnerConnection, PairHistoryInteraction(CreationFixture.RuneActor, CreationFixture.Account, MsgAttackType.InteractionAccept));
        Assert.Equal(CreationFixture.RuneActor, f.Current.InteractionPartnerUid);
        Assert.Equal(CreationFixture.Account, f.CurrentFor(partnerConnection).InteractionPartnerUid);
        f.Sent.Clear(); f.Frames(CreationFixture.RuneActor).Clear();

        var before = f.Data.Files(); var expected = PairHistoryMovedFiles(f, before);
        var ledger = File.ReadAllBytes(f.Data.State); var history = HistoryFiles(f);
        Assert.NotEmpty(history);
        Assert.Equal(1UL, JsonNode.Parse(ledger)!["ArchiveCount"]!.GetValue<ulong>());
        var newborn = f.Current; var partner = f.CurrentFor(partnerConnection);
        var clients = new[] { f.Native, partner.NativeGameClient! };
        var bindings = clients.Select(client => client.OrdinarySpellBinding!).ToArray();
        var players = clients.Select(client => client.Player).ToArray();
        var images = clients.Select(client => client.OrdinarySpellBinding!.Items.Image).ToArray();
        var spells = clients.Select(client => client.MySpells!.ClientSpells).ToArray();
        using (var fault = LegacyJournalInterruption.StopAfter(1))
        {
            f.Handler.PairMovementJournalInterruption = fault;
            try
            {
                await f.SendAsync(new MsgActionPacket { Type = MsgActionType.Jump, TargetPositionX = 412,
                    TargetPositionY = 351, Facing = 6, Timestamp = 123456 }.ToFrameBytes());
            }
            finally { f.Handler.PairMovementJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString()); Assert.Equal(new[] { 0, 1 }, fault.Visited);
        }
        // A partially durable pair is fenced and both holders retire; the
        // captured sessions remain evidence, not admitted live owners.
        Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.False(f.Store.TryGet(partnerConnection, out _));
        Assert.All(bindings, binding => Assert.True(binding.IsRetired));
        Assert.Equal((410L, 350L), (newborn.Character.X, newborn.Character.Y));
        Assert.Equal((410L, 350L), (partner.Character.X, partner.Character.Y));
        Assert.Empty(f.Sent); Assert.Empty(f.Frames(CreationFixture.RuneActor));
        for (int index = 0; index < clients.Length; index++)
        {
            Assert.Same(players[index], clients[index].Player); Assert.Equal(0U, (uint)players[index].Action);
            Assert.Equal((ushort)410, players[index].X); Assert.Equal((ushort)350, players[index].Y);
            Assert.Same(images[index], bindings[index].Items.Image);
            Assert.Same(spells[index], clients[index].MySpells!.ClientSpells);
        }
        Assert.Equal(ledger, File.ReadAllBytes(f.Data.State)); AssertHistoricalFiles(history, HistoryFiles(f));
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!; Assert.Equal(21, record["Version"]!.GetValue<int>());
        var operation = record["PairMovement"]!;
        Assert.Equal(2, operation["Kind"]!.GetValue<int>());
        // The mover's account is GREATER: canonical target0 is partner, not actor.
        Assert.Equal((long)CreationFixture.Account, operation["ActorAccountId"]!.GetValue<long>());
        Assert.Equal(CreationFixture.Account, operation["ActorUid"]!.GetValue<uint>());
        Assert.Equal((long)CreationFixture.RuneActor, operation["PartnerAccountId"]!.GetValue<long>());
        Assert.Equal(CreationFixture.RuneActor, operation["PartnerUid"]!.GetValue<uint>());
        var targets = new[] { f.Data.UserPath, f.CharacterPath };
        var changes = record["Changes"]!.AsArray(); Assert.Equal(2, changes.Count);
        for (int index = 0; index < targets.Length; index++)
        {
            var change = changes[index]!; string key = Path.GetRelativePath(f.Data.Root, targets[index]);
            Assert.Equal((int)LegacyTradeJournalFileKind.Character, change["Kind"]!.GetValue<int>());
            Assert.Equal((long)(index == 0 ? CreationFixture.RuneActor : CreationFixture.Account), change["FileId"]!.GetValue<long>());
            Assert.Equal(before[key], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.Equal(expected[key], Convert.FromBase64String(change["After"]!.GetValue<string>()));
            Assert.Equal(index == 0 ? expected[key] : before[key], File.ReadAllBytes(targets[index]));
        }
        foreach (var row in before)
            if (!targets.Contains(Path.Combine(f.Data.Root, row.Key), StringComparer.OrdinalIgnoreCase))
                Assert.Equal(row.Value, File.ReadAllBytes(Path.Combine(f.Data.Root, row.Key)));
        string transaction = record["TransactionId"]!.GetValue<string>();
        await f.CloseAsync(); int outputCount = f.Sent.Count + f.Frames(CreationFixture.RuneActor).Count;
        for (int restart = 0; restart < 2; restart++)
        {
            using var recovered = f.Open();
            Assert.True(recovered.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
            f.Data.AssertFiles(expected); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
            AssertHistoricalFiles(history, HistoryFiles(f)); Assert.Equal(payload, CompletedIntent(f).CopyPayload());
            Assert.Equal(completed.Allocation!.First, CompletedIntent(f).Allocation!.First);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.All(targets, path => Assert.False(File.Exists(path + ".managed-trade-" + transaction + ".tmp")));
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
            Assert.Equal(99_300, saved.Money); Assert.Equal("700", saved.Attributes["WHMoney"]);
            Assert.Equal((412L, 351L), (saved.X, saved.Y));
            Assert.True(LegacyItemUidRawCatalog.TryScan(f.Data.Items, f.Data.Mailbox, f.Data.Redeem, f.Data.Claim,
                out var observation, out var reason), reason);
            Assert.True(observation.Maximum <= JsonNode.Parse(ledger)!["HighWater"]!.GetValue<uint>());
            Assert.Equal(outputCount, f.Sent.Count + f.Frames(CreationFixture.RuneActor).Count);
        }
    }, beforeBootstrap: PairHistorySeedPartner);

    private static void PairHistorySeedPartner(CreationFixture f)
    {
        // Existing safe fixture hook, BEFORE bootstrap. This changes only the
        // preexisting actor's saved geometry; no newborn is edited or granted.
        byte[] bytes = File.ReadAllBytes(f.Data.UserPath);
        string text = CharacterSnapshotFileEncoding.Decode(bytes, out var encoding);
        text = PairHistoryReplace(text, "X", 430, 410); text = PairHistoryReplace(text, "Y", 380, 350);
        File.WriteAllBytes(f.Data.UserPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
    }
    private static Dictionary<string, byte[]> PairHistoryMovedFiles(CreationFixture f, Dictionary<string, byte[]> before)
    {
        var result = before.ToDictionary(row => row.Key, row => row.Value, StringComparer.OrdinalIgnoreCase);
        foreach (string path in new[] { f.Data.UserPath, f.CharacterPath })
        {
            string key = Path.GetRelativePath(f.Data.Root, path);
            string text = CharacterSnapshotFileEncoding.Decode(result[key], out var encoding);
            text = PairHistoryReplace(text, "X", 410, 412); text = PairHistoryReplace(text, "Y", 350, 351);
            result[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        }
        return result;
    }
    private static string PairHistoryReplace(string text, string key, long before, long after)
    {
        string token = "\r\n" + key + "=" + before.ToString(CultureInfo.InvariantCulture) + "\r\n";
        Assert.Equal(2, text.Split(token, StringSplitOptions.None).Length);
        return text.Replace(token, "\r\n" + key + "=" + after.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal);
    }
    private static byte[] PairHistoryInteraction(uint actor, uint target, MsgAttackType kind) =>
        new MsgAttackPacket(new(actor, target, 410, 350, kind, 0, 0, 190, MsgAttackEffect.None, 0)).ToFrameBytes();
}
