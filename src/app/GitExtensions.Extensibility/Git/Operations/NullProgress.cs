namespace GitExtensions.Extensibility.Git.Operations;

/// <summary>
///  An <see cref="IProgress{T}"/> implementation that discards all reported values.
/// </summary>
public sealed class NullProgress<T> : IProgress<T>
{
    /// <summary>
    ///  Gets a shared instance of <see cref="NullProgress{T}"/>.
    /// </summary>
    public static NullProgress<T> Instance { get; } = new();

    void IProgress<T>.Report(T value)
    {
        // Intentionally empty — no observer is attached.
    }
}
