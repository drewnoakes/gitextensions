using System.Diagnostics.CodeAnalysis;
using GitCommands.ExternalLinks;

namespace GitUI.CommandsDialogs.SettingsDialog.RevisionLinks;

public interface ICloudProviderExternalLinkDefinitionExtractor
{
    string ServiceName { get; }
    Image Icon { get; }
    bool IsValidRemoteUrl(string remoteUrl);
    IList<ExternalLinkDefinition> GetDefinitions(string remoteUrl);

    /// <summary>
    ///  Tries to construct a web URL for viewing the given branch on this provider's site.
    /// </summary>
    bool TryBuildBranchUrl(string remoteUrl, string branchName, [NotNullWhen(true)] out string? url);
}
