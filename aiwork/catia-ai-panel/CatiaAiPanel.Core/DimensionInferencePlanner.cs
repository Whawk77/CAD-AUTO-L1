using System.Globalization;

namespace CatiaAiPanel.Core;

public static class DimensionInferencePlanner
{
    public static IReadOnlyList<string> Build(IReadOnlyList<MeasurementSnapshot> measurements)
    {
        var suggestions = new List<string>();
        var length = Find(measurements, "overall_length");
        var width = Find(measurements, "overall_width");
        var height = Find(measurements, "overall_height");

        if (length is not null && width is not null && height is not null)
        {
            suggestions.Add(
                $"采用局部原点 (0,0,0)，X=总长 {Mm(length.Value)}，Y=总宽 {Mm(width.Value)}，Z=总高 {Mm(height.Value)}；主体默认从 XY 平面矩形拉伸。 ");
        }

        var holePitch = Find(measurements, "hole_pitch");
        var holeDiameter = measurements.FirstOrDefault(x =>
            Normalize(x.Name).Contains("hole", StringComparison.Ordinal) &&
            x.Quantity.Equals("diameter", StringComparison.OrdinalIgnoreCase));
        if (holePitch is not null && holeDiameter is null)
        {
            var inferred = Math.Max(1d, Math.Min(holePitch.Value * 0.4d, (height?.Value ?? holePitch.Value) * 0.5d));
            suggestions.Add($"存在孔距但没有孔径：建议 AI_INFERRED_hole_diameter={Mm(inferred)}；孔组围绕主体中心对称布置。 ");
        }

        var slotWidth = Find(measurements, "slot_width");
        var slotDepth = Find(measurements, "slot_depth");
        var slotRadius = Find(measurements, "slot_radius") ?? Find(measurements, "slot_end_radius");
        if (slotWidth is not null && slotDepth is not null && slotRadius is null)
        {
            var inferred = Math.Min(Math.Abs(slotWidth.Value), Math.Abs(slotDepth.Value)) / 2d;
            suggestions.Add($"存在槽宽/槽深但没有端部半径：建议 AI_INFERRED_slot_end_radius={Mm(inferred)}，槽默认位于对应面的中心。 ");
        }

        suggestions.Add("只有账本中已有语义证据的孔、槽、圆角才允许补齐；没有任何测量证据的特征不得凭空创建。 ");
        suggestions.Add("所有补齐值必须创建为 AI_INFERRED_* 参数，并写入 reconstructionPlan.assumptions，便于用户在执行前识别和修改。 ");
        return suggestions;
    }

    private static MeasurementSnapshot? Find(IReadOnlyList<MeasurementSnapshot> measurements, string semantic) =>
        measurements.FirstOrDefault(x => Normalize(x.Name).Contains(semantic, StringComparison.Ordinal));

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');

    private static string Mm(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture) + " mm";
}
