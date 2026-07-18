using System.Text.RegularExpressions;

namespace CatiaAiPanel.Core;

public static partial class MacroCompatibilityNormalizer
{
    public static MacroProposal Normalize(MacroProposal proposal)
    {
        var code = proposal.Macro.Code;
        if (string.IsNullOrWhiteSpace(code)) return proposal;

        var changed = false;
        foreach (Match assignment in ReferenceAssignmentPattern().Matches(code))
        {
            var referenceName = assignment.Groups["reference"].Value;
            var geometryName = assignment.Groups["geometry"].Value;
            var monoConstraint = new Regex(
                $@"(?<prefix>\.AddMonoEltCst\s*\(\s*[^,\r\n]+,\s*){Regex.Escape(referenceName)}(?<suffix>\s*\))",
                RegexOptions.IgnoreCase);
            var updated = monoConstraint.Replace(code, $"${{prefix}}{geometryName}${{suffix}}");
            if (!string.Equals(updated, code, StringComparison.Ordinal))
            {
                code = updated;
                changed = true;
            }
        }

        if (!changed) return proposal;
        var warning = "CATIA V5 兼容修正：Sketcher.AddMonoEltCst 使用原始二维几何对象，而不是 Part.CreateReferenceFromObject 的 Reference。";
        var risks = proposal.RiskFlags.Contains(warning, StringComparer.Ordinal) ? proposal.RiskFlags : [.. proposal.RiskFlags, warning];
        return proposal with { Macro = proposal.Macro with { Code = code }, RiskFlags = risks };
    }

    [GeneratedRegex(@"\bSet\s+(?<reference>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*[A-Za-z_][A-Za-z0-9_]*\.CreateReferenceFromObject\s*\(\s*(?<geometry>[A-Za-z_][A-Za-z0-9_]*)\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ReferenceAssignmentPattern();
}
