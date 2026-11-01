using System.Collections.ObjectModel;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public Task RealStanceAndPhysicalPreserveSupportedComparersAndUnrelatedData(int kind, bool wrapped) =>
        RunProficiencyAsync(true, async f =>
        {
            IEqualityComparer<string> comparer = kind switch
            {
                0 => StringComparer.Ordinal,
                1 => StringComparer.OrdinalIgnoreCase,
                2 => EqualityComparer<string>.Default,
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            foreach (uint uid in new[] { Actor, Target })
            {
                var before = f.Current(uid);
                var fields = new Dictionary<string, string>(before.Character.Attributes, comparer) { ["Other"] = "upper" };
                if (kind != 1) fields.Add("other", "lower");
                fields.Remove("Action"); fields.Add("action", LegacyActionState.RequireCurrent(before.Character)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (uid == Actor) fields.Add("activepick", "yes");
                f.Owner.Store.Set(f.Owner.Connection(uid), before with { Character = before.Character with
                    { Attributes = wrapped ? new ReadOnlyDictionary<string, string>(fields) : fields } });
                Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(f.Binding(uid)));
            }
            var files = f.Files(); var stamp = f.Binding().Player.AttackStamp;
            var stanceBefore = f.Current();
            f.Clear();
            await f.Owner.SendAsync(Actor, new MsgActionPacket { Type = MsgActionType.ChangeStance, DwParam = 50 }.ToFrameBytes());
            Assert.NotSame(stanceBefore, f.Current());
            Assert.Equal(50U, LegacyActionState.RequireCurrent(f.Current().Character));
            Assert.Equal(50U, (uint)f.Binding().Player.Action); Assert.Equal(stamp, f.Binding().Player.AttackStamp);
            Assert.DoesNotContain(f.Current().Character.Attributes.Keys, key => key.Equals("ActivePick", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(f.Owner.ActorFrames, bytes => MsgActionPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
                packet.Type == MsgActionType.ChangeStance && packet.DwParam == 50 && packet.ObjId == Actor);
            Assert.Contains(f.Owner.ActorFrames, bytes => MsgActionPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
                packet.Type == MsgActionType.ActionPick && packet.Strings.Contains("Interrupted"));
            f.AssertFiles(files); AssertAttributeScope(f, comparer, kind != 1); f.Clear();
            await AssertProficiencyHitAsync(f, existing: true);
            AssertAttributeScope(f, comparer, kind != 1);
            Assert.Equal("0", f.Current(Actor).Character.Attributes["action"]);
            Assert.DoesNotContain("Action", f.Current(Actor).Character.Attributes.Keys, StringComparer.Ordinal);
        });

    [Fact]
    public Task AmbiguousActivePickRefusesRealStanceBeforePublicationOrOutputs() => RunProficiencyAsync(true, async f =>
    {
        var initial = f.Current();
        var fields = new Dictionary<string, string>(initial.Character.Attributes, StringComparer.Ordinal)
            { ["Other"] = "upper", ["other"] = "lower", ["ActivePick"] = "yes", ["activepick"] = "yes" };
        f.Owner.Store.Set(f.Owner.Connection(Actor), initial with { Character = initial.Character with { Attributes = fields } });
        var before = CaptureProficiencyRefusal(f); int logStart = f.Owner.Log.ToString().Length;
        await f.Owner.SendAsync(Actor, new MsgActionPacket { Type = MsgActionType.ChangeStance, DwParam = 50 }.ToFrameBytes());
        AssertProficiencyRefusal(f, before);
        Assert.Contains("characterAttributeOwnedFieldAmbiguous", f.Owner.Log.ToString()[logStart..], StringComparison.Ordinal);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task AmbiguousActualPhysicalOwnedFieldRefusesBeforeFilesNativeStampOrOutput(bool target) =>
        RunProficiencyAsync(true, async f =>
        {
            uint uid = target ? Target : Actor;
            var initial = f.Current(uid);
            var fields = new Dictionary<string, string>(initial.Character.Attributes, StringComparer.Ordinal)
                { ["Other"] = "upper", ["other"] = "lower", ["ThundercloudSight"] = "0", ["thundercloudsight"] = "0" };
            f.Owner.Store.Set(f.Owner.Connection(uid), initial with { Character = initial.Character with { Attributes = fields } });
            Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(f.Binding(uid)));
            var before = CaptureProficiencyRefusal(f);
            int logStart = f.Owner.Log.ToString().Length;
            await f.AttackAsync();
            AssertProficiencyRefusal(f, before);
            Assert.Equal("upper", f.Current(uid).Character.Attributes["Other"]);
            Assert.Equal("lower", f.Current(uid).Character.Attributes["other"]);
            Assert.Contains("ordinaryBasicPhysicalPreparation:InvalidDataException", f.Owner.Log.ToString()[logStart..], StringComparison.Ordinal);
        });

    private static void AssertAttributeScope(AttackFixture f, IEqualityComparer<string> comparer, bool caseDistinct)
    {
        foreach (uint uid in new[] { Actor, Target })
        {
            var current = f.Current(uid);
            var proof = LegacyNativeAttributeMapProof.CaptureRaw(current.Character.Attributes);
            Assert.Same(comparer, proof.CapturedComparer); Assert.True(proof.IsCurrentPlain());
            Assert.Equal("upper", current.Character.Attributes["Other"]);
            if (caseDistinct) Assert.Equal("lower", current.Character.Attributes["other"]);
            Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(f.Binding(uid)));
        }
    }
}
