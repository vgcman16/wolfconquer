using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyPlayerSpawnActionProjectionTests
{
    // DATA / codec only. Field31 is still a protobuf numeric field; the
    // source-shaped projection changes its value, not the wire schema.
    [Theory]
    [InlineData("65786", "FA01", "FA8104", 250)]
    [InlineData("273344", "C057", "C0D710", 11200)]
    [InlineData("4294967295", "FFFF03", "FFFFFFFF0F", 65535)]
    public void HighServerActionProjectsOnlyItsLow16OutgoingValue(
        string saved, string projectedVarint, string fullVarint, int expected)
    {
        var snapshot = Snapshot(saved);
        var packet = LegacyPlayerSpawnPacket.FromSnapshot(snapshot);
        Assert.Equal((ulong)expected, packet.GetNumericField(31));
        Assert.Equal(ExpectedFrame(projectedVarint), packet.ToFrameBytes());
        Assert.Equal(uint.Parse(saved, System.Globalization.CultureInfo.InvariantCulture), LegacyActionState.RequireCurrent(snapshot));
        Assert.Equal(saved, snapshot.Attributes["Action"]);

        // The decoder/roundtrip remains full-width. An incoming DATA image is
        // not silently normalized, nor does it authorize a native transition.
        var unprojected = ExpectedFrame(fullVarint);
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(unprojected), out var parsed));
        Assert.Equal(ulong.Parse(saved, System.Globalization.CultureInfo.InvariantCulture), parsed.GetNumericField(31));
        Assert.Equal(unprojected, parsed.ToFrameBytes());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-number")]
    [InlineData("-1")]
    [InlineData("Sit")]
    public void ExistingUnboundCodecFallbackNeedsNoInventedAdmission(string? saved)
    {
        var snapshot = Snapshot(saved);
        var packet = LegacyPlayerSpawnPacket.FromSnapshot(snapshot);
        Assert.Equal(ExpectedFrame("00"), packet.ToFrameBytes());
        var session = new WorldSession(7, "DataOnly", snapshot, PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
        Assert.Equal(packet.ToFrameBytes(), LegacyPlayerSpawnPacket.FromSnapshot(session).ToFrameBytes());
        Assert.Null(session.NativeGameClient);
        if (saved is null) Assert.Empty(snapshot.Attributes);
        else Assert.Equal(saved, snapshot.Attributes["Action"]);
    }

    [Fact]
    public Task ActualCurrentSessionProjectsWithoutChangingStateAndRetiredOrMisboundSessionRefuses()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(minHitPoints: 1) with
        { Attributes = new Dictionary<string, string> { ["Action"] = "MagicDefender" } };
        return OwnedEquipmentBehaviorFixture.RunAsync(new(character, PlayerItemSnapshot.Empty),
            OwnedEquipmentBehaviorFixture.Catalog(), f =>
            {
                f.ActorFrames.Clear();
                var before = f.Current(); var files = f.Data.Files(); var native = before.NativeGameClient!;
                Assert.Equal(273344u, (uint)native.Player.Action);
                var packet = LegacyPlayerSpawnPacket.FromSnapshot(before);
                Assert.Equal(11200UL, packet.GetNumericField(31));
                Assert.Equal(LegacyPlayerSpawnPacket.FromSnapshot(before.Character).ToFrameBytes(), packet.ToFrameBytes());
                var fields = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["Action"] = "4294967295" };
                f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor), before with
                { Character = before.Character with { Attributes = fields } }, out var final);
                Assert.Equal(65535UL, LegacyPlayerSpawnPacket.FromSnapshot(final).GetNumericField(31));
                Assert.Equal(uint.MaxValue, (uint)native.Player.Action);
                Assert.Equal(uint.MaxValue, LegacyActionState.RequireCurrent(final.Character));
                Assert.Throws<InvalidOperationException>(() => LegacyPlayerSpawnPacket.FromSnapshot(before));
                Assert.Throws<InvalidOperationException>(() => LegacyPlayerSpawnPacket.FromSnapshot(final with { AccountId = 1 }));
                Assert.Same(final, f.Current()); f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames);
                Assert.True(f.Store.TryRemove(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out var retired));
                Assert.Same(final, retired);
                Assert.Throws<InvalidOperationException>(() => LegacyPlayerSpawnPacket.FromSnapshot(final));
                Assert.Equal(uint.MaxValue, (uint)native.Player.Action);
                f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames); return Task.CompletedTask;
            });
    }

    private static CharacterSnapshot Snapshot(string? saved) => OwnedEquipmentBehaviorFixture.Character() with
    {
        Uid = 7, Name = "A", Body = 1000, Face = 0, Hair = 0, Class = 0, FirstClass = 0,
        SecondClass = 0, Reborn = 0, Level = 1, X = 1, Y = 2, MinHitPoints = 0,
        Attributes = saved is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["Action"] = saved },
    };

    private static byte[] ExpectedFrame(string actionVarint)
    {
        // Independent complete2227 fixture: Mesh1000/UID7, ten zero flag words,
        // empty equipment, X1/Y2, Level1/BattlePower1, server2, seven names and
        // five HundredWeapons. Only the literal field31 varint varies.
        var payload = Convert.FromHexString(
            "08E8071007180020002800" +
            "3000300030003000300030003000300030003000" +
            "40004800500058006000D00100D80101E00102E80100F00100F801" + actionVarint +
            "900200980201A00200A80200B00200D002904ED80200" +
            "F80300800400880400900400980400A00400A80400D80401F80402" +
            "DA060141DA0600DA0600DA0600DA0600DA0600DA0600" +
            "E00601E80600E80600E80600E80600E80600F00600F80600800701880701C80700");
        var frame = new byte[payload.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2227);
        payload.CopyTo(frame, 4);
        return frame;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
