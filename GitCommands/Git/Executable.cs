using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GitCommands.Logging;
using GitUIPluginInterfaces;
using JetBrains.Annotations;

namespace GitCommands
{
    /// <summary>
    /// Defines an executable that can be launched to create processes.
    /// </summary>
    public interface IExecutable
    {
        /// <summary>
        /// Starts a process of this executable.
        /// </summary>
        /// <remarks>
        /// This is a low level means of starting a process. Most code will want to use on of the extension methods
        /// provided by <see cref="ExecutableExtensions"/>.
        /// </remarks>
        /// <param name="arguments">Any command line arguments to be passed to the executable when it is started.</param>
        /// <param name="createWindow">Whether to create a window for the process or not.</param>
        /// <param name="redirectInput">Whether the standard input stream of the process will be written to.</param>
        /// <param name="redirectOutput">Whether the standard output stream of the process will be read from.</param>
        /// <param name="outputEncoding">The <see cref="Encoding"/> to use when interpreting standard output and standard
        /// error, or <c>null</c> if <paramref name="redirectOutput"/> is <c>false</c>.</param>
        /// <returns>The started process.</returns>
        [NotNull]
        [MustUseReturnValue]
        IProcess Start(ArgumentString arguments = default, bool createWindow = false, bool redirectInput = false, bool redirectOutput = false, [CanBeNull] Encoding outputEncoding = null);
    }

    /// <summary>
    /// Defines a process instance.
    /// </summary>
    /// <remarks>
    /// This process will either be running or exited.
    /// </remarks>
    public interface IProcess : IDisposable
    {
        /// <summary>
        /// Gets an object that facilitates writing to the process's standard input stream.
        /// </summary>
        /// <remarks>
        /// To access the underlying <see cref="Stream"/>, dereference <see cref="StreamWriter.BaseStream"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">This process's input was not redirected
        /// when calling <see cref="IExecutable.Start"/>.</exception>
        StreamWriter StandardInput { get; }

        /// <summary>
        /// Gets an object that facilitates writing to the process's standard output stream.
        /// </summary>
        /// <remarks>
        /// To access the underlying <see cref="Stream"/>, dereference <see cref="StreamWriter.BaseStream"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">This process's output was not redirected
        /// when calling <see cref="IExecutable.Start"/>.</exception>
        StreamReader StandardOutput { get; }

        /// <summary>
        /// Gets an object that facilitates writing to the process's standard error stream.
        /// </summary>
        /// <remarks>
        /// To access the underlying <see cref="Stream"/>, dereference <see cref="StreamWriter.BaseStream"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">This process's output was not redirected
        /// when calling <see cref="IExecutable.Start"/>.</exception>
        StreamReader StandardError { get; }

        /// <summary>
        /// Gets any exit code returned by this process.
        /// </summary>
        /// <remarks>
        /// This value will be <c>null</c> if the process is still running, or if this object was disposed
        /// before the process exited.
        /// </remarks>
        int? ExitCode { get; }

        /// <summary>
        /// Blocks the calling thread until the process exits, or when this object is disposed.
        /// </summary>
        /// <returns>The process's exit code, or <c>null</c> if this object was disposed before the process exited.</returns>
        int? WaitForExit();

        /// <summary>
        /// Returns a task that completes when the process exits, or when this object is disposed.
        /// </summary>
        /// <returns>A task that yields the process's exit code, or <c>null</c> if this object was disposed before the process exited.</returns>
        Task<int?> WaitForExitAsync();

        /// <summary>
        /// Waits for the process to reach an idle state.
        /// </summary>
        /// <see cref="Process.WaitForInputIdle()"/>
        void WaitForInputIdle();
    }

    /// <inheritdoc />
    public sealed class Executable : IExecutable
    {
        private readonly string _workingDir;
        private readonly Func<string> _fileNameProvider;

        public Executable([NotNull] string fileName, [NotNull] string workingDir = "")
            : this(() => fileName, workingDir)
        {
        }

        public Executable([NotNull] Func<string> fileNameProvider, [NotNull] string workingDir = "")
        {
            _workingDir = workingDir;
            _fileNameProvider = fileNameProvider;
        }

        /// <inheritdoc />
        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public IProcess Start(ArgumentString arguments = default, bool createWindow = false, bool redirectInput = false, bool redirectOutput = false, Encoding outputEncoding = null)
        {
            // TODO should we set these on the child process only?
            EnvironmentConfiguration.SetEnvironmentVariables();

            var args = (arguments.Arguments ?? "").Replace("$QUOTE$", "\\\"");

            var fileName = _fileNameProvider();

            return new ProcessWrapper(fileName, args, _workingDir, createWindow, redirectInput, redirectOutput, outputEncoding);
        }

        #region ProcessWrapper

        /// <summary>
        /// Manages the lifetime of a process. The <see cref="System.Diagnostics.Process"/> object has many members
        /// that throw at different times in the lifecycle of the process, such as after it is disposed. This class
        /// provides a simplified API that meets the need of this application via the <see cref="IProcess"/> interface.
        /// </summary>
        private sealed class ProcessWrapper : IProcess
        {
            // TODO should this use TaskCreationOptions.RunContinuationsAsynchronously
            private readonly TaskCompletionSource<int?> _exitTaskCompletionSource = new TaskCompletionSource<int?>();

            private readonly Process _process;
            private readonly ProcessOperation _logOperation;
            private readonly bool _redirectInput;
            private readonly bool _redirectOutput;

            private int _disposed;

            /// <inheritdoc />
            public int? ExitCode { get; private set; }

            [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
            public ProcessWrapper(string fileName, string arguments, string workDir, bool createWindow, bool redirectInput, bool redirectOutput, [CanBeNull] Encoding outputEncoding)
            {
                Debug.Assert(redirectOutput == (outputEncoding != null), "redirectOutput == (outputEncoding != null)");
                _redirectInput = redirectInput;
                _redirectOutput = redirectOutput;

                _process = new Process
                {
                    EnableRaisingEvents = true,
                    StartInfo =
                    {
                        UseShellExecute = false,
                        ErrorDialog = false,
                        CreateNoWindow = !createWindow,
                        RedirectStandardInput = redirectInput,
                        RedirectStandardOutput = redirectOutput,
                        RedirectStandardError = redirectOutput,
                        StandardOutputEncoding = outputEncoding,
                        StandardErrorEncoding = outputEncoding,
                        FileName = fileName,
                        Arguments = arguments,
                        WorkingDirectory = workDir
                    }
                };

                _logOperation = CommandLog.LogProcessStart(fileName, arguments);

                _process.Exited += OnProcessExit;

                _process.Start();

                _logOperation.SetProcessId(_process.Id);
            }

            private void OnProcessExit(object sender, EventArgs eventArgs)
            {
                // The Exited event can be raised after the process is disposed, however
                // if the Process is disposed then reading ExitCode will throw.
                if (_disposed == 0)
                {
                    ExitCode = _process.ExitCode;
                }

                _logOperation.LogProcessEnd(ExitCode);
                _exitTaskCompletionSource.TrySetResult(ExitCode);
            }

            /// <inheritdoc />
            public StreamWriter StandardInput
            {
                get
                {
                    if (!_redirectInput)
                    {
                        throw new InvalidOperationException("Process was not created with redirected input.");
                    }

                    return _process.StandardInput;
                }
            }

            /// <inheritdoc />
            public StreamReader StandardOutput
            {
                get
                {
                    if (!_redirectOutput)
                    {
                        throw new InvalidOperationException("Process was not created with redirected output.");
                    }

                    return _process.StandardOutput;
                }
            }

            /// <inheritdoc />
            public StreamReader StandardError
            {
                get
                {
                    if (!_redirectOutput)
                    {
                        throw new InvalidOperationException("Process was not created with redirected output.");
                    }

                    return _process.StandardError;
                }
            }

            /// <inheritdoc />
            public void WaitForInputIdle() => _process.WaitForInputIdle();

            /// <inheritdoc />
            public Task<int?> WaitForExitAsync() => _exitTaskCompletionSource.Task;

            /// <inheritdoc />
            public int? WaitForExit()
            {
                if (_disposed == 0)
                {
                    return ExitCode;
                }

                _process.WaitForExit();

                return GitUI.ThreadHelper.JoinableTaskFactory.Run(() => _exitTaskCompletionSource.Task);
            }

            /// <inheritdoc />
            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                {
                    return;
                }

                _process.Exited -= OnProcessExit;

                _exitTaskCompletionSource.TrySetResult(null);

                _process.Dispose();
            }
        }

        #endregion
    }

    /// <summary>
    /// Provides extension methods for <see cref="IExecutable"/> that provider operations on executables
    /// at a higher level than <see cref="IExecutable.Start"/>.
    /// </summary>
    public static class ExecutableExtensions
    {
        private static readonly Regex _ansiCodePattern = new Regex(@"\u001B[\u0040-\u005F].*?[\u0040-\u007E]", RegexOptions.Compiled);
        private static readonly Encoding _defaultOutputEncoding = GitModule.SystemEncoding;

        /// <summary>
        /// Launches a process for the executable and returns its output.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="GitUI.ThreadHelper.JoinableTaskFactory"/> to allow the calling thread to
        /// do useful work while waiting for the process to exit. Internally, this method delegates to
        /// <see cref="GetOutputAsync"/>.
        /// </remarks>
        /// <param name="executable">The executable from which to launch a process.</param>
        /// <param name="arguments">The arguments to pass to the executable</param>
        /// <param name="input">Bytes to be written to the process's standard input stream, or <c>null</c> if no input is required.</param>
        /// <param name="outputEncoding">The text encoding to use when decoding bytes read from the process's standard output and standard error streams, or <c>null</c> if the default encoding is to be used.</param>
        /// <param name="cache">A <see cref="CommandCache"/> to use if command results may be cached, otherwise <c>null</c>.</param>
        /// <param name="stripAnsiEscapeCodes">A flag indicating whether ANSI escape codes should be removed from output strings.</param>
        /// <returns>The concatenation of standard output and standard error. To receive these outputs separately, use <see cref="Execute"/> instead.</returns>
        [NotNull]
        [MustUseReturnValue("If output text is not required, use " + nameof(RunCommand) + " instead")]
        public static string GetOutput(
            this IExecutable executable,
            ArgumentString arguments = default,
            byte[] input = null,
            Encoding outputEncoding = null,
            CommandCache cache = null,
            bool stripAnsiEscapeCodes = true)
        {
            return GitUI.ThreadHelper.JoinableTaskFactory.Run(
                () => executable.GetOutputAsync(arguments, input, outputEncoding, cache, stripAnsiEscapeCodes));
        }

        /// <summary>
        /// Launches a process for the executable and returns its output.
        /// </summary>
        /// <param name="executable">The executable from which to launch a process.</param>
        /// <param name="arguments">The arguments to pass to the executable</param>
        /// <param name="input">Bytes to be written to the process's standard input stream, or <c>null</c> if no input is required.</param>
        /// <param name="outputEncoding">The text encoding to use when decoding bytes read from the process's standard output and standard error streams, or <c>null</c> if the default encoding is to be used.</param>
        /// <param name="cache">A <see cref="CommandCache"/> to use if command results may be cached, otherwise <c>null</c>.</param>
        /// <param name="stripAnsiEscapeCodes">A flag indicating whether ANSI escape codes should be removed from output strings.</param>
        /// <returns>A task that yields the concatenation of standard output and standard error. To receive these outputs separately, use <see cref="ExecuteAsync"/> instead.</returns>
        [ItemNotNull]
        public static async Task<string> GetOutputAsync(
            this IExecutable executable,
            ArgumentString arguments = default,
            byte[] input = null,
            Encoding outputEncoding = null,
            CommandCache cache = null,
            bool stripAnsiEscapeCodes = true)
        {
            if (outputEncoding == null)
            {
                outputEncoding = _defaultOutputEncoding;
            }

            if (cache != null && cache.TryGet(arguments, out var output, out var error))
            {
                return ComposeOutput();
            }

            using (var process = executable.Start(
                arguments,
                createWindow: false,
                redirectInput: input != null,
                redirectOutput: true,
                outputEncoding))
            {
                if (input != null)
                {
                    await process.StandardInput.BaseStream.WriteAsync(input, 0, input.Length);
                    process.StandardInput.Close();
                }

                var outputBuffer = new MemoryStream();
                var errorBuffer = new MemoryStream();
                var outputTask = process.StandardOutput.BaseStream.CopyToAsync(outputBuffer);
                var errorTask = process.StandardError.BaseStream.CopyToAsync(errorBuffer);
                var exitTask = process.WaitForExitAsync();

                await Task.WhenAll(outputTask, errorTask, exitTask);

                output = outputBuffer.ToArray();
                error = errorBuffer.ToArray();

                if (cache != null && process.ExitCode == 0)
                {
                    cache.Add(arguments, output, error);
                }

                return ComposeOutput();
            }

            string ComposeOutput()
            {
                var composedOutput = EncodingHelper.DecodeString(output, error, ref outputEncoding);

                return stripAnsiEscapeCodes
                    ? StripAnsiCodes(composedOutput)
                    : composedOutput;
            }
        }

        /// <summary>
        /// Launches a process for the executable and returns <c>true</c> if its exit code is zero.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="GitUI.ThreadHelper.JoinableTaskFactory"/> to allow the calling thread to
        /// do useful work while waiting for the process to exit. Internally, this method delegates to
        /// <see cref="RunCommandAsync"/>.
        /// </remarks>
        /// <param name="executable">The executable from which to launch a process.</param>
        /// <param name="arguments">The arguments to pass to the executable</param>
        /// <param name="input">Bytes to be written to the process's standard input stream, or <c>null</c> if no input is required.</param>
        /// <param name="createWindow">A flag indicating whether a console window should be created and bound to the process.</param>
        /// <returns><c>true</c> if the process's exit code was zero, otherwise <c>false</c>.</returns>
        [MustUseReturnValue("Callers should verify that " + nameof(RunCommand) + " returned true")]
        public static bool RunCommand(
            this IExecutable executable,
            ArgumentString arguments = default,
            byte[] input = null,
            bool createWindow = false)
        {
            return GitUI.ThreadHelper.JoinableTaskFactory.Run(
                () => executable.RunCommandAsync(arguments, input, createWindow));
        }

        /// <summary>
        /// Launches a process for the executable and returns <c>true</c> if its exit code is zero.
        /// </summary>
        /// <param name="executable">The executable from which to launch a process.</param>
        /// <param name="arguments">The arguments to pass to the executable</param>
        /// <param name="input">Bytes to be written to the process's standard input stream, or <c>null</c> if no input is required.</param>
        /// <param name="createWindow">A flag indicating whether a console window should be created and bound to the process.</param>
        /// <returns>A task that yields <c>true</c> if the process's exit code was zero, otherwise <c>false</c>.</returns>
        public static async Task<bool> RunCommandAsync(
            this IExecutable executable,
            ArgumentString arguments = default,
            byte[] input = null,
            bool createWindow = false)
        {
            using (var process = executable.Start(arguments, createWindow: createWindow, redirectInput: input != null))
            {
                if (input != null)
                {
                    await process.StandardInput.BaseStream.WriteAsync(input, 0, input.Length);
                    process.StandardInput.Close();
                }

                return await process.WaitForExitAsync() == 0;
            }
        }

        /// <summary>
        /// Launches a process for the executable and returns output lines as they become available.
        /// </summary>
        /// <param name="executable">The executable from which to launch a process.</param>
        /// <param name="arguments">The arguments to pass to the executable</param>
        /// <param name="input">Bytes to be written to the process's standard input stream, or <c>null</c> if no input is required.</param>
        /// <param name="outputEncoding">The text encoding to use when decoding bytes read from the process's standard output and standard error streams, or <c>null</c> if the default encoding is to be used.</param>
        /// <param name="stripAnsiEscapeCodes">A flag indicating whether ANSI escape codes should be removed from output strings.</param>
        /// <returns>An enumerable sequence of lines that yields lines as they become available. Lines from standard output are returned first, followed by lines from standard error.</returns>
        [MustUseReturnValue("If output lines are not required, use " + nameof(RunCommand) + " instead")]
        public static IEnumerable<string> GetOutputLines(
            this IExecutable executable,
            ArgumentString arguments = default,
            byte[] input = null,
            Encoding outputEncoding = null,
            bool stripAnsiEscapeCodes = true)
        {
            // TODO make this method async, maybe via IAsyncEnumerable<...>?

            if (outputEncoding == null)
            {
                outputEncoding = _defaultOutputEncoding;
            }

            using (var process = executable.Start(arguments, createWindow: false, redirectInput: input != null, redirectOutput: true, outputEncoding))
            {
                if (input != null)
                {
                    process.StandardInput.BaseStream.Write(input, 0, input.Length);
                    process.StandardInput.Close();
                }

                while (true)
                {
                    var line = process.StandardOutput.ReadLine();

                    if (line == null)
                    {
                        break;
                    }

                    yield return stripAnsiEscapeCodes ? StripAnsiCodes(line) : line;
                }

                while (true)
                {
                    var line = process.StandardError.ReadLine();

                    if (line == null)
                    {
                        break;
                    }

                    yield return stripAnsiEscapeCodes ? StripAnsiCodes(line) : line;
                }

                process.WaitForExit();
            }
        }

        /// <summary>
        /// Launches a process for the executable and returns an object detailing exit code, standard output and standard error values.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="GitUI.ThreadHelper.JoinableTaskFactory"/> to allow the calling thread to
        /// do useful work while waiting for the process to exit. Internally, this method delegates to
        /// <see cref="ExecuteAsync"/>.
        /// </remarks>
        /// <param name="executable">The executable from which to launch a process.</param>
        /// <param name="arguments">The arguments to pass to the executable</param>
        /// <param name="writeInput">A callback that writes bytes to the process's standard input stream, or <c>null</c> if no input is required.</param>
        /// <param name="outputEncoding">The text encoding to use when decoding bytes read from the process's standard output and standard error streams, or <c>null</c> if the default encoding is to be used.</param>
        /// <param name="stripAnsiEscapeCodes">A flag indicating whether ANSI escape codes should be removed from output strings.</param>
        /// <returns>An <see cref="ExecutionResult"/> object that gives access to exit code, standard output and standard error values.</returns>
        [MustUseReturnValue("If execution result is not required, use " + nameof(RunCommand) + " instead")]
        public static ExecutionResult Execute(
            this IExecutable executable,
            ArgumentString arguments,
            Action<StreamWriter> writeInput = null,
            Encoding outputEncoding = null,
            bool stripAnsiEscapeCodes = true)
        {
            return GitUI.ThreadHelper.JoinableTaskFactory.Run(
                () => executable.ExecuteAsync(arguments, writeInput, outputEncoding, stripAnsiEscapeCodes));
        }

        /// <summary>
        /// Launches a process for the executable and returns an object detailing exit code, standard output and standard error values.
        /// </summary>
        /// <param name="executable">The executable from which to launch a process.</param>
        /// <param name="arguments">The arguments to pass to the executable</param>
        /// <param name="writeInput">A callback that writes bytes to the process's standard input stream, or <c>null</c> if no input is required.</param>
        /// <param name="outputEncoding">The text encoding to use when decoding bytes read from the process's standard output and standard error streams, or <c>null</c> if the default encoding is to be used.</param>
        /// <param name="stripAnsiEscapeCodes">A flag indicating whether ANSI escape codes should be removed from output strings.</param>
        /// <returns>A task that yields an <see cref="ExecutionResult"/> object that gives access to exit code, standard output and standard error values.</returns>
        public static async Task<ExecutionResult> ExecuteAsync(
            this IExecutable executable,
            ArgumentString arguments,
            Action<StreamWriter> writeInput = null,
            Encoding outputEncoding = null,
            bool stripAnsiEscapeCodes = true)
        {
            if (outputEncoding == null)
            {
                outputEncoding = _defaultOutputEncoding;
            }

            using (var process = executable.Start(arguments, createWindow: false, redirectInput: writeInput != null, redirectOutput: true, outputEncoding))
            {
                if (writeInput != null)
                {
                    // TODO do we want to make this async?
                    writeInput(process.StandardInput);
                    process.StandardInput.Close();
                }

                var outputBuffer = new MemoryStream();
                var errorBuffer = new MemoryStream();
                var outputTask = process.StandardOutput.BaseStream.CopyToAsync(outputBuffer);
                var errorTask = process.StandardError.BaseStream.CopyToAsync(errorBuffer);
                var exitTask = process.WaitForExitAsync();

                await Task.WhenAll(outputTask, errorTask, exitTask);

                var output = outputEncoding.GetString(outputBuffer.GetBuffer(), 0, (int)outputBuffer.Length);
                var error = outputEncoding.GetString(errorBuffer.GetBuffer(), 0, (int)errorBuffer.Length);

                if (stripAnsiEscapeCodes)
                {
                    output = StripAnsiCodes(output);
                    error = StripAnsiCodes(error);
                }

                var exitCode = await process.WaitForExitAsync();

                return new ExecutionResult(output, error, exitCode);
            }
        }

        [Pure]
        [NotNull]
        private static string StripAnsiCodes([NotNull] string input)
        {
            // Returns the original string if no ANSI codes are found
            return _ansiCodePattern.Replace(input, "");
        }
    }
}