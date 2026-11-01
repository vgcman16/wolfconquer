using System.Collections.ObjectModel;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeOwnedActionTests
{
    // DATA ONLY: projection supplies no admission, persistence or action owner.
    [Theory]
    [InlineData(null, "0")]
    [InlineData("0", "0")]
    [InlineData("65786", "65786")]
    [InlineData("4294967295", "4294967295")]
    [InlineData("MagicDefender", "273344")]
    [InlineData("sIt", "250")]
    [InlineData("273344", "273344")]
    public void AdmissionCanonicalizesFullWidthCompatibilityWithoutNarrowingServerState(string? saved, string expected)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["Opaque"] = "retain" };
        if (saved is not null) fields.Add("aCtIoN", saved);
        var source = Session(fields);
        var final = LegacyActionState.ForAdmission(source);
        Assert.Equal(uint.Parse(expected, CultureInfo.InvariantCulture), LegacyActionState.RequireCurrent(final.Character));
        Assert.Equal(expected, final.Character.Attributes["Action"]);
        Assert.Contains(final.Character.Attributes.Keys, key => key == "Action");
        Assert.Equal("retain", final.Character.Attributes["Opaque"]);
        Assert.NotSame(source.Character.Attributes, final.Character.Attributes);
        Assert.Equal(saved is null ? 1 : 2, fields.Count);
        if (saved is null) Assert.Throws<InvalidOperationException>(() => LegacyActionState.RequireCurrent(source.Character));
        else Assert.Equal(saved, fields["aCtIoN"]);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("4294967296")]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("UnknownAction")]
    [InlineData("Sit, Wave")]
    [InlineData("250,100")]
    public void InvalidWidthAndCombinedEnumTextCannotBecomeAnAction(string saved)
    {
        var source = Session(new Dictionary<string, string> { ["Action"] = saved });
        Assert.Throws<InvalidOperationException>(() => LegacyActionState.ForAdmission(source));
        Assert.Throws<InvalidOperationException>(() => LegacyActionState.RequireCurrent(source.Character));
        Assert.Equal(saved, source.Character.Attributes["Action"]);
    }

    [Fact]
    public void PresentNullRefusesInsteadOfBecomingAbsentNone()
    {
        var source = Session(new Dictionary<string, string> { ["Action"] = null! });
        Assert.Throws<InvalidOperationException>(() => LegacyActionState.ForAdmission(source));
        Assert.Null(source.Character.Attributes["Action"]);
    }

    [Fact]
    public void DuplicateCaseKeysRefuseEvenWhenTheyDescribeTheSameAction()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["Action"] = "250", ["action"] = "250" };
        Assert.Throws<InvalidOperationException>(() => LegacyActionState.ForAdmission(Session(fields)));
        Assert.Equal(2, fields.Count);
    }

    [Fact]
    public void ReadOnlyWrapperCannotHideCustomIndexedValuesBehindBclEnumeration()
    {
        var raw = new SplitViewAttributes { ["Action"] = "65786" };
        var wrapped = new ReadOnlyDictionary<string, string>(raw);
        Assert.Equal("65786", wrapped.Single().Value); Assert.Equal("250", wrapped["Action"]);
        Assert.Throws<InvalidOperationException>(() => new LegacyActionState.NativeValueProof(Session(wrapped).Character));
        Assert.Equal("65786", raw.Single().Value);
    }

    [Theory]
    [InlineData("Action")]
    [InlineData("Opaque")]
    public void FinalProofObservesAllCapturedMapFields(string changed)
    {
        var fields = new Dictionary<string, string> { ["Action"] = "65786", ["Opaque"] = "before" };
        var proof = new LegacyActionState.NativeValueProof(Session(new ReadOnlyDictionary<string, string>(fields)).Character);
        Assert.True(proof.IsCurrentPlain());
        fields[changed] = "250";
        Assert.False(proof.IsCurrentPlain()); Assert.Equal(65786u, proof.Value);
    }

    [Fact]
    public void WholeUnboundComponentKeepsFullUInt32NumericOnlySemantics()
    {
        var client = new LegacyNativeGameClientRuntime();
        Assert.Equal(LegacyNativePlayerMapFlags.ConquerAction.None, client.Player.Action);
        client.Player.Action = (LegacyNativePlayerMapFlags.ConquerAction)uint.MaxValue;
        Assert.Equal(uint.MaxValue, (uint)client.Player.Action);
        lock (client.SpellAdmissionSyncRoot)
        lock (client.Player.SpellAdmissionSyncRoot)
            client.Player.Action = LegacyNativePlayerMapFlags.ConquerAction.MagicDefender;
        Assert.Equal(273344u, (uint)client.Player.Action);
        Assert.Null(client.OrdinarySpellBinding); Assert.False(client.Player.HasOwnedActionAttachment);
    }

    [Fact]
    public Task GenuineStorePublishesFullActionAndRejectsInvalidJointHpChange() => Run(f =>
    {
        var before = f.Current(); var native = before.NativeGameClient!; var files = f.Data.Files();
        Assert.Equal(273344u, (uint)native.Player.Action);
        var fields = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Action"] = "65786" };
        f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor), before with
        { Character = before.Character with { Attributes = fields } }, out var final);
        Assert.Same(final, f.Current()); Assert.Equal(65786u, (uint)native.Player.Action);
        Assert.Equal(65786u, LegacyActionState.RequireCurrent(final.Character));
        Assert.Equal(before.Character.MinHitPoints, final.Character.MinHitPoints);
        Assert.Equal(before.Character.MinMana, final.Character.MinMana);
        Assert.Equal(LegacyStaminaState.RequireCurrent(before.Character), native.Player.Stamina);
        f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames);
        var invalid = new Dictionary<string, string>(final.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Action"] = "4294967296" };
        Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor), final with
        { Character = final.Character with { MinHitPoints = 2, Attributes = invalid } }));
        Assert.Same(final, f.Current()); Assert.Equal(65786u, (uint)native.Player.Action);
        Assert.Equal(checked((int)before.Character.MinHitPoints), native.Player.HitPoints);
        f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames); return Task.CompletedTask;
    });

    [Fact]
    public Task AttachedAndRetiredPlayersCannotUseSetterOrConsumeStatueUid() => Run(f =>
    {
        var current = f.Current(); var client = current.NativeGameClient!; var player = client.Player;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
        var files = f.Data.Files(); var counter = LegacyNativeStatue.CounterUID;
        var count = counter.Count; var statue = LegacyNativeStatue.StaticStatue; var sob = LegacyNativeStatue.StaticSobNpc;
        var angle = player.Angle;
        Assert.Throws<InvalidOperationException>(() => player.Action = LegacyNativePlayerMapFlags.ConquerAction.Sit);
        LegacyNativeStatue.CreateStatue(client, 301, 141, 0, 0, Static: true);
        LegacyNativeStatue.ElitePkStatue(client);
        Assert.Same(counter, LegacyNativeStatue.CounterUID); Assert.Equal(count, counter.Count);
        Assert.Same(statue, LegacyNativeStatue.StaticStatue); Assert.Same(sob, LegacyNativeStatue.StaticSobNpc);
        Assert.Equal(angle, player.Angle); Assert.Equal(273344u, (uint)player.Action);
        Assert.Same(current, f.Current()); f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames);
        Assert.True(f.Store.TryRemove(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out var retired));
        Assert.Same(current, retired); Assert.True(binding.IsRetired); Assert.Same(binding, player.AttachedActionBinding);
        Assert.Throws<InvalidOperationException>(() => player.Action = LegacyNativePlayerMapFlags.ConquerAction.None);
        LegacyNativeStatue.CreateStatue(client, 301, 141, 0, 0, Static: true);
        LegacyNativeStatue.ElitePkStatue(client);
        Assert.Equal(count, counter.Count); Assert.Same(statue, LegacyNativeStatue.StaticStatue);
        Assert.Same(sob, LegacyNativeStatue.StaticSobNpc); Assert.Equal(273344u, (uint)player.Action);
        f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames); return Task.CompletedTask;
    });

    private static WorldSession Session(IReadOnlyDictionary<string, string> fields) => new(1, "DataOnly",
        OwnedEquipmentBehaviorFixture.Character() with { Attributes = fields }, PlayerItemSnapshot.Empty,
        PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);

    private static Task Run(Func<OwnedEquipmentBehaviorFixture, Task> body)
    {
        var character = OwnedEquipmentBehaviorFixture.Character(minHitPoints: 1) with
        { Attributes = new Dictionary<string, string> { ["Action"] = "MagicDefender" } };
        return OwnedEquipmentBehaviorFixture.RunAsync(new(character, PlayerItemSnapshot.Empty),
            OwnedEquipmentBehaviorFixture.Catalog(), f => { f.ActorFrames.Clear(); return body(f); });
    }

    private sealed class SplitViewAttributes : Dictionary<string, string>, IDictionary<string, string>
    {
        string IDictionary<string, string>.this[string key]
        { get => key == "Action" ? "250" : base[key]; set => base[key] = value; }
        bool IDictionary<string, string>.TryGetValue(string key, out string value)
        {
            if (key == "Action") { value = "250"; return true; }
            return base.TryGetValue(key, out value!);
        }
    }
}
