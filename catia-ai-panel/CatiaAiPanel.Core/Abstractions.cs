namespace CatiaAiPanel.Core;

public interface ICatiaSession : IAsyncDisposable
{
    Task<CatiaContextSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
    Task<ExecutionPreconditionResult> ValidateExecutionAsync(
        MacroProposal proposal,
        CatiaContextSnapshot generatedFrom,
        CancellationToken cancellationToken = default);
    Task<object?> ExecuteScriptAsync(
        string libraryDirectory,
        string programName,
        string entryPoint,
        object[] parameters,
        CancellationToken cancellationToken = default);
    Task StartNativeMeasureAsync(CancellationToken cancellationToken = default);
    Task StartNativeMeasureBetweenAsync(CancellationToken cancellationToken = default);
    Task<MeasurementCaptureResult> CaptureMeasurementsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MeasurementSnapshot>> RenameMeasurementAsync(
        string measurementId,
        string semanticName,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MeasurementSnapshot>> DeleteMeasurementAsync(
        string measurementId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MeasurementSnapshot>> ClearMeasurementsAsync(
        CancellationToken cancellationToken = default);
}

public interface ICatiaContextSerializer
{
    string Serialize(CatiaContextSnapshot snapshot);
}

public interface ICodexConversation
{
    Task<MacroProposal> StartAsync(
        ConversationState state,
        string prompt,
        IProgress<CodexStreamEvent>? progress = null,
        CancellationToken cancellationToken = default);
    Task<MacroProposal> ResumeAsync(
        ConversationState state,
        string prompt,
        IProgress<CodexStreamEvent>? progress = null,
        CancellationToken cancellationToken = default);
    void CancelActive();
}

public interface IMacroSafetyValidator
{
    MacroSafetyReport Validate(MacroProposal proposal);
}

public interface IMacroExecutor
{
    Task<MacroExecutionResult> ExecuteAsync(
        MacroExecutionRequest request,
        CancellationToken cancellationToken = default);
}
