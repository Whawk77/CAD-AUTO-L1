using System.Text.RegularExpressions;

namespace CatiaAiPanel.Core;

public sealed partial class MacroSafetyValidator : IMacroSafetyValidator
{
    private static readonly (string Rule, Regex Pattern, string Message)[] BlockingRules =
    [
        ("external-process", ProcessPattern(), "禁止启动外部进程或 Shell。"),
        ("network", NetworkPattern(), "禁止网络访问。"),
        ("registry", RegistryPattern(), "禁止访问注册表。"),
        ("filesystem", FileSystemPattern(), "禁止任意文件系统访问。"),
        ("catia-lifecycle", CatiaLifecyclePattern(), "禁止退出 CATIA、关闭文档或 SaveAs。"),
        ("nested-script", NestedScriptPattern(), "禁止从宏再次执行脚本或进程。"),
        ("unexpected-entry", UnexpectedEntryPattern(), "宏必须公开 Function AIEntry(contextJson)。")
    ];

    public MacroSafetyReport Validate(MacroProposal proposal)
    {
        var findings = new List<MacroSafetyFinding>();

        // A clarification response intentionally contains no executable macro.
        // Do not present the absence of AIEntry as a safety violation in the UI.
        if (proposal.NeedsClarification && string.IsNullOrWhiteSpace(proposal.Macro.Code))
        {
            findings.AddRange(proposal.RiskFlags.Select(x => new MacroSafetyFinding("model-risk", x, false)));
            return new(findings.Count > 0 ? RiskLevel.Warning : RiskLevel.None, findings);
        }

        if (!proposal.Macro.Language.Equals("CATScript", StringComparison.OrdinalIgnoreCase) &&
            !proposal.Macro.Language.Equals("VBScript", StringComparison.OrdinalIgnoreCase))
            findings.Add(new("language", "首版只允许 CATScript/VBScript。", true));

        if (!proposal.Macro.EntryPoint.Equals("AIEntry", StringComparison.OrdinalIgnoreCase))
            findings.Add(new("entry-point", "入口点必须为 AIEntry。", true));
        if (!AiEntryPattern().IsMatch(proposal.Macro.Code))
            findings.Add(new("missing-entry", "代码必须包含 Function AIEntry(contextJson)。", true));

        var createsPart = CreatePartPattern().IsMatch(proposal.Macro.Code);
        if (proposal.TargetDocumentMode == TargetDocumentMode.NewPart && !createsPart)
            findings.Add(new("missing-new-part", "新建 Part 提案必须调用 CATIA.Documents.Add(\"Part\")。", true));
        if (proposal.TargetDocumentMode == TargetDocumentMode.NewPart && proposal.OperationKind != OperationKind.Write)
            findings.Add(new("new-part-kind", "新建 CATPart 必须声明为 write 操作。", true));
        if (proposal.TargetDocumentMode == TargetDocumentMode.CurrentDocument && createsPart)
            findings.Add(new("unexpected-new-part", "提案声明操作当前文档，却尝试新建 CATPart。", true));
        if (proposal.TargetDocumentMode == TargetDocumentMode.NewPart && createsPart)
        {
            var createMatch = CreatePartPattern().Match(proposal.Macro.Code);
            var beforeCreate = proposal.Macro.Code[..createMatch.Index];
            if (ActiveDocumentPattern().IsMatch(beforeCreate) || DocumentsItemPattern().IsMatch(proposal.Macro.Code))
                findings.Add(new("reference-document-write-risk",
                    "新建 Part 宏不得在创建目标文档前取得 ActiveDocument，也不得通过 Documents.Item 访问参考文档。", true));
        }

        foreach (var rule in BlockingRules)
            if (rule.Pattern.IsMatch(proposal.Macro.Code))
                findings.Add(new(rule.Rule, rule.Message, true));

        if (proposal.OperationKind == OperationKind.Write)
            findings.Add(new("catia-write",
                proposal.TargetDocumentMode == TargetDocumentMode.NewPart
                    ? "该宏会新建并修改一个 CATPart；参考文档保持不变，仍需人工确认。"
                    : "该宏会修改活动 CATIA 文档；执行前必须保存并人工确认。",
                false));
        if (proposal.RiskFlags.Count > 0)
            findings.AddRange(proposal.RiskFlags.Select(x => new MacroSafetyFinding("model-risk", x, false)));

        var level = findings.Any(x => x.IsBlocking)
            ? RiskLevel.Blocked
            : findings.Count > 0 ? RiskLevel.Warning : RiskLevel.None;
        return new(level, findings);
    }

    [GeneratedRegex(@"\b(CreateObject|GetObject)\s*\(|\bShell\s*\(|\bExecuteProcessus\b|\bExecuteBackgroundProcessus\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProcessPattern();

    [GeneratedRegex(@"\b(XMLHTTP|WinHttp|InternetExplorer\.Application|ADODB\.Stream|MSXML2\.)\b|https?://", RegexOptions.IgnoreCase)]
    private static partial Regex NetworkPattern();

    [GeneratedRegex(@"\b(RegRead|RegWrite|RegDelete|StdRegProv)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RegistryPattern();

    [GeneratedRegex(@"\b(Scripting\.FileSystemObject|FileSystemObject|OpenTextFile|CreateTextFile|DeleteFile|DeleteFolder|CopyFile|MoveFile)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FileSystemPattern();

    [GeneratedRegex(@"\.\s*(Quit|Close|SaveAs)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CatiaLifecyclePattern();

    [GeneratedRegex(@"\b(ExecuteScript|Evaluate|StartCommand)\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex NestedScriptPattern();

    [GeneratedRegex(@"\b(Sub|Function)\s+CATMain\b", RegexOptions.IgnoreCase)]
    private static partial Regex UnexpectedEntryPattern();

    [GeneratedRegex(@"\bFunction\s+AIEntry\s*\(\s*contextJson\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex AiEntryPattern();

    [GeneratedRegex(@"\bDocuments\s*\.\s*Add\s*\(\s*[""']Part[""']\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex CreatePartPattern();

    [GeneratedRegex(@"\bActiveDocument\b", RegexOptions.IgnoreCase)]
    private static partial Regex ActiveDocumentPattern();

    [GeneratedRegex(@"\bDocuments\s*\.\s*Item\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex DocumentsItemPattern();
}
