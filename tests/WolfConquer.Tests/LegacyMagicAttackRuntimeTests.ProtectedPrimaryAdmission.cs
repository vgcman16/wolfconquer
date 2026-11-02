using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)48)]
    public Task OwnedDirectMonsterCastRejectsProtectedPrimaryDespiteEligibleSecondary(byte sort) =>
        RunProtectedPrimaryAdmissionAsync(sort, async (f, monsters) =>
        {
            var before = f.Current(PlayerUid);
            var files = ProcFiles(f);
            await f.SendAsync(PlayerUid, ProtectedPrimaryQuery(monsters[1], sort));
            AssertProtectedPrimaryRefusal(f, before, files, monsters);
            Assert.Contains("CanAttackMonster.Verified rejected Reviver monster family", f.Log.ToString());
        });

    [Fact]
    public Task OwnedGenericDirectMonsterCastRejectsProtectedPrimaryBeforeAdmission() =>
        RunProtectedPrimaryAdmissionAsync(48, async (f, monsters) =>
        {
            var before = f.Current(PlayerUid);
            var files = ProcFiles(f);
            var context = f.Handler.CreateMonsterRuntimeContext(new(f.Connection(PlayerUid), before));
            var query = new MsgAttackPacket(CreateMagicQuery(monsters[1], legacyEncodedQuery: false,
                spellId: ThunderSpellId, x: 838, y: 584));
            var operations = Assert.IsType<WorldOperationBarrier>(typeof(WorldServerFrameHandler)
                .GetField("_worldOperations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var method = typeof(WorldPacketHandlers).GetMethod("HandleLegacyPlayerMonsterMagicAttackAsync",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            using var shared = await operations.EnterSharedAsync();
            object? pending;
            try { pending = method.Invoke(null, [context, query, CancellationToken.None, false]); }
            catch (TargetInvocationException error) when (error.InnerException is { } inner)
            { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
            Assert.True(await Assert.IsType<ValueTask<bool>>(pending));
            AssertProtectedPrimaryRefusal(f, before, files, monsters);
        });

    [Theory]
    [InlineData((byte)48)]
    [InlineData((byte)5)]
    public Task OwnedAcceptedDirectOrAreaCastExcludesProtectedRecipientAndChargesOnce(byte sort) =>
        RunProtectedPrimaryAdmissionAsync(sort, async (f, monsters) =>
        {
            // DirectAttack needs its eligible primary. Bomb may use the protected
            // role as a center while damaging the two eligible area recipients.
            var selected = sort == 5 ? monsters[1] : monsters[0];
            await f.SendAsync(PlayerUid, ProtectedPrimaryQuery(selected, sort));
            var current = f.Current(PlayerUid);
            Assert.Equal(493L, current.Character.MinMana);
            Assert.Equal(493U, current.NativePlayer!.Mana);
            Assert.Equal("95", current.Character.Attributes["Stamina"]);
            Assert.Equal((ushort)95, current.NativePlayer.Stamina);
            Assert.True(current.MagicCooldowns!.ContainsKey(sort == 5 ? LightningBombSpellId : ThunderSpellId));
            Assert.Equal(493L, f.Data.Loader.LoadByAccountId(PlayerUid)!.MinMana);
            // Stamina is session/native state; this scalar save persists mana only.
            Assert.Equal("100", f.Data.Loader.LoadByAccountId(PlayerUid)!.Attributes["Stamina"]);
            var costs = ParseMsgUpdates(f.ActorFrames).Where(update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 && update.Updates[0].Type is
                    LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana).ToArray();
            Assert.Equal(new[]
            {
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95),
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493),
            }, costs.Select(update => update.Updates[0]).ToArray());
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(f.ActorFrames[0]), out var stamina));
            Assert.Equal(costs[0].Updates.ToArray(), stamina.Updates.ToArray());
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(f.ActorFrames[1]), out var mana));
            Assert.Equal(costs[1].Updates.ToArray(), mana.Updates.ToArray());
            var animation = Assert.Single(ParseSpellAnimations(f.ActorFrames));
            Assert.Equal(new[] { monsters[0].Uid, monsters[2].Uid }, animation.Targets.Select(target => target.Uid).ToArray());
            Assert.All(animation.Targets, target => Assert.True(target.Damage > 0));
            var states = Assert.IsType<MonsterRuntimeStateStore>(f.Handler.CreateMonsterRuntimeContext(
                new(f.Connection(PlayerUid), current)).MonsterStates);
            foreach (var monster in monsters)
            {
                Assert.True(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visible));
                if (monster.Uid == monsters[1].Uid) Assert.Equal(monster.Hitpoints, visible.Hitpoints);
                else Assert.InRange(visible.Hitpoints, 1U, monster.Hitpoints - 1);
            }
        });
}
