namespace CatiaAiPanel.Core;

public sealed class AiWorkflowController(
    ICatiaSession catia,
    ICodexConversation codex,
    IMacroExecutor executor,
    PromptBuilder promptBuilder,
    SessionStore sessionStore)
{
    public ConversationState? Current { get; private set; }

    public MacroProposal? RestoreLatestPending()
    {
        Current = sessionStore.LoadLatestPending();
        if (Current?.CurrentProposal is not { } proposal) return null;
        proposal = MacroCompatibilityNormalizer.Normalize(proposal);
        Current.CurrentProposal = proposal;
        return proposal;
    }

    public async Task<MacroProposal> SubmitAsync(
        string userRequest,
        IProgress<CodexStreamEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userRequest)) throw new ArgumentException("请输入任务描述。", nameof(userRequest));
        var context = await catia.CaptureAsync(cancellationToken).ConfigureAwait(false);
        if (context.Document is null) throw new InvalidOperationException(context.ConnectionMessage);
        var state = sessionStore.Create();
        state.UserRequest = userRequest.Trim();
        state.Context = context;
        Current = state;
        var proposal = MacroCompatibilityNormalizer.Normalize(await codex.StartAsync(state, promptBuilder.BuildInitial(userRequest, context), progress, cancellationToken)
            .ConfigureAwait(false));
        state.CurrentProposal = proposal;
        await sessionStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        return proposal;
    }

    public async Task<MacroProposal> PrepareReconstructionAsync(
        string userIntent = "",
        IProgress<CodexStreamEvent>? progress = null,
        CancellationToken cancellationToken = default,
        bool allowDimensionInference = true)
    {
        var context = await catia.CaptureAsync(cancellationToken).ConfigureAwait(false);
        if (context.Document is null) throw new InvalidOperationException(context.ConnectionMessage);
        if (context.Measurements.Count == 0)
            throw new InvalidOperationException("测量账本为空。请先使用 CATIA 原生测量采集尺寸。");
        var state = sessionStore.Create();
        state.UserRequest = "根据测量账本分析并参数化重建新 CATPart" +
                            (string.IsNullOrWhiteSpace(userIntent) ? "" : $"：{userIntent.Trim()}");
        state.Context = context;
        Current = state;
        var proposal = MacroCompatibilityNormalizer.Normalize(await codex.StartAsync(
                state,
                promptBuilder.BuildReconstruction(context, userIntent, allowDimensionInference),
                progress,
                cancellationToken)
            .ConfigureAwait(false));
        state.CurrentProposal = proposal;
        await sessionStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        return proposal;
    }

    public async Task<WorkflowExecutionOutcome> ExecuteConfirmedAsync(
        IProgress<CodexStreamEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var state = Current ?? throw new InvalidOperationException("没有活动会话。");
        var proposal = state.CurrentProposal ?? throw new InvalidOperationException("没有待执行宏。");
        var generatedFrom = state.Context ?? throw new InvalidOperationException("缺少生成上下文。");
        var result = await executor.ExecuteAsync(
            new(state.SessionDirectory, proposal, generatedFrom, TimeSpan.FromSeconds(120)), cancellationToken)
            .ConfigureAwait(false);
        state.LastExecution = result;
        await sessionStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);

        MacroProposal? revision = null;
        if (!result.Success && !result.TimedOut && state.CorrectionCount < 3 && !string.IsNullOrWhiteSpace(state.CodexThreadId))
        {
            state.CorrectionCount++;
            var context = result.After ?? await catia.CaptureAsync(cancellationToken).ConfigureAwait(false);
            revision = MacroCompatibilityNormalizer.Normalize(await codex.ResumeAsync(state, promptBuilder.BuildCorrection(state, "", context), progress, cancellationToken)
                .ConfigureAwait(false));
            state.Context = context;
            state.CurrentProposal = revision;
            await sessionStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        }
        return new(result, revision);
    }

    public async Task<MacroProposal> ContinueCorrectionAsync(
        string feedback,
        IProgress<CodexStreamEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var state = Current ?? throw new InvalidOperationException("没有活动会话。");
        if (state.CorrectionCount >= 3) throw new InvalidOperationException("当前请求已达到 3 次修正上限，请创建新请求。");
        var context = await catia.CaptureAsync(cancellationToken).ConfigureAwait(false);
        state.CorrectionCount++;
        var proposal = MacroCompatibilityNormalizer.Normalize(await codex.ResumeAsync(state, promptBuilder.BuildCorrection(state, feedback, context), progress, cancellationToken)
            .ConfigureAwait(false));
        state.Context = context;
        state.CurrentProposal = proposal;
        await sessionStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        return proposal;
    }

    public void CancelCodex() => codex.CancelActive();
}
