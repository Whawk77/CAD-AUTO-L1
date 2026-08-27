# Graph Report - 8.25  (2026-08-27)

## Corpus Check
- 145 files · ~192,783 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2313 nodes · 8160 edges · 110 communities (101 shown, 9 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 620 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `813b3419`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Point2D Module
- OutlineFeature2D Module
- HoleFeature2D Module
- GeometryCollector Module
- DimensionPlan Module
- FeatureRecognizer2D Module
- OutlineSegment
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
- HoleFeature Module
- HoleCalloutPlan Module
- .RecognizeOutlineSlotFeatures
- OutlineGeometryQuery
- FeatureRecognizer2D.cs
- StackingLayerItem Module
- .VerticalExtensionOverlapsOutlineSegment
- Commands Module
- NativeDiameterDimensioner Module
- .HorizontalProbeCrossesInterior
- DimensionDrawer Module
- DatumDefinition Module
- DimensionLayoutRules.cs
- HoleFeature2D
- .VerticalExtensionOverlapsOutlineSegment
- DimensionRuleConfig
- SlotArcCandidate Module
- FeatureRecognizer2D
- HoleCalloutPlanner
- CadAuto Core Planning
- DimensionDrawer Module
- SlotFeature2D
- FeatureRecognizer
- .IsVertical
- .AppendDimensionDiagnostics
- .AddLooseHoleCenterDistances
- AnnotationCaseStore
- .PointsEqual
- AUTOFIXDIM Agent Rules
- 规则债务：死 API / 双实现对照表
- 重构验证手册（分支 `refactor/v2-roadmap`）
- DimensionRuleConfig
- DimensionPlanner.cs
- .SuppressOutlineSegmentsOnOverallEnvelope
- SlotFeature2D
- .ResolveDimStyle
- CoordinateFrame2D
- run-cad-regression-suite.ps1
- .ArbitrateOuterContourStepOverallRemainders
- .PromptForDatumHole
- DimensionExtensionLineRenderer
- write-cad-visual-report.ps1
- PluginEntry
- .ResolveAnnotationLayer
- 旋转不变量（四向标注一致）v3
- run-hole-slot-cad-regression.ps1
- FeatureRecognizer2D.cs
- M4 Round 2 calibration matrix
- DebugAnnotationRenderer Module
- Core Test ↔ Minimal DWG Regression
- CAD 自动标注规则与回归体系整改计划
- M5 正式定义（2026-07-31）
- M4-positive-collisions 视觉真值
- run-core-cad-regression.ps1
- 3. 根因
- 阶段 0：冻结行为与建立三态真值矩阵
- 阶段 1：加固候选级测试门禁
- 阶段 2：类型化候选语义
- 阶段 3：单次统一裁决
- 阶段 4：统一方向模型与生产识别
- 阶段 5：自动化真实 CAD 回归
- 阶段 6：收敛人工视觉验收
- run cad test ps1
- Remediation case truth matrix
- 5. 测试门禁
- validate-core-cad-report.ps1
- .Place
- CadAuto Core csproj
- Test Has Property

## God Nodes (most connected - your core abstractions)
1. `OutlineFeature2D` - 418 edges
2. `Point2D` - 244 edges
3. `Program` - 238 edges
4. `PlannedDimension` - 172 edges
5. `DimensionPlan` - 151 edges
6. `DimensionPlanner` - 124 edges
7. `DimensionSide` - 120 edges
8. `DimensionLayoutRules` - 105 edges
9. `DimensionDrawer` - 102 edges
10. `Segment2D` - 101 edges

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

## Communities (110 total, 9 thin omitted)

### Community 0 - "Point2D Module"
Cohesion: 0.05
Nodes (33): Datum2D, HoleFeature2D, SlotFeature2D, DimensionPlanner, FunctionalHoleGroupPlan, LooseHoleLineGroup, LooseHoleLocationPlan, LooseHoleMacroGroup (+25 more)

### Community 2 - "HoleFeature2D Module"
Cohesion: 0.11
Nodes (19): BlockReference, SlotFeature, ObjectId, Point2d, SlotArcCandidate, SlotLineCandidate, ThreadArcInfo, ThreadMinorCircleInfo (+11 more)

### Community 3 - "GeometryCollector Module"
Cohesion: 0.19
Nodes (4): DimensionRuleConfig, Dictionary, ThreadArcRules, double

### Community 4 - "DimensionPlan Module"
Cohesion: 0.14
Nodes (10): BlockTableRecord, Database, Entity, Line, ObjectId, Point3d, Polyline, Polyline2d (+2 more)

### Community 5 - "FeatureRecognizer2D Module"
Cohesion: 0.11
Nodes (17): LProfileArcAttachment, LProfileAxisChain, LProfileConcaveTurn, LProfileEdge, LProfileFilletProjection, LProfileHalfEdge, LProfileMatch, LProfileOuterContour (+9 more)

### Community 6 - "OutlineSegment"
Cohesion: 0.11
Nodes (4): StructureFeatureAxis, StructureFeatureExtractor, IList, List

### Community 7 - "Assert Module"
Cohesion: 0.18
Nodes (5): LProfileOverallSideSelection, OutlineEnvelope2D, LProfileOuterContour, LProfileOverallSideScore, LProfileOverallSideSelection

### Community 8 - "Commands Module"
Cohesion: 0.23
Nodes (9): AnnotationMetadata, Database, Entity, IEnumerable, List, string, Transaction, DateTime (+1 more)

### Community 9 - "Segment2D"
Cohesion: 0.11
Nodes (13): StructureFeature, List, StructureMeasurementSelector, Action, HashSet, IList, List, StructureMeasurementSelector (+5 more)

### Community 10 - "DimensionDrawer Module"
Cohesion: 0.10
Nodes (6): Point2D, StructureEndpointRules, IEnumerable, IList, StructureEndpointSpan, IEquatable

### Community 11 - "DimensionPlanner"
Cohesion: 0.19
Nodes (4): DimensionOrientation, DimensionPlanner, string, LProfileMatch

### Community 12 - "DimensionDrawer Module"
Cohesion: 0.08
Nodes (18): CadEntityWriter, BlockTableRecord, Color, Database, double, ObjectId, Point3d, string (+10 more)

### Community 13 - "FeatureRecognizer2D"
Cohesion: 0.14
Nodes (3): Segment2D, FeatureRecognizer2D, IEnumerable

### Community 14 - "OutlineFeature Module"
Cohesion: 0.11
Nodes (18): 10. 一句话总结, 1. 问题陈述, 2. 现状代码（缺陷点）, 3. 目标行为, 4.1 共删谓词（新）, 4.2 伪代码, 4.3 不改动的部分, 4.4 注释同步 (+10 more)

### Community 16 - "CadAuto Core Geometry"
Cohesion: 0.10
Nodes (4): CadAuto.Core.Geometry, CadAuto.Core.Model, CadAuto.Core.Rules, CadAuto.Core.Planning

### Community 17 - "OutlineSegment Module"
Cohesion: 0.13
Nodes (7): DimensionDrawer, BlockTableRecord, List, Transaction, DeferredDim, DimSide, RotatedDimension

### Community 19 - "HoleFeature Module"
Cohesion: 0.18
Nodes (8): OutlineSegment, ObjectId, Point2d, FeatureRecognizer, bool, double, Point2d, Vector2d

### Community 20 - "HoleCalloutPlan Module"
Cohesion: 0.12
Nodes (10): DimensionDeduplicationItem, DimensionDeduplicationRules, PartitionCandidate, double, HashSet, IList, int, List (+2 more)

### Community 22 - ".RecognizeOutlineSlotFeatures"
Cohesion: 0.11
Nodes (8): DimensionType, DimSide, DimensionPlanCadItem, Point3d, DimensionPlanCadMapper, IEnumerable, CadAuto.CadAdapter.Mapping, CadAuto.CadAdapter.Model

### Community 23 - "OutlineGeometryQuery"
Cohesion: 0.24
Nodes (8): DeferredDim, PlacedDim, TextBounds, bool, double, int, string, TextBounds

### Community 24 - "FeatureRecognizer2D.cs"
Cohesion: 0.25
Nodes (4): IDictionary, IEnumerable, ISet, Tuple

### Community 25 - "StackingLayerItem Module"
Cohesion: 0.07
Nodes (19): DimensionSide, DimensionLayoutItem, DimensionLayoutRules, LayoutBlock, HashSet, IDictionary, IEnumerable, IList (+11 more)

### Community 26 - ".VerticalExtensionOverlapsOutlineSegment"
Cohesion: 0.23
Nodes (8): ChamferFeature, Point2d, OutlineFeature, List, Point2d, Entity, Polyline, Polyline2d

### Community 27 - "Commands Module"
Cohesion: 0.12
Nodes (22): HoleFeature, ObjectId, Point3d, DiameterCalloutJig, NativeDiameterDimensioner, BlockTableRecord, Database, Document (+14 more)

### Community 28 - "NativeDiameterDimensioner Module"
Cohesion: 0.18
Nodes (7): FilletFeature, Point2d, Editor, IEnumerable, IList, Point2d, Point3d

### Community 29 - ".HorizontalProbeCrossesInterior"
Cohesion: 0.19
Nodes (4): IList, ISet, SlotArcCandidate, SlotLineCandidate

### Community 30 - "DimensionDrawer Module"
Cohesion: 0.16
Nodes (9): AnnotationCase, AnnotationCaseDecision, AnnotationStrategyIds, List, string, AnnotationCaseStore, IList, List (+1 more)

### Community 31 - "DatumDefinition Module"
Cohesion: 0.14
Nodes (13): 1. 销对之间的孔 → 功能孔, 2. 同侧结构段对齐（方案 1）, 3. OutlineSegment 复述 overall, 4. 结构宽/高 vs overall — 规则过大已修正, 5. 文档与测试, Handoff — 尺寸规则改进（2026-07-20）, 仓库与分支, 工作约定 (+5 more)

### Community 32 - "DimensionLayoutRules.cs"
Cohesion: 0.20
Nodes (6): Arc2D, FilletFeature2D, OutlineGeometryQuery, double, IList, List

### Community 33 - "HoleFeature2D"
Cohesion: 0.20
Nodes (8): LProfileBoundaryGraph, Dictionary, HashSet, IEnumerable, List, Tuple, LProfileConcaveTurn, LProfileHalfEdge

### Community 34 - ".VerticalExtensionOverlapsOutlineSegment"
Cohesion: 0.11
Nodes (9): DimensionPlanner, Func, IEnumerable, IgnoredPoint, IList, ISet, List, StructurePoint (+1 more)

### Community 35 - "DimensionRuleConfig"
Cohesion: 0.33
Nodes (3): DimensionDiagnosticReport, IEnumerable, List

### Community 36 - "SlotArcCandidate Module"
Cohesion: 0.19
Nodes (3): Color, IDictionary, PlacedDim

### Community 37 - "FeatureRecognizer2D"
Cohesion: 0.11
Nodes (18): GeometryCollector, OutlineEntityCandidate, Arc, Editor, Entity, IEnumerable, IList, Line (+10 more)

### Community 38 - "HoleCalloutPlanner"
Cohesion: 0.39
Nodes (4): AnnotationCaseOverlay, HashSet, IList, List

### Community 39 - "CadAuto Core Planning"
Cohesion: 0.11
Nodes (18): A. Overall partition 误删 / 漏删, B. 外框碎段 OutlineSegment + 结构双开, C. StructureOverallPartition 过宽（修“其它图崩了”）, D. U 槽链对齐, E. Phase 2 — `default(Point2D)==(0,0)` 哨兵, F. Phase 3 — Complementary Snap 收紧, graphify, HANDOFF 2026-07-21 — Overall partition / envelope / slot chain / Point2D snap / complementary snap (+10 more)

### Community 41 - "SlotFeature2D"
Cohesion: 0.33
Nodes (4): SlotArcCandidate, SlotLineCandidate, CadAuto.Core.Recognition, CadAuto.Core.Tests

### Community 43 - "FeatureRecognizer"
Cohesion: 0.14
Nodes (7): AutoFixDimOutputScope, CadToCoreModelMapper, IEnumerable, List, DatumDefinition, HoleKind, HoleKind2D

### Community 44 - ".IsVertical"
Cohesion: 0.08
Nodes (5): Program, Action, HashSet, int, List

### Community 45 - ".AppendDimensionDiagnostics"
Cohesion: 0.29
Nodes (6): DiagnosticDimensionSide, IEnumerable, StringBuilder, DiagnosticRunContext, Exception, KeyValuePair

### Community 46 - ".AddLooseHoleCenterDistances"
Cohesion: 0.40
Nodes (4): IgnoredPoint, StructurePoint, SuppressReason, string

### Community 48 - "AnnotationCaseStore"
Cohesion: 0.50
Nodes (3): IndexedLayoutItem, StackingLayerItem, TextSlideCandidate

### Community 50 - "AUTOFIXDIM Agent Rules"
Cohesion: 0.06
Nodes (42): AUTOFIXDIM Agent Rules, Annotation Product Invariants, Explicit Validation Authorization, Graphify-First Codebase Navigation, Safe File Operations, Deployment and Manual Verification, Four-Direction Automated Regression, Manual AutoCAD Verification Flow (+34 more)

### Community 51 - "规则债务：死 API / 双实现对照表"
Cohesion: 0.08
Nodes (25): 1. 目的, 2. 状态标签, 3.1 `StructureSuppressionRules` — 台阶有用性 / 冗余簇, 3.2 `StructureSuppressionRules` — 四向延伸线 public 版（死）, 3.3 `DimensionDeduplicationRules` — 对称半边, 3.4 文档幽灵符号, 3.5 明确不是死 API（对照，防误删）, 3. 死 API 对照表 (+17 more)

### Community 53 - "重构验证手册（分支 `refactor/v2-roadmap`）"
Cohesion: 0.09
Nodes (21): 3.1 采基线（如已在上一轮采过可复用）, 3.2 用新 DLL 重跑同一张图同一命令, 4.1 诊断与报告（零出图变化）, 4.2 XData 与清理（**有行为变化**）, 4.3 识别（**有行为变化**）, 4.4 包络与失败回退（**有行为变化，风险最高**）, 4.5 部署与加载, 5.1 布局回归报告新鲜度 (+13 more)

### Community 54 - "DimensionRuleConfig"
Cohesion: 0.09
Nodes (12): CommandMethod, Commands, DiagnosticRunContext, Document, Editor, IList, int, List (+4 more)

### Community 55 - "DimensionPlanner.cs"
Cohesion: 0.29
Nodes (6): DimStyleManager, Color, Database, ObjectId, string, Transaction

### Community 56 - ".SuppressOutlineSegmentsOnOverallEnvelope"
Cohesion: 0.40
Nodes (3): Circle2D, DrawingGeometry, List

### Community 57 - "SlotFeature2D"
Cohesion: 0.14
Nodes (5): OutlineArc, ObjectId, Point2d, Arc, SlotArcRules

### Community 58 - ".ResolveDimStyle"
Cohesion: 0.12
Nodes (11): GeneratedAnnotation, ObjectId, AutoFixDimOutputScope, DiagnosticBounds, DiagnosticFileIdentity, CadAuto.CadAdapter.Recognition, CadAuto.CadAdapter, AutoFixtureDim (+3 more)

### Community 59 - "CoordinateFrame2D"
Cohesion: 0.22
Nodes (7): CornerFeatureLeaderJig, double, JigPrompts, SamplerStatus, string, WorldDraw, DrawJig

### Community 60 - "run-cad-regression-suite.ps1"
Cohesion: 0.22
Nodes (11): ConvertTo-LispString(), Get-DllEvidence(), Get-NormalizedReportHash(), Get-NormalizedVisualReportHash(), Get-Sha256(), Get-VisualGateStatus(), Invoke-CoreCadCase(), Invoke-CoreConsole() (+3 more)

### Community 61 - ".ArbitrateOuterContourStepOverallRemainders"
Cohesion: 0.14
Nodes (10): OutlineFeature2D, List, AnnotationCaseRuntime, IList, IEnumerable, DimensionPlanner, IgnoredPoint, IList (+2 more)

### Community 63 - "DimensionExtensionLineRenderer"
Cohesion: 0.12
Nodes (11): CoordinateFrame2D, double, CoordinateFrameModelTransform, IEnumerable, List, DimensionPlan, Dictionary, int (+3 more)

### Community 65 - "write-cad-visual-report.ps1"
Cohesion: 0.21
Nodes (6): Get-Number(), Get-Orientation(), Get-Point(), Test-PointInRect(), Test-SegmentRect(), Test-SegmentsIntersect()

### Community 67 - ".ResolveAnnotationLayer"
Cohesion: 0.18
Nodes (7): LayerManager, Database, string, Transaction, DiameterJigResult, CadAuto.CadAdapter.Environment, CadAuto.CadAdapter.Rendering

### Community 68 - "旋转不变量（四向标注一致）v3"
Cohesion: 0.18
Nodes (10): CAD 真图轨 F338（test2 拓扑 / 合成边）, Phase 状态, Signature, 产品共识（grill-me 2026-08-07 锁定）, 旋转不变量（四向标注一致）v3, 架构, 核心测试, 直角单测 F215 (+2 more)

### Community 70 - "run-hole-slot-cad-regression.ps1"
Cohesion: 0.36
Nodes (8): ConvertTo-LispString(), Get-DllEvidence(), Get-NormalizedReportHash(), Get-NormalizedVisualHash(), Get-Sha256(), Invoke-CoreConsole(), Invoke-HoleSlotRun(), Write-Json()

### Community 72 - "M4 Round 2 calibration matrix"
Cohesion: 0.22
Nodes (8): Confirmed synthetic defects, Final calibrated evidence, Independent M4 Round 2 verification / WarningOnly evidence passed, Legal-contact counterexamples, M4 Round 2 calibration matrix, Real saved final DWG positive evidence, Round 1 archived false-positive modes, Round 2 results

### Community 73 - "DebugAnnotationRenderer Module"
Cohesion: 0.14
Nodes (13): Entity, DebugAnnotationRenderer, Color, Database, ObjectId, Point2d, Point3d, string (+5 more)

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

### Community 95 - "5. 测试门禁"
Cohesion: 0.50
Nodes (4): 5.1 每次规则修改的最低证据, 5.2 分级门禁, 5.3 基线变更规则, 5. 测试门禁

### Community 101 - ".Place"
Cohesion: 0.16
Nodes (10): DimensionCandidateDiagnostic, List, DimensionCandidateDecision, DimensionCandidateOwnerKind, DimensionCandidateRole, DimensionCandidateSemantics, IEnumerable, List (+2 more)

### Community 105 - "Test Has Property"
Cohesion: 0.60
Nodes (3): Get-PhysicalRank(), Test-DimensionSelector(), Test-HasProperty()

## Knowledge Gaps
- **177 isolated node(s):** `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests`, `AutoFixDimOutputScope`, `DiagnosticBounds` (+172 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **9 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `OutlineFeature2D` connect `.ArbitrateOuterContourStepOverallRemainders` to `Point2D Module`, `OutlineFeature2D Module`, `FeatureRecognizer2D Module`, `OutlineSegment`, `Assert Module`, `Segment2D`, `DimensionDrawer Module`, `DimensionPlanner`, `FeatureRecognizer2D`, `FeatureRecognizer Module`, `CadAuto Core Geometry`, `OutlineSegment Module`, `.CreateDefault`, `DimensionPlanner`, `FeatureRecognizer2D.cs`, `StackingLayerItem Module`, `.HorizontalProbeCrossesInterior`, `DimensionLayoutRules.cs`, `HoleFeature2D`, `.VerticalExtensionOverlapsOutlineSegment`, `HoleCalloutPlanner`, `DimensionDrawer Module`, `FeatureRecognizer`, `.IsVertical`, `DimSide Module`, `.PointsEqual`, `.DrawSlotRadiusLeadersWithJig`, `.SuppressOutlineSegmentsOnOverallEnvelope`, `.PromptForDatumHole`, `DimensionExtensionLineRenderer`, `.AssertLProfileRuleEndpointsOnBoundary`, `FeatureRecognizer.cs`, `.SuppressStructureClosedChainRedundantPositioningOnSide`, `.AssertTest2BottomContourPlan`, `.OrthogonalRotatedVerticalChainKeepsRealWidths`?**
  _High betweenness centrality (0.222) - this node is a cross-community bridge._
- **Why does `DimensionDrawer` connect `OutlineSegment Module` to `DimensionRuleConfig`, `SlotArcCandidate Module`, `GeometryCollector Module`, `DebugAnnotationRenderer Module`, `FeatureRecognizer`, `DimensionDrawer Module`, `.AppendDimensionDiagnostics`, `HoleCalloutPlan Module`, `.RecognizeOutlineSlotFeatures`, `OutlineGeometryQuery`, `DimensionRuleConfig`, `StackingLayerItem Module`, `.VerticalExtensionOverlapsOutlineSegment`, `NativeDiameterDimensioner Module`, `.ArbitrateOuterContourStepOverallRemainders`, `DimensionExtensionLineRenderer`?**
  _High betweenness centrality (0.121) - this node is a cross-community bridge._
- **Why does `Point2D` connect `DimensionDrawer Module` to `Point2D Module`, `OutlineFeature2D Module`, `FeatureRecognizer2D Module`, `OutlineSegment`, `Assert Module`, `Segment2D`, `DimensionPlanner`, `FeatureRecognizer2D`, `CadAuto Core Geometry`, `.CreateDefault`, `HoleCalloutPlan Module`, `FeatureRecognizer2D.cs`, `StackingLayerItem Module`, `.HorizontalProbeCrossesInterior`, `DimensionLayoutRules.cs`, `HoleFeature2D`, `.VerticalExtensionOverlapsOutlineSegment`, `DimensionDrawer Module`, `SlotFeature2D`, `.IsVertical`, `.AddLooseHoleCenterDistances`, `DimSide Module`, `AnnotationCaseStore`, `.SuppressOutlineSegmentsOnOverallEnvelope`, `.ArbitrateOuterContourStepOverallRemainders`, `.PromptForDatumHole`, `DimensionExtensionLineRenderer`, `FeatureRecognizer.cs`, `.Place`, `.OrthogonalRotatedVerticalChainKeepsRealWidths`?**
  _High betweenness centrality (0.108) - this node is a cross-community bridge._
- **What connects `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests` to the rest of the system?**
  _177 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Point2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.052342423334332235 - nodes in this community are weakly interconnected._
- **Should `OutlineFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.14112903225806453 - nodes in this community are weakly interconnected._
- **Should `HoleFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.10707070707070707 - nodes in this community are weakly interconnected._