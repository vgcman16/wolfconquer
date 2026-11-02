using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)] [InlineData(false, 3)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)]
    public Task ActualJumpPreservesOrdinalOpaqueFieldsAndUniqueOwnedAliases(bool away, int vectorShape) => Run(async f =>
    {
        await f.StanceAsync(250);
        if (away)
        {
            // Earn Away through the public Frame action before adding the
            // unrelated Ordinal map. No native flag or timer is manufactured.
            await f.SendAsync(new MsgActionPacket { ObjId = Uid, Type = MsgActionType.Away }.ToFrameBytes());
            Assert.Equal("1", f.Current.Character.Attributes["Away"]);
        }
        ClearOutput(f);
        Assert.Empty(f.Native.Player!.BitVector.ArrayFlags);
        var savedBeforeMap = f.Data.Files();
        PublishJumpAttributeMap(f, vectorShape);
        f.Data.AssertFiles(savedBeforeMap);
        var before = f.Current; var peer = f.Get(Peer); var binding = f.Native.OrdinarySpellBinding!;
        var expected = ExpectedCoordinates(f, savedBeforeMap, (Uid, 430, 380, 432, 381));
        var stamp = f.Native.Player.AttackStamp;
        void AssertCommitted()
        {
            f.Data.AssertFiles(expected);
            AssertJumpAttributeOwner(f, binding, before, peer, away, stamp);
        }
        f.BeforeSend = (_, _) => AssertCommitted();
        try { await f.JumpAsync(); } finally { f.BeforeSend = null; }
        AssertCommitted();
        // Independent protobuf literal encoder retained by the real-action
        // fixture: complete D13 action137 echo, not a production projection.
        Assert.Equal(JumpGolden(), Assert.Single(f.Sent));
        if (away)
        {
            Assert.Equal(2, f.PeerSent.Count);
            Assert.True(LegacyPlayerSpawnPacket.TryParse(Read(f.PeerSent[0]), out var spawn));
            Assert.Equal((ulong)Uid, spawn.GetNumericField(2));
            Assert.Equal(432UL, spawn.GetNumericField(27)); Assert.Equal(381UL, spawn.GetNumericField(28));
            Assert.Equal(6UL, spawn.GetNumericField(30)); Assert.Equal(100UL, spawn.GetNumericField(31));
            Assert.Equal(0UL, spawn.GetNumericField(37)); Assert.All(spawn.StatusFlags, word => Assert.Equal(0UL, word));
            Assert.Equal(JumpGolden(), f.PeerSent[1]);
            Assert.Equal(new[] { Peer, Uid, Peer }, f.Order.Select(row => row.Recipient));
        }
        else
        {
            Assert.Equal(JumpGolden(), Assert.Single(f.PeerSent));
            Assert.Equal(new[] { Uid, Peer }, f.Order.Select(row => row.Recipient));
        }
        Assert.Equal(vectorShape == 0 ? 0 : vectorShape == 3 ? 2 : 1,
            f.Current.Character.Attributes.Keys.Count(IsJumpVectorName));
        // Zero-vector representation coverage does not claim a live flag
        // removal, Ride cost, timer retirement or armed attack cancellation.
    });

    [Theory]
    [InlineData("angle")] [InlineData("away")]
    [InlineData("duplicate-status")] [InlineData("duplicate-bitvector")] [InlineData("conflicting-vectors")]
    public Task ActualJumpRefusesEscapedAmbiguousInputsBeforeClearingEarnedAwayOrMoving(string invalidKind) => Run(async f =>
    {
        await f.StanceAsync(250);
        await f.SendAsync(new MsgActionPacket { ObjId = Uid, Type = MsgActionType.Away }.ToFrameBytes());
        Assert.Equal("1", f.Current.Character.Attributes["Away"]); ClearOutput(f);
        var fields = PublishJumpAttributeMap(f, 3);
        var before = f.Current; var peer = f.Get(Peer); var binding = f.Native.OrdinarySpellBinding!;
        var files = f.Data.Files(); var native = JumpAttributeNativeValues(f); var slot = CaptureJumpAttributeSlot(f);
        // Declared negative escaped plain DATA after valid Store publication.
        // No owner/binding/capability is injected; restore it for real cleanup.
        Assert.Same(fields, before.Character.Attributes);
        var (key, value) = invalidKind switch
        {
            "angle" => ("ANGLE", "4"),
            "away" => ("aWaY", "0"),
            "duplicate-status" => ("StatusFlags", ZeroJumpVector),
            "duplicate-bitvector" => ("BitVector", ZeroJumpVector),
            "conflicting-vectors" => ("bItVeCtOr", "1/"),
            _ => throw new InvalidOperationException("Unknown declared negative input."),
        };
        bool present = fields.TryGetValue(key, out var previous);
        fields[key] = value;
        var invalid = fields.ToArray();
        try
        {
            await f.JumpAsync();
            AssertJumpAttributeRefusal(f, before, peer, binding, files, native, slot);
            Assert.Equal(invalid, fields.ToArray());
            Assert.Equal("1", fields["Away"]);
        }
        finally { if (present) fields[key] = previous!; else fields.Remove(key); }
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
    });

    [Theory]
    [InlineData("action")]
    [InlineData("duplicate-status")]
    [InlineData("duplicate-bitvector")]
    [InlineData("conflicting-vectors")]
    public Task ActualStoreRefusesAmbiguousActionOrVectorDataBeforeItCanAuthorizeJump(string invalidKind) => Run(async f =>
    {
        await f.StanceAsync(250); ClearOutput(f);
        PublishJumpAttributeMap(f, 3);
        var before = f.Current; var peer = f.Get(Peer); var binding = f.Native.OrdinarySpellBinding!;
        var files = f.Data.Files(); var native = JumpAttributeNativeValues(f); var slot = CaptureJumpAttributeSlot(f);
        var invalid = new Dictionary<string, string>(before.Character.Attributes, StringComparer.Ordinal);
        switch (invalidKind)
        {
            case "action": invalid.Add("ACTION", "0"); break;
            case "duplicate-status": invalid.Add("StatusFlags", ZeroJumpVector); break;
            case "duplicate-bitvector": invalid.Add("BitVector", ZeroJumpVector); break;
            case "conflicting-vectors": invalid["bItVeCtOr"] = "1/"; break;
            default: throw new InvalidOperationException("Unknown declared negative input.");
        }
        var raw = invalid.ToArray();
        // The real generic owner rejects these earlier than Jump. Do not
        // weaken that admission proof to make a late handler assertion pass.
        if (invalidKind == "action")
            Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection,
                before with { Character = before.Character with { Attributes = invalid } }));
        else
            Assert.Throws<InvalidDataException>(() => f.Store.Set(f.Connection,
                before with { Character = before.Character with { Attributes = invalid } }));
        Assert.Equal(raw, invalid.ToArray());
        AssertJumpAttributeRefusal(f, before, peer, binding, files, native, slot);
    });
}
