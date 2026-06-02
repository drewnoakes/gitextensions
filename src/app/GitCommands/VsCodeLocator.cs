namespace GitCommands;

/// <summary>
///  Locates Visual Studio Code and Visual Studio Code Insiders installations.
/// </summary>
public static class VsCodeLocator
{
    private static readonly Lazy<string?> _vsCodePath = new(FindVsCode);
    private static readonly Lazy<string?> _vsCodeInsidersPath = new(FindVsCodeInsiders);

    /// <summary>
    ///  Gets the full path to <c>Code.exe</c>, or <see langword="null"/> if not found.
    /// </summary>
    public static string? VsCodePath => _vsCodePath.Value;

    /// <summary>
    ///  Gets the full path to <c>Code - Insiders.exe</c>, or <see langword="null"/> if not found.
    /// </summary>
    public static string? VsCodeInsidersPath => _vsCodeInsidersPath.Value;

    /// <summary>
    ///  Forces eager evaluation of both paths on the current thread so that
    ///  subsequent property accesses are free. Call from a background thread
    ///  to avoid blocking the UI.
    /// </summary>
    public static void EnsureInitialized()
    {
        _ = _vsCodePath.Value;
        _ = _vsCodeInsidersPath.Value;
    }

    private static string? FindVsCode()
    {
        string result = "Code.exe".FindInFolders(
        [
            Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code"),
            @"Microsoft VS Code\",
        ]);

        return result.Length > 0 ? result : null;
    }

    private static string? FindVsCodeInsiders()
    {
        string result = "Code - Insiders.exe".FindInFolders(
        [
            Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code Insiders"),
            @"Microsoft VS Code Insiders\",
        ]);

        return result.Length > 0 ? result : null;
    }
}
