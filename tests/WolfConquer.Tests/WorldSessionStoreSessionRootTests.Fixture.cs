using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldSessionStoreSessionRootTests
{
    public static IEnumerable<object[]> InvalidProvidedRoots()
    {
        yield return [string.Empty];
        yield return [" \t\r\n"];
        yield return ["invalid\0root"];
        var platformInvalid = Path.GetInvalidPathChars().FirstOrDefault(character => character != '\0');
        if (platformInvalid != default) yield return ["invalid" + platformInvalid + "root"];
    }

    private sealed record RootOwners(LegacyOwnedAdmissionRuntimeFixture Disk,
        LegacyOwnedAdmissionRuntime Runtime, LegacyMailboxPackageProducer Producer)
    {
        internal WorldSession Session(string? usersRoot = null)
        {
            var uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
            var loader = Disk.Loader;
            var character = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(uid));
            return new(uid, character.Name, character, loader.LoadPlayerItemsByCharacterUid(uid),
                loader.LoadPlayerSpellsByCharacterUid(uid), loader.LoadPlayerProficienciesByCharacterUid(uid),
                CombatGear: loader.LoadCombatGearByCharacterUid(uid), UsersRoot: usersRoot);
        }
    }

    private static void WithRegisteredOwners(Action<RootOwners> body)
    {
        var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        LegacyOwnedAdmissionRuntime? runtime = null;
        var errors = new List<Exception>();
        try
        {
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(fixture.Loader, fixture.Options(), out runtime, out var reason), reason);
            var actual = Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime);
            Assert.True(LegacyMailboxPackageProducer.TryCreate(actual.Ordinary.Registry, fixture.ExpectedDomain,
                actual.Recovery, fixture.Users, fixture.Items, fixture.Mailbox, fixture.CombatGear,
                out var producer, out reason), reason);
            body(new(fixture, actual, Assert.IsType<LegacyMailboxPackageProducer>(producer)));
        }
        catch (Exception error) { errors.Add(error); }

        var closed = runtime is null;
        try { runtime?.Dispose(); closed = true; }
        catch (Exception error) { errors.Add(error); }
        if (closed)
        {
            try { fixture.Dispose(); }
            catch (Exception error) { errors.Add(error); }
        }
        else errors.Add(new InvalidOperationException($"Session-root fixture retained because actual owner closure is unproven; ownedRoot={fixture.Root}."));
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Session-root assertion and/or owned cleanup failed.", errors);
    }

    private static void AssertInvalidRoot(Action operation)
    {
        var error = Assert.Throws<InvalidOperationException>(operation);
        Assert.Equal("sessionStorageRootInvalid", error.Message);
    }

    private static void AssertCurrent(WorldSessionStore store, ConquerTcpConnection connection, WorldSession expected)
    {
        Assert.True(store.TryGet(connection, out var current));
        Assert.Same(expected, current);
    }
}
