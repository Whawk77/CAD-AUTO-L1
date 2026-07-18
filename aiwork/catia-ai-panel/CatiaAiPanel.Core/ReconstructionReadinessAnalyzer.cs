namespace CatiaAiPanel.Core;

public static class ReconstructionReadinessAnalyzer
{
    public static ReconstructionReadiness Analyze(IReadOnlyList<MeasurementSnapshot> measurements)
    {
        var checklist = new List<MeasurementChecklistItem>();
        var issues = new List<string>();
        var facts = new List<string>();
        var semanticMeasurements = measurements.Where(x => !IsGenericName(x)).ToArray();

        AddDimension(checklist, measurements, "overall_length", "零件总长", "length", "局部 X 基准");
        AddDimension(checklist, measurements, "overall_width", "零件总宽", "length", "局部 Y 基准");
        AddDimension(checklist, measurements, "overall_height", "零件总高/厚度", "length", "局部 Z 基准");

        var radii = semanticMeasurements.Where(x => x.Quantity is "radius" or "diameter").ToArray();
        checklist.Add(new(
            "feature_radii",
            "所有参与轮廓的圆角、圆弧或孔径",
            "radius/diameter",
            "对应特征中心与所在平面",
            radii.Length > 0 ? "present" : "missing",
            radii.Length > 0 ? $"已记录 {radii.Length} 条半径/直径。" : "尚无半径或直径记录。"));

        var located = semanticMeasurements.Count(x => x.Coordinates.Count >= 3);
        checklist.Add(new(
            "feature_locations",
            "关键特征相对零件局部基准的位置",
            "coordinates/distance",
            "datum_origin + X/Y/Z",
            located > 0 ? "partial" : "missing",
            located > 0 ? $"{located} 条记录带坐标，但当前为装配坐标，需要确认局部原点。" : "没有带坐标的特征记录。"));

        checklist.Add(new(
            "feature_strategy",
            "主体建模策略与特征顺序",
            "design-intent",
            "例如：截面拉伸 → 槽 → 孔 → 圆角",
            "partial",
            "CGR 不包含原始特征历史，需要用户或 AI 根据测量确认。"));

        var genericNames = measurements.Count(IsGenericName);
        if (genericNames > 0)
            issues.Add($"{genericNames} 条尺寸仍是原生对象名称，尚未标记为总长、总宽、孔径、槽深等建模语义。 ");
        if (semanticMeasurements.Any(x => x.Approximate))
            issues.Add("账本包含近似值（~ 或 ≈），不能直接作为最终约束。 ");
        if (located > 0)
            issues.Add("坐标来自装配全局坐标；必须确定零件局部原点和轴向后才能用于草图约束。 ");

        AddFacts(facts, measurements);
        var requiredReady = checklist.Where(x => x.Key is "overall_length" or "overall_width" or "overall_height")
            .All(x => x.Status == "present");
        // Design intent is supplied in the reconstruction request, not obtained from a
        // physical measurement.  Treat it as advisory here so a complete measurement
        // ledger is not permanently reported as incomplete solely for that reason.
        var blockingMeasurements = checklist.Where(x => x.Key != "feature_strategy");
        var ready = semanticMeasurements.Length > 0 && requiredReady &&
                    !semanticMeasurements.Any(x => x.Approximate) && blockingMeasurements.All(x => x.Status != "missing");
        var score = Math.Clamp(
            (semanticMeasurements.Length > 0 ? 15 : 0) +
            checklist.Count(x => x.Status == "present") * 15 +
            checklist.Count(x => x.Status == "partial") * 7 -
            Math.Min(10, genericNames) -
            (semanticMeasurements.Any(x => x.Approximate) ? 10 : 0),
            0,
            100);

        return new(ready, score, "parameterized-feature-reconstruction", checklist, issues, facts);
    }

    private static void AddDimension(
        List<MeasurementChecklistItem> checklist,
        IReadOnlyList<MeasurementSnapshot> measurements,
        string key,
        string description,
        string quantity,
        string reference)
    {
        var match = measurements.FirstOrDefault(x => MatchesSemanticName(x.Name, key));
        checklist.Add(new(
            key,
            description,
            quantity,
            reference,
            match is null ? "missing" : "present",
            match is null ? "尚未标记。" : $"{match.Name} = {match.Value:G10} {match.Unit}"));
    }

    private static bool MatchesSemanticName(string name, string key)
    {
        var normalized = name.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        if (normalized.Contains(key, StringComparison.Ordinal)) return true;
        return key switch
        {
            "overall_length" => normalized.Contains("总长") || normalized.Contains("外形长"),
            "overall_width" => normalized.Contains("总宽") || normalized.Contains("外形宽"),
            "overall_height" => normalized.Contains("总高") || normalized.Contains("厚度") || normalized.Contains("外形高"),
            _ => false
        };
    }

    private static bool IsGenericName(MeasurementSnapshot measurement)
    {
        var name = measurement.Name.Trim();
        if ((measurement.Provenance.StartsWith("CATIA SPA Measurable", StringComparison.OrdinalIgnoreCase) ||
             measurement.Provenance.StartsWith("CATIA native Measure dialog", StringComparison.OrdinalIgnoreCase)) &&
            name.Equals(measurement.Source.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;
        return name.StartsWith("弧 在", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("直线 在", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("面 在", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("NativeMeasure", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Distance.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Selection.", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddFacts(List<string> facts, IReadOnlyList<MeasurementSnapshot> measurements)
    {
        var lengths = measurements.Where(x => x.Quantity is "length" or "distance" or "minimumDistance")
            .Select(x => $"{x.Value:G10} {x.Unit}").Distinct().ToArray();
        var radii = measurements.Where(x => x.Quantity == "radius")
            .Select(x => $"{x.Value:G10} {x.Unit}").Distinct().ToArray();
        if (lengths.Length > 0) facts.Add($"不同长度值：{string.Join(", ", lengths)}");
        if (radii.Length > 0) facts.Add($"不同半径值：{string.Join(", ", radii)}");

        var points = measurements.Where(x => x.Coordinates.Count >= 3).Select(x => x.Coordinates).ToArray();
        if (points.Length == 0) return;
        var minX = points.Min(x => x[0]); var maxX = points.Max(x => x[0]);
        var minY = points.Min(x => x[1]); var maxY = points.Max(x => x[1]);
        var minZ = points.Min(x => x[2]); var maxZ = points.Max(x => x[2]);
        facts.Add($"已测中心点坐标跨度：ΔX={maxX - minX:G10}, ΔY={maxY - minY:G10}, ΔZ={maxZ - minZ:G10} mm（仍需确认局部基准）。");
    }
}
