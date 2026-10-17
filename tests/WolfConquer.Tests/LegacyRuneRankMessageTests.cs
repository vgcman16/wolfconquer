using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRuneRankMessageTests
{
    [Fact]
    public void LegacyRuneRankMessageWritesUnrankedStartupTalk()
    {
        var packet = LegacyRuneRankMessage.CreateStartup(Snapshot(), PlayerItems(), []);

        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));
        Assert.Equal(LegacyMsgMessagePacket.RuneRankChatType, parsed.ChatType);
        Assert.Equal("My ranking: Unranked; Current rune pack score: 2000", parsed.Content[3]);
    }

    [Fact]
    public void LegacyRuneRankMessageWritesRankedStartupTalk()
    {
        var rankings = new[]
        {
            Ranking("First", 5001),
            Ranking("HeroOne", 1293600),
        };

        var packet = LegacyRuneRankMessage.CreateStartup(Snapshot(), PlayerItems(), rankings);

        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));
        Assert.Equal(LegacyMsgMessagePacket.RuneRankChatType, parsed.ChatType);
        Assert.Equal("My ranking: 2; Current rune pack score: 2000", parsed.Content[3]);
    }

    private static CharacterSnapshot Snapshot()
    {
        return CharacterSnapshotParser.ParseText("""
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
            """);
    }

    private static PlayerItemSnapshot PlayerItems()
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: [],
            RuneItems:
            [
                new PlayerItemSnapshotItem { Uid = 1001, ItemId = 402005, Position = 101 },
            ]);
    }

    private static RuneRankingRecord Ranking(string name, int characterId)
    {
        return new RuneRankingRecord(
            CharacterName: name,
            Score: 1,
            CharacterId: characterId,
            SourceFile: "RuneRanking.txt",
            LineNumber: 1,
            RawLine: "",
            Fields: []);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
