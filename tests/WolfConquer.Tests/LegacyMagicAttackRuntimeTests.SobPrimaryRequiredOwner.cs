using System.Reflection;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public Task SavedProcessAttributesCannotReplaceAnAdmittedGuildWarPermissionOwner() =>
        RunOwnedSobPrimaryAsync(1, "guild-war-attribute", async (fixture, sobs, states) =>
        {
            var before = fixture.Current(PlayerUid);
            Assert.NotNull(before.NativeGameClient!.OrdinarySpellBinding);
            Assert.Equal("Alive", before.Character.Attributes["LegacyNewGuildWarProcess"]);
            var files = ProcFiles(fixture);
            var life = states.GetVisible(sobs[1]).Life;
            var context = fixture.Handler.CreateMonsterRuntimeContext(new(fixture.Connection(PlayerUid), before))
                with { GuildWarStates = null };
            var operations = Assert.IsType<WorldOperationBarrier>(typeof(WorldServerFrameHandler)
                .GetField("_worldOperations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Handler));
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            using var shared = await operations.EnterSharedAsync();
            await registry.HandleAsync(context, ReadFrame(OwnedSobQuery(sobs[1], 1)));
            Assert.Same(before, fixture.Current(PlayerUid));
            Assert.Equal(500U, before.NativePlayer!.Mana);
            Assert.Equal((ushort)100, before.NativePlayer.Stamina);
            Assert.Null(before.MagicCooldowns);
            Assert.Empty(fixture.ActorFrames);
            Assert.Equal(life, states.GetVisible(sobs[1]).Life);
            ProcFilesUnchanged(fixture, files);
            Assert.Contains("guildWarPermissionOwnerRequired", fixture.Log.ToString());
        });
}
