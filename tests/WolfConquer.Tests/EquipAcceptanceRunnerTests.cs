using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class EquipAcceptanceRunnerTests
{
    [WindowsGameAcceptanceFact]
    public async Task ActualInspectorSnapshotsRequireBothExactRelocationsAndRejectChangedInputs()
    {
        const uint account = 2749001, originalUid = 8013201, itemId = 410339;
        var parent = Path.GetFullPath(Path.GetTempPath());
        var fixtureRoot = Path.Combine(parent, "wolf-owned-admission-" + Guid.NewGuid().ToString("N"));
        var evidenceRoot = Path.Combine(parent, "wolf-game-acceptance-" + Guid.NewGuid().ToString("N"));
        var complete = false;
        try
        {
            Assert.True(LegacyOwnedAdmissionFixture.TryCreate(fixtureRoot, account, account, originalUid,
                ConquerProtocolProfile.VisibleDx8D13, out var fixture, out var reason), reason);
            Assert.NotNull(fixture);
            Assert.True(LegacyOwnedAdmissionOptions.TryReadFile(fixture.ConfigurationPath, out var options, out reason), reason);
            var routes = Assert.IsType<LegacyOwnedAdmissionOptions>(options);
            Assert.True(LegacyItemUidRegistry.TryOpen(routes.RegistryOptions, routes.ExpectedDomain, out var registry, out reason), reason);
            uint uid;
            using (var issuer = Assert.IsType<LegacyItemUidRegistry>(registry))
            {
                Assert.True(issuer.TryReserve(1, out var reservation, out reason), reason);
                Assert.True(Assert.IsType<LegacyItemUidRegistry.Reservation>(reservation).TryTake(out var ids));
                uid = Assert.Single(ids);
            }
            // Quiescent test fixture seeding through the real serializer, not a game equip or grant claim.
            var loader = new PlayerItemSnapshotLoader(routes.ItemsRoot);
            var original = loader.LoadByCharacterUid(account);
            var gear = new PlayerItemSnapshotItem { Uid = uid, ItemId = itemId, StackSize = 1, Durability = 321, MaximumDurability = 321 };
            var baseline = original with { InventoryItems = original.InventoryItems.Concat(new[] { gear }).ToArray() };
            loader.SaveByCharacterUid(account, baseline);
            var statePath = Path.Combine(routes.ItemsRoot, ".managed-item-uids", "state.json");
            var uidState = File.ReadAllBytes(statePath);
            var originalCharacter = File.ReadAllBytes(Path.Combine(routes.UsersRoot, account + ".ini"));
            var repository = RepositoryRoot();
            using var tool = TestProjectProcess.Create(repository, "WolfConquer.DataTool");
            var args = new[] { "-Scenario", "equip-unequip", "-DataToolDll", tool.StartInfo.ArgumentList[0],
                "-OwnedAdmissionConfig", fixture.ConfigurationPath, "-AccountId", account.ToString(), "-EvidenceRoot", evidenceRoot,
                "-DotNetPath", FindApplication(tool.StartInfo.FileName, "DOTNET_HOST_PATH"), "-ExpectedItemUid", uid.ToString(),
                "-ExpectedItemId", itemId.ToString(), "-ExpectedEquipmentPosition", "4" };
            var initial = await RunAsync(args);
            Assert.Equal(2, initial.ExitCode);
            Assert.Equal("WaitingForEquip", Status(initial.Output));
            var before = File.ReadAllBytes(Path.Combine(evidenceRoot, "before.json"));
            var receipt = File.ReadAllBytes(Path.Combine(evidenceRoot, "receipt.json"));
            var unchanged = await RunAsync(args.Concat(new[] { "-Resume" }).ToArray());
            Assert.Equal(2, unchanged.ExitCode);
            Assert.Equal("ClientActionPendingOrStateMismatch", Status(unchanged.Output));

            var wrongSlot = baseline with { InventoryItems = original.InventoryItems, EquipmentItems = new[] { gear.WithPosition(5) } };
            loader.SaveByCharacterUid(account, wrongSlot);
            var wrong = await RunAsync(args.Concat(new[] { "-Resume" }).ToArray());
            Assert.Equal(2, wrong.ExitCode);
            Assert.Equal("ClientActionPendingOrStateMismatch", Status(wrong.Output));
            loader.SaveByCharacterUid(account, baseline with { InventoryItems = [],
                EquipmentItems = new[] { original.InventoryItems[0].WithPosition(1), gear.WithPosition(4) } });
            var unrelatedMove = await RunAsync(args.Concat(new[] { "-Resume" }).ToArray());
            Assert.Equal(2, unrelatedMove.ExitCode);
            Assert.Equal("ClientActionPendingOrStateMismatch", Status(unrelatedMove.Output));
            loader.SaveByCharacterUid(account, wrongSlot with { EquipmentItems = new[] { gear.WithPosition(4) } });
            var characterPath = Path.Combine(routes.UsersRoot, account + ".ini");
            var changedCharacter = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(originalCharacter)
                .Replace("Money=100", "Money=101", StringComparison.Ordinal));
            Assert.NotEqual(originalCharacter, changedCharacter);
            File.WriteAllBytes(characterPath, changedCharacter);
            var changedGold = await RunAsync(args.Concat(new[] { "-Resume" }).ToArray());
            Assert.Equal(2, changedGold.ExitCode);
            Assert.Equal("ClientActionPendingOrStateMismatch", Status(changedGold.Output));
            File.WriteAllBytes(characterPath, originalCharacter);
            var equipped = await RunAsync(args.Concat(new[] { "-Resume" }).ToArray());
            Assert.Equal(2, equipped.ExitCode);
            Assert.Equal("SavedEquipObserved", Status(equipped.Output));
            string equipReceipt;
            using (var json = JsonDocument.Parse(equipped.Output)) equipReceipt = json.RootElement.GetProperty("receipt").GetString()!;
            loader.SaveByCharacterUid(account, baseline);
            var unequipped = await RunAsync(args.Concat(new[] { "-Resume", "-EquippedReceipt", equipReceipt }).ToArray());
            Assert.Equal(2, unequipped.ExitCode);
            Assert.Equal("SavedEquipUnequipObserved", Status(unequipped.Output));
            Assert.Equal(before, File.ReadAllBytes(Path.Combine(evidenceRoot, "before.json")));
            Assert.Equal(receipt, File.ReadAllBytes(Path.Combine(evidenceRoot, "receipt.json")));
            Assert.Equal(uidState, File.ReadAllBytes(statePath));
            Assert.Equal(originalCharacter, File.ReadAllBytes(Path.Combine(routes.UsersRoot, account + ".ini")));
            var mismatched = args.ToArray();
            mismatched[Array.IndexOf(mismatched, "-ExpectedEquipmentPosition") + 1] = "5";
            var refusal = await RunAsync(mismatched.Concat(new[] { "-Resume", "-EquippedReceipt", equipReceipt }).ToArray());
            Assert.Equal(1, refusal.ExitCode);
            Assert.Equal("PrerequisiteMissingOrOutcomeUnproven", Status(refusal.Output));
            complete = true;
        }
        finally
        {
            if (complete) { DeleteOwned(fixtureRoot, "wolf-owned-admission-"); DeleteOwned(evidenceRoot, "wolf-game-acceptance-"); }
        }
    }

    private static string? Status(string text)
    { using var json = JsonDocument.Parse(text); return json.RootElement.GetProperty("status").GetString(); }
    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        var start = new ProcessStartInfo(FindApplication("pwsh", "WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", Path.Combine(RepositoryRoot(), "tools", "testing", "Invoke-GameAcceptance.ps1") }.Concat(args))
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException primary)
        {
            try { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception cleanup) { throw new AggregateException("Acceptance child timeout and cleanup both failed.", primary, cleanup); }
            throw;
        }
        return (process.ExitCode, await output, await error);
    }

    private static string FindApplication(string command, string variable)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return Path.GetFullPath(configured);
        if (Path.IsPathFullyQualified(command) && File.Exists(command)) return command;
        var name = OperatingSystem.IsWindows() && !command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? command + ".exe" : command;
        foreach (var part in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var candidate = Path.Combine(part.Trim('"'), name);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        throw new FileNotFoundException("Required actual executable not found: " + name);
    }
    private static string RepositoryRoot([CallerFilePath] string source = "")
    {
        for (var path = new DirectoryInfo(Path.GetDirectoryName(source)!); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "WolfConquer.sln"))) return path.FullName;
        throw new DirectoryNotFoundException("Repository root unavailable.");
    }
    private static void DeleteOwned(string root, string prefix)
    {
        var full = Path.GetFullPath(root);
        var leaf = Path.GetFileName(full);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(full), ignoreCase: true);
        Assert.StartsWith(prefix, leaf, StringComparison.Ordinal);
        Assert.True(Guid.TryParseExact(leaf[prefix.Length..], "N", out var id) && id != Guid.Empty);
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
