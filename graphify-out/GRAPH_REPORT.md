# Graph Report - CAD-AUTO-L1  (2026-10-17)

## Corpus Check
- 162 files · ~206,821 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2475 nodes · 8850 edges · 113 communities (102 shown, 11 thin omitted)
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 588 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `a980ade3`
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
- HoleCalloutPlanner
- CadAuto Core Planning
- DimensionDrawer Module
- SlotFeature2D
- FeatureRecognizer
- .AppendDimensionDiagnostics
- .AddLooseHoleCenterDistances
- .CollectOuterContourStepSourceGeometryIds
- AnnotationCaseStore
- AUTOFIXDIM Agent Rules
- 规则债务：死 API / 双实现对照表
- .DrawSlotRadiusLeadersWithJig
- 重构验证手册（分支 `refactor/v2-roadmap`）
- .DrawLinearDimensions
- DimensionPlanner.cs
- .ArbitrateOuterContourStepOverallRemainders
- DimensionExtensionLineRenderer
- HoleFeature2D
- run-cad-regression-suite.ps1
- .ArbitrateOuterContourStepOverallRemainders
- IList
- .PointsEqual
- .AssertLProfileRuleEndpointsOnBoundary
- write-cad-visual-report.ps1
- DrawingGeometry
- .ResolveAnnotationLayer
- 旋转不变量（四向标注一致）v3
- .AddLooseHoleCenterDistances
- run-hole-slot-cad-regression.ps1
- M4 Round 2 calibration matrix
- Core Test ↔ Minimal DWG Regression
- CAD 自动标注规则与回归体系整改计划
- M5 正式定义（2026-07-31）
- M4-positive-collisions 视觉真值
- run-core-cad-regression.ps1
- 3. 根因
- .DrawCornerFeatureLeadersWithJig
- DimensionPlanPostValidator
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
- .AssertHasDimension
- CadAuto Core csproj
- DimensionExtensionLineRenderer
- Test Has Property
- DiameterCalloutJig
- .ResolveAnnotationLayer
- .CollectOuterContourStepSourceGeometryIds
- FeatureDiagnosticCounts

## God Nodes (most connected - your core abstractions)
1. `OutlineFeature2D` - 425 edges
2. `Point2D` - 266 edges
3. `Program` - 248 edges
4. `PlannedDimension` - 193 edges
5. `DimensionPlan` - 157 edges
6. `DimensionSide` - 130 edges
7. `DimensionPlanner` - 125 edges
8. `Segment2D` - 111 edges
9. `DimensionLayoutRules` - 105 edges
10. `DimensionDrawer` - 103 edges

## Surprising Connections (you probably didn't know these)
- `Annotation Product Invariants` --semantically_similar_to--> `Real Geometry Attachment Rules`  [INFERRED] [semantically similar]
  AGENTS.md → docs/DimensionRules.md
- `Current Source Command Surface` --semantically_similar_to--> `Current ASD Command Surface`  [INFERRED] [semantically similar]
  docs/Environment.md → README.md
- `CAD Smoke-Test Helper` --semantically_similar_to--> `Manual CAD Smoke Test`  [INFERRED] [semantically similar]
  README.md → docs/Deployment.md
- `CadAnnotationSession` --references--> `Segment2D`  [EXTRACTED]
  CadAuto.CadAdapter/Modules/CadAnnotationSession.cs → CadAuto.Core/Geometry/Segment2D.cs
- `CadAnnotationSession` --references--> `AnnotationSession`  [EXTRACTED]
  CadAuto.CadAdapter/Modules/CadAnnotationSession.cs → CadAuto.Core/Planning/Modules/AnnotationSession.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **AUTOFIXDIM Annotation Rule System** — docs_recognitionrules_recognition_rules, docs_dimensionrules_dimension_rules, agents_annotation_product_invariants, docs_environment_source_map [INFERRED 0.85]
- **Four-Direction Regression Evidence Chain** — docs_dimensionlayoutregression_dl01_fixture, docs_dimensionlayoutregression_trace_and_report_archiving, docs_dimensionlayoutregression_structured_report_validation, docs_dimensionlayoutregression_visual_image_review, docs_dimensionlayoutregression_completion_gate [EXTRACTED 1.00]

## Communities (113 total, 11 thin omitted)

### Community 0 - "Point2D Module"
Cohesion: 0.09
Nodes (16): CadAnnotationSession, Database, Document, Editor, IList, ObjectId, Transaction, CornerAnnotationModule (+8 more)

### Community 1 - "OutlineFeature2D Module"
Cohesion: 0.12
Nodes (17): HoleFeature, ObjectId, Point3d, NativeDiameterDimensioner, BlockTableRecord, Database, Document, Editor (+9 more)

### Community 2 - "HoleFeature2D Module"
Cohesion: 0.16
Nodes (7): OutlineFeature2D, List, IgnoredPoint, StructurePoint, DimensionPlanner, IList, List

### Community 3 - "CadEntityWriter"
Cohesion: 0.21
Nodes (8): CadEntityWriter, BlockTableRecord, Color, Database, double, ObjectId, string, Transaction

### Community 4 - "DimensionPlan Module"
Cohesion: 0.09
Nodes (17): DiagnosticCenterlineEndpoint, DiagnosticRunContext, BlockTableRecord, Database, Entity, Line, List, ObjectId (+9 more)

### Community 5 - "FeatureRecognizer2D Module"
Cohesion: 0.14
Nodes (14): BlockReference, SlotArcCandidate, SlotLineCandidate, ThreadArcInfo, ThreadMinorCircleInfo, Editor, IEnumerable, List (+6 more)

### Community 6 - "AnnotationCase"
Cohesion: 0.14
Nodes (12): LProfileArcAttachment, LProfileEdge, LProfileFilletProjection, LProfileHalfEdge, LProfileLocalAnchorCandidate, LProfileLocalAnchorPair, LProfileOverallSideSelection, LProfileOverallSplitEvidence (+4 more)

### Community 7 - "Assert Module"
Cohesion: 0.12
Nodes (11): LProfileAxisChain, LProfileConcaveTurn, LProfileMatch, LProfileOuterContour, IList, List, LProfileArcAttachment, LProfileAxisChain (+3 more)

### Community 8 - "Commands Module"
Cohesion: 0.18
Nodes (11): AnnotationMetadata, GeneratedAnnotation, Database, Entity, IEnumerable, List, ObjectId, string (+3 more)

### Community 9 - "Segment2D"
Cohesion: 0.07
Nodes (18): StructureFeature, StructureFeatureAxis, StructureFeatureKind, List, StructureFeatureExtractor, IList, List, StructureMeasurementSelector (+10 more)

### Community 10 - "DimensionDrawer Module"
Cohesion: 0.22
Nodes (9): HoleCalloutKind, HoleCalloutPlan, List, HoleCalloutCluster, HoleCalloutPlanner, IEnumerable, IList, List (+1 more)

### Community 11 - "DimensionPlanner"
Cohesion: 0.15
Nodes (3): DimensionOrientation, LProfileMatch, LProfileStepCoverageEvidence

### Community 12 - "DimensionDrawer Module"
Cohesion: 0.23
Nodes (8): Entity, DebugAnnotationRenderer, Color, Database, ObjectId, Point2d, Point3d, string

### Community 13 - "FeatureRecognizer2D"
Cohesion: 0.14
Nodes (10): ChamferFeature, Point2d, OutlineFeature, List, Point2d, OutlineSegment, ObjectId, Point2d (+2 more)

### Community 14 - "OutlineFeature Module"
Cohesion: 0.11
Nodes (18): 10. 一句话总结, 1. 问题陈述, 2. 现状代码（缺陷点）, 3. 目标行为, 4.1 共删谓词（新）, 4.2 伪代码, 4.3 不改动的部分, 4.4 注释同步 (+10 more)

### Community 16 - "CadAuto Core Geometry"
Cohesion: 0.09
Nodes (4): CadAuto.Core.Geometry, CadAuto.Core.Model, CadAuto.Core.Rules, CadAuto.Core.Planning

### Community 17 - "OutlineSegment Module"
Cohesion: 0.10
Nodes (26): CoordinateFrame2D, double, CoordinateFrameModelTransform, IEnumerable, List, CenterlineEndpoint2D, Datum2D, List (+18 more)

### Community 18 - ".CreateDefault"
Cohesion: 0.15
Nodes (6): Circle2D, Segment2D, DrawingGeometry, List, FeatureRecognizer2D, IEnumerable

### Community 19 - ".AdjustShortLocalDimensionTextPositions"
Cohesion: 0.23
Nodes (3): FunctionalHoleGroupPlan, PinGroupPlan, List

### Community 20 - "HoleCalloutPlan Module"
Cohesion: 0.12
Nodes (10): DimensionDeduplicationItem, DimensionDeduplicationRules, PartitionCandidate, double, HashSet, IList, int, List (+2 more)

### Community 21 - "DimensionPlanner"
Cohesion: 0.17
Nodes (5): HoleFeature2D, DimensionPlanner, IList, List, Tuple

### Community 22 - ".RecognizeOutlineSlotFeatures"
Cohesion: 0.29
Nodes (3): AutoFixtureDim, IExtensionApplication, PluginEntry

### Community 23 - "OutlineGeometryQuery"
Cohesion: 0.27
Nodes (4): IList, List, DeferredDim, DimSide

### Community 24 - "FeatureRecognizer2D.cs"
Cohesion: 0.06
Nodes (5): Program, Action, HashSet, int, List

### Community 25 - "StackingLayerItem Module"
Cohesion: 0.06
Nodes (27): DimensionPlanCadItem, Point3d, LProfileOverallSideScore, DimensionReadingLevel, DimensionSide, LaneOccupancy, Dictionary, IEnumerable (+19 more)

### Community 26 - ".VerticalExtensionOverlapsOutlineSegment"
Cohesion: 0.13
Nodes (7): Point2D, FilletFeature2D, StructureEndpointRules, IEnumerable, IList, StructureEndpointSpan, IEquatable

### Community 28 - "DimensionDiagnosticReport"
Cohesion: 0.38
Nodes (4): IEnumerable, StringBuilder, DiagnosticRunContext, KeyValuePair

### Community 29 - ".HorizontalProbeCrossesInterior"
Cohesion: 0.11
Nodes (16): AnnotationCase, AnnotationCaseDecision, AnnotationCaseRuntime, AnnotationStrategyIds, IList, List, string, AnnotationCaseOverlay (+8 more)

### Community 30 - "DimensionDrawer Module"
Cohesion: 0.27
Nodes (8): DimensionType, DeferredDim, PlacedDim, TextBounds, bool, double, int, string

### Community 31 - "DatumDefinition Module"
Cohesion: 0.14
Nodes (13): 1. 销对之间的孔 → 功能孔, 2. 同侧结构段对齐（方案 1）, 3. OutlineSegment 复述 overall, 4. 结构宽/高 vs overall — 规则过大已修正, 5. 文档与测试, Handoff — 尺寸规则改进（2026-07-20）, 仓库与分支, 工作约定 (+5 more)

### Community 32 - "DimensionLayoutRules.cs"
Cohesion: 0.28
Nodes (4): OutlineGeometryQuery, double, IList, List

### Community 33 - "HoleFeature2D"
Cohesion: 0.22
Nodes (4): LProfileBoundaryGraph, Dictionary, HashSet, LProfileHalfEdge

### Community 34 - ".VerticalExtensionOverlapsOutlineSegment"
Cohesion: 0.18
Nodes (3): SlotFeature2D, Func, IEnumerable

### Community 38 - "HoleCalloutPlanner"
Cohesion: 0.18
Nodes (5): AnnotationModuleRunner, AnnotationModuleIds, string, CadAuto.Core.Planning.Modules, CadAuto.CadAdapter.Modules

### Community 39 - "CadAuto Core Planning"
Cohesion: 0.11
Nodes (18): A. Overall partition 误删 / 漏删, B. 外框碎段 OutlineSegment + 结构双开, C. StructureOverallPartition 过宽（修“其它图崩了”）, D. U 槽链对齐, E. Phase 2 — `default(Point2D)==(0,0)` 哨兵, F. Phase 3 — Complementary Snap 收紧, graphify, HANDOFF 2026-07-21 — Overall partition / envelope / slot chain / Point2D snap / complementary snap (+10 more)

### Community 40 - "DimensionDrawer Module"
Cohesion: 0.19
Nodes (3): Color, IDictionary, PlacedDim

### Community 41 - "SlotFeature2D"
Cohesion: 0.14
Nodes (9): Point3d, CornerCalloutJigResult, CornerCalloutRenderer, Database, Editor, ObjectId, Point2d, Point3d (+1 more)

### Community 43 - "FeatureRecognizer"
Cohesion: 0.18
Nodes (3): IOutlineFacts, OutlineFacts, double

### Community 45 - ".AppendDimensionDiagnostics"
Cohesion: 0.12
Nodes (10): AutoFixDimOutputScope, DiagnosticDimensionSide, CommandMethod, Commands, Document, Editor, IList, int (+2 more)

### Community 47 - ".CollectOuterContourStepSourceGeometryIds"
Cohesion: 0.11
Nodes (9): DimensionPlanner, string, Tuple, OutlineEnvelope2D, LProfileLocalAnchorCandidate, LProfileLocalAnchorPair, LProfileOuterContour, LProfileOverallSideScore (+1 more)

### Community 48 - "AnnotationCaseStore"
Cohesion: 0.16
Nodes (8): OutlineArc, ObjectId, Point2d, SlotFeature, ObjectId, Point2d, Arc, IList

### Community 50 - "AUTOFIXDIM Agent Rules"
Cohesion: 0.06
Nodes (42): AUTOFIXDIM Agent Rules, Annotation Product Invariants, Explicit Validation Authorization, Graphify-First Codebase Navigation, Safe File Operations, Deployment and Manual Verification, Four-Direction Automated Regression, Manual AutoCAD Verification Flow (+34 more)

### Community 51 - "规则债务：死 API / 双实现对照表"
Cohesion: 0.08
Nodes (25): 1. 目的, 2. 状态标签, 3.1 `StructureSuppressionRules` — 台阶有用性 / 冗余簇, 3.2 `StructureSuppressionRules` — 四向延伸线 public 版（死）, 3.3 `DimensionDeduplicationRules` — 对称半边, 3.4 文档幽灵符号, 3.5 明确不是死 API（对照，防误删）, 3. 死 API 对照表 (+17 more)

### Community 52 - ".DrawSlotRadiusLeadersWithJig"
Cohesion: 0.18
Nodes (6): FeatureRecognizer, bool, double, Entity, Polyline, Polyline2d

### Community 53 - "重构验证手册（分支 `refactor/v2-roadmap`）"
Cohesion: 0.09
Nodes (21): 3.1 采基线（如已在上一轮采过可复用）, 3.2 用新 DLL 重跑同一张图同一命令, 4.1 诊断与报告（零出图变化）, 4.2 XData 与清理（**有行为变化**）, 4.3 识别（**有行为变化**）, 4.4 包络与失败回退（**有行为变化，风险最高**）, 4.5 部署与加载, 5.1 布局回归报告新鲜度 (+13 more)

### Community 54 - ".DrawLinearDimensions"
Cohesion: 0.10
Nodes (14): DimStyleManager, Color, Database, ObjectId, string, Transaction, CadToCoreModelMapper, IEnumerable (+6 more)

### Community 55 - "DimensionPlanner.cs"
Cohesion: 0.09
Nodes (18): GeometryCollector, OutlineEntityCandidate, Arc, Editor, Entity, IEnumerable, IList, Line (+10 more)

### Community 57 - "DimensionExtensionLineRenderer"
Cohesion: 0.20
Nodes (5): ISet, IList, ISet, SlotArcCandidate, SlotLineCandidate

### Community 59 - "HoleFeature2D"
Cohesion: 0.30
Nodes (4): LooseHoleLineGroup, LooseHoleLocationPlan, LooseHoleMacroGroup, List

### Community 60 - "run-cad-regression-suite.ps1"
Cohesion: 0.22
Nodes (11): ConvertTo-LispString(), Get-DllEvidence(), Get-NormalizedReportHash(), Get-NormalizedVisualReportHash(), Get-Sha256(), Get-VisualGateStatus(), Invoke-CoreCadCase(), Invoke-CoreConsole() (+3 more)

### Community 61 - ".ArbitrateOuterContourStepOverallRemainders"
Cohesion: 0.12
Nodes (7): DimensionPlanner, Func, IEnumerable, IList, ISet, List, IEnumerable

### Community 62 - "IList"
Cohesion: 0.22
Nodes (7): CornerFeatureLeaderJig, double, JigPrompts, SamplerStatus, string, WorldDraw, DrawJig

### Community 63 - ".PointsEqual"
Cohesion: 0.05
Nodes (18): DimensionPlanCadMapper, IEnumerable, DimensionPlan, Dictionary, int, List, DimensionPlanner, IDictionary (+10 more)

### Community 65 - "write-cad-visual-report.ps1"
Cohesion: 0.21
Nodes (6): Get-Number(), Get-Orientation(), Get-Point(), Test-PointInRect(), Test-SegmentRect(), Test-SegmentsIntersect()

### Community 66 - "DrawingGeometry"
Cohesion: 0.33
Nodes (4): SlotArcCandidate, SlotLineCandidate, CadAuto.Core.Recognition, CadAuto.Core.Tests

### Community 67 - ".ResolveAnnotationLayer"
Cohesion: 0.19
Nodes (12): SupportBlockCommand, Arc, Database, Document, Editor, Entity, List, ObjectId (+4 more)

### Community 68 - "旋转不变量（四向标注一致）v3"
Cohesion: 0.18
Nodes (10): CAD 真图轨 F338（test2 拓扑 / 合成边）, Phase 状态, Signature, 产品共识（grill-me 2026-08-07 锁定）, 旋转不变量（四向标注一致）v3, 架构, 核心测试, 直角单测 F215 (+2 more)

### Community 69 - ".AddLooseHoleCenterDistances"
Cohesion: 0.25
Nodes (3): DimensionDiagnosticReport, IEnumerable, List

### Community 70 - "run-hole-slot-cad-regression.ps1"
Cohesion: 0.36
Nodes (8): ConvertTo-LispString(), Get-DllEvidence(), Get-NormalizedReportHash(), Get-NormalizedVisualHash(), Get-Sha256(), Invoke-CoreConsole(), Invoke-HoleSlotRun(), Write-Json()

### Community 72 - "M4 Round 2 calibration matrix"
Cohesion: 0.22
Nodes (8): Confirmed synthetic defects, Final calibrated evidence, Independent M4 Round 2 verification / WarningOnly evidence passed, Legal-contact counterexamples, M4 Round 2 calibration matrix, Real saved final DWG positive evidence, Round 1 archived false-positive modes, Round 2 results

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

### Community 82 - "3. 根因"
Cohesion: 0.33
Nodes (6): 3.1 裁决模型以“删除顺序”为核心, 3.2 业务语义依附调试字符串, 3.3 测试真值链断层, 3.4 基线状态没有统一表达, 3.5 回归编排按 case 重复执行, 3. 根因

### Community 83 - ".DrawCornerFeatureLeadersWithJig"
Cohesion: 0.12
Nodes (11): FilletFeature, Point2d, DimensionDrawer, BlockTableRecord, Editor, IEnumerable, ObjectId, Point2d (+3 more)

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
Cohesion: 0.12
Nodes (12): DimSide, DiameterJigResult, AutoFixDimOutputScope, DiagnosticBounds, DiagnosticFileIdentity, CadAuto.CadAdapter.Recognition, CadAuto.CadAdapter.Mapping, CadAuto.CadAdapter.Environment (+4 more)

### Community 95 - "5. 测试门禁"
Cohesion: 0.50
Nodes (4): 5.1 每次规则修改的最低证据, 5.2 分级门禁, 5.3 基线变更规则, 5. 测试门禁

### Community 101 - ".AssertHasDimension"
Cohesion: 0.50
Nodes (3): IndexedLayoutItem, StackingLayerItem, TextSlideCandidate

### Community 104 - "DimensionExtensionLineRenderer"
Cohesion: 0.33
Nodes (5): DimensionExtensionLineRenderer, Database, IList, string, Tuple

### Community 105 - "Test Has Property"
Cohesion: 0.60
Nodes (3): Get-PhysicalRank(), Test-DimensionSelector(), Test-HasProperty()

### Community 106 - "DiameterCalloutJig"
Cohesion: 0.22
Nodes (7): DiameterCalloutJig, double, JigPrompts, SamplerStatus, string, Vector3d, WorldDraw

### Community 107 - ".ResolveAnnotationLayer"
Cohesion: 0.33
Nodes (4): LayerManager, Database, string, Transaction

### Community 115 - ".CollectOuterContourStepSourceGeometryIds"
Cohesion: 0.18
Nodes (10): DimensionCandidateDiagnostic, List, DimensionCandidateDecision, DimensionCandidateOwnerKind, DimensionCandidateRole, DimensionCandidateSemantics, IEnumerable, List (+2 more)

## Knowledge Gaps
- **177 isolated node(s):** `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests`, `AutoFixDimOutputScope`, `DiagnosticBounds` (+172 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **11 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `OutlineFeature2D` connect `HoleFeature2D Module` to `Point2D Module`, `Assert Module`, `Segment2D`, `DimensionPlanner`, `FeatureRecognizer Module`, `CadAuto Core Geometry`, `OutlineSegment Module`, `.CreateDefault`, `.AdjustShortLocalDimensionTextPositions`, `DimensionPlanner`, `OutlineGeometryQuery`, `FeatureRecognizer2D.cs`, `StackingLayerItem Module`, `.VerticalExtensionOverlapsOutlineSegment`, `.HorizontalProbeCrossesInterior`, `DimensionLayoutRules.cs`, `HoleFeature2D`, `.VerticalExtensionOverlapsOutlineSegment`, `FeatureRecognizer2D.cs`, `FeatureRecognizer`, `.IsVertical`, `.AddLooseHoleCenterDistances`, `.CollectOuterContourStepSourceGeometryIds`, `.PointsEqual`, `DimensionExtensionLineRenderer`, `.ResolveDimStyle`, `HoleFeature2D`, `.ArbitrateOuterContourStepOverallRemainders`, `.PointsEqual`, `.AssertLProfileRuleEndpointsOnBoundary`, `.DrawCornerFeatureLeadersWithJig`, `.AssertLProfileRuleEndpointsOnBoundary`?**
  _High betweenness centrality (0.222) - this node is a cross-community bridge._
- **Why does `Point2D` connect `.VerticalExtensionOverlapsOutlineSegment` to `HoleFeature2D Module`, `AnnotationCase`, `Assert Module`, `Segment2D`, `DimensionDrawer Module`, `DimensionPlanner`, `FeatureRecognizer Module`, `CadAuto Core Geometry`, `OutlineSegment Module`, `.CreateDefault`, `.AdjustShortLocalDimensionTextPositions`, `HoleCalloutPlan Module`, `DimensionPlanner`, `StackingLayerItem Module`, `DimensionLayoutRules.cs`, `HoleFeature2D`, `.VerticalExtensionOverlapsOutlineSegment`, `FeatureRecognizer2D.cs`, `FeatureRecognizer`, `.IsVertical`, `.AddLooseHoleCenterDistances`, `.CollectOuterContourStepSourceGeometryIds`, `.PointsEqual`, `DimensionExtensionLineRenderer`, `.ResolveDimStyle`, `HoleFeature2D`, `.ArbitrateOuterContourStepOverallRemainders`, `.PointsEqual`, `.AssertLProfileRuleEndpointsOnBoundary`, `DrawingGeometry`, `.AssertHasDimension`, `.CollectOuterContourStepSourceGeometryIds`?**
  _High betweenness centrality (0.113) - this node is a cross-community bridge._
- **Why does `DimensionDrawer` connect `.DrawCornerFeatureLeadersWithJig` to `Point2D Module`, `OutlineFeature2D Module`, `HoleFeature2D Module`, `CadEntityWriter`, `DimensionDrawer Module`, `FeatureRecognizer2D`, `OutlineSegment Module`, `HoleCalloutPlan Module`, `OutlineGeometryQuery`, `StackingLayerItem Module`, `DimensionDrawer Module`, `SlotArcCandidate Module`, `FeatureRecognizer2D`, `DimensionDrawer Module`, `SlotFeature2D`, `.AppendDimensionDiagnostics`, `.DrawLinearDimensions`, `.PointsEqual`, `.AddLooseHoleCenterDistances`, `DimensionPlanCadItem`, `DimensionExtensionLineRenderer`?**
  _High betweenness centrality (0.110) - this node is a cross-community bridge._
- **What connects `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests` to the rest of the system?**
  _177 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Point2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.09446693657219973 - nodes in this community are weakly interconnected._
- **Should `OutlineFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.12427409988385599 - nodes in this community are weakly interconnected._
- **Should `DimensionPlan Module` be split into smaller, more focused modules?**
  _Cohesion score 0.09080841638981174 - nodes in this community are weakly interconnected._