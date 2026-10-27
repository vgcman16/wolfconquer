using System.Buffers.Binary;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionFixtureTests
{
    // Literal native Add(1110) grant: 25-byte frame2101; zero-level/XP/soul row.
    private static readonly byte[] ProfileCycloneFrame =
    [
        0x19, 0x00, 0x35, 0x08, 0, 0, 0, 0, 0x56, 0x04,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];
    private static readonly byte[] ProfileCycloneFile = Convert.FromHexString("01000000560400000000000000000000");

    private void AssertProfileCommitted(LegacyOwnedAdmissionOptions options, Dictionary<string, byte[]> before)
    {
        var loader = options.CreateSnapshotLoader();
        AssertProfileSaved(loader, 1000);
        Assert.Equal(1, loader.LoadByAccountId(AccountUid)!.Online);
        var spell = Assert.Single(loader.LoadPlayerSpellsByCharacterUid(AccountUid).Spells);
        Assert.Equal(new PlayerSpellSnapshotEntry(1110, 0, 0, 0, 0, 0), spell);
        Assert.Equal(ProfileCycloneFile, File.ReadAllBytes(Path.Combine(options.SpellsRoot, AccountUid + ".bin")));
        var after = Files(Destination);
        Assert.Equal(before.Keys.Order(StringComparer.OrdinalIgnoreCase), after.Keys.Order(StringComparer.OrdinalIgnoreCase));
        var character = Path.Combine("Users", AccountUid + ".ini");
        var spells = Path.Combine("PlayersSpells", AccountUid + ".bin");
        foreach (var (path, bytes) in before)
            if (!string.Equals(path, character, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(path, spells, StringComparison.OrdinalIgnoreCase)) Assert.Equal(bytes, after[path]);
        var original = CharacterSnapshotFileEncoding.Decode(before[character], out var encoding);
        Assert.Single(Regex.Matches(original, "(?m)^Online=0(?=\\r?$)"));
        var expected = Regex.Replace(original, "(?m)^Online=0(?=\\r?$)", "Online=1");
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(expected)).ToArray(), after[character]);
        Assert.False(LegacyTradeJournal.HasPending(options.UsersRoot));
        AssertInitialNamespace(Destination, options.ExpectedDomain);
    }

    private static void AssertD13ProfileHeroClasses(byte[][] frames)
    {
        var loader = Assert.Single(frames, value => BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(2, 2)) == 22210);
        Assert.Equal(new byte[] { 0xE8, 0x03, 0, 0 }, loader.AsSpan(17, 4).ToArray());
        var hero = Assert.Single(frames, value => BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(2, 2)) == 2221);
        var offset = 4;
        var classes = 0;
        while (offset < hero.Length)
        {
            var start = offset;
            var tag = ReadOwnedAppearanceVarint(hero, ref offset);
            Assert.NotEqual(0UL, tag);
            if ((tag & 7) == 0)
            {
                var value = ReadOwnedAppearanceVarint(hero, ref offset);
                if ((tag >> 3) == 22)
                {
                    classes++;
                    Assert.Equal(1000UL, value);
                    Assert.Equal(new byte[] { 0xB0, 0x01, 0xE8, 0x07 }, hero.AsSpan(start, offset - start).ToArray());
                }
            }
            else
            {
                Assert.Equal(2UL, tag & 7);
                var length = ReadOwnedAppearanceVarint(hero, ref offset);
                Assert.InRange(length, 0UL, (ulong)(hero.Length - offset));
                offset += checked((int)length);
            }
        }
        Assert.Equal(1, classes);
    }

    private static byte[] ProfileConfirmSpells() => new MsgActionPacket
    {
        ObjId = AccountUid, Timestamp = 1234, Type = MsgActionType.ConfirmSpells,
        PositionX = 410, PositionY = 354, Facing = 3,
    }.ToFrameBytes();

    private static bool IsProfileOpcode(byte[] frame, ushort opcode) =>
        frame.Length >= 4 && BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2, 2)) == opcode;
}
