using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CatiaAiPanel.Core;

internal sealed class MeasurementNotebookStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _rootDirectory;
    private readonly Dictionary<string, IReadOnlyList<MeasurementSnapshot>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public MeasurementNotebookStore(string? rootDirectory = null)
    {
        _rootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatiaAiPanel",
            "measurements");
    }

    public IReadOnlyList<MeasurementSnapshot> Load(string documentKey)
    {
        if (_cache.TryGetValue(documentKey, out var cached)) return cached;
        var path = GetPath(documentKey);
        var suppressed = LoadSuppressed(documentKey);
        IReadOnlyList<MeasurementSnapshot> records = [];
        try
        {
            if (File.Exists(path))
                records = (JsonSerializer.Deserialize<List<MeasurementSnapshot>>(File.ReadAllText(path), Options) ?? [])
                    .Where(x => !IsInvalidNativeContainerRecord(x))
                    .Where(x => !suppressed.Contains(x.Id))
                    .ToArray();
        }
        catch { }
        _cache[documentKey] = records;
        return records;
    }

    public IReadOnlyList<MeasurementSnapshot> MergeAndSave(
        string documentKey,
        IEnumerable<MeasurementSnapshot> incoming)
    {
        var existing = Load(documentKey);
        var suppressed = LoadSuppressed(documentKey);
        var merged = existing
            .Concat(incoming.Where(x => !suppressed.Contains(x.Id)))
            .GroupBy(MergeKey, StringComparer.OrdinalIgnoreCase)
            .Select(MergeGroup)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Quantity, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (JsonSerializer.Serialize(existing, Options) == JsonSerializer.Serialize(merged, Options))
            return existing;
        Directory.CreateDirectory(_rootDirectory);
        File.WriteAllText(GetPath(documentKey), JsonSerializer.Serialize(merged, Options), new UTF8Encoding(false));
        _cache[documentKey] = merged;
        return merged;
    }

    private static MeasurementSnapshot MergeGroup(IGrouping<string, MeasurementSnapshot> group)
    {
        var latest = group.Last();
        if (!IsGenericMeasurementName(latest)) return latest;
        var semantic = group.LastOrDefault(x => !IsGenericMeasurementName(x));
        return semantic is null ? latest : latest with { Name = semantic.Name };
    }

    public IReadOnlyList<MeasurementSnapshot> RenameAndSave(
        string documentKey,
        string measurementId,
        string semanticName)
    {
        var cleanName = semanticName.Trim();
        if (string.IsNullOrWhiteSpace(cleanName)) throw new ArgumentException("尺寸语义名称不能为空。", nameof(semanticName));
        var existing = Load(documentKey);
        if (!existing.Any(x => x.Id.Equals(measurementId, StringComparison.OrdinalIgnoreCase)))
            throw new KeyNotFoundException("找不到要命名的测量记录。");
        var updated = existing.Select(x => x.Id.Equals(measurementId, StringComparison.OrdinalIgnoreCase)
                ? x with { Name = cleanName }
                : x)
            .ToArray();
        Directory.CreateDirectory(_rootDirectory);
        File.WriteAllText(GetPath(documentKey), JsonSerializer.Serialize(updated, Options), new UTF8Encoding(false));
        _cache[documentKey] = updated;
        return updated;
    }

    public IReadOnlyList<MeasurementSnapshot> DeleteAndSave(string documentKey, string measurementId)
    {
        var existing = Load(documentKey);
        if (!existing.Any(x => x.Id.Equals(measurementId, StringComparison.OrdinalIgnoreCase)))
            throw new KeyNotFoundException("找不到要删除的测量记录。");
        var suppressed = LoadSuppressed(documentKey);
        suppressed.Add(measurementId);
        SaveSuppressed(documentKey, suppressed);
        var updated = existing.Where(x => !x.Id.Equals(measurementId, StringComparison.OrdinalIgnoreCase)).ToArray();
        Save(documentKey, updated);
        return updated;
    }

    public IReadOnlyList<MeasurementSnapshot> ClearAndSave(string documentKey)
    {
        var existing = Load(documentKey);
        var suppressed = LoadSuppressed(documentKey);
        foreach (var record in existing) suppressed.Add(record.Id);
        SaveSuppressed(documentKey, suppressed);
        IReadOnlyList<MeasurementSnapshot> updated = [];
        Save(documentKey, updated);
        return updated;
    }

    public void ResetSuppression(string documentKey)
    {
        SaveSuppressed(documentKey, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        _cache.Remove(documentKey);
    }

    private void Save(string documentKey, IReadOnlyList<MeasurementSnapshot> records)
    {
        Directory.CreateDirectory(_rootDirectory);
        File.WriteAllText(GetPath(documentKey), JsonSerializer.Serialize(records, Options), new UTF8Encoding(false));
        _cache[documentKey] = records;
    }

    private HashSet<string> LoadSuppressed(string documentKey)
    {
        try
        {
            var path = GetSuppressedPath(documentKey);
            if (File.Exists(path))
                return new(JsonSerializer.Deserialize<string[]>(File.ReadAllText(path), Options) ?? [], StringComparer.OrdinalIgnoreCase);
        }
        catch { }
        return new(StringComparer.OrdinalIgnoreCase);
    }

    private void SaveSuppressed(string documentKey, HashSet<string> ids)
    {
        Directory.CreateDirectory(_rootDirectory);
        File.WriteAllText(GetSuppressedPath(documentKey), JsonSerializer.Serialize(ids.Order(StringComparer.OrdinalIgnoreCase), Options), new UTF8Encoding(false));
    }

    private string GetPath(string documentKey)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(documentKey)))[..24];
        return Path.Combine(_rootDirectory, $"{hash}.json");
    }

    private string GetSuppressedPath(string documentKey) =>
        Path.Combine(_rootDirectory, Path.GetFileNameWithoutExtension(GetPath(documentKey)) + ".suppressed.json");

    private static bool IsInvalidNativeContainerRecord(MeasurementSnapshot record) =>
        record.Provenance.StartsWith("CATIA native Measure dialog", StringComparison.OrdinalIgnoreCase) &&
        record.Source is "DataFrame" or "LabelFrame" or "CenterPt" or "SubDefinitionFrame1" or "SubDefinitionFrame2";

    private static bool IsGenericMeasurementName(MeasurementSnapshot record)
    {
        var value = record.Name.Trim();
        if ((record.Provenance.StartsWith("CATIA SPA Measurable", StringComparison.OrdinalIgnoreCase) ||
             record.Provenance.StartsWith("CATIA native Measure dialog", StringComparison.OrdinalIgnoreCase)) &&
            value.Equals(record.Source.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;
        return value.StartsWith("弧 在", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("直线 在", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("面 在", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("NativeMeasure", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("Distance.", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("Selection.", StringComparison.OrdinalIgnoreCase);
    }

    private static string MergeKey(MeasurementSnapshot record)
    {
        if (!record.Provenance.StartsWith("CATIA native Measure dialog", StringComparison.OrdinalIgnoreCase))
            return record.Id;
        return string.Join("|",
            "native",
            record.Source,
            record.Quantity,
            record.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            record.Unit,
            string.Join(",", record.Coordinates.Select(x => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
    }
}
