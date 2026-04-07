# Operation Pattern Design

## Overview

This document describes a new pattern for structuring git operations in Git Extensions.
The goal is to replace the current approach — where operations are methods scattered
across god-interfaces (`IGitUICommands`, `IGitModule`) — with first-class operation
objects that are composable, testable, and self-describing.

## Core Abstractions

All types live in `GitExtensions.Extensibility.Git.Operations`.

### IOperation / IOperation\<TResult\>

An operation is an object that describes a unit of work and can execute itself:

```csharp
interface IOperation : IOperationMetadata
{
    Task ExecuteAsync(IOperationContext context, CancellationToken cancellationToken);
}

interface IOperation<TResult> : IOperationMetadata
{
    Task<TResult> ExecuteAsync(IOperationContext context, CancellationToken cancellationToken);
}
```

Operations are:
- **Immutable data + behaviour** — parameters as `init`-only properties, execution via `ExecuteAsync`
- **Self-describing** — metadata declares characteristics (title, changes repo, accesses remote, etc.)
- **Composable** — an operation can invoke sub-operations via `context.Runner`
- **Idempotent** — cheap to create, safe to reuse

### IOperationMetadata

Describes an operation's characteristics. The runner uses this for cross-cutting concerns:

```csharp
interface IOperationMetadata
{
    string Title { get; }
    bool CanChangeRepo { get; }
    bool AccessesRemote { get; }
    bool RequiresValidWorkingDirectory { get; }
    bool ProvidesProgress { get; }
}
```

### IOperationRunner

Runs operations with cross-cutting concerns (validation, notifications, hooks):

```csharp
interface IOperationRunner
{
    Task RunAsync(IOperation operation, CancellationToken cancellationToken);
    Task<TResult> RunAsync<TResult>(IOperation<TResult> operation, CancellationToken cancellationToken);
}
```

### IOperationContext

Provided to operations during execution:

```csharp
interface IOperationContext
{
    IOperationRunner Runner { get; }      // for sub-operations
    IGitRepository Repository { get; }    // repo identity, config, git executable
    IGitModule Module { get; }            // full module (transitional)
    IWin32Window? Window { get; }         // UI context (null when headless)
    IProgress<string> Progress { get; }   // progress reporting (never null)
}
```

### SimpleGitOperation

Base class for the ~80% of operations that just build arguments and run git:

```csharp
abstract class SimpleGitOperation : IOperation
{
    protected abstract ArgumentString BuildArguments();
    // ExecuteAsync starts the process via context.Repository.GitExecutable
}
```

### Interactive Operations (Porcelain vs Plumbing)

Operations that require UI implement `IInteractiveOperation` (which extends `IRequiresUI`).
The runner rejects these when no `Window` is available.

```csharp
interface IRequiresUI { }
interface IInteractiveOperation : IOperation, IRequiresUI { }
interface IInteractiveOperation<TResult> : IOperation<TResult>, IRequiresUI { }
```

### IGitRepository

Extracted from `IGitModule` — the structural identity and configuration of a repository,
without any operations. This is what `IOperationContext.Repository` exposes.

`IGitModule` extends `IGitRepository` and provides a transitional `Repository` property.

## OperationRunner Implementation

`OperationRunner` in `GitExtensions.Extensibility` provides the default cross-cutting pipeline:

1. Check cancellation
2. Validate working directory (outermost only)
3. Validate interactive context (outermost only)
4. Lock `RepoChangedNotifier`
5. Increment nesting depth (`AsyncLocal<int>`)
6. Execute operation
7. Decrement nesting depth
8. Unlock notifier (trigger notification if `CanChangeRepo` and succeeded)

Nesting is supported: sub-operations lock/unlock symmetrically. Notifications defer until
the outermost operation completes.

## Operations Implemented

### Plumbing (non-interactive)

| Operation | Base | Notes |
|-----------|------|-------|
| StashSaveOperation | SimpleGitOperation | Supports partial stash, message, keep-index |
| StashPopOperation | SimpleGitOperation | Named stash support |
| StashDropOperation | SimpleGitOperation | |
| StashApplyOperation | SimpleGitOperation | |
| StashStagedOperation | SimpleGitOperation | |
| CheckoutBranchOperation | SimpleGitOperation | Merge/force, remote, new branch |
| DeleteBranchOperation | SimpleGitOperation | Force, mixed local/remote |
| DeleteTagOperation | SimpleGitOperation | |
| CherryPickOperation | SimpleGitOperation | --no-commit, extra args |
| CleanOperation | SimpleGitOperation | Dry run, directories, excludes |
| MergeBranchOperation | SimpleGitOperation | Strategy, squash, no-commit |
| RebaseOperation | SimpleGitOperation | Interactive, autosquash |
| DeleteRemoteBranchesOperation | SimpleGitOperation | AccessesRemote = true |
| FetchOperation | IOperation | Config-aware (reads fetch.parallel, submodule.fetchjobs) |
| GetCurrentCheckoutOperation | IOperation\<ObjectId?\> | Query, CanChangeRepo = false |
| RevParseOperation | IOperation\<ObjectId?\> | Query with SHA short-circuit |

### Composite

| Operation | Composes | Notes |
|-----------|----------|-------|
| PullOperation | FetchOperation + MergeBranchOperation or RebaseOperation | Delegates via context.Runner |

### Porcelain (interactive)

| Operation | Wraps | Notes |
|-----------|-------|-------|
| StashSaveInteractiveOperation | StashSaveOperation | Requires Window |
| DeleteBranchInteractiveOperation | DeleteBranchOperation | Shows confirmation, returns OperationResult |

## Integration

`IGitUICommands.OperationRunner` exposes the runner to all forms and controls.
Created in `GitUICommands` constructor, wired to `Module` and `RepoChangedNotifier`.

## Known Challenges

### 1. FormProcess Owns Process Execution (MAJOR)

**Problem:** Today, `FormProcess` both starts the git process AND displays its output.
Operations also start the git process (via `SimpleGitOperation.ExecuteAsync`).
These two things can't both own the same process.

**Current architecture:**
```
FormProcess.ShowDialog()
  → FormStatus.Start()
    → ProcessCallback (= FormProcess.ProcessStart)
      → ConsoleOutputControl.StartProcess(command, args, workDir, envVars)
        → System.Diagnostics.Process.Start()
        → AsyncStreamReader captures stdout/stderr
        → FireDataReceived events → UI display
        → FireProcessExited → OnExit → Done()
```

**What needs to change:** The operation should own process execution. The UI dialog
should observe the operation's progress, not own the process. Two approaches:

**Approach A — Output-capturing operations:**
Enhance `SimpleGitOperation` (or create a new variant) to capture stdout/stderr
and report line-by-line via `IProgress<string>`. Build a new progress dialog that
subscribes to this progress and displays it. The operation owns the process, the
dialog is pure presentation.

**Approach B — IProcess handoff:**
The operation starts the process and exposes the running `IProcess` for a display
component to read from. This requires the operation to return an `IProcess` handle,
which leaks implementation details and couples the display to the process model.

**Recommended:** Approach A. Operations report progress, dialogs observe it.

### 2. Synchronous Call Sites

**Problem:** Most callers are synchronous WinForms event handlers.

**Solution:** Convert event handlers to `async void` — standard WinForms async pattern.
Do NOT add synchronous wrapper methods. Callers should explicitly await.

### 3. FormProcess Side Effects

`FormProcess` does more than display output:
- WSL path wrapping
- Git index unlock on abort
- Retry support (FormRemoteProcess)
- Auto-close on success
- Output history recording

These become runner concerns or operation implementations over time.

### 4. Pre/Post Events and Script Hooks

The C# events (`PreCheckoutBranch`, `PostCommit`, etc.) and `ScriptEvent` system
aren't part of the operation system yet. Future work: implement as runner decorators.

### 5. FormatBranchName on IGitModule

`FetchOperation` needs `Module.FormatBranchName()` which is on `IGitModule` not
`IGitRepository`. Handled by the transitional `Module` property on context.

## Migration Strategy

### Phase 1: Foundation (COMPLETE)
- Core abstractions, runner, base classes
- 17 plumbing + 2 porcelain + 2 query + 1 composite operations
- IGitRepository extracted from IGitModule
- Runner wired into IGitUICommands
- 69 unit tests

### Phase 2: Process Output Bridge
- Enhance operations to capture and report stdout/stderr via `IProgress<string>`
- Build new progress dialog that observes operation progress
- Migrate one operation end-to-end

### Phase 3: Incremental Call Site Migration
- Convert event handlers to `async void`
- Replace old method calls with `await runner.RunAsync(...)`
- Remove old methods from IGitUICommands

### Phase 4: FormProcess Feature Parity
- Abort/cancel, WSL wrapping, index unlock, auto-close, output history

### Phase 5: Hooks and Decorators
- Script hooks, pre/post events, diagnostic waterfall

### Phase 6: Cleanup
- Remove Commands static class, slim IGitUICommands, remove IGitModule
