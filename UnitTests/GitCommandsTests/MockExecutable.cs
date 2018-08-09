﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GitCommands;
using GitUIPluginInterfaces;
using JetBrains.Annotations;
using NUnit.Framework;

namespace GitCommandsTests
{
    internal sealed class MockExecutable : IExecutable
    {
        private readonly ConcurrentDictionary<string, ConcurrentStack<string>> _outputStackByArguments = new ConcurrentDictionary<string, ConcurrentStack<string>>();
        private readonly ConcurrentDictionary<string, int> _commandArgumentsSet = new ConcurrentDictionary<string, int>();
        private readonly List<MockProcess> _processes = new List<MockProcess>();
        private int _nextCommandId;

        [MustUseReturnValue]
        public IDisposable StageOutput(string arguments, string output)
        {
            var stack = _outputStackByArguments.GetOrAdd(
                arguments,
                args => new ConcurrentStack<string>());

            stack.Push(output);

            return new DelegateDisposable(
                () =>
                {
                    if (_outputStackByArguments.TryGetValue(arguments, out var s) &&
                        s.TryPeek(out var r) &&
                        ReferenceEquals(output, r))
                    {
                        throw new AssertionException($"Staged output should have been consumed.\nArguments: {arguments}\nOutput: {output}");
                    }
                });
        }

        [MustUseReturnValue]
        public IDisposable StageCommand(string arguments)
        {
            var id = Interlocked.Increment(ref _nextCommandId);
            _commandArgumentsSet[arguments] = id;

            return new DelegateDisposable(
                () =>
                {
                    if (_commandArgumentsSet.TryGetValue(arguments, out var storedId) && storedId != id)
                    {
                        throw new AssertionException($"Staged command should have been consumed.\nArguments: {arguments}");
                    }
                });
        }

        public void Verify()
        {
            Assert.IsEmpty(_outputStackByArguments, "All staged output should have been consumed.");
            Assert.IsEmpty(_commandArgumentsSet, "All staged output should have been consumed.");

            foreach (var process in _processes)
            {
                process.Verify();
            }
        }

        public IProcess Start(ArgumentString arguments, bool createWindow, bool redirectInput, bool redirectOutput, Encoding outputEncoding)
        {
            if (_outputStackByArguments.TryRemove(arguments, out var queue) && queue.TryPop(out var output))
            {
                if (queue.Count == 0)
                {
                    _outputStackByArguments.TryRemove(arguments, out _);
                }

                var process = new MockProcess(output);

                _processes.Add(process);
                return process;
            }

            if (_commandArgumentsSet.TryRemove(arguments, out _))
            {
                var process = new MockProcess();
                _processes.Add(process);
                return process;
            }

            throw new Exception("Unexpected arguments: " + arguments);
        }

        private sealed class MockProcess : IProcess
        {
            public MockProcess([CanBeNull] string output)
            {
                StandardOutput = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(output ?? "")));
                StandardError = new StreamReader(new MemoryStream());
                StandardInput = new StreamWriter(new MemoryStream());
            }

            public MockProcess()
            {
                StandardOutput = new StreamReader(new MemoryStream());
                StandardError = new StreamReader(new MemoryStream());
                StandardInput = new StreamWriter(new MemoryStream());
            }

            public StreamWriter StandardInput { get; }
            public StreamReader StandardOutput { get; }
            public StreamReader StandardError { get; }

            public int? ExitCode { get; }

            public int? WaitForExit()
            {
                // TODO implement if needed
                return 0;
            }

            public Task<int?> WaitForExitAsync()
            {
                // TODO implement if needed
                return Task.FromResult((int?)0);
            }

            public void WaitForInputIdle()
            {
                // TODO implement if needed
            }

            public void Dispose()
            {
                // TODO implement if needed
            }

            public void Verify()
            {
                // all output should have been read
                Assert.AreEqual(StandardOutput.BaseStream.Length, StandardOutput.BaseStream.Position);
                Assert.AreEqual(StandardError.BaseStream.Length, StandardError.BaseStream.Position);

                // no input should have been written (yet)
                Assert.AreEqual(0, StandardInput.BaseStream.Length);
            }
        }

        private sealed class DelegateDisposable : IDisposable
        {
            private readonly Action _disposeAction;

            public DelegateDisposable(Action disposeAction)
            {
                _disposeAction = disposeAction;
            }

            public void Dispose()
            {
                _disposeAction();
            }
        }
    }
}