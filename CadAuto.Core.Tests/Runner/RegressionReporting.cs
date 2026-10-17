using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        private sealed class TestExecutionRecord
        {
            public string Name { get; set; }
            public int Priority { get; set; }
            public bool Selected { get; set; }
            public string Status { get; set; }
            public string Reason { get; set; }
            public string Error { get; set; }
            public double? ElapsedMilliseconds { get; set; }
        }

        private static readonly List<TestExecutionRecord> TestExecutions = new List<TestExecutionRecord>();
        private static string _executionFilter;
        private static bool _diagnosticContinuation;

        private static void ParseRegressionArguments(string[] args, out string filter, out bool diagnosticContinuation, out string reportPath)
        {
            filter = null;
            diagnosticContinuation = false;
            reportPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.IsNullOrEmpty(arg)) { continue; }
                if (arg == "--continue-on-failure")
                {
                    diagnosticContinuation = true;
                }
                else if (arg == "--report-json")
                {
                    if (reportPath != null || ++i >= args.Length || string.IsNullOrWhiteSpace(args[i]))
                        throw new ArgumentException("--report-json requires one new JSON file path.");
                    reportPath = Path.GetFullPath(args[i]);
                    if (!string.Equals(Path.GetExtension(reportPath), ".json", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("Regression report must use a .json extension.");
                    if (File.Exists(reportPath))
                        throw new IOException("Refusing to overwrite an existing regression report: " + reportPath);
                }
                else
                {
                    if (arg == "--filter")
                    {
                        if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]))
                            throw new ArgumentException("--filter requires a test name substring or P0/P1/P2/P3.");
                        arg = args[i];
                    }
                    else if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException("Unknown regression option: " + arg);
                    }
                    if (filter != null) { throw new ArgumentException("Only one regression filter is supported."); }
                    filter = arg;
                }
            }
        }

        private static void InitializeTestExecutions(string filter, bool diagnosticContinuation)
        {
            _executionFilter = filter;
            _diagnosticContinuation = diagnosticContinuation;
            TestExecutions.Clear();
            FailedTests.Clear();
            _passedTests = 0;
            foreach (var test in RegisteredTests)
            {
                int priority = GetTestPriority(test.Item1);
                bool selected = MatchesFilter(test.Item1, priority, filter);
                TestExecutions.Add(new TestExecutionRecord { Name = test.Item1, Priority = priority, Selected = selected,
                    Status = "Skipped", Reason = selected ? "NotReached" : "FilteredOut" });
            }
            _selectedTests = TestExecutions.Count(t => t.Selected);
        }

        private static void MarkBlockedTests(int failedPriority, string failedName)
        {
            foreach (var record in TestExecutions.Where(t => t.Selected && t.Reason == "NotReached"))
            {
                record.Reason = "BlockedByP" + failedPriority + ":" + failedName;
                Console.WriteLine("SKIP P" + record.Priority + " " + record.Name + " :: " + record.Reason);
            }
        }

        private static object CreateRegressionReport()
        {
            int passed = TestExecutions.Count(t => t.Status == "Passed");
            int failed = TestExecutions.Count(t => t.Status == "Failed");
            int blocked = TestExecutions.Count(t => t.Selected && t.Status == "Skipped");
            int filtered = TestExecutions.Count(t => !t.Selected);
            return new { schemaVersion = 1, scope = "CoreRegression_NotCadValidation",
                generatedAt = DateTime.UtcNow.ToString("o"), runtimeVersion = Environment.Version.ToString(),
                assemblyPath = typeof(Program).Assembly.Location, filter = _executionFilter,
                diagnosticContinuation = _diagnosticContinuation,
                productGatePassed = !_diagnosticContinuation && string.IsNullOrEmpty(_executionFilter) && failed == 0 && blocked == 0,
                registeredCount = TestExecutions.Count, selectedCount = passed + failed + blocked,
                executedCount = passed + failed, passedCount = passed, failedCount = failed,
                skippedCount = blocked + filtered, blockedCount = blocked, filteredOutCount = filtered,
                tests = TestExecutions.OrderBy(t => t.Priority).ThenBy(t => RegisteredTests.FindIndex(r => r.Item1 == t.Name)).ToList() };
        }

        private static void WriteRegressionReport(string reportPath)
        {
            if (string.IsNullOrEmpty(reportPath)) { return; }
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            // CreateNew is deliberate: neither old evidence nor an existing production file may be replaced.
            using (var stream = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(serializer.Serialize(CreateRegressionReport()));
        }

        private static void PrintRegressionSummary()
        {
            int executed = TestExecutions.Count(t => t.Status != "Skipped");
            int blocked = TestExecutions.Count(t => t.Selected && t.Status == "Skipped");
            int filtered = TestExecutions.Count(t => !t.Selected);
            Console.WriteLine("CadAuto.Core.Tests: registered=" + TestExecutions.Count + " selected=" + _selectedTests
                + " executed=" + executed + " passed=" + _passedTests + " failed=" + FailedTests.Count
                + " skipped=" + (blocked + filtered) + " (blocked=" + blocked + ", filtered=" + filtered + ").");
            if (_diagnosticContinuation)
                Console.WriteLine("DIAGNOSTIC ONLY: continuation does not authorize plugin build, deployment or CAD validation.");
        }

        // Synthetic actions exercise the actual executor. No product assertion is weakened or reclassified.
        private static void CheckDetailedRunner()
        {
            var output = Console.Out;
            try
            {
                Console.SetOut(new StringWriter());
                string[][] names = { P0Tests.Take(2).ToArray(), P1Tests.Take(2).ToArray(),
                    new[] { "ReportSyntheticOne", "ReportSyntheticTwo" }, P3Tests.Take(2).ToArray() };
                for (int failedTier = -1; failedTier <= 3; failedTier++)
                foreach (bool continuation in new[] { false, true })
                {
                    RegisteredTests.Clear();
                    var visited = new List<string>();
                    for (int tier = 0; tier < names.Length; tier++)
                    {
                        int currentTier = tier;
                        foreach (string name in names[tier])
                        {
                            string captured = name;
                            RunTest(name, () => { visited.Add(captured); if (currentTier == failedTier) throw new Exception("report self-check failure"); });
                        }
                    }
                    RunRegisteredTests(null, continuation);
                    int executed = continuation || failedTier < 0 ? 8 : failedTier == 0 ? 1 : (failedTier + 1) * 2;
                    int failed = failedTier < 0 ? 0 : continuation || failedTier > 0 ? 2 : 1;
                    Assert(visited.SequenceEqual(names.SelectMany(n => n).Take(executed)), "Detailed runner order/continuation mismatch");
                    Assert(TestExecutions.Count == 8 && _selectedTests == 8, "Selected count must include gate-blocked cases");
                    Assert(TestExecutions.Count(t => t.Status == "Failed") == failed && FailedTests.Count == failed, "Failure classification mismatch");
                    Assert(TestExecutions.Count(t => t.Status == "Passed") == executed - failed, "Unexecuted tests must not pass");
                    Assert(TestExecutions.Count(t => t.Status == "Skipped") == 8 - executed, "Blocked classification mismatch");
                    Assert(TestExecutions.Where(t => t.Status == "Failed").All(t => t.Error.Contains("System.Exception")), "Full failure stack must be retained");
                    Assert(TestExecutions.Where(t => t.Status == "Skipped").All(t => t.Reason.StartsWith("BlockedByP")), "Every blocked case needs a reason");
                    var serializer = new JavaScriptSerializer();
                    var json = serializer.Deserialize<Dictionary<string, object>>(serializer.Serialize(CreateRegressionReport()));
                    Assert((int)json["registeredCount"] == 8 && (int)json["executedCount"] == executed, "Machine summary must match execution");
                    Assert((bool)json["productGatePassed"] == (!continuation && failedTier < 0), "Diagnostic continuation cannot bypass the product gate");
                }
                // Filtering still classifies every registration, without calling unselected actions.
                RegisteredTests.Clear();
                RunTest(names[0][0], () => { throw new Exception("unselected test executed"); });
                RunTest("ReportSyntheticOne", () => { });
                RunRegisteredTests("P2", true);
                Assert(TestExecutions.Single(t => !t.Selected).Reason == "FilteredOut"
                    && TestExecutions.Single(t => t.Selected).Status == "Passed", "Filter status mismatch");
                string filter, path; bool diagnostic;
                ParseRegressionArguments(new[] { "P2", "--continue-on-failure", "--report-json", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json") }, out filter, out diagnostic, out path);
                Assert(filter == "P2" && diagnostic && path.EndsWith(".json"), "Regression argument parsing mismatch");
                bool rejected = false;
                try { ParseRegressionArguments(new[] { "--unknown" }, out filter, out diagnostic, out path); }
                catch (ArgumentException) { rejected = true; }
                Assert(rejected, "Unknown runner flags must not silently select no tests");
                string existing = Path.Combine(Path.GetTempPath(), "cad-regression-report-self-check-" + Guid.NewGuid().ToString("N") + ".json");
                WriteRegressionReport(existing);
                rejected = false;
                try { WriteRegressionReport(existing); }
                catch (IOException) { rejected = true; }
                Assert(rejected, "A historical report must never be overwritten");
            }
            finally
            {
                Console.SetOut(output);
                RegisteredTests.Clear(); FailedTests.Clear(); TestExecutions.Clear();
                _passedTests = _selectedTests = 0;
                _executionFilter = null; _diagnosticContinuation = false;
            }
        }
    }
}
