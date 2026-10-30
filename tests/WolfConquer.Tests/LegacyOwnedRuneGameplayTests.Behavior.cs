using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneGameplayTests
{
    [Fact]
    public Task ActualEquipGrantsAndClaimsBeforeItsFirstReplyAndActualUnequipRetiresThatOwnedGrant() => Run(async f =>
    {
        int first = f.Sent.Count, observed = 0;
        f.BeforeSend = _ => { f.AssertConverged(); f.AssertMarker("14160:16"); observed++; };
        try { await f.DispatchAsync(F.Equip()); } finally { f.BeforeSend = null; }
        Assert.Equal(7, observed); var output = f.Sent.Skip(first).ToArray();
        Assert.Equal(Usage(F.First, 212, 58), output[0]);
        Assert.Equal(Convert.FromHexString("19003508000000005037000010000000000000000000000000"), output[1]);
        Assert.Equal(Usage(F.First, 102, 57, 112233, 44, 55, 66), output[2]);
        Assert.Equal(new PlayerSpellSnapshotEntry(F.FirstSpell, 16, 0, 0, 0, 0),
            Assert.Single(f.Current.PlayerSpells.Spells, row => row.Id == F.FirstSpell));
        first = f.Sent.Count; await f.DispatchAsync(F.Unequip()); f.AssertConverged(); f.AssertMarker("");
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == F.FirstSpell);
        output = f.Sent.Skip(first).ToArray(); Assert.Equal(6, output.Length);
        Assert.Equal(new MsgActionPacket { ObjId = F.Uid, DwParam = F.FirstSpell, Type = (MsgActionType)109 }.ToFrameBytes(), output[0]);
        Assert.Equal(Usage(F.First, 102, 58, 112244, 77, 88, 99), output[1]);
    });

    [Fact]
    public Task UnknownPreexistingMappedSpellIsNeitherOverwrittenNorAdoptedByRealRuneActions() => Run(async f =>
    {
        await f.DispatchAsync(F.Equip()); f.AssertConverged(); f.AssertMarker(null);
        Assert.Equal(f.OriginalSpells, File.ReadAllBytes(f.SpellsPath));
        int first = f.Sent.Count; await f.DispatchAsync(F.Unequip()); f.AssertConverged(); f.AssertMarker(null);
        Assert.Equal(f.OriginalSpells, File.ReadAllBytes(f.SpellsPath));
        Assert.Equal(5, f.Sent.Count - first); Assert.Equal(F.Unequip().ToFrameBytes(), f.Sent[first]);
    }, legacy: true);

    [Fact]
    public Task ASecondRealMappedProviderRetainsTheExactGrantUntilTheLastProviderLeaves() => Run(async f =>
    {
        await f.DispatchAsync(F.Equip()); var granted = File.ReadAllBytes(f.SpellsPath);
        await f.DispatchAsync(F.Equip(F.Shared, 122)); f.AssertConverged(); f.AssertMarker("14160:16");
        Assert.Equal(granted, File.ReadAllBytes(f.SpellsPath));
        int first = f.Sent.Count; await f.DispatchAsync(F.Unequip());
        f.AssertConverged(); f.AssertMarker("14160:16"); Assert.Equal(granted, File.ReadAllBytes(f.SpellsPath));
        Assert.Equal(5, f.Sent.Count - first);
        await f.DispatchAsync(F.Unequip(F.Shared)); f.AssertConverged(); f.AssertMarker("");
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == F.FirstSpell);
    });

    [Fact]
    public Task ActualSameIdThenSlotDisplacementOrdersEachRemovalBeforeTheNewGrantAndEcho() => Run(async f =>
    {
        await f.DispatchAsync(F.Equip(F.Shared, 122)); await f.DispatchAsync(F.Equip(F.Other, 102));
        f.AssertMarker("14160:16,15470:0"); int first = f.Sent.Count;
        await f.DispatchAsync(F.Equip()); f.AssertConverged(); f.AssertMarker("14160:16");
        var output = f.Sent.Skip(first).ToArray(); Assert.Equal(10, output.Length);
        Assert.Equal(Usage(F.Shared, 122, 58), output[0]);
        Assert.Equal(new MsgActionPacket { ObjId = F.Uid, DwParam = F.FirstSpell, Type = (MsgActionType)109 }.ToFrameBytes(), output[1]);
        Assert.Equal(Usage(F.Other, 102, 58), output[2]);
        Assert.Equal(new MsgActionPacket { ObjId = F.Uid, DwParam = F.OtherSpell, Type = (MsgActionType)109 }.ToFrameBytes(), output[3]);
        Assert.Equal(Convert.FromHexString("19003508000000005037000010000000000000000000000000"), output[4]);
        Assert.Equal(Usage(F.First, 102, 57, 112233, 44, 55, 66), output[5]);
    }, sameId: true);

    [Theory]
    [InlineData(105, 0, 0u, true)] [InlineData(106, 0, 0u, false)]
    [InlineData(125, 0, 0u, true)] [InlineData(126, 0, 0u, false)]
    [InlineData(114, 4, 31u, true)] [InlineData(134, 4, 31u, true)]
    [InlineData(115, 4, 31u, false)] [InlineData(135, 4, 31u, false)]
    public Task RealAdmittedBeastRowsAndHammerFlagsBoundBothTwelveSlotFamilies(ushort slot, byte extra, uint flags, bool admitted) =>
        Run(async f =>
        {
            if (!admitted) await f.AssertRefusedAsync(() => f.DispatchAsync(F.Equip(F.Yellow, slot)));
            else
            {
                await f.DispatchAsync(F.Equip(F.Yellow, slot)); f.AssertConverged(); f.AssertMarker(null);
                Assert.Equal(slot, Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == F.Yellow).Position);
                Assert.Equal(f.OriginalSpells, File.ReadAllBytes(f.SpellsPath));
            }
        }, extraSlots: extra, flags: flags);

    // Independent complete2137 layout, including original request metadata and
    // all reserved tail bytes. These expectations do not invoke its codec.
    private static byte[] Usage(uint uid, ulong parameter, ushort action, uint time = 0, uint second = 0, uint third = 0, uint fourth = 0)
    {
        var bytes = new byte[91]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 91);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2137); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8), parameter); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), action);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(18), time); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(22), second);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(26), third); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(30), fourth); return bytes;
    }
}
