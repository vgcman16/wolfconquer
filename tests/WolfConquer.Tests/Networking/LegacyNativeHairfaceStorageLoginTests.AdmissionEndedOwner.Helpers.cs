using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    private static void AssertEndedOrdinaryWriterRefused(string role, LoginInput input,
        WorldServerFrameHandler handler, ConquerTcpConnection connection, WorldSession current)
    {
        if (role == "Character")
            Assert.False(CharacterSnapshotScalarPersistence.TryPersist(input.Users,
                current.Character with { Money = current.Character.Money - 1 }, out _, out _));
        else if (role == "Store")
            Assert.False(AdmissionOwner<WorldSessionStore>(handler, "_sessions").TryCompareAndSet(connection,
                current, current with { Character = current.Character with { Money = current.Character.Money - 1 } }));
        else Assert.Throws<IOException>(() => WriteEndedOrdinarySnapshot(role, input));
    }

    private static void WriteEndedOrdinarySnapshot(string role, LoginInput input)
    {
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        if (role == "Spells") new PlayerSpellSnapshotLoader(roots.Spells!).SaveByCharacterUid(Uid,
            new([new(65000, 1, 23, 0, 0, 0)]));
        else if (role == "Proficiencies") new PlayerProficiencySnapshotLoader(roots.Proficiencies!).SaveByCharacterUid(Uid,
            new([new(420, 1, 0, 23, 100)]));
        else throw new ArgumentOutOfRangeException(nameof(role));
    }

    private static void AssertCurrentOrdinaryWriterUsable(string role, LoginInput input,
        WorldServerFrameHandler handler, ConquerTcpConnection connection, WorldSession current)
    {
        if (role == "Character")
        {
            Assert.True(CharacterSnapshotScalarPersistence.TryPersist(input.Users,
                current.Character with { Money = current.Character.Money - 1 }, out _, out var reason), reason);
            Assert.Equal(current.Character.Money - 1, new CharacterSnapshotLoader(input.Users)
                .TryLoadByAccountIdForAdmission(Uid).Snapshot!.Money);
        }
        else if (role == "Store")
        {
            var replacement = current with { }; // Same genuine business/native state, a real new holder value.
            Assert.True(AdmissionOwner<WorldSessionStore>(handler, "_sessions").TryCompareAndSet(connection, current, replacement));
            Assert.Same(replacement, handler.ReadCurrentSessionForNativeOwnerObservation(connection));
        }
        else
        {
            WriteEndedOrdinarySnapshot(role, input);
            var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
            if (role == "Spells")
                Assert.Equal(new PlayerSpellSnapshotEntry(65000, 1, 23, 0, 0, 0),
                    Assert.Single(new PlayerSpellSnapshotLoader(roots.Spells!).LoadByCharacterUid(Uid).Spells));
            else
                Assert.Equal(new PlayerProficiencySnapshotEntry(420, 1, 0, 23, 100),
                    Assert.Single(new PlayerProficiencySnapshotLoader(roots.Proficiencies!).LoadByCharacterUid(Uid).Proficiencies));
        }
    }

    // Preserve primary assertion failures if controlled owner restoration also
    // fails. Restoration is an explicit fixture action, never a production hook.
    private static void RethrowEndedOwnerFailures(List<Exception> errors)
    {
        if (errors.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Ended owner assertions and controlled fixture restoration failed.", errors);
    }
}
