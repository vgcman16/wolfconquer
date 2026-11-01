using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    internal const uint DeathTargetAccount = CreationFixture.Account + 1, DeathTargetUid = 1_293_601;
    private const uint DeathArmorUid = 5_337_700, DeathArmorType = 130_009;

    internal static Task RunPhysicalDeathAsync(Func<PhysicalDeathFixture, Task> body, bool changed = true) => Run(async f =>
    {
        await CreateAndAssert(f); await DepositNewbornMoney(f); await f.ConnectRuneActorAsync();
        var target = f.NewConnection(DeathTargetAccount);
        await f.SendOnAsync(target, new LegacyMsgLoginGamePacket(DeathTargetAccount).ToFrameBytes());
        Assert.True(f.Store.TryGet(target, out var admitted), f.Log.ToString());
        var live = Assert.IsType<WorldSession>(admitted);
        Assert.Equal((long)DeathTargetAccount, live.AccountId); Assert.Equal((long)DeathTargetUid, live.Character.Uid);
        Assert.Equal(200L, live.Character.MinHitPoints); Assert.False(live.PlayerSpells.ContainsSpell(20000));
        Assert.Equal((byte)0xC7, File.ReadAllBytes(DeathFile(f, LegacyTradeJournalFileKind.Spells))[15]);
        var maximum = f.Frames(DeathTargetAccount).Select(bytes =>
                LegacyMsgUpdatePacket.TryParse(CreationFixture.ReadFrame(bytes), out var update) ? update : null)
            .Where(update => update is not null).SelectMany(update => update!.Updates)
            .Where(row => row.Type == LegacyMsgUpdateDataType.MaxHitpoints).Last().Value;
        f.Sent.Clear(); f.Frames(CreationFixture.RuneActor).Clear(); f.Frames(DeathTargetAccount).Clear();
        await body(new PhysicalDeathFixture(f, target, changed, maximum));
    }, beforeBootstrap: f => f.ConfigurePhysicalDeathInputs(changed));

    internal sealed partial class PhysicalDeathFixture(CreationFixture owner, ConquerTcpConnection target, bool changed, ulong maximum)
    {
        internal CreationFixture Owner { get; } = owner;
        internal ConquerTcpConnection Target { get; } = target;
        internal ConquerTcpConnection Actor => Owner.RuneConnection!;
        internal bool Changed { get; } = changed;
        internal ulong Maximum { get; } = maximum;
        internal WorldSession ActorSession => Owner.CurrentFor(Actor);
        internal WorldSession TargetSession => Owner.CurrentFor(Target);
        internal string CharacterPath => DeathFile(Owner, LegacyTradeJournalFileKind.Character);
        internal string ItemsPath => DeathFile(Owner, LegacyTradeJournalFileKind.Items);
        internal string SpellsPath => DeathFile(Owner, LegacyTradeJournalFileKind.Spells);
        internal Action<uint, byte[]>? BeforePhysicalSend;
        internal readonly List<(uint Recipient, byte[] Bytes)> PhysicalFrames = [];
        internal PhysicalDeathBefore? Before { get; private set; }
    }

    private static string DeathFile(CreationFixture f, LegacyTradeJournalFileKind kind) => f.Data.Options().Roots.PathFor(kind,
        kind == LegacyTradeJournalFileKind.Character ? DeathTargetAccount : DeathTargetUid);

    internal sealed partial class CreationFixture
    {
        internal void ConfigurePhysicalDeathInputs(bool changed)
        {
            // This existing hook runs BEFORE Bootstrap. Newborn is absent and
            // never receives invented combat stats, items or eligibility.
            var actor = DeathSavedCharacter(RuneActor, "DeathActor", 6000, 830, 5000, 300);
            WriteDeathCharacter(Data.UserPath, actor);
            var target = DeathSavedCharacter(DeathTargetUid, "DeathTarget", 7051, 831, 200, 0);
            WriteDeathCharacter(DeathFile(this, LegacyTradeJournalFileKind.Character), target);
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(DeathTargetUid, new(
                new PlayerItemSnapshotItem[] { new() { Uid = DeathArmorUid, ItemId = DeathArmorType, Position = 0,
                    Durability = 100, MaximumDurability = 100, StackSize = 1, MythSoul = changed ? 15U : 0U } }, [], [], [], []));
            var spells = new PlayerSpellSnapshot(new PlayerSpellSnapshotEntry[] { new(6001, 2, 1234, 1, 0, 0) });
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(DeathTargetUid, spells);
            var bytes = File.ReadAllBytes(DeathFile(this, LegacyTradeJournalFileKind.Spells)); bytes[15] = 0xC7;
            File.WriteAllBytes(DeathFile(this, LegacyTradeJournalFileKind.Spells), bytes);
            new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(DeathTargetUid, PlayerProficiencySnapshot.Empty);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(DeathTargetUid, PlayerQuestSnapshot.Empty);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(DeathTargetUid, CombatGearSnapshot.Empty);
            new LegacyMailboxStore(Data.Root).Save(DeathTargetUid);
            Catalog = BuildPhysicalDeathCatalog(Catalog);
        }
    }

    private static CharacterSnapshot DeathSavedCharacter(uint uid, string name, long job, long x, long hp, long strength)
    {
        var value = OwnedEquipmentBehaviorFixture.Character(uid, name, job, strength: strength, agility: 0,
            mapId: 1000, x: x, y: 584, minHitPoints: hp) with { Spirit = 10, MinMana = 50, MaxMana = 50 };
        return value with { Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { ["PkMode"] = "0", ["DynamicID"] = "1", ["Stamina"] = "100", ["Action"] = "0", ["MaxMana"] = "50",
            ["ExpBallUsed"] = "0", ["SecurityPass"] = "0,0,0",
            ["EpicTrojan"] = "0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/" } };
    }

    private static void WriteDeathCharacter(string path, CharacterSnapshot value)
    {
        var values = new Dictionary<string, string>(value.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in CharacterSnapshotScalarPersistence.BuildPersistedValues(value)) values[pair.Key] = pair.Value;
        values["Name"] = value.Name;
        File.WriteAllText(path, "[Character]\r\n" + string.Join("\r\n", values.Select(pair => pair.Key + "=" + pair.Value)) + "\r\n",
            new UTF8Encoding(true));
    }

    private static WorldDataCatalog BuildPhysicalDeathCatalog(WorldDataCatalog original)
    {
        // Exact retained DBServer/magictype.txt3237, raw20006/type20000/level5.
        const string venom = "20006@@20000@@16@@Venom@@1@@0@@1@@8@@5@@0@@30150@@0@@550@@2@@16@@16@@355@@0@@0@@0@@1@@0@@0@@3@@16@@0@@0@@0@@0@@0@@1@@0@@0@@800@@0@@3000@@1@@20@@0@@0@@0@@0@@0@@0@@30100@@0@@0@@800@@1000@@0@@0@@0@@0@@";
        // Complete actual2000000 descriptor; no explicit IsRuneSkill field.
        const string effect = "[2000000]\r\nName=Venom\r\nClientRepresent=4\r\nSortOfAct=5\r\nDesc=Venom\r\nDescEx=Venom\r\nHide=1\r\n";
        return NativeCallerWorldDataFixture.Create(original.Items.Concat(new[] { OwnedEquipmentBehaviorFixture.Item(DeathArmorType) }).ToArray(), [],
            magic: new[] { MagicTypeParser.ParseLine(venom) },
            magicEffects: original.MagicEffects.Concat(MagicEffectParser.Parse(effect)).ToArray(),
            professionLevelBenefits: original.ProfessionLevelBenefits.ToArray(),
            runeStorageAttributes: original.RuneStorageAttributes.ToArray(), runeLevelExperience: original.RuneLevelExperience.ToArray(),
            innerPowerRanks: original.InnerPowerRanks.ToArray());
    }
}
