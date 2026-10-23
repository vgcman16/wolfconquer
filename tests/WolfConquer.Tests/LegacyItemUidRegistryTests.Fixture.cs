using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemUidRegistryTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        internal readonly string Root;
        internal Fixture()
        {
            Root = Path.Combine(parent, "wolf-item-uid-registry-" + Guid.NewGuid().ToString("N"));
            ValidateRoot();
            Ordinary(parent);
            Directory.CreateDirectory(Users);
            Directory.CreateDirectory(Items);
            Directory.CreateDirectory(Mail);
        }
        internal string Users => Path.Combine(Root, "Users");
        internal string Items => Path.Combine(Root, "PlayersItems");
        internal string Mail => Path.Combine(Root, "PlayersMailList");
        internal string Redeem => Path.Combine(Root, "RedeemContainer.bin");
        internal string Claim => Path.Combine(Root, "ClaimContainer.bin");
        internal string Registry => Path.Combine(Items, ".managed-item-uids");
        internal string State => Path.Combine(Registry, "state.json");
        internal string Pending => Path.Combine(Registry, "pending.json");
        internal string SnapshotJournal => Path.Combine(Users, ".managed-trade-journal");
        internal LegacyItemUidRegistryOptions Options(uint? floor = 1000) =>
            new(Users, Items, Mail, Redeem, Claim, floor, "owned synthetic historical high-water evidence");

        internal LegacyItemUidRegistry Bootstrap(uint floor = 1000)
        {
            Assert.True(LegacyItemUidRegistry.TryBootstrap(Options(floor), out var owner, out var reason), reason);
            return Assert.IsType<LegacyItemUidRegistry>(owner);
        }

        internal LegacyItemUidRegistry Open(uint floor = 1000)
        {
            Assert.True(LegacyItemUidRegistry.TryOpen(Options(floor), out var owner, out var reason), reason);
            return Assert.IsType<LegacyItemUidRegistry>(owner);
        }

        internal static uint[] Reserve(LegacyItemUidRegistry owner, int count)
        {
            Assert.True(owner.TryReserve(count, out var range, out var reason), reason);
            Assert.NotNull(range);
            Assert.True(range.TryTake(out var ids));
            Assert.False(range.TryTake(out var replay));
            Assert.Empty(replay);
            return ids;
        }

        internal string SeedItems(uint account, params PlayerItemSnapshotItem[] rows)
        {
            var path = Path.Combine(Items, account + ".bin");
            File.WriteAllBytes(path, PlayerItemSnapshotLoader.ToLegacyBytes(new(rows, [], [], [], [])));
            return path;
        }

        internal void SeedMail(uint itemUid, bool expired = false)
        {
            var mailbox = new LegacyMailboxStore(Root);
            mailbox.Add(11, new LegacyMailboxEntry
            {
                Id = 1, Title = "owned", ItemId = itemUid, Timer = DateTime.Now.AddDays(1).Ticks,
            });
            mailbox.Save(11);
            if (!expired) return;
            var path = mailbox.GetMailboxPath(11);
            var bytes = File.ReadAllBytes(path);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(bytes.Length - sizeof(long)), 1);
            File.WriteAllBytes(path, bytes);
        }

        internal void SeedDetained(uint itemUid, bool alias = false, bool rewardOnly = false)
        {
            Assert.Equal(322, Unsafe.SizeOf<LegacyNativeMsgDetainedItem>());
            var row = new LegacyNativeMsgDetainedItem
            {
                UID = 1, ItemUID = itemUid, ItemID = 723700, OwnerUID = 11, GainerUID = 12,
                Action = rewardOnly ? LegacyNativeMsgDetainedItem.ContainerType.RewardCps :
                    LegacyNativeMsgDetainedItem.ContainerType.ClaimPage,
            };
            var bytes = new byte[4 + Unsafe.SizeOf<LegacyNativeMsgDetainedItem>()];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
            MemoryMarshal.Write(bytes.AsSpan(4), in row);
            File.WriteAllBytes(Redeem, bytes);
            if (alias) File.WriteAllBytes(Claim, bytes);
        }

        internal static PlayerItemSnapshotItem Item(uint uid,
            IReadOnlyList<PlayerItemSnapshotItem>? deposits = null, ushort position = 0) => new()
        {
            Uid = uid, ItemId = 723700, StackSize = 1, Position = position, DepositedItems = deposits ?? [],
        };

        private void ValidateRoot()
        {
            Assert.Equal(parent, Path.GetDirectoryName(Path.GetFullPath(Root)));
            const string prefix = "wolf-item-uid-registry-";
            var name = Path.GetFileName(Root);
            Assert.StartsWith(prefix, name);
            Assert.True(Guid.TryParseExact(name[prefix.Length..], "N", out _));
        }

        private static void Ordinary(string path)
        {
            if (File.Exists(path) || Directory.Exists(path))
                Assert.Equal((FileAttributes)0, File.GetAttributes(path) & FileAttributes.ReparsePoint);
        }

        public void Dispose()
        {
            ValidateRoot();
            Ordinary(parent);
            Ordinary(Root);
            foreach (var directory in new[] { Registry, SnapshotJournal, Users, Items, Mail, Root })
            {
                if (!Directory.Exists(directory)) continue;
                Ordinary(directory);
                foreach (var file in Directory.GetFiles(directory))
                {
                    Assert.Equal(directory, Path.GetDirectoryName(Path.GetFullPath(file)));
                    Assert.StartsWith(Root + Path.DirectorySeparatorChar, Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase);
                    Ordinary(file);
                    File.Delete(file);
                }
            }
            // Unknown directories are never recursively removed.
            foreach (var directory in new[] { Registry, SnapshotJournal, Users, Items, Mail })
                if (Directory.Exists(directory)) Directory.Delete(directory, false);
            Directory.Delete(Root, false);
        }
    }
}
