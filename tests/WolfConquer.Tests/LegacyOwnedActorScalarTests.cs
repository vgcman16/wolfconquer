using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActorScalarTests
{
    [Fact]
    public Task ActualAdmissionAdoptsDeclaredSavedSixScalarsWithoutInventingRebirthOrInstanceAuthorization() => Run(f =>
    {
        AssertCoherent(f, 430, 380, 250);
        Assert.Equal((ushort)0, f.Native.Player.Px); Assert.Equal((ushort)0, f.Native.Player.Py);
        Assert.Equal("907", f.Current.Character.Attributes["DynamicID"]);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task GenuineFrameMovementPublishesCoordinatesActionAndSourceCursorsBeforeReply(bool jump) => Run(async f =>
    {
        var before = f.Data.Files(); var expected = MovedFiles(f, before, jump ? 432 : 431, jump ? 381 : 380);
        var items = f.Binding.Items.Image; var spells = f.Native.MySpells!.ClientSpells;
        f.BeforeSend = _ =>
        {
            f.Data.AssertFiles(expected); AssertCoherent(f, jump ? (ushort)432 : (ushort)431, jump ? (ushort)381 : (ushort)380, jump ? 100U : 0U);
            Assert.Equal((ushort)430, f.Native.Player.Px); Assert.Equal((ushort)380, f.Native.Player.Py);
            Assert.Same(items, f.Binding.Items.Image); Assert.Same(spells, f.Native.MySpells.ClientSpells);
        };
        try { await f.SendAsync(jump ? Jump() : Walk()); } finally { f.BeforeSend = null; }
        Assert.Single(f.Sent); f.Data.AssertFiles(expected);
        AssertCoherent(f, jump ? (ushort)432 : (ushort)431, jump ? (ushort)381 : (ushort)380, jump ? 100U : 0U);
        var moved = f.Current; var fields = new Dictionary<string, string>(moved.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["ActorScalarPublicationSentinel"] = "after" };
        // Actual typed Store owner publication, not player authorization to
        // mutate a scalar. An unrelated/equal-coordinate update keeps cursors.
        f.Store.Set(f.Connection, moved with { Character = moved.Character with { Attributes = fields } }, out var final);
        Assert.Same(final, f.Current); Assert.Equal("after", final.Character.Attributes["ActorScalarPublicationSentinel"]);
        Assert.Equal((ushort)430, f.Native.Player.Px); Assert.Equal((ushort)380, f.Native.Player.Py);
        Assert.Single(f.Sent); f.Data.AssertFiles(expected);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualAttachedOrEofRetiredActorRejectsAllDirectSettersBeforeAnyCursorEffect(bool retired) => Run(async f =>
    {
        var player = f.Native.Player; var binding = f.Binding;
        if (retired)
        {
            await f.Connection.CompleteTransportCloseAsync();
            Assert.True(binding.IsRetired); Assert.False(f.Store.TryGet(f.Connection, out _));
        }
        var files = f.Data.Files(); int outputs = f.Sent.Count; var current = retired ? null : f.Current;
        Assert.Throws<InvalidOperationException>(() => player.Reborn = 0);
        Assert.Throws<InvalidOperationException>(() => player.Agility = 0);
        Assert.Throws<InvalidOperationException>(() => player.X = 0);
        Assert.Throws<InvalidOperationException>(() => player.Y = 0);
        Assert.Throws<InvalidOperationException>(() => player.Map = 0);
        Assert.Throws<InvalidOperationException>(() => player.DynamicID = 0);
        Assert.Equal((byte)2, player.Reborn); Assert.Equal((ushort)36, player.Agility);
        Assert.Equal((ushort)430, player.X); Assert.Equal((ushort)380, player.Y);
        Assert.Equal(1000U, player.Map); Assert.Equal(907U, player.DynamicID);
        Assert.Equal((ushort)0, player.Px); Assert.Equal((ushort)0, player.Py);
        Assert.Equal(1, player.HitPoints); Assert.Equal(50U, player.Mana); Assert.Equal((ushort)72, player.Stamina);
        Assert.Equal(250U, (uint)player.Action); f.Data.AssertFiles(files); Assert.Equal(outputs, f.Sent.Count);
        if (!retired) Assert.Same(current, f.Current);
    });

    [Theory]
    [InlineData("_reborn")] [InlineData("_agility")] [InlineData("xx")]
    [InlineData("yy")] [InlineData("_mmmap")] [InlineData("_dynamicId")]
    public Task EscapedPhysicalScalarDriftRefusesAnActualJointStorePublicationWithoutAnyOtherEffect(string fieldName) => Run(f =>
    {
        var before = f.Current; var player = f.Native.Player; var files = f.Data.Files();
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Binding));
        var field = typeof(LegacyNativePlayerRuntime).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        object original = field.GetValue(player)!;
        object corrupt = original switch { byte number => (object)(byte)(number + 1), ushort number => (object)(ushort)(number + 1),
            uint number => number + 1, _ => throw new InvalidOperationException("Unexpected actual scalar backing type.") };
        try
        {
            // Declared NEGATIVE field escape only. No positive native scalar,
            // queue, permission or proposed-value capability is manufactured.
            field.SetValue(player, corrupt);
            var attrs = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Action"] = "0" };
            Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection, before with
                { Character = before.Character with { MinMana = 1, Attributes = attrs } }));
            Assert.Equal(corrupt, field.GetValue(player)); Assert.Same(before, f.Current);
            Assert.Equal(250U, (uint)player.Action); Assert.Equal(1, player.HitPoints); Assert.Equal(50U, player.Mana);
            Assert.Equal((ushort)72, player.Stamina); Assert.Equal((ushort)0, player.Px); Assert.Equal((ushort)0, player.Py);
            f.Data.AssertFiles(files); Assert.Empty(f.Sent);
        }
        finally { field.SetValue(player, original); }
        AssertCoherent(f, 430, 380, 250); f.Data.AssertFiles(files); return Task.CompletedTask;
    });

    [Theory]
    [InlineData("-1")] [InlineData("4294967296")] [InlineData("")] [InlineData("not-a-number")]
    public Task MalformedSavedDynamicIdentityRefusesRealLoginBeforeAnyOwnerResourceOrOutput(string raw) => Run(async f =>
    {
        AssertUnadmitted(f);
        await f.SendAsync(Walk()); await f.SendAsync(Jump());
        await f.SendAsync(new MsgActionPacket { Type = MsgActionType.ChangeStance, DwParam = 250 }.ToFrameBytes());
        AssertUnadmitted(f);
    }, dynamicId: raw, admit: false);
}
