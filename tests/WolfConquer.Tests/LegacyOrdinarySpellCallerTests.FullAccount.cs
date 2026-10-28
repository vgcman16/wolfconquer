using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinarySpellCallerTests
{
    [Fact]
    public async Task GenuineAdmittedFullAccountWarriorRetainsOpaqueSpellAndPublishesCanonicalRows()
    {
        await Run(async f =>
        {
            await f.LoginAsync(); var before = f.Current; AssertNative(f);
            Assert.IsType<LegacyNativeSpellStorage.CanonicalSessionSpellRows>(before.PlayerSpells.Spells);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection, before));
            Assert.True(f.Recovery.IsReadyForCharacterSpells(f.Users, f.Spells));
            byte[] prior = File.ReadAllBytes(f.SpellPath); Assert.Equal(0xA5, prior[15]); int first = f.Sent.Count;
            Assert.True(await FullAccount(f, before)); var after = f.Current;
            Assert.NotSame(before, after); Assert.Equal(2005, after.Character.Class); Assert.Equal(140, after.Character.Level);
            Assert.Equal(before.Character.Money, after.Character.Money); Assert.Equal(before.Character.ConquerPoints, after.Character.ConquerPoints);
            Assert.Same(before.PlayerItems, after.PlayerItems); AssertNative(f);
            byte[] saved = File.ReadAllBytes(f.SpellPath); Assert.Equal(prior.AsSpan(4, 12).ToArray(), saved.AsSpan(4, 12).ToArray());
            Assert.Equal(0xA5, saved[15]); Assert.False(LegacyTradeJournal.HasPending(f.Users));
            var spells = f.Sent.Skip(first).Where(bytes => Frame(bytes).Header.Opcode == LegacySpellPacket.Opcode).ToArray();
            Assert.Equal(9, spells.Length);
            Assert.Equal(new ushort[] { 1095, 1020, 1090, 1025, 1045, 1046, 1020, 1015, 1040 }, spells.Select(bytes =>
            {
                Assert.True(LegacySpellPacket.TryParse(Frame(bytes), out var packet)); return packet.Id;
            }));
            Assert.Equal(Convert.FromHexString("19003508000000004704000000000000000000000000000000"), spells[0]);
            Assert.True(f.Disconnected);
        }, lineage: false);
    }

    [Theory]
    [InlineData("blocked-recovery")]
    [InlineData("newer-character")]
    [InlineData("newer-spells")]
    [InlineData("native-divergence")]
    public async Task GenuineAdmittedFullAccountRefusalPreservesResourcesFilesAndSuccessOutputs(string fault)
    {
        await Run(async f =>
        {
            await f.LoginAsync(); var before = f.Current; AssertNative(f);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection, before));
            if (fault == "blocked-recovery") f.Recovery.Block("owned refusal control");
            else if (fault == "newer-character")
            {
                string text = Encoding.UTF8.GetString(File.ReadAllBytes(f.CharacterPath));
                Assert.Contains("Money=1000", text); File.WriteAllText(f.CharacterPath, text.Replace("Money=1000", "Money=1001", StringComparison.Ordinal), new UTF8Encoding(false));
            }
            else if (fault == "newer-spells")
            {
                byte[] bytes = File.ReadAllBytes(f.SpellPath); bytes[8] ^= 1; File.WriteAllBytes(f.SpellPath, bytes);
            }
            else Assert.IsType<LegacyNativeSpellStorage>(f.Native.MySpells).ClientSpells[6001].Experience++;
            var files = f.Files(); int first = f.Sent.Count;
            Assert.True(await FullAccount(f, before));
            Assert.Same(before, f.Current); Assert.Equal(before.Character.Money, f.Current.Character.Money);
            Assert.Equal(before.Character.ConquerPoints, f.Current.Character.ConquerPoints);
            Assert.Equal(first, f.Sent.Count); Assert.False(f.Disconnected); f.RequireFiles(files);
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        }, lineage: false);
    }
}
