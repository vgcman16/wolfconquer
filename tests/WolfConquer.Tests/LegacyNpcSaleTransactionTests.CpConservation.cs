using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(4294967295L, 1L, 2294967296L, false)]
    [InlineData(4294967295L, 1L, 2294967296L, true)]
    [InlineData(2000000000L, 4294967295L, 4294967295L, false)]
    [InlineData(2000000000L, 4294967295L, 4294967295L, true)]
    [InlineData(4294967295L, 4294967295L, 6589934590L, false)]
    [InlineData(4294967295L, 4294967295L, 6589934590L, true)]
    public async Task PickupConservesWideCpTotalsAndEntireBankTransferBeforeFirstReply(long cps,
        long amount, long transferred, bool exactBankCapacity)
    {
        const uint uid = 1_299_993;
        const long inventoryCap = 2_000_000_000;
        var bank = exactBankCapacity ? checked(long.MaxValue - transferred) : 444L;
        Seed(uid, warehouseAlias: true);
        ReplaceCharacterField(uid, "ConquerPoints", cps.ToString(CultureInfo.InvariantCulture));
        ReplaceCharacterField(uid, "BankCps", bank.ToString(CultureInfo.InvariantCulture));
        var owner = NewOwner(identity: new(42));
        Player? player = null;
        WorldSession? before = null;
        FloorItemRuntimeEntry? entry = null;
        byte[]? characterBytes = null;
        var expected = new PersistencePickupWallet(1_000, inventoryCap, 50, checked(bank + transferred));
        bool armed = false;
        int observations = 0;
        Exception? callbackFailure = null;
        player = await Login(owner, uid, sentFrame =>
        {
            if (!armed || observations != 0) return;
            observations++;
            callbackFailure = Record.Exception(() =>
            {
                Assert.NotNull(before); Assert.NotNull(entry); Assert.NotNull(characterBytes);
                AssertPersistencePickupCommitted(owner, player!, before, entry, characterBytes, expected);
                var native = player!.Connection.NativeGameClient!;
                Assert.Equal((uint)inventoryCap, native.Player.ConquerPoints);
                Assert.Equal((ulong)expected.BankCps, native.ConquerPointsSave);
                Assert.Equal(before.Character.BoundConquerPoints, native.Player.BoundConquerPoints);
            });
        });
        Assert.NotNull(player.Connection.NativeGameClient!.OrdinarySpellBinding);
        before = Current(owner, player);
        entry = SeedPersistencePickupFloor(owner, player, disposition: 2, amount: amount);
        characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;
        armed = true;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        Assert.Equal(1, observations); Assert.Null(callbackFailure);
        AssertPersistencePickupCommitted(owner, player, before, entry, characterBytes, expected);
        Assert.Equal((decimal)cps + bank + amount, (decimal)expected.ConquerPoints + expected.BankCps);
        var frames = player.Sent.Skip(first).ToArray();
        AssertPersistencePickupFrames(frames, entry, expected);
        var boxFrame = Assert.Single(frames, bytes => LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(boxFrame), out var box));
        Assert.Equal(transferred.ToString(CultureInfo.InvariantCulture) +
            " Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.", box.Text);
        Assert.Equal(0U, box.Unknown); Assert.Equal(ushort.MaxValue, box.InputMaxLength);
        Assert.Equal((byte)0, box.OptionId); Assert.Equal(LegacyNpcReplyInteractType.MessageBox, box.InteractType);
        var walletFrame = Assert.Single(frames, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Updates.Any(value => value.Type == LegacyMsgUpdateDataType.ConquerPoints));
        Assert.Equal(new LegacyMsgUpdatePacket(uid,
            [new(LegacyMsgUpdateDataType.ConquerPoints, (ulong)inventoryCap)]).ToFrameBytes(), walletFrame);
        Assert.True(Array.IndexOf(frames, boxFrame) < Array.IndexOf(frames, walletFrame));
        Assert.Equal((uint)inventoryCap, player.Connection.NativeGameClient!.Player.ConquerPoints);
        Assert.Equal((ulong)expected.BankCps, player.Connection.NativeGameClient.ConquerPointsSave);
        await AssertPersistencePickupReplayIsSilent(owner, player);
        Assert.Equal((uint)inventoryCap, player.Connection.NativeGameClient.Player.ConquerPoints);
        Assert.Equal((ulong)expected.BankCps, player.Connection.NativeGameClient.ConquerPointsSave);
    }

    [Theory]
    [InlineData(2000000000L, 1L, 1L)]
    [InlineData(4294967295L, 1L, 2294967296L)]
    [InlineData(4294967295L, 4294967295L, 6589934590L)]
    public async Task WideCpBankOverflowRefusesBeforeClaimJournalFilesNativeTupleOrOutput(long cps,
        long amount, long transferred)
    {
        const uint uid = 1_299_995;
        var bank = checked(long.MaxValue - (transferred - 1)); // Exactly one less than the required room.
        Seed(uid, warehouseAlias: true);
        ReplaceCharacterField(uid, "ConquerPoints", cps.ToString(CultureInfo.InvariantCulture));
        ReplaceCharacterField(uid, "BankCps", bank.ToString(CultureInfo.InvariantCulture));
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid);
        Assert.NotNull(player.Connection.NativeGameClient!.OrdinarySpellBinding);
        var before = Current(owner, player);
        var entry = SeedPersistencePickupFloor(owner, player, disposition: 2, amount: amount);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var allFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        var native = player.Connection.NativeGameClient!;
        var nativeBefore = (native.Player.ConquerPoints, native.Player.BoundConquerPoints,
            native.ConquerPointsSave, native.Player.Money, native.Player.WHMoney);
        var first = player.Sent.Count;
        Assert.False(LegacyFloorPickupWallet.TryAdd(before.Character, FloorItemRuntimePickupKind.ConquerPoints,
            amount, out var rejected, out var reason));
        Assert.Null(rejected); Assert.Equal("floorPickupCurrencyOverflow", reason);

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPersistencePickupDenied(owner, player, before, entry, characterBytes, itemBytes, first);
        Assert.Equal(nativeBefore, (native.Player.ConquerPoints, native.Player.BoundConquerPoints,
            native.ConquerPointsSave, native.Player.Money, native.Player.WHMoney));
        var actualPaths = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path)).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal(allFiles.Keys.Order(StringComparer.OrdinalIgnoreCase), actualPaths);
        foreach (var file in allFiles) Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(root, file.Key)));
        Assert.Equal(FloorItemRuntimePickupStatus.Available, DropFloor(owner).GetPickupStatus(
            PersistencePickupFloorUid, uid, DateTimeOffset.UtcNow, out var retained));
        Assert.Same(entry, retained);
    }
}
