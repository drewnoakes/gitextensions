using GitExtensions.Extensibility.Git;
using GitUIPluginInterfaces;

namespace GitCommands.Git.Extensions;

public static class GitRevisionExtensions
{
    /// <summary>
    /// Gets whether <paramref name="sha1"/> identifies an artificial revision.
    /// </summary>
    /// <param name="sha1">The revision string to check.</param>
    /// <returns><see langword="true"/> if the revision is artificial, otherwise <see langword="false"/>.</returns>
    public static bool IsArtificial(this string? sha1)
    {
        return sha1 is not null
            && ObjectId.TryParse(sha1, out ObjectId objectId)
            && objectId.IsArtificial;
    }

    /// <summary>
    /// Gets whether <paramref name="sha1"/> identifies an artificial working directory tree revision.
    /// </summary>
    /// <param name="sha1">The revision string to check.</param>
    /// <returns><see langword="true"/> if the revision represents a working directory tree, otherwise <see langword="false"/>.</returns>
    public static bool IsArtificialWorkTree(this string? sha1)
    {
        return sha1 is not null
            && ObjectId.TryParse(sha1, out ObjectId objectId)
            && objectId.IsArtificialWorkTree;
    }
}
