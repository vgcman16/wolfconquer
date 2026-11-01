using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData("unknown-field")] [InlineData("wrong-number-kind")]
    [InlineData("wrong-account")] [InlineData("lost-origin")]
    [InlineData("reverse-prefix")] [InlineData("foreign-stage")]
    [InlineData("changed-items")] [InlineData("duplicate-player")]
    public Task CorruptingAnActuallyIssuedDeathIntentCannotAuthorizeRecoveryOrDiscardHistory(string fault) =>
        RunPhysicalDeathAsync(async f =>
        {
            var captured = await CapturePhysicalDeathAsync(f, 0);
            await f.Owner.CloseAsync();
            var record = captured.Record;
            var operation = record["PhysicalDeath"]!.AsObject();
            bool changedRecord = true;
            switch (fault)
            {
                case "unknown-field": operation["Accepted"] = true; break;
                case "wrong-number-kind": operation["TargetUid"] = DeathTargetUid.ToString(System.Globalization.CultureInfo.InvariantCulture); break;
                case "wrong-account": record["Changes"]![0]!["FileId"] = DeathTargetUid; break;
                case "lost-origin":
                {
                    var row = record["Changes"]![0]!;
                    var image = Convert.FromBase64String(row["After"]!.GetValue<string>());
                    var text = CharacterSnapshotFileEncoding.Decode(image, out var encoding);
                    const string marker = "ManagedDerivedSpellOrigins=20000:5\r\n";
                    Assert.Contains(marker, text, StringComparison.Ordinal);
                    row["After"] = Convert.ToBase64String(encoding.GetPreamble().Concat(
                        encoding.GetBytes(text.Replace(marker, "", StringComparison.Ordinal))).ToArray());
                    break;
                }
                case "reverse-prefix":
                    File.WriteAllBytes(f.SpellsPath, captured.Before.After[Path.GetRelativePath(f.Owner.Data.Root, f.SpellsPath)]);
                    changedRecord = false; break;
                case "foreign-stage":
                    File.WriteAllBytes(f.SpellsPath + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp", [1]);
                    changedRecord = false; break;
                case "changed-items":
                {
                    var image = File.ReadAllBytes(f.ItemsPath); Assert.NotEmpty(image);
                    image[^1] ^= 1; File.WriteAllBytes(f.ItemsPath, image);
                    changedRecord = false; break;
                }
                case "duplicate-player":
                    File.WriteAllBytes(Path.Combine(f.Owner.Data.Users, (DeathTargetAccount + 1).ToString(
                        System.Globalization.CultureInfo.InvariantCulture) + ".ini"), File.ReadAllBytes(f.CharacterPath));
                    changedRecord = false; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            if (changedRecord) File.WriteAllText(f.Owner.PendingPath, record.ToJsonString(), new UTF8Encoding(false));
            var files = f.Owner.Data.Files();
            for (int restart = 0; restart < 2; restart++)
            {
                Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Owner.Data.Loader, f.Owner.Data.Options(),
                    out var opened, out var reason), reason);
                Assert.Null(opened); Assert.False(string.IsNullOrWhiteSpace(reason));
                f.Owner.Data.AssertFiles(files); AssertDeathHistoryUnchanged(f, captured.Before);
            }
            using var lease = new FileStream(f.Owner.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        });
}
