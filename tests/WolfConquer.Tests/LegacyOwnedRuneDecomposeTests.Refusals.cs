using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneDecomposeTests
{
    [Theory]
    [InlineData("locked")] [InlineData("stacked")] [InlineData("child")]
    [InlineData("inventory-full")] [InlineData("bag-full")]
    [InlineData("missing-cap")] [InlineData("malformed-cap")] [InlineData("missing-progress")]
    public Task ActualDecomposePreflightRejectsInvalidCostOrRewardStateBeforeAnyUidReservation(string fault) => Run(async f =>
    {
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Native.OrdinarySpellBinding!));
        await AssertDecomposeRefused(f, () => f.SendAsync(DecomposeFixture.Request()));
        f.AssertConverged();
    }, fault: fault);

    [Theory]
    [InlineData("unknown-uid")] [InlineData("zero-uid")] [InlineData("zero-count")]
    [InlineData("fifty-count")] [InlineData("maximum-count")]
    [InlineData("short-prefix")] [InlineData("short-forty-nine")] [InlineData("no-argument")]
    public Task ActualMalformedDecomposeRequestCannotChoosePacketIdOrReserveRewardUids(string fault) => Run(async f =>
    {
        uint uid = fault == "unknown-uid" ? DecomposeFixture.Target + 999 : fault == "zero-uid" ? 0 : DecomposeFixture.Target;
        uint count = fault switch { "zero-count" => 0, "fifty-count" => 50, "maximum-count" => uint.MaxValue,
            "short-prefix" => 2, "short-forty-nine" => 49, _ => 1 };
        var bytes = DecomposeFixture.Request(uid, count);
        if (fault == "no-argument")
        {
            bytes = bytes[..^sizeof(uint)];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        }
        await AssertDecomposeRefused(f, () => f.SendAsync(bytes));
        f.AssertConverged();
    });

    [Theory]
    [InlineData("ambiguous-worn")] [InlineData("ambiguous-alternate")]
    [InlineData("absent-worn")] [InlineData("absent-alternate")]
    public Task Worn8140CannotBorrowTheStoredRuneClassificationExemption(string mode) => Run(async f =>
    {
        Assert.Contains(f.Current.PlayerItems.RuneItems, row => row.ItemId == 4_028_001 && row.Position is 102 or 122);
        await AssertDecomposeRefused(f, () => f.SendAsync(DecomposeFixture.Request()));
        f.AssertConverged();
    }, mode: mode);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DriftInTheActualAdoptedRewardOrCurveCannotAuthorizeDecomposeBirths(bool progress) => Run(async f =>
    {
        var fields = progress
            ? Assert.IsType<string[]>(Assert.Single(f.Catalog.RuneLevelExperience, row => row.RuneType == 402 && row.Level == 17).Fields)
            : Assert.IsType<string[]>(Assert.Single(f.Catalog.ItemsById[(int)DecomposeFixture.FragmentType]).Fields);
        int offset = progress ? 3 : ItemType.StackSizeFieldIndex; string prior = fields[offset];
        fields[offset] = "invalid";
        try { await AssertDecomposeRefused(f, () => f.SendAsync(DecomposeFixture.Request())); }
        finally { fields[offset] = prior; }
        f.AssertConverged();
    });

    [Theory]
    [InlineData("Character")] [InlineData("Items")] [InlineData("Spells")]
    public Task ActualSavedBeforeDriftRefusesDecomposeWithoutRepairOrUidReservation(string kind) => Run(async f =>
    {
        string path = kind switch { "Character" => f.CharacterPath, "Items" => f.ItemsPath, _ => f.SpellsPath };
        byte[] original = File.ReadAllBytes(path), changed;
        if (kind == "Character")
        {
            string text = CharacterSnapshotFileEncoding.Decode(original, out var encoding);
            var character = CharacterSnapshotParser.ParseText(text);
            string old = "Money=" + character.Money.ToString(CultureInfo.InvariantCulture) + "\r\n";
            Assert.Contains(old, text, StringComparison.Ordinal);
            string next = "Money=" + checked(character.Money + 1).ToString(CultureInfo.InvariantCulture) + "\r\n";
            changed = encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace(old, next, StringComparison.Ordinal))).ToArray();
        }
        else if (kind == "Items")
        {
            var items = PlayerItemSnapshotLoader.Parse(original);
            changed = PlayerItemSnapshotLoader.ToLegacyBytes(items with { RuneItems = items.RuneItems.Select(row =>
                row.Uid == DecomposeFixture.Target ? row.WithRuneExperience(row.RuneExperience + 1) : row).ToArray() });
        }
        else { changed = original.ToArray(); changed[15] ^= 1; }
        Assert.NotEqual(original, changed); File.WriteAllBytes(path, changed);
        try { await AssertDecomposeRefused(f, () => f.SendAsync(DecomposeFixture.Request())); }
        finally { File.WriteAllBytes(path, original); }
        f.AssertConverged();
    });

    [Fact]
    public Task EndedRealRuntimeCannotReserveDecomposeRewardsThroughItsRetainedFrame() => Run(async f =>
    {
        await f.Runtime.DisposeAsync();
        await AssertDecomposeRefused(f, () => f.SendAsync(DecomposeFixture.Request()), blocked: true);
    });
}
