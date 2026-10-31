using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task RealAccountAuthenticationCreatesAndArchivesOnceBeforeNativeOutputAndRelogDoesNotGrantAgain() => Run(async f =>
    {
        await CreateAndAssert(f); var committed = f.Data.Files(); var holder = f.Current; int first = f.Sent.Count;
        await f.CreateAsync(); Assert.Equal(first, f.Sent.Count); Assert.Same(holder, f.Current); f.Data.AssertFiles(committed);
        await f.CloseAsync(); using (f.Open()) f.Data.AssertFiles(committed);
        await f.ReopenFrameAsync(() => f.Data.AssertFiles(committed));
        AssertCurrentCreated(f, committed[Path.GetRelativePath(f.Data.Root, f.ItemsPath)]);
        f.Data.AssertFiles(committed); _ = CompletedIntent(f);
    });

    [Fact]
    public Task ARealNewbornBankCommitAndDifferentRuneOwnerCanProgressWithoutReplayingCompletedCreation() => Run(async f =>
    {
        await CreateAndAssert(f); await DepositNewbornMoney(f);
        var newborn = File.ReadAllBytes(f.CharacterPath); var intent = CompletedIntent(f); var payload = intent.CopyPayload();
        await f.ConnectRuneActorAsync(); var actor = f.CurrentFor(f.RuneConnection!); var highWater = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!["HighWater"]!.GetValue<uint>();
        var request = DecomposeRequest();
        await f.SendOnAsync(f.RuneConnection!, request);
        var updated = f.CurrentFor(f.RuneConnection!); Assert.DoesNotContain(updated.PlayerItems.RuneItems, row => row.Uid == CreationFixture.RuneUid);
        Assert.Equal(highWater + 2, JsonNode.Parse(File.ReadAllBytes(f.Data.State))!["HighWater"]!.GetValue<uint>());
        Assert.Equal(newborn, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(payload, CompletedIntent(f).CopyPayload());
        Assert.Equal(99_300, f.Current.Character.Money); Assert.Equal("700", f.Current.Character.Attributes["WHMoney"]);
        Assert.Equal(actor.PlayerSpells.Spells, updated.PlayerSpells.Spells); Assert.False(f.Runtime.Recovery.IsBlocked);
        var settled = f.Data.Files(); await f.CloseAsync(); using (f.Open()) f.Data.AssertFiles(settled);
        using (f.Open()) f.Data.AssertFiles(settled);
    });

    private static byte[] DecomposeRequest()
    {
        var writer = new WolfConquer.Core.Packets.PacketWriter(WorldPacketOpcodes.MsgItemUsage, MsgItemUsagePacket.PayloadLength + sizeof(uint));
        writer.WriteUInt32(0, 9_999_999); writer.WriteUInt16(12, (ushort)MsgItemUsageAction.DecomposeRune);
        writer.WriteUInt32(14, 511_043_495); writer.WriteUInt32(18, 1); writer.WriteUInt32(MsgItemUsagePacket.PayloadLength, CreationFixture.RuneUid);
        return writer.ToArray();
    }
}
