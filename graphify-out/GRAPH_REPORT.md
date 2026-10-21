# Graph Report - 9.1 托压块版本  (2026-10-21)

## Corpus Check
- 269 files · ~649,963 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 3141 nodes · 10876 edges · 126 communities (113 shown, 13 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 850 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `03dbc0db`
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
- FeatureRecognizer Module
- CadAuto Core Geometry
- OutlineSegment Module
- .CreateDefault
- .AdjustShortLocalDimensionTextPositions
- HoleCalloutPlan Module
- DimensionPlanner
- .RecognizeOutlineSlotFeatures
- OutlineGeometryQuery
- StackingLayerItem Module
- .VerticalExtensionOverlapsOutlineSegment
- Commands Module
- DimensionDiagnosticReport
- .HorizontalProbeCrossesInterior
- DimensionDrawer Module
- DatumDefinition Module
- DimensionLayoutRules.cs
- .VerticalExtensionOverlapsOutlineSegment
- SlotArcCandidate Module
- FeatureRecognizer2D
- HoleCalloutPlanner
- CadAuto Core Planning
- DimensionDrawer Module
- SlotFeature2D
- FeatureRecognizer
- .AppendDimensionDiagnostics
- .ResolveDimStyle
- AnnotationCaseStore
- .PointsEqual
- AUTOFIXDIM Agent Rules
- 规则债务：死 API / 双实现对照表
- 重构验证手册（分支 `refactor/v2-roadmap`）
- DimensionExtensionLineRenderer
- Point2d
- HoleFeature2D
- DimensionExtensionLineRenderer
- .ResolveDimStyle
- HoleFeature2D
- run-cad-regression-suite.ps1
- .AddDimensionDebugLabel
- DimensionRuleConfig
- write-cad-visual-report.ps1
- .CreateOutlinePlanLocal
- .ResolveAnnotationLayer
- 旋转不变量（四向标注一致）v3
- .AddLooseHoleCenterDistances
- run-hole-slot-cad-regression.ps1
- .PointsEqual
- M4 Round 2 calibration matrix
- 逐条结论与证据
- Core Test ↔ Minimal DWG Regression
- CAD 自动标注规则与回归体系整改计划
- M5 正式定义（2026-07-31）
- M4-positive-collisions 视觉真值
- run-core-cad-regression.ps1
- .ToDeduplicationItem
- 3. 根因
- DimensionPlanner.cs
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
- OutlineTopologyDiagnostic
- CadAuto Core csproj
- .ThreeViewFirstAngleKeepsLengthAndHeightOnFrontAndWidthOnTop
- DimensionDiagnosticReport
- Test Has Property
- 逐条结论与证据
- .AssertLProfileRuleEndpointsOnBoundary
- .RunRegisteredTests
- .IsDatumBlock
- DimensionPlanner.BoundaryInformation.cs
- .Map
- .ToDeduplicationItem
- Core 回归执行恢复与凹轮廓基准夹点修复
- HoleKind2D
- 逐条结论与证据
- .CollectHoleSourcesFromOutlineSelection
- Core 回归执行恢复与凹轮廓基准夹点修复
- 当前标注规则
- 标注规则消融实验
- 不遮挡中心线标注规则
- CAD 标注规则单规则消融报告
- DrawingGeometry
- AblationExperiments.md
- 核心原则
- README.md

## God Nodes (most connected - your core abstractions)
1. `OutlineFeature2D` - 497 edges
2. `Point2D` - 328 edges
3. `Program` - 325 edges
4. `PlannedDimension` - 221 edges
5. `DimensionPlan` - 197 edges
6. `DimensionSide` - 175 edges
7. `DimensionPlanner` - 132 edges
8. `Segment2D` - 123 edges
9. `DimensionLayoutRules` - 118 edges
10. `DimensionPlanner` - 111 edges

## Surprising Connections (you probably didn't know these)
- `Annotation Product Invariants` --semantically_similar_to--> `Real Geometry Attachment Rules`  [INFERRED] [semantically similar]
  AGENTS.md → docs/DimensionRules.md
- `Current Source Command Surface` --semantically_similar_to--> `Current ASD Command Surface`  [INFERRED] [semantically similar]
  docs/Environment.md → README.md
- `CAD Smoke-Test Helper` --semantically_similar_to--> `Manual CAD Smoke Test`  [INFERRED] [semantically similar]
  README.md → docs/Deployment.md
- `DatumDefinition` --references--> `StructureDatumMarker2D`  [EXTRACTED]
  CadAuto.CadAdapter/Model/DatumDefinition.cs → CadAuto.Core/Model/Datum2D.cs
- `PreparedView` --references--> `DatumDefinition`  [EXTRACTED]
  ThreeViewTrialCommand.cs → CadAuto.CadAdapter/Model/DatumDefinition.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **AUTOFIXDIM Annotation Rule System** — docs_recognitionrules_recognition_rules, docs_dimensionrules_dimension_rules, agents_annotation_product_invariants, docs_environment_source_map [INFERRED 0.85]
- **Four-Direction Regression Evidence Chain** — docs_dimensionlayoutregression_dl01_fixture, docs_dimensionlayoutregression_trace_and_report_archiving, docs_dimensionlayoutregression_structured_report_validation, docs_dimensionlayoutregression_visual_image_review, docs_dimensionlayoutregression_completion_gate [EXTRACTED 1.00]

## Communities (126 total, 13 thin omitted)

### Community 0 - "Point2D Module"
Cohesion: 0.14
Nodes (5): Color, IDictionary, IEnumerable, PlacedDim, TextBounds

### Community 1 - "OutlineFeature2D Module"
Cohesion: 0.06
Nodes (35): HoleFeature, ObjectId, Point3d, DiameterCalloutJig, NativeDiameterDimensioner, BlockTableRecord, Database, Document (+27 more)

### Community 2 - "HoleFeature2D Module"
Cohesion: 0.12
Nodes (8): OutlineFeature2D, List, DimensionPlanner, IEnumerable, IgnoredPoint, IList, List, StructurePoint

### Community 3 - "CadEntityWriter"
Cohesion: 0.09
Nodes (16): DiagnosticRunContext, BlockTableRecord, Database, Entity, Line, List, ObjectId, Point3d (+8 more)

### Community 5 - "FeatureRecognizer2D Module"
Cohesion: 0.06
Nodes (10): DimensionPlanner, IDictionary, IEnumerable, IList, ISet, List, string, Tuple (+2 more)

### Community 6 - "AnnotationCase"
Cohesion: 0.10
Nodes (17): GeometryCollector, OutlineEntityCandidate, Arc, Editor, Entity, IList, Line, List (+9 more)

### Community 8 - "Commands Module"
Cohesion: 0.10
Nodes (19): CadEntityWriter, BlockTableRecord, Color, Database, double, Entity, IList, ObjectId (+11 more)

### Community 9 - "Segment2D"
Cohesion: 0.05
Nodes (32): AnnotationCase, AnnotationCaseDecision, AnnotationStrategyIds, List, string, AnnotationCaseOverlay, HashSet, IList (+24 more)

### Community 10 - "DimensionDrawer Module"
Cohesion: 0.11
Nodes (9): CadToCoreModelMapper, IEnumerable, List, DimensionDrawer, BlockTableRecord, ObjectId, RotatedDimension, Transaction (+1 more)

### Community 11 - "DimensionPlanner"
Cohesion: 0.11
Nodes (8): DimensionOrientation, DimensionPlanner, string, Tuple, LProfileLocalAnchorPair, LProfileMatch, LProfileOuterContour, LProfileStepCoverageEvidence

### Community 12 - "DimensionDrawer Module"
Cohesion: 0.12
Nodes (28): aggregate(), behavior(), canonical(), compare(), digest(), evaluate(), layout_metrics(), matches() (+20 more)

### Community 13 - "FeatureRecognizer2D"
Cohesion: 0.18
Nodes (7): ChamferFeature, Point2d, OutlineSegment, ObjectId, Point2d, Point2d, Vector2d

### Community 14 - "OutlineFeature Module"
Cohesion: 0.11
Nodes (18): 10. 一句话总结, 1. 问题陈述, 2. 现状代码（缺陷点）, 3. 目标行为, 4.1 共删谓词（新）, 4.2 伪代码, 4.3 不改动的部分, 4.4 注释同步 (+10 more)

### Community 16 - "CadAuto Core Geometry"
Cohesion: 0.07
Nodes (16): CoordinateFrameModelTransform, FilletFeature2D, HoleKind2D, HoleCalloutCluster, List, SlotArcCandidate, SlotLineCandidate, DimensionLayoutRules (+8 more)

### Community 17 - "OutlineSegment Module"
Cohesion: 0.09
Nodes (13): CoordinateFrame2D, double, IEnumerable, List, CenterlineEndpoint2D, StructureDatumMarker2D, DimensionPlanner, IEnumerable (+5 more)

### Community 18 - ".CreateDefault"
Cohesion: 0.50
Nodes (3): BoundaryInformationConstraint, IEnumerable, int

### Community 19 - ".AdjustShortLocalDimensionTextPositions"
Cohesion: 0.10
Nodes (3): AblationInput, Dictionary, Func

### Community 20 - "HoleCalloutPlan Module"
Cohesion: 0.12
Nodes (10): DimensionDeduplicationItem, DimensionDeduplicationRules, PartitionCandidate, double, HashSet, IList, int, List (+2 more)

### Community 21 - "DimensionPlanner"
Cohesion: 0.14
Nodes (12): LProfileBoundaryGraph, LProfileConcaveTurn, LProfileEdge, LProfileFilletProjection, LProfileHalfEdge, LProfileOverallSideScore, LProfileOverallSplitEvidence, LProfileStepCoverageEvidence (+4 more)

### Community 22 - ".RecognizeOutlineSlotFeatures"
Cohesion: 0.17
Nodes (11): OrthographicViewFrame, OrthographicViewRole, OrthographicViewSetAssigner, OrthographicViewSetAssignment, PartExtentKind, ViewExtentOwnership, List, string (+3 more)

### Community 23 - "OutlineGeometryQuery"
Cohesion: 0.12
Nodes (14): BlockReference, SlotArcCandidate, SlotLineCandidate, ThreadArcInfo, Editor, IEnumerable, IList, ISet (+6 more)

### Community 25 - "StackingLayerItem Module"
Cohesion: 0.11
Nodes (8): DimensionPlanner, Func, IEnumerable, IgnoredPoint, IList, ISet, List, StructurePoint

### Community 26 - ".VerticalExtensionOverlapsOutlineSegment"
Cohesion: 0.24
Nodes (9): CenterlineEndpointMatch, FunctionalHoleGroupPlan, IgnoredPoint, LooseHoleLineGroup, LooseHoleMacroGroup, StructurePoint, SuppressReason, List (+1 more)

### Community 27 - "Commands Module"
Cohesion: 0.05
Nodes (42): ablation-confirmation.log（exit=1，14.358s）, ablation-rerun.log（exit=1，14.413s）, datum-reference-after.log（exit=0，0.316s）, detailed-runner-self-check.log（exit=0，0.127s）, final-ablation-self-check.log（exit=0，1.043s）, final-datum-reference-self-check.log（exit=0，0.348s）, final-default-gate.log（exit=1，0.868s）, final-full-diagnostic.log（exit=1，1.046s） (+34 more)

### Community 28 - "DimensionDiagnosticReport"
Cohesion: 0.20
Nodes (10): AutoFixDimOutputScope, DiagnosticDimensionSide, Commands, IEnumerable, int, string, StringBuilder, DiagnosticRunContext (+2 more)

### Community 29 - ".HorizontalProbeCrossesInterior"
Cohesion: 0.05
Nodes (22): DimensionLayoutItem, DimensionLayoutRules, IndexedLayoutItem, LayoutBlock, StackingLayerItem, TextSlideCandidate, HashSet, IDictionary (+14 more)

### Community 30 - "DimensionDrawer Module"
Cohesion: 0.17
Nodes (5): LProfileAxisChain, LProfileMatch, IEnumerable, LProfileArcAttachment, LProfileAxisChain

### Community 31 - "DatumDefinition Module"
Cohesion: 0.14
Nodes (13): 1. 销对之间的孔 → 功能孔, 2. 同侧结构段对齐（方案 1）, 3. OutlineSegment 复述 overall, 4. 结构宽/高 vs overall — 规则过大已修正, 5. 文档与测试, Handoff — 尺寸规则改进（2026-07-20）, 仓库与分支, 工作约定 (+5 more)

### Community 32 - "DimensionLayoutRules.cs"
Cohesion: 0.15
Nodes (5): Arc2D, OutlineGeometryQuery, double, IList, List

### Community 34 - ".VerticalExtensionOverlapsOutlineSegment"
Cohesion: 0.08
Nodes (25): 10.1 Core 布局, 10.2 renderer 链, 10. 布局与 CAD 绘制, 11. 最终校验, 12. 回归定位入口, 13. 建议下一位 agent 的分析顺序, 14. 本次未做的事, 1. 分析边界 (+17 more)

### Community 37 - "FeatureRecognizer2D"
Cohesion: 0.16
Nodes (3): List, DeferredDim, DimSide

### Community 38 - "HoleCalloutPlanner"
Cohesion: 0.06
Nodes (22): Datum2D, List, HoleFeature2D, SlotFeature2D, LooseHoleLocationPlan, IList, DimensionPlanner, Func (+14 more)

### Community 39 - "CadAuto Core Planning"
Cohesion: 0.11
Nodes (18): A. Overall partition 误删 / 漏删, B. 外框碎段 OutlineSegment + 结构双开, C. StructureOverallPartition 过宽（修“其它图崩了”）, D. U 槽链对齐, E. Phase 2 — `default(Point2D)==(0,0)` 哨兵, F. Phase 3 — Complementary Snap 收紧, graphify, HANDOFF 2026-07-21 — Overall partition / envelope / slot chain / Point2D snap / complementary snap (+10 more)

### Community 40 - "DimensionDrawer Module"
Cohesion: 0.07
Nodes (29): ablation-after.log (exit=1), ablation-self-check.log (exit=0), after-build.log (exit=0), baseline-after-repeat.log (exit=0), baseline-before.log (exit=0), before-build.log (exit=0), coverage-after.log (exit=0), coverage-before.log (exit=1) (+21 more)

### Community 41 - "SlotFeature2D"
Cohesion: 0.18
Nodes (10): CAD 真图轨 F338（test2 拓扑 / 合成边）, Phase 状态, Signature, 产品共识（grill-me 2026-08-07 锁定）, 旋转不变量（四向标注一致）v3, 架构, 核心测试, 直角单测 F215 (+2 more)

### Community 43 - "FeatureRecognizer"
Cohesion: 0.13
Nodes (3): Segment2D, ChamferFeature2D, StructureSuppressionRules

### Community 45 - ".AppendDimensionDiagnostics"
Cohesion: 0.08
Nodes (7): AnnotationCaseRuntime, AnnotationCaseSnapshot, IList, CommandMethod, Document, Editor, IList

### Community 46 - ".ResolveDimStyle"
Cohesion: 0.18
Nodes (4): LProfileArcAttachment, LProfileBoundaryGraph, LProfileEdge, LProfileLocalAnchorCandidate

### Community 48 - "AnnotationCaseStore"
Cohesion: 0.14
Nodes (13): DimensionType, DeferredDim, DimSide, PlacedDim, TextBounds, bool, double, int (+5 more)

### Community 49 - ".PointsEqual"
Cohesion: 0.11
Nodes (3): StructureFeatureExtractor, IList, List

### Community 50 - "AUTOFIXDIM Agent Rules"
Cohesion: 0.06
Nodes (42): AUTOFIXDIM Agent Rules, Annotation Product Invariants, Explicit Validation Authorization, Graphify-First Codebase Navigation, Safe File Operations, Deployment and Manual Verification, Four-Direction Automated Regression, Manual AutoCAD Verification Flow (+34 more)

### Community 51 - "规则债务：死 API / 双实现对照表"
Cohesion: 0.08
Nodes (25): 1. 目的, 2. 状态标签, 3.1 `StructureSuppressionRules` — 台阶有用性 / 冗余簇, 3.2 `StructureSuppressionRules` — 四向延伸线 public 版（死）, 3.3 `DimensionDeduplicationRules` — 对称半边, 3.4 文档幽灵符号, 3.5 明确不是死 API（对照，防误删）, 3. 死 API 对照表 (+17 more)

### Community 53 - "重构验证手册（分支 `refactor/v2-roadmap`）"
Cohesion: 0.09
Nodes (21): 3.1 采基线（如已在上一轮采过可复用）, 3.2 用新 DLL 重跑同一张图同一命令, 4.1 诊断与报告（零出图变化）, 4.2 XData 与清理（**有行为变化**）, 4.3 识别（**有行为变化**）, 4.4 包络与失败回退（**有行为变化，风险最高**）, 4.5 部署与加载, 5.1 布局回归报告新鲜度 (+13 more)

### Community 54 - "DimensionExtensionLineRenderer"
Cohesion: 0.12
Nodes (10): FilletFeature, Point2d, SlotFeature, ObjectId, Point2d, Editor, IEnumerable, IList (+2 more)

### Community 55 - "Point2d"
Cohesion: 0.12
Nodes (13): CornerCalloutJigResult, CornerCalloutRenderer, CornerFeatureLeaderJig, Database, double, Editor, JigPrompts, ObjectId (+5 more)

### Community 56 - "HoleFeature2D"
Cohesion: 0.20
Nodes (11): Candidate, DimensionExtensionLineRules, ExtensionLineLayoutItem, ExtensionLineRenderPlan, bool, double, IEnumerable, IList (+3 more)

### Community 57 - "DimensionExtensionLineRenderer"
Cohesion: 0.12
Nodes (7): Point2D, LProfileLocalAnchorCandidate, LProfileLocalAnchorPair, StructureEndpointRules, IEnumerable, StructureEndpointSpan, IEquatable

### Community 58 - ".ResolveDimStyle"
Cohesion: 0.11
Nodes (14): DimensionCandidateDiagnostic, List, DimensionCandidateDecision, DimensionCandidateOwnerKind, DimensionCandidateRole, DimensionCandidateSemantics, IEnumerable, List (+6 more)

### Community 59 - "HoleFeature2D"
Cohesion: 0.04
Nodes (3): Program, HashSet, int

### Community 60 - "run-cad-regression-suite.ps1"
Cohesion: 0.22
Nodes (11): ConvertTo-LispString(), Get-DllEvidence(), Get-NormalizedReportHash(), Get-NormalizedVisualReportHash(), Get-Sha256(), Get-VisualGateStatus(), Invoke-CoreCadCase(), Invoke-CoreConsole() (+3 more)

### Community 61 - ".AddDimensionDebugLabel"
Cohesion: 0.19
Nodes (9): OutlineFeature, List, Point2d, FeatureRecognizer, bool, double, Entity, Polyline (+1 more)

### Community 63 - "DimensionRuleConfig"
Cohesion: 0.13
Nodes (10): BoundaryInformationConstraint, DimensionPlanner, List, string, OutlineTopologyDiagnostic, OutlineTopologyEdge, OutlineTopologyNode, OutlineTopologyStep (+2 more)

### Community 65 - "write-cad-visual-report.ps1"
Cohesion: 0.21
Nodes (6): Get-Number(), Get-Orientation(), Get-Point(), Test-PointInRect(), Test-SegmentRect(), Test-SegmentsIntersect()

### Community 66 - ".CreateOutlinePlanLocal"
Cohesion: 0.23
Nodes (9): AnnotationMetadata, Database, Entity, IEnumerable, List, string, Transaction, DateTime (+1 more)

### Community 67 - ".ResolveAnnotationLayer"
Cohesion: 0.17
Nodes (12): SupportBlockCommand, Arc, Database, Document, Editor, Entity, List, ObjectId (+4 more)

### Community 68 - "旋转不变量（四向标注一致）v3"
Cohesion: 0.13
Nodes (9): LProfileOuterContour, LProfileOverallSideSelection, IList, OutlineEnvelope2D, LProfileConcaveTurn, LProfileFilletProjection, LProfileOverallSideScore, LProfileOverallSideSelection (+1 more)

### Community 69 - ".AddLooseHoleCenterDistances"
Cohesion: 0.12
Nodes (9): Program, List, Action, Program, TestExecutionRecord, bool, List, string (+1 more)

### Community 70 - "run-hole-slot-cad-regression.ps1"
Cohesion: 0.36
Nodes (8): ConvertTo-LispString(), Get-DllEvidence(), Get-NormalizedReportHash(), Get-NormalizedVisualHash(), Get-Sha256(), Invoke-CoreConsole(), Invoke-HoleSlotRun(), Write-Json()

### Community 71 - ".PointsEqual"
Cohesion: 0.10
Nodes (19): BoundaryInformationCompletion, CAD 标注规则单规则消融报告, FunctionalHoleAssignment, LocalEnvelopeSuppression, MeasuredDeduplication, MirroredDeduplication, NonPinNearestDatum, OutlineOverallPartition (+11 more)

### Community 72 - "M4 Round 2 calibration matrix"
Cohesion: 0.22
Nodes (8): Confirmed synthetic defects, Final calibrated evidence, Independent M4 Round 2 verification / WarningOnly evidence passed, Legal-contact counterexamples, M4 Round 2 calibration matrix, Real saved final DWG positive evidence, Round 1 archived false-positive modes, Round 2 results

### Community 73 - "逐条结论与证据"
Cohesion: 0.10
Nodes (19): BoundaryInformationCompletion, CAD 标注规则单规则消融报告, FunctionalHoleAssignment, LocalEnvelopeSuppression, MeasuredDeduplication, MirroredDeduplication, NonPinNearestDatum, OutlineOverallPartition (+11 more)

### Community 74 - "Core Test ↔ Minimal DWG Regression"
Cohesion: 0.29
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

### Community 80 - ".ToDeduplicationItem"
Cohesion: 0.19
Nodes (4): IList, ISet, SlotArcCandidate, SlotLineCandidate

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

### Community 94 - "DimensionPlanCadItem"
Cohesion: 0.07
Nodes (18): GeneratedAnnotation, ObjectId, HoleKind, DiameterJigResult, AutoFixDimOutputScope, DiagnosticBounds, DiagnosticCenterlineEndpoint, DiagnosticFileIdentity (+10 more)

### Community 95 - "5. 测试门禁"
Cohesion: 0.50
Nodes (4): 5.1 每次规则修改的最低证据, 5.2 分级门禁, 5.3 基线变更规则, 5. 测试门禁

### Community 101 - "OutlineTopologyDiagnostic"
Cohesion: 0.09
Nodes (17): DimStyleManager, Color, Database, ObjectId, Transaction, LayerManager, Database, string (+9 more)

### Community 103 - ".ThreeViewFirstAngleKeepsLengthAndHeightOnFrontAndWidthOnTop"
Cohesion: 0.19
Nodes (7): IEnumerable, CenterlineEndpointDefinition, DatumDefinition, List, Point3d, PinGroupHorizontalLayoutPreference, PinGroupVerticalLayoutPreference

### Community 104 - "DimensionDiagnosticReport"
Cohesion: 0.22
Nodes (8): ASD4 正确性基线：L01-13, 下一步建议, 仍待人工确认, 公差及变化范围, 几何和标注依据, 可复用代码, 标准答案与成对检查, 范围与来源

### Community 105 - "Test Has Property"
Cohesion: 0.60
Nodes (3): Get-PhysicalRank(), Test-DimensionSelector(), Test-HasProperty()

### Community 106 - "逐条结论与证据"
Cohesion: 0.10
Nodes (19): BoundaryInformationCompletion, CAD 标注规则单规则消融报告, FunctionalHoleAssignment, LocalEnvelopeSuppression, MeasuredDeduplication, MirroredDeduplication, NonPinNearestDatum, OutlineOverallPartition (+11 more)

### Community 107 - ".AssertLProfileRuleEndpointsOnBoundary"
Cohesion: 0.17
Nodes (9): DimensionExtensionLineRenderer, Color, Database, IList, ObjectId, Point3d, string, Tuple (+1 more)

### Community 113 - ".RunRegisteredTests"
Cohesion: 0.18
Nodes (9): 下次匹配条件, 不能套用, 代码改动, 水平销距按本组轮廓侧，不同侧的组间定位不对齐, 症状类别, 确认的标注规则, 诊断证据, 错误代码（文件与分支） (+1 more)

### Community 120 - ".IsDatumBlock"
Cohesion: 0.40
Nodes (4): SVG 导出验证摘要, 命令与结果, 失败、未执行与风险, 文件与产物

### Community 123 - ".Map"
Cohesion: 0.16
Nodes (5): OutlineArc, ObjectId, Point2d, Arc, SlotArcRules

### Community 125 - "Core 回归执行恢复与凹轮廓基准夹点修复"
Cohesion: 0.17
Nodes (10): Core 回归执行恢复与凹轮廓基准夹点修复, 为什么原来有 238 项未执行, 尚未解决, 并发现场诊断, 新的显式诊断执行方式, 最小修复, 消融重跑与默认行为差异, 目标失败的根因和最小输入 (+2 more)

### Community 127 - "HoleKind2D"
Cohesion: 0.18
Nodes (4): DimensionPlanner, string, FeatureDiagnosticCounts, StructureCoveragePolicy

### Community 128 - "逐条结论与证据"
Cohesion: 0.17
Nodes (12): BoundaryInformationCompletion, FunctionalHoleAssignment, LocalEnvelopeSuppression, MeasuredDeduplication, MirroredDeduplication, NonPinNearestDatum, OutlineOverallPartition, ParallelDatumCompaction (+4 more)

### Community 130 - "Core 回归执行恢复与凹轮廓基准夹点修复"
Cohesion: 0.17
Nodes (12): Core 回归执行恢复与凹轮廓基准夹点修复, 为什么原来有 238 项未执行, 尚未解决, 并发现场诊断, 新的显式诊断执行方式, 最小修复, 消融重跑与默认行为差异, 目标失败的根因和最小输入 (+4 more)

### Community 131 - "当前标注规则"
Cohesion: 0.18
Nodes (11): 10. 当前边界, 1. 识别范围, 2. 尺寸对象与夹点, 3. 总体与结构尺寸, 4. 孔、销孔与定位链, 5. 孔阵列、槽与中心线端点, 6. 容差与标注文字, 7. 尺寸排列与避让 (+3 more)

### Community 134 - "标注规则消融实验"
Cohesion: 0.20
Nodes (10): 2026-10-17 执行证据, 两个配置必须分开, 代码与数据地图, 后续验收门槛, 已隔离的实际规则, 指标含义与局限, 标注规则消融实验, 目的与范围 (+2 more)

### Community 135 - "不遮挡中心线标注规则"
Cohesion: 0.25
Nodes (8): 不遮挡中心线标注规则, 双圆弧槽右侧布局, 外轮廓避让, 已确认的典型故障, 普通孔四孔阵, 目的, 诊断要求, 验收条件

### Community 136 - "CAD 标注规则单规则消融报告"
Cohesion: 0.25
Nodes (7): CAD 标注规则单规则消融报告, 单规则比较（固定成对可评估子集）, 完整系统基线（部分契约，不是全图真值）, 现有回归门禁, 真值、依赖和未评估边界, 重复运行, 默认失败及证据索引

### Community 137 - "DrawingGeometry"
Cohesion: 0.40
Nodes (3): Circle2D, DrawingGeometry, List

### Community 140 - "核心原则"
Cohesion: 0.33
Nodes (6): 1. 先确定尺寸对象，再确定夹点, 2. 按布局侧选择中心线端点, 3. 普通孔和销孔中心距, 4. 单圆弧 U 型槽, 5. 双圆弧槽, 核心原则

## Knowledge Gaps
- **398 isolated node(s):** `DimSide`, `DiameterJigResult`, `TestExecutionRecord`, `AutoFixDimOutputScope`, `DiagnosticBounds` (+393 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `OutlineFeature2D` connect `HoleFeature2D Module` to `.CollectHoleSourcesFromOutlineSelection`, `DimensionPlan Module`, `FeatureRecognizer2D Module`, `Assert Module`, `DrawingGeometry`, `DimensionDrawer Module`, `Segment2D`, `DimensionPlanner`, `FeatureRecognizer Module`, `CadAuto Core Geometry`, `OutlineSegment Module`, `.AdjustShortLocalDimensionTextPositions`, `DimensionPlanner`, `StackingLayerItem Module`, `.HorizontalProbeCrossesInterior`, `DimensionDrawer Module`, `DimensionLayoutRules.cs`, `SlotArcCandidate Module`, `FeatureRecognizer2D`, `HoleCalloutPlanner`, `FeatureRecognizer`, `.AppendDimensionDiagnostics`, `.PointsEqual`, `DimensionExtensionLineRenderer`, `.ResolveDimStyle`, `HoleFeature2D`, `DimensionRuleConfig`, `旋转不变量（四向标注一致）v3`, `.ToDeduplicationItem`, `DimensionPlanner.cs`, `.CreateRectangleAt`, `.ToDeduplicationItem`, `HoleKind2D`?**
  _High betweenness centrality (0.188) - this node is a cross-community bridge._
- **Why does `DimensionDrawer` connect `DimensionDrawer Module` to `Point2D Module`, `OutlineFeature2D Module`, `HoleFeature2D Module`, `FeatureRecognizer2D`, `Commands Module`, `.AssertLProfileRuleEndpointsOnBoundary`, `AnnotationCaseStore`, `OutlineSegment Module`, `CadAuto Core Geometry`, `HoleCalloutPlan Module`, `DimensionExtensionLineRenderer`, `Point2d`, `DimensionPlanner.BoundaryInformation.cs`, `DimensionDiagnosticReport`, `.AddDimensionDebugLabel`?**
  _High betweenness centrality (0.093) - this node is a cross-community bridge._
- **Why does `Point2D` connect `DimensionExtensionLineRenderer` to `OutlineFeature2D Module`, `HoleFeature2D Module`, `DimensionPlan Module`, `FeatureRecognizer2D Module`, `Assert Module`, `DrawingGeometry`, `Segment2D`, `DimensionPlanner`, `CadAuto Core Geometry`, `OutlineSegment Module`, `.AdjustShortLocalDimensionTextPositions`, `HoleCalloutPlan Module`, `DimensionPlanner`, `StackingLayerItem Module`, `.VerticalExtensionOverlapsOutlineSegment`, `.HorizontalProbeCrossesInterior`, `DimensionDrawer Module`, `DimensionLayoutRules.cs`, `HoleCalloutPlanner`, `FeatureRecognizer`, `.ResolveDimStyle`, `.PointsEqual`, `DimensionExtensionLineRenderer`, `HoleFeature2D`, `.ResolveDimStyle`, `HoleFeature2D`, `DimensionRuleConfig`, `旋转不变量（四向标注一致）v3`, `.ToDeduplicationItem`, `DimensionPlanner.cs`?**
  _High betweenness centrality (0.081) - this node is a cross-community bridge._
- **What connects `DimSide`, `DiameterJigResult`, `TestExecutionRecord` to the rest of the system?**
  _398 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Point2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.14130434782608695 - nodes in this community are weakly interconnected._
- **Should `OutlineFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.060939060939060936 - nodes in this community are weakly interconnected._
- **Should `HoleFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.12273524254821741 - nodes in this community are weakly interconnected._