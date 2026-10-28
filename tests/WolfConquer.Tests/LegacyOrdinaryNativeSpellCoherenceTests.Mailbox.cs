using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task RealCombinedOrdinaryAndMailboxPackageCommitRetainsCanonicalSpellsAndNativeRows()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var current = f.Current; var storage = RequireCoherent(f);
            Assert.NotNull(f.PackageProducer); Assert.Same(f.PackageProducer, f.Store.MailboxPackages);
            var spells = current.PlayerSpells; var map = storage.ClientSpells; var rows = map.ToArray();
            byte[] spellFile = File.ReadAllBytes(f.SpellPath); int first = f.Sent.Count;
            var request = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimRecordType, 21, []).ToFrameBytes();
            await f.FrameAsync(request);
            Assert.True(f.Sent.Count > first, f.Log.ToString());
            Assert.NotSame(current, f.Current);
            Assert.Same(spells, f.Current.PlayerSpells); Assert.Same(storage, f.Native.MySpells);
            Assert.Same(map, storage.ClientSpells); Assert.Equal(rows, map.ToArray()); RequireCoherent(f);
            Assert.Equal(spellFile, File.ReadAllBytes(f.SpellPath));
            var mail = Assert.Single(new LegacyMailboxStore(f.Root).Load(WorldServerFrameHandlerTests.NativeSpellJoinFixture.Uid), x => x.Id == 21);
            Assert.Equal(0UL, mail.Flag); Assert.False(LegacyTradeJournal.HasPending(f.Users));
            Assert.True(f.Sent.Count > first);
        }, combined: true, lineage: false);
    }
    [Fact]
    public async Task GenuineMailboxPackageRejectsDivergedRawSpellsBeforeDurableWork()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var current = f.Current; var storage = RequireCoherent(f);
            var native = Assert.Single(storage.ClientSpells.ToArray(), p => p.Key == 6001).Value;
            native.Experience++;
            var files = f.Files(); int first = f.Sent.Count;
            var request = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimRecordType, 21, []).ToFrameBytes();
            await f.FrameAsync(request);
            Assert.Same(current, f.Current); Assert.Equal(1112, native.Experience);
            f.RequireFiles(files); Assert.Equal(first, f.Sent.Count);
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
            var mail = Assert.Single(new LegacyMailboxStore(f.Root).Load(WorldServerFrameHandlerTests.NativeSpellJoinFixture.Uid), x => x.Id == 21);
            Assert.Equal(1UL, mail.Flag);
        }, combined: true, lineage: false);
    }
}
