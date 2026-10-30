using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneContainerLifecycleTests
{
    [Theory]
    [InlineData("store")] [InlineData("bag")] [InlineData("drop")]
    public Task ActualLastProviderContainerActionCommitsExactImagesBeforeItsFirstReply(string kind) => Run(async f =>
    {
        await PrepareClaim(f, kind); var before = f.Current; var ledger = File.ReadAllBytes(f.Data.State);
        var oldCharacter = File.ReadAllBytes(f.CharacterPath); var oldSpells = File.ReadAllBytes(f.SpellsPath);
        var expectedItems = before.PlayerItems with { RuneItems = before.PlayerItems.RuneItems.Where(row => kind != "drop" || row.Uid != F.First)
            .Select(row => row.Uid == F.First ? row.WithPosition(kind == "store" ? (ushort)212 : (ushort)211) : row)
            .OrderBy(row => row.Position).ThenBy(row => row.Position is 211 or 212 ? 0u : row.Uid).ToArray() };
        var itemImage = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems);
        var text = CharacterSnapshotFileEncoding.Decode(oldCharacter, out var encoding); Assert.Contains("ManagedRuneSpellOrigins=14160:16\r\n", text);
        var characterImage = encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace("ManagedRuneSpellOrigins=14160:16\r\n",
            "ManagedRuneSpellOrigins=\r\n", StringComparison.Ordinal))).ToArray();
        // The actual producer appended its zero default row after the opaque
        // legacy row; independent removal keeps every original byte, includingA5.
        var spellImage = oldSpells[..^PlayerSpellSnapshotLoader.LegacySpellSize];
        BinaryPrimitives.WriteInt32LittleEndian(spellImage, BinaryPrimitives.ReadInt32LittleEndian(oldSpells) - 1);
        int first = f.Sent.Count, observed = 0;
        f.BeforeSend = _ =>
        {
            Assert.Equal(characterImage, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(itemImage, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(spellImage, File.ReadAllBytes(f.SpellsPath)); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
            f.AssertConverged(); f.AssertMarker(""); observed++;
        };
        try { await Send(f, kind); } finally { f.BeforeSend = null; }
        Assert.Equal(kind == "drop" ? 2 : 12, observed);
        Assert.Equal(new MsgActionPacket { ObjId = F.Uid, DwParam = F.FirstSpell, Type = (MsgActionType)109 }.ToFrameBytes(), f.Sent[first]);
        if (kind == "drop") Assert.Equal(new MsgItemUsagePacket(F.First, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), f.Sent[first + 1]);
        else
        {
            Assert.Equal(new LegacyRuneStoragePacket(kind == "store" ? (byte)0 : (byte)1, F.First, 0).ToFrameBytes(), f.Sent[first + 1]);
            LegacySelectedEquipmentQueryAssertions.NeutralQuery(f.Sent, first + 2, F.Uid);
        }
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == F.FirstSpell);
    });

    [Theory]
    [InlineData("store")] [InlineData("bag")] [InlineData("drop")]
    public Task AnActualOtherEquippedProviderRetainsTheExactManagedGrantAndOrigin(string kind) => Run(async f =>
    {
        await f.DispatchAsync(F.Equip()); await f.DispatchAsync(F.Equip(F.Shared, 122));
        var spellImage = File.ReadAllBytes(f.SpellsPath); int first = f.Sent.Count;
        await Send(f, kind); f.AssertConverged(); f.AssertMarker("14160:16");
        Assert.Equal(spellImage, File.ReadAllBytes(f.SpellsPath));
        Assert.Equal(kind == "drop" ? 1 : 11, f.Sent.Count - first);
        Assert.Contains(f.Current.PlayerItems.RuneItems, row => row.Uid == F.Shared && row.Position == 122);
    });

    [Theory]
    [InlineData("store")] [InlineData("bag")] [InlineData("drop")]
    public Task APreexistingUnmarkedSpellIsPreservedWithoutBeingAdopted(string kind) => Run(async f =>
    {
        if (kind == "store") await Send(f, "bag", F.Shared);
        await f.DispatchAsync(F.Equip()); f.AssertMarker(null); var saved = File.ReadAllBytes(f.SpellsPath);
        int first = f.Sent.Count; await Send(f, kind); f.AssertConverged(); f.AssertMarker(null);
        Assert.Equal(saved, File.ReadAllBytes(f.SpellsPath)); Assert.Equal(kind == "drop" ? 1 : 11, f.Sent.Count - first);
    }, legacy: true);

    [Theory]
    [InlineData("store")] [InlineData("bag")]
    public Task AnAlreadyTargetContainerRowHasTheOriginalResponseWithoutItemOrSpellChanges(string kind) => Run(async f =>
    {
        if (kind == "bag") await Send(f, kind);
        var itemImage = File.ReadAllBytes(f.ItemsPath); var spells = File.ReadAllBytes(f.SpellsPath); int first = f.Sent.Count;
        await Send(f, kind); f.AssertConverged(); f.AssertMarker(null);
        Assert.Equal(11, f.Sent.Count - first); Assert.Equal(itemImage, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(spells, File.ReadAllBytes(f.SpellsPath));
        const uint missing = F.First + 1000; var files = f.Data.Files(); first = f.Sent.Count;
        await Send(f, kind, missing); f.AssertConverged(); f.Data.AssertFiles(files);
        Assert.Equal(11, f.Sent.Count - first);
        Assert.Equal(new LegacyRuneStoragePacket(kind == "store" ? (byte)0 : (byte)1, missing, 0).ToFrameBytes(), f.Sent[first]);
        LegacySelectedEquipmentQueryAssertions.NeutralQuery(f.Sent, first + 1, F.Uid);
    });
}
