using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExistingUnmarkedSpellSurvivesActualLoginAndEquipmentQuery(bool omitUnusedMagic) => Run(async f =>
    {
        await CreateAndAssert(f); await DepositNewbornMoney(f); await f.ConnectRuneActorAsync();
        var beforeLogin = f.Data.Files(); var history = HistoryFiles(f); var ledger = File.ReadAllBytes(f.Data.State);
        var newborn = File.ReadAllBytes(f.CharacterPath);
        Assert.NotEmpty(history);
        var target = f.NewConnection(DeathTargetAccount);
        await f.SendOnAsync(target, new LegacyMsgLoginGamePacket(DeathTargetAccount).ToFrameBytes());
        var session = f.CurrentFor(target);
        Assert.Equal((long)DeathTargetAccount, session.AccountId); Assert.Equal((long)DeathTargetUid, session.Character.Uid);
        AssertUnmarkedRow(session);
        var afterLogin = beforeLogin.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        string characterKey = Path.GetRelativePath(f.Data.Root, DeathFile(f, LegacyTradeJournalFileKind.Character));
        string characterText = CharacterSnapshotFileEncoding.Decode(afterLogin[characterKey], out var encoding);
        const string offline = "\r\nOnline=0\r\n";
        Assert.Equal(2, characterText.Split(offline, StringSplitOptions.None).Length);
        characterText = characterText.Replace(offline, "\r\nOnline=1\r\n", StringComparison.Ordinal);
        afterLogin[characterKey] = encoding.GetPreamble().Concat(encoding.GetBytes(characterText)).ToArray();
        f.Data.AssertFiles(afterLogin);
        AssertHistoricalFiles(history, HistoryFiles(f)); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
        f.Frames(DeathTargetAccount).Clear();

        Dictionary<string, byte[]>? afterEquipBeforeQuery = null;
        f.BeforeSend = (account, _) =>
        {
            if (account != DeathTargetAccount || afterEquipBeforeQuery is not null) return;
            var current = f.CurrentFor(target);
            var armor = Assert.Single(current.PlayerItems.EquipmentItems);
            Assert.Equal(DeathArmorUid, armor.Uid); Assert.Equal((ushort)3, armor.Position);
            AssertUnmarkedRow(current);
            afterEquipBeforeQuery = f.Data.Files();
        };
        try
        {
            await f.SendOnAsync(target, new MsgItemUsagePacket(DeathArmorUid, 3, MsgItemUsageAction.Equip, 1234, 0, 0, 0).ToFrameBytes());
        }
        finally { f.BeforeSend = null; }
        Assert.NotNull(afterEquipBeforeQuery);
        f.Data.AssertFiles(afterEquipBeforeQuery!);
        string spellKey = Path.GetRelativePath(f.Data.Root, DeathFile(f, LegacyTradeJournalFileKind.Spells));
        Assert.Equal(UnmarkedSavedBytes(), afterEquipBeforeQuery![spellKey]);
        Assert.Equal(afterLogin[characterKey], afterEquipBeforeQuery[characterKey]);
        string itemsKey = Path.GetRelativePath(f.Data.Root, DeathFile(f, LegacyTradeJournalFileKind.Items));
        Assert.Equal(afterLogin.Keys.Order(StringComparer.OrdinalIgnoreCase), afterEquipBeforeQuery.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var row in afterLogin)
            if (!string.Equals(row.Key, itemsKey, StringComparison.OrdinalIgnoreCase))
                Assert.Equal(row.Value, afterEquipBeforeQuery[row.Key]);
        session = f.CurrentFor(target);
        AssertUnmarkedRow(session);
        var binding = session.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        var native = binding.Storage.ClientSpells[20000];
        Assert.Equal((ushort)7, native.Level); Assert.Equal(12345678, native.Experience);
        Assert.Equal((byte)3, native.PreviousLevel); Assert.Equal((byte)4, native.SoulLevel); Assert.Equal((byte)1, native.UseSpellSoul);
        Assert.DoesNotContain(f.Frames(DeathTargetAccount), bytes => bytes.Length >= 10 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)) == 2101 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8)) == 20000);
        Assert.DoesNotContain(f.Frames(DeathTargetAccount), bytes =>
            MsgActionPacket.TryParse(CreationFixture.ReadFrame(bytes), out var action) &&
            (ushort)action.Type == 109 && action.DwParam == 20000);
        AssertHistoricalFiles(history, HistoryFiles(f)); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
        Assert.Equal(newborn, File.ReadAllBytes(f.CharacterPath)); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }, beforeBootstrap: f =>
    {
        f.ConfigurePhysicalDeathInputs(true);
        // Declared saved input, before the real ownership scan. Byte 11 is opaque.
        File.WriteAllBytes(DeathFile(f, LegacyTradeJournalFileKind.Spells), UnmarkedSavedBytes());
        if (omitUnusedMagic) f.OmitUnmarkedUnusedMagic();
    });

    internal sealed partial class CreationFixture
    {
        internal void OmitUnmarkedUnusedMagic()
        {
            var catalog = Catalog;
            Catalog = NativeCallerWorldDataFixture.Create(catalog.Items.ToArray(), [], magic: [],
                magicEffects: catalog.MagicEffects.ToArray(), professionLevelBenefits: catalog.ProfessionLevelBenefits.ToArray(),
                runeStorageAttributes: catalog.RuneStorageAttributes.ToArray(), runeLevelExperience: catalog.RuneLevelExperience.ToArray(),
                innerPowerRanks: catalog.InnerPowerRanks.ToArray());
        }
    }
    private static byte[] UnmarkedSavedBytes() =>
    [
        2, 0, 0, 0,
        0x71, 0x17, 2, 0, 0xD2, 0x04, 0, 0, 1, 0, 0, 0xC7,
        0x20, 0x4E, 7, 0, 0x4E, 0x61, 0xBC, 0, 3, 4, 1, 0xA9
    ];

    private static void AssertUnmarkedRow(WorldSession session)
    {
        Assert.False(session.Character.Attributes.ContainsKey("ManagedDerivedSpellOrigins"));
        Assert.Equal(new[] { new PlayerSpellSnapshotEntry(6001, 2, 1234, 1, 0, 0),
            new PlayerSpellSnapshotEntry(20000, 7, 12345678, 3, 4, 1) }, session.PlayerSpells.Spells);
    }
}
