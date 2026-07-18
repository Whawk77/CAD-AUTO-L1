using System.Globalization;
using System.Text;

namespace CatiaAiPanel.Core;

public static class MeasurementSummaryFormatter
{
    public static string Format(IReadOnlyList<MeasurementSnapshot> records)
    {
        var text = new StringBuilder($"已测量尺寸（{records.Count} 条记录）");
        var classifiedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var classifiedCount = 0;

        foreach (var type in GuidedMeasurementTypes.All)
        {
            var matches = records.Where(record => MatchesType(record.Name, type.Key)).ToArray();
            if (matches.Length == 0) continue;
            classifiedCount += matches.Length;
            foreach (var record in matches) classifiedIds.Add(record.Id);
            text.Append("\n✓ ").Append(type.Label).Append("：").Append(FormatValues(matches));
        }

        if (classifiedCount == 0)
            text.Append("\n尚无已分类尺寸；请先选择尺寸类型并完成原生测量。");

        var unclassified = records.Where(record => !classifiedIds.Contains(record.Id)).ToArray();
        foreach (var group in unclassified.GroupBy(record => QuantityLabel(record.Quantity)).Take(8))
        {
            text.Append("\n• 未分类").Append(group.Key).Append("：").Append(FormatValues(group));
        }
        return text.ToString();
    }

    private static bool MatchesType(string name, string key)
    {
        var normalized = name.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return normalized.Equals(key, StringComparison.Ordinal) ||
               normalized.StartsWith(key + "_", StringComparison.Ordinal);
    }

    private static string FormatValues(IEnumerable<MeasurementSnapshot> records)
    {
        var values = records
            .GroupBy(record => $"{record.Approximate}|{record.Value:R}|{record.Unit}")
            .Select(group =>
            {
                var record = group.First();
                var value = record.Value.ToString("G10", CultureInfo.InvariantCulture);
                var count = group.Count();
                return $"{(record.Approximate ? "≈" : "")}{value} {record.Unit}{(count > 1 ? $" ×{count}" : "")}";
            })
            .Take(10)
            .ToArray();
        return string.Join("，", values);
    }

    private static string QuantityLabel(string quantity) => quantity.ToLowerInvariant() switch
    {
        "length" => "长度",
        "distance" or "minimumdistance" or "maximumdistance" => "距离",
        "diameter" => "直径",
        "radius" => "半径",
        "angle" => "角度",
        "area" => "面积",
        "volume" => "体积",
        "perimeter" => "周长",
        _ => quantity
    };
}
