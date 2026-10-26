using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task OrdinaryLoginPublishesOnlyNewCommittedCycloneAtItsSourceStartupSlot()
    {
        using var fixture = new SavedSpellPublicationFixture(_usersRoot, alreadyCyclone: false);
        var initial = File.ReadAllBytes(fixture.SpellPath);
        var observed = 0;
        fixture.ObserveSend = frame =>
        {
            if (!IsSpellFrame(frame)) return;
            observed++;
            Assert.Contains("WorldServer ordinary admission committed key=1293600.", fixture.Log.ToString());
            Assert.Equal(new ushort[] { 6001, 1110 }, fixture.Current.PlayerSpells.Spells.Select(row => row.Id));
            Assert.Equal(new ushort[] { 6001, 1110 }, PlayerSpellSnapshotLoader.Parse(File.ReadAllBytes(fixture.SpellPath)).Spells.Select(row => row.Id));
        };
        await fixture.Login();
        Assert.Equal(1, observed);
        var spellIndex = fixture.Sent.FindIndex(IsSpellFrame);
        Assert.Equal(NewCycloneWire, fixture.Sent[spellIndex]);
        var sashIndex = fixture.Sent.FindLastIndex(frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.InventorySash));
        var groupIndex = fixture.Sent.FindIndex(frame => ReadFrame(frame).Header.Opcode == LegacySameGroupServerListPacket.Opcode);
        Assert.True(sashIndex >= 0 && spellIndex == sashIndex + 1);
        Assert.Equal(spellIndex + 1, groupIndex);
        Assert.Equal(initial.AsSpan(4).ToArray(), File.ReadAllBytes(fixture.SpellPath).AsSpan(4, initial.Length - 4).ToArray());
        await fixture.AssertAdmission(); // Replayed login does not resend/regrant.
        Assert.Equal(1, observed);
    }

    [Fact]
    public async Task OrdinaryExistingCycloneIsNotRegrantedAndAction78PublishesStoredOrderThenExactEcho()
    {
        using var fixture = new SavedSpellPublicationFixture(_usersRoot, alreadyCyclone: true);
        var spellBytes = File.ReadAllBytes(fixture.SpellPath);
        await fixture.Login();
        Assert.DoesNotContain(fixture.Sent, IsSpellFrame);
        await fixture.AssertAdmission();
        var before = fixture.Current;
        var files = fixture.Files();
        var first = fixture.Sent.Count;
        var request = ConfirmSpellRequest();
        await fixture.Confirm(request);
        Assert.Equal(first + 3, fixture.Sent.Count);
        Assert.Equal(LearnedWire, fixture.Sent[first]);
        Assert.Equal(LearnedCycloneWire, fixture.Sent[first + 1]);
        Assert.Equal(request, fixture.Sent[first + 2]);
        Assert.Same(before, fixture.Current);
        Assert.Equal(spellBytes, File.ReadAllBytes(fixture.SpellPath));
        fixture.AssertFiles(files);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryNewCyclonePublicationStopsItsTailWhenRealOwnerRetires(bool duringGrant)
    {
        using var fixture = new SavedSpellPublicationFixture(_usersRoot, alreadyCyclone: false);
        var retired = 0;
        fixture.ObserveSend = frame =>
        {
            var boundary = duringGrant ? IsSpellFrame(frame) :
                LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                    update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.InventorySash);
            if (!boundary || retired++ != 0) return;
            if (duringGrant) fixture.EndLifetime();
            else Assert.True(fixture.Store.TryRemove(fixture.Connection, out _));
        };
        await fixture.Login();
        Assert.Equal(1, retired);
        Assert.Equal(duringGrant ? 1 : 0, fixture.Sent.Count(IsSpellFrame));
        Assert.DoesNotContain(fixture.Sent, frame => ReadFrame(frame).Header.Opcode == LegacySameGroupServerListPacket.Opcode);
        Assert.DoesNotContain(fixture.Sent, frame => ReadFrame(frame).Header.Opcode == WorldPacketOpcodes.LegacyRuneStorage);
        Assert.DoesNotContain("WorldServer legacy MsgLoginGame Success", fixture.Log.ToString());
        Assert.Equal(new ushort[] { 6001, 1110 }, PlayerSpellSnapshotLoader.Parse(File.ReadAllBytes(fixture.SpellPath)).Spells.Select(row => row.Id));
    }

    [Theory]
    [InlineData(0)] // Remove/recreate the actual Store holder with the same UID.
    [InlineData(1)] // End the actual frame-owned transport lifetime.
    [InlineData(2)] // Replace the actual learned set while a send is awaited.
    public async Task Action78StopsRemainingSpellsAndEchoAfterCapturedOwnerOrMembershipRetires(int mode)
    {
        using var fixture = new SavedSpellPublicationFixture(_usersRoot, alreadyCyclone: true);
        await fixture.Login();
        var files = fixture.Files();
        var first = fixture.Sent.Count;
        var observed = 0;
        fixture.ObserveSend = frame =>
        {
            if (!IsSpellFrame(frame) || observed++ != 0) return;
            var actual = fixture.Current;
            if (mode == 0)
            {
                Assert.True(fixture.Store.TryRemove(fixture.Connection, out _));
                fixture.Store.Set(fixture.Connection, actual);
            }
            else if (mode == 1) fixture.EndLifetime();
            else
            {
                // Corrupt the actual captured collection in place: this must be
                // detected even without a Store version/reference change.
                var rows = Assert.IsType<PlayerSpellSnapshotEntry[]>(actual.PlayerSpells.Spells);
                rows[1] = LearnedCyclone with { Experience = 2223 };
            }
        };
        await fixture.Confirm(ConfirmSpellRequest());
        Assert.Equal(1, observed);
        Assert.Equal(first + 1, fixture.Sent.Count);
        Assert.Equal(LearnedWire, fixture.Sent[first]);
        fixture.AssertFiles(files);
    }

    [Theory]
    [InlineData(0)] // Duplicate ID in the tail: no valid-prefix leak.
    [InlineData(1)] // Negative experience in the tail.
    [InlineData(2)] // Zero ID in the tail.
    [InlineData(3)] // Missing required row in the tail.
    public async Task Action78ValidatesTheWholeLearnedSetBeforeAnyResponse(int defect)
    {
        using var fixture = new SavedSpellPublicationFixture(_usersRoot, alreadyCyclone: true);
        await fixture.Login();
        PlayerSpellSnapshotEntry bad = defect switch
        {
            0 => Learned with { Level = 9 },
            1 => LearnedCyclone with { Experience = -1 },
            2 => LearnedCyclone with { Id = 0 },
            _ => null!, // Deliberately malformed input, not a production owner.
        };
        var poisoned = fixture.Current;
        // Deliberate post-admission runtime corruption, not a substitute owner
        // or saver; do not run auto-persistence to manufacture malformed files.
        var rows = Assert.IsType<PlayerSpellSnapshotEntry[]>(poisoned.PlayerSpells.Spells);
        rows[1] = bad;
        var files = fixture.Files();
        var first = fixture.Sent.Count;
        await fixture.Confirm(ConfirmSpellRequest());
        Assert.Equal(first, fixture.Sent.Count);
        Assert.Same(poisoned, fixture.Current);
        fixture.AssertFiles(files);
    }

    [Fact]
    public async Task Action78AllowsCharacterOnlyVersionWithItsSameRealOwnerAndLearnedSet()
    {
        using var fixture = new SavedSpellPublicationFixture(_usersRoot, alreadyCyclone: true);
        await fixture.Login();
        var rawSpells = File.ReadAllBytes(fixture.SpellPath);
        var first = fixture.Sent.Count;
        var changed = 0;
        fixture.ObserveSend = frame =>
        {
            if (!IsSpellFrame(frame) || changed++ != 0) return;
            var actual = fixture.Current;
            fixture.Store.Set(fixture.Connection, actual with
            { Character = actual.Character with { MinHitPoints = 123 } });
        };
        var request = ConfirmSpellRequest();
        await fixture.Confirm(request);
        Assert.Equal(2, changed);
        Assert.Equal(first + 3, fixture.Sent.Count);
        Assert.Equal(LearnedWire, fixture.Sent[first]);
        Assert.Equal(LearnedCycloneWire, fixture.Sent[first + 1]);
        Assert.Equal(request, fixture.Sent[first + 2]);
        Assert.Equal(123, fixture.Current.Character.MinHitPoints);
        Assert.Equal(rawSpells, File.ReadAllBytes(fixture.SpellPath));
    }

}
