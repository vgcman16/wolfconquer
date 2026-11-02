using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task RealModernSqlAuthNativeSaleAndColdRelogPreserveExactAccountIdentity() => Run(async f =>
    {
        var database = await f.Database.SnapshotAsync();
        Assert.NotEqual(SqlWorldFixture.FirstUid + 1000, f.Current.Character.Uid);
        await AssertSuccessfulSale(f);
        long money = f.Current.Character.Money; var committedItems = File.ReadAllBytes(f.ItemsPath);
        var otherItems = File.ReadAllBytes(f.OtherItemsPath); long otherMoney = f.Other.Character.Money;
        await f.CloseWorldAsync(); await f.OpenWorldAsync();
        Assert.Equal(SqlWorldFixture.FirstUid, f.Current.AccountId);
        Assert.Equal(SqlWorldFixture.FirstUid, f.Current.Character.Uid);
        Assert.Equal(money, f.Current.Character.Money); Assert.Equal(committedItems, File.ReadAllBytes(f.ItemsPath));
        Assert.DoesNotContain(f.Current.PlayerItems.InventoryItems, row => row.Uid == SqlWorldFixture.SoldUid);
        Assert.Equal(otherMoney, f.Other.Character.Money); Assert.Equal(otherItems, File.ReadAllBytes(f.OtherItemsPath));
        f.AssertConverged();
        f.Sent.Clear(); await f.SellAsync(); Assert.Empty(f.Sent);
        Assert.Equal(money, f.Current.Character.Money); Assert.Equal(committedItems, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(database, await f.Database.SnapshotAsync());
        Assert.False(File.Exists(Path.Combine(f.Data.Users, (SqlWorldFixture.FirstUid + 1000) + ".ini")));
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualCharacterFileLeaseRefusesPairThenSameSqlActorRetriesExactlyOnce() => Run(async f =>
    {
        var files = f.Data.Files(); var database = await f.Database.SnapshotAsync(); var owner = f.Current;
        var nativeImage = f.Native.OrdinarySpellBinding!.Items.Image;
        using (var unavailable = new FileStream(f.UserPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await f.SellAsync();
            Assert.Empty(f.Sent); Assert.Same(owner, f.Current);
            Assert.Same(nativeImage, f.Native.OrdinarySpellBinding.Items.Image); Assert.True(nativeImage.IsCurrentPlain());
        }
        f.Data.AssertFiles(files); Assert.False(f.Runtime!.Recovery.IsBlocked);
        await AssertSuccessfulSale(f);
        Assert.Equal(database, await f.Database.SnapshotAsync());
        long money = f.Current.Character.Money; var items = File.ReadAllBytes(f.ItemsPath);
        await f.CloseWorldAsync(); await f.OpenWorldAsync();
        Assert.Equal(money, f.Current.Character.Money); Assert.Equal(items, File.ReadAllBytes(f.ItemsPath));
        f.Sent.Clear(); await f.SellAsync(); Assert.Empty(f.Sent); Assert.Equal(money, f.Current.Character.Money);
    });

    private static async Task AssertSuccessfulSale(SqlWorldFixture f)
    {
        var owner = f.Current; var other = f.Other;
        long expected = checked(owner.Character.Money + 300);
        var peerCharacter = File.ReadAllBytes(f.OtherUserPath); var peerItems = File.ReadAllBytes(f.OtherItemsPath);
        var spells = File.ReadAllBytes(Path.Combine(f.Data.Spells, SqlWorldFixture.FirstUid + ".bin"));
        int observed = 0; long starts = f.Peer!.WireStarts;
        f.BeforeSend = _ =>
        {
            Assert.Equal(expected, f.Current.Character.Money); f.AssertConverged();
            Assert.DoesNotContain(f.Current.PlayerItems.InventoryItems, row => row.Uid == SqlWorldFixture.SoldUid);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.Equal(spells, File.ReadAllBytes(Path.Combine(f.Data.Spells, SqlWorldFixture.FirstUid + ".bin")));
            Assert.Equal(peerCharacter, File.ReadAllBytes(f.OtherUserPath)); Assert.Equal(peerItems, File.ReadAllBytes(f.OtherItemsPath));
            observed++;
        };
        try { await f.SellAsync(); } finally { f.BeforeSend = null; }
        Assert.Equal(3, observed); Assert.Equal(3, f.Sent.Count); Assert.Equal(starts + 3, f.Peer.WireStarts);
        Assert.Equal(RemoveGolden(SqlWorldFixture.SoldUid), f.Sent[0]);
        foreach (var bytes in f.Sent.Skip(1))
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
            var money = Assert.Single(update.Updates);
            Assert.Equal(LegacyMsgUpdateDataType.Money, money.Type); Assert.Equal(checked((ulong)expected), money.Value);
        }
        Assert.Equal(f.Sent[1], f.Sent[2]);
        Assert.Equal(SqlWorldFixture.KeptUid, Assert.Single(f.Current.PlayerItems.InventoryItems).Uid);
        Assert.Same(other, f.Other); Assert.Empty(f.OtherSent);
        Assert.Equal(peerCharacter, File.ReadAllBytes(f.OtherUserPath)); Assert.Equal(peerItems, File.ReadAllBytes(f.OtherItemsPath));
        Assert.Contains("KeepBytes=startup-owned-value", File.ReadAllText(f.UserPath));
        var committed = f.Data.Files(); var current = f.Current;
        f.Sent.Clear(); await f.SellAsync(); Assert.Empty(f.Sent); Assert.Same(current, f.Current); f.Data.AssertFiles(committed);
        Assert.Equal(expected, f.Current.Character.Money); f.AssertConverged();
    }

    private static byte[] RemoveGolden(uint uid)
    {
        var bytes = new byte[91]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 91);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), WorldPacketOpcodes.MsgItemUsage);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 3); return bytes;
    }
}
