using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    private sealed class PrestigeClientGuildImage
    {
        private readonly LegacyNativeGuildRuntime? guild;
        private readonly string? name;
        private readonly ConcurrentDictionary<uint, LegacyNativeGuildRuntime.Member>? members;
        private readonly KeyValuePair<uint, LegacyNativeGuildRuntime.Member>[] entries;
        private readonly (LegacyNativeGuildRuntime.Member Member, uint Uid, string? Name, uint Points)[] values;

        internal PrestigeClientGuildImage(LegacyNativeGameClientRuntime client)
        {
            guild = client.Player.MyGuild;
            name = guild?.GuildName;
            members = guild?.Members;
            entries = members?.ToArray() ?? [];
            values = entries.Select(pair => (pair.Value, pair.Value.UID, (string?)pair.Value.Name,
                pair.Value.PrestigePoints)).ToArray();
        }

        internal void RequireUnchanged(LegacyNativeGameClientRuntime client)
        {
            Assert.Same(guild, client.Player.MyGuild);
            Assert.Equal(name, client.Player.MyGuild?.GuildName);
            Assert.Same(members, client.Player.MyGuild?.Members);
            if (members is null) return;
            Assert.Equal(entries.Select(pair => pair.Key).Order(), members.Keys.Order());
            foreach (var entry in entries)
            {
                Assert.True(members.TryGetValue(entry.Key, out var member));
                Assert.Same(entry.Value, member);
            }
            foreach (var value in values)
            {
                Assert.Equal(value.Uid, value.Member.UID);
                Assert.Equal(value.Name, value.Member.Name);
                Assert.Equal(value.Points, value.Member.PrestigePoints);
            }
        }
    }
}
