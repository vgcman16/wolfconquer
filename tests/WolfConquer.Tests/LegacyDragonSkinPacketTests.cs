using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDragonSkinPacketTests
{
    [Fact]
    public void LegacyDragonSkinOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2415, WorldPacketOpcodes.LegacyAnimaSkin);
        Assert.Equal(WorldPacketOpcodes.LegacyAnimaSkin, LegacyDragonSkinPacket.Opcode);
    }

    [Fact]
    public void LegacyDragonSkinTypeValuesMatchLegacyTypeIdEnum()
    {
        Assert.Equal(0U, (uint)LegacyDragonSkinType.MyShow);
        Assert.Equal(1U, (uint)LegacyDragonSkinType.PlayerView);
        Assert.Equal(2U, (uint)LegacyDragonSkinType.Unlock);
        Assert.Equal(3U, (uint)LegacyDragonSkinType.Retrieve);
        Assert.Equal(4U, (uint)LegacyDragonSkinType.Select);
        Assert.Equal(5U, (uint)LegacyDragonSkinType.Restore);
        Assert.Equal(6U, (uint)LegacyDragonSkinType.Effect);
    }

    [Fact]
    public void LegacyDragonSkinStartupRestoresWhenNoSkinIsEquipped()
    {
        var packet = LegacyDragonSkinPacket.CreateStartup(CreateSnapshot("0/2/1/2/"));

        Assert.Equal(LegacyDragonSkinType.Restore, packet.Type);
        Assert.Equal(1_293_600U, packet.UserID);
        Assert.Equal(0U, packet.SkinType);
        Assert.True(packet.OK);
        Assert.Equal(
            "0c006f09080510a0fa4e2001",
            Convert.ToHexString(packet.ToFrameBytes()).ToLowerInvariant());

        var frame = ReadFrame(packet.ToFrameBytes());
        Assert.True(LegacyDragonSkinPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyDragonSkinStartupRestoresWhenAnimaSkinIsMissing()
    {
        var packet = LegacyDragonSkinPacket.CreateStartup(CreateSnapshot(animaSkin: null));

        Assert.Equal(LegacyDragonSkinType.Restore, packet.Type);
        Assert.Equal(1_293_600U, packet.UserID);
        Assert.Equal(0U, packet.SkinType);
        Assert.True(packet.OK);
    }

    [Fact]
    public void LegacyDragonSkinStartupShowsEquippedSkin()
    {
        var packet = LegacyDragonSkinPacket.CreateStartup(CreateSnapshot("3/3/1/2/3/"));

        Assert.Equal(LegacyDragonSkinType.MyShow, packet.Type);
        Assert.Equal(1_293_600U, packet.UserID);
        Assert.Equal(3U, packet.SkinType);
        Assert.False(packet.OK);
        Assert.Equal(
            "0a006f0910a0fa4e1803",
            Convert.ToHexString(packet.ToFrameBytes()).ToLowerInvariant());
    }

    [Fact]
    public void LegacyDragonSkinStateParsesLegacyAnimaSkinSnapshot()
    {
        var state = LegacyDragonSkinState.FromSnapshot(CreateSnapshot("3/3/1/2/3/"));

        Assert.Equal(3U, state.EquippedSkin);
        Assert.Equal([1U, 2U, 3U], state.OwnedSkins);
        Assert.True(state.Owns(2));
        Assert.Equal("3/3/1/2/3/", state.ToSnapshotValue());
    }

    [Fact]
    public void LegacyDragonSkinStateUsesLegacyDefaultsForMissingSnapshot()
    {
        var state = LegacyDragonSkinState.FromSnapshot(CreateSnapshot(animaSkin: null));

        Assert.Equal(0U, state.EquippedSkin);
        Assert.Empty(state.OwnedSkins);
        Assert.False(state.Owns(1));
        Assert.Equal("0/0/", state.ToSnapshotValue());
    }

    [Fact]
    public void LegacyDragonSkinStateUpdatesEquippedSkinOnlyWhenOwned()
    {
        var state = LegacyDragonSkinState.FromSnapshot(CreateSnapshot("1/2/1/2/"));

        Assert.Equal(2U, state.WithEquipped(2).EquippedSkin);
        Assert.Equal(1U, state.WithEquipped(3).EquippedSkin);
        Assert.Equal(0U, state.Restore().EquippedSkin);
    }

    [Fact]
    public void LegacyDragonSkinStateAddsOwnedSkinWithoutEquipping()
    {
        var state = LegacyDragonSkinState.FromSnapshot(CreateSnapshot("0/1/1/"));

        var updated = state.AddOwned(2);

        Assert.Equal(0U, updated.EquippedSkin);
        Assert.Equal([1U, 2U], updated.OwnedSkins);
        Assert.Equal("0/2/1/2/", updated.ToSnapshotValue());
        Assert.Same(updated, updated.AddOwned(2));
    }

    [Fact]
    public void LegacyDragonSkinStateRemovesOwnedSkinAndClearsEquippedWhenRetrieved()
    {
        var state = LegacyDragonSkinState.FromSnapshot(CreateSnapshot("2/3/1/2/3/"));

        var updated = state.RemoveOwned(2);

        Assert.Equal(0U, updated.EquippedSkin);
        Assert.Equal([1U, 3U], updated.OwnedSkins);
        Assert.Equal("0/2/1/3/", updated.ToSnapshotValue());
        Assert.Same(updated, updated.RemoveOwned(2));
    }

    [Fact]
    public void LegacyDragonSkinPacketRoundTrips()
    {
        var packet = new LegacyDragonSkinPacket(
            LegacyDragonSkinType.Effect,
            UserID: 1_293_600,
            SkinType: 7,
            OK: true);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyDragonSkinPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyDragonSkinPacketParsesOmittedDefaultFields()
    {
        var frame = ReadFrame(LegacyDragonSkinPacket.CreateStartup(CreateSnapshot("3/3/1/2/3/")).ToFrameBytes());

        Assert.True(LegacyDragonSkinPacket.TryParse(frame, out var parsed));

        Assert.Equal(LegacyDragonSkinType.MyShow, parsed.Type);
        Assert.Equal(1_293_600U, parsed.UserID);
        Assert.Equal(3U, parsed.SkinType);
        Assert.False(parsed.OK);
    }

    private static CharacterSnapshot CreateSnapshot(string? animaSkin)
    {
        var animaSkinLine = animaSkin is null ? string.Empty : $"AnimaSkin={animaSkin}";
        return CharacterSnapshotParser.ParseText(
            $$"""
            [Character]
            UID=1293600
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0
            {{animaSkinLine}}
            """);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
