using System.Diagnostics;
using System.Text;

namespace CatiaAiPanel.Core;

public sealed class CodexConversation : ICodexConversation, IDisposable
{
    private readonly object _gate = new();
    private Process? _activeProcess;

    public async Task<MacroProposal> StartAsync(
        ConversationState state,
        string prompt,
        IProgress<CodexStreamEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunAsync(state, prompt, resume: false, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (CodexSilentExitException)
        {
            state.CodexThreadId = null;
            progress?.Report(new("retry", "Codex 未返回消息，正在自动重试一次…"));
            return await RunAsync(state, prompt, resume: false, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<MacroProposal> ResumeAsync(
        ConversationState state,
        string prompt,
        IProgress<CodexStreamEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state.CodexThreadId))
            throw new InvalidOperationException("当前会话没有可续接的 Codex thread id。");
        return RunAsync(state, prompt, resume: true, progress, cancellationToken);
    }

    private async Task<MacroProposal> RunAsync(
        ConversationState state,
        string prompt,
        bool resume,
        IProgress<CodexStreamEvent>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(state.SessionDirectory);
        var schemaPath = Path.Combine(state.SessionDirectory, "macro-output.schema.json");
        if (!File.Exists(schemaPath))
            await File.WriteAllTextAsync(schemaPath, CodexOutputSchema.Json, new UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);

        var startInfo = BuildStartInfo(state, schemaPath, resume);
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        lock (_gate)
        {
            if (_activeProcess is { HasExited: false })
                throw new InvalidOperationException("已有 Codex 请求正在运行。");
            _activeProcess = process;
        }

        var stderr = new StringBuilder();
        var jsonlDiagnostics = new StringBuilder();
        string? finalAgentMessage = null;
        var eventLogPath = Path.Combine(state.SessionDirectory, $"codex-events-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}.jsonl");
        await using var eventLog = new StreamWriter(eventLogPath, append: false, new UTF8Encoding(false));
        try
        {
            if (!process.Start()) throw new InvalidOperationException("无法启动 codex 进程。");
            await process.StandardInput.WriteAsync(prompt.AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();

            var errorTask = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } errorLine)
                {
                    stderr.AppendLine(errorLine);
                    if (!CodexDiagnosticFilter.IsBenignStderr(errorLine))
                        progress?.Report(new("stderr", errorLine));
                }
            }, cancellationToken);

            while (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                await eventLog.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
                var parsed = CodexJsonlParser.Parse(line);
                if (!string.IsNullOrWhiteSpace(parsed.ThreadId)) state.CodexThreadId = parsed.ThreadId;
                if (!string.IsNullOrWhiteSpace(parsed.AgentMessage)) finalAgentMessage = parsed.AgentMessage;
                if (parsed.IsError) jsonlDiagnostics.AppendLine(parsed.Message);
                progress?.Report(new(parsed.Type, parsed.Message, parsed.ThreadId));
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await errorTask.ConfigureAwait(false);
            await eventLog.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                var detail = string.Join(Environment.NewLine,
                    new[] { stderr.ToString().Trim(), jsonlDiagnostics.ToString().Trim() }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                if (string.IsNullOrWhiteSpace(finalAgentMessage) && string.IsNullOrWhiteSpace(detail))
                    throw new CodexSilentExitException(process.ExitCode, eventLogPath);
                throw new InvalidOperationException($"Codex 退出码 {process.ExitCode}：{detail}\n诊断：{eventLogPath}");
            }
            if (string.IsNullOrWhiteSpace(finalAgentMessage))
                throw new CodexSilentExitException(process.ExitCode, eventLogPath);

            return CodexJsonlParser.ParseProposal(finalAgentMessage);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        finally
        {
            lock (_gate) if (ReferenceEquals(_activeProcess, process)) _activeProcess = null;
        }
    }

    private static ProcessStartInfo BuildStartInfo(ConversationState state, string schemaPath, bool resume)
    {
        var arguments = new List<string> { "exec" };
        if (resume)
        {
            arguments.Add("resume");
            arguments.Add(state.CodexThreadId!);
        }
        arguments.Add("--json");
        arguments.Add("--output-schema");
        arguments.Add(schemaPath);
        arguments.Add("--ignore-user-config");
        arguments.Add("--skip-git-repo-check");
        if (!resume)
        {
            arguments.Add("--sandbox");
            arguments.Add("read-only");
            arguments.Add("-C");
            arguments.Add(state.SessionDirectory);
        }
        arguments.Add("-");

        var command = ResolveCodexCommand();
        var info = new ProcessStartInfo(command.Path)
        {
            WorkingDirectory = state.SessionDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        if (command.IsCommandScript)
        {
            var npmEntry = Path.Combine(Path.GetDirectoryName(command.Path)!, "node_modules", "@openai", "codex", "bin", "codex.js");
            if (File.Exists(npmEntry))
            {
                info.FileName = ResolveNodeExecutable();
                info.ArgumentList.Add(npmEntry);
                foreach (var argument in arguments) info.ArgumentList.Add(argument);
            }
            else
            {
                info.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                info.Arguments = "/d /s /c \"\"" + command.Path + "\" " +
                                 string.Join(" ", arguments.Select(QuoteForCmd)) + "\"";
            }
        }
        else
        {
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
        }
        return info;
    }

    private static (string Path, bool IsCommandScript) ResolveCodexCommand()
    {
        var configured = Environment.GetEnvironmentVariable("CATIA_AI_CODEX_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return (configured, configured.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase));

        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var directory in pathEntries)
        {
            var cleanDirectory = directory.Trim('"');
            var executable = Path.Combine(cleanDirectory, "codex.exe");
            if (File.Exists(executable)) return (executable, false);
            var script = Path.Combine(cleanDirectory, "codex.cmd");
            if (File.Exists(script)) return (script, true);
        }
        throw new FileNotFoundException("找不到 Codex CLI。请安装 codex，或通过 CATIA_AI_CODEX_PATH 指定 codex.exe/codex.cmd。 ");
    }

    private static string QuoteForCmd(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";

    private static string ResolveNodeExecutable()
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), "node.exe");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Codex npm 包存在，但找不到 node.exe。");
    }

    public void CancelActive()
    {
        lock (_gate) if (_activeProcess is { HasExited: false } process) TryKill(process);
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { }
    }

    public void Dispose() => CancelActive();

    private sealed class CodexSilentExitException(int exitCode, string logPath)
        : InvalidOperationException($"Codex 静默退出（退出码 {exitCode}），且没有最终消息。诊断：{logPath}");
}
