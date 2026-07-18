using System.Text.Json.Serialization;

namespace CatiaAiPanel.Core;

public enum CatiaConnectionState { Disconnected, Connected, Faulted }
public enum OperationKind { Read, Write }
public enum TargetDocumentMode { CurrentDocument, NewPart }
public enum RiskLevel { None, Warning, Blocked }

public sealed record TreeNodeSnapshot(
    string Name,
    string Kind,
    IReadOnlyList<TreeNodeSnapshot> Children);

public sealed record SelectedObjectSnapshot(
    string Type,
    string Name,
    IReadOnlyList<string> ParentPath,
    IReadOnlyList<double> Coordinates,
    string Reference,
    string LeafProduct);

public sealed record MeasurementSnapshot(
    string Id,
    string Name,
    string Quantity,
    double Value,
    string Unit,
    string GeometryType,
    string Source,
    IReadOnlyList<double> Coordinates,
    IReadOnlyList<double> Direction,
    double? Accuracy,
    string Provenance)
{
    public string RawValue { get; init; } = "";
    public bool Approximate { get; init; }
}

public sealed record MeasurementCaptureResult(
    IReadOnlyList<MeasurementSnapshot> Records,
    IReadOnlyList<string> Warnings);

public sealed record MeasurementChecklistItem(
    string Key,
    string Description,
    string Quantity,
    string Reference,
    string Status,
    string Evidence);

public sealed record ReconstructionReadiness(
    bool ReadyToModel,
    int Score,
    string Strategy,
    IReadOnlyList<MeasurementChecklistItem> Checklist,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> DerivedFacts);

public sealed record ReconstructionPlan(
    bool ReadyToModel,
    string Strategy,
    IReadOnlyList<MeasurementChecklistItem> MissingMeasurements,
    IReadOnlyList<string> Assumptions);

public sealed record DocumentSnapshot(
    string Name,
    string FullName,
    string DocumentType,
    string Workbench,
    bool Saved,
    bool ReadOnly,
    string InWorkObject,
    IReadOnlyList<TreeNodeSnapshot> Tree);

public sealed record CatiaContextSnapshot(
    DateTimeOffset CapturedAt,
    CatiaConnectionState ConnectionState,
    string ConnectionMessage,
    DocumentSnapshot? Document,
    IReadOnlyList<SelectedObjectSnapshot> Selection,
    bool Truncated,
    string Fingerprint)
{
    public string CatiaVersion { get; init; } = "";
    public IReadOnlyList<MeasurementSnapshot> Measurements { get; init; } = [];
    public IReadOnlyList<MeasurementSnapshot> LiveNativeMeasurements { get; init; } = [];
    public IReadOnlyList<string> MeasurementWarnings { get; init; } = [];
    public ReconstructionReadiness ReconstructionReadiness { get; init; } =
        new(false, 0, "undetermined", [], ["尚未分析测量账本。"], []);

    public static CatiaContextSnapshot Disconnected(string message) =>
        new(DateTimeOffset.Now, CatiaConnectionState.Disconnected, message, null, [], false, "");
}

public sealed record MacroCode(
    string Language,
    string EntryPoint,
    string Code);

public sealed record MacroProposal(
    string AssistantMessage,
    bool NeedsClarification,
    MacroCode Macro,
    OperationKind OperationKind,
    IReadOnlyList<string> ExpectedEffects,
    IReadOnlyList<string> RiskFlags,
    IReadOnlyList<string> VerificationHints)
{
    public TargetDocumentMode TargetDocumentMode { get; init; } = TargetDocumentMode.CurrentDocument;
    public ReconstructionPlan ReconstructionPlan { get; init; } =
        new(false, "not-applicable", [], []);

    [JsonIgnore]
    public bool HasExecutableCode => !NeedsClarification && !string.IsNullOrWhiteSpace(Macro.Code);
}

public sealed record MacroSafetyFinding(string Rule, string Message, bool IsBlocking);

public sealed record MacroSafetyReport(
    RiskLevel Level,
    IReadOnlyList<MacroSafetyFinding> Findings)
{
    public bool IsBlocked => Level == RiskLevel.Blocked;
}

public sealed record ExecutionPreconditionResult(bool Allowed, IReadOnlyList<string> Reasons);

public sealed record MacroExecutionRequest(
    string SessionDirectory,
    MacroProposal Proposal,
    CatiaContextSnapshot GeneratedFrom,
    TimeSpan Timeout);

public sealed record DocumentStateDiff(
    bool DocumentChanged,
    bool SavedStateChanged,
    bool SelectionChanged,
    int TreeNodeDelta,
    IReadOnlyList<string> Summary);

public sealed record MacroRuntimePayload(
    bool Success,
    string Summary,
    IReadOnlyList<string> Warnings,
    string Diagnostics);

public sealed record MacroExecutionResult(
    bool Success,
    bool TimedOut,
    string Summary,
    string? Error,
    MacroRuntimePayload? Runtime,
    CatiaContextSnapshot Before,
    CatiaContextSnapshot? After,
    DocumentStateDiff? Diff,
    MacroSafetyReport Safety);

public sealed record CodexStreamEvent(string Type, string Message, string? ThreadId = null);

public sealed class ConversationState
{
    public required string LocalSessionId { get; init; }
    public required string SessionDirectory { get; init; }
    public string? CodexThreadId { get; set; }
    public string UserRequest { get; set; } = "";
    public int CorrectionCount { get; set; }
    public CatiaContextSnapshot? Context { get; set; }
    public MacroProposal? CurrentProposal { get; set; }
    public MacroExecutionResult? LastExecution { get; set; }
}

public sealed record WorkflowExecutionOutcome(
    MacroExecutionResult Execution,
    MacroProposal? RevisedProposal);
