namespace GitExtensions.Extensibility.Git.Operations;

/// <summary>
///  Marker interface identifying an operation as interactive (requiring UI context).
///  The runner will reject interactive operations when no <see cref="IOperationContext.Window"/> is available.
/// </summary>
public interface IRequiresUI
{
}

/// <summary>
///  Marker interface for operations that require user interaction (showing dialogs,
///  prompts, or progress UI). This is analogous to git's "porcelain" commands.
/// </summary>
/// <remarks>
///  <para>
///   Interactive operations may only be executed when <see cref="IOperationContext.Window"/>
///   is available. The runner will reject interactive operations in headless contexts.
///  </para>
///  <para>
///   Interactive operations typically compose non-interactive ("plumbing") operations.
///   For example, a <c>CheckoutBranchInteractiveOperation</c> would show a dialog for
///   branch selection, then invoke a <see cref="SimpleGitOperation"/>-derived checkout.
///  </para>
/// </remarks>
public interface IInteractiveOperation : IOperation, IRequiresUI
{
}

/// <summary>
///  Marker interface for interactive operations that produce a typed result.
/// </summary>
/// <typeparam name="TResult">The type of the result produced by the operation.</typeparam>
public interface IInteractiveOperation<TResult> : IOperation<TResult>, IRequiresUI
{
}
