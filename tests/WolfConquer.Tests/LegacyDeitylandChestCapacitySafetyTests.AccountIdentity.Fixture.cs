using System.Globalization;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    private sealed partial class Fixture
    {
        private readonly List<string> accountProofFiles = [];
        private byte[] unrelatedUidNamedBefore = [];

        private string UidNamedAccountProofPath => Path.Combine(
            Users, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini");

        internal void SeedUnrelatedUidNamedCharacterFile()
        {
            Assert.Equal(301, Session.AccountId);
            Assert.Equal(CharacterUid, Session.Character.Uid);
            Assert.NotEqual(CharacterPath, UidNamedAccountProofPath);
            var unrelated = Encoding.UTF8.GetString(characterBefore)
                .Replace("UID=1293600", "UID=1293601", StringComparison.Ordinal)
                .Replace("Name=ChestTester", "Name=UnrelatedOwner", StringComparison.Ordinal)
                .Replace(Environment.NewLine + "ConquerPoints=55" + Environment.NewLine,
                    Environment.NewLine + "ConquerPoints=777" + Environment.NewLine, StringComparison.Ordinal);
            File.WriteAllText(UidNamedAccountProofPath, unrelated, new UTF8Encoding(false, true));
            unrelatedUidNamedBefore = File.ReadAllBytes(UidNamedAccountProofPath);
            accountProofFiles.Add(UidNamedAccountProofPath);
        }

        internal void AssertDistinctAccountCpSaved()
        {
            Assert.Equal(301, Current.AccountId);
            Assert.Equal(CharacterUid, Current.Character.Uid);
            Assert.Equal(200_055, Current.Character.ConquerPoints);
            AssertCharacterCpSaved();
            var parsed = CharacterSnapshotParser.ParseText(
                File.ReadAllText(CharacterPath), CharacterPath);
            Assert.Equal(CharacterUid, parsed.Uid);
            Assert.Equal("ChestTester", parsed.Name);
            Assert.Equal(200_055, parsed.ConquerPoints);
            Assert.Equal(unrelatedUidNamedBefore, File.ReadAllBytes(UidNamedAccountProofPath));
            var unrelated = CharacterSnapshotParser.ParseText(
                File.ReadAllText(UidNamedAccountProofPath), UidNamedAccountProofPath);
            Assert.Equal(1_293_601, unrelated.Uid);
            Assert.Equal(777, unrelated.ConquerPoints);
            Assert.DoesNotContain("PARITY-GAP", Log.ToString());
            AssertOnlyExpectedFiles();
        }

        internal void PrepareAccountPersistenceFault(string fault)
        {
            if (fault == "missing")
                File.Delete(CharacterPath); // Exact owned account leaf, never a fake writer/allocator fault.
            else if (fault == "wrongUid")
                File.WriteAllText(CharacterPath, Encoding.UTF8.GetString(characterBefore)
                    .Replace("UID=1293600", "UID=301", StringComparison.Ordinal), new UTF8Encoding(false, true));
            else if (fault == "uidMissing")
                File.WriteAllText(CharacterPath, Encoding.UTF8.GetString(characterBefore)
                    .Replace("UID=1293600" + Environment.NewLine, "", StringComparison.Ordinal), new UTF8Encoding(false, true));
            else
                Assert.Equal("usersRootMissing", fault);
        }

        internal void AssertAccountPersistenceOwnerRejectsWithoutEffects(string fault, string expectedReason)
        {
            var before = Current;
            var physicalBefore = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
            var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
            var signature = new[]
            {
                typeof(string), typeof(long), typeof(long),
                typeof(IReadOnlyDictionary<string, string>),
                typeof(string).MakeByRefType(), typeof(string).MakeByRefType(),
            };
            var method = typeof(WorldPacketHandlers).GetMethod(
                "TryPersistLegacyCharacterAttributes", BindingFlags.Static | BindingFlags.NonPublic,
                binder: null, types: signature, modifiers: null);
            Assert.NotNull(method);
            object?[] arguments =
            [
                fault == "usersRootMissing" ? null : Users,
                Session.AccountId, Session.Character.Uid,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ConquerPoints"] = "200055" },
                null, null,
            ];
            // This is the genuine private persistence owner, NOT whole chest admission.
            Assert.False(Assert.IsType<bool>(method.Invoke(null, arguments)));
            Assert.Equal(expectedReason, Assert.IsType<string>(arguments[5]));
            Assert.Equal(fault == "usersRootMissing" ? "" : CharacterPath, Assert.IsType<string>(arguments[4]));
            Assert.Same(before, Current);
            Assert.Equal(physicalBefore, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Empty(Sent);
            Assert.Equal(files.Keys.Order(StringComparer.Ordinal),
                Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            foreach (var file in files)
                Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            Assert.Equal(unrelatedUidNamedBefore, File.ReadAllBytes(UidNamedAccountProofPath));
        }
    }
}
