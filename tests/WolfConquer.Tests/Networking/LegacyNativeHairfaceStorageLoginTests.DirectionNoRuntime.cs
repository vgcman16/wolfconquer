using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NoRuntimeDirectionRefusesEscapedRepeatAndPreservesRealOwnerAndSavedImage(bool descriptorOnly)
    {
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, _) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            var client = birth.Connection.NativeGameClient!;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
            Assert.Null(typeof(WorldServerFrameHandler).GetField("_ownedAdmissionRuntime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));
            Assert.False(client.OnAutoAttack); Assert.Null(client.AutoAttack);
            byte[] saved = File.ReadAllBytes(input.FilePath);
            var physical = typeof(LegacyNativeGameClientRuntime).GetField(descriptorOnly ? "autoAttack" : "onAutoAttack", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var descriptor = new LegacyNativeInteractQuery { UID = Uid, OpponentUID = Uid + 1, AtkType = 2 };
            try
            {
                // Declared negative physical escape, never a real repeat or grant.
                physical.SetValue(client, descriptorOnly ? (object)descriptor : true);
                await wire.SendAsync(new MsgActionPacket { ObjId = Uid, Type = MsgActionType.ChangeDirection, Facing = 3 }.ToFrameBytes(), fixture.Token);
                var dispatch = await fixture.NextAsync();
                Assert.Same(accepted, dispatch.Before); Assert.Same(accepted, dispatch.After);
                Assert.Same(binding, client.OrdinarySpellBinding); Assert.False(binding.IsRetired);
                Assert.Equal(!descriptorOnly, client.OnAutoAttack);
                if (descriptorOnly) Assert.Same(descriptor, client.AutoAttack); else Assert.Null(client.AutoAttack);
                Assert.Equal("1", accepted.Character.Attributes["Angle"]);
                Assert.Equal(saved, File.ReadAllBytes(input.FilePath));
            }
            finally { physical.SetValue(client, descriptorOnly ? null : (object)false); }
        });
    }
    [Fact]
    public async Task NoRuntimeBlockedRecoveryCannotPublishDirection()
    {
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, _) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            var client = birth.Connection.NativeGameClient!;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
            byte[] saved = File.ReadAllBytes(input.FilePath);
            var recovery = AdmissionOwner<LegacyTradeRecoveryState>(handler, "_tradeRecovery");
            recovery.Block("directionNegativeOwnedRecovery");
            await wire.SendAsync(new MsgActionPacket { ObjId = Uid, Type = MsgActionType.ChangeDirection, Facing = 3 }.ToFrameBytes(), fixture.Token);
            var dispatch = await fixture.NextAsync();
            Assert.Same(accepted, dispatch.Before); Assert.Same(accepted, dispatch.After);
            Assert.Same(binding, client.OrdinarySpellBinding); Assert.False(binding.IsRetired);
            Assert.False(client.OnAutoAttack); Assert.Null(client.AutoAttack);
            Assert.Equal("1", accepted.Character.Attributes["Angle"]); Assert.Equal(saved, File.ReadAllBytes(input.FilePath));
        });
    }

    [Fact]
    public async Task NoRuntimeRetiredTransportCannotPublishDirectionOrRebindItsHolder()
    {
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, _) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            var client = birth.Connection.NativeGameClient!;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
            byte[] saved = File.ReadAllBytes(input.FilePath);
            await birth.Connection.CompleteTransportCloseAsync();
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection)); Assert.True(binding.IsRetired);
            var request = new MsgActionPacket { ObjId = Uid, Type = MsgActionType.ChangeDirection, Facing = 3 }.ToFrameBytes();
            await handler.HandleFrameAsync(birth.Connection, OwnedEquipmentBehaviorFixture.ReadFrame(request));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection)); Assert.True(binding.IsRetired);
            Assert.Equal("1", accepted.Character.Attributes["Angle"]); Assert.Equal(saved, File.ReadAllBytes(input.FilePath));
            GC.KeepAlive(wire);
        });
    }
}



