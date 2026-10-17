using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public void VisibleDx8D13HandlerCreatesMonsterRuntimeContextWithD13Profile()
    {
        const long accountId = 1_293_600;
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId, uid: accountId);
        var loader = new CharacterSnapshotLoader(_usersRoot);
        var character = loader.LoadByAccountId(accountId);
        Assert.NotNull(character);
        var connection = CreateConnection();
        var active = new WorldActiveSession(
            connection,
            new WorldSession(
                accountId,
                "monster-runtime-profile-test",
                character,
                PlayerItemSnapshot.Empty,
                PlayerSpellSnapshot.Empty,
                PlayerProficiencySnapshot.Empty));
        var handler = new WorldServerFrameHandler(
            loader,
            ConquerProtocolProfile.VisibleDx8D13);

        var context = handler.CreateMonsterRuntimeContext(active);

        Assert.Same(connection, context.Connection);
        Assert.Same(active.Session, context.Session);
        Assert.Equal(ConquerProtocolProfile.VisibleDx8D13, context.ProtocolProfile);
        Assert.NotEqual(ConquerProtocolProfile.RootD11_7899, context.ProtocolProfile);
    }
}
