using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompleteLoginPublishesPhysicalCompletionOnlyAfterFinalStartupSend(bool forgedProjection)
    {
        const uint uid = 1_299_181;
        Seed(uid);
        if (forgedProjection) File.AppendAllText(UserPath(uid), "CompleteLogin=1\n");
        var owner = NewOwner();
        Player? player = null;
        var armed = false;
        Exception? callbackFailure = null;
        var observed = new List<(bool Physical, bool Issued)>();
        player = await Login(owner, uid, bytes =>
        {
            try
            {
                if (armed && IsCompleteLoginAbility(bytes))
                    observed.Add((player!.Connection.NativeGameClient!.Player.CompleteLogin,
                        LegacyCompleteLoginRuntime.IsComplete(player.Connection, Current(owner, player))));
            }
            catch (Exception ex) { callbackFailure ??= ex; }
        });
        var native = player.Connection.NativeGameClient!.Player;
        Assert.False(native.CompleteLogin);
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(player.Connection, Current(owner, player)));
        Assert.Equal(forgedProjection, HasCompleteLoginProjection(Current(owner, player)));
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;
        armed = true;

        await CompleteLogin(owner, player);

        Assert.Null(callbackFailure);
        Assert.Equal((false, false), Assert.Single(observed));
        var burst = player.Sent.Skip(first).ToArray();
        Assert.True(burst.Length >= 3);
        Assert.Equal(WorldPacketOpcodes.LegacyMsgUpdate, ReadFrame(burst[0]).Header.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyMsgVipStatus, ReadFrame(burst[1]).Header.Opcode);
        Assert.True(LegacyUserAbilityScorePacket.TryParse(ReadFrame(burst[^1]), out var ability));
        Assert.Equal(LegacyUserAbilityScorePacket.ResponseType, ability.Type);
        Assert.Equal(uid, ability.Uid);
        Assert.Equal(100U, ability.Level);
        Assert.True(native.CompleteLogin);
        Assert.True(LegacyCompleteLoginRuntime.IsComplete(player.Connection, Current(owner, player)));
        Assert.True(HasCompleteLoginProjection(Current(owner, player)));

        await CompleteLogin(owner, player);

        Assert.Null(callbackFailure);
        Assert.Equal(2, observed.Count);
        Assert.Equal((true, true), observed[1]);
        Assert.Same(native, player.Connection.NativeGameClient.Player);
        Assert.True(native.CompleteLogin);
        Assert.True(LegacyCompleteLoginRuntime.IsComplete(player.Connection, Current(owner, player)));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
    }

    [Fact]
    public async Task CompleteLoginMergesWalletAndOrdinaryStatePublishedDuringStartup()
    {
        const uint uid = 1_299_182;
        Seed(uid);
        var owner = NewOwner();
        Player? player = null;
        var armed = false;
        var changed = false;
        Exception? callbackFailure = null;
        WorldSession? newer = null;
        player = await Login(owner, uid, bytes =>
        {
            try
            {
                if (!armed || changed || ReadFrame(bytes).Header.Opcode != WorldPacketOpcodes.LegacyMsgVipStatus) return;
                changed = true;
                newer = CompleteLoginWalletChange(Current(owner, player!));
                owner.Sessions.Set(player!.Connection, newer);
            }
            catch (Exception ex) { callbackFailure ??= ex; }
        });
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        armed = true;

        await CompleteLogin(owner, player);

        Assert.Null(callbackFailure);
        Assert.True(changed);
        Assert.NotNull(newer);
        var completed = Current(owner, player);
        AssertCompleteLoginWalletChange(completed);
        Assert.Same(newer.PlayerItems, completed.PlayerItems);
        Assert.True(HasCompleteLoginProjection(completed));
        Assert.True(player.Connection.NativeGameClient!.Player.CompleteLogin);
        Assert.True(LegacyCompleteLoginRuntime.IsComplete(player.Connection, completed));
        var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid)));
        Assert.Equal(900, saved.Money);
        Assert.Equal("150", saved.Attributes["WHMoney"]);
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
    }

    [Fact]
    public async Task CompleteLoginRegistryResolvesCurrentOwnerInsteadOfStaleContextSnapshot()
    {
        const uint uid = 1_299_183;
        Seed(uid);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var stale = Current(owner, player);
        var walletChanged = CompleteLoginWalletChange(stale);
        var newer = walletChanged with
        {
            Character = walletChanged.Character with
            {
                Level = 101,
                Attributes = new Dictionary<string, string>(walletChanged.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                {
                    ["VipLevel"] = "6",
                },
            },
        };
        owner.Sessions.Set(player.Connection, newer);
        var first = player.Sent.Count;
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(player.Connection, owner.Log, stale,
            WorldData: CreateWorldData(900), Sessions: owner.Sessions,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        // This deliberately supplies an old context to the actual Data132
        // registry handler; it is not an alternate login/transport fixture.
        await registry.HandleAsync(context, CompleteLoginFrame(uid), player.Lifetime.Token);

        var completed = Current(owner, player);
        AssertCompleteLoginWalletChange(completed);
        Assert.True(HasCompleteLoginProjection(completed));
        Assert.True(player.Connection.NativeGameClient!.Player.CompleteLogin);
        Assert.True(LegacyCompleteLoginRuntime.IsComplete(player.Connection, completed));
        Assert.False(HasCompleteLoginProjection(stale));
        Assert.Equal(101, completed.Character.Level);
        var startup = player.Sent.Skip(first).ToArray();
        var vipBytes = Assert.Single(startup, bytes => LegacyVipStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyVipStatusPacket.TryParse(ReadFrame(vipBytes), out var vip));
        Assert.Equal(LegacyVipStatusFlags.FullVip, vip.Flags);
        var abilityBytes = Assert.Single(startup, IsCompleteLoginAbility);
        Assert.True(LegacyUserAbilityScorePacket.TryParse(ReadFrame(abilityBytes), out var ability));
        Assert.Equal(101U, ability.Level);
        var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid)));
        Assert.Equal(900, saved.Money);
        Assert.Equal(101, saved.Level);
        Assert.Equal("150", saved.Attributes["WHMoney"]);
    }

    [Fact]
    public async Task CompleteLoginUnsupportedInterserverBranchDoesNotIssueNativeCompletion()
    {
        const uint uid = 1_299_184;
        Seed(uid);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var session = Current(owner, player);
        var attributes = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["OnInterServer"] = "1",
            ["CompleteLogin"] = "1",
        };
        var held = session with { Character = session.Character with { Attributes = attributes } };
        owner.Sessions.Set(player.Connection, held);
        var first = player.Sent.Count;
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));

        await CompleteLogin(owner, player);

        Assert.Equal(first, player.Sent.Count);
        Assert.Same(held, Current(owner, player));
        Assert.True(HasCompleteLoginProjection(held));
        Assert.False(player.Connection.NativeGameClient!.Player.CompleteLogin);
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(player.Connection, held));
        Assert.Contains("PARITY-GAP MsgAction CompleteLogin interserver no-op", owner.Log.ToString());
        Assert.DoesNotContain("WorldServer handled MsgAction CompleteLogin", owner.Log.ToString());
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
    }

    private async Task CompleteLogin(Owner owner, Player player, CancellationToken? cancellationToken = null) =>
        await owner.Handler.HandleFrameAsync(player.Connection, CompleteLoginFrame(player.Uid),
            cancellationToken ?? player.Lifetime.Token);

    private static PacketFrame CompleteLoginFrame(uint uid) => ReadFrame(new MsgActionPacket
    {
        ObjId = uid,
        Type = MsgActionType.CompleteLogin,
    }.ToFrameBytes());

    private static bool IsCompleteLoginAbility(byte[] bytes) =>
        LegacyUserAbilityScorePacket.TryParse(ReadFrame(bytes), out var packet) &&
        packet.Type == LegacyUserAbilityScorePacket.ResponseType;

    private static bool HasCompleteLoginProjection(WorldSession session) =>
        session.Character.Attributes.TryGetValue("CompleteLogin", out var value) && value == "1";

    private static WorldSession CompleteLoginWalletChange(WorldSession current) => current with
    {
        Character = current.Character with
        {
            Money = 900,
            Attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["WHMoney"] = "150",
                ["CompleteLoginMidBurstState"] = "retained",
            },
        },
        ActiveNpcId = 777,
    };

    private static void AssertCompleteLoginWalletChange(WorldSession current)
    {
        Assert.Equal(900, current.Character.Money);
        Assert.Equal("150", current.Character.Attributes["WHMoney"]);
        Assert.Equal("retained", current.Character.Attributes["CompleteLoginMidBurstState"]);
        Assert.Equal(777U, current.ActiveNpcId);
    }
}
