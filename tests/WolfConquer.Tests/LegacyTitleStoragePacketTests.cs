using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTitleStoragePacketTests
{
    [Fact]
    public void LegacyTitleStorageOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2313, WorldPacketOpcodes.LegacyMsgTitleStorage);
        Assert.Equal(WorldPacketOpcodes.LegacyMsgTitleStorage, LegacyTitleStoragePacket.Opcode);
    }

    [Fact]
    public void LegacyTitleStorageActionsMatchLegacyValues()
    {
        Assert.Equal(0U, (uint)LegacyTitleStorageAction.UpdateScore);
        Assert.Equal(1U, (uint)LegacyTitleStorageAction.UseTitle);
        Assert.Equal(3U, (uint)LegacyTitleStorageAction.RemoveTitle);
        Assert.Equal(4U, (uint)LegacyTitleStorageAction.Equip);
        Assert.Equal(5U, (uint)LegacyTitleStorageAction.UnEquip);
        Assert.Equal(6U, (uint)LegacyTitleStorageAction.FullLoad);
    }

    [Fact]
    public void LegacyTitleStorageWritesCapturedRadiantWingsRemovalFrames()
    {
        var first = new LegacyTitleStoragePacket(
            LegacyTitleStorageAction.UnEquip,
            DwParam1: 0,
            DwParam2: 6002,
            DwParam3: 21);
        var second = new LegacyTitleStoragePacket(
            LegacyTitleStorageAction.UnEquip,
            DwParam1: 59270,
            DwParam2: 6002,
            DwParam3: 21,
            new LegacyTitleStorageTitle(6002, 21));

        Assert.Equal("0d0009090805100018f22e2015", Convert.ToHexString(first.ToFrameBytes()).ToLowerInvariant());
        Assert.Equal(
            "1a00090908051086cf0318f22e20152a0908f22e101518002000",
            Convert.ToHexString(second.ToFrameBytes()).ToLowerInvariant());

        Assert.True(LegacyTitleStoragePacket.TryParse(ReadFrame(second.ToFrameBytes()), out var parsed));
        Assert.Equal(LegacyTitleStorageAction.UnEquip, parsed.Action);
        Assert.Equal(59270U, parsed.DwParam1);
        Assert.Equal(6002U, parsed.DwParam2);
        Assert.Equal(21U, parsed.DwParam3);
        Assert.Equal(new LegacyTitleStorageTitle(6002, 21), parsed.Title);
    }

    [Fact]
    public void LegacyStartupCheckUpRemovalsMatchCapturedRadiantWingsAndVictorBurst()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-title-storage-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(
                Path.Combine(dbRoot, "title_type.txt"),
                string.Join(
                    "\r\n",
                    "6002@@21@@RadiantWings@@0@@0@@0@@0@@0@@",
                    "2002@@3@@Victor@@0@@0@@0@@0@@150@@",
                    "9999@@1@@Anchor@@0@@0@@0@@0@@59120@@"));
            var snapshot = Snapshot("3,6002,0,2002,0,9999,0,");

            var packets = LegacyTitleStoragePacket.CreateLegacyStartupCheckUpRemovals(snapshot, usersRoot);

            Assert.Equal(8, packets.Count);
            Assert.Equal("0d0009090805100018f22e2015", Hex(packets[0]));
            Assert.Equal("1a00090908051086cf0318f22e20152a0908f22e101518002000", Hex(packets[1]));
            Assert.Equal("1a00090908001086cf0318f22e20152a0908f22e101518002000", Hex(packets[2]));
            Assert.Equal("1a00090908031086cf0318f22e20152a0908f22e101518002000", Hex(packets[3]));
            Assert.Equal("0d0009090805100018d20f2003", Hex(packets[4]));
            Assert.Equal("1a000909080510f0cd0318d20f20032a0908d20f100318002000", Hex(packets[5]));
            Assert.Equal("1a000909080010f0cd0318d20f20032a0908d20f100318002000", Hex(packets[6]));
            Assert.Equal("1a000909080310f0cd0318d20f20032a0908d20f100318002000", Hex(packets[7]));
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyStartupFullLoadSendsRemainingTitlesAfterCheckUpRemovals()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-title-full-load-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(
                Path.Combine(dbRoot, "title_type.txt"),
                string.Join(
                    "\r\n",
                    "6002@@21@@RadiantWings@@0@@0@@0@@0@@0@@",
                    "2002@@3@@Victor@@0@@0@@0@@0@@150@@",
                    "9999@@1@@Anchor@@0@@0@@0@@0@@59120@@"));
            var snapshot = Snapshot("3,6002,0,2002,0,9999,0,");

            var packets = LegacyTitleStoragePacket.CreateLegacyStartupFullLoad(snapshot, usersRoot);

            Assert.Equal(2, packets.Count);
            Assert.Equal(LegacyTitleStorageAction.UpdateScore, packets[0].Action);
            Assert.Equal(59120U, packets[0].DwParam1);
            Assert.Equal(9999U, packets[0].DwParam2);
            Assert.Equal(1U, packets[0].DwParam3);
            Assert.Equal(new LegacyTitleStorageTitle(9999, 1), packets[0].Title);
            Assert.Equal(LegacyTitleStorageAction.FullLoad, packets[1].Action);
            Assert.Equal(100U, packets[1].DwParam1);
            Assert.Equal(9999U, packets[1].DwParam2);
            Assert.Equal(1U, packets[1].DwParam3);
            Assert.Equal(new LegacyTitleStorageTitle(9999, 1), packets[1].Title);
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyStartupFullLoadSendsActiveSpecialTitleAndWingMarkers()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-title-active-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(
                Path.Combine(dbRoot, "title_type.txt"),
                string.Join(
                    "\r\n",
                    "9999@@1@@Anchor@@0@@0@@0@@0@@59120@@",
                    "7001@@2@@ActiveTitle@@0@@0@@0@@0@@5@@",
                    "6008@@7@@ActiveWing@@0@@0@@0@@0@@10@@"));
            var snapshot = Snapshot(
                "1,9999,0,",
                """
                SpecialTitleID=70010004
                SpecialWingID=60080001
                """);

            var packets = LegacyTitleStoragePacket.CreateLegacyStartupFullLoad(snapshot, usersRoot);

            Assert.Equal(4, packets.Count);
            Assert.Equal(LegacyTitleStorageAction.UpdateScore, packets[2].Action);
            Assert.Equal(59120U, packets[2].DwParam1);
            Assert.Equal(7001U, packets[2].DwParam2);
            Assert.Equal(2U, packets[2].DwParam3);
            Assert.Equal(new LegacyTitleStorageTitle(7001, 2, DwParam1: 100), packets[2].Title);
            Assert.Equal(LegacyTitleStorageAction.UpdateScore, packets[3].Action);
            Assert.Equal(59120U, packets[3].DwParam1);
            Assert.Equal(6008U, packets[3].DwParam2);
            Assert.Equal(7U, packets[3].DwParam3);
            Assert.Equal(new LegacyTitleStorageTitle(6008, 7, DwParam1: 100), packets[3].Title);
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyStartupFullLoadUsesSelectedSpecialTitlePairAsActiveMarker()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-title-selected-pair-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(
                Path.Combine(dbRoot, "title_type.txt"),
                string.Join(
                    "\r\n",
                    "6012@@6012@@WingsofRomance@@0@@0@@0@@0@@0@@",
                    "6023@@6023@@VioletWings@@0@@0@@0@@0@@0@@"));
            var snapshot = Snapshot("2,6012,0,6023,1,");

            var packets = LegacyTitleStoragePacket.CreateLegacyStartupFullLoad(snapshot, usersRoot);

            Assert.Equal(5, packets.Count);
            Assert.Equal(LegacyTitleStorageAction.UpdateScore, packets[4].Action);
            Assert.Equal(6023U, packets[4].DwParam2);
            Assert.Equal(6023U, packets[4].DwParam3);
            Assert.Equal(new LegacyTitleStorageTitle(6023, 6023, DwParam1: 100), packets[4].Title);
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyStartupCheckUpKeepsWardrobeEarnedTitlesAndRegrantsProjectTitles()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-title-project-checkup-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(
                Path.Combine(dbRoot, "title_type.txt"),
                string.Join(
                    "\r\n",
                    "6002@@21@@RadiantWings@@0@@0@@0@@0@@0@@",
                    "2002@@3@@Victor@@0@@0@@0@@0@@150@@",
                    "2006@@7@@SwiftChaser@@0@@0@@0@@0@@150@@",
                    "6003@@22@@StarlightWings@@0@@0@@0@@0@@0@@",
                    "6004@@23@@MoonlightWings@@0@@0@@0@@0@@0@@"));
            var snapshot = Snapshot(
                "5,6002,0,2002,0,2006,0,6003,0,6004,0,",
                "ProjectManager=1\r\n");
            var playerItems = new PlayerItemSnapshot(
                [],
                [],
                [],
                [
                    new PlayerItemSnapshotItem
                    {
                        ItemId = 181305,
                        WarehouseId = 100,
                        Position = 9,
                    },
                ],
                [],
                [
                    new PlayerItemSnapshotItem { ItemId = 200559 },
                ]);
            var coatTypes = new Dictionary<int, CoatStorageType>
            {
                [181305] = new(
                    ItemTypeId: 181305,
                    StorageType: 1,
                    Parameters: [1, 0, 0, 0, 0, 5],
                    SourceFile: "coat_storage_type.txt",
                    LineNumber: 1,
                    RawLine: "",
                    Fields: []),
                [200559] = new(
                    ItemTypeId: 200559,
                    StorageType: 89,
                    Parameters: [2, 0, 0, 0, 0, 5],
                    SourceFile: "coat_storage_type.txt",
                    LineNumber: 2,
                    RawLine: "",
                    Fields: []),
            };

            var packets = LegacyTitleStoragePacket.CreateLegacyStartupCheckUpRemovals(
                snapshot,
                usersRoot,
                playerItems,
                coatTypes);

            Assert.DoesNotContain(packets, packet => packet.DwParam2 is 6003 or 6004);
            Assert.Equal(
                [
                    (LegacyTitleStorageAction.UnEquip, 6002U),
                    (LegacyTitleStorageAction.UnEquip, 6002U),
                    (LegacyTitleStorageAction.UpdateScore, 6002U),
                    (LegacyTitleStorageAction.RemoveTitle, 6002U),
                    (LegacyTitleStorageAction.UnEquip, 2002U),
                    (LegacyTitleStorageAction.UnEquip, 2002U),
                    (LegacyTitleStorageAction.UpdateScore, 2002U),
                    (LegacyTitleStorageAction.RemoveTitle, 2002U),
                    (LegacyTitleStorageAction.UnEquip, 2006U),
                    (LegacyTitleStorageAction.UnEquip, 2006U),
                    (LegacyTitleStorageAction.UpdateScore, 2006U),
                    (LegacyTitleStorageAction.RemoveTitle, 2006U),
                    (LegacyTitleStorageAction.UpdateScore, 6002U),
                    (LegacyTitleStorageAction.FullLoad, 6002U),
                    (LegacyTitleStorageAction.UpdateScore, 2002U),
                    (LegacyTitleStorageAction.FullLoad, 2002U),
                    (LegacyTitleStorageAction.UpdateScore, 2006U),
                    (LegacyTitleStorageAction.FullLoad, 2006U),
                ],
                packets.Select(packet => (packet.Action, packet.DwParam2)).ToArray());
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    private static string Hex(LegacyTitleStoragePacket packet)
    {
        return Convert.ToHexString(packet.ToFrameBytes()).ToLowerInvariant();
    }

    private static CharacterSnapshot Snapshot(string specialTitles, string extraAttributes = "")
    {
        return CharacterSnapshotParser.ParseText($$"""
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
            VipLevel=0
            SpecialTitles={{specialTitles}}
            {{extraAttributes}}
            PkPoints=0
            Online=0
            """);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
