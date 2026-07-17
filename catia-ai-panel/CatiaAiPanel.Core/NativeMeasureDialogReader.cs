using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace CatiaAiPanel.Core;

internal static partial class NativeMeasureDialogReader
{
    public static MeasurementCaptureResult Capture(string documentKey)
    {
        try
        {
            var process = Process.GetProcessesByName("CNEXT")
                .FirstOrDefault(x => x.MainWindowHandle != IntPtr.Zero);
            if (process is null) return new([], []);
            var root = AutomationElement.FromHandle(process.MainWindowHandle);
            var resultPane = FindVisibleResultPane(root);
            if (resultPane is null) return new([], []);

            var elements = resultPane.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                .Cast<AutomationElement>()
                .ToArray();
            var source = FindSource(elements);
            var fields = ReadFields(elements);
            var records = ParseFields(documentKey, source, fields);
            return records.Count == 0
                ? new([], ["已找到 CATIA 原生测量窗口，但没有读取到数值字段。请等待计算完成后再同步。"])
                : new(records, []);
        }
        catch (ElementNotAvailableException)
        {
            return new([], ["CATIA 测量窗口已关闭或正在刷新，请重新打开后同步。"]);
        }
        catch (Exception ex)
        {
            return new([], [$"读取 CATIA 原生测量窗口失败：{ex.Message}"]);
        }
    }

    internal static IReadOnlyList<MeasurementSnapshot> ParseFields(
        string documentKey,
        string source,
        IReadOnlyList<NativeMeasureField> fields)
    {
        var parsed = fields
            .Select(x => (Field: x, Parsed: ParseValue(x.Value, x.Label)))
            .Where(x => x.Parsed is not null)
            .Select(x => (x.Field, Value: x.Parsed!))
            .ToArray();
        if (parsed.Length == 0) return [];

        var x = FindCoordinate(parsed, "centerX");
        var y = FindCoordinate(parsed, "centerY");
        var z = FindCoordinate(parsed, "centerZ");
        IReadOnlyList<double> coordinates = x is not null && y is not null && z is not null
            ? [x.Number, y.Number, z.Number]
            : [];
        var scalarFields = parsed.Where(x => !IsCoordinateQuantity(NormalizeQuantity(x.Field.Label, x.Field.AutomationId))).ToArray();
        if (scalarFields.Length == 0) scalarFields = parsed;

        var recordName = string.IsNullOrWhiteSpace(source) ? "NativeMeasure" : TrimName(source);
        var records = scalarFields.Select(item =>
        {
            var quantity = NormalizeQuantity(item.Field.Label, item.Field.AutomationId);
            var geometry = InferGeometry(source, quantity);
            var normalized = NormalizeUnit(item.Value.Number, item.Value.Unit, quantity);
            return new MeasurementSnapshot(
                StableId($"native|{documentKey}|{source}|{quantity}|{normalized.Value:R}|{normalized.Unit}|{string.Join(",", coordinates.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))}"),
                recordName,
                quantity,
                normalized.Value,
                normalized.Unit,
                geometry,
                source,
                coordinates,
                [],
                null,
                "CATIA native Measure dialog via UI Automation")
            {
                RawValue = item.Field.Value,
                Approximate = item.Value.Approximate
            };
        }).ToList();
        foreach (var radius in records.Where(x => x.Quantity == "radius").ToArray())
        {
            records.Add(radius with
            {
                Id = StableId($"native|{documentKey}|{source}|diameter|{radius.Value * 2d:R}|{radius.Unit}|{string.Join(",", coordinates.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))}"),
                Quantity = "diameter",
                Value = radius.Value * 2d,
                RawValue = $"2 × {radius.RawValue}",
                Provenance = radius.Provenance + "; diameter derived exactly as 2 × radius"
            });
        }
        return records;
    }

    private static AutomationElement? FindVisibleResultPane(AutomationElement root)
    {
        var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane);
        return root.FindAll(TreeScope.Descendants, condition)
            .Cast<AutomationElement>()
            .Where(x => IsVisible(x) && IsResultLabel(Safe(() => x.Current.Name) ?? ""))
            .OrderByDescending(x => x.Current.BoundingRectangle.Width * x.Current.BoundingRectangle.Height)
            .FirstOrDefault();
    }

    private static bool IsVisible(AutomationElement element)
    {
        try
        {
            var rectangle = element.Current.BoundingRectangle;
            return !element.Current.IsOffscreen && rectangle.Width > 0 && rectangle.Height > 0;
        }
        catch { return false; }
    }

    private static string FindSource(IEnumerable<AutomationElement> elements)
    {
        var afterSelectionLabel = false;
        var sources = new List<string>();
        foreach (var element in elements)
        {
            var name = (Safe(() => element.Current.Name) ?? "").Trim();
            var type = Safe(() => element.Current.ControlType);
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (IsSelectionLabel(name))
            {
                afterSelectionLabel = true;
                continue;
            }
            if (afterSelectionLabel && type is not null &&
                (type == ControlType.Image || type == ControlType.Text) &&
                !LooksLikeLabel(name) &&
                !name.Equals("精确值", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("Exact value", StringComparison.OrdinalIgnoreCase))
            {
                sources.Add(name);
                afterSelectionLabel = false;
            }
        }
        return string.Join(" ↔ ", sources.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<NativeMeasureField> ReadFields(IReadOnlyList<AutomationElement> elements)
    {
        var result = new List<NativeMeasureField>();
        var lastLabel = "";
        foreach (var element in elements)
        {
            var name = (Safe(() => element.Current.Name) ?? "").Trim();
            var type = Safe(() => element.Current.ControlType);
            var id = Safe(() => element.Current.AutomationId) ?? "";
            if (type != ControlType.Edit && !string.IsNullOrWhiteSpace(name) && LooksLikeLabel(name))
                lastLabel = name.TrimEnd('：', ':');
            if (type != ControlType.Edit || !IsVisible(element)) continue;
            var value = ReadValue(element);
            if (string.IsNullOrWhiteSpace(value)) continue;
            result.Add(new(lastLabel, value.Trim(), id));
        }
        return result;
    }

    private static string ReadValue(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern))
                return ((ValuePattern)valuePattern).Current.Value ?? "";
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out var textPattern))
                return ((TextPattern)textPattern).DocumentRange.GetText(-1) ?? "";
        }
        catch { }
        return Safe(() => element.Current.Name);
    }

    private static bool LooksLikeLabel(string text) =>
        text.EndsWith('：') || text.EndsWith(':') ||
        text is "X" or "Y" or "Z" ||
        LabelWords().IsMatch(text);

    private static ParsedValue? ParseValue(string text, string label)
    {
        var match = NumberWithUnit().Match(text.Replace(" ", ""));
        if (!match.Success) return null;
        var numberText = match.Groups["number"].Value.Replace(',', '.');
        if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return null;
        var unit = match.Groups["unit"].Value;
        if (string.IsNullOrWhiteSpace(unit)) unit = InferUnit(label);
        return new(number, unit, match.Groups["approx"].Success);
    }

    private static string InferUnit(string label)
    {
        var quantity = NormalizeQuantity(label, "");
        return quantity switch
        {
            "angle" => "deg",
            "area" => "mm²",
            "volume" => "mm³",
            _ => "mm"
        };
    }

    private static (double Value, string Unit) NormalizeUnit(double value, string unit, string quantity)
    {
        var normalized = unit.Trim().ToLowerInvariant();
        return normalized switch
        {
            "m" => (value * 1000d, "mm"),
            "cm" => (value * 10d, "mm"),
            "m²" or "m2" => (value * 1_000_000d, "mm²"),
            "cm²" or "cm2" => (value * 100d, "mm²"),
            "m³" or "m3" => (value * 1_000_000_000d, "mm³"),
            "cm³" or "cm3" => (value * 1000d, "mm³"),
            "°" => (value, "deg"),
            "" when quantity == "angle" => (value, "deg"),
            _ => (value, string.IsNullOrWhiteSpace(unit) ? InferUnit(quantity) : unit)
        };
    }

    private static string NormalizeQuantity(string label, string automationId)
    {
        var value = label.Trim().TrimEnd('：', ':').ToLowerInvariant();
        if (value is "x" or "中心点x" or "centerx") return "centerX";
        if (value is "y" or "中心点y" or "centery") return "centerY";
        if (value is "z" or "中心点z" or "centerz") return "centerZ";
        if (value.Contains("半径") || value.Contains("radius")) return "radius";
        if (value.Contains("直径") || value.Contains("diameter")) return "diameter";
        if (value.Contains("长度") || value.Contains("length")) return "length";
        if (value.Contains("距离") || value.Contains("distance")) return "distance";
        if (value.Contains("面积") || value.Contains("area")) return "area";
        if (value.Contains("体积") || value.Contains("volume")) return "volume";
        if (value.Contains("周长") || value.Contains("perimeter")) return "perimeter";
        if (value.Contains("角") || value.Contains("angle")) return "angle";
        return string.IsNullOrWhiteSpace(value) ? "value" : Regex.Replace(value, @"\s+", "_");
    }

    private static bool IsResultLabel(string text) =>
        text.Trim().Equals("结果", StringComparison.OrdinalIgnoreCase) ||
        text.Trim().Equals("Result", StringComparison.OrdinalIgnoreCase) ||
        text.Trim().Equals("Results", StringComparison.OrdinalIgnoreCase);

    internal static bool IsSelectionLabel(string text)
    {
        var value = text.Trim().TrimEnd('：', ':');
        return SelectionLabelPattern().IsMatch(value);
    }

    private static bool IsCoordinateQuantity(string quantity) => quantity is "centerX" or "centerY" or "centerZ";

    private static ParsedValue? FindCoordinate(
        IEnumerable<(NativeMeasureField Field, ParsedValue Value)> values,
        string quantity) =>
        values.FirstOrDefault(x => NormalizeQuantity(x.Field.Label, x.Field.AutomationId) == quantity).Value;

    private static string InferGeometry(string source, string quantity)
    {
        if (source.Contains("弧") || source.Contains("Arc", StringComparison.OrdinalIgnoreCase)) return "Arc";
        if (source.Contains("圆") || quantity is "radius" or "diameter") return "Circle";
        if (source.Contains("直线") || source.Contains("Line", StringComparison.OrdinalIgnoreCase)) return "Line";
        if (source.Contains("面") || source.Contains("Face", StringComparison.OrdinalIgnoreCase)) return "Surface";
        if (source.Contains("边") || source.Contains("Edge", StringComparison.OrdinalIgnoreCase)) return "Edge";
        return "NativeMeasure";
    }

    private static string TrimName(string value) => value.Length <= 96 ? value : value[..96];

    private static string StableId(string source)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(source);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..24];
    }

    private static T Safe<T>(Func<T> getter)
    {
        try { return getter(); }
        catch { return default!; }
    }

    internal sealed record NativeMeasureField(string Label, string Value, string AutomationId = "");
    private sealed record ParsedValue(double Number, string Unit, bool Approximate);

    [GeneratedRegex(@"(?<approx>[~≈])?(?<number>[-+]?\d+(?:[\.,]\d+)?)(?<unit>mm³|mm²|cm³|cm²|m³|m²|mm|cm|m|deg|rad|°)?", RegexOptions.IgnoreCase)]
    private static partial Regex NumberWithUnit();

    [GeneratedRegex(@"半径|直径|长度|距离|面积|体积|周长|角度|中心点|radius|diameter|length|distance|area|volume|perimeter|angle|center", RegexOptions.IgnoreCase)]
    private static partial Regex LabelWords();

    [GeneratedRegex(@"^(选择|Selection)\s*(?:[12一二])?$", RegexOptions.IgnoreCase)]
    private static partial Regex SelectionLabelPattern();
}
