using System.Text;

namespace GitExtensions.Extensibility.Git;

/// <summary>
///  Represents the structural identity and configuration of a git repository.
///  This interface provides access to repository metadata, settings, paths,
///  and the low-level git execution primitives, without including any
///  higher-level operations.
/// </summary>
/// <remarks>
///  <para>
///   This is the interface that <see cref="Operations.IOperationContext"/> exposes
///   to operations. It is intentionally narrow — operations should use the
///   operation pattern for anything that invokes git, rather than calling
///   methods on this interface.
///  </para>
///  <para>
///   During the migration period, <see cref="IGitModule"/> extends this interface
///   and also exposes <see cref="IGitModule.Repository"/> as a convenience property.
///  </para>
/// </remarks>
public interface IGitRepository
{
    // ── Identity ──────────────────────────────────────────────────────

    /// <summary>
    ///  Gets the directory which contains the git repository (the working tree root).
    /// </summary>
    string WorkingDir { get; }

    /// <summary>
    ///  Gets the location of the <c>.git</c> directory for the current working folder.
    /// </summary>
    string WorkingDirGitDir { get; }

    /// <summary>
    ///  Gets the git common directory.
    ///  See <see href="https://git-scm.com/docs/git-rev-parse#Documentation/git-rev-parse.txt---git-common-dir"/>.
    /// </summary>
    string GitCommonDirectory { get; }

    /// <summary>
    ///  Gets a value indicating whether the working directory is a valid git repository.
    /// </summary>
    bool IsValidGitWorkingDir();

    /// <summary>
    ///  Gets a value indicating whether the repository is bare (no working tree).
    /// </summary>
    bool IsBareRepository();

    /// <summary>
    ///  Gets the git version for the default git executable.
    /// </summary>
    IGitVersion GitVersion { get; }

    // ── Submodule graph ──────────────────────────────────────────────

    /// <summary>
    ///  Gets the super-project of the current git module, if this is a submodule.
    /// </summary>
    IGitModule? SuperprojectModule { get; }

    /// <summary>
    ///  Gets the submodule path if this is a submodule, otherwise <see langword="null"/>.
    /// </summary>
    string? SubmodulePath { get; }

    /// <summary>
    ///  Gets a value indicating whether the repository has submodules.
    /// </summary>
    bool HasSubmodules();

    /// <summary>
    ///  Gets a submodule by name.
    /// </summary>
    IGitModule GetSubmodule(string? submoduleName);

    /// <summary>
    ///  Gets the local paths of any submodules.
    /// </summary>
    IReadOnlyList<string> GetSubmodulesLocalPaths(bool recursive = true);

    // ── Configuration ────────────────────────────────────────────────

    /// <summary>
    ///  Gets the effective (merged) value of a git config setting.
    /// </summary>
    string GetEffectiveSetting(string setting, string defaultValue = "");

    /// <summary>
    ///  Gets the effective value of a git config setting, converted to the specified type.
    /// </summary>
    T? GetEffectiveSetting<T>(string setting) where T : struct;

    /// <summary>
    ///  Gets the effective settings source.
    /// </summary>
    Settings.SettingsSource GetEffectiveSettings();

    /// <summary>
    ///  Invalidates the cached git config settings to trigger a reload.
    /// </summary>
    void InvalidateGitSettings();

    // ── Encoding ─────────────────────────────────────────────────────

    /// <summary>
    ///  Gets the encoding used for commit messages.
    /// </summary>
    Encoding CommitEncoding { get; }

    /// <summary>
    ///  Gets the encoding used for file contents.
    /// </summary>
    Encoding FilesEncoding { get; }

    /// <summary>
    ///  Gets the encoding used for log output (commit headers, author, etc.).
    /// </summary>
    Encoding LogOutputEncoding { get; }

    // ── Path utilities ───────────────────────────────────────────────

    /// <summary>
    ///  Converts a path for the git executable. For WSL Git, the path will be adjusted.
    /// </summary>
    string GetPathForGitExecution(string? path);

    /// <summary>
    ///  Converts a path to Windows application (native) format.
    /// </summary>
    string GetWindowsPath(string path);

    /// <summary>
    ///  Resolves a path relative to the <c>.git</c> directory.
    /// </summary>
    string ResolveGitInternalPath(string relativePath);

    /// <summary>
    ///  Gets the full path for a submodule given its local path.
    /// </summary>
    string GetSubmoduleFullPath(string localPath);

    // ── Git execution primitives ─────────────────────────────────────

    /// <summary>
    ///  Gets the default git executable associated with this repository.
    /// </summary>
    IExecutable GitExecutable { get; }

    /// <summary>
    ///  Gets the git command runner for background/detached execution.
    /// </summary>
    IGitCommandRunner GitCommandRunner { get; }
}
