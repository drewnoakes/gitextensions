using System.Diagnostics.CodeAnalysis;
using GitCommands.Config;
using GitExtensions.Extensibility.Git;

namespace GitCommands.Remotes;

/// <summary>
///  Constructs web URLs for viewing a branch on known hosting providers
///  (GitHub, Azure DevOps, GitLab, Bitbucket).
/// </summary>
public static class RemoteBranchWebUrl
{
    /// <summary>
    ///  Tries to build a web URL for the given branch on the specified remote.
    /// </summary>
    /// <param name="module">The git module, used to look up the remote's fetch URL.</param>
    /// <param name="remoteName">The name of the remote (e.g. <c>origin</c>).</param>
    /// <param name="branchName">The branch name without any remote prefix (e.g. <c>main</c>).</param>
    /// <param name="url">The resulting web URL, if the remote is hosted on a known provider.</param>
    /// <returns><see langword="true"/> if a URL was constructed; <see langword="false"/> otherwise.</returns>
    public static bool TryBuild(IGitModule module, string remoteName, string branchName, [NotNullWhen(true)] out string? url)
    {
        url = null;

        string remoteUrl = module.GetSetting(string.Format(SettingKeyString.RemoteUrl, remoteName));
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return false;
        }

        return TryBuildFromUrl(remoteUrl, branchName, out url);
    }

    /// <summary>
    ///  Tries to build a web URL for the given branch from a remote fetch URL.
    /// </summary>
    public static bool TryBuildFromUrl(string remoteUrl, string branchName, [NotNullWhen(true)] out string? url)
    {
        url = null;

        // Azure DevOps
        AzureDevOpsRemoteParser azureParser = new();
        if (azureParser.TryExtractAzureDevopsDataFromRemoteUrl(remoteUrl, out string? owner, out string? project, out string? repo))
        {
            string? repoWebUrl = AzureDevOpsRemoteParser.BuildRepositoryUrl(remoteUrl, owner, project, repo);
            if (repoWebUrl is not null)
            {
                url = $"{repoWebUrl}?version=GB{Uri.EscapeDataString(branchName)}";
                return true;
            }
        }

        // GitHub, GitLab, Bitbucket, and other git hosting
        GitHostingRemoteParser hostingParser = new();
        if (hostingParser.TryExtractGitHostingDataFromRemoteUrl(remoteUrl, out string? hosting, out string? hostOwner, out string? hostRepo))
        {
            if (hosting.Contains("github", StringComparison.OrdinalIgnoreCase))
            {
                url = $"https://{hosting}/{hostOwner}/{hostRepo}/tree/{Uri.EscapeDataString(branchName)}";
                return true;
            }

            if (hosting.Contains("gitlab", StringComparison.OrdinalIgnoreCase))
            {
                url = $"https://{hosting}/{hostOwner}/{hostRepo}/-/tree/{Uri.EscapeDataString(branchName)}";
                return true;
            }

            if (hosting.Contains("bitbucket", StringComparison.OrdinalIgnoreCase))
            {
                url = $"https://{hosting}/{hostOwner}/{hostRepo}/src/{Uri.EscapeDataString(branchName)}";
                return true;
            }

            // Fallback for unknown hosting providers — assume GitHub-style URL pattern.
            url = $"https://{hosting}/{hostOwner}/{hostRepo}/tree/{Uri.EscapeDataString(branchName)}";
            return true;
        }

        return false;
    }
}
