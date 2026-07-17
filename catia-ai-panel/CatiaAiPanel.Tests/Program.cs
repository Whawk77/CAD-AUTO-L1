using System.Text.Json;
using CatiaAiPanel.Core;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Context fingerprint is stable", TestFingerprint),
    ("Measurement ledger is serialized for Codex", TestMeasurementSerialization),
    ("Native Measure dialog fields are parsed", TestNativeMeasureParsing),
    ("Guided native measurement binds only one compatible new result", TestGuidedMeasurementMatch),
    ("Continuous measurements accumulate without duplicates", TestMeasurementNotebookMerge),
    ("Reconstruction readiness reports missing semantics", TestReconstructionReadiness),
    ("Reconstruction readiness accepts a complete semantic ledger", TestCompleteReconstructionReadiness),
    ("Dimension inference proposes explicit derived parameters", TestDimensionInference),
    ("Latest pending proposal can be restored", TestSessionRestore),
    ("Sketch constraint references are normalized", TestMacroCompatibility),
    ("Context diff detects changes", TestDiff),
    ("Execution preconditions block unsafe state", TestPreconditions),
    ("New Part execution permits read-only reference", TestNewPartPreconditions),
    ("Safety validator blocks OS access", TestSafetyBlocks),
    ("Safety validator accepts constrained macro", TestSafetyAccepts),
    ("Safety validator requires declared New Part target", TestNewPartSafety),
    ("Safety validator isolates the reference document", TestNewPartIsolation),
    ("Safety validator accepts clarification without macro", TestSafetyAcceptsClarification),
    ("Codex JSONL parser reads thread and proposal", TestCodexProtocol),
    ("Benign Codex plugin warning is filtered", TestCodexDiagnosticFilter),
    ("Macro executor returns structured result", TestMacroExecutor),
    ("Macro executor rejects an unstructured result", TestMacroExecutorRejectsUnstructuredResult),
    ("Workflow resumes failed execution once", TestWorkflowCorrection)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  {test.Name}: {ex.Message}");
    }
}
Console.WriteLine($"\n{tests.Length - failed}/{tests.Length} tests passed.");
if (failed == 0 && args.Contains("--codex-smoke", StringComparer.OrdinalIgnoreCase))
{
    try
    {
        await TestLiveCodexConversation();
        Console.WriteLine("PASS  Live Codex start and resume");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  Live Codex start and resume: {ex.Message}");
    }
}
if (failed == 0 && args.Contains("--catia-smoke", StringComparer.OrdinalIgnoreCase))
{
    try
    {
        await using var liveCatia = new CatiaSession();
        var snapshot = await liveCatia.CaptureAsync();
        if (snapshot.Document is null) throw new InvalidOperationException(snapshot.ConnectionMessage);
        Console.WriteLine($"CATIA {snapshot.Document.Name} | {snapshot.Document.Workbench} | " +
                          $"selection={snapshot.Selection.Count} | truncated={snapshot.Truncated}");
        foreach (var selected in snapshot.Selection.Take(8))
            Console.WriteLine($"  SELECT type={selected.Type} name={selected.Name} leaf={selected.LeafProduct} " +
                              $"reference={selected.Reference}");
        foreach (var warning in snapshot.MeasurementWarnings)
            Console.WriteLine($"  MEASURE WARN {warning}");
        foreach (var item in snapshot.ReconstructionReadiness.Checklist)
            Console.WriteLine($"  READY {item.Key}={item.Status} | {item.Evidence}");
        Console.WriteLine(MeasurementSummaryFormatter.Format(snapshot.Measurements));
        Console.WriteLine("PASS  Live CATIA context capture");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  Live CATIA context capture: {ex.Message}");
    }
}
if (failed == 0 && args.Contains("--workflow-codex-smoke", StringComparer.OrdinalIgnoreCase))
{
    try
    {
        await TestLiveCatiaCodexWorkflow();
        Console.WriteLine("PASS  Live CATIA + Codex workflow");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  Live CATIA + Codex workflow: {ex.Message}");
    }
}
if (failed == 0 && args.Contains("--measurement-smoke", StringComparer.OrdinalIgnoreCase))
{
    try
    {
        await using var liveCatia = new CatiaSession();
        var result = await liveCatia.CaptureMeasurementsAsync();
        Console.WriteLine($"Measurement ledger: {result.Records.Count} record(s)");
        foreach (var record in result.Records.Take(10))
            Console.WriteLine($"  {record.Name}.{record.Quantity} = {record.Value:G10} {record.Unit}");
        foreach (var warning in result.Warnings) Console.WriteLine($"  WARN {warning}");
        Console.WriteLine("PASS  Live CATIA measurement capture");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  Live CATIA measurement capture: {ex.Message}");
    }
}
if (failed == 0 && args.Contains("--reconstruction-codex-smoke", StringComparer.OrdinalIgnoreCase))
{
    try
    {
        await TestLiveReconstructionPlanning();
        Console.WriteLine("PASS  Live reconstruction planning");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  Live reconstruction planning: {ex.Message}");
    }
}
if (failed == 0 && args.Contains("--synthetic-model-codex-smoke", StringComparer.OrdinalIgnoreCase))
{
    try
    {
        await TestSyntheticModelProposal();
        Console.WriteLine("PASS  Synthetic complete ledger produces a safe model proposal");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  Synthetic model proposal: {ex.Message}");
    }
}
var renameFlagIndex = Array.FindIndex(args, value =>
    value.Equals("--rename-live-measurements", StringComparison.OrdinalIgnoreCase));
if (failed == 0 && renameFlagIndex >= 0)
{
    try
    {
        var renameArgs = args[(renameFlagIndex + 1)..];
        if (renameArgs.Length == 0 || renameArgs.Length % 2 != 0)
            throw new ArgumentException("--rename-live-measurements requires <id> <name> pairs.");

        await using var liveCatia = new CatiaSession();
        for (var index = 0; index < renameArgs.Length; index += 2)
        {
            var records = await liveCatia.RenameMeasurementAsync(renameArgs[index], renameArgs[index + 1]);
            var renamed = records.Single(record => record.Id == renameArgs[index]);
            Console.WriteLine($"RENAMED {renamed.Id} -> {renamed.Name} = {renamed.Value:G10} {renamed.Unit}");
        }
        Console.WriteLine("PASS  Live measurement rename");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  Live measurement rename: {ex.Message}");
    }
}
if (failed == 0 && args.Contains("--clear-live-measurements", StringComparer.OrdinalIgnoreCase))
{
    try
    {
        await using var liveCatia = new CatiaSession();
        var records = await liveCatia.ClearMeasurementsAsync();
        Console.WriteLine($"CLEARED live measurement notebook; remaining={records.Count}");
        Console.WriteLine("PASS  Live measurement clear");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  Live measurement clear: {ex.Message}");
    }
}
return failed == 0 ? 0 : 1;

static Task TestCodexDiagnosticFilter()
{
    const string benign = "WARN codex_core_skills_loader: ignoring interface.icon large: icon path with '..' must resolve under plugin assets/";
    const string current = "2026-07-17 WARN codex_core_skills::loader: ignoring interface.icon_large: icon path with '..' must resolve under plugin assets/";
    const string manifest = "WARN codex_core_plugins::manifest: ignoring interface.defaultPrompt[0]: prompt must be at most 128 characters path=C:/plugins/plugin.json";
    const string marketplace = "WARN codex_core_plugins::marketplace: skipping marketplace that failed to load path=C:/plugins/marketplace.json error=invalid marketplace file";
    Assert.True(CodexDiagnosticFilter.IsBenignStderr(benign));
    Assert.True(CodexDiagnosticFilter.IsBenignStderr(current));
    Assert.True(CodexDiagnosticFilter.IsBenignStderr(manifest));
    Assert.True(CodexDiagnosticFilter.IsBenignStderr(marketplace));
    Assert.False(CodexDiagnosticFilter.IsBenignStderr("authentication failed"));
    Assert.False(CodexDiagnosticFilter.IsBenignStderr("codex_core_plugins::manifest: failed to parse plugin.json"));
    return Task.CompletedTask;
}

static Task TestFingerprint()
{
    var document = Document(saved: true);
    var selection = new[] { Selection("Pad.1") };
    Assert.Equal(ContextFingerprint.Compute(document, selection), ContextFingerprint.Compute(document, selection));
    Assert.NotEqual(ContextFingerprint.Compute(document, selection), ContextFingerprint.Compute(document, [Selection("Pad.2")]));
    var measured = new MeasurementSnapshot("m1", "overall_length", "length", 10, "mm", "Line", "Line.1", [], [], null, "test");
    Assert.NotEqual(ContextFingerprint.Compute(document, selection), ContextFingerprint.Compute(document, selection, [measured]));
    var changedTree = document with { Tree = [new("Different", "Body", [])] };
    Assert.Equal(ContextFingerprint.Compute(document, selection), ContextFingerprint.Compute(changedTree, selection));
    return Task.CompletedTask;
}

static Task TestMeasurementSerialization()
{
    var measurement = new MeasurementSnapshot(
        "m1", "overall_length", "minimumDistance", 125.5, "mm", "Distance", "A → B",
        [0, 0, 0, 125.5, 0, 0], [125.5, 0, 0], 0.01, "test");
    var context = Context(Document(saved: true), [Selection("Pad.1")], "same") with
    {
        CatiaVersion = "V5-6R2022 (B32, SP6)",
        Measurements = [measurement]
    };
    var serializer = new CatiaContextSerializer();
    var json = serializer.Serialize(context);
    Assert.True(json.Contains("overall_length", StringComparison.Ordinal));
    Assert.True(json.Contains("V5-6R2022", StringComparison.Ordinal));
    var prompt = new PromptBuilder(serializer).BuildInitial("重建零件", context);
    Assert.True(prompt.Contains("measurements", StringComparison.Ordinal));
    Assert.True(prompt.Contains("Parameters", StringComparison.Ordinal));
    Assert.True(new PromptBuilder(serializer).BuildReconstruction(context)
        .Contains("ValuateFromString", StringComparison.Ordinal));
    return Task.CompletedTask;
}

static Task TestNativeMeasureParsing()
{
    var fields = new NativeMeasureDialogReader.NativeMeasureField[]
    {
        new("半径", "5mm", "dynamic-1"),
        new("X", "3492.5mm", "dynamic-2"),
        new("Y", "321.5mm", "dynamic-3"),
        new("Z", "195mm", "dynamic-4")
    };
    var records = NativeMeasureDialogReader.ParseFields("demo", "弧 在 Part.1 内", fields);
    Assert.Equal(2, records.Count);
    var radius = records.Single(x => x.Quantity == "radius");
    var diameter = records.Single(x => x.Quantity == "diameter");
    Assert.Equal(5d, radius.Value);
    Assert.Equal(10d, diameter.Value);
    Assert.Equal(3, radius.Coordinates.Count);
    Assert.Equal(3492.5d, radius.Coordinates[0]);
    Assert.Equal("5mm", radius.RawValue);
    Assert.True(NativeMeasureDialogReader.IsSelectionLabel("选择 1："));
    Assert.True(NativeMeasureDialogReader.IsSelectionLabel("Selection 2"));
    return Task.CompletedTask;
}

static Task TestDimensionInference()
{
    static MeasurementSnapshot M(string name, double value, string quantity = "length") =>
        new(name, name, quantity, value, "mm", "test", name, [], [], null, "test");

    var measurements = new[]
    {
        M("overall_length", 65),
        M("overall_width", 16),
        M("overall_height", 43.7),
        M("hole_pitch", 10.44, "distance"),
        M("slot_width", 49.366),
        M("slot_depth", 27.862)
    };
    var suggestions = DimensionInferencePlanner.Build(measurements);
    Assert.True(suggestions.Any(x => x.Contains("AI_INFERRED_hole_diameter=4.176 mm", StringComparison.Ordinal)));
    Assert.True(suggestions.Any(x => x.Contains("AI_INFERRED_slot_end_radius=13.931 mm", StringComparison.Ordinal)));

    var context = Context(Document(saved: true), [], "inference") with { Measurements = measurements };
    var prompt = new PromptBuilder(new CatiaContextSerializer()).BuildReconstruction(context, allowDimensionInference: true);
    Assert.True(prompt.Contains("自动补齐未测尺寸", StringComparison.Ordinal));
    Assert.True(prompt.Contains("AI_INFERRED_", StringComparison.Ordinal));
    return Task.CompletedTask;
}

static async Task TestSessionRestore()
{
    var directory = Path.Combine(Path.GetTempPath(), "catia-ai-panel-session-restore-" + Guid.NewGuid().ToString("N"));
    var store = new SessionStore(directory);
    try
    {
        var completed = store.Create();
        completed.CurrentProposal = TestData.Proposal(OperationKind.Read, TestData.ValidCode);
        completed.LastExecution = new(true, false, "done", null, null,
            Context(Document(saved: true), [], "completed"), null, null,
            new(RiskLevel.None, []));
        await store.SaveAsync(completed);

        var pending = store.Create();
        pending.CurrentProposal = TestData.Proposal(OperationKind.Write, TestData.NewPartCode) with
        {
            TargetDocumentMode = TargetDocumentMode.NewPart
        };
        await store.SaveAsync(pending);

        await Task.Delay(20);
        var revised = store.Create();
        revised.CurrentProposal = pending.CurrentProposal;
        revised.CorrectionCount = 1;
        revised.LastExecution = new(false, false, "failed", "424", null,
            Context(Document(saved: true), [], "before"), Context(Document(saved: true), [], "after"), null,
            new(RiskLevel.None, []));
        await store.SaveAsync(revised);

        Assert.Equal(revised.LocalSessionId, store.LoadLatestPending()?.LocalSessionId);
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            foreach (var stateFile in Directory.EnumerateFiles(directory, "state.json", SearchOption.AllDirectories))
                File.Delete(stateFile);
            foreach (var child in Directory.EnumerateDirectories(directory)) Directory.Delete(child);
            Directory.Delete(directory);
        }
    }
}

static Task TestMacroCompatibility()
{
    const string code = """
        Function AIEntry(contextJson)
          Set lineRef = targetPart.CreateReferenceFromObject(line2D1)
          Set circleRef = targetPart.CreateReferenceFromObject(circle2D1)
          Set lengthCst = constraints.AddMonoEltCst(catCstTypeDistance, lineRef)
          Set radiusCst = constraints.AddMonoEltCst(catCstTypeRadius, circleRef)
          AIEntry = "{}"
        End Function
        """;
    var proposal = TestData.Proposal(OperationKind.Write, code);
    var normalized = MacroCompatibilityNormalizer.Normalize(proposal);
    Assert.True(normalized.Macro.Code.Contains("catCstTypeDistance, line2D1", StringComparison.Ordinal));
    Assert.True(normalized.Macro.Code.Contains("catCstTypeRadius, circle2D1", StringComparison.Ordinal));
    Assert.True(normalized.RiskFlags.Any(x => x.Contains("Sketcher.AddMonoEltCst", StringComparison.Ordinal)));
    return Task.CompletedTask;
}

static Task TestMeasurementNotebookMerge()
{
    var directory = Path.Combine(Path.GetTempPath(), "catia-ai-panel-measurements-" + Guid.NewGuid().ToString("N"));
    try
    {
        var store = new MeasurementNotebookStore(directory);
        var first = new MeasurementSnapshot("a", "radius_a", "radius", 5, "mm", "Arc", "A", [], [], null, "test");
        var second = new MeasurementSnapshot("b", "length_b", "length", 50, "mm", "Line", "B", [], [], null, "test");
        Assert.Equal(1, store.MergeAndSave("doc", [first]).Count);
        Assert.Equal(2, store.MergeAndSave("doc", [second]).Count);
        var updated = first with { Value = 6 };
        var merged = store.MergeAndSave("doc", [updated]);
        Assert.Equal(2, merged.Count);
        Assert.Equal(6d, merged.Single(x => x.Id == "a").Value);
        store.RenameAndSave("doc", "a", "overall_length");
        var polledAgain = store.MergeAndSave("doc", [updated with { Name = "弧 在 Part.1 内" }]);
        Assert.Equal("overall_length", polledAgain.Single(x => x.Id == "a").Name);
        var spa = new MeasurementSnapshot("spa", "Edge.1", "length", 25, "mm", "Line", "Edge.1", [], [], null,
            "CATIA SPA Measurable from current selection; SI converted to displayed unit");
        store.MergeAndSave("doc", [spa]);
        store.RenameAndSave("doc", "spa", "slot_length");
        var spaPolledAgain = store.MergeAndSave("doc", [spa]);
        Assert.Equal("slot_length", spaPolledAgain.Single(x => x.Id == "spa").Name);
        var afterDelete = store.DeleteAndSave("doc", "a");
        Assert.False(afterDelete.Any(x => x.Id == "a"));
        Assert.False(store.MergeAndSave("doc", [updated]).Any(x => x.Id == "a"));
        store.ResetSuppression("doc");
        Assert.True(store.MergeAndSave("doc", [updated]).Any(x => x.Id == "a"));
        Assert.Equal(0, store.ClearAndSave("doc").Count);
        Assert.Equal(0, store.MergeAndSave("doc", [updated, second, spa]).Count);
        return Task.CompletedTask;
    }
    finally { CleanupDirectory(directory); }
}

static Task TestReconstructionReadiness()
{
    var measurements = new[]
    {
        new MeasurementSnapshot("r1", "弧 在 Part.1 内", "radius", 5, "mm", "Arc", "Arc.1", [0, 0, 0], [], null, "test"),
        new MeasurementSnapshot("r2", "弧 在 Part.1 内", "radius", 5, "mm", "Arc", "Arc.2", [40, 40, 0], [], null, "test"),
        new MeasurementSnapshot("l1", "直线 在 Part.1 内", "length", 75, "mm", "Line", "Line.1", [], [], null, "test"),
        new MeasurementSnapshot("s1", "Edge.1", "length", 25, "mm", "Line", "Edge.1", [], [], null,
            "CATIA SPA Measurable from current selection; SI converted to displayed unit")
    };
    var readiness = ReconstructionReadinessAnalyzer.Analyze(measurements);
    Assert.False(readiness.ReadyToModel);
    Assert.True(readiness.Checklist.Any(x => x.Key == "overall_length" && x.Status == "missing"));
    Assert.True(readiness.Issues.Any(x => x.Contains("语义")));
    Assert.True(readiness.DerivedFacts.Any(x => x.Contains("ΔX=40")));
    return Task.CompletedTask;
}

static Task TestGuidedMeasurementMatch()
{
    var old = new MeasurementSnapshot("old", "old", "length", 10, "mm", "Line", "Line.1", [], [], null, "test");
    var radius = new MeasurementSnapshot("radius", "arc", "radius", 5, "mm", "Arc", "Arc.1", [], [], null, "test");
    var length = new MeasurementSnapshot("length", "line", "minimumDistance", 75, "mm", "Line", "Line.2", [], [], null, "test");
    var match = GuidedMeasurementMatcher.FindNewCompatibleRecord(
        new HashSet<string>([old.Id], StringComparer.OrdinalIgnoreCase), [old, radius, length], "length");
    Assert.Equal("length", match.Record?.Id);
    Assert.False(match.Ambiguous);
    Assert.Equal("diameter", GuidedMeasurementMatcher.InferExpectedQuantity("hole_01_diameter"));
    Assert.False(GuidedMeasurementMatcher.ShouldUseMeasureBetween("overall_width"));
    Assert.True(GuidedMeasurementMatcher.ShouldUseMeasureBetween("hole_01_x"));
    Assert.True(GuidedMeasurementMatcher.ShouldUseMeasureBetween("hole_pitch"));
    Assert.False(GuidedMeasurementMatcher.ShouldUseMeasureBetween("hole_diameter"));

    var ambiguous = GuidedMeasurementMatcher.FindNewCompatibleRecord(
        new HashSet<string>(), [old, length], "length");
    Assert.True(ambiguous.Ambiguous);
    Assert.True(ambiguous.Record is null);
    var pair = length with { Id = "pair", Source = "Face.1 ↔ Face.2" };
    var pairMatch = GuidedMeasurementMatcher.FindNewCompatibleRecord(
        new HashSet<string>(), [length, pair], "length", requirePairSource: true);
    Assert.Equal("pair", pairMatch.Record?.Id);
    var preferred = GuidedMeasurementMatcher.FindPreferredRecord(
        new HashSet<string>(), [length], [old, length], "length");
    Assert.Equal("length", preferred.Record?.Id);
    Assert.False(preferred.Ambiguous);
    Assert.True(GuidedMeasurementMatcher.HasCompatibleSingleSource([length], "length"));
    Assert.True(GuidedMeasurementMatcher.HasCompatiblePairSource([pair], "length"));
    Assert.Equal(GuidedMeasurementTypes.All.Count, GuidedMeasurementTypes.All.Select(x => x.Key).Distinct().Count());
    Assert.True(GuidedMeasurementTypes.All.Any(x => x.Key == "overall_height" && x.Label == "总高"));
    Assert.True(GuidedMeasurementTypes.All.Any(x => x.Key == "hole_diameter" && x.Label == "孔径"));
    Assert.True(GuidedMeasurementTypes.All.Any(x => x.Key == "hole_pitch" && x.Label == "孔距"));
    Assert.True(GuidedMeasurementTypes.Find("feature_radii") is null);
    var summary = MeasurementSummaryFormatter.Format([
        length with { Id = "semantic", Name = "overall_width", Quantity = "distance" },
        radius
    ]);
    Assert.True(summary.Contains("总宽：75 mm"));
    Assert.True(summary.Contains("未分类半径：5 mm"));
    var sideBySide = SideBySideLayout.Calculate(new(0, 0, 1920, 1080), 420, 320, 700, 720);
    Assert.Equal(1500, sideBySide.Catia.Width);
    Assert.Equal(420, sideBySide.Panel.Width);
    Assert.Equal(sideBySide.Catia.Right, sideBySide.Panel.Left);
    return Task.CompletedTask;
}

static Task TestCompleteReconstructionReadiness()
{
    var measurements = new[]
    {
        new MeasurementSnapshot("l1", "overall_length", "length", 75, "mm", "Line", "Line.1", [], [], null, "test"),
        new MeasurementSnapshot("l2", "overall_width", "length", 40, "mm", "Line", "Line.2", [], [], null, "test"),
        new MeasurementSnapshot("l3", "overall_height", "length", 3, "mm", "Line", "Line.3", [], [], null, "test"),
        new MeasurementSnapshot("r1", "corner_radius", "radius", 5, "mm", "Arc", "Arc.1", [0, 0, 0], [], null, "test"),
        new MeasurementSnapshot("extra", "Edge.99", "length", 999, "mm", "Line", "Edge.99", [], [], null,
            "CATIA SPA Measurable from current selection; SI converted to displayed unit") { Approximate = true }
    };
    var readiness = ReconstructionReadinessAnalyzer.Analyze(measurements);
    Assert.True(readiness.ReadyToModel);
    Assert.Equal("partial", readiness.Checklist.Single(x => x.Key == "feature_strategy").Status);
    return Task.CompletedTask;
}

static Task TestDiff()
{
    var before = Context(Document(saved: true), [Selection("Pad.1")], "A");
    var afterDocument = Document(saved: false) with
    {
        Tree = [new("PartBody", "Body", [new("Pad.1", "Shape", [])])]
    };
    var after = Context(afterDocument, [Selection("Pad.2")], "B");
    var diff = ContextDiffer.Compare(before, after);
    Assert.True(diff.SavedStateChanged);
    Assert.True(diff.SelectionChanged);
    Assert.Equal(1, diff.TreeNodeDelta);
    return Task.CompletedTask;
}

static Task TestPreconditions()
{
    var write = TestData.Proposal(OperationKind.Write, TestData.ValidCode);
    var generated = Context(Document(saved: true), [], "same");
    var unsaved = Context(Document(saved: false), [], "same");
    var result = ExecutionPreconditions.Evaluate(write, generated, unsaved);
    Assert.False(result.Allowed);
    Assert.True(result.Reasons.Any(x => x.Contains("未保存")));

    var read = TestData.Proposal(OperationKind.Read, TestData.ValidCode);
    Assert.True(ExecutionPreconditions.Evaluate(read, generated, generated).Allowed);
    Assert.False(ExecutionPreconditions.Evaluate(read, generated, generated with { Fingerprint = "changed" }).Allowed);
    return Task.CompletedTask;
}

static Task TestNewPartPreconditions()
{
    var proposal = TestData.Proposal(OperationKind.Write, TestData.NewPartCode) with
    {
        TargetDocumentMode = TargetDocumentMode.NewPart
    };
    var reference = Context(Document(saved: false) with { ReadOnly = true }, [], "same");
    var result = ExecutionPreconditions.Evaluate(proposal, reference, reference);
    Assert.True(result.Allowed);
    return Task.CompletedTask;
}

static Task TestSafetyBlocks()
{
    var validator = new MacroSafetyValidator();
    var report = validator.Validate(TestData.Proposal(OperationKind.Read,
        "Function AIEntry(contextJson)\nSet x = CreateObject(\"WScript.Shell\")\nEnd Function"));
    Assert.True(report.IsBlocked);
    Assert.True(report.Findings.Any(x => x.Rule == "external-process"));
    var lifecycle = validator.Validate(TestData.Proposal(OperationKind.Write,
        "Function AIEntry(contextJson)\ndoc.SaveAs \"x.CATPart\"\nEnd Function"));
    Assert.True(lifecycle.Findings.Any(x => x.Rule == "catia-lifecycle" && x.IsBlocking));
    return Task.CompletedTask;
}

static Task TestSafetyAccepts()
{
    var report = new MacroSafetyValidator().Validate(TestData.Proposal(OperationKind.Read, TestData.ValidCode));
    Assert.False(report.IsBlocked);
    return Task.CompletedTask;
}

static Task TestNewPartSafety()
{
    var validator = new MacroSafetyValidator();
    var newPart = TestData.Proposal(OperationKind.Write, TestData.NewPartCode) with
    {
        TargetDocumentMode = TargetDocumentMode.NewPart
    };
    Assert.False(validator.Validate(newPart).IsBlocked);
    var missingCreate = TestData.Proposal(OperationKind.Write, TestData.ValidCode) with
    {
        TargetDocumentMode = TargetDocumentMode.NewPart
    };
    Assert.True(validator.Validate(missingCreate).Findings.Any(x => x.Rule == "missing-new-part" && x.IsBlocking));
    return Task.CompletedTask;
}

static Task TestNewPartIsolation()
{
    var validator = new MacroSafetyValidator();
    const string unsafeCode = """
        Function AIEntry(contextJson)
          Set sourceDoc = CATIA.ActiveDocument
          Set newDoc = CATIA.Documents.Add("Part")
          AIEntry = "ok"
        End Function
        """;
    var proposal = TestData.Proposal(OperationKind.Write, unsafeCode) with
    {
        TargetDocumentMode = TargetDocumentMode.NewPart
    };
    var report = validator.Validate(proposal);
    Assert.True(report.IsBlocked);
    Assert.True(report.Findings.Any(x => x.Rule == "reference-document-write-risk"));
    return Task.CompletedTask;
}

static Task TestSafetyAcceptsClarification()
{
    var proposal = new MacroProposal(
        "请使用 CATIA 原生测量。",
        true,
        new("CATScript", "AIEntry", ""),
        OperationKind.Read,
        [],
        ["CGR 不包含参数化特征。"],
        []);
    var report = new MacroSafetyValidator().Validate(proposal);
    Assert.False(report.IsBlocked);
    Assert.False(report.Findings.Any(x => x.Rule == "missing-entry"));
    return Task.CompletedTask;
}

static Task TestCodexProtocol()
{
    var started = CodexJsonlParser.Parse("{\"type\":\"thread.started\",\"thread_id\":\"abc\"}");
    Assert.Equal("abc", started.ThreadId);
    var proposalJson = JsonSerializer.Serialize(new
    {
        assistantMessage = "ready",
        needsClarification = false,
        macro = new { language = "CATScript", entryPoint = "AIEntry", code = TestData.ValidCode },
        operationKind = "read",
        expectedEffects = Array.Empty<string>(),
        riskFlags = Array.Empty<string>(),
        verificationHints = new[] { "check" }
    });
    var line = JsonSerializer.Serialize(new { type = "item.completed", item = new { type = "agent_message", text = proposalJson } });
    var parsed = CodexJsonlParser.Parse(line);
    var proposal = CodexJsonlParser.ParseProposal(parsed.AgentMessage!);
    Assert.Equal(OperationKind.Read, proposal.OperationKind);
    Assert.Equal("AIEntry", proposal.Macro.EntryPoint);
    return Task.CompletedTask;
}

static async Task TestMacroExecutor()
{
    var context = Context(Document(saved: true), [], "same");
    var catia = new FakeCatia(context)
    {
        ExecuteResult = "{\"success\":true,\"summary\":\"ok\",\"warnings\":\"one warning\",\"diagnostics\":\"\"}"
    };
    var directory = Path.Combine(Path.GetTempPath(), "catia-ai-panel-test-" + Guid.NewGuid().ToString("N"));
    try
    {
        var executor = new MacroExecutor(catia, new MacroSafetyValidator(), new CatiaContextSerializer());
        var result = await executor.ExecuteAsync(new(directory, TestData.Proposal(OperationKind.Read, TestData.ValidCode), context, TimeSpan.FromSeconds(1)));
        Assert.True(result.Success);
        Assert.Equal("ok", result.Summary);
        Assert.Equal(1, result.Runtime!.Warnings.Count);
        Assert.Equal(1, catia.ExecuteCount);
        Assert.True(catia.LastMacroBytes is { Length: > 0 });
        Assert.False(catia.LastMacroBytes!.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.True(CatScriptFileEncoding.Get().GetString(catia.LastMacroBytes!).Contains("AIEntry", StringComparison.Ordinal));
    }
    finally { CleanupDirectory(directory); }
}

static async Task TestMacroExecutorRejectsUnstructuredResult()
{
    var context = Context(Document(saved: true), [], "same");
    var catia = new FakeCatia(context) { ExecuteResult = "done" };
    var directory = Path.Combine(Path.GetTempPath(), "catia-ai-panel-test-" + Guid.NewGuid().ToString("N"));
    try
    {
        var executor = new MacroExecutor(catia, new MacroSafetyValidator(), new CatiaContextSerializer());
        var result = await executor.ExecuteAsync(new(directory,
            TestData.Proposal(OperationKind.Read, TestData.ValidCode), context, TimeSpan.FromSeconds(1)));
        Assert.False(result.Success);
        Assert.True(result.Error?.Contains("原始返回", StringComparison.Ordinal) == true);
    }
    finally { CleanupDirectory(directory); }
}

static async Task TestWorkflowCorrection()
{
    var context = Context(Document(saved: true), [], "same");
    var catia = new FakeCatia(context) { ExecuteResult = "{\"success\":false,\"summary\":\"bad\",\"warnings\":[],\"diagnostics\":\"x\"}" };
    var codex = new FakeCodex();
    var directory = Path.Combine(Path.GetTempPath(), "catia-ai-panel-workflow-" + Guid.NewGuid().ToString("N"));
    try
    {
        var serializer = new CatiaContextSerializer();
        var workflow = new AiWorkflowController(
            catia,
            codex,
            new MacroExecutor(catia, new MacroSafetyValidator(), serializer),
            new PromptBuilder(serializer),
            new SessionStore(directory));
        await workflow.SubmitAsync("inspect");
        var outcome = await workflow.ExecuteConfirmedAsync();
        Assert.False(outcome.Execution.Success);
        Assert.True(outcome.RevisedProposal is not null);
        Assert.Equal(1, codex.ResumeCount);
    }
    finally { CleanupDirectory(directory); }
}

static async Task TestLiveCodexConversation()
{
    var directory = Path.Combine(Path.GetTempPath(), "catia-ai-panel-codex-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var state = new ConversationState { LocalSessionId = "smoke", SessionDirectory = directory };
    using var codex = new CodexConversation();
    const string prompt = """
    返回符合 Schema 的 CATIA 提案。assistantMessage 必须为“中文输入通过”，needsClarification 为 false，
    operationKind 为 read，不声明影响或风险。生成 CATScript Function AIEntry(contextJson)，返回
    {"success":true,"summary":"尺寸读取测试","warnings":[],"diagnostics":""}。不要使用工具。
    """;
    try
    {
        var first = await codex.StartAsync(state, prompt);
        Assert.True(!string.IsNullOrWhiteSpace(state.CodexThreadId));
        Assert.Equal("AIEntry", first.Macro.EntryPoint);
        Assert.Equal("中文输入通过", first.AssistantMessage);
        var second = await codex.ResumeAsync(state, "返回相同的安全宏，但把 assistantMessage 改为“中文续接通过”。不要使用工具。");
        Assert.Equal("中文续接通过", second.AssistantMessage);
    }
    finally { CleanupDirectory(directory); }
}

static async Task TestLiveCatiaCodexWorkflow()
{
    var directory = Path.Combine(Path.GetTempPath(), "catia-ai-panel-e2e-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        await using var catia = new CatiaSession();
        var context = await catia.CaptureAsync();
        if (context.Document is null) throw new InvalidOperationException(context.ConnectionMessage);
        var serializer = new CatiaContextSerializer();
        var prompt = new PromptBuilder(serializer).BuildInitial("我想测量零件的尺寸", context);
        var state = new ConversationState { LocalSessionId = "e2e", SessionDirectory = directory, Context = context };
        using var codex = new CodexConversation();
        var proposal = await codex.StartAsync(state, prompt);
        Assert.Equal("AIEntry", proposal.Macro.EntryPoint);
        if (context.Selection.Any(x => x.Name.EndsWith(".cgr", StringComparison.OrdinalIgnoreCase)))
        {
            Assert.True(proposal.NeedsClarification);
            Assert.False(proposal.HasExecutableCode);
            Assert.True(proposal.AssistantMessage.Contains("测量", StringComparison.Ordinal));
        }
        Console.WriteLine($"E2E proposal: clarification={proposal.NeedsClarification}, operation={proposal.OperationKind}");
    }
    finally { CleanupDirectory(directory); }
}

static async Task TestLiveReconstructionPlanning()
{
    var directory = Path.Combine(Path.GetTempPath(), "catia-ai-panel-reconstruction-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        await using var catia = new CatiaSession();
        var context = await catia.CaptureAsync();
        if (context.Document is null) throw new InvalidOperationException(context.ConnectionMessage);
        if (context.Measurements.Count == 0) throw new InvalidOperationException("Live measurement ledger is empty.");
        var serializer = new CatiaContextSerializer();
        var state = new ConversationState { LocalSessionId = "reconstruction", SessionDirectory = directory, Context = context };
        using var codex = new CodexConversation();
        var proposal = await codex.StartAsync(state, new PromptBuilder(serializer).BuildReconstruction(context));
        Console.WriteLine($"Reconstruction: ready={proposal.ReconstructionPlan.ReadyToModel}, " +
                          $"missing={proposal.ReconstructionPlan.MissingMeasurements.Count}, " +
                          $"assumptions={proposal.ReconstructionPlan.Assumptions.Count}, " +
                          $"code={proposal.Macro.Code.Length}, target={proposal.TargetDocumentMode}");
        if (proposal.ReconstructionPlan.ReadyToModel)
        {
            Assert.True(proposal.HasExecutableCode);
            Assert.Equal(TargetDocumentMode.NewPart, proposal.TargetDocumentMode);
            var safety = new MacroSafetyValidator().Validate(proposal);
            foreach (var finding in safety.Findings)
                Console.WriteLine($"  SAFETY {(finding.IsBlocking ? "BLOCK" : "WARN")} {finding.Rule}: {finding.Message}");
            Assert.False(safety.IsBlocked);
        }
        else
        {
            Assert.True(proposal.NeedsClarification);
            Assert.False(proposal.HasExecutableCode);
            Assert.True(proposal.ReconstructionPlan.MissingMeasurements.Count > 0);
        }
    }
    finally { CleanupDirectory(directory); }
}

static async Task TestSyntheticModelProposal()
{
    var directory = Path.Combine(Path.GetTempPath(), "catia-ai-panel-synthetic-model-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var measurements = new MeasurementSnapshot[]
        {
            new("l1", "overall_length", "length", 75, "mm", "Line", "local X extent", [], [1, 0, 0], null, "confirmed test datum"),
            new("l2", "overall_width", "length", 40, "mm", "Line", "local Y extent", [], [0, 1, 0], null, "confirmed test datum"),
            new("l3", "overall_height", "length", 3, "mm", "Line", "Pad thickness along +Z", [], [0, 0, 1], null, "confirmed test datum"),
            new("r1", "corner_radius", "radius", 5, "mm", "Arc", "all four profile corners", [32.5, 15, 0], [], null, "confirmed test datum")
        };
        var baseContext = Context(Document(saved: true), [], "synthetic");
        var context = baseContext with
        {
            Measurements = measurements,
            ReconstructionReadiness = ReconstructionReadinessAnalyzer.Analyze(measurements),
            Fingerprint = ContextFingerprint.Compute(baseContext.Document, [], measurements)
        };
        Assert.True(context.ReconstructionReadiness.ReadyToModel);
        const string intent = "Create a rounded rectangular plate centered on the local XY origin: 75 mm in X, 40 mm in Y, four equal R5 corners, then Pad 3 mm along +Z. No holes or additional features.";
        var serializer = new CatiaContextSerializer();
        var state = new ConversationState { LocalSessionId = "synthetic-model", SessionDirectory = directory, Context = context };
        using var codex = new CodexConversation();
        var proposal = await codex.StartAsync(state, new PromptBuilder(serializer).BuildReconstruction(context, intent));
        Assert.False(proposal.NeedsClarification);
        Assert.True(proposal.ReconstructionPlan.ReadyToModel);
        Assert.Equal(OperationKind.Write, proposal.OperationKind);
        Assert.Equal(TargetDocumentMode.NewPart, proposal.TargetDocumentMode);
        Assert.True(proposal.HasExecutableCode);
        var safety = new MacroSafetyValidator().Validate(proposal);
        if (safety.IsBlocked)
            throw new InvalidOperationException(string.Join(" | ", safety.Findings.Where(x => x.IsBlocking).Select(x => $"{x.Rule}: {x.Message}")));
        Assert.True(proposal.Macro.Code.Contains("Parameters", StringComparison.OrdinalIgnoreCase));
        _ = CatScriptFileEncoding.Get().GetBytes(proposal.Macro.Code);
        Console.WriteLine($"Synthetic proposal: code={proposal.Macro.Code.Length} chars, effects={proposal.ExpectedEffects.Count}, safety={safety.Level}");
    }
    finally { CleanupDirectory(directory); }
}

static DocumentSnapshot Document(bool saved) =>
    new("Demo.CATPart", "C:\\Models\\Demo.CATPart", "CATPart", "PrtCfg", saved, false, "PartBody",
        [new("PartBody", "Body", [])]);

static SelectedObjectSnapshot Selection(string name) => new("Pad", name, ["Demo", "PartBody"], [], name, "");

static CatiaContextSnapshot Context(DocumentSnapshot document, IReadOnlyList<SelectedObjectSnapshot> selection, string fingerprint) =>
    new(DateTimeOffset.Now, CatiaConnectionState.Connected, "connected", document, selection, false, fingerprint);

static void CleanupDirectory(string root)
{
    if (!Directory.Exists(root)) return;
    foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) File.Delete(file);
    foreach (var directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
        Directory.Delete(directory);
    Directory.Delete(root);
}

sealed class FakeCatia(CatiaContextSnapshot context) : ICatiaSession
{
    public object? ExecuteResult { get; init; }
    public int ExecuteCount { get; private set; }
    public byte[]? LastMacroBytes { get; private set; }
    public Task<CatiaContextSnapshot> CaptureAsync(CancellationToken cancellationToken = default) => Task.FromResult(context);
    public Task<ExecutionPreconditionResult> ValidateExecutionAsync(MacroProposal proposal, CatiaContextSnapshot generatedFrom, CancellationToken cancellationToken = default) =>
        Task.FromResult(ExecutionPreconditions.Evaluate(proposal, generatedFrom, context));
    public Task<object?> ExecuteScriptAsync(string libraryDirectory, string programName, string entryPoint, object[] parameters, CancellationToken cancellationToken = default)
    {
        ExecuteCount++;
        LastMacroBytes = File.ReadAllBytes(Path.Combine(libraryDirectory, programName));
        return Task.FromResult(ExecuteResult);
    }
    public Task StartNativeMeasureAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StartNativeMeasureBetweenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<MeasurementCaptureResult> CaptureMeasurementsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new MeasurementCaptureResult(context.Measurements, []));
    public Task<IReadOnlyList<MeasurementSnapshot>> RenameMeasurementAsync(
        string measurementId, string semanticName, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MeasurementSnapshot>>(context.Measurements.Select(x =>
            x.Id == measurementId ? x with { Name = semanticName } : x).ToArray());
    public Task<IReadOnlyList<MeasurementSnapshot>> DeleteMeasurementAsync(
        string measurementId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MeasurementSnapshot>>(context.Measurements.Where(x => x.Id != measurementId).ToArray());
    public Task<IReadOnlyList<MeasurementSnapshot>> ClearMeasurementsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MeasurementSnapshot>>([]);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class FakeCodex : ICodexConversation
{
    public int ResumeCount { get; private set; }
    public Task<MacroProposal> StartAsync(ConversationState state, string prompt, IProgress<CodexStreamEvent>? progress = null, CancellationToken cancellationToken = default)
    {
        state.CodexThreadId = "thread-1";
        return Task.FromResult(TestData.Proposal(OperationKind.Read, TestData.ValidCode));
    }
    public Task<MacroProposal> ResumeAsync(ConversationState state, string prompt, IProgress<CodexStreamEvent>? progress = null, CancellationToken cancellationToken = default)
    {
        ResumeCount++;
        return Task.FromResult(TestData.Proposal(OperationKind.Read, TestData.ValidCode));
    }
    public void CancelActive() { }
}

static class Assert
{
    public static void True(bool value) { if (!value) throw new InvalidOperationException("Expected true."); }
    public static void False(bool value) { if (value) throw new InvalidOperationException("Expected false."); }
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
    }
    public static void NotEqual<T>(T expected, T actual)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Did not expect {actual}.");
    }
}

static class TestData
{
    public const string ValidCode = """""
    Function AIEntry(contextJson)
      AIEntry = "{""success"":true,""summary"":""ok"",""warnings"":[],""diagnostics"":""""}"
    End Function
    """"";

    public const string NewPartCode = """""
    Function AIEntry(contextJson)
      Set newDoc = CATIA.Documents.Add("Part")
      AIEntry = "{""success"":true,""summary"":""created"",""warnings"":[],""diagnostics"":""""}"
    End Function
    """"";

    public static MacroProposal Proposal(OperationKind kind, string code) =>
        new("proposal", false, new("CATScript", "AIEntry", code), kind, [], [], []);
}
