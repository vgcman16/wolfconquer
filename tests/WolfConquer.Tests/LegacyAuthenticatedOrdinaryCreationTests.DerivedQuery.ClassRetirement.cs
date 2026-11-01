using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task RealQueryOriginIsRetiredByActualFullAccountClassChoiceBeforeOutput() => Run(async owner =>
    {
        await CreateAndAssert(owner); await DepositNewbornMoney(owner); await owner.ConnectRuneActorAsync();
        var target = owner.NewConnection(DeathTargetAccount);
        await owner.SendOnAsync(target, new LegacyMsgLoginGamePacket(DeathTargetAccount).ToFrameBytes());
        var admitted = owner.CurrentFor(target);
        Assert.Equal((long)DeathTargetAccount, admitted.AccountId);
        Assert.Equal((long)DeathTargetUid, admitted.Character.Uid);
        Assert.Equal(2L, admitted.Character.Reborn); Assert.Equal(1L, admitted.Character.Level);
        var maximum = owner.Frames(DeathTargetAccount).Select(bytes =>
                LegacyMsgUpdatePacket.TryParse(CreationFixture.ReadFrame(bytes), out var update) ? update : null)
            .Where(update => update is not null).SelectMany(update => update!.Updates)
            .Where(row => row.Type == LegacyMsgUpdateDataType.MaxHitpoints).Last().Value;
        owner.Frames(DeathTargetAccount).Clear();
        var f = new PhysicalDeathFixture(owner, target, true, maximum);
        var grant = await EquipForDerivedQueryAsync(f);
        Assert.Equal("20000:5", f.TargetSession.Character.Attributes["ManagedDerivedSpellOrigins"]);
        Assert.True(f.TargetSession.PlayerSpells.ContainsSpell(20000));
        owner.Data.AssertFiles(grant.After); AssertDerivedQueryHistory(f, grant);

        // Real Frame menu and option, using the retained visible SobNpc row below.
        owner.Frames(DeathTargetAccount).Clear();
        await owner.SendOnAsync(target, LegacyNpcServerRequestPacket.Script(465123, 0).ToFrameBytes());
        Assert.NotEmpty(owner.Frames(DeathTargetAccount));
        owner.Frames(DeathTargetAccount).Clear();
        var beforeCharacter = File.ReadAllBytes(f.CharacterPath);
        var beforeSpells = File.ReadAllBytes(f.SpellsPath);
        int observed = 0;
        owner.BeforeSend = (account, _) =>
        {
            if (account != DeathTargetAccount || observed++ != 0) return;
            AssertClassRetirementImages(f, beforeCharacter, beforeSpells);
            AssertDerivedQueryHistory(f, grant);
        };
        try { await owner.SendOnAsync(target, LegacyNpcServerRequestPacket.Script(465123, 2).ToFrameBytes()); }
        finally { owner.BeforeSend = null; }
        Assert.True(observed > 0, owner.Log.ToString());
        AssertClassRetirementImages(f, beforeCharacter, beforeSpells);
        AssertDerivedQueryHistory(f, grant);
        Assert.Contains("WorldServer applied LegacyNpc AccountSecurity FullAccount class reset", owner.Log.ToString(), StringComparison.Ordinal);
        Assert.False(LegacyTradeJournal.HasPending(owner.Data.Users));
    }, beforeBootstrap: owner =>
    {
        owner.ConfigurePhysicalDeathInputs(true);
        var path = DeathFile(owner, LegacyTradeJournalFileKind.Character);
        var original = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(path), out _));
        // A declared saved eligible target before bootstrap; never edit a live session.
        var attributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["DynamicID"] = "0" };
        WriteDeathCharacter(path, original with { Level = 1, MapId = 5000, X = 42, Y = 67,
            PreviousMapId = 5000, PreviousMapX = 42, PreviousMapY = 67, Attributes = attributes });
        owner.ConfigureClassRetirementNpcCatalog();
    });

    internal sealed partial class CreationFixture
    {
        internal void ConfigureClassRetirementNpcCatalog()
        {
            var catalog = Catalog;
            // Retained server DBServer/SobNpcs.txt line 35, first eleven fields;
            // the optional localized label has no role in NPC visibility or action.
            var fullAccount = SobNpcParser.ParseLine("465123,FullAccount,2,54336,5000,42,67,0,0,1,11");
            Catalog = NativeCallerWorldDataFixture.Create(catalog.Items.ToArray(), [], sobNpcs: new[] { fullAccount },
                magic: catalog.Magic.ToArray(), magicEffects: catalog.MagicEffects.ToArray(),
                professionLevelBenefits: catalog.ProfessionLevelBenefits.ToArray(), runeStorageAttributes: catalog.RuneStorageAttributes.ToArray(),
                runeLevelExperience: catalog.RuneLevelExperience.ToArray(), innerPowerRanks: catalog.InnerPowerRanks.ToArray());
        }
    }
    private static void AssertClassRetirementImages(PhysicalDeathFixture f, byte[] beforeCharacter, byte[] beforeSpells)
    {
        var characterBytes = File.ReadAllBytes(f.CharacterPath);
        var spellBytes = File.ReadAllBytes(f.SpellsPath);
        Assert.NotEqual(beforeCharacter, characterBytes); Assert.NotEqual(beforeSpells, spellBytes);
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(characterBytes, out _));
        var spells = PlayerSpellSnapshotLoader.Parse(spellBytes);
        Assert.Equal((long)DeathTargetUid, saved.Uid); Assert.Equal(1005L, saved.Class);
        Assert.Empty(LegacyDerivedSpellOrigins.Read(saved));
        Assert.DoesNotContain(spells.Spells, row => row.Id == 20000);
        Assert.Equal(new ushort[] { 1095, 1020, 1090, 1046, 1045 }, spells.Spells.Select(row => row.Id));
        var current = f.TargetSession;
        Assert.Equal(1005L, current.Character.Class);
        Assert.Empty(LegacyDerivedSpellOrigins.Read(current.Character));
        Assert.DoesNotContain(current.PlayerSpells.Spells, row => row.Id == 20000);
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
    }
}
