using System.Buffers;
using System.Globalization;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyGmRuntimeTests
{
    private static async Task WithGmAsync(Func<OperatorGearMailFixture, Task> test,
        string[]? capabilities = null, bool enabled = true, uint? account = null, uint? character = null,
        bool completeCatalog = true)
    {
        OperatorGearMailFixture? owner = null;
        Exception? primary = null;
        try
        {
            owner = new OperatorGearMailFixture();
            owner.Initialize(completeCatalog ? KitCatalog() : null,
                enabled ? capabilities ?? ["inspect.self", "mail.testkit.self"] : null, account, character);
            await owner.LoginAsync();
            owner.Sent.Clear();
            await test(owner);
        }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (owner is not null)
        {
            owner.RetainEvidence = primary is not null;
            try { await owner.DisposeAsync(); } catch (Exception error) { cleanup = error; }
        }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

    private static ItemType[] KitCatalog()
    {
        var domain = Guid.Parse("225acc35-dbb0-40f2-a89c-d39a99b05e25");
        return new[] { "basic-trojan", "socket-materials", "consumables" }
            .SelectMany(kit => LegacyOperatorTestKits.Create(domain, domain, 1, 1, kit).Items)
            .Select(row => row.ItemId).Distinct().Where(id => id != 1_000_000 && id != OperatorGearMailFixture.GearId)
            .Select(id =>
            {
                var fields = Enumerable.Repeat("0", 66).ToArray();
                fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "SyntheticGm" + fields[0];
                fields[ItemType.DurabilityFieldIndex] = "321"; fields[ItemType.StackSizeFieldIndex] = "1";
                return new ItemType(checked((int)id), fields[1], fields);
            }).ToArray();
    }

    private static async Task CommandAsync(OperatorGearMailFixture owner, string text,
        uint channel = LegacyMsgMessagePacket.TalkChatType)
    {
        var bytes = new LegacyMsgMessagePacket(LegacyMsgMessagePacket.WhiteColor, channel,
            uint.MaxValue, uint.MaxValue, 0, 0, 0, 1, 0, 0,
            ["Forged[GM]", "ALLUSERS", string.Empty, text, string.Empty, string.Empty, string.Empty]).ToFrameBytes();
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        await owner.Handler.HandleFrameAsync(owner.Connection, frame).AsTask().WaitAsync(TimeSpan.FromSeconds(20));
    }

    private static string[] Messages(OperatorGearMailFixture owner) => owner.SentSnapshot()
        .Select(bytes =>
        {
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
            return LegacyMsgMessagePacket.TryParse(frame, out var packet) ? packet.Content[3] : null;
        }).Where(text => text is not null).Cast<string>().ToArray();

    private static void AssertRefusal(OperatorGearMailFixture owner)
    {
        var text = Assert.Single(Messages(owner));
        Assert.StartsWith("GM command refused", text, StringComparison.Ordinal);
        Assert.DoesNotContain(owner.Fixture.Root, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Forged", text, StringComparison.Ordinal);
    }
}
