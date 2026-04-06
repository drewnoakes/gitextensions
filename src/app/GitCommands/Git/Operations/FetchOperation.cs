using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Fetches from a remote repository via <c>git fetch</c>.
/// </summary>
public sealed class FetchOperation : IOperation
{
    /// <summary>
    ///  Gets the remote to fetch from (e.g. <c>origin</c>).
    /// </summary>
    public string? Remote { get; init; }

    /// <summary>
    ///  Gets the remote branch to fetch.
    /// </summary>
    public string? RemoteBranch { get; init; }

    /// <summary>
    ///  Gets the local branch to map the fetched branch to.
    /// </summary>
    public string? LocalBranch { get; init; }

    /// <summary>
    ///  Gets a value controlling tag fetching.
    ///  <see langword="true"/> to fetch tags, <see langword="false"/> to skip tags,
    ///  <see langword="null"/> for default behavior.
    /// </summary>
    public bool? FetchTags { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to convert a shallow clone to a full clone.
    /// </summary>
    public bool IsUnshallow { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to prune remote-tracking branches that no longer exist on the remote.
    /// </summary>
    public bool PruneRemoteBranches { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to also prune remote tags.
    /// </summary>
    public bool PruneRemoteBranchesAndTags { get; init; }

    /// <inheritdoc />
    public string Title => "Fetch";

    /// <inheritdoc />
    public bool CanChangeRepo => true;

    /// <inheritdoc />
    public bool AccessesRemote => true;

    /// <inheritdoc />
    public bool RequiresValidWorkingDirectory => true;

    /// <inheritdoc />
    public bool ProvidesProgress => true;

    /// <inheritdoc />
    public async Task ExecuteAsync(IOperationContext context, CancellationToken cancellationToken)
    {
        ArgumentString gitOptions = BuildFetchOptions(context.Module);
        ArgumentString fetchArgs = BuildFetchArgs(context.Module);

        ArgumentString arguments = new GitArgumentBuilder("fetch", gitOptions: gitOptions)
        {
            "--progress",
            {
                !string.IsNullOrEmpty(Remote) || !string.IsNullOrEmpty(RemoteBranch) || !string.IsNullOrEmpty(LocalBranch),
                fetchArgs
            },
        };

        using IProcess process = context.Module.GitExecutable.Start(
            arguments,
            throwOnErrorExit: true,
            cancellationToken: cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
    }

    private static ArgumentString BuildFetchOptions(IGitModule module)
    {
        return new ArgumentBuilder
        {
            { string.IsNullOrWhiteSpace(module.GetEffectiveSetting("fetch.parallel")), "-c fetch.parallel=0" },
            { string.IsNullOrWhiteSpace(module.GetEffectiveSetting("submodule.fetchjobs")), "-c submodule.fetchjobs=0" },
        };
    }

    private ArgumentString BuildFetchArgs(IGitModule module)
    {
        string? remoteBranch = RemoteBranch?.Replace(" ", "");
        string? localBranch = LocalBranch?.Replace(" ", "");

        string branchArguments = "";

        if (!string.IsNullOrEmpty(remoteBranch))
        {
            if (remoteBranch.StartsWith('+'))
            {
                remoteBranch = remoteBranch[1..];
            }

            branchArguments = "+" + module.FormatBranchName(remoteBranch);

            if (!string.IsNullOrEmpty(localBranch))
            {
                branchArguments += ":" + GitRefName.GetFullBranchName(localBranch);
            }
        }

        return new ArgumentBuilder
        {
            Remote.ToPosixPath()?.Trim().Quote(),
            branchArguments,
            { FetchTags == true, "--tags" },
            { FetchTags == false, "--no-tags" },
            { IsUnshallow, "--unshallow" },
            { PruneRemoteBranches || PruneRemoteBranchesAndTags, "--prune --force" },
            { PruneRemoteBranchesAndTags, "--prune-tags" },
        };
    }
}
