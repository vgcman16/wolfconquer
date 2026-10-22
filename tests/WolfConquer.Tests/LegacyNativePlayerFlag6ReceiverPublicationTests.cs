using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Physical Player/registration/death states are declared MODEL INPUTS, not earned Team flags.
    [Theory]
    [InlineData(true, LegacyMsgUpdateDataType.Hitpoints)]
    [InlineData(false, LegacyMsgUpdateDataType.Hitpoints)]
    [InlineData(true, LegacyMsgUpdateDataType.Stamina)]
    [InlineData(false, LegacyMsgUpdateDataType.Stamina)]
    public async Task NativeFlag6RevivePublicEntryRetainsOriginalReceiverAcrossPreStatusPlayerReplacement(
        bool originalVisible, LegacyMsgUpdateDataType replaceAfter)
    {
        var origin = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Local);
        var owner = new LegacyNativeGameClientRuntime();
        var original = owner.Player;
        if (originalVisible)
            Assert.True(NativeFlag6TestModel.AddFlagFixture(original, origin));
        var originalRow = NativeFlag6TestModel.Current(original);
        var replacement = new LegacyNativePlayerRuntime(owner);
        if (!originalVisible)
            Assert.True(NativeFlag6TestModel.AddFlagFixture(replacement, origin.AddSeconds(1)));
        var replacementRow = NativeFlag6TestModel.Current(replacement);
        Assert.Equal(originalVisible, NativeFlag6TestModel.Visible(original));
        Assert.Equal(!originalVisible, NativeFlag6TestModel.Visible(replacement));
        Assert.Same(original, owner.Player);
        Assert.NotSame(original, replacement);

        var frames = new List<byte[]>();
        var world = new WorldSessionStore();
        var replaced = false;
        var triggerCount = 0;
        ConquerTcpConnection connection = null!;
        connection = new ConquerTcpConnection(null, null,
            sendAsync: (bytes, _) =>
            {
                var saved = bytes.ToArray();
                frames.Add(saved);
                if (LegacyMsgUpdatePacket.TryParse(ReadFrame(saved), out var update) &&
                    (replaceAfter == LegacyMsgUpdateDataType.Hitpoints
                        ? update.Updates.Count == 2 &&
                          update.Updates[0].Type == LegacyMsgUpdateDataType.MaxHitpoints &&
                          update.Updates[1].Type == LegacyMsgUpdateDataType.Hitpoints
                        : update.Updates.Count == 1 && update.Updates[0].Type == replaceAfter))
                {
                    triggerCount++;
                    Assert.False(replaced); // No second matching setter/send is hidden by this fixture.
                    Assert.Same(original, owner.Player);
                    Assert.True(world.TryGet(connection, out var current));
                    Assert.NotNull(current);
                    Assert.Same(owner, current.NativeGameClient);
                    Assert.Same(original, current.NativePlayer);
                    owner.Player = replacement; // SAME physical client; explicit MODEL Player adoption.
                    replaced = true;
                    Assert.Same(replacement, current.NativePlayer);
                }
                return ValueTask.CompletedTask;
            }, nativeGameClient: owner);
        var session = CreateSession(minHitPoints: 0, spellIds: [],
            attributes: new Dictionary<string, string> { ["StatusFlags"] = "1056" }) with
            { NativeGameClient = owner };
        world.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, TextWriter.Null, session, Sessions: world);

        await PacketHandlers.HandleAsync(context, ReadFrame(new MsgActionPacket
            { ObjId = PlayerUid, Type = MsgActionType.Revive }.ToFrameBytes()));

        Assert.True(replaced);
        Assert.Equal(1, triggerCount);
        Assert.Equal(6, frames.Count);
        // Existing target layout: FIVE update frames, SIX entries, then the separate MapStatus.
        // This records inherited compatibility order, NOT complete native revive wire parity.
        var types = new[]
        {
            new[] { LegacyMsgUpdateDataType.MaxHitpoints },
            new[] { LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints },
            new[] { LegacyMsgUpdateDataType.Stamina },
            new[] { LegacyMsgUpdateDataType.StatusFlag },
            new[] { LegacyMsgUpdateDataType.Mesh },
        };
        var parsedUpdates = new List<LegacyMsgUpdatePacket>();
        LegacyMsgUpdatePacket? status = null;
        for (var index = 0; index < types.Length; index++)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update));
            Assert.Equal(PlayerUid, update.Uid); // Existing compatibility UID, NOT physical hydration proof.
            Assert.Equal(types[index].Length, update.Updates.Count);
            Assert.Equal(types[index], update.Updates.Select(entry => entry.Type).ToArray());
            parsedUpdates.Add(update);
            if (index == 3) status = update;
        }
        Assert.Equal(6, parsedUpdates.Sum(update => update.Updates.Count));
        var maximumHitpoints = Assert.Single(parsedUpdates[0].Updates);
        Assert.True(maximumHitpoints.Value > 0);
        Assert.Equal(maximumHitpoints.Value, parsedUpdates[1].Updates[0].Value);
        Assert.Equal(maximumHitpoints.Value, parsedUpdates[1].Updates[1].Value);
        Assert.Equal(100UL, Assert.Single(parsedUpdates[2].Updates).Value);
        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(frames[5]), out var mapStatus));
        Assert.Equal((uint)session.Character.MapId, mapStatus.Id);
        Assert.NotNull(status);
        var words = Assert.Single(status.Updates).Values!;
        Assert.Equal(18, words.Count);
        Assert.Equal(originalVisible ? LegacyNativePlayerFlag6Lifecycle.Mask : 0UL,
            words[0] & LegacyNativePlayerFlag6Lifecycle.Mask);
        Assert.Equal(0UL, words[0] & ((1UL << 5) | (1UL << 10)));
        Assert.True(world.TryGet(connection, out var revived));
        Assert.NotNull(revived);
        Assert.True(revived.Character.MinHitPoints > 0);
        Assert.Equal((ulong)revived.Character.MinHitPoints, maximumHitpoints.Value);
        Assert.Same(owner, revived.NativeGameClient);
        Assert.Same(replacement, revived.NativePlayer);
        Assert.Same(replacement, owner.Player);
        Assert.Same(originalRow, NativeFlag6TestModel.Current(original));
        Assert.Same(replacementRow, NativeFlag6TestModel.Current(replacement));
        Assert.Equal(originalVisible, NativeFlag6TestModel.Visible(original));
        Assert.Equal(!originalVisible, NativeFlag6TestModel.Visible(replacement));
        if (originalRow is not null) Assert.Equal(origin, originalRow.Timer);
        if (replacementRow is not null) Assert.Equal(origin.AddSeconds(1), replacementRow.Timer);
    }
}
