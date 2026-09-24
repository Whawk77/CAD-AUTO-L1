# Graph Report - 9.1 托压块版本  (2026-09-24)

## Corpus Check
- 151 files · ~215,817 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2566 nodes · 9246 edges · 122 communities (111 shown, 11 thin omitted)
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 671 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `dc33bfb8`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Point2D Module
- OutlineFeature2D Module
- HoleFeature2D Module
- CadEntityWriter
- DimensionPlan Module
- FeatureRecognizer2D Module
- AnnotationCase
- Assert Module
- Commands Module
- Segment2D
- DimensionDrawer Module
- DimensionPlanner
- DimensionDrawer Module
- FeatureRecognizer2D
- OutlineFeature Module
- CadAuto Core Geometry
- OutlineSegment Module
- .CreateDefault
- .AdjustShortLocalDimensionTextPositions
- HoleCalloutPlan Module
- DimensionPlanner
- .RecognizeOutlineSlotFeatures
- OutlineGeometryQuery
- FeatureRecognizer2D.cs
- StackingLayerItem Module
- .VerticalExtensionOverlapsOutlineSegment
- Commands Module
- DimensionDiagnosticReport
- .HorizontalProbeCrossesInterior
- DimensionDrawer Module
- DatumDefinition Module
- DimensionLayoutRules.cs
- HoleFeature2D
- .VerticalExtensionOverlapsOutlineSegment
- FeatureRecognizer2D.cs
- SlotArcCandidate Module
- FeatureRecognizer2D
- HoleCalloutPlanner
- CadAuto Core Planning
- DimensionDrawer Module
- SlotFeature2D
- FeatureRecognizer
- .AppendDimensionDiagnostics
- .ResolveDimStyle
- .CollectOuterContourStepSourceGeometryIds
- AnnotationCaseStore
- AUTOFIXDIM Agent Rules
- 规则债务：死 API / 双实现对照表
- .DrawSlotRadiusLeadersWithJig
- 重构验证手册（分支 `refactor/v2-roadmap`）
- .DrawLinearDimensions
- StructureSuppressionRules
- HoleFeature2D
- DimensionExtensionLineRenderer
- .ResolveDimStyle
- run-cad-regression-suite.ps1
- .ArbitrateOuterContourStepOverallRemainders
- DiameterCalloutJig
- DimensionRuleConfig
- .AssertLProfileRuleEndpointsOnBoundary
- write-cad-visual-report.ps1
- .CreateOutlinePlanLocal
- .ResolveAnnotationLayer
- 旋转不变量（四向标注一致）v3
- .AddLooseHoleCenterDistances
- run-hole-slot-cad-regression.ps1
- .PointsEqual
- M4 Round 2 calibration matrix
- DimensionPlanCadItem
- Core Test ↔ Minimal DWG Regression
- CAD 自动标注规则与回归体系整改计划
- M5 正式定义（2026-07-31）
- M4-positive-collisions 视觉真值
- run-core-cad-regression.ps1
- .GetDimLineCoordinate
- 3. 根因
- .DrawCornerFeatureLeadersWithJig
- PinGroupPlan
- 阶段 0：冻结行为与建立三态真值矩阵
- 阶段 1：加固候选级测试门禁
- 阶段 2：类型化候选语义
- 阶段 3：单次统一裁决
- 阶段 4：统一方向模型与生产识别
- 阶段 5：自动化真实 CAD 回归
- 阶段 6：收敛人工视觉验收
- run cad test ps1
- Remediation case truth matrix
- DimensionPlanCadItem
- 5. 测试门禁
- validate-core-cad-report.ps1
- CadAuto Core csproj
- .Place
- .Map
- Test Has Property
- AnnotationCaseSnapshot
- DimensionLayoutRules.cs
- .RunRegisteredTests
- FeatureDiagnosticCounts
- .AddDimensionDebugLabel
- DimensionDiagnosticReport

## God Nodes (most connected - your core abstractions)
1. `OutlineFeature2D` - 449 edges
2. `Point2D` - 300 edges
3. `Program` - 266 edges
4. `PlannedDimension` - 197 edges
5. `DimensionPlan` - 165 edges
6. `DimensionSide` - 161 edges
7. `DimensionPlanner` - 129 edges
8. `DimensionLayoutRules` - 119 edges
9. `Segment2D` - 115 edges
10. `DimensionDrawer` - 103 edges

## Surprising Connections (you probably didn't know these)
- `Annotation Product Invariants` --semantically_similar_to--> `Real Geometry Attachment Rules`  [INFERRED] [semantically similar]
  AGENTS.md → docs/DimensionRules.md
- `Current Source Command Surface` --semantically_similar_to--> `Current ASD Command Surface`  [INFERRED] [semantically similar]
  docs/Environment.md → README.md
- `CAD Smoke-Test Helper` --semantically_similar_to--> `Manual CAD Smoke Test`  [INFERRED] [semantically similar]
  README.md → docs/Deployment.md
- `FeatureRecognizer` --references--> `DimensionRuleConfig`  [EXTRACTED]
  CadAuto.CadAdapter/Recognition/FeatureRecognizer.cs → CadAuto.Core/Rules/DimensionRuleConfig.cs
- `CadEntityWriter` --references--> `DimensionRuleConfig`  [EXTRACTED]
  CadAuto.CadAdapter/Rendering/CadEntityWriter.cs → CadAuto.Core/Rules/DimensionRuleConfig.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **AUTOFIXDIM Annotation Rule System** — docs_recognitionrules_recognition_rules, docs_dimensionrules_dimension_rules, agents_annotation_product_invariants, docs_environment_source_map [INFERRED 0.85]
- **Four-Direction Regression Evidence Chain** — docs_dimensionlayoutregression_dl01_fixture, docs_dimensionlayoutregression_trace_and_report_archiving, docs_dimensionlayoutregression_structured_report_validation, docs_dimensionlayoutregression_visual_image_review, docs_dimensionlayoutregression_completion_gate [EXTRACTED 1.00]

## Communities (122 total, 11 thin omitted)

### Community 0 - "Point2D Module"
Cohesion: 0.23
Nodes (3): Color, IDictionary, PlacedDim

### Community 1 - "OutlineFeature2D Module"
Cohesion: 0.20
Nodes (15): HoleFeature, ObjectId, Point3d, NativeDiameterDimensioner, BlockTableRecord, Database, Document, Editor (+7 more)

### Community 2 - "HoleFeature2D Module"
Cohesion: 0.12
Nodes (7): IEnumerable, DimensionPlanner, IEnumerable, IgnoredPoint, IList, List, StructurePoint

### Community 3 - "CadEntityWriter"
Cohesion: 0.13
Nodes (8): DimensionPlanner, Func, IEnumerable, IgnoredPoint, IList, ISet, List, StructurePoint

### Community 4 - "DimensionPlan Module"
Cohesion: 0.14
Nodes (10): BlockTableRecord, Database, Entity, Line, ObjectId, Point3d, Polyline, Polyline2d (+2 more)

### Community 5 - "FeatureRecognizer2D Module"
Cohesion: 0.11
Nodes (18): BlockReference, SlotFeature, ObjectId, Point2d, SlotArcCandidate, SlotLineCandidate, ThreadArcInfo, ThreadMinorCircleInfo (+10 more)

### Community 6 - "AnnotationCase"
Cohesion: 0.10
Nodes (18): GeometryCollector, OutlineEntityCandidate, Arc, Editor, Entity, IEnumerable, IList, Line (+10 more)

### Community 7 - "Assert Module"
Cohesion: 0.12
Nodes (7): CadToCoreModelMapper, IEnumerable, List, ChamferFeature, Point2d, HoleKind, CadAuto.CadAdapter.Model

### Community 8 - "Commands Module"
Cohesion: 0.19
Nodes (8): CadEntityWriter, BlockTableRecord, Color, Database, double, ObjectId, string, Transaction

### Community 9 - "Segment2D"
Cohesion: 0.07
Nodes (16): StructureFeature, StructureFeatureAxis, StructureFeatureKind, List, StructureFeatureExtractor, IList, List, StructureMeasurementSelector (+8 more)

### Community 10 - "DimensionDrawer Module"
Cohesion: 0.08
Nodes (6): DimensionPlanner, IDictionary, IList, List, string, Tuple

### Community 11 - "DimensionPlanner"
Cohesion: 0.15
Nodes (6): DimensionOrientation, DimensionPlanner, string, LProfileMatch, LProfileOuterContour, LProfileStepCoverageEvidence

### Community 12 - "DimensionDrawer Module"
Cohesion: 0.08
Nodes (13): DimensionCandidateDiagnostic, List, DimensionCandidateDecision, DimensionCandidateOwnerKind, DimensionCandidateRole, DimensionCandidateSemantics, IEnumerable, List (+5 more)

### Community 13 - "FeatureRecognizer2D"
Cohesion: 0.12
Nodes (14): OutlineFeature, List, Point2d, OutlineSegment, ObjectId, Point2d, FeatureRecognizer, bool (+6 more)

### Community 14 - "OutlineFeature Module"
Cohesion: 0.11
Nodes (18): 10. 一句话总结, 1. 问题陈述, 2. 现状代码（缺陷点）, 3. 目标行为, 4.1 共删谓词（新）, 4.2 伪代码, 4.3 不改动的部分, 4.4 注释同步 (+10 more)

### Community 16 - "CadAuto Core Geometry"
Cohesion: 0.09
Nodes (8): FilletFeature2D, HoleCalloutCluster, List, StructureEndpointSpan, CadAuto.Core.Geometry, CadAuto.Core.Model, CadAuto.Core.Rules, CadAuto.Core.Planning

### Community 17 - "OutlineSegment Module"
Cohesion: 0.09
Nodes (4): DimensionPlanner, int, DimensionPlanPostValidator, IEnumerable

### Community 18 - ".CreateDefault"
Cohesion: 0.13
Nodes (13): Entity, DebugAnnotationRenderer, Color, Database, ObjectId, Point2d, Point3d, string (+5 more)

### Community 19 - ".AdjustShortLocalDimensionTextPositions"
Cohesion: 0.15
Nodes (4): Datum2D, List, SlotFeature2D, DimensionSide

### Community 20 - "HoleCalloutPlan Module"
Cohesion: 0.12
Nodes (10): DimensionDeduplicationItem, DimensionDeduplicationRules, PartitionCandidate, double, HashSet, IList, int, List (+2 more)

### Community 21 - "DimensionPlanner"
Cohesion: 0.13
Nodes (13): CenterlineEndpoint2D, CenterlineEndpointMatch, FunctionalHoleGroupPlan, IgnoredPoint, LooseHoleLineGroup, LooseHoleLocationPlan, LooseHoleMacroGroup, StructurePoint (+5 more)

### Community 22 - ".RecognizeOutlineSlotFeatures"
Cohesion: 0.16
Nodes (4): IDictionary, List, IndexedLayoutItem, StackingLayerItem

### Community 23 - "OutlineGeometryQuery"
Cohesion: 0.24
Nodes (7): HoleCalloutKind, HoleCalloutPlan, List, HoleCalloutPlanner, IEnumerable, IList, HoleCalloutCluster

### Community 24 - "FeatureRecognizer2D.cs"
Cohesion: 0.13
Nodes (8): IList, Point3d, RotatedDimension, IEnumerable, DimensionTextPlacementItem, DimensionTextSlidePlacement, TextBounds2D, TextSlideCandidate

### Community 25 - "StackingLayerItem Module"
Cohesion: 0.33
Nodes (5): DimensionReadingLevel, IndexedLayoutItem, LayoutBlock, StackingLayerItem, TextSlideCandidate

### Community 26 - ".VerticalExtensionOverlapsOutlineSegment"
Cohesion: 0.20
Nodes (5): CoordinateFrame2D, double, CoordinateFrameModelTransform, IEnumerable, List

### Community 27 - "Commands Module"
Cohesion: 0.12
Nodes (3): DimensionLayoutItem, Tuple, VerticalRebalanceMove

### Community 28 - "DimensionDiagnosticReport"
Cohesion: 0.28
Nodes (6): DiagnosticDimensionSide, IEnumerable, StringBuilder, DiagnosticRunContext, Exception, KeyValuePair

### Community 29 - ".HorizontalProbeCrossesInterior"
Cohesion: 0.20
Nodes (8): DiameterCalloutJig, double, JigPrompts, SamplerStatus, string, Vector3d, WorldDraw, DrawJig

### Community 30 - "DimensionDrawer Module"
Cohesion: 0.33
Nodes (4): SlotArcCandidate, SlotLineCandidate, CadAuto.Core.Recognition, CadAuto.Core.Tests

### Community 31 - "DatumDefinition Module"
Cohesion: 0.14
Nodes (13): 1. 销对之间的孔 → 功能孔, 2. 同侧结构段对齐（方案 1）, 3. OutlineSegment 复述 overall, 4. 结构宽/高 vs overall — 规则过大已修正, 5. 文档与测试, Handoff — 尺寸规则改进（2026-07-20）, 仓库与分支, 工作约定 (+5 more)

### Community 32 - "DimensionLayoutRules.cs"
Cohesion: 0.16
Nodes (5): Arc2D, OutlineGeometryQuery, double, IList, List

### Community 33 - "HoleFeature2D"
Cohesion: 0.15
Nodes (14): LProfileBoundaryGraph, LProfileConcaveTurn, LProfileEdge, LProfileFilletProjection, LProfileHalfEdge, LProfileLocalAnchorCandidate, LProfileLocalAnchorPair, LProfileOverallSideScore (+6 more)

### Community 34 - ".VerticalExtensionOverlapsOutlineSegment"
Cohesion: 0.08
Nodes (25): 10.1 Core 布局, 10.2 renderer 链, 10. 布局与 CAD 绘制, 11. 最终校验, 12. 回归定位入口, 13. 建议下一位 agent 的分析顺序, 14. 本次未做的事, 1. 分析边界 (+17 more)

### Community 35 - "FeatureRecognizer2D.cs"
Cohesion: 0.11
Nodes (4): Segment2D, ChamferFeature2D, FeatureRecognizer2D, IEnumerable

### Community 36 - "SlotArcCandidate Module"
Cohesion: 0.27
Nodes (4): AnnotationCaseStore, IList, List, StringBuilder

### Community 37 - "FeatureRecognizer2D"
Cohesion: 0.21
Nodes (3): DimensionLayoutRules, string, LayoutBlock

### Community 38 - "HoleCalloutPlanner"
Cohesion: 0.13
Nodes (9): DimensionPlanner, Func, IEnumerable, IList, List, Tuple, FunctionalHoleGroupPlan, LooseHoleLocationPlan (+1 more)

### Community 39 - "CadAuto Core Planning"
Cohesion: 0.11
Nodes (18): A. Overall partition 误删 / 漏删, B. 外框碎段 OutlineSegment + 结构双开, C. StructureOverallPartition 过宽（修“其它图崩了”）, D. U 槽链对齐, E. Phase 2 — `default(Point2D)==(0,0)` 哨兵, F. Phase 3 — Complementary Snap 收紧, graphify, HANDOFF 2026-07-21 — Overall partition / envelope / slot chain / Point2D snap / complementary snap (+10 more)

### Community 40 - "DimensionDrawer Module"
Cohesion: 0.07
Nodes (5): Program, Action, HashSet, int, List

### Community 41 - "SlotFeature2D"
Cohesion: 0.13
Nodes (14): 1. 先确定尺寸对象，再确定夹点, 2. 按布局侧选择中心线端点, 3. 普通孔和销孔中心距, 4. 单圆弧 U 型槽, 5. 双圆弧槽, 不遮挡中心线标注规则, 双圆弧槽右侧布局, 外轮廓避让 (+6 more)

### Community 43 - "FeatureRecognizer"
Cohesion: 0.20
Nodes (9): AnnotationCase, AnnotationCaseDecision, AnnotationStrategyIds, List, string, AnnotationCaseOverlay, HashSet, IList (+1 more)

### Community 45 - ".AppendDimensionDiagnostics"
Cohesion: 0.11
Nodes (8): AutoFixDimOutputScope, CommandMethod, Commands, Document, Editor, IList, int, string

### Community 47 - ".CollectOuterContourStepSourceGeometryIds"
Cohesion: 0.17
Nodes (6): LProfileOverallSideSelection, Tuple, OutlineEnvelope2D, LProfileLocalAnchorPair, LProfileOverallSideScore, LProfileOverallSideSelection

### Community 50 - "AUTOFIXDIM Agent Rules"
Cohesion: 0.06
Nodes (42): AUTOFIXDIM Agent Rules, Annotation Product Invariants, Explicit Validation Authorization, Graphify-First Codebase Navigation, Safe File Operations, Deployment and Manual Verification, Four-Direction Automated Regression, Manual AutoCAD Verification Flow (+34 more)

### Community 51 - "规则债务：死 API / 双实现对照表"
Cohesion: 0.08
Nodes (25): 1. 目的, 2. 状态标签, 3.1 `StructureSuppressionRules` — 台阶有用性 / 冗余簇, 3.2 `StructureSuppressionRules` — 四向延伸线 public 版（死）, 3.3 `DimensionDeduplicationRules` — 对称半边, 3.4 文档幽灵符号, 3.5 明确不是死 API（对照，防误删）, 3. 死 API 对照表 (+17 more)

### Community 52 - ".DrawSlotRadiusLeadersWithJig"
Cohesion: 0.18
Nodes (4): Point2D, StructureEndpointRules, IEnumerable, IEquatable

### Community 53 - "重构验证手册（分支 `refactor/v2-roadmap`）"
Cohesion: 0.09
Nodes (21): 3.1 采基线（如已在上一轮采过可复用）, 3.2 用新 DLL 重跑同一张图同一命令, 4.1 诊断与报告（零出图变化）, 4.2 XData 与清理（**有行为变化**）, 4.3 识别（**有行为变化**）, 4.4 包络与失败回退（**有行为变化，风险最高**）, 4.5 部署与加载, 5.1 布局回归报告新鲜度 (+13 more)

### Community 54 - ".DrawLinearDimensions"
Cohesion: 0.26
Nodes (3): HashSet, IList, DimensionStackingPlacement

### Community 57 - "DimensionExtensionLineRenderer"
Cohesion: 0.19
Nodes (5): ISet, IList, ISet, SlotArcCandidate, SlotLineCandidate

### Community 58 - ".ResolveDimStyle"
Cohesion: 0.16
Nodes (5): OutlineArc, ObjectId, Point2d, Arc, SlotArcRules

### Community 60 - "run-cad-regression-suite.ps1"
Cohesion: 0.22
Nodes (11): ConvertTo-LispString(), Get-DllEvidence(), Get-NormalizedReportHash(), Get-NormalizedVisualReportHash(), Get-Sha256(), Get-VisualGateStatus(), Invoke-CoreCadCase(), Invoke-CoreConsole() (+3 more)

### Community 61 - ".ArbitrateOuterContourStepOverallRemainders"
Cohesion: 0.10
Nodes (8): DimensionKind, IEnumerable, ISet, PlannedDimension, List, StructurePlacementAdapter, IList, IList

### Community 62 - "DiameterCalloutJig"
Cohesion: 0.12
Nodes (12): LProfileArcAttachment, LProfileAxisChain, LProfileMatch, LProfileOuterContour, IList, LProfileArcAttachment, LProfileAxisChain, LProfileBoundaryGraph (+4 more)

### Community 63 - "DimensionRuleConfig"
Cohesion: 0.24
Nodes (4): DimensionRuleConfig, Dictionary, ThreadArcRules, double

### Community 64 - ".AssertLProfileRuleEndpointsOnBoundary"
Cohesion: 0.40
Nodes (3): Circle2D, DrawingGeometry, List

### Community 65 - "write-cad-visual-report.ps1"
Cohesion: 0.21
Nodes (6): Get-Number(), Get-Orientation(), Get-Point(), Test-PointInRect(), Test-SegmentRect(), Test-SegmentsIntersect()

### Community 66 - ".CreateOutlinePlanLocal"
Cohesion: 0.23
Nodes (9): AnnotationMetadata, Database, Entity, IEnumerable, List, string, Transaction, DateTime (+1 more)

### Community 67 - ".ResolveAnnotationLayer"
Cohesion: 0.13
Nodes (16): LayerManager, Database, string, Transaction, SupportBlockCommand, Arc, Database, Document (+8 more)

### Community 68 - "旋转不变量（四向标注一致）v3"
Cohesion: 0.18
Nodes (10): CAD 真图轨 F338（test2 拓扑 / 合成边）, Phase 状态, Signature, 产品共识（grill-me 2026-08-07 锁定）, 旋转不变量（四向标注一致）v3, 架构, 核心测试, 直角单测 F215 (+2 more)

### Community 69 - ".AddLooseHoleCenterDistances"
Cohesion: 0.29
Nodes (6): DimStyleManager, Color, Database, ObjectId, string, Transaction

### Community 70 - "run-hole-slot-cad-regression.ps1"
Cohesion: 0.36
Nodes (8): ConvertTo-LispString(), Get-DllEvidence(), Get-NormalizedReportHash(), Get-NormalizedVisualHash(), Get-Sha256(), Invoke-CoreConsole(), Invoke-HoleSlotRun(), Write-Json()

### Community 72 - "M4 Round 2 calibration matrix"
Cohesion: 0.22
Nodes (8): Confirmed synthetic defects, Final calibrated evidence, Independent M4 Round 2 verification / WarningOnly evidence passed, Legal-contact counterexamples, M4 Round 2 calibration matrix, Real saved final DWG positive evidence, Round 1 archived false-positive modes, Round 2 results

### Community 73 - "DimensionPlanCadItem"
Cohesion: 0.27
Nodes (5): DimensionType, DimensionPlanCadItem, Point3d, DimensionPlanCadMapper, IEnumerable

### Community 74 - "Core Test ↔ Minimal DWG Regression"
Cohesion: 0.25
Nodes (7): Bring-up run, Case manifest contract, Core Test ↔ Minimal DWG Regression, Extract fixture once, Initial case, Normal run, Purpose

### Community 75 - "CAD 自动标注规则与回归体系整改计划"
Cohesion: 0.25
Nodes (7): 1. 目标与边界, 2. 现状证据, 4. 整改阶段, 6. 量化验收指标, 7. 推荐实施顺序, 8. 开工前决策清单, CAD 自动标注规则与回归体系整改计划

### Community 77 - "M5 正式定义（2026-07-31）"
Cohesion: 0.29
Nodes (7): M5-1：PR 快速层与 nightly 门禁运营化, M5-2：Hole/Slot 真实 fixture 补齐, M5 回滚边界与不在范围, M5 当前状态（2026-08-03）, M5 正式定义（2026-07-31）, M5 进入条件, M5 退出条件

### Community 78 - "M4-positive-collisions 视觉真值"
Cohesion: 0.29
Nodes (6): Debug-v3 DLL, M4-positive-collisions 视觉真值, 已确认真值, 权威输入, 理想参考, 边界

### Community 79 - "run-core-cad-regression.ps1"
Cohesion: 0.38
Nodes (4): Build-NewPluginVersion(), Get-LatestDebugDirectory(), Get-NormalizedVisualReportHash(), Invoke-VisualInspection()

### Community 81 - ".GetDimLineCoordinate"
Cohesion: 0.36
Nodes (8): DeferredDim, DimSide, PlacedDim, TextBounds, bool, double, int, string

### Community 82 - "3. 根因"
Cohesion: 0.33
Nodes (6): 3.1 裁决模型以“删除顺序”为核心, 3.2 业务语义依附调试字符串, 3.3 测试真值链断层, 3.4 基线状态没有统一表达, 3.5 回归编排按 case 重复执行, 3. 根因

### Community 83 - ".DrawCornerFeatureLeadersWithJig"
Cohesion: 0.12
Nodes (10): CenterlineEndpointDefinition, DatumDefinition, List, Point3d, DiagnosticRunContext, List, DiagnosticBounds, DiagnosticCenterlineEndpoint (+2 more)

### Community 85 - "阶段 0：冻结行为与建立三态真值矩阵"
Cohesion: 0.40
Nodes (5): 交付物, 工作, 退出条件, 阶段 0：冻结行为与建立三态真值矩阵, 风险与回滚

### Community 86 - "阶段 1：加固候选级测试门禁"
Cohesion: 0.40
Nodes (5): 交付物, 工作, 退出条件, 阶段 1：加固候选级测试门禁, 风险与回滚

### Community 87 - "阶段 2：类型化候选语义"
Cohesion: 0.40
Nodes (5): 交付物, 工作, 退出条件, 阶段 2：类型化候选语义, 风险与回滚

### Community 88 - "阶段 3：单次统一裁决"
Cohesion: 0.40
Nodes (5): 交付物, 工作, 退出条件, 阶段 3：单次统一裁决, 风险与回滚

### Community 89 - "阶段 4：统一方向模型与生产识别"
Cohesion: 0.40
Nodes (5): 交付物, 工作, 退出条件, 阶段 4：统一方向模型与生产识别, 风险与回滚

### Community 90 - "阶段 5：自动化真实 CAD 回归"
Cohesion: 0.40
Nodes (5): 交付物, 工作, 退出条件, 阶段 5：自动化真实 CAD 回归, 风险与回滚

### Community 91 - "阶段 6：收敛人工视觉验收"
Cohesion: 0.40
Nodes (5): 交付物, 工作, 退出条件, 阶段 6：收敛人工视觉验收, 风险与回滚

### Community 93 - "Remediation case truth matrix"
Cohesion: 0.40
Nodes (4): Promotion rules, Remediation case truth matrix, Status meanings, Validation

### Community 94 - "DimensionPlanCadItem"
Cohesion: 0.08
Nodes (16): GeneratedAnnotation, ObjectId, DiameterJigResult, AutoFixDimOutputScope, DiagnosticBounds, DiagnosticCenterlineEndpoint, DiagnosticFileIdentity, CadAuto.CadAdapter.Recognition (+8 more)

### Community 95 - "5. 测试门禁"
Cohesion: 0.50
Nodes (4): 5.1 每次规则修改的最低证据, 5.2 分级门禁, 5.3 基线变更规则, 5. 测试门禁

### Community 103 - ".Place"
Cohesion: 0.14
Nodes (4): List, RotatedDimension, DeferredDim, DimSide

### Community 104 - ".Map"
Cohesion: 0.18
Nodes (3): DimensionDiagnosticReport, IEnumerable, List

### Community 105 - "Test Has Property"
Cohesion: 0.60
Nodes (3): Get-PhysicalRank(), Test-DimensionSelector(), Test-HasProperty()

### Community 106 - "AnnotationCaseSnapshot"
Cohesion: 0.47
Nodes (3): AnnotationCaseRuntime, AnnotationCaseSnapshot, IList

### Community 108 - "DimensionLayoutRules.cs"
Cohesion: 0.11
Nodes (14): CornerCalloutJigResult, CornerCalloutRenderer, CornerFeatureLeaderJig, Database, double, Editor, JigPrompts, ObjectId (+6 more)

### Community 113 - ".RunRegisteredTests"
Cohesion: 0.18
Nodes (9): 下次匹配条件, 不能套用, 代码改动, 水平销距按本组轮廓侧，不同侧的组间定位不对齐, 症状类别, 确认的标注规则, 诊断证据, 错误代码（文件与分支） (+1 more)

### Community 122 - "DimensionDiagnosticReport"
Cohesion: 0.15
Nodes (10): FilletFeature, Point2d, DimensionDrawer, BlockTableRecord, Editor, IEnumerable, ObjectId, Point2d (+2 more)

## Knowledge Gaps
- **216 isolated node(s):** `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests`, `AutoFixDimOutputScope`, `DiagnosticBounds` (+211 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **11 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `OutlineFeature2D` connect `.PointsEqual` to `HoleFeature2D Module`, `CadEntityWriter`, `Assert Module`, `Segment2D`, `DimensionDrawer Module`, `DimensionPlanner`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `CadAuto Core Geometry`, `OutlineSegment Module`, `.AdjustShortLocalDimensionTextPositions`, `.RecognizeOutlineSlotFeatures`, `.VerticalExtensionOverlapsOutlineSegment`, `Commands Module`, `DimensionLayoutRules.cs`, `HoleFeature2D`, `FeatureRecognizer2D.cs`, `FeatureRecognizer2D`, `HoleCalloutPlanner`, `DimensionDrawer Module`, `FeatureRecognizer`, `.IsVertical`, `.ResolveDimStyle`, `.CollectOuterContourStepSourceGeometryIds`, `.PointsEqual`, `.DrawSlotRadiusLeadersWithJig`, `.DrawLinearDimensions`, `StructureSuppressionRules`, `HoleFeature2D`, `DimensionExtensionLineRenderer`, `HoleFeature2D`, `.ArbitrateOuterContourStepOverallRemainders`, `DiameterCalloutJig`, `.AssertLProfileRuleEndpointsOnBoundary`, `.AssertHasDimension`, `PinGroupPlan`, `.SuppressStructureClosedChainRedundantPositioningOnSide`, `.Place`, `AnnotationCaseSnapshot`, `.AssertLProfileRuleEndpointsOnBoundary`, `.ResolveAnnotationLayer`, `DimensionDiagnosticReport`?**
  _High betweenness centrality (0.206) - this node is a cross-community bridge._
- **Why does `DimensionDrawer` connect `DimensionDiagnosticReport` to `Point2D Module`, `FeatureRecognizer2D`, `.Place`, `Commands Module`, `.Map`, `DimensionPlanCadItem`, `.PointsEqual`, `DimensionLayoutRules.cs`, `FeatureRecognizer2D`, `.AppendDimensionDiagnostics`, `.GetDimLineCoordinate`, `.CreateDefault`, `HoleCalloutPlan Module`, `.AddDimensionDebugLabel`, `.VerticalExtensionOverlapsOutlineSegment`, `DimensionDiagnosticReport`, `DimensionRuleConfig`?**
  _High betweenness centrality (0.128) - this node is a cross-community bridge._
- **Why does `Point2D` connect `.DrawSlotRadiusLeadersWithJig` to `HoleFeature2D Module`, `CadEntityWriter`, `Segment2D`, `DimensionDrawer Module`, `DimensionPlanner`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `CadAuto Core Geometry`, `OutlineSegment Module`, `.AdjustShortLocalDimensionTextPositions`, `HoleCalloutPlan Module`, `DimensionPlanner`, `OutlineGeometryQuery`, `FeatureRecognizer2D.cs`, `StackingLayerItem Module`, `.VerticalExtensionOverlapsOutlineSegment`, `Commands Module`, `DimensionDrawer Module`, `DimensionLayoutRules.cs`, `HoleFeature2D`, `FeatureRecognizer2D.cs`, `FeatureRecognizer2D`, `HoleCalloutPlanner`, `DimensionDrawer Module`, `.IsVertical`, `.ResolveDimStyle`, `.CollectOuterContourStepSourceGeometryIds`, `.PointsEqual`, `.DrawLinearDimensions`, `StructureSuppressionRules`, `HoleFeature2D`, `DimensionExtensionLineRenderer`, `HoleFeature2D`, `.ArbitrateOuterContourStepOverallRemainders`, `DiameterCalloutJig`, `.AssertLProfileRuleEndpointsOnBoundary`, `.PointsEqual`, `.AddDimensionDebugLabel`?**
  _High betweenness centrality (0.103) - this node is a cross-community bridge._
- **What connects `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests` to the rest of the system?**
  _216 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `HoleFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.12066365007541478 - nodes in this community are weakly interconnected._
- **Should `CadEntityWriter` be split into smaller, more focused modules?**
  _Cohesion score 0.13176470588235295 - nodes in this community are weakly interconnected._
- **Should `DimensionPlan Module` be split into smaller, more focused modules?**
  _Cohesion score 0.14482758620689656 - nodes in this community are weakly interconnected._