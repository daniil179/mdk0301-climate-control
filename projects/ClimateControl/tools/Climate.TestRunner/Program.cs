using System.Collections.Concurrent;
using System.Text.Json;
using Xunit.Runners;

if (args.Length != 2) throw new ArgumentException("Arguments: assembly-name output.json");
string file = Path.Combine(AppContext.BaseDirectory, args[0] + ".dll");
using var runner = AssemblyRunner.WithoutAppDomain(file);
using var done = new ManualResetEventSlim();
var rows = new ConcurrentBag<object>();
int failures = 0, total = 0;
runner.OnTestPassed = item =>
{
    Console.WriteLine("PASS " + item.TestDisplayName);
    rows.Add(new { name = item.TestDisplayName, outcome = "Passed", seconds = item.ExecutionTime });
};
runner.OnTestFailed = item =>
{
    Console.WriteLine("FAIL " + item.TestDisplayName + "\n" + item.ExceptionMessage + "\n" + item.ExceptionStackTrace);
    rows.Add(new { name = item.TestDisplayName, outcome = "Failed", message = item.ExceptionMessage });
};
runner.OnErrorMessage = item => Console.WriteLine("RUNNER ERROR " + item.ExceptionMessage + "\n" + item.ExceptionStackTrace);
runner.OnExecutionComplete = summary =>
{
    failures = summary.TestsFailed; total = summary.TotalTests;
    Console.WriteLine($"TOTAL {summary.TotalTests}; FAILED {summary.TestsFailed}; SKIPPED {summary.TestsSkipped}");
    done.Set();
};
runner.Start(new AssemblyRunnerStartOptions { MaxParallelThreads = 1, Parallel = false });
if (!done.Wait(TimeSpan.FromMinutes(3))) throw new TimeoutException("xUnit execution timed out.");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
File.WriteAllText(args[1], JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
Environment.ExitCode = failures == 0 && total > 0 ? 0 : 1;
