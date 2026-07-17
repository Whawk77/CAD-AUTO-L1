using System.Runtime.InteropServices;

namespace CatiaAiPanel.Core;

public sealed class CatiaSession : ICatiaSession
{
    private const int MaxTreeDepth = 4;
    private const int MaxTreeNodes = 200;
    private const int MaxSelectionItems = 100;
    private const int MaxMeasurements = 200;
    private readonly StaWorker _sta = new("CATIA Automation STA");
    private readonly MeasurementNotebookStore _measurementNotebook = new();
    private object? _application;

    public Task<CatiaContextSnapshot> CaptureAsync(CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync(CaptureOnSta, cancellationToken);

    public async Task<ExecutionPreconditionResult> ValidateExecutionAsync(
        MacroProposal proposal,
        CatiaContextSnapshot generatedFrom,
        CancellationToken cancellationToken = default)
    {
        var current = await CaptureAsync(cancellationToken).ConfigureAwait(false);
        return ExecutionPreconditions.Evaluate(proposal, generatedFrom, current);
    }

    public Task<object?> ExecuteScriptAsync(
        string libraryDirectory,
        string programName,
        string entryPoint,
        object[] parameters,
        CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync<object?>(() =>
        {
            var app = EnsureApplication();
            object? systemService = null;
            try
            {
                systemService = ComDispatch.Get(app, "SystemService");
                if (systemService is null) throw new COMException("CATIA SystemService 不可用。");
                // CatScriptLibraryType.catScriptLibraryTypeDirectory = 1 in B28 InfTypeLib.
                return ComDispatch.Call(systemService, "ExecuteScript", libraryDirectory, 1, programName, entryPoint, parameters);
            }
            finally { ComHelpers.Release(systemService); }
        }, cancellationToken);

    public Task StartNativeMeasureAsync(CancellationToken cancellationToken = default) =>
        StartTrustedCommandAsync("Measure Item", cancellationToken);

    public Task StartNativeMeasureBetweenAsync(CancellationToken cancellationToken = default) =>
        StartTrustedCommandAsync("Measure Between", cancellationToken);

    private Task StartTrustedCommandAsync(string command, CancellationToken cancellationToken) =>
        _sta.InvokeAsync(() =>
        {
            var app = EnsureApplication();
            object? document = null;
            try
            {
                document = ComDispatch.Get(app, "ActiveDocument");
                if (document is not null) _measurementNotebook.ResetSuppression(GetDocumentKey(document));
            }
            finally { ComHelpers.Release(document); }
            // Fixed trusted command. Generated macros are still forbidden from calling StartCommand.
            ComDispatch.Call(app, "StartCommand", command);
            return true;
        }, cancellationToken);

    public Task<MeasurementCaptureResult> CaptureMeasurementsAsync(CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync(CaptureMeasurementsOnSta, cancellationToken);

    public Task<IReadOnlyList<MeasurementSnapshot>> RenameMeasurementAsync(
        string measurementId,
        string semanticName,
        CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync<IReadOnlyList<MeasurementSnapshot>>(() =>
        {
            var app = EnsureApplication();
            object? document = null;
            try
            {
                document = ComDispatch.Get(app, "ActiveDocument");
                if (document is null) throw new InvalidOperationException("CATIA 没有活动文档。");
                return _measurementNotebook.RenameAndSave(GetDocumentKey(document), measurementId, semanticName);
            }
            finally { ComHelpers.Release(document); }
        }, cancellationToken);

    public Task<IReadOnlyList<MeasurementSnapshot>> DeleteMeasurementAsync(
        string measurementId,
        CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync<IReadOnlyList<MeasurementSnapshot>>(() =>
        {
            var app = EnsureApplication();
            object? document = null;
            try
            {
                document = ComDispatch.Get(app, "ActiveDocument");
                if (document is null) throw new InvalidOperationException("CATIA 没有活动文档。");
                return _measurementNotebook.DeleteAndSave(GetDocumentKey(document), measurementId);
            }
            finally { ComHelpers.Release(document); }
        }, cancellationToken);

    public Task<IReadOnlyList<MeasurementSnapshot>> ClearMeasurementsAsync(
        CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync<IReadOnlyList<MeasurementSnapshot>>(() =>
        {
            var app = EnsureApplication();
            object? document = null;
            try
            {
                document = ComDispatch.Get(app, "ActiveDocument");
                if (document is null) throw new InvalidOperationException("CATIA 没有活动文档。");
                return _measurementNotebook.ClearAndSave(GetDocumentKey(document));
            }
            finally { ComHelpers.Release(document); }
        }, cancellationToken);

    private MeasurementCaptureResult CaptureMeasurementsOnSta()
    {
        var app = EnsureApplication();
        object? document = null;
        try
        {
            document = ComDispatch.Get(app, "ActiveDocument");
            if (document is null)
                return new([], ["CATIA 没有活动文档。"]);

            var documentKey = GetDocumentKey(document);
            var warnings = new List<string>();
            var nativeResult = NativeMeasureDialogReader.Capture(documentKey);
            warnings.AddRange(nativeResult.Warnings);
            var captured = nativeResult.Records
                .Concat(CapturePersistentDistances(document, recompute: true, warnings))
                .Concat(CaptureSelectedMeasurables(document, warnings, MaxSelectionItems))
                .Concat(CaptureSelectedPairDistance(document, warnings))
                .Take(MaxMeasurements)
                .ToArray();
            var merged = _measurementNotebook.MergeAndSave(documentKey, captured);
            if (captured.Length == 0)
                warnings.Add("没有发现可记录的测量。请保持 Measure Item/Measure Between 结果窗口打开，再点击同步。 ");
            return new(merged, warnings);
        }
        finally { ComHelpers.Release(document); }
    }

    private CatiaContextSnapshot CaptureOnSta()
    {
        try
        {
            var appObject = EnsureApplication();
            object? documentObject;
            try
            {
                documentObject = ComDispatch.Get(appObject, "ActiveDocument");
            }
            catch (Exception ex)
            {
                return new(DateTimeOffset.Now, CatiaConnectionState.Faulted,
                    $"无法读取 CATIA 活动文档（0x{ex.HResult:X8}）：{ex.Message}", null, [], false, "");
            }
            if (documentObject is null)
                return CatiaContextSnapshot.Disconnected("CATIA 已连接，但没有活动文档。") with
                { ConnectionState = CatiaConnectionState.Connected };

            try
            {
                var documentName = ComHelpers.Text(() => ComDispatch.Get(documentObject, "Name"));
                var documentType = Path.GetExtension(documentName).TrimStart('.');
                var workbench = ComHelpers.Text(() => ComDispatch.Call(appObject, "GetWorkbenchId"));
                var treeBudget = new TreeBudget(MaxTreeNodes);
                var inWorkObject = "";
                IReadOnlyList<TreeNodeSnapshot> tree;

                if (documentType.Equals("CATPart", StringComparison.OrdinalIgnoreCase))
                    tree = CapturePart(documentObject, treeBudget, out inWorkObject);
                else if (documentType.Equals("CATProduct", StringComparison.OrdinalIgnoreCase))
                    tree = CaptureProduct(documentObject, treeBudget);
                else
                    tree = [];

                var documentSnapshot = new DocumentSnapshot(
                    documentName,
                    ComHelpers.Text(() => ComDispatch.Get(documentObject, "FullName"), 1024),
                    documentType,
                    workbench,
                    ComHelpers.Bool(() => ComDispatch.Get(documentObject, "Saved")),
                    ComHelpers.Bool(() => ComDispatch.Get(documentObject, "ReadOnly")),
                    inWorkObject,
                    tree);

                bool selectionTruncated;
                var selection = CaptureSelection(documentObject, out selectionTruncated);
                var documentKey = GetDocumentKey(documentObject);
                var measurements = _measurementNotebook.Load(documentKey);
                var liveNativeMeasurement = NativeMeasureDialogReader.Capture(documentKey);
                var measurementWarnings = new List<string>();
                measurementWarnings.AddRange(liveNativeMeasurement.Warnings);
                var automaticSelectionMeasurements = CaptureSelectedMeasurables(documentObject, measurementWarnings, 20)
                    .Concat(CaptureSelectedPairDistance(documentObject, measurementWarnings))
                    .ToArray();
                if (liveNativeMeasurement.Records.Count > 0 || automaticSelectionMeasurements.Length > 0)
                    measurements = _measurementNotebook.MergeAndSave(documentKey,
                        liveNativeMeasurement.Records.Concat(automaticSelectionMeasurements));
                var fingerprint = ContextFingerprint.Compute(documentSnapshot, selection, measurements);
                var snapshot = new CatiaContextSnapshot(
                    DateTimeOffset.Now,
                    CatiaConnectionState.Connected,
                    $"已连接 CATIA：{documentName}",
                    documentSnapshot,
                    selection,
                    treeBudget.Truncated || selectionTruncated,
                    fingerprint);
                return snapshot with
                {
                    CatiaVersion = ReadCatiaVersion(appObject),
                    Measurements = measurements,
                    LiveNativeMeasurements = liveNativeMeasurement.Records,
                    MeasurementWarnings = measurementWarnings.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray(),
                    ReconstructionReadiness = ReconstructionReadinessAnalyzer.Analyze(measurements)
                };
            }
            finally { ComHelpers.Release(documentObject); }
        }
        catch (COMException ex)
        {
            DropApplication();
            return new(DateTimeOffset.Now, CatiaConnectionState.Faulted,
                $"CATIA COM 错误 0x{ex.HResult:X8}：{ex.Message}", null, [], false, "");
        }
        catch (Exception ex)
        {
            DropApplication();
            return new(DateTimeOffset.Now, CatiaConnectionState.Disconnected,
                $"未连接 CATIA：{ex.Message}", null, [], false, "");
        }
    }

    private static string GetDocumentKey(object document)
    {
        var fullName = ComHelpers.Text(() => ComDispatch.Get(document, "FullName"), 1024);
        return string.IsNullOrWhiteSpace(fullName)
            ? ComHelpers.Text(() => ComDispatch.Get(document, "Name"), 512)
            : fullName;
    }

    private static string ReadCatiaVersion(object application)
    {
        object? configuration = ComHelpers.Object(() => ComDispatch.Get(application, "SystemConfiguration"));
        if (configuration is null) return "CATIA V5";
        try
        {
            var release = ComHelpers.Int(() => ComDispatch.Get(configuration, "Release"));
            var version = ComHelpers.Int(() => ComDispatch.Get(configuration, "Version"));
            var servicePack = ComHelpers.Int(() => ComDispatch.Get(configuration, "ServicePack"));
            if (release <= 0) return "CATIA V5";
            var year = release + 1990;
            return $"V{version}-6R{year} (B{release}, SP{servicePack})";
        }
        finally { ComHelpers.Release(configuration); }
    }

    private static IReadOnlyList<MeasurementSnapshot> CapturePersistentDistances(
        object document,
        bool recompute,
        List<string> warnings)
    {
        object? spa = null;
        object? distances = null;
        try
        {
            spa = ComHelpers.Object(() => ComDispatch.Call(document, "GetWorkbench", "SPAWorkbench"));
            if (spa is null) return [];
            distances = ComHelpers.Object(() => ComDispatch.Get(spa, "Distances"));
            if (distances is null) return [];
            var count = Math.Min(ComHelpers.Int(() => ComDispatch.Get(distances, "Count")), MaxMeasurements);
            var records = new List<MeasurementSnapshot>(count);
            for (var index = 1; index <= count; index++)
            {
                object? distance = ComHelpers.Object(() => ComDispatch.Call(distances, "Item", index));
                if (distance is null) continue;
                try
                {
                    if (recompute)
                    {
                        try { ComDispatch.Call(distance, "Compute"); }
                        catch (Exception ex) { warnings.Add($"Distance.{index} 重新计算失败：{ex.Message}"); }
                    }

                    if (!TryReadDouble(distance, "Value", out var value) &&
                        !TryReadDouble(distance, "MinimumDistance", out value))
                        continue;

                    var name = ComHelpers.Text(() => ComDispatch.Get(distance, "Name"));
                    if (string.IsNullOrWhiteSpace(name)) name = $"Distance.{index}";
                    var measureType = ComHelpers.Int(() => ComDispatch.Get(distance, "MeasureType"));
                    var first = ReadCoordinates(distance, "GetFirstPointCoordinates", 3);
                    var second = ReadCoordinates(distance, "GetSecondPointCoordinates", 3);
                    var coordinates = first.Concat(second).ToArray();
                    var direction = first.Count == 3 && second.Count == 3
                        ? new[] { second[0] - first[0], second[1] - first[1], second[2] - first[2] }
                        : [];
                    var firstProduct = ReadObjectName(distance, "FirstProduct");
                    var secondProduct = ReadObjectName(distance, "SecondProduct");
                    var source = string.Join(" → ", new[] { firstProduct, secondProduct }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    double? accuracy = TryReadDouble(distance, "Accuracy", out var accuracyValue) ? accuracyValue : null;
                    records.Add(new(
                        StableMeasurementId($"distance|{name}|{source}"),
                        name,
                        measureType switch
                        {
                            1 => "distanceX",
                            2 => "distanceY",
                            3 => "distanceZ",
                            4 => "distanceBand",
                            _ => "minimumDistance"
                        },
                        value,
                        "mm",
                        "Distance",
                        source,
                        coordinates,
                        direction,
                        accuracy,
                        "CATIA SPA persistent Distance (Keep Measure)"));
                }
                finally { ComHelpers.Release(distance); }
            }
            return records;
        }
        catch (Exception ex)
        {
            warnings.Add($"读取 CATIA 保留测量失败：{ex.Message}");
            return [];
        }
        finally
        {
            ComHelpers.Release(distances);
            ComHelpers.Release(spa);
        }
    }

    private static IReadOnlyList<MeasurementSnapshot> CaptureSelectedMeasurables(
        object document,
        List<string> warnings,
        int maxItems)
    {
        object? spa = null;
        object? selection = null;
        try
        {
            spa = ComHelpers.Object(() => ComDispatch.Call(document, "GetWorkbench", "SPAWorkbench"));
            selection = ComHelpers.Object(() => ComDispatch.Get(document, "Selection"));
            if (spa is null || selection is null) return [];
            var count = Math.Min(ComHelpers.Int(() => ComDispatch.Get(selection, "Count2")), maxItems);
            var records = new List<MeasurementSnapshot>();
            for (var index = 1; index <= count; index++)
            {
                object? selected = null;
                object? reference = null;
                object? measurable = null;
                object? valueObject = null;
                try
                {
                    selected = ComHelpers.Object(() => ComDispatch.Call(selection, "Item2", index));
                    if (selected is null) continue;
                    valueObject = ComHelpers.Object(() => ComDispatch.Get(selected, "Value"));
                    var name = ComHelpers.Text(() => valueObject is null ? null : ComDispatch.Get(valueObject, "Name"));
                    if (string.IsNullOrWhiteSpace(name)) name = $"Selection.{index}";
                    reference = ComHelpers.Object(() => ComDispatch.Get(selected, "Reference"));
                    if (reference is null)
                    {
                        warnings.Add($"{name} 没有可供 SPA 测量的 Reference；请使用 CATIA 原生 Measure Item/Between。");
                        continue;
                    }
                    measurable = ComHelpers.Object(() => ComDispatch.Call(spa, "GetMeasurable", reference));
                    if (measurable is null)
                    {
                        warnings.Add($"{name} 属于 CGR/可视化表示，SPA GetMeasurable 不可用；请使用 CATIA 原生 Measure Item/Between。");
                        continue;
                    }

                    var source = ComHelpers.Text(() => ComDispatch.Get(reference, "DisplayName"));
                    if (string.IsNullOrWhiteSpace(source)) source = name;
                    var geometryType = GeometryTypeName(ComHelpers.Int(() => ComDispatch.Get(measurable, "GeometryName")));
                    var coordinates = Scale(ReadCoordinates(measurable, "GetCenter", 3), 1000d);
                    if (coordinates.Count == 0) coordinates = Scale(ReadCoordinates(measurable, "GetPoint", 3), 1000d);
                    var direction = ReadCoordinates(measurable, "GetAxis", 3);
                    if (direction.Count == 0) direction = ReadCoordinates(measurable, "GetDirection", 3);

                    var beforeCount = records.Count;
                    AddScalar(records, measurable, name, "radius", "Radius", "mm", 1000d, geometryType, source, coordinates, direction);
                    AddDerivedDiameter(records, measurable, name, geometryType, source, coordinates, direction);
                    AddScalar(records, measurable, name, "length", "Length", "mm", 1000d, geometryType, source, coordinates, direction);
                    AddScalar(records, measurable, name, "perimeter", "Perimeter", "mm", 1000d, geometryType, source, coordinates, direction);
                    AddScalar(records, measurable, name, "area", "Area", "mm²", 1_000_000d, geometryType, source, coordinates, direction);
                    AddScalar(records, measurable, name, "volume", "Volume", "mm³", 1_000_000_000d, geometryType, source, coordinates, direction);
                    AddScalar(records, measurable, name, "angle", "Angle", "rad", 1d, geometryType, source, coordinates, direction);
                    if (records.Count == beforeCount)
                        warnings.Add($"{name} 可建立 Measurable，但没有公开可读取的标量尺寸；请使用 CATIA 原生 Measure Item/Between。");
                }
                catch (Exception ex)
                {
                    warnings.Add($"读取选择项 {index} 的 SPA 尺寸失败：{ex.Message}");
                }
                finally
                {
                    ComHelpers.Release(measurable);
                    ComHelpers.Release(reference);
                    ComHelpers.Release(valueObject);
                    ComHelpers.Release(selected);
                }
            }
            return records;
        }
        catch (Exception ex)
        {
            warnings.Add($"读取当前选择的几何测量失败：{ex.Message}");
            return [];
        }
        finally
        {
            ComHelpers.Release(selection);
            ComHelpers.Release(spa);
        }
    }

    private static IReadOnlyList<MeasurementSnapshot> CaptureSelectedPairDistance(
        object document,
        List<string> warnings)
    {
        object? spa = null;
        object? selection = null;
        object? firstSelected = null;
        object? secondSelected = null;
        object? firstReference = null;
        object? secondReference = null;
        object? measurable = null;
        try
        {
            spa = ComHelpers.Object(() => ComDispatch.Call(document, "GetWorkbench", "SPAWorkbench"));
            selection = ComHelpers.Object(() => ComDispatch.Get(document, "Selection"));
            if (spa is null || selection is null || ComHelpers.Int(() => ComDispatch.Get(selection, "Count2")) != 2)
                return [];
            firstSelected = ComHelpers.Object(() => ComDispatch.Call(selection, "Item2", 1));
            secondSelected = ComHelpers.Object(() => ComDispatch.Call(selection, "Item2", 2));
            if (firstSelected is null || secondSelected is null) return [];
            firstReference = ComHelpers.Object(() => ComDispatch.Get(firstSelected, "Reference"));
            secondReference = ComHelpers.Object(() => ComDispatch.Get(secondSelected, "Reference"));
            if (firstReference is null || secondReference is null) return [];
            measurable = ComHelpers.Object(() => ComDispatch.Call(spa, "GetMeasurable", firstReference));
            if (measurable is null || !TryCallDouble(measurable, "GetMinimumDistance", secondReference, out var distance))
                return [];

            var firstName = ComHelpers.Text(() => ComDispatch.Get(firstReference, "DisplayName"));
            var secondName = ComHelpers.Text(() => ComDispatch.Get(secondReference, "DisplayName"));
            var source = $"{firstName} ↔ {secondName}".Trim();
            var points = Scale(ReadCoordinates(measurable, "GetMinimumDistancePoints", 6, secondReference), 1000d);
            IReadOnlyList<double> direction = points.Count == 6
                ? [points[3] - points[0], points[4] - points[1], points[5] - points[2]]
                : [];
            return
            [
                new MeasurementSnapshot(
                    StableMeasurementId($"selection-between|{source}"),
                    source,
                    "minimumDistance",
                    distance * 1000d,
                    "mm",
                    "Distance",
                    source,
                    points,
                    direction,
                    null,
                    "CATIA SPA Measurable minimum distance from two current selections; SI converted to mm")
            ];
        }
        catch (Exception ex)
        {
            warnings.Add($"读取两个选择对象的最小距离失败：{ex.Message}");
            return [];
        }
        finally
        {
            ComHelpers.Release(measurable);
            ComHelpers.Release(secondReference);
            ComHelpers.Release(firstReference);
            ComHelpers.Release(secondSelected);
            ComHelpers.Release(firstSelected);
            ComHelpers.Release(selection);
            ComHelpers.Release(spa);
        }
    }

    private static void AddScalar(
        List<MeasurementSnapshot> records,
        object measurable,
        string name,
        string quantity,
        string property,
        string unit,
        double scale,
        string geometryType,
        string source,
        IReadOnlyList<double> coordinates,
        IReadOnlyList<double> direction)
    {
        if (!TryReadDouble(measurable, property, out var value) || !double.IsFinite(value)) return;
        records.Add(new(
            StableMeasurementId($"selection|{source}|{quantity}"),
            name,
            quantity,
            value * scale,
            unit,
            geometryType,
            source,
            coordinates,
            direction,
            null,
            "CATIA SPA Measurable from current selection; SI converted to displayed unit"));
    }

    private static void AddDerivedDiameter(
        List<MeasurementSnapshot> records,
        object measurable,
        string name,
        string geometryType,
        string source,
        IReadOnlyList<double> coordinates,
        IReadOnlyList<double> direction)
    {
        if (!TryReadDouble(measurable, "Radius", out var radius) || !double.IsFinite(radius)) return;
        records.Add(new(
            StableMeasurementId($"selection|{source}|diameter"),
            name,
            "diameter",
            radius * 2000d,
            "mm",
            geometryType,
            source,
            coordinates,
            direction,
            null,
            "CATIA SPA Measurable; diameter derived exactly as 2 × radius; SI converted to mm"));
    }

    private static bool TryReadDouble(object target, string property, out double value)
    {
        try
        {
            value = Convert.ToDouble(ComDispatch.Get(target, property));
            return double.IsFinite(value);
        }
        catch
        {
            value = 0;
            return false;
        }
    }

    private static bool TryCallDouble(object target, string method, object argument, out double value)
    {
        try
        {
            value = Convert.ToDouble(ComDispatch.Call(target, method, argument));
            return double.IsFinite(value);
        }
        catch
        {
            value = 0;
            return false;
        }
    }

    private static IReadOnlyList<double> ReadCoordinates(object target, string method, int count)
        => ReadCoordinates(target, method, count, null);

    private static IReadOnlyList<double> ReadCoordinates(object target, string method, int count, object? argument)
    {
        try
        {
            object values = Enumerable.Repeat<object>(0d, count).ToArray();
            if (argument is null) ComDispatch.Call(target, method, values);
            else ComDispatch.Call(target, method, argument, values);
            return ((object[])values).Select(Convert.ToDouble).Take(count).ToArray();
        }
        catch { return []; }
    }

    private static IReadOnlyList<double> Scale(IReadOnlyList<double> values, double factor) =>
        values.Select(x => x * factor).ToArray();

    private static string ReadObjectName(object target, string property)
    {
        object? value = ComHelpers.Object(() => ComDispatch.Get(target, property));
        try { return ComHelpers.Text(() => value is null ? null : ComDispatch.Get(value, "Name")); }
        finally { ComHelpers.Release(value); }
    }

    private static string GeometryTypeName(int value) => value switch
    {
        2 => "Volume",
        3 => "Surface",
        4 => "Cylinder",
        5 => "Sphere",
        6 => "Cone",
        7 => "Plane",
        8 => "Curve",
        9 => "Circle",
        10 => "Line",
        11 => "Point",
        12 => "AxisSystem",
        _ => "Unknown"
    };

    private static string StableMeasurementId(string source)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(source);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..24];
    }

    private static IReadOnlyList<SelectedObjectSnapshot> CaptureSelection(object document, out bool truncated)
    {
        truncated = false;
        object? selectionObject = ComHelpers.Object(() => ComDispatch.Get(document, "Selection"));
        if (selectionObject is null) return [];
        try
        {
            var total = ComHelpers.Int(() => ComDispatch.Get(selectionObject, "Count2"));
            var count = Math.Min(total, MaxSelectionItems);
            truncated = total > count;
            var result = new List<SelectedObjectSnapshot>(count);
            for (var index = 1; index <= count; index++)
            {
                object? selectedObject = ComHelpers.Object(() => ComDispatch.Call(selectionObject, "Item2", index));
                if (selectedObject is null) continue;
                try
                {
                    var value = ComHelpers.Object(() => ComDispatch.Get(selectedObject, "Value"));
                    var valueName = ComHelpers.Text(() => value is null ? "" : ComDispatch.Get(value, "Name"));
                    var parentPath = CaptureParentPath(value);
                    var coordinates = CaptureCoordinates(selectedObject);
                    object? reference = ComHelpers.Object(() => ComDispatch.Get(selectedObject, "Reference"));
                    object? leafProduct = ComHelpers.Object(() => ComDispatch.Get(selectedObject, "LeafProduct"));
                    try
                    {
                        result.Add(new(
                            ComHelpers.Text(() => ComDispatch.Get(selectedObject, "Type")),
                            string.IsNullOrWhiteSpace(valueName)
                                ? ComHelpers.Text(() => ComDispatch.Get(selectedObject, "Name"))
                                : valueName,
                            parentPath,
                            coordinates,
                            ComHelpers.Text(() => reference is null ? "" : ComDispatch.Get(reference, "DisplayName")),
                            ComHelpers.Text(() => leafProduct is null ? "" : ComDispatch.Get(leafProduct, "Name"))));
                    }
                    finally
                    {
                        ComHelpers.Release(reference);
                        ComHelpers.Release(leafProduct);
                    }
                }
                finally { ComHelpers.Release(selectedObject); }
            }
            return result;
        }
        finally { ComHelpers.Release(selectionObject); }
    }

    private static IReadOnlyList<double> CaptureCoordinates(object selected)
    {
        try
        {
            object values = new object[] { 0d, 0d, 0d };
            ComDispatch.Call(selected, "GetCoordinates", values);
            return ((object[])values).Select(Convert.ToDouble).Take(3).ToArray();
        }
        catch { return []; }
    }

    private static IReadOnlyList<string> CaptureParentPath(object? value)
    {
        var path = new List<string>();
        var current = value;
        try
        {
            for (var depth = 0; current is not null && depth < 8; depth++)
            {
                var name = ComHelpers.Text(() => ComDispatch.Get(current, "Name"));
                if (!string.IsNullOrWhiteSpace(name) &&
                    !name.Equals("CNEXT", StringComparison.OrdinalIgnoreCase) &&
                    (path.Count == 0 || !path[^1].Equals(name, StringComparison.OrdinalIgnoreCase)))
                    path.Add(name);
                var parent = ComHelpers.Object(() => ComDispatch.Get(current, "Parent"));
                if (!ReferenceEquals(current, value)) ComHelpers.Release(current);
                current = parent;
            }
        }
        finally
        {
            if (!ReferenceEquals(current, value)) ComHelpers.Release(current);
            ComHelpers.Release(value);
        }
        path.Reverse();
        return path;
    }

    private static IReadOnlyList<TreeNodeSnapshot> CapturePart(object document, TreeBudget budget, out string inWorkObject)
    {
        inWorkObject = "";
        object? partObject = ComHelpers.Object(() => ComDispatch.Get(document, "Part"));
        if (partObject is null) return [];
        try
        {
            object? inWork = ComHelpers.Object(() => ComDispatch.Get(partObject, "InWorkObject"));
            try { inWorkObject = ComHelpers.Text(() => inWork is null ? "" : ComDispatch.Get(inWork, "Name")); }
            finally { ComHelpers.Release(inWork); }

            var roots = new List<TreeNodeSnapshot>();
            AppendCollection(roots, ComHelpers.Object(() => ComDispatch.Get(partObject, "Bodies")), "Body", 0, budget, CapturePartChildren);
            AppendCollection(roots, ComHelpers.Object(() => ComDispatch.Get(partObject, "HybridBodies")), "HybridBody", 0, budget, CapturePartChildren);
            return roots;
        }
        finally { ComHelpers.Release(partObject); }
    }

    private static IReadOnlyList<TreeNodeSnapshot> CaptureProduct(object document, TreeBudget budget)
    {
        object? productObject = ComHelpers.Object(() => ComDispatch.Get(document, "Product"));
        if (productObject is null) return [];
        try
        {
            return [CaptureProductNode(productObject, 0, budget)];
        }
        finally { ComHelpers.Release(productObject); }
    }

    private static TreeNodeSnapshot CaptureProductNode(object productObject, int depth, TreeBudget budget)
    {
        var name = ComHelpers.Text(() => ComDispatch.Get(productObject, "Name"));
        var partNumber = ComHelpers.Text(() => ComDispatch.Get(productObject, "PartNumber"));
        var display = string.IsNullOrWhiteSpace(partNumber) ? name : $"{name} [{partNumber}]";
        var children = new List<TreeNodeSnapshot>();
        if (depth < MaxTreeDepth && budget.TryTake())
            AppendCollection(children, ComHelpers.Object(() => ComDispatch.Get(productObject, "Products")), "Product", depth + 1, budget,
                (item, childDepth, childBudget) => CaptureProductNode(item, childDepth, childBudget));
        else if (depth >= MaxTreeDepth) budget.Truncated = true;
        return new(display, "Product", children);
    }

    private static TreeNodeSnapshot CapturePartChildren(object item, int depth, TreeBudget budget)
    {
        var children = new List<TreeNodeSnapshot>();
        if (depth < MaxTreeDepth)
        {
            AppendCollection(children, ComHelpers.Object(() => ComDispatch.Get(item, "Shapes")), "Shape", depth + 1, budget, CaptureLeaf);
            AppendCollection(children, ComHelpers.Object(() => ComDispatch.Get(item, "Sketches")), "Sketch", depth + 1, budget, CaptureLeaf);
            AppendCollection(children, ComHelpers.Object(() => ComDispatch.Get(item, "HybridShapes")), "HybridShape", depth + 1, budget, CaptureLeaf);
            AppendCollection(children, ComHelpers.Object(() => ComDispatch.Get(item, "HybridBodies")), "HybridBody", depth + 1, budget, CapturePartChildren);
        }
        else budget.Truncated = true;
        return new(ComHelpers.Text(() => ComDispatch.Get(item, "Name")), "FeatureContainer", children);
    }

    private static TreeNodeSnapshot CaptureLeaf(object item, int _, TreeBudget __)
    {
        return new(ComHelpers.Text(() => ComDispatch.Get(item, "Name")), "Feature", []);
    }

    private static void AppendCollection(
        List<TreeNodeSnapshot> target,
        object? collectionObject,
        string fallbackKind,
        int depth,
        TreeBudget budget,
        Func<object, int, TreeBudget, TreeNodeSnapshot> factory)
    {
        if (collectionObject is null) return;
        try
        {
            var count = ComHelpers.Int(() => ComDispatch.Get(collectionObject, "Count"));
            for (var i = 1; i <= count; i++)
            {
                if (!budget.TryTake()) break;
                object? item = ComHelpers.Object(() => ComDispatch.Call(collectionObject, "Item", i));
                if (item is null) continue;
                try
                {
                    var node = factory(item, depth, budget);
                    target.Add(string.IsNullOrWhiteSpace(node.Kind) ? node with { Kind = fallbackKind } : node);
                }
                finally { ComHelpers.Release(item); }
            }
        }
        finally { ComHelpers.Release(collectionObject); }
    }

    private object EnsureApplication()
    {
        if (_application is not null) return _application;
        object? app;
        try
        {
            app = Microsoft.VisualBasic.Interaction.GetObject(null, "CATIA.Application");
        }
        catch { app = null; }
        if (app is null)
        {
            var clsid = Guid.Empty;
            NativeMethods.CLSIDFromProgID("CATIA.Application", out clsid);
            NativeMethods.GetActiveObject(ref clsid, IntPtr.Zero, out app);
        }
        _application = app ?? throw new COMException("未发现正在运行的 CATIA 实例。");
        return _application;
    }

    private void DropApplication()
    {
        ComHelpers.Release(_application);
        _application = null;
    }

    public async ValueTask DisposeAsync()
    {
        await _sta.InvokeAsync(() => { DropApplication(); return true; }).ConfigureAwait(false);
        await _sta.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class TreeBudget(int remaining)
    {
        private int _remaining = remaining;
        public bool Truncated { get; set; }
        public bool TryTake()
        {
            if (_remaining <= 0) { Truncated = true; return false; }
            _remaining--;
            return true;
        }
    }

    private static class NativeMethods
    {
        [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        internal static extern void CLSIDFromProgID(string progId, out Guid clsid);

        [DllImport("oleaut32.dll", PreserveSig = false)]
        internal static extern void GetActiveObject(
            ref Guid rclsid,
            IntPtr reserved,
            [MarshalAs(UnmanagedType.IUnknown)] out object? ppunk);
    }
}
