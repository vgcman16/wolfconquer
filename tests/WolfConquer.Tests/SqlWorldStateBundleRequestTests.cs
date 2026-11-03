using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;
using static WolfConquer.Tests.SqlWorldStateBundleTests;

namespace WolfConquer.Tests;

public sealed class SqlWorldStateBundleRequestTests
{
    private static readonly Guid Operation = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Fact]
    public void CompleteDescriptorHasIndependentV2HeaderIdentityAndExplicitMissingSidecarGolden()
    {
        var core = SqlWorldStateTestData.Image(501, 9001);
        var complete = Request(SqlWorldStateOperationKind.Create, Operation, new SqlWorldStateBundleChange(Bundle(core), 0));
        var bytes = complete.CopyDescriptorBytes(); var character = core.CopyCharacterBytes(); var items = core.CopyItemBytes();
        // Literal little-endian framing, including .NET mixed-endian Guid representation.
        Assert.Equal(Convert.FromHexString("574353514C57303202000000010133221100554477668899AABBCCDDEEFF"), bytes[..30]);
        Assert.Equal(Convert.FromHexString("F501000000000000292300000000000000000000"), bytes[30..50]);
        Assert.Equal(character.Length, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(50, 4)));
        Assert.Equal(character, bytes.AsSpan(54, character.Length).ToArray());
        var itemLengthOffset = 54 + character.Length;
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(itemLengthOffset, 4)));
        Assert.Equal(new byte[4], items); Assert.Equal(items, bytes.AsSpan(itemLengthOffset + 4, 4).ToArray());
        Assert.Equal(new byte[4], bytes.AsSpan(itemLengthOffset + 8, 4).ToArray()); // zero physical UIDs
        var suffix = itemLengthOffset + 12;
        Assert.Equal(Convert.FromHexString("010000000000020000000000030000000000040000000000"), bytes[suffix..]);
        Assert.Equal(suffix + 24, bytes.Length); Assert.Equal(SHA256.HashData(bytes), complete.CopyDescriptorHash());

        var old = SqlWorldStateTestData.Request(SqlWorldStateOperationKind.Create, Operation, new SqlWorldStateChange(core, 0));
        var oldBytes = old.CopyDescriptorBytes();
        Assert.Equal("WCSQLW01", Encoding.ASCII.GetString(oldBytes, 0, 8));
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(oldBytes.AsSpan(8, 4)));
        Assert.Equal(suffix, oldBytes.Length); Assert.Equal(bytes.AsSpan(12, suffix - 12).ToArray(), oldBytes[12..]);
    }

    [Theory]
    [InlineData(SqlWorldStateSidecarKind.Spells)]
    [InlineData(SqlWorldStateSidecarKind.Proficiencies)]
    [InlineData(SqlWorldStateSidecarKind.Quests)]
    [InlineData(SqlWorldStateSidecarKind.CombatGear)]
    public void EverySidecarPresenceAndEveryRetainedRawByteParticipatesInExactDescriptor(SqlWorldStateSidecarKind kind)
    {
        var core = SqlWorldStateTestData.Image(501, 9001);
        var absent = Request(SqlWorldStateOperationKind.Create, Operation, new SqlWorldStateBundleChange(Bundle(core), 0));
        var empty = kind == SqlWorldStateSidecarKind.CombatGear ? EmptyGear() : new byte[4];
        var raw = kind switch
        {
            SqlWorldStateSidecarKind.Spells => Spells(),
            SqlWorldStateSidecarKind.Proficiencies => Proficiencies(),
            SqlWorldStateSidecarKind.Quests => Quests(),
            _ => EmptyGear(),
        };
        var changed = raw.ToArray();
        if (kind == SqlWorldStateSidecarKind.CombatGear)
            changed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(raw).Replace("MasteryPoints=0", "MasteryPoints=1", StringComparison.Ordinal));
        else changed[kind == SqlWorldStateSidecarKind.Spells ? 15 : kind == SqlWorldStateSidecarKind.Proficiencies ? 13 : 20] ^= 1;
        var presentEmpty = Request(SqlWorldStateOperationKind.Create, Operation, new SqlWorldStateBundleChange(Bundle(core, Replace(kind, empty)), 0));
        var present = Request(SqlWorldStateOperationKind.Create, Operation, new SqlWorldStateBundleChange(Bundle(core, Replace(kind, raw)), 0));
        var different = Request(SqlWorldStateOperationKind.Create, Operation, new SqlWorldStateBundleChange(Bundle(core, Replace(kind, changed)), 0));
        Assert.False(absent.CopyDescriptorBytes().AsSpan().SequenceEqual(presentEmpty.CopyDescriptorBytes()));
        Assert.False(present.CopyDescriptorHash().AsSpan().SequenceEqual(different.CopyDescriptorHash()));
        var offset = absent.CopyDescriptorBytes().Length - 24 + ((int)kind - 1) * 6;
        foreach (var pair in new[] { (presentEmpty, empty), (present, raw), (different, changed) })
        {
            var descriptor = pair.Item1.CopyDescriptorBytes();
            Assert.Equal((byte)kind, descriptor[offset]); Assert.Equal((byte)1, descriptor[offset + 1]);
            Assert.Equal(pair.Item2.Length, BinaryPrimitives.ReadInt32LittleEndian(descriptor.AsSpan(offset + 2, 4)));
            Assert.Equal(pair.Item2, descriptor.AsSpan(offset + 6, pair.Item2.Length).ToArray());
            Assert.Equal(absent.CopyDescriptorBytes().Length + pair.Item2.Length, descriptor.Length);
        }
    }

    [Fact]
    public void RequestOwnsChangesSortsAccountsAndPhysicalUidsAndHashesExactOperationAndRevision()
    {
        var initial = SqlWorldStateTestData.Initial(); var first = Bundle(initial.First, Replace(SqlWorldStateSidecarKind.Spells, Spells()));
        var second = Bundle(initial.Second, Replace(SqlWorldStateSidecarKind.Quests, Quests()));
        var input = new SqlWorldStateBundleChange[] { new(second, 3), new(first, 2) };
        var request = Request(SqlWorldStateOperationKind.SavePair, Operation, input);
        var bytes = request.CopyDescriptorBytes(); var hash = request.CopyDescriptorHash();
        var ordered = Request(SqlWorldStateOperationKind.SavePair, Operation, new(first, 2), new(second, 3));
        Assert.Equal(bytes, ordered.CopyDescriptorBytes()); Assert.Equal(hash, ordered.CopyDescriptorHash());
        Assert.Equal(new long[] { 501, 502 }, request.Changes.ToArray().Select(row => row.Image.AccountId));
        Assert.Equal(new uint[] { 71001, 71002, 71003 }, request.Changes[0].Image.ItemUids.ToArray());
        var uidOffset = 30 + 20 + 4 + initial.First.CopyCharacterBytes().Length + 4 + initial.First.CopyItemBytes().Length;
        Assert.Equal(Convert.FromHexString("03000000591501005A1501005B150100"), bytes.AsSpan(uidOffset, 16).ToArray());
        input[0] = new(first, 99); Array.Clear(request.CopyDescriptorBytes()); Array.Clear(request.CopyDescriptorHash());
        var copies = request.Changes.ToArray(); Array.Clear(copies);
        Assert.Equal(bytes, request.CopyDescriptorBytes()); Assert.Equal(hash, request.CopyDescriptorHash());
        Assert.Same(first, request.Changes[0].Image); Assert.Same(second, request.Changes[1].Image);
        Assert.Equal(2, request.Changes[0].ExpectedRevision); Assert.Equal(3, request.Changes[1].ExpectedRevision);
        Assert.False(hash.AsSpan().SequenceEqual(Request(SqlWorldStateOperationKind.SavePair, Guid.NewGuid(), new(first, 2), new(second, 3)).CopyDescriptorHash()));
        Assert.False(hash.AsSpan().SequenceEqual(Request(SqlWorldStateOperationKind.SavePair, Operation, new(first, 3), new(second, 3)).CopyDescriptorHash()));
        Assert.Equal(SHA256.HashData(bytes), hash);
    }

    [Fact]
    public void RevisionParticipantAndWholeClosureConflictRulesRemainTheActualV1StorageRules()
    {
        var initial = SqlWorldStateTestData.Initial(); var first = Bundle(initial.First); var second = Bundle(initial.Second);
        var sameCharacter = Bundle(SqlWorldStateTestData.Image(502, 9001, SqlWorldStateTestData.SecondItems()));
        var foreignChild = Bundle(SqlWorldStateTestData.Image(502, 9002, PlayerItemSnapshot.Empty with
        { InventoryItems = [SqlWorldStateTestData.Item(SqlWorldStateTestData.ChildUid)] }));
        var cases = new[]
        {
            (Guid.Empty, SqlWorldStateOperationKind.Create, new SqlWorldStateBundleChange[] { new(first, 0) }, SqlWorldStateFailure.InvalidOperationId),
            (Operation, (SqlWorldStateOperationKind)0, new SqlWorldStateBundleChange[] { new(first, 0) }, SqlWorldStateFailure.InvalidOperationKind),
            (Operation, SqlWorldStateOperationKind.SavePair, new SqlWorldStateBundleChange[] { new(first, 1) }, SqlWorldStateFailure.ParticipantCount),
            (Operation, SqlWorldStateOperationKind.Save, Array.Empty<SqlWorldStateBundleChange>(), SqlWorldStateFailure.ParticipantCount),
            (Operation, SqlWorldStateOperationKind.Save, new SqlWorldStateBundleChange[] { new(null!, 1) }, SqlWorldStateFailure.InvalidCharacterImage),
            (Operation, SqlWorldStateOperationKind.Create, new SqlWorldStateBundleChange[] { new(first, 1) }, SqlWorldStateFailure.InvalidRevision),
            (Operation, SqlWorldStateOperationKind.Save, new SqlWorldStateBundleChange[] { new(first, 0) }, SqlWorldStateFailure.InvalidRevision),
            (Operation, SqlWorldStateOperationKind.Save, new SqlWorldStateBundleChange[] { new(first, long.MaxValue) }, SqlWorldStateFailure.InvalidRevision),
            (Operation, SqlWorldStateOperationKind.SavePair, new SqlWorldStateBundleChange[] { new(first, 1), new(first, 1) }, SqlWorldStateFailure.ParticipantIdentityConflict),
            (Operation, SqlWorldStateOperationKind.SavePair, new SqlWorldStateBundleChange[] { new(first, 1), new(sameCharacter, 1) }, SqlWorldStateFailure.ParticipantIdentityConflict),
            (Operation, SqlWorldStateOperationKind.SavePair, new SqlWorldStateBundleChange[] { new(first, 1), new(foreignChild, 1) }, SqlWorldStateFailure.DuplicateItemUid),
        };
        foreach (var (id, kind, changes, expected) in cases)
        {
            Assert.False(SqlWorldStateBundleRequest.TryCreate(id, kind, changes, out var request, out var failure));
            Assert.Null(request); Assert.Equal(expected, failure);
        }
        Assert.Equal(0, Request(SqlWorldStateOperationKind.Create, Operation, new SqlWorldStateBundleChange(first, 0)).Changes[0].ExpectedRevision);
        Assert.Equal(long.MaxValue - 1, Request(SqlWorldStateOperationKind.Save, Operation, new SqlWorldStateBundleChange(first, long.MaxValue - 1)).Changes[0].ExpectedRevision);
        Assert.Same(second, Request(SqlWorldStateOperationKind.SavePair, Operation, new(second, 1), new(first, 1)).Changes[1].Image);
    }

    private static SqlWorldStateBundleRequest Request(SqlWorldStateOperationKind kind, Guid operation, params SqlWorldStateBundleChange[] changes)
    {
        Assert.True(SqlWorldStateBundleRequest.TryCreate(operation, kind, changes, out var request, out var failure), failure.ToString());
        Assert.Equal(SqlWorldStateFailure.None, failure); return Assert.IsType<SqlWorldStateBundleRequest>(request);
    }
}
