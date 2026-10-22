using System.ComponentModel;
using System.Text.RegularExpressions;
using WolfConquer.Updates.Progress;

namespace WolfConquer.Updates.Source;

public sealed record SourceUpdateResult(string Status, string BeforeCommit, string AfterCommit, string Message);

/// <summary>Updates source only: trusted remote, clean checkout and fast-forward only.</summary>
public sealed class SourceCheckoutUpdater
{
    private readonly bool _allowLocalRemoteForTests;
    public SourceCheckoutUpdater() { }
    internal SourceCheckoutUpdater(bool allowLocalRemoteForTests) => _allowLocalRemoteForTests = allowLocalRemoteForTests;

    public async Task<SourceUpdateResult> UpdateAsync(string repositoryRoot, string expectedRemoteUrl,
        string branch = "main", CancellationToken cancellationToken = default, IProgress<UpdateOperationProgress>? progress = null)
    {
        var observer = new UpdateProgressReporter(progress);
        var before = "";
        SourceUpdateGit? git = null;
        var mergeAttempted = false;
        SourceUpdateResult Result(string status, string message, string? after = null)
        {
            observer.Report(status switch { "updated" => UpdateOperationPhase.Completed, "current" => UpdateOperationPhase.Current,
                _ => UpdateOperationPhase.Skipped }, message);
            return new(status, before, after ?? before, message);
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            observer.Report(UpdateOperationPhase.Checking, "Checking trusted source checkout and local changes.");
            if (!SourceCheckoutSafety.IsAllowedRemote(expectedRemoteUrl, _allowLocalRemoteForTests))
                return Result("refused", "Expected remote must be an explicit public HTTPS URL without credentials.");
            var root = SourceCheckoutSafety.Root(repositoryRoot);
            if (!Directory.Exists(root)) return Result("refused", "Checkout directory does not exist.");
            git = new SourceUpdateGit(root, _allowLocalRemoteForTests);
            var top = (await git.RunAsync(cancellationToken, "rev-parse", "--show-toplevel")).RequireSuccess();
            if (!SourceCheckoutSafety.SamePath(root, top)) return Result("refused", "An exact worktree root is required.");
            before = (await git.RunAsync(cancellationToken, "rev-parse", "--verify", "HEAD^{commit}")).RequireSuccess();
            if (string.IsNullOrWhiteSpace(branch) || branch.StartsWith('-') || branch.Contains("@{") ||
                (await git.RunAsync(cancellationToken, "check-ref-format", "refs/heads/" + branch)).ExitCode != 0)
                return Result("refused", "Requested branch is invalid.");
            if (!await OnBranchAsync(git, branch, cancellationToken)) return Result("refused", "Current branch does not match the requested branch.");
            if (!await SourceCheckoutSafety.ConfigurationIsSafeAsync(git, expectedRemoteUrl, cancellationToken))
                return Result("refused", "Origin URL, URL rewriting, or executable filter configuration is not trusted.");
            if (await SourceCheckoutSafety.HasBlockedStateAsync(git, root, cancellationToken))
                return Result("refused", "In-progress Git operation or submodule checkout requires manual attention.");
            if (await SourceCheckoutSafety.IsDirtyAsync(git, cancellationToken))
                return Result("local-changes", "Tracked or untracked local changes were preserved.");

            var reference = "refs/heads/" + branch;
            observer.Report(UpdateOperationPhase.Fetching, "Fetching the trusted source branch without changing working files.");
            var advertised = (await git.RunAsync(cancellationToken, "ls-remote", "--exit-code", "--refs", "--", expectedRemoteUrl, reference)).RequireSuccess();
            var parts = advertised.Split('\t');
            if (parts.Length != 2 || parts[1] != reference || !Regex.IsMatch(parts[0], "\\A(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})\\z"))
                return Result("refused", "Trusted branch did not advertise one commit.");
            var target = parts[0];
            // No remote.origin.fetch configuration, local ref publication, or shared FETCH_HEAD authority.
            (await git.RunAsync(cancellationToken, "fetch", "--no-tags", "--no-recurse-submodules", "--no-auto-maintenance",
                "--no-write-fetch-head", "--", expectedRemoteUrl, reference)).RequireSuccess();
            if ((await git.RunAsync(cancellationToken, "cat-file", "-t", target)).RequireSuccess() != "commit")
                return Result("refused", "Advertised branch commit was not fetched.");
            if (await SourceCheckoutSafety.TargetContainsSubmodulesAsync(git, target, cancellationToken))
                return Result("refused", "Trusted branch contains submodules and requires manual handling.");

            if (!await OnBranchAsync(git, branch, cancellationToken) ||
                (await git.RunAsync(cancellationToken, "rev-parse", "HEAD")).RequireSuccess() != before ||
                !await SourceCheckoutSafety.ConfigurationIsSafeAsync(git, expectedRemoteUrl, cancellationToken) ||
                await SourceCheckoutSafety.HasBlockedStateAsync(git, root, cancellationToken))
                return Result("refused", "Checkout identity or Git state changed during fetch.");
            if (await SourceCheckoutSafety.IsDirtyAsync(git, cancellationToken))
                return Result("local-changes", "Local changes appeared during fetch and were preserved.");
            if (target.Equals(before, StringComparison.OrdinalIgnoreCase)) return Result("current", "Checkout matches the advertised trusted branch commit.");
            var forward = await git.RunAsync(cancellationToken, "merge-base", "--is-ancestor", before, target);
            if (forward.ExitCode == 1)
            {
                var ahead = await git.RunAsync(cancellationToken, "merge-base", "--is-ancestor", target, before);
                if (ahead.ExitCode is not (0 or 1)) ahead.RequireSuccess();
                return Result("local-commits", ahead.ExitCode == 0 ? "Local commits are ahead of the remote and were preserved." :
                    "Local and remote commits diverged; neither side was reset or rebased.");
            }
            forward.RequireSuccess();
            // Repeat immediately before merge, not merely before the network operation.
            if (!await OnBranchAsync(git, branch, cancellationToken) ||
                (await git.RunAsync(cancellationToken, "rev-parse", "HEAD")).RequireSuccess() != before ||
                await SourceCheckoutSafety.HasBlockedStateAsync(git, root, cancellationToken))
                return Result("refused", "Checkout changed before fast-forward.");
            if (await SourceCheckoutSafety.IsDirtyAsync(git, cancellationToken))
                return Result("local-changes", "Local changes appeared before fast-forward and were preserved.");
            mergeAttempted = true;
            observer.Report(UpdateOperationPhase.FastForwarding, "Fast-forwarding the clean source checkout.");
            (await git.RunAsync(cancellationToken, "merge", "--ff-only", "--no-edit", "--no-overwrite-ignore", "--", target)).RequireSuccess();
            var after = (await git.RunAsync(cancellationToken, "rev-parse", "HEAD")).RequireSuccess();
            return after.Equals(target, StringComparison.OrdinalIgnoreCase)
                ? Result("updated", "Source fast-forwarded to the pinned trusted branch commit; no services were restarted.", after)
                : Result("refused", "Checkout HEAD changed unexpectedly; inspect it manually.", after);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            observer.Report(UpdateOperationPhase.Skipped, "Source update canceled; inspect the checkout before retrying.");
            throw;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException or Win32Exception or TimeoutException)
        {
            var after = before;
            if (mergeAttempted && git is not null)
            {
                try { after = (await git.RunAsync(CancellationToken.None, "rev-parse", "HEAD")).RequireSuccess(); }
                catch (Exception) { after = ""; }
            }
            return Result("refused", "Source update did not finish; no reset, stash, forced checkout, build or service restart was requested.", after);
        }
        finally { git?.Dispose(); }
    }

    private static async Task<bool> OnBranchAsync(SourceUpdateGit git, string branch, CancellationToken token)
    {
        var current = await git.RunAsync(token, "symbolic-ref", "--quiet", "--short", "HEAD");
        return current.ExitCode == 0 && current.Output.TrimEnd('\r', '\n') == branch;
    }
}
