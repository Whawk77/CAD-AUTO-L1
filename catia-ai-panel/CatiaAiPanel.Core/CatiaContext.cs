using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CatiaAiPanel.Core;

public sealed class CatiaContextSerializer : ICatiaContextSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string Serialize(CatiaContextSnapshot snapshot) => JsonSerializer.Serialize(snapshot, Options);
}

public static class ContextFingerprint
{
    public static string Compute(
        DocumentSnapshot? document,
        IReadOnlyList<SelectedObjectSnapshot> selection,
        IReadOnlyList<MeasurementSnapshot>? measurements = null)
    {
        var stable = new
        {
            document = document is null ? null : new
            {
                document.Name,
                document.FullName,
                document.DocumentType,
                document.ReadOnly
            },
            selection,
            measurements = (measurements ?? []).Select(x => new
            {
                x.Id,
                x.Name,
                x.Quantity,
                x.Value,
                x.Unit,
                x.Source,
                x.Coordinates,
                x.Approximate
            })
        };
        var json = JsonSerializer.Serialize(stable);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}

internal static class ComHelpers
{
    public static string Text(Func<object?> getter, int maxLength = 512)
    {
        try
        {
            var value = Convert.ToString(getter()) ?? "";
            return value.Length <= maxLength ? value : value[..maxLength] + "…";
        }
        catch { return ""; }
    }

    public static int Int(Func<object?> getter)
    {
        try { return Convert.ToInt32(getter()); }
        catch { return 0; }
    }

    public static bool Bool(Func<object?> getter, bool fallback = false)
    {
        try { return Convert.ToBoolean(getter()); }
        catch { return fallback; }
    }

    public static object? Object(Func<object?> getter)
    {
        try { return getter(); }
        catch { return null; }
    }

    public static void Release(object? value)
    {
        if (value is null || !Marshal.IsComObject(value)) return;
        try { Marshal.FinalReleaseComObject(value); }
        catch { }
    }
}

internal static class ComDispatch
{
    public static object? Get(object target, string property) =>
        target.GetType().InvokeMember(
            property,
            BindingFlags.GetProperty,
            null,
            target,
            null);

    public static object? Call(object target, string method, params object?[] arguments) =>
        target.GetType().InvokeMember(
            method,
            BindingFlags.InvokeMethod,
            null,
            target,
            arguments);
}

public static class ContextDiffer
{
    public static DocumentStateDiff Compare(CatiaContextSnapshot before, CatiaContextSnapshot after)
    {
        var documentChanged = before.Document?.FullName != after.Document?.FullName ||
                              before.Document?.DocumentType != after.Document?.DocumentType;
        var savedChanged = before.Document?.Saved != after.Document?.Saved;
        var selectionChanged = !SelectionKey(before.Selection).SequenceEqual(SelectionKey(after.Selection));
        var nodeDelta = CountNodes(after.Document?.Tree) - CountNodes(before.Document?.Tree);
        var summary = new List<string>();
        if (documentChanged) summary.Add("活动文档已变化");
        if (savedChanged) summary.Add($"保存状态：{before.Document?.Saved} → {after.Document?.Saved}");
        if (selectionChanged) summary.Add("当前选择已变化");
        if (nodeDelta != 0) summary.Add($"特征树节点变化：{nodeDelta:+#;-#;0}");
        if (summary.Count == 0) summary.Add("未检测到可序列化的状态变化");
        return new(documentChanged, savedChanged, selectionChanged, nodeDelta, summary);
    }

    private static IEnumerable<string> SelectionKey(IEnumerable<SelectedObjectSnapshot> items) =>
        items.Select(x => $"{x.Type}|{x.Name}|{x.Reference}");

    private static int CountNodes(IReadOnlyList<TreeNodeSnapshot>? nodes) =>
        nodes?.Sum(x => 1 + CountNodes(x.Children)) ?? 0;
}

public static class ExecutionPreconditions
{
    public static ExecutionPreconditionResult Evaluate(
        MacroProposal proposal,
        CatiaContextSnapshot generatedFrom,
        CatiaContextSnapshot current)
    {
        var reasons = new List<string>();
        if (current.ConnectionState != CatiaConnectionState.Connected || current.Document is null)
            reasons.Add("CATIA 未连接或没有活动文档。");
        if (generatedFrom.Document?.FullName != current.Document?.FullName)
            reasons.Add("活动文档与生成宏时不同。");
        if (!string.Equals(generatedFrom.Fingerprint, current.Fingerprint, StringComparison.Ordinal))
            reasons.Add("选择、文档或测量账本已变化，请重新生成宏。");
        if (proposal.OperationKind == OperationKind.Write &&
            proposal.TargetDocumentMode == TargetDocumentMode.CurrentDocument &&
            current.Document is not null)
        {
            if (current.Document.ReadOnly) reasons.Add("活动文档为只读。");
            if (!current.Document.Saved) reasons.Add("活动文档包含未保存修改，请先保存。");
        }
        return new(reasons.Count == 0, reasons);
    }
}
