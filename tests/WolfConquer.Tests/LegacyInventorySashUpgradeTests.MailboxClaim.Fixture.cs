using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private const uint AtomicMailId = 21;
    private const uint AtomicMailItemUid = 7_654_321;
    private const ulong AtomicMailMoney = 125;
    private const uint AtomicMailCp = 7;

    private sealed partial class Fixture
    {
        internal string AtomicMailPath => new LegacyMailboxStore(Root).GetMailboxPath(Uid);
        internal LegacyMailboxEntry AtomicMailSeed { get; private set; } = null!;

        internal async Task EnterAtomicMailFrameAsync(int inventoryCount = 1, bool mixed = false,
            uint flag = 0, bool malformedTail = false, ulong? rewardMoney = null,
            uint? rewardCp = null, bool hasAttachment = true, bool extraEmptyMail = false,
            long packageClass = 10, bool savedPackageGear = false)
        {
            Seed(frameLogin: true);
            if (packageClass != 10)
                File.WriteAllBytes(CharacterPath, ProjectSashFields(File.ReadAllBytes(CharacterPath),
                    new Dictionary<string, string> { ["Class"] = packageClass.ToString(CultureInfo.InvariantCulture) }));
            string? packageGearRoot = null;
            if (savedPackageGear)
            {
                packageGearRoot = Path.Combine(Root, "PlayersCombatGear");
                var gear = CombatGearSnapshot.Empty with
                {
                    MasteryPoints = 23, PirateExp = 17,
                    AirPowers = CombatGearAirPowersSnapshot.Empty with { Points = 19 },
                };
                new CombatGearSnapshotLoader(packageGearRoot).SaveByCharacterUid(Uid, gear);
            }
            var inventory = Enumerable.Range(0, inventoryCount).Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)(10_000 + index)), ItemId = 1_088_001,
                Durability = 5, MaximumDurability = 10, StackSize = 1,
            }).ToArray();
            var attachment = new PlayerItemSnapshotItem
            {
                Uid = AtomicMailItemUid, ItemId = 1_088_001, Durability = 5, MaximumDurability = 10,
                WarehouseId = 1000, StackSize = 3, Bound = 1, Plus = 2, Bless = 1,
                SocketProgress = 17, Color = 3, PerfectionLevel = 2, PerfectionProgress = 19,
                OwnerUid = 77, OwnerName = "MailOwner", Signature = "mail sentinel",
            };
            SaveTransferSetup(Current.PlayerItems with
            {
                InventoryItems = inventory,
                WarehouseItems = Current.PlayerItems.WarehouseItems.Append(attachment)
                    .OrderBy(item => item.WarehouseId).ThenBy(item => item.Uid).ToArray(),
            });
            AtomicMailSeed = new LegacyMailboxEntry
            {
                Id = AtomicMailId, Title = "Atomic attachment", SenderName = "System sentinel",
                Content = "Preserve every other mailbox byte.", ItemId = hasAttachment ? AtomicMailItemUid : 0,
                Money = rewardMoney ?? (mixed ? AtomicMailMoney : 0),
                EMoney = rewardCp ?? (mixed ? AtomicMailCp : 0),
                EmoneyRecordType = 19, Flag = flag, Timer = new DateTime(2099, 1, 1).ToBinary(),
            };
            var mailboxes = new LegacyMailboxStore(Root);
            mailboxes.Add(Uid, AtomicMailSeed);
            mailboxes.Add(Uid, new LegacyMailboxEntry
            {
                Id = 22, Title = "Unselected sentinel", SenderName = "Other sender", Content = "Keep me intact.",
                Money = 37, EMoney = 11, EmoneyRecordType = 5, Timer = AtomicMailSeed.Timer,
            });
            if (extraEmptyMail)
                mailboxes.Add(Uid, new LegacyMailboxEntry
                {
                    Id = 23, Title = "Second empty row", SenderName = "Delete fixture", Content = "Empty reward fields.",
                    EmoneyRecordType = 31, Timer = AtomicMailSeed.Timer,
                });
            mailboxes.Save(Uid); // Genuine producer, before Frame construction/admission.
            Assert.Equal(4 + (extraEmptyMail ? 3 : 2) * 740, File.ReadAllBytes(AtomicMailPath).Length);
            if (extraEmptyMail)
            {
                // Valid uninterpreted padding after the first title NUL, not a malformed row.
                // Deleting other rows must preserve it instead of normalizing this record.
                var raw = File.ReadAllBytes(AtomicMailPath);
                const int titleOffset = 4 + 740; // Unselected second row (ID22).
                Assert.Equal(22U, BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(titleOffset + 704)));
                Assert.Equal("Unselected sentinel", new LegacyMailboxStore(Root).Load(Uid)[1].Title);
                Assert.InRange(raw.AsSpan(titleOffset, LegacyMailboxEntry.TitleByteLength).IndexOf((byte)0), 0, 62);
                Assert.Equal((byte)0, raw[titleOffset + 63]);
                raw[titleOffset + 63] = 0x5a;
                File.WriteAllBytes(AtomicMailPath, raw);
                Assert.Equal("Unselected sentinel", new LegacyMailboxStore(Root).Load(Uid)[1].Title);
            }

            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var loader = new CharacterSnapshotLoader(Users, Items, playerSpellsRoot: null,
                playerProficienciesRoot: null, playersCombatGearRoot: packageGearRoot, playerQuestsRoot: null);
            frameOwner = new WorldServerFrameHandler(loader, handoff, worldData: null,
                runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: Log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            var ticket = handoff.Issue(Account, "mailbox-atomic-frame", ConquerRuntimeDefaults.WorldServer);
            var first = Sent.Count;
            await FrameAsync(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes());
            var parsed = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath);
            Assert.Equal(WorldSnapshotResponsePacket.Build(WorldSnapshotWireStatus.Success, Account, parsed), Sent[first]);
            await FrameAsync(new MsgActionPacket { ObjId = Uid, Type = MsgActionType.SetLocation }.ToFrameBytes());
            Assert.Null(Connection.NativeGameClient); // Actual managed handoff, no native issuer claim.
            Assert.Equal(inventoryCount, new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(Uid).InventoryItems.Count);
            // Inject after genuine admission: strict-load refusals have separate coverage.
            if (malformedTail)
                File.WriteAllBytes(AtomicMailPath, [.. File.ReadAllBytes(AtomicMailPath), 0x7f]);
        }

        internal static byte[] AtomicMailRequest(bool claimAll = false) => new LegacyMailboxModePacket(
            claimAll ? LegacyMailboxModeType.ClaimAll : LegacyMailboxModeType.ClaimItem,
            claimAll ? 0 : AtomicMailId, claimAll ? [AtomicMailId] : []).ToFrameBytes();

        internal void HoldAtomicMailFile() =>
            heldFiles.Add(new FileStream(AtomicMailPath, FileMode.Open, FileAccess.Read, FileShare.None));
    }

    private static PlayerItemSnapshot ExpectedAtomicMailItems(PlayerItemSnapshot before)
    {
        var attachment = Assert.Single(before.WarehouseItems,
            item => item.WarehouseId == 1000 && item.Uid == AtomicMailItemUid);
        return before with
        {
            InventoryItems = before.InventoryItems.Append(attachment.WithWarehouseId(0, position: 0))
                .OrderBy(item => item.Uid).ToArray(),
            WarehouseItems = before.WarehouseItems.Where(item => item.Uid != AtomicMailItemUid).ToArray(),
        };
    }

    private static Dictionary<string, byte[]> ExpectedAtomicMailFiles(Fixture fixture, Snapshot before, bool mixed = false)
    {
        var expected = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase);
        expected[Path.GetRelativePath(fixture.Root, fixture.ItemPath)] =
            PlayerItemSnapshotLoader.ToLegacyBytes(ExpectedAtomicMailItems(before.Owner.PlayerItems));
        var mailPath = Path.GetRelativePath(fixture.Root, fixture.AtomicMailPath);
        var mail = before.Files[mailPath].ToArray();
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(mail));
        Assert.Equal(4 + 2 * 740, mail.Length);
        Assert.Equal(AtomicMailId, BinaryPrimitives.ReadUInt32LittleEndian(mail.AsSpan(4 + 704)));
        Assert.Equal(AtomicMailItemUid, BinaryPrimitives.ReadUInt32LittleEndian(mail.AsSpan(4 + 720)));
        // Existing managed codec: count prefix, then 740-byte entries. No production serializer oracle.
        mail.AsSpan(4 + 720, 4).Clear();
        if (mixed) mail.AsSpan(4 + 708, 12).Clear(); // Money (8), EMoney (4), preserving record type and timer.
        expected[mailPath] = mail;
        if (mixed)
        {
            var characterPath = Path.GetRelativePath(fixture.Root, fixture.CharacterPath);
            expected[characterPath] = ProjectSashFields(before.Files[characterPath], new Dictionary<string, string>
            {
                ["Money"] = (before.Owner.Character.Money + (long)AtomicMailMoney).ToString(CultureInfo.InvariantCulture),
                ["ConquerPoints"] = (before.Owner.Character.ConquerPoints + AtomicMailCp).ToString(CultureInfo.InvariantCulture),
            });
        }
        return expected;
    }

    private static void AssertAtomicMailCommitted(Fixture fixture, Snapshot before, bool mixed = false)
    {
        fixture.AssertFiles(ExpectedAtomicMailFiles(fixture, before, mixed));
        var loaded = new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid);
        Assert.Equal(JsonSerializer.Serialize(ExpectedAtomicMailItems(before.Owner.PlayerItems)), JsonSerializer.Serialize(loaded));
        Assert.Single(loaded.InventoryItems, item => item.Uid == AtomicMailItemUid);
        Assert.DoesNotContain(loaded.WarehouseItems, item => item.Uid == AtomicMailItemUid);
        var mail = Assert.Single(new LegacyMailboxStore(fixture.Root).Load(Fixture.Uid), entry => entry.Id == AtomicMailId);
        Assert.Equal(0U, mail.ItemId);
        Assert.Equal(mixed ? 0UL : fixture.AtomicMailSeed.Money, mail.Money);
        Assert.Equal(mixed ? 0U : fixture.AtomicMailSeed.EMoney, mail.EMoney);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static byte[] AtomicMailItemFrame(Snapshot before) => LegacyGameItemPacket.FromSnapshotItem(
        Assert.Single(ExpectedAtomicMailItems(before.Owner.PlayerItems).InventoryItems,
            item => item.Uid == AtomicMailItemUid)).ToFrameBytes();

    private static void AssertAtomicMailFrames(Fixture fixture, Snapshot before, bool claimAll, bool mixed,
        DateTime started, DateTime ended, int firstFrame, bool replay = false)
    {
        var frames = fixture.Sent.Skip(firstFrame).ToArray();
        var prefix = new List<byte[]>();
        if (mixed && !replay)
        {
            prefix.Add(LegacyMsgMessagePacket.SystemTalk($"STR_PICK_MONEY@@{AtomicMailMoney}@@").ToFrameBytes());
            prefix.Add(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.Money,
                checked((ulong)before.Owner.Character.Money + AtomicMailMoney))]).ToFrameBytes());
            prefix.Add(LegacyMsgMessagePacket.SystemTalk($"You received {AtomicMailCp} ConquerPoints.").ToFrameBytes());
            prefix.Add(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)before.Owner.Character.ConquerPoints + AtomicMailCp))]).ToFrameBytes());
        }
        if (!replay) prefix.Add(AtomicMailItemFrame(before));
        Assert.Equal(prefix.Count + (claimAll ? 2 : 1), frames.Length);
        for (var index = 0; index < prefix.Count; index++) Assert.Equal(prefix[index], frames[index]);
        if (claimAll)
        {
            Assert.True(LegacyMailListPacket.TryParse(ReadFrame(frames[prefix.Count]), out var refresh));
            Assert.Equal(LegacyMailListType.ClaimAllUpdate, refresh.Type);
            Assert.Equal(1, refresh.Count);
            var actual = Assert.Single(refresh.Mails);
            var cleared = fixture.AtomicMailSeed.WithClearedItemId().WithClaimedMoney();
            var earliest = LegacyMailListPacket.FromMailboxEntry(cleared, started);
            var latest = LegacyMailListPacket.FromMailboxEntry(cleared, ended);
            Assert.Equal(earliest with { ExpirationDate = actual.ExpirationDate }, actual);
            Assert.InRange(actual.ExpirationDate, latest.ExpirationDate, earliest.ExpirationDate);
        }
        Assert.Equal(Fixture.AtomicMailRequest(claimAll), frames[^1]);
    }
}
