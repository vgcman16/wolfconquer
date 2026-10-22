using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaTests
{
    // Getter prestate only: not a replacement for the unported full Class setter.
    private static void Class(LegacyNativeGameClientRuntime user, uint value) =>
        typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(user.Player, value);

    private static LegacyNativeGameClientRuntime Client(uint job = 5000, bool alternate = false)
    {
        var user = new LegacyNativeGameClientRuntime();
        Class(user, job);
        user.Equipment = new(user) { Alternante = alternate };
        return user;
    }

    private static LegacyNativeNinja.Item Put(LegacyNativeNinja ninja, uint key, uint position, uint id = 100)
    {
        var item = new LegacyNativeNinja.Item { ItemID = id, Position = position, Level = key };
        Assert.True(ninja.Items.TryAdd(key, item));
        return item;
    }

    private sealed class Tables : IDisposable
    {
        private readonly Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? priorTypes = LegacyNativeNinjaFile.gouyu_type;
        private readonly Dictionary<uint, LegacyNativeNinjaFile.Item>? priorImmortals = LegacyNativeNinjaFile.gouyu_immortals;

        internal Tables()
        {
            LegacyNativeNinjaFile.gouyu_type = new();
            LegacyNativeNinjaFile.gouyu_immortals = new();
        }

        public void Dispose()
        {
            LegacyNativeNinjaFile.gouyu_type = priorTypes!;
            LegacyNativeNinjaFile.gouyu_immortals = priorImmortals!;
        }
    }
}
