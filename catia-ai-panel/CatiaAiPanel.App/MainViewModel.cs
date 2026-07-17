using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using CatiaAiPanel.Core;

namespace CatiaAiPanel.App;

public sealed record ChatMessage(string Role, string Text, DateTimeOffset At);

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ICatiaSession _catia;
    private readonly AiWorkflowController _workflow;
    private readonly IMacroSafetyValidator _safety;
    private readonly DispatcherTimer _contextTimer;
    private CancellationTokenSource? _operationCancellation;
    private bool _polling;
    private bool _busy;
    private string _userPrompt = "";
    private string _feedback = "";
    private string _status = "正在连接 CATIA…";
    private string _contextSummary = "尚未读取上下文";
    private string _macroCode = "";
    private string _proposalSummary = "尚无宏提案";
    private string _riskSummary = "";
    private string _effectsSummary = "";
    private string _verificationSummary = "";
    private MacroProposal? _proposal;
    private string _lastMeasurementSignature = "";
    private bool _measurementSnapshotInitialized;
    private MeasurementSnapshot? _selectedMeasurement;
    private string _measurementSemanticName = "";
    private string _readinessSummary = "尚未分析建模准备度";
    private string _measuredDimensionsSummary = "已测量尺寸（0 条记录）\n尚无已分类尺寸；请先选择尺寸类型并完成原生测量。";
    private string _modelingBlockerSummary = "尚未运行自动建模分析。";
    private bool _allowDimensionInference = true;
    private string _guidedMeasurementName = "";
    private string _guidedMeasurementStatus = "选择或输入建模尺寸名称；CATIA 中选一项可自动读 Measurable，选两项可自动读最小距离。";
    private HashSet<string>? _guidedMeasurementBaseline;
    private string? _guidedMeasurementDocument;
    private string? _pendingGuidedMeasurementName;
    private string? _pendingGuidedQuantity;
    private bool _pendingGuidedRequiresPair;
    private bool _guidedNativeCommandStarted;
    private CatiaContextSnapshot? _lastContext;
    private bool _proposalStale;
    private string _lastNativeFallbackKey = "";

    public MainViewModel(ICatiaSession catia, AiWorkflowController workflow, IMacroSafetyValidator safety)
    {
        _catia = catia;
        _workflow = workflow;
        _safety = safety;
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(UserPrompt));
        ContinueCommand = new AsyncRelayCommand(ContinueAsync, () => !IsBusy && _proposal is not null);
        NativeMeasureCommand = new AsyncRelayCommand(StartNativeMeasureAsync, () => !IsBusy);
        NativeMeasureBetweenCommand = new AsyncRelayCommand(StartNativeMeasureBetweenAsync, () => !IsBusy);
        SyncMeasurementsCommand = new AsyncRelayCommand(SyncMeasurementsAsync, () => !IsBusy);
        PrepareReconstructionCommand = new AsyncRelayCommand(PrepareReconstructionAsync, () => !IsBusy && Measurements.Count > 0);
        RenameMeasurementCommand = new AsyncRelayCommand(RenameMeasurementAsync,
            () => !IsBusy && SelectedMeasurement is not null && !string.IsNullOrWhiteSpace(MeasurementSemanticName));
        StartGuidedMeasurementCommand = new AsyncRelayCommand(StartGuidedMeasurementAsync,
            () => !IsBusy && !string.IsNullOrWhiteSpace(GuidedMeasurementName) && _lastContext?.Document is not null);
        ClearGuidedMeasurementCommand = new RelayCommand(ClearCurrentGuidedMeasurement,
            () => !IsBusy && (_pendingGuidedMeasurementName is not null || !string.IsNullOrWhiteSpace(GuidedMeasurementName)));
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        RejectCommand = new RelayCommand(Reject, () => _proposal is not null && !IsBusy);
        if (_workflow.RestoreLatestPending() is { } pendingProposal)
        {
            ApplyProposal(pendingProposal);
            Messages.Add(new("系统", "已恢复最近一次尚未执行的宏提案，请重新预览并确认。", DateTimeOffset.Now));
            Status = "已恢复待确认的建模宏";
        }
        _contextTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _contextTimer.Tick += async (_, _) => await PollContextAsync();
        _contextTimer.Start();
        _ = PollContextAsync();
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<MeasurementSnapshot> Measurements { get; } = [];
    public IReadOnlyList<GuidedMeasurementType> GuidedMeasurementTypes { get; } =
    [
        new("", "请选择尺寸语义", "选择后等待下一条对应的原生测量结果", false),
        .. CatiaAiPanel.Core.GuidedMeasurementTypes.All
    ];
    public ICommand SubmitCommand { get; }
    public ICommand ContinueCommand { get; }
    public ICommand NativeMeasureCommand { get; }
    public ICommand NativeMeasureBetweenCommand { get; }
    public ICommand SyncMeasurementsCommand { get; }
    public ICommand PrepareReconstructionCommand { get; }
    public ICommand RenameMeasurementCommand { get; }
    public ICommand StartGuidedMeasurementCommand { get; }
    public ICommand ClearGuidedMeasurementCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RejectCommand { get; }

    public string UserPrompt { get => _userPrompt; set { Set(ref _userPrompt, value); RefreshCommands(); } }
    public string Feedback { get => _feedback; set => Set(ref _feedback, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string ContextSummary { get => _contextSummary; private set => Set(ref _contextSummary, value); }
    public string MacroCode { get => _macroCode; private set => Set(ref _macroCode, value); }
    public string ProposalSummary { get => _proposalSummary; private set => Set(ref _proposalSummary, value); }
    public string RiskSummary { get => _riskSummary; private set => Set(ref _riskSummary, value); }
    public string EffectsSummary { get => _effectsSummary; private set => Set(ref _effectsSummary, value); }
    public string VerificationSummary { get => _verificationSummary; private set => Set(ref _verificationSummary, value); }
    public string ReadinessSummary { get => _readinessSummary; private set => Set(ref _readinessSummary, value); }
    public string MeasuredDimensionsSummary { get => _measuredDimensionsSummary; private set => Set(ref _measuredDimensionsSummary, value); }
    public string ModelingBlockerSummary { get => _modelingBlockerSummary; private set => Set(ref _modelingBlockerSummary, value); }
    public bool AllowDimensionInference { get => _allowDimensionInference; set => Set(ref _allowDimensionInference, value); }
    public string GuidedMeasurementName
    {
        get => _guidedMeasurementName;
        set
        {
            if (Set(ref _guidedMeasurementName, value) && !string.IsNullOrWhiteSpace(value))
                ArmGuidedMeasurement(value, announce: true);
            RefreshCommands();
        }
    }
    public string GuidedMeasurementStatus { get => _guidedMeasurementStatus; private set => Set(ref _guidedMeasurementStatus, value); }
    public string MeasurementSemanticName
    {
        get => _measurementSemanticName;
        set { Set(ref _measurementSemanticName, value); RefreshCommands(); }
    }
    public MeasurementSnapshot? SelectedMeasurement
    {
        get => _selectedMeasurement;
        set
        {
            if (Set(ref _selectedMeasurement, value))
                MeasurementSemanticName = value?.Name ?? "";
            RefreshCommands();
        }
    }
    public bool IsBusy { get => _busy; private set { Set(ref _busy, value); RefreshCommands(); OnPropertyChanged(nameof(CanExecute)); } }
    public bool CanExecute => !IsBusy && !_proposalStale && _proposal is { HasExecutableCode: true } && !_safety.Validate(_proposal).IsBlocked;
    public MacroProposal? CurrentProposal => _proposal;

    public async Task ExecuteConfirmedAsync()
    {
        if (!CanExecute) return;
        await RunOperationAsync(async (progress, token) =>
        {
            Status = "正在 CATIA 中执行宏…";
            var outcome = await _workflow.ExecuteConfirmedAsync(progress, token);
            var result = outcome.Execution;
            var detail = new StringBuilder(result.Summary);
            if (!string.IsNullOrWhiteSpace(result.Error)) detail.AppendLine().Append(result.Error);
            if (result.Diff is not null) detail.AppendLine().Append(string.Join("；", result.Diff.Summary));
            Messages.Add(new("执行结果", detail.ToString(), DateTimeOffset.Now));
            Status = result.TimedOut ? "宏执行等待超时" : result.Success ? "宏执行完成" : "宏执行失败";
            if (outcome.RevisedProposal is not null)
            {
                ApplyProposal(outcome.RevisedProposal);
                Messages.Add(new("AI 修正版", outcome.RevisedProposal.AssistantMessage, DateTimeOffset.Now));
                Status = "已生成修正版，等待再次确认";
            }
        });
    }

    public async Task DeleteSelectedMeasurementAsync()
    {
        var selected = SelectedMeasurement;
        if (selected is null || IsBusy) return;
        await RunOperationAsync(async (_, token) =>
        {
            var updated = await _catia.DeleteMeasurementAsync(selected.Id, token);
            SelectedMeasurement = null;
            ApplyMeasurementDisplay(updated);
            Messages.Add(new("测量账本", $"已删除：{selected.Name} = {selected.Value:G10} {selected.Unit}", DateTimeOffset.Now));
            Status = "已删除选中的测量尺寸。";
        });
    }

    public async Task ClearMeasurementNotebookAsync()
    {
        if (IsBusy) return;
        await RunOperationAsync(async (_, token) =>
        {
            var cleared = await _catia.ClearMeasurementsAsync(token);
            SelectedMeasurement = null;
            ClearGuidedMeasurement("当前文档的测量账本已清空；请选择新的尺寸类型开始测量。");
            ApplyMeasurementDisplay(cleared);
            Messages.Add(new("测量账本", "已清空当前文档的全部测量尺寸。", DateTimeOffset.Now));
            Status = "测量账本已清空。";
        });
    }

    private async Task SubmitAsync()
    {
        var request = UserPrompt.Trim();
        Messages.Add(new("你", request, DateTimeOffset.Now));
        UserPrompt = "";
        if (RedirectAffirmativeToConfirmation(request)) return;
        await RunOperationAsync(async (progress, token) =>
        {
            Status = "Codex 正在生成 CATScript…";
            var proposal = await _workflow.SubmitAsync(request, progress, token);
            ApplyProposal(proposal);
            Messages.Add(new("AI", proposal.AssistantMessage, DateTimeOffset.Now));
            Status = proposal.NeedsClarification ? "需要补充信息" : "宏已生成，等待预览确认";
        });
    }

    private async Task ContinueAsync()
    {
        var text = Feedback.Trim();
        if (!string.IsNullOrWhiteSpace(text)) Messages.Add(new("你的反馈", text, DateTimeOffset.Now));
        Feedback = "";
        if (RedirectAffirmativeToConfirmation(text)) return;
        await RunOperationAsync(async (progress, token) =>
        {
            Status = "Codex 正在修正宏…";
            var proposal = await _workflow.ContinueCorrectionAsync(text, progress, token);
            ApplyProposal(proposal);
            Messages.Add(new("AI 修正版", proposal.AssistantMessage, DateTimeOffset.Now));
            Status = "已生成修正版，等待再次确认";
        });
    }

    private bool RedirectAffirmativeToConfirmation(string text)
    {
        if (_proposal is null || !IsAffirmative(text)) return false;
        if (CanExecute)
        {
            Status = "宏已准备好，请点击“确认执行”";
            Messages.Add(new("系统", "“发送”和“继续修正”不会执行宏。请先查看宏预览与风险，然后点击“确认执行”。", DateTimeOffset.Now));
        }
        else
        {
            var reason = GetExecutionBlockReason();
            Status = reason;
            Messages.Add(new("系统", reason, DateTimeOffset.Now));
        }
        return true;
    }

    private string GetExecutionBlockReason()
    {
        if (IsBusy) return "Codex 正在运行，请等待完成后再确认执行。";
        if (_proposalStale) return "测量账本已变化，请重新点击“分析/自动建模”。";
        if (_proposal is null) return "当前没有待执行的宏提案。";
        if (!_proposal.HasExecutableCode) return "当前提案没有可执行宏，请先补充信息或重新分析。";
        var blocking = _safety.Validate(_proposal).Findings.FirstOrDefault(x => x.IsBlocking);
        return blocking is null ? "宏尚未满足执行条件。" : "宏被安全规则阻止：" + blocking.Message;
    }

    private static bool IsAffirmative(string text)
    {
        var normalized = text.Trim().TrimEnd('。', '！', '!', '.', '，', ',').ToLowerInvariant();
        return normalized is "可以" or "确认" or "同意" or "执行" or "继续执行" or "开始执行" or "ok" or "yes";
    }

    private async Task StartNativeMeasureAsync()
    {
        await RunOperationAsync(async (_, token) =>
        {
            await _catia.StartNativeMeasureAsync(token);
            Status = "已启动 CATIA 原生测量，请在 CATIA 中选择测量对象。";
            Messages.Add(new("系统", "已打开 CATIA Measure Item。测量结果会自动连续写入账本；“同步测量记录”可用于立即核对。", DateTimeOffset.Now));
        });
    }

    private async Task StartNativeMeasureBetweenAsync()
    {
        await RunOperationAsync(async (_, token) =>
        {
            await _catia.StartNativeMeasureBetweenAsync(token);
            Status = "已启动 CATIA Measure Between。";
            Messages.Add(new("系统", "请选择两个对象。结果会自动连续写入账本；建议勾选 Keep Measure 作为第二条采集路径。", DateTimeOffset.Now));
        });
    }

    private async Task SyncMeasurementsAsync()
    {
        await RunOperationAsync(async (_, token) =>
        {
            Status = "正在同步 CATIA 测量…";
            var result = await _catia.CaptureMeasurementsAsync(token);
            var details = result.Records.Count == 0
                ? "没有同步到可用测量记录。"
                : $"已保存 {result.Records.Count} 条建模测量记录。\n" +
                  string.Join("\n", result.Records.Take(8).Select(x =>
                      $"{x.Name}.{x.Quantity} = {x.Value:G10} {x.Unit}" +
                      (x.Coordinates.Count == 3 ? $" @ ({string.Join(", ", x.Coordinates.Select(v => v.ToString("G10")))}) mm" : "")));
            if (result.Records.Count > 8) details += $"\n…另有 {result.Records.Count - 8} 条";
            if (result.Warnings.Count > 0) details += "\n" + string.Join("\n", result.Warnings.Select(x => $"⚠ {x}"));
            Messages.Add(new("测量账本", details, DateTimeOffset.Now));
            Status = result.Records.Count == 0 ? "未发现可记录的测量" : $"已同步 {result.Records.Count} 条测量参数";
            await PollContextAsync();
        });
    }

    private async Task RenameMeasurementAsync()
    {
        var selected = SelectedMeasurement;
        if (selected is null) return;
        var semanticName = MeasurementSemanticName.Trim();
        await RunOperationAsync(async (_, token) =>
        {
            var updated = await _catia.RenameMeasurementAsync(selected.Id, semanticName, token);
            UpdateMeasurementRows(updated);
            SelectedMeasurement = Measurements.FirstOrDefault(x => x.Id == selected.Id);
            Status = $"已将尺寸命名为 {semanticName}";
            Messages.Add(new("测量语义", $"{selected.Value:G10} {selected.Unit} → {semanticName}", DateTimeOffset.Now));
        });
    }

    private async Task StartGuidedMeasurementAsync()
    {
        var name = GuidedMeasurementName.Trim();
        if (!ArmGuidedMeasurement(name, announce: false)) return;
        var measurementType = CatiaAiPanel.Core.GuidedMeasurementTypes.Find(name);
        var useMeasureBetween = _pendingGuidedRequiresPair;
        var displayName = measurementType?.Label ?? name;
        _guidedNativeCommandStarted = true;
        GuidedMeasurementStatus = useMeasureBetween
            ? $"两对象距离：{displayName}。{measurementType?.Hint ?? "请依次选择两个对象"}。"
            : $"单对象测量：{displayName}。{measurementType?.Hint ?? "请选择一个对象并等待结果"}。";

        await RunOperationAsync(async (_, token) =>
        {
            if (useMeasureBetween)
                await _catia.StartNativeMeasureBetweenAsync(token);
            else
                await _catia.StartNativeMeasureAsync(token);
            Status = $"请在 CATIA 中完成 {displayName} 的原生测量，结果将自动命名入账。";
            Messages.Add(new("引导测量", useMeasureBetween
                ? $"{displayName} 使用两对象距离：请依次选择两个对象。"
                : $"{displayName} 使用单对象测量：请选择一个对象并等待测量结果。", DateTimeOffset.Now));
        });
    }

    private bool ArmGuidedMeasurement(string name, bool announce)
    {
        var cleanName = name.Trim();
        var context = _lastContext;
        if (context?.Document is null || string.IsNullOrWhiteSpace(cleanName)) return false;
        var measurementType = CatiaAiPanel.Core.GuidedMeasurementTypes.Find(cleanName);
        _guidedMeasurementBaseline = context.Measurements.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _guidedMeasurementDocument = DocumentIdentity(context);
        _pendingGuidedMeasurementName = cleanName;
        _pendingGuidedQuantity = GuidedMeasurementMatcher.InferExpectedQuantity(cleanName);
        _pendingGuidedRequiresPair = measurementType?.RequiresTwoObjects ?? GuidedMeasurementMatcher.ShouldUseMeasureBetween(cleanName);
        _guidedNativeCommandStarted = false;
        if (announce)
        {
            var displayName = measurementType?.Label ?? cleanName;
            GuidedMeasurementStatus = _pendingGuidedRequiresPair
                ? $"已选择 {displayName}：等待下一条两对象距离结果。"
                : $"已选择 {displayName}：等待下一条单对象测量结果。";
        }
        return true;
    }

    private void ClearCurrentGuidedMeasurement()
    {
        var clearedName = _pendingGuidedMeasurementName ?? GuidedMeasurementName;
        ClearGuidedMeasurement("已清空当前测量；已经确认写入的测量账本保持不变。");
        _lastNativeFallbackKey = "";
        if (!string.IsNullOrWhiteSpace(clearedName))
            Messages.Add(new("引导测量", $"已取消并清空当前测量：{clearedName}。", DateTimeOffset.Now));
        RefreshCommands();
    }

    private async Task PrepareReconstructionAsync()
    {
        var intent = UserPrompt.Trim();
        if (!string.IsNullOrWhiteSpace(intent))
        {
            Messages.Add(new("建模意图", intent, DateTimeOffset.Now));
            UserPrompt = "";
        }
        await RunOperationAsync(async (progress, token) =>
        {
            Status = "Codex 正在检查尺寸完整性并规划参数化建模…";
            var proposal = await _workflow.PrepareReconstructionAsync(intent, progress, token, AllowDimensionInference);
            ApplyProposal(proposal);
            var canModel = proposal.ReconstructionPlan.ReadyToModel && proposal.HasExecutableCode;
            Messages.Add(new("建模助手", canModel
                ? proposal.AssistantMessage
                : "尚未执行 CATIA 建模。\n" + FormatModelingBlockers(proposal), DateTimeOffset.Now));
            Status = canModel
                ? proposal.ReconstructionPlan.Assumptions.Count > 0
                    ? $"近似建模宏已生成：自动补齐 {proposal.ReconstructionPlan.Assumptions.Count} 项，等待预览确认"
                    : "建模宏已生成，等待预览确认"
                : $"尚不能自动建模：还缺 {proposal.ReconstructionPlan.MissingMeasurements.Count} 项信息（未执行 CATIA）";
        });
    }

    private async Task RunOperationAsync(Func<IProgress<CodexStreamEvent>, CancellationToken, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        _operationCancellation = new CancellationTokenSource();
        var progress = new Progress<CodexStreamEvent>(x =>
        {
            if (x.Type == "turn.started") Status = "Codex 正在分析…";
            else if (x.Type is "error" or "turn.failed") Status = x.Message;
            else if (x.Type == "stderr" && !string.IsNullOrWhiteSpace(x.Message))
                Status = "Codex 诊断：" + x.Message;
        });
        try { await action(progress, _operationCancellation.Token); }
        catch (OperationCanceledException) { Status = "操作已取消"; }
        catch (Exception ex)
        {
            Status = "操作失败";
            Messages.Add(new("错误", ex.Message, DateTimeOffset.Now));
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            IsBusy = false;
        }
    }

    private void ApplyProposal(MacroProposal proposal)
    {
        _proposal = proposal;
        _proposalStale = false;
        if (_workflow.Current?.Context is { } generatedContext)
        {
            _lastMeasurementSignature = MeasurementSignature(generatedContext.Measurements);
            _measurementSnapshotInitialized = true;
        }
        MacroCode = proposal.Macro.Code;
        var missing = proposal.ReconstructionPlan.MissingMeasurements;
        var assumptions = proposal.ReconstructionPlan.Assumptions;
        ModelingBlockerSummary = FormatModelingBlockers(proposal);
        ProposalSummary = missing.Count == 0
            ? proposal.AssistantMessage + (assumptions.Count == 0 ? "" : "\n\n自动补齐/建模假设：\n" +
              string.Join("\n", assumptions.Select(x => "• " + x)))
            : proposal.AssistantMessage + "\n\n待测量/确认：\n" +
              string.Join("\n", missing.Select(x => $"• {x.Description}｜{x.Quantity}｜基准：{x.Reference}"));
        EffectsSummary = proposal.ExpectedEffects.Count == 0 ? "未声明影响" : string.Join("\n• ", proposal.ExpectedEffects.Prepend(""));
        VerificationSummary = proposal.VerificationHints.Count == 0 ? "未提供验证建议" : string.Join("\n• ", proposal.VerificationHints.Prepend(""));
        var report = _safety.Validate(proposal);
        RiskSummary = report.Findings.Count == 0
            ? "未发现静态风险"
            : string.Join("\n", report.Findings.Select(x => $"{(x.IsBlocking ? "⛔" : "⚠")} {x.Message}"));
        OnPropertyChanged(nameof(CurrentProposal));
        OnPropertyChanged(nameof(CanExecute));
        RefreshCommands();
        SuggestFromProposal(proposal);
    }

    private static string FormatModelingBlockers(MacroProposal proposal)
    {
        if (proposal.ReconstructionPlan.ReadyToModel && proposal.HasExecutableCode)
        {
            var assumptions = proposal.ReconstructionPlan.Assumptions;
            return assumptions.Count == 0
                ? "✓ 建模分析通过：已生成宏，等待预览与人工确认。"
                : $"✓ 已生成近似建模宏：自动补齐 {assumptions.Count} 项。\n" +
                  string.Join("\n", assumptions.Take(5).Select(x => "• " + x)) +
                  (assumptions.Count > 5 ? $"\n• 另有 {assumptions.Count - 5} 项，请查看宏预览" : "") +
                  "\n所有补齐值将在执行前等待人工确认。";
        }

        var missing = proposal.ReconstructionPlan.MissingMeasurements;
        if (missing.Count == 0)
            return "尚不能自动建模（未执行 CATIA）。Codex 未生成可执行宏，请查看宏预览中的说明。";

        var lines = missing.Take(6)
            .Select(x => $"• {x.Description}｜需要：{x.Quantity}｜基准：{x.Reference}");
        var remainder = missing.Count > 6 ? $"\n• 另有 {missing.Count - 6} 项，请查看宏预览" : "";
        return $"尚不能自动建模（未执行 CATIA）\n还缺 {missing.Count} 项建模信息：\n" +
               string.Join("\n", lines) + remainder + "\n补测/确认后再次点击“分析/自动建模”。";
    }

    private void Reject()
    {
        if (_proposal is not null) Messages.Add(new("系统", "已拒绝当前宏；没有执行任何 CATIA 操作。", DateTimeOffset.Now));
        _proposal = null;
        _proposalStale = false;
        MacroCode = "";
        ProposalSummary = "当前宏已拒绝";
        RiskSummary = EffectsSummary = VerificationSummary = "";
        OnPropertyChanged(nameof(CurrentProposal));
        OnPropertyChanged(nameof(CanExecute));
        RefreshCommands();
    }

    private void Cancel()
    {
        _operationCancellation?.Cancel();
        _workflow.CancelCodex();
    }

    private async Task PollContextAsync()
    {
        if (_polling || IsBusy) return;
        _polling = true;
        try
        {
            var snapshot = await _catia.CaptureAsync();
            _lastContext = snapshot;
            var records = await TryBindGuidedMeasurementAsync(snapshot);
            var readiness = ReferenceEquals(records, snapshot.Measurements)
                ? snapshot.ReconstructionReadiness
                : ReconstructionReadinessAnalyzer.Analyze(records);
            ApplyMeasurementDisplay(records, readiness);
            SuggestNextGuidedMeasurement(readiness);
            if (_pendingGuidedMeasurementName is null && snapshot.Selection.Count > 0 &&
                snapshot.MeasurementWarnings.FirstOrDefault() is { } warning)
            {
                GuidedMeasurementStatus = warning + " 点击“开始并自动记录”进入原生测量。";
            }
            var measurementSignature = MeasurementSignature(records);
            if (_measurementSnapshotInitialized &&
                !string.Equals(_lastMeasurementSignature, measurementSignature, StringComparison.Ordinal))
            {
                Messages.Add(new("测量账本", $"已自动更新，当前共 {records.Count} 条记录。", DateTimeOffset.Now));
                if (_proposal is not null)
                {
                    _proposalStale = true;
                    RiskSummary = "⛔ 测量账本已改变，当前宏已失效；请重新分析/生成。\n" + RiskSummary;
                    OnPropertyChanged(nameof(CanExecute));
                    RefreshCommands();
                }
            }
            _lastMeasurementSignature = measurementSignature;
            _measurementSnapshotInitialized = true;
            Status = snapshot.ConnectionMessage;
            ContextSummary = snapshot.Document is null
                ? snapshot.ConnectionMessage
                : $"{snapshot.CatiaVersion} · {snapshot.Document.DocumentType}\n{snapshot.Document.Name}\n" +
                  $"工作台：{snapshot.Document.Workbench}\n" +
                  $"选择：{snapshot.Selection.Count} 项 · 已保存：{snapshot.Document.Saved} · 只读：{snapshot.Document.ReadOnly}" +
                  $"\n测量账本：{snapshot.Measurements.Count} 条" +
                  (snapshot.Truncated ? "\n上下文已截断" : "");
            await TryStartNativeFallbackAsync(snapshot);
        }
        catch (Exception ex) { ContextSummary = ex.Message; }
        finally { _polling = false; }
    }

    private async Task TryStartNativeFallbackAsync(CatiaContextSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(GuidedMeasurementName) || _guidedNativeCommandStarted) return;
        if (snapshot.Selection.Count == 0)
        {
            _lastNativeFallbackKey = "";
            return;
        }
        if (!snapshot.MeasurementWarnings.Any(x =>
                x.Contains("原生 Measure", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("CGR", StringComparison.OrdinalIgnoreCase)))
            return;

        var key = GuidedMeasurementName + "|" + string.Join("|", snapshot.Selection.Select(x =>
            $"{x.Type}:{x.Name}:{x.Reference}:{x.LeafProduct}"));
        if (string.Equals(key, _lastNativeFallbackKey, StringComparison.Ordinal)) return;
        _lastNativeFallbackKey = key;
        GuidedMeasurementStatus = $"{snapshot.Selection[0].Name} 不支持 SPA，正在自动切换 CATIA 原生测量…";
        Messages.Add(new("自动回退", $"{snapshot.Selection[0].Name} 是 CGR/可视化对象；已为 {GuidedMeasurementName} 启动原生测量。", DateTimeOffset.Now));
        await StartGuidedMeasurementAsync();
    }

    private async Task<IReadOnlyList<MeasurementSnapshot>> TryBindGuidedMeasurementAsync(CatiaContextSnapshot snapshot)
    {
        if (_pendingGuidedMeasurementName is null || _pendingGuidedQuantity is null || _guidedMeasurementBaseline is null)
            return snapshot.Measurements;
        if (!string.Equals(_guidedMeasurementDocument, DocumentIdentity(snapshot), StringComparison.OrdinalIgnoreCase))
        {
            ClearGuidedMeasurement("活动文档已改变，本次引导测量已取消，避免尺寸绑定到错误零件。");
            return snapshot.Measurements;
        }

        var match = GuidedMeasurementMatcher.FindPreferredRecord(
            _guidedMeasurementBaseline,
            snapshot.LiveNativeMeasurements,
            snapshot.Measurements,
            _pendingGuidedQuantity,
            _pendingGuidedRequiresPair);
        if (match.Ambiguous)
        {
            GuidedMeasurementStatus = match.Message + " 请关闭多余测量结果后重试。";
            return snapshot.Measurements;
        }
        if (match.Record is null)
        {
            if (_pendingGuidedRequiresPair &&
                GuidedMeasurementMatcher.HasCompatibleSingleSource(snapshot.LiveNativeMeasurements, _pendingGuidedQuantity) &&
                !GuidedMeasurementMatcher.HasCompatiblePairSource(snapshot.LiveNativeMeasurements, _pendingGuidedQuantity))
            {
                GuidedMeasurementStatus = $"已读取第一个对象的 {_pendingGuidedQuantity} 结果；请在 CATIA 原生测量窗口继续选择第二个对象。";
            }
            return snapshot.Measurements;
        }

        var semanticName = _pendingGuidedMeasurementName;
        ClearGuidedMeasurement($"已自动记录 {semanticName} = {match.Record.Value:G10} {match.Record.Unit}");
        var updated = await _catia.RenameMeasurementAsync(match.Record.Id, semanticName);
        Messages.Add(new("自动测量记录", $"{semanticName} = {match.Record.Value:G10} {match.Record.Unit}", DateTimeOffset.Now));
        Status = $"已自动记录 {semanticName}";
        return updated;
    }

    private void SuggestNextGuidedMeasurement(ReconstructionReadiness readiness)
    {
        if (_pendingGuidedMeasurementName is not null || !string.IsNullOrWhiteSpace(GuidedMeasurementName)) return;
        var next = readiness.Checklist.FirstOrDefault(x =>
            x.Status == "missing" && CatiaAiPanel.Core.GuidedMeasurementTypes.Find(x.Key) is not null);
        if (next is not null)
        {
            GuidedMeasurementName = next.Key;
            return;
        }
        if (readiness.Checklist.Any(x => x.Status == "missing" && x.Key == "feature_radii"))
            GuidedMeasurementStatus = "基础外形尺寸已记录；请手动选择孔径或圆角半径，不能用内部检查项 feature_radii 代替。";
    }

    private void SuggestFromProposal(MacroProposal proposal)
    {
        if (_pendingGuidedMeasurementName is not null || !string.IsNullOrWhiteSpace(GuidedMeasurementName)) return;
        var next = proposal.ReconstructionPlan.MissingMeasurements.FirstOrDefault(x =>
            !x.Quantity.Equals("design-intent", StringComparison.OrdinalIgnoreCase) &&
            !x.Quantity.Equals("text", StringComparison.OrdinalIgnoreCase));
        if (next is null) return;
        if (CatiaAiPanel.Core.GuidedMeasurementTypes.Find(next.Key) is not null)
        {
            GuidedMeasurementName = next.Key;
            GuidedMeasurementStatus = $"下一项：{next.Description}；基准：{next.Reference}";
        }
        else
        {
            GuidedMeasurementName = "";
            GuidedMeasurementStatus = $"待测项“{next.Description}”需要你从下拉框手动选择具体尺寸语义。";
        }
    }

    private void ClearGuidedMeasurement(string status)
    {
        _pendingGuidedMeasurementName = null;
        _pendingGuidedQuantity = null;
        _guidedMeasurementBaseline = null;
        _guidedMeasurementDocument = null;
        _pendingGuidedRequiresPair = false;
        _guidedNativeCommandStarted = false;
        GuidedMeasurementName = "";
        GuidedMeasurementStatus = status;
    }

    private static string DocumentIdentity(CatiaContextSnapshot snapshot) =>
        snapshot.Document is null ? "" : $"{snapshot.Document.FullName}|{snapshot.Document.Name}";

    private void UpdateMeasurementRows(IReadOnlyList<MeasurementSnapshot> records)
    {
        var signature = string.Join("|", records.Select(x => $"{x.Id}:{x.Name}:{x.Value:R}:{x.Unit}"));
        var current = string.Join("|", Measurements.Select(x => $"{x.Id}:{x.Name}:{x.Value:R}:{x.Unit}"));
        if (signature == current) return;
        var selectedId = SelectedMeasurement?.Id;
        Measurements.Clear();
        foreach (var record in records) Measurements.Add(record);
        SelectedMeasurement = selectedId is null ? null : Measurements.FirstOrDefault(x => x.Id == selectedId);
        RefreshCommands();
    }

    private void ApplyMeasurementDisplay(
        IReadOnlyList<MeasurementSnapshot> records,
        ReconstructionReadiness? readiness = null)
    {
        UpdateMeasurementRows(records);
        var analyzed = readiness ?? ReconstructionReadinessAnalyzer.Analyze(records);
        ReadinessSummary = FormatReadiness(analyzed);
        MeasuredDimensionsSummary = MeasurementSummaryFormatter.Format(records);
    }

    private static string MeasurementSignature(IEnumerable<MeasurementSnapshot> records) =>
        string.Join("|", records.Select(x => $"{x.Id}:{x.Name}:{x.Quantity}:{x.Value:R}:{x.Unit}"));

    private static string FormatReadiness(ReconstructionReadiness readiness)
    {
        var pending = readiness.Checklist.Where(x => x.Status != "present").ToArray();
        var text = $"准备度 {readiness.Score}/100 · {(readiness.ReadyToModel ? "可生成模型" : "仍需测量/确认")}";
        if (pending.Length > 0)
            text += "\n" + string.Join("\n", pending.Select(x => $"• {x.Description}：{x.Evidence}"));
        if (readiness.DerivedFacts.Count > 0)
            text += "\n" + string.Join("\n", readiness.DerivedFacts.Select(x => $"✓ {x}"));
        return text;
    }

    private void RefreshCommands()
    {
        (SubmitCommand as IRefreshableCommand)?.RaiseCanExecuteChanged();
        (ContinueCommand as IRefreshableCommand)?.RaiseCanExecuteChanged();
        (NativeMeasureCommand as IRefreshableCommand)?.RaiseCanExecuteChanged();
        (NativeMeasureBetweenCommand as IRefreshableCommand)?.RaiseCanExecuteChanged();
        (SyncMeasurementsCommand as IRefreshableCommand)?.RaiseCanExecuteChanged();
        (PrepareReconstructionCommand as IRefreshableCommand)?.RaiseCanExecuteChanged();
        (RenameMeasurementCommand as IRefreshableCommand)?.RaiseCanExecuteChanged();
        (StartGuidedMeasurementCommand as IRefreshableCommand)?.RaiseCanExecuteChanged();
        (ClearGuidedMeasurementCommand as IRefreshableCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as IRefreshableCommand)?.RaiseCanExecuteChanged();
        (RejectCommand as IRefreshableCommand)?.RaiseCanExecuteChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

internal interface IRefreshableCommand : ICommand { void RaiseCanExecuteChanged(); }

internal sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : IRefreshableCommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => execute();
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : IRefreshableCommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public async void Execute(object? parameter) => await execute();
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
