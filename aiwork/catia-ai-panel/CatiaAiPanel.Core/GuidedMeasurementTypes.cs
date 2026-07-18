namespace CatiaAiPanel.Core;

public sealed record GuidedMeasurementType(string Key, string Label, string Hint, bool RequiresTwoObjects);

public static class GuidedMeasurementTypes
{
    public static IReadOnlyList<GuidedMeasurementType> All { get; } =
    [
        new("overall_length", "总长", "选择一条代表总长的边或线", false),
        new("overall_width", "总宽", "选择一条代表总宽的边或线", false),
        new("overall_height", "总高", "选择一条代表总高的边或线", false),
        new("thickness", "厚度", "选择一条代表厚度的边或线", false),
        new("hole_diameter", "孔径", "选择孔的圆边或圆柱面", false),
        new("hole_pitch", "孔距", "依次选择两个孔的圆边、圆柱面或轴线", true),
        new("corner_radius", "圆角半径", "选择圆角弧线或圆角面", false),
        new("center_distance", "中心距", "依次选择两个圆、圆柱面或轴线", true),
        new("slot_width", "槽宽", "选择一条代表槽宽的边或线", false),
        new("slot_depth", "槽深", "选择一条代表槽深的边或线", false),
        new("step_height", "台阶高度", "选择一条代表台阶高度的边或线", false),
        new("offset_distance", "偏移距离", "依次选择两个需要定位的基准对象", true)
    ];

    public static GuidedMeasurementType? Find(string key) =>
        All.FirstOrDefault(type => type.Key.Equals(key.Trim(), StringComparison.OrdinalIgnoreCase));
}
