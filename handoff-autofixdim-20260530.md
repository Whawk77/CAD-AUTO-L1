# AutoFixtureDim 交接摘要

## 当前任务背景

项目路径：`D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340`

用户正在调 AutoCAD .NET 插件的尺寸自动标注规则，重点是 `TopStructWidth` / 顶部斜边、平台、忽略点诊断。用户明确要求：后续不要主动编译源码；他们自己运行 `cad-test` 或 `ASD4` 验证。

## 用户约束

- 根目录 `AGENTS.md`：禁止批量删除文件/目录，不要使用 `del /s`、`rd /s`、`rmdir /s`、`Remove-Item -Recurse`、`rm -rf`。
- 需要删除文件时只能一次删除一个明确路径。
- 用户之前要求：Codex 不主动编译源码。除非用户明确要求，不要运行构建。
- 普通 AutoCAD 交互方式不能被 `run-cad-test.ps1` 支持逻辑污染。

## 当前未提交状态

`git status --short` 显示：

- `M acad.slg`
- `M autocad-net-c-autocad-autocad-net-source-backup-20260522-1340/Commands.cs`
- `M autocad-net-c-autocad-autocad-net-source-backup-20260522-1340/DimensionDrawer.cs`
- `M autocad-net-c-autocad-autocad-net-source-backup-20260522-1340/scripts/cad-test.scr`

主要代码改动在 `Commands.cs` 和 `DimensionDrawer.cs`。`acad.slg`、`scripts/cad-test.scr` 可能是运行/脚本生成产物，处理前先问用户。

## 已做改动概览

### 1. ASD4 增加方向选择

文件：`Commands.cs`

- `ASD4` 仍调用 `RunAutoFixDim(clearExistingBeforeGenerate: true, diagnosticsEnabled: true)`。
- 在 `RunAutoFixDim` 开始处新增：
  - `PromptForDiagnosticSide(editor)`
  - 选项：`All / Top / Bottom / Left / Right`
- 诊断模式下传入 `DimensionDrawer(..., diagnosticsEnabled: true, diagnosticSide: selected)`。
- 诊断模式下跳过后续交互式引线/孔径引线，避免干扰单方向排查。

注意：`PromptForDiagnosticSide` 的中文提示是新写的，文件中已有一些乱码中文消息，未整理。

### 2. DimensionDrawer 增加诊断方向过滤

文件：`DimensionDrawer.cs`

新增 enum：

```csharp
public enum DiagnosticDimensionSide
{
    All,
    Bottom,
    Top,
    Left,
    Right
}
```

构造函数新增参数：

```csharp
DiagnosticDimensionSide diagnosticSide = DiagnosticDimensionSide.All
```

新增字段：`_diagnosticSide`。

`DrawStepOutlineDimensions()` 中按诊断方向决定是否执行：

- Top: `DrawTopStepWidth(outline)`
- Bottom: `DrawLowerRightStepWidth(outline)`
- Left: `DrawRightStepHeight(outline)`
- Right: `DrawRightSideStepHeight(outline)`

`FlushStackedDimensions()` 中按诊断方向只 flush 对应 side。

### 3. 顶部诊断点元数据

文件：`DimensionDrawer.cs`

新增结构：

- `StructurePoint { Point2d Point; string Source; }`
- `IgnoredPoint { Point2d Point; string Reason; }`
- `PointDebugLabel`
- `PointDebugLabelPlacement`

Top width 现在结构点带来源，忽略点带原因。

Top 诊断标签：

- `SP:VTM` = `VerticalTopMost`
- `SP:TIE` = `TopInclinedEndpoint`
- `SP:TSE` = `TopSlopeEndpoint`
- `IG:XOUT` = `TopExtensionCrossesOutline`
- `IG:D45` = `TopDirectional45Endpoint`
- `IG:IGR` = `TopInnerGrooveSharedEndpoint`
- `IG:D45B` / `IG:IGRB` 也有映射，但主要用于 bottom 逻辑原因。

点诊断标签排布：

- `AddPointDebugLabels()` 逐点选择候选偏移位置。
- 用 `EstimatePointDebugLabelBounds()` 和 `PointDebugTextBoundsOverlap()` 避免文字重叠。
- 只画一根直线引出，不画箭头头部。

### 4. 其他方向基础诊断标签

Bottom / Left / Right 目前只做基础诊断：

- Bottom structure points: `SP:VBM`
- Left structure points: `SP:HLM`
- Right structure points: `SP:HRM`
- ignored points 暂标：`IG:UNK`

这些还没有像 Top 一样细分原因。用户后续可能会要求继续拆成 `XOUT/D45/IGR`。

### 5. 45°忽略规则收紧

新增：

```csharp
IsIgnorableFortyFiveDegreeSegment()
```

区别：

- 原 `IsFortyFiveDegreeSegment()` 允许约 8% 误差，保留给结构识别。
- 新 `IsIgnorableFortyFiveDegreeSegment()` 允许约 2% 误差，只用于“端点忽略”。

替换位置：

- `ShouldIgnoreSideDirectionalInclinedEndpoint`
- `ShouldIgnoreSideInnerGrooveEndpoint`
- `GetDirectionalInclinedIgnoreReason`

目标：非 45°斜边端点不应进入 ignoredPoints。

### 6. 内槽共享点误吞修正

`IsInnerGrooveIgnoredEndpoint()` 现在不仅要求共享水平点，还要求该点也是当前 Top/Bottom 方向下的方向性忽略端点：

```csharp
return sharedPoint.HasValue
    && PointsEqual(point, sharedPoint.Value)
    && IsDirectionalIgnoredEndpoint(point, chamfer, invertDirection: isTopSide);
```

修复目标：顶部 `/` 斜边左下端点不应因连接水平线被误判为内槽共享忽略点。

### 7. 新增 Top Slope Endpoint 规则

新增函数：

- `AddTopSlopeEndpointStructurePoints`
- `IsTopSlopeEndpointStructurePoint`
- `AddTopSlopeEndpointStructurePoint`
- `EndpointConnectsHorizontalSegment`
- `EndpointConnectsHigherHorizontalSegment`
- `IsPointOnHorizontalSegment`

设计目标：把非水平/非竖直斜边的低端点加入 Top width，来源显示 `SP:TSE`。

当前规则：

- 遍历非水平、非竖直、非 arc chord 的 segment。
- low = Y 更低端点，high = 另一端。
- low 不在 ignoredPoints，不在 envelope side。
- high.Y > low.Y。
- low 落在某条水平段上。
- high 落在更高位置的水平段上。

最近一次用户反馈：“没有命中”。已将“水平连接”从只认端点 `SegmentTouchesPoint()` 改为 `IsPointOnHorizontalSegment()`，即点落在水平段中间也算连接。尚未由用户验证。

## 当前可能的问题/注意点

1. `SP:TSE` 仍可能不命中

可能原因：

- `low` 被 `IsEnvelopeSidePoint(low, outline)` 挡掉。
- `high` 没有落在更高水平段上。
- 图形识别中该斜边被拆成了多段或端点坐标与水平段不共线。
- `IsPointOnHorizontalSegment()` 使用 `_config.GeometryTolerance`，如果 CAD 几何有小错位，可能仍过严。

下一步建议：给 `TSE` 未命中原因也加诊断，例如 `MISS:TSE:HLOW/HHIGH/ENV/IGN`，或者先临时把候选斜边两端标出来。

2. Top 的 `SP:VTM` 来源仍然太宽

用户已指出一些右侧结构点不该进入 Top。当前代码仍会把任何竖线组最高点作为 `SP:VTM`，只要没有被 ignoredPoints 排掉。后续应收紧 Top VTM 的语义，例如要求附近有顶部水平边/顶部平台结构，避免右侧高度结构混入 top width。

3. 其他方向诊断还很粗

Bottom/Left/Right 只加了基础结构点/忽略点标签，没有原因细分。用户已经希望四个方向都能诊断，后续可能需要把 Top 的 `IgnoredPoint` 元数据模型扩展到三个方向。

4. `ASD4` 选择方向后“只生成那个方向”目前主要针对 step outline dimensions 和 flush side

普通 hole/slot dimensions 是否完全被抑制：`DrawLinearDimensions()` 仍调用 `DrawHolePositionDimensions` / `DrawSlotDimensions`，但最后 flush 只 flush 所选 side，所以其他 side 不输出。仍需用户验证是否符合“仅生成那个方向”的预期。如果希望诊断只看外轮廓 step，应在诊断模式下跳过 hole/slot dimension generation。

5. 未编译

按用户要求，所有改动只做源码和 `git diff --check`，没有构建。用户应运行 `cad-test` 或 `ASD4` 验证。

## 验证命令/流程

用户通常运行：

```powershell
.\cad-test
```

或在 AutoCAD 中运行：

```text
ASD4
```

`ASD4` 会清理旧标注并提示方向。

## 建议下一步

1. 让用户用 `ASD4 -> Top` 重新测试刚修过的 `SP:TSE`。
2. 如果仍无 `SP:TSE`，先不要猜，给 `TSE` 增加失败原因诊断。
3. 再处理 Top `SP:VTM` 过宽的问题：把明显右侧/左侧高度结构点排除出 Top width。
4. 若用户要求四方向同等级诊断，再把 `IgnoredPoint` 元数据从 Top 扩展到 Bottom/Left/Right。

## 重要文件定位

- `Commands.cs`
  - `ASD4`: line around 30
  - `RunAutoFixDim`: around 63
  - `PromptForDiagnosticSide`: around 289
  - `DrawLinearDimensions`: around 611

- `DimensionDrawer.cs`
  - `DiagnosticDimensionSide`: top of file
  - `DrawStepOutlineDimensions`: around 209
  - `DrawTopStepWidth`: around 2353
  - `BuildTopSideHorizontalStructurePoints`: around 2468
  - `AddTopSlopeEndpointStructurePoints`: around 3113
  - `GetDirectionalInclinedIgnoreReason`: around 3571
  - `IsIgnorableFortyFiveDegreeSegment`: around 4014
  - `FlushStackedDimensions`: around 4897
  - point debug labels: around 5432
