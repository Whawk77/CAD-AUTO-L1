namespace CatiaAiPanel.Core;

public static class CodexDiagnosticFilter
{
    public static bool IsBenignStderr(string line)
    {
        var isSkillsLoaderOrPluginManifest =
            line.Contains("codex_core_skills_loader", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("codex_core_skills::loader", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("codex_core_plugins::manifest", StringComparison.OrdinalIgnoreCase) ||
            (line.Contains("codex_core", StringComparison.OrdinalIgnoreCase) &&
             (line.Contains("skills", StringComparison.OrdinalIgnoreCase) ||
              line.Contains("plugins", StringComparison.OrdinalIgnoreCase)) &&
             (line.Contains("loader", StringComparison.OrdinalIgnoreCase) ||
              line.Contains("manifest", StringComparison.OrdinalIgnoreCase)));
        var isInvalidOptionalMarketplace =
            line.Contains("codex_core_plugins::marketplace", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("skipping marketplace that failed to load", StringComparison.OrdinalIgnoreCase);
        return (isSkillsLoaderOrPluginManifest &&
                line.Contains("ignoring interface.", StringComparison.OrdinalIgnoreCase)) ||
               isInvalidOptionalMarketplace;
    }
}
