namespace CatiaAiPanel.Core;

public sealed record GuidedMeasurementMatch(
    MeasurementSnapshot? Record,
    bool Ambiguous,
    string Message);

public static class GuidedMeasurementMatcher
{
    public static GuidedMeasurementMatch FindPreferredRecord(
        IReadOnlySet<string> baselineIds,
        IReadOnlyList<MeasurementSnapshot> liveNative,
        IReadOnlyList<MeasurementSnapshot> accumulated,
        string expectedQuantity,
        bool requirePairSource = false)
    {
        var live = FindNewCompatibleRecord(baselineIds, liveNative, expectedQuantity, requirePairSource);
        return live.Record is not null || live.Ambiguous
            ? live
            : FindNewCompatibleRecord(baselineIds, accumulated, expectedQuantity, requirePairSource);
    }

    public static GuidedMeasurementMatch FindNewCompatibleRecord(
        IReadOnlySet<string> baselineIds,
        IReadOnlyList<MeasurementSnapshot> current,
        string expectedQuantity,
        bool requirePairSource = false)
    {
        var candidates = current
            .Where(x => !baselineIds.Contains(x.Id) && IsCompatible(expectedQuantity, x.Quantity))
            .Where(x => !requirePairSource || x.Source.Contains("↔", StringComparison.Ordinal))
            .ToArray();
        return candidates.Length switch
        {
            0 => new(null, false, $"尚未检测到新的 {expectedQuantity} 测量结果。"),
            1 => new(candidates[0], false, "已检测到新的兼容测量结果。"),
            _ => new(null, true, $"一次检测到 {candidates.Length} 个兼容结果，无法安全确定应绑定哪一个。")
        };
    }

    public static string InferExpectedQuantity(string semanticName)
    {
        var value = semanticName.Trim().ToLowerInvariant();
        if (ContainsAny(value, "diameter", "直径", "孔径")) return "diameter";
        if (ContainsAny(value, "radius", "圆角", "半径")) return "radius";
        if (ContainsAny(value, "angle", "角度", "夹角")) return "angle";
        return "length";
    }

    public static bool ShouldUseMeasureBetween(string semanticName)
    {
        var value = semanticName.Trim().ToLowerInvariant();
        var knownType = GuidedMeasurementTypes.Find(value);
        if (knownType is not null) return knownType.RequiresTwoObjects;
        if (value.EndsWith("_x", StringComparison.Ordinal) ||
            value.EndsWith("_y", StringComparison.Ordinal) ||
            value.EndsWith("_z", StringComparison.Ordinal)) return true;
        return ContainsAny(value,
            "distance", "offset", "pitch", "spacing", "position",
            "距离", "间距", "位置");
    }

    public static bool HasCompatibleSingleSource(
        IReadOnlyList<MeasurementSnapshot> current,
        string expectedQuantity) =>
        current.Any(x => IsCompatible(expectedQuantity, x.Quantity) &&
                         !x.Source.Contains("↔", StringComparison.Ordinal));

    public static bool HasCompatiblePairSource(
        IReadOnlyList<MeasurementSnapshot> current,
        string expectedQuantity) =>
        current.Any(x => IsCompatible(expectedQuantity, x.Quantity) &&
                         x.Source.Contains("↔", StringComparison.Ordinal));

    private static bool IsCompatible(string expected, string actual)
    {
        if (expected.Equals(actual, StringComparison.OrdinalIgnoreCase)) return true;
        return expected.Equals("length", StringComparison.OrdinalIgnoreCase) &&
               actual is "length" or "distance" or "minimumDistance" or "maximumDistance";
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(x => value.Contains(x, StringComparison.OrdinalIgnoreCase));
}
