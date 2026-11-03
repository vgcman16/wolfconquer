using MySqlConnector;
using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task RealSqlDuplicateUsernameCannotSelectTheSingleMatchingPassword() => Run(async f =>
    {
        await f.Database.AddDuplicateUsernameAsync(SqlWorldFixture.FirstUid + 3000);
        var rows = await f.Database.CreateRepository().FindByUsernameAsync(OwnedModernSqlAuthDatabase.FirstUsername);
        Assert.Equal(2, rows.Count);
        var files = f.Data.Files(); var sql = await f.Database.SnapshotAsync();
        int tickets = f.TicketCount; var owner = f.Current; var peer = f.Other;
        var forward = await f.AuthenticateAsync(OwnedModernSqlAuthDatabase.FirstUsername, OwnedModernSqlAuthDatabase.SyntheticPassword);
        Assert.Equal(LegacyAccountForwardType.InvalidInfo, forward.Type); Assert.Equal(0U, forward.Identifier);
        Assert.Equal(tickets, f.TicketCount); Assert.Same(owner, f.Current); Assert.Same(peer, f.Other);
        Assert.Empty(f.Sent); Assert.Empty(f.OtherSent); f.Data.AssertFiles(files);
        Assert.Equal(sql, await f.Database.SnapshotAsync()); f.AssertConverged();
    });

    [ModernSqlAuthFixtureFact]
    public Task RealSqlWrongAndMissingAccountsCannotIssueReadyOrTouchSavedPlayers() => Run(async f =>
    {
        var files = f.Data.Files(); var sql = await f.Database.SnapshotAsync(); int tickets = f.TicketCount;
        var owner = f.Current; var other = f.Other;
        var wrong = await f.AuthenticateAsync(OwnedModernSqlAuthDatabase.FirstUsername, "wrong_fixture");
        Assert.Equal(LegacyAccountForwardType.InvalidInfo, wrong.Type); Assert.Equal(0U, wrong.Identifier);
        var missing = await f.AuthenticateAsync("missing_fixture", OwnedModernSqlAuthDatabase.SyntheticPassword);
        Assert.Equal(LegacyAccountForwardType.WrongAccount, missing.Type); Assert.Equal(0U, missing.Identifier);
        Assert.Equal(tickets, f.TicketCount); Assert.Same(owner, f.Current); Assert.Same(other, f.Other);
        Assert.Empty(f.Sent); Assert.Empty(f.OtherSent); f.Data.AssertFiles(files);
        Assert.Equal(sql, await f.Database.SnapshotAsync()); f.AssertConverged();
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualMissingFixtureSqlTableIsContainedByAccountFrameWithoutReadyOrHandoff() => Run(async f =>
    {
        var files = f.Data.Files(); int tickets = f.TicketCount; var owner = f.Current; var other = f.Other;
        var holders = f.Store.Snapshot();
        var binding = owner.NativeGameClient!.OrdinarySpellBinding!;
        var otherBinding = other.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(otherBinding));
        await f.Database.RemoveAccountsForProviderFailureAsync();
        // Prove the real provider/repository failure separately from the public
        // dispatcher's intentional contain-and-log contract.
        var error = await Assert.ThrowsAsync<MySqlException>(async () =>
        {
            _ = await f.Database.CreateRepository().FindByUsernameAsync(OwnedModernSqlAuthDatabase.FirstUsername);
        });
        Assert.Equal(1146, error.Number);
        int diagnosticStart = f.Diagnostics.Length;
        var frames = await f.ObserveAuthenticationAsync(OwnedModernSqlAuthDatabase.FirstUsername,
            OwnedModernSqlAuthDatabase.SyntheticPassword);
        Assert.Empty(frames); Assert.Equal(tickets, f.TicketCount);
        var diagnostic = f.Diagnostics[diagnosticStart..];
        Assert.Contains("account-frame-dispatch", diagnostic);
        Assert.Contains("PARITY-BUG account runtime exception", diagnostic);
        Assert.Contains("exception=MySqlException:", diagnostic);
        Assert.DoesNotContain("AccountServer legacy login Ready", diagnostic);
        Assert.Same(owner, f.Current); Assert.Same(other, f.Other);
        Assert.Equal(holders.Count, f.Store.Snapshot().Count);
        foreach (var holder in holders)
        {
            Assert.True(f.Store.TryGet(holder.Connection, out var retained)); Assert.Same(holder.Session, retained);
        }
        Assert.Same(binding, f.Native.OrdinarySpellBinding);
        Assert.Same(otherBinding, f.Other.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(otherBinding));
        Assert.Empty(f.Sent); Assert.Empty(f.OtherSent); f.Data.AssertFiles(files); f.AssertConverged();
    });
}
