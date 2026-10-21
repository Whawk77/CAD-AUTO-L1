# 外轮廓标注代码分析包

日期：2026-09-10  
用途：交给下一位 agent 做外轮廓标注问题分析。本文只整理现状，不提出未经诊断授权的源码修改。

## 1. 分析边界

包含：

- 外轮廓选择与主轮廓识别；
- Overall 宽/高；
- 外轮廓台阶、结构宽高、OutlineSegment；
- 四侧抑制、重复消除、布局、渲染和最终校验。

不包含：孔、销孔、槽定位的具体规则，以及角点引线；它们只在调用链中作为输入或后续阶段出现。

## 2. 当前快照

- 实际 cwd：`D:\work\AI\9.1 托压块版本`
- 分支：`codex/non-occluding-centerline-rule`
- 整理前 Git 工作树：干净。
- `graphify-out\graph.json` 存在；已按“外轮廓尺寸如何识别、规划、抑制、布局和绘制”查询过图谱。
- 没有编译、运行测试、启动 AutoCAD 或执行 CAD 脚本。
- `docs\Environment.md` 中记录的旧 checkout 路径不是本次实际 cwd，以当前实际路径为准。

## 3. 真实调用链

```text
ASD / AUTOFIXDIM
  -> Commands.RunAutoFixDim
  -> GeometryCollector.PromptForOutlineSelection
  -> FeatureRecognizer.RecognizeOutline
  -> CadToCoreModelMapper.ToCoreOutline
  -> DimensionPlanner.CreateDimensionPlan
       -> CreateOutlinePlanLocal
            -> Overall 宽/高
            -> FeatureFirst 结构链
               或 legacy 四侧结构链
            -> 原始 OutlineSegment
            -> LProfile 规则
            -> SuppressDuplicateDimensions
            -> DimensionPlanPostValidator
  -> DimensionDrawer.DrawDimensionPlan
  -> DimensionDrawer.FlushStackedDimensions
  -> DimensionLayoutRules.CreateStackingPlan
  -> DimensionDrawer.RenderPlacedDimensions
```

关键入口：`Commands.cs:716-873`；计划绘制入口：`Commands.cs:1172-1183`。

## 4. 当前最重要的分支事实

| 场景 | 配置 | 实际路径 |
| --- | --- | --- |
| 正常 ASD / AUTOFIXDIM | `Commands.cs:725-728` 设置 `UseFeatureFirstStructurePipeline = true` | FeatureFirst 结构识别、选择、放置 |
| ASDCOREDBG | `Commands.cs:604-614` 使用默认配置 | 默认走 legacy `AddStepOutlineDimensions` |
| 完整 Core 计划 | `DimensionPlanner.CreateDimensionPlan` | 轮廓计划完成后还会加入孔/槽，再次做抑制和校验 |

`Commands.cs:726-727` 的注释仍描述旧状态，但下一行已打开 FeatureFirst；不要只按注释判断生产路径。

FeatureFirst 分支进入 `DimensionPlanner.Suppression.cs:56-79` 后直接返回，不会执行 legacy 分支中的 `ArbitrateOuterContourStepOverallRemainders`。遇到 `OuterContourStepOverallRemainder` 相关差异时，先确认实际运行的是哪条分支。

## 5. 文件地图与职责

| 阶段 | 文件 | 重点符号 |
| --- | --- | --- |
| 命令编排 | `Commands.cs` | `RunAutoFixDim`、`RunCoreDebug`、`DrawLinearDimensions` |
| 选择过滤 | `CadAuto.CadAdapter/Collection/GeometryCollector.cs` | `PromptForOutlineSelection`、`SelectMainOutlineComponent` |
| CAD 轮廓模型 | `CadAuto.CadAdapter/Model/OutlineSelection.cs`、`OutlineFeature.cs`、`OutlineSegment.cs`、`OutlineArc.cs` | 选择集、顶点、线段、弧段 |
| 生产识别 | `CadAuto.CadAdapter/Recognition/FeatureRecognizer.cs` | `RecognizeOutline`、`AddEntityKeyPoints`、`AddArcChord`、倒角/圆角识别 |
| CAD -> Core | `CadAuto.CadAdapter/Mapping/CadToCoreModelMapper.cs` | `ToCoreOutline` |
| Core 轮廓模型 | `CadAuto.Core/Model/OutlineFeature2D.cs` | Min/Max、Vertices、Segments、Arcs、Chamfers、Fillets |
| 主规划器 | `CadAuto.Core/Planning/DimensionPlanner.cs` | `CreateOutlinePlanLocal`、`CreateDimensionPlan`、`AddDimension` |
| legacy 结构 | `CadAuto.Core/Planning/DimensionPlanner.Structure.cs` | `AddStepOutlineDimensions`、四侧结构尺寸、互补余量 |
| FeatureFirst 识别 | `CadAuto.Core/Planning/StructureFeatureExtractor.cs` | 台阶/凹槽/外包络残差/链分类 |
| FeatureFirst 选择 | `CadAuto.Core/Planning/StructureMeasurementSelector.cs` | 链选择、整体分割、嵌套和余量规则 |
| FeatureFirst 放置 | `CadAuto.Core/Planning/StructurePlacementAdapter.cs` | 结构 feature -> `PlannedDimension` |
| 抑制 | `CadAuto.Core/Planning/DimensionPlanner.Suppression.cs` | 局部外包络、Overall 分割、镜像和重复抑制 |
| 轮廓边界几何 | `CadAuto.Core/Rules/OutlineGeometryQuery.cs` | 真实边界、水平/垂直边界点、包络候选 |
| 布局 | `CadAuto.Core/Rules/DimensionLayoutRules.cs` | 侧向堆叠、层级、外移、文字/轮廓冲突 |
| 最终校验 | `CadAuto.Core/Rules/DimensionPlanPostValidator.cs` | Overall、真实附着点、零长度、包络一致性 |
| CAD 映射/绘制 | `CadAuto.CadAdapter/Rendering/DimensionPlanCadMapper.cs`、`DimensionDrawer.cs` | 计划 -> CAD 尺寸实体，侧向 flush 和实际绘制 |
| Core 回归入口 | `CadAuto.Core.Tests/Program.cs` | 外轮廓、四侧、旋转不变性、LProfile 用例 |

生产 `FeatureRecognizer` 与测试用 `CadAuto.Core/Recognition/FeatureRecognizer2D.cs` 是两套实现；Core 测试识别通过不等于生产 CAD 识别通过。

## 6. 外轮廓识别链

### 6.1 选择集

`GeometryCollector.PromptForOutlineSelection`（约 `:41-125`）做四件事：

1. 用户选择实体；
2. 优先保留 `DRAWING` 图层且 linetype 不像 `CENTER/HIDDEN/DASH/DOT/PHANTOM` 的实体；
3. 若有闭合多段线，按候选评分选主多段线；
4. 否则按端点约 `0.01` 的连接关系做 BFS，只返回得分最高的连通分量，不把所有选择对象当成轮廓。

候选评分在 `GeometryCollector.cs:481-548`，连接判断在 `:571-589`。评分综合图层语义、线宽、闭合性、范围、长度和连续性。

### 6.2 生产 CAD 识别

`FeatureRecognizer.RecognizeOutline`：

- 闭合 `Polyline/Polyline2d` 走 `RecognizeLightweightOutline` / `RecognizePolyline2dOutline`（约 `:809-876`）；
- 普通选择集走 `AddEntityKeyPoints`（约 `:889-1012`），支持 Line、Arc、Polyline/Polyline2d 的真实线段和弧；
- Arc 会加入端点、真实 `OutlineArc` 和圆弧四个基准极值候选；
- polyline bulge 会转成 `OutlineArc`，同时保留弦段标记 `IsArcChord=true`；
- Circle 在 fallback 中只暂存四个象限点，若没有真实线段/弧段，随后会被移除，因此裸圆不会单独构成外轮廓。

包络 Min/Max 是通过真实顶点、弧端点和弧的 cardinal extrema 更新。Core 侧的 `OutlineGeometryQuery.TryGetEnvelope` 会再次从真实几何推导包络；弧弦段不作为真实外边界。

### 6.3 映射

`CadToCoreModelMapper.ToCoreOutline`（约 `:11-70`）复制 Min/Max、顶点、线段、弧、倒角和圆角；线段/弧的 CAD `SourceId` 被保留到 Core `SourceKey`，便于追踪来源。

## 7. 计划生成

`DimensionPlanner.CreateOutlinePlanLocal`（`DimensionPlanner.cs:217-251`）顺序：

1. 建立局部坐标系并清空 loose chain；
2. 检测 LProfile；
3. 先加入 Overall width/height；
4. 按配置选择 FeatureFirst 或 legacy 结构路径；
5. 加入原始水平/垂直 `OutlineSegment`；
6. 应用 LProfile 规则；
7. 运行抑制；
8. 运行 `DimensionPlanPostValidator` 并捕获最终尺寸。

`AddOverallWidth/Height` 在 `DimensionPlanner.Structure.cs:13-47`：端点来自真实左/右/下/上边界，`ForceOuterLevel=true`，整体尺寸不使用分段延长线。

`AddDimension` 在 `DimensionPlanner.cs:745-772` 统一创建候选，跳过零长度和低于容差的跨度；普通非 Overall 尺寸默认 `UseSegmentedExtensionLines=true`。

所有要求附着到轮廓的尺寸都要经过 `OutlineGeometryQuery.IsPointOnBoundary` 或真实水平/垂直边界搜索；不能用理论交点代替真实几何。相关辅助逻辑在 `DimensionPlanner.cs:638-710`。

## 8. 两套结构规划路径

### 8.1 FeatureFirst（当前正常 ASD）

调用：

```text
StructureFeatureExtractor.Extract
  -> StructureMeasurementSelector.Select
  -> StructurePlacementAdapter.Place
  -> plan.Add
```

核心位置：

- `StructureFeatureExtractor.cs:25-174`：从轴对齐真实边、微间隙、圆角桥接和包络残差恢复 RealStep、NotchOpening、BodyRemainder、NoiseTip，并分配链；
- `StructureMeasurementSelector.cs:22-47`：先收集平衡台阶链，再做噪声/残差、每轴链选择、单一 Overall、嵌套、开链/覆盖链、重复和 body remainder 规则；
- `StructurePlacementAdapter.cs:21-117`：将保留 feature 映射到 Top/Bottom/Left/Right 结构角色、对齐键和局部放置标志；Overall 单独放置。

结构 feature 的 `PreferredSide` 是放置提示，不是唯一选择条件；判断应同时看 `Axis/T0/T1/CrossPosition/ChainId/Keep/SourceKey/SupportSourceKeys`。

### 8.2 legacy（当前 ASDCOREDBG 默认路径）

`DimensionPlanner.Structure.cs:49-172`：

- 先处理原始线段尺寸，跳过弧弦和 Overall 边界线；
- `ResolveHorizontalOutlineSegmentSide` / `ResolveVerticalOutlineSegmentSide` 用包络边、raycast 和最近包络判断侧；
- `AddStepOutlineDimensions` 依次处理 Top/Bottom/Left/Right；
- 左侧还有 datum-rooted 外轮廓高度，顶部有 chamfered step 组合尺寸。

四侧 builder 约在：Top `:302-334`、Left `:393-444`、Right `:446-498`、Bottom `:1260-1300`。Bottom builder 明确保留了“不做 complementary remainder snap/removal，因为 Bottom 是 datum side”的历史分支，见 `:1292-1294`。

legacy 中大量 helper 用于轮廓恢复：侧向 inset edge 注入、微小竖段吸收、小倒角 gap bridge、contour-backed edge 判断、cross-level snap 和 complementary partition removal。不要只看四个 builder 的入口，需沿 helper 调用追踪。

## 9. 抑制规则重点

`DimensionPlanner.Suppression.cs:36-175` 说明这是多阶段、有顺序依赖的流水线；完整计划中 `SuppressDuplicateDimensions` 会运行两次，不能把第二次结果误认为第一次候选生成结果。

### FeatureFirst 抑制顺序

`SuppressDuplicateDimensions` 的 FeatureFirst 分支（约 `:56-79`）：

1. `SuppressNearOverallInsetStructureBodies`；
2. `SuppressInsetStructureComplementaryToEnvelopeArm`；
3. 四侧 `SuppressOutlineSegmentsThatPartitionOverall`；
4. 水平/垂直镜像去重；
5. `SuppressOutlineSegmentsOnOverallEnvelope`、`SuppressInteriorOutlineSegments`；
6. 同侧重复测量；
7. `SuppressLocalGeometryOnOverallEnvelope`；
8. 返回，不进入 legacy 四向 arbitration。

### legacy 抑制顺序

legacy 分支还会执行：

- structure/OutlineSegment 分割 Overall；
- datum-rooted complementary；
- `ArbitrateOuterContourStepOverallRemainders`；
- projected partition、open bottom derivable、镜像、残余 cleanup；
- multi-piece/closed chain；
- 最后 duplicate 和 local-envelope suppression。

重要方法：

- `SuppressOutlineSegmentsThatPartitionOverall`：约 `:2615`；
- `SuppressOutlineSegmentsOnOverallEnvelope`：约 `:2800`、`:2845`；
- `SuppressNearOverallInsetStructureBodies`：约 `:3118`；
- `SuppressInsetStructureComplementaryToEnvelopeArm`：约 `:3187`；
- `SuppressMirroredDuplicates`：约 `:3777`；
- `SuppressDuplicateMeasuredDimensions`：约 `:3859`；
- `SuppressStructureDimensionsThatPartitionOverall`：约 `:3990`；
- `SuppressProjectedStructureDimensionsThatPartitionOverall`：约 `:4124`；
- `FormsCompleteOverallPartition`：约 `:4341`。

### 外包络局部几何规则

`FindLocalGeometryOnOverallEnvelope`（约 `:387-456`）只针对结构宽高、`OutlineSegment`、TopChamferedStepWidth、RightChamferedStepHeight 等角色；要求候选两端落在包络边线上，且该边是完整长度边。它会保护跨侧闭链中的真实台阶，不应简单理解为“贴着包络就全部删除”。

抑制证据会写入 `PlannedDimension` / `DimensionPlan`：`RuleId`、`DebugRole`、`SourceGeometryIds`、`TopologyEvidence`、`SuppressionReason` 等字段是定位误删或漏删的首选入口。

## 10. 布局与 CAD 绘制

### 10.1 Core 布局

`DimensionLayoutRules.CreateStackingPlan`（约 `:89-195`）把计划尺寸转成四侧 layout block，应用：

- Overall 外层提升；
- 侧向堆叠和对齐 lane；
- 共享延长线对齐；
- rooted block / physical outward order；
- 文字覆盖和尺寸线进入轮廓检查。

`TryGetLocalDimLineCoordinate` 约 `:1443-1515` 会检查局部边界和尺寸线是否进入轮廓内部。不能为了视觉留白把合法附着点挪离真实轮廓；`DIMEXO` 负责延长线起始偏移。

### 10.2 renderer 链

`DimensionDrawer.DrawDimensionPlan`（约 `:894-945`）把计划按侧分桶；`FlushStackedDimensions`（约 `:579-657`）调用布局，构建 placed dimensions，再进行实际绘制。

待其他 agent 复核的一处可疑路径：`PlannedDimension.UseSegmentedExtensionLines` 会在 planner 和 `AddPlannedDimension` 中传递，但 `DimensionDrawer.RenderPlacedDimensions` 约 `:844-853` 调用 `AddRotatedDimension` 时把 `useSegmentedExtensionLines` 硬编码为 `false`。这可能影响普通外轮廓尺寸的延长线形态；目前只记录为核查点，没有改动或认定为已确认 bug。

## 11. 最终校验

`DimensionPlanPostValidator` 重点检查：

- 从真实几何重新推导包络并与存储包络对照；
- Overall width/height 各只有一个；
- Overall 端点在真实边界、`ForceOuterLevel=true`、不使用分段延长线；
- `FirstPointMustLieOnOutline` 的尺寸必须有真实附着点和正确必需坐标；
- 不允许零长度尺寸。

因此“有外侧尺寸线”或 `isAttachmentValid=true` 不能单独证明最终截图视觉正确；还要核对最终放置坐标、侧别、文本/箭头冲突和同一张图的 CAD 回放。

## 12. 回归定位入口

测试集中在 `CadAuto.Core.Tests/Program.cs`，重点按问题类型搜索：

| 类型 | 用例入口 |
| --- | --- |
| Overall/真实包络 | `RectangularOutlineKeepsOverallDimensions:589`、`ChamferedOutlineKeepsOverallDimensions:631`、`FilletedOutlineKeepsOverallDimensions:696`、`OverallDimensionsUseBoundaryGripPoints:739`、`ArcEnvelopeUsesRealInteriorGripPoint:771` |
| OutlineSegment 分割 | `OutlineSegmentsThatPartitionOverallAreSuppressed:1318`、`ThreeOutlineSegmentsThatPartitionOverallAreSuppressed:1361`、`OutlineSegmentOnOverallEnvelopeIsSuppressed:1417` |
| 结构分割/台阶 | `StructureWidthsThatPartitionOverallAreSuppressed:3229`、`BottomOuterContourStepKeeps20AndSuppresses70Body:3661`、`OutlineSegmentBodyLengthClosedByStepIsSuppressed:3799` |
| 链与余量 | `ProjectedThreePieceHorizontalChainKeepsRealSteps:4039`、`TopClosedChainKeepsRealStepsDropsUnbackedBody:4108`、`BottomMultiLevelStepsKeepFeatureChainDropBodyRemainder:4442`、`MultiPieceStructureOverallRemainderDropsLongBody:4524` |
| 四向/旋转 | `RotationSignature_OuterStepOverallStructureIsStable:4873`、`RotationSignature_ClosedChainStructureIsStable:4907`、`RotationSignature_F215...:4953-4980`、`RotationSignature_F338...:5007-5022` |
| LProfile | `LProfileAnnotationRulesUseTopology:6157`、`LProfileTopologyRejectionIsDiagnostic:6218`、`RoundedRectangleOuterContourIsNotLProfile:6229` |
| datum/侧向抑制 | `DatumRootedLeftOuterEnvelopeDimensionsAreSuppressed:6010`、`DatumRootedBottomAndLeftOuterEnvelopeDimensionsAreSuppressed:6057`、`RightArmHeightOnOverallMaxXIsSuppressed:6559` |

CAD 回归 manifest：

- `regression/core-cad/cases.json`：`OC01-bottom-outer-step-partition`；
- fixture：`regression/core-cad/fixtures/OC01-bottom-outer-step-partition.dwg`；
- 四侧布局：`regression/dimension-layout/cases.json`、`regression/dimension-layout/fixtures/DL01-four-side-dimension-layout.dwg`；
- 规则说明：`docs/DimensionRules.md`、`docs/DimensionLayoutRegression.md`、`docs/RecognitionRules.md`、`docs/RotationInvariance.md`。

## 13. 建议下一位 agent 的分析顺序

1. 先锁定同一个 checkout、DWG、DLL、RunId 和诊断报告；不要把其他 worktree 或旧 Debug-vN 的结果混进来。
2. 从异常尺寸的 `DebugRole/OwnerKind/RuleId/SourceGeometryIds/TopologyEvidence` 反查候选来源。
3. 确认当前命令是 FeatureFirst 还是 legacy；正常 ASD 与 ASDCOREDBG 不是同一路径。
4. 分段判断：选择集 -> 生产识别 -> Core 映射 -> 候选生成 -> 抑制 -> 布局 -> CAD 绘制。
5. 若异常是“尺寸缺失”，先判断是未生成、被哪条规则抑制、还是布局/renderer 未显示；若是“尺寸位置错误”，先检查真实附着点和 `DimensionLayoutRules`，不要先改识别。
6. 先用同一张图复现并收集诊断，再提出最小根因修改；Core 静态/测试结果不能代替同图 AutoCAD 截图验收。

## 14. 本次未做的事

- 未修改任何源码规则；
- 未更新项目 docs；
- 未编译 DLL；
- 未运行 Core 测试；
- 未启动 AutoCAD、未执行 CAD 回放。

