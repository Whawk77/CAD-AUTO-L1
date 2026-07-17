using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CatiaAiPanel.Core;

public sealed class MacroExecutor(
    ICatiaSession catia,
    IMacroSafetyValidator safetyValidator,
    ICatiaContextSerializer contextSerializer) : IMacroExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<MacroExecutionResult> ExecuteAsync(
        MacroExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var safety = safetyValidator.Validate(request.Proposal);
        if (safety.IsBlocked)
            return Failure("宏被安全策略阻止。", string.Join(" ", safety.Findings.Where(x => x.IsBlocking).Select(x => x.Message)), request.GeneratedFrom, safety);

        var preconditions = await catia.ValidateExecutionAsync(
            request.Proposal, request.GeneratedFrom, cancellationToken).ConfigureAwait(false);
        if (!preconditions.Allowed)
            return Failure("执行前条件不满足。", string.Join(" ", preconditions.Reasons), request.GeneratedFrom, safety);

        Directory.CreateDirectory(request.SessionDirectory);
        var programName = $"macro-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}.CATScript";
        var macroPath = Path.Combine(request.SessionDirectory, programName);
        try
        {
            await File.WriteAllTextAsync(
                    macroPath,
                    request.Proposal.Macro.Code,
                    CatScriptFileEncoding.Get(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (EncoderFallbackException ex)
        {
            return Failure("CATScript 文件编码失败。",
                $"宏包含当前 Windows ANSI 代码页无法表示的字符：{ex.Message}", request.GeneratedFrom, safety);
        }
        catch (Exception ex)
        {
            return Failure("无法创建 CATScript 文件。", ex.Message, request.GeneratedFrom, safety);
        }

        var before = await catia.CaptureAsync(cancellationToken).ConfigureAwait(false);
        object? rawResult;
        try
        {
            var execution = catia.ExecuteScriptAsync(
                request.SessionDirectory,
                programName,
                request.Proposal.Macro.EntryPoint,
                [contextSerializer.Serialize(before)],
                CancellationToken.None);
            rawResult = await execution.WaitAsync(request.Timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return new(false, true, "CATIA 宏执行超过等待时间。", "面板没有终止 CATIA；请等待宏自行返回。",
                null, before, null, null, safety);
        }
        catch (COMException ex)
        {
            var afterError = await SafeCaptureAsync().ConfigureAwait(false);
            return new(false, false, "CATIA 返回 COM 异常。", $"0x{ex.HResult:X8}: {ex.Message}", null,
                before, afterError, afterError is null ? null : ContextDiffer.Compare(before, afterError), safety);
        }
        catch (Exception ex)
        {
            var afterError = await SafeCaptureAsync().ConfigureAwait(false);
            return new(false, false, "宏执行失败。", ex.Message, null,
                before, afterError, afterError is null ? null : ContextDiffer.Compare(before, afterError), safety);
        }

        var after = await SafeCaptureAsync().ConfigureAwait(false);
        var runtime = ParseRuntime(rawResult);
        var success = runtime.Success;
        var summary = runtime.Summary;
        return new(success, false, summary, success ? null : runtime.Diagnostics, runtime,
            before, after, after is null ? null : ContextDiffer.Compare(before, after), safety);
    }

    private async Task<CatiaContextSnapshot?> SafeCaptureAsync()
    {
        try { return await catia.CaptureAsync().ConfigureAwait(false); }
        catch { return null; }
    }

    private static MacroRuntimePayload ParseRuntime(object? raw)
    {
        var text = Convert.ToString(raw);
        if (string.IsNullOrWhiteSpace(text))
            return new(false, "宏已执行，但没有返回结构化结果。", [], "AIEntry 必须返回结果 JSON。");
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var success = root.TryGetProperty("success", out var successElement) && successElement.ValueKind == JsonValueKind.True;
            var summary = root.TryGetProperty("summary", out var summaryElement) ? summaryElement.ToString() : "";
            var diagnostics = root.TryGetProperty("diagnostics", out var diagnosticsElement) ? diagnosticsElement.ToString() : "";
            IReadOnlyList<string> warnings = [];
            if (root.TryGetProperty("warnings", out var warningsElement))
            {
                warnings = warningsElement.ValueKind switch
                {
                    JsonValueKind.Array => warningsElement.EnumerateArray().Select(x => x.ToString()).ToArray(),
                    JsonValueKind.String when !string.IsNullOrWhiteSpace(warningsElement.GetString()) => [warningsElement.GetString()!],
                    _ => []
                };
            }
            return new(success, summary, warnings, diagnostics);
        }
        catch (Exception ex)
        {
            return new(false, "宏返回值不是有效的结果 JSON。", [], $"{ex.Message} 原始返回：{text}");
        }
    }

    private static MacroExecutionResult Failure(
        string summary,
        string error,
        CatiaContextSnapshot before,
        MacroSafetyReport safety) =>
        new(false, false, summary, error, null, before, null, null, safety);
}

internal static class CatScriptFileEncoding
{
    private static readonly Lazy<Encoding> Value = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var codePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
        if (codePage <= 0) codePage = 1252;
        return Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    });

    public static Encoding Get() => Value.Value;
}
