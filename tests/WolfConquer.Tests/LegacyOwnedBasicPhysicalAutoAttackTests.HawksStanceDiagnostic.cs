using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Fact]
    public Task GenuineHawksProficiencySetupKeepsActorStanceOwnerCurrent()
        => RunHawksProficiencyAsync(async f =>
        {
            var actor = f.Binding(Actor); var target = f.Binding(Target);
            Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(actor));
            Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(target));
            await f.MintHawksAsync();
            Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(actor));
            Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(target));
            Assert.False(actor.IsRetired); Assert.False(target.IsRetired);
            Assert.False(f.Owner.Recovery.IsBlocked);
            var before = f.Files();
            await f.Owner.SendAsync(Actor, new MsgActionPacket { Type = MsgActionType.ChangeStance, DwParam = 250 }.ToFrameBytes());
            var current = f.Current(Actor);
            uint managed = LegacyActionState.RequireCurrent(current.Character);
            string detail = $"After genuine target Hawks and real actor stance: managed={managed}; native={(uint)actor.Player.Action}; " +
                $"actorCurrent={f.Owner.Store.IsBoundSpellOwnerCurrent(actor)}; targetCurrent={f.Owner.Store.IsBoundSpellOwnerCurrent(target)}; " +
                $"actorRetired={actor.IsRetired}; targetRetired={target.IsRetired}; recovery={f.Owner.Recovery.IsBlocked}; " +
                $"actorStamp={actor.Player.AttackStamp:O}; nativeRepeat={actor.Client.OnAutoAttack}; " +
                $"actorFrames={f.Owner.Frames(Actor).Count}; targetFrames={f.Owner.Frames(Target).Count}\n{f.Owner.Log}";
            Assert.True(managed == 250U, detail);
            Assert.Equal(250U, (uint)actor.Player.Action);
            Assert.False(actor.Client.OnAutoAttack);
            f.AssertFiles(before);
        });
}
