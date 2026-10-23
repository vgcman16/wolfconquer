using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySuperGuildWarClaimSafetyTests
{
    private const uint NpcId = 25_015;
    private const uint RewardId = 2_100_065;
    private const ushort RewardDurability = 610;
    private sealed record Recipient(ConquerTcpConnection Connection, List<byte[]> Sent, long Account, uint Uid);
    private sealed record Attempt(WorldSession Before, WorldSession After, byte[][] BeforeFiles,
        byte[][] AfterFiles, byte[][] OwnerFrames, byte[][][] OtherFrames, CharacterSnapshot Reloaded,
        PlayerItemSnapshot SavedItems, byte[] BeforeItems, KeyValuePair<string, string>[] BeforeAttributes,
        Exception? Failure, string Log);

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-superguild-claim-" + Guid.NewGuid().ToString("N"));
        private readonly WorldSessionStore sessions = new();
        private readonly List<Recipient> recipients = [];
        private readonly StringWriter log = new();
        private readonly PacketHandlerRegistry<WorldPacketHandlerContext> registry =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        private WorldDataCatalog catalog = Catalog();

        internal Fixture(ulong rank, int inventoryCount = 0)
        {
            ValidateOwnedRoot();
            Directory.CreateDirectory(Users);
            Directory.CreateDirectory(Items);
            WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
            for (var index = 0; index < 3; index++)
            {
                long account = 100 + index;
                uint uid = checked(1_293_800U + (uint)index);
                var rows = Enumerable.Range(0, index == 0 ? inventoryCount : 0).Select(row =>
                    new PlayerItemSnapshotItem
                    {
                        Uid = checked(50_000U + (uint)index * 1000 + (uint)row), ItemId = 723700,
                        Durability = 10, MaximumDurability = 10, Position = 0, StackSize = 1,
                    }).ToArray();
                var sent = new List<byte[]>();
                var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, token) =>
                {
                    token.ThrowIfCancellationRequested(); sent.Add(frame.ToArray()); return ValueTask.CompletedTask;
                });
                var recipient = new Recipient(connection, sent, account, uid);
                recipients.Add(recipient);
                var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["DynamicID"] = "0", ["GuildID"] = "77", ["GuildName"] = "WinnerGuild",
                    ["GuildRank"] = rank.ToString(CultureInfo.InvariantCulture),
                    ["LegacySuperGuildWarProcess"] = "Dead", ["Sentinel"] = "keep",
                };
                var character = new CharacterSnapshot(uid, "SuperGuildHero" + index, 1003, 0, 0, 7051,
                    7051, 0, 0, 140, 0, 1002, 430, 378, 1002, 430, 378, 0, 0, 0, 0, 0,
                    1, 0, 200, 100, 0, 0, 0, 0, account + ".ini", attributes, new Dictionary<string, string>());
                var session = new WorldSession(account, "superguild-owned-" + index, character,
                    new PlayerItemSnapshot(rows, [], [], [], []), PlayerSpellSnapshot.Empty,
                    PlayerProficiencySnapshot.Empty, UsersRoot: Users);
                WriteCharacter(recipient, session.Character);
                new PlayerItemSnapshotLoader(Items).SaveByCharacterUid(uid, session.PlayerItems);
                sessions.Set(connection, session with
                {
                    Character = ReadCharacter(recipient),
                    PlayerItems = new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(uid),
                });
            }
        }

        internal void IntroduceFault(string kind)
        {
            var recipient = recipients[0];
            var current = Current(recipient);
            var character = current.Character;
            switch (kind)
            {
                case "cp-overflow": character = character with { ConquerPoints = long.MaxValue }; break;
                case "money-overflow": character = character with { Money = long.MaxValue }; break;
                case "name": character = character with { Name = new string('N', 70_000) }; break;
                case "guild-name":
                    character = character with { Attributes = CopyAttributes(character, "GuildName", new string('G', 70_000)) };
                    break;
                case "attributes":
                    var bad = CopyAttributes(character, "Unrelated", "A");
                    bad.Add("unrelated", "B");
                    bad[LegacyStatusFlagDeathRegistration.Key(152)] = "true";
                    character = character with { Attributes = bad };
                    break;
                case "durability": catalog = Catalog(65_536); break;
                default: throw new InvalidOperationException("Unknown owned preparation fault.");
            }
            sessions.Set(recipient.Connection, current with { Character = character });
            WriteCharacter(recipient, character);
        }

        internal void RepairFault(string kind)
        {
            var recipient = recipients[0];
            var current = Current(recipient);
            var character = current.Character;
            switch (kind)
            {
                case "cp-overflow": character = character with { ConquerPoints = 100 }; break;
                case "money-overflow": character = character with { Money = 200 }; break;
                case "name": character = character with { Name = "SuperGuildHero0" }; break;
                case "guild-name": character = character with { Attributes = CopyAttributes(character, "GuildName", "WinnerGuild") }; break;
                case "attributes":
                    var repaired = new Dictionary<string, string>(character.Attributes, StringComparer.Ordinal);
                    repaired.Remove("unrelated");
                    character = character with { Attributes = repaired };
                    break;
                case "durability": catalog = Catalog(); break;
                default: throw new InvalidOperationException("Unknown owned preparation fault.");
            }
            sessions.Set(recipient.Connection, current with { Character = character });
            WriteCharacter(recipient, character);
        }

        internal async Task<Attempt> ClickAsync(int index)
        {
            foreach (var recipient in recipients) recipient.Sent.Clear();
            log.GetStringBuilder().Clear();
            var target = recipients[index];
            var before = Current(target);
            var beforeFiles = ReadFiles();
            var beforeItems = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
            var beforeAttributes = before.Character.Attributes.ToArray();
            var context = new WorldPacketHandlerContext(target.Connection, log, before,
                WorldData: catalog, Sessions: sessions, PlayerItemsRoot: Items);
            Assert.Contains(WorldPacketOpcodes.LegacyNpcReply, registry.Opcodes);
            var packet = new LegacyNpcDialogRequestPacket(NpcId, 0, 7,
                (byte)LegacyNpcReplyInteractType.Option, 0, "");
            Exception? failure = null;
            try { await registry.HandleAsync(context, Frame(packet.ToFrameBytes())); }
            catch (Exception error) { failure = error; }
            return new(before, Current(target), beforeFiles, ReadFiles(), target.Sent.ToArray(),
                recipients.Where(recipient => !ReferenceEquals(recipient, target))
                    .Select(recipient => recipient.Sent.ToArray()).ToArray(),
                ReadCharacter(target),
                new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(target.Uid),
                beforeItems, beforeAttributes,
                failure, log.ToString());
        }

        private WorldSession Current(Recipient recipient)
        {
            Assert.True(sessions.TryGet(recipient.Connection, out var current));
            return Assert.IsType<WorldSession>(current);
        }

        private static Dictionary<string, string> CopyAttributes(CharacterSnapshot character, string key, string value)
        {
            var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.Ordinal) { [key] = value };
            return attributes;
        }

        private byte[][] ReadFiles() => recipients.SelectMany(recipient => new[]
            { CharacterPath(recipient), ItemPath(recipient) }).Select(File.ReadAllBytes).ToArray();
        private string Users => Path.Combine(root, "Users");
        private string Items => Path.Combine(root, "PlayersItems");
        private string CharacterPath(Recipient recipient) => Path.Combine(Users, recipient.Account + ".ini");
        private string ItemPath(Recipient recipient) => Path.Combine(Items, recipient.Uid + ".bin");

        // The active business fixture intentionally keeps Account100/UID1293800.
        // Use the genuine owned-file parser, not the separate login loader's
        // AccountId==UID policy; no new distinct-ID login authority is asserted.
        private CharacterSnapshot ReadCharacter(Recipient recipient) => CharacterSnapshotParser.ParseText(
            CharacterSnapshotFileEncoding.ReadAllText(CharacterPath(recipient), out _),
            Path.GetFileName(CharacterPath(recipient)));

        private void WriteCharacter(Recipient recipient, CharacterSnapshot character)
        {
            ValidateOwnedRoot();
            // Canonical owned baseline; repair changes only the deliberately bad
            // precondition's session/file image, never entitlement state.
            var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.Ordinal)
            {
                ["UID"] = Number(character.Uid), ["Name"] = character.Name,
                ["Body"] = Number(character.Body), ["Face"] = Number(character.Face), ["Haire"] = Number(character.Hair),
                ["Class"] = Number(character.Class), ["FirstClass"] = Number(character.FirstClass),
                ["SecoundeClass"] = Number(character.SecondClass), ["Reborn"] = Number(character.Reborn),
                ["Level"] = Number(character.Level), ["Experience"] = Number(character.Experience),
                ["Map"] = Number(character.MapId), ["X"] = Number(character.X), ["Y"] = Number(character.Y),
                ["PMap"] = Number(character.PreviousMapId), ["PMapX"] = Number(character.PreviousMapX),
                ["PMapY"] = Number(character.PreviousMapY), ["Strength"] = Number(character.Strength),
                ["Agility"] = Number(character.Agility), ["Vitaliti"] = Number(character.Vitality),
                ["Spirit"] = Number(character.Spirit), ["Atributes"] = Number(character.AttributePoints),
                ["MinHitPoints"] = Number(character.MinHitPoints), ["MinMana"] = Number(character.MinMana),
                ["Money"] = Number(character.Money), ["ConquerPoints"] = Number(character.ConquerPoints),
                ["BoundConquerPoints"] = Number(character.BoundConquerPoints), ["BankCps"] = Number(character.BankConquerPoints),
                ["PkPoints"] = Number(character.PkPoints), ["Online"] = Number(character.Online),
            };
            File.WriteAllLines(CharacterPath(recipient), new[] { "[Character]" }
                .Concat(attributes.Select(pair => pair.Key + "=" + pair.Value)));
        }

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

        private void ValidateOwnedRoot()
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            Assert.Equal(temp, Path.GetDirectoryName(full));
            const string prefix = "wolf-superguild-claim-";
            var name = Path.GetFileName(full);
            Assert.StartsWith(prefix, name);
            Assert.True(Guid.TryParseExact(name[prefix.Length..], "N", out _));
            foreach (var path in new[] { Users, Items }.Concat(recipients.SelectMany(recipient =>
                new[] { CharacterPath(recipient), ItemPath(recipient) })))
                for (string? current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                    if (File.Exists(current) || Directory.Exists(current))
                        Assert.Equal((FileAttributes)0, File.GetAttributes(current) & FileAttributes.ReparsePoint);
        }

        public void Dispose()
        {
            WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
            log.Dispose();
            ValidateOwnedRoot();
            foreach (var recipient in recipients)
                foreach (var path in new[] { CharacterPath(recipient), ItemPath(recipient) })
                {
                    Assert.Equal((FileAttributes)0, File.GetAttributes(path) & FileAttributes.ReparsePoint);
                    File.Delete(path);
                }
            Directory.Delete(Users, false); Directory.Delete(Items, false); Directory.Delete(root, false);
        }
    }

    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
