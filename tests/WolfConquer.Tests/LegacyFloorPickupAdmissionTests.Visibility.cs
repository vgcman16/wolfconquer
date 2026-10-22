using System.Collections.Concurrent;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyFloorPickupAdmissionTests
{
    // Registered D13 handler + disposable managed files, not actual-client or SQL
    // proof. Native FloorMap's guessed-UID lookup does not deny another DynamicID.
    [Theory]
    [InlineData(88u, false)]
    [InlineData(77u, true)]
    public async Task RealPickupPersistsMoneyAcrossInstanceLookupButRemovesOnlyFromCapturedFloorScope(
        uint pickerDynamicId, bool pickerReceivesRemoval)
    {
        var f = Create();
        var characterText = File.ReadAllText(f.CharacterPath) + "\nDynamicID=" +
            pickerDynamicId.ToString(CultureInfo.InvariantCulture) + "\n";
        File.WriteAllText(f.CharacterPath, characterText);
        var current = Current(f) with { Character = CharacterSnapshotParser.ParseText(characterText) };
        f.Sessions.Set(f.Connection, current);
        Assert.Same(f.Client, current.NativeGameClient);
        Assert.Same(f.Client.Player, current.NativePlayer);
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(f.Connection, current, f.Context.LocalServerIdentity));

        Assert.True(f.Floor.TryRemoveVisible(FloorUid, out var removed));
        Assert.Same(f.Entry, removed);
        var scoped = removed with { DynamicId = 77u };
        f.Floor.AddVisible(scoped);
        f = f with { Entry = scoped };
        var sameFloorScope = AddFloorVisibilityPeer(f, Uid + 1, 77u);
        var otherFloorScope = AddFloorVisibilityPeer(f, Uid + 2, 88u);
        var itemBytesBefore = File.ReadAllBytes(f.ItemPath);

        await Pickup(f.Context); // Deliberately stale supplied context; current holder is authoritative.

        Assert.Equal(ConquerProtocolProfile.VisibleDx8D13, f.Context.ProtocolProfile);
        var updated = Current(f);
        Assert.Equal(350, updated.Character.Money); // Existing100 + actual floor amount250.
        Assert.Equal(50, updated.Character.ConquerPoints);
        Assert.Equal(pickerDynamicId.ToString(CultureInfo.InvariantCulture), updated.Character.Attributes["DynamicID"]);
        Assert.Empty(f.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.False(f.Floor.TryGetVisibleEntry(FloorUid, out _));
        AssertPersistedAssets(f);
        Assert.Equal(itemBytesBefore, File.ReadAllBytes(f.ItemPath));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.Same(f.Client, updated.NativeGameClient);
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(f.Connection, updated, f.Context.LocalServerIdentity));
        Assert.False(f.Client.Player.IsCheckedPass);
        Assert.False(f.Client.Player.CompleteLogin);

        Assert.Equal(pickerReceivesRemoval ? 4 : 3, f.Sent.Count);
        Assert.Equal(2, f.Sent.Count(bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == Uid && packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.Money && update.Value == 350UL)));
        Assert.Contains(f.Sent, bytes => LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content.Contains("You have picked up a 250 silvers."));

        var expectedRemoval = (scoped.Packet with { DropType = LegacyFloorItemDropType.Remove }).ToFrameBytes();
        var pickerRemovals = f.Sent.Where(bytes => LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.DropType == LegacyFloorItemDropType.Remove).ToArray();
        if (pickerReceivesRemoval) Assert.Equal(expectedRemoval, Assert.Single(pickerRemovals));
        else Assert.Empty(pickerRemovals);
        Assert.Equal(expectedRemoval, Assert.Single(sameFloorScope.Sent));
        Assert.Empty(otherFloorScope.Sent);
        Assert.True(f.Sessions.TryGet(sameFloorScope.Connection, out _));
        Assert.True(f.Sessions.TryGet(otherFloorScope.Connection, out _));
        GC.KeepAlive(sameFloorScope.Connection);
        GC.KeepAlive(otherFloorScope.Connection);
    }

    private static FloorVisibilityPeer AddFloorVisibilityPeer(Fixture f, uint uid, uint dynamicId)
    {
        var sent = new ConcurrentQueue<byte[]>();
        var connection = new ConquerTcpConnection(null, null, (bytes, _) =>
        {
            // The real view removal must follow the durable wallet and floor commit.
            Assert.Equal(350, CharacterSnapshotParser.ParseText(File.ReadAllText(f.CharacterPath)).Money);
            Assert.Empty(f.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
            sent.Enqueue(bytes.ToArray());
            return ValueTask.CompletedTask;
        });
        var current = Current(f);
        var character = current.Character with
        {
            Uid = uid,
            Name = "FloorVisibilityPeer" + uid,
            Attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture) },
        };
        f.Sessions.Set(connection, new WorldSession(uid, "floor-visibility-peer", character,
            PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty));
        return new FloorVisibilityPeer(connection, sent);
    }

    // SessionStore weak keys do not own connection lifetimes. This test owns its
    // live peer handles through the await and every outbound assertion.
    private sealed record FloorVisibilityPeer(ConquerTcpConnection Connection, ConcurrentQueue<byte[]> Sent);
}
