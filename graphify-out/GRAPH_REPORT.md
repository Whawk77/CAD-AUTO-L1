# Graph Report - autocad-dim  (2026-07-24)

## Corpus Check
- 107 files · ~98,521 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1615 nodes · 5938 edges · 60 communities (52 shown, 8 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 382 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `df456269`
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
- StructureSuppressionRules Module
- DimensionDrawer Module
- FeatureRecognizer Module
- OutlineFeature Module
- FeatureRecognizer Module
- CadAuto Core Geometry
- OutlineSegment Module
- HoleFeature Module
- HoleCalloutPlan Module
- .RecognizeOutlineSlotFeatures
- DimensionDiagnosticReport
- FeatureRecognizer2D.cs
- StackingLayerItem Module
- .VerticalExtensionOverlapsOutlineSegment
- Commands Module
- NativeDiameterDimensioner Module
- .HorizontalProbeCrossesInterior
- DimensionDrawer Module
- DatumDefinition Module
- DimensionLayoutRules.cs
- DimensionPlanner
- .VerticalExtensionOverlapsOutlineSegment
- DimensionDrawer
- SlotArcCandidate Module
- FeatureRecognizer2D
- .CreateDiagnosticRunContext
- CadAuto Core Planning
- DimensionDrawer Module
- FeatureRecognizer
- .IsVertical
- .AppendDimensionDiagnostics
- .RecognizeOutlineSlotFeatures
- DimSide Module
- .DrawLinearDimensions
- AUTOFIXDIM Agent Rules
- PluginEntry
- DimensionPlanner.cs
- DebugAnnotationRenderer Module
- run cad test ps1
- Test Has Property

## God Nodes (most connected - your core abstractions)
1. `OutlineFeature2D` - 273 edges
2. `DimensionPlanner` - 271 edges
3. `Point2D` - 237 edges
4. `Program` - 139 edges
5. `Segment2D` - 121 edges
6. `DimensionDrawer` - 113 edges
7. `DimensionLayoutRules` - 94 edges
8. `DimensionPlan` - 93 edges
9. `DimensionSide` - 83 edges
10. `PlannedDimension` - 83 edges

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

## Communities (60 total, 8 thin omitted)

### Community 0 - "Point2D Module"
Cohesion: 0.08
Nodes (11): HoleFeature2D, FunctionalHoleGroupPlan, LooseHoleLocationPlan, LooseHoleMacroGroup, List, PinGroupPlan, List, FunctionalHoleGroupPlan (+3 more)

### Community 1 - "OutlineFeature2D Module"
Cohesion: 0.08
Nodes (7): Point2D, DimensionPlanner, IEnumerable, int, IEquatable, IgnoredPoint, StructurePoint

### Community 2 - "HoleFeature2D Module"
Cohesion: 0.18
Nodes (9): ChamferFeature, Point2d, OutlineFeature, List, Point2d, OutlineSegment, ObjectId, Point2d (+1 more)

### Community 3 - "GeometryCollector Module"
Cohesion: 0.16
Nodes (5): CommandMethod, Commands, Editor, IList, string

### Community 4 - "DimensionPlan Module"
Cohesion: 0.14
Nodes (10): BlockTableRecord, Database, Entity, Line, ObjectId, Point3d, Polyline, Polyline2d (+2 more)

### Community 5 - "FeatureRecognizer2D Module"
Cohesion: 0.10
Nodes (15): AutoCadGeometryConverter, Arc, Circle, Line, Point2d, Point3d, Polyline, Arc2D (+7 more)

### Community 6 - "OutlineSegment"
Cohesion: 0.14
Nodes (5): Circle2D, DrawingGeometry, List, CadAuto.Core.Geometry, CadAuto.Core.Model

### Community 7 - "Assert Module"
Cohesion: 0.05
Nodes (5): Action, Program, HashSet, int, List

### Community 8 - "Commands Module"
Cohesion: 0.23
Nodes (9): AnnotationMetadata, Database, Entity, IEnumerable, List, string, Transaction, DateTime (+1 more)

### Community 11 - "StructureSuppressionRules Module"
Cohesion: 0.10
Nodes (3): Segment2D, ChamferFeature2D, StructureSuppressionRules

### Community 12 - "DimensionDrawer Module"
Cohesion: 0.12
Nodes (10): CadEntityWriter, BlockTableRecord, Color, Database, double, ObjectId, string, Transaction (+2 more)

### Community 13 - "FeatureRecognizer Module"
Cohesion: 0.10
Nodes (6): DimensionOrientation, DimensionPlan, Dictionary, int, List, Tuple

### Community 14 - "OutlineFeature Module"
Cohesion: 0.11
Nodes (18): 10. 一句话总结, 1. 问题陈述, 2. 现状代码（缺陷点）, 3. 目标行为, 4.1 共删谓词（新）, 4.2 伪代码, 4.3 不改动的部分, 4.4 注释同步 (+10 more)

### Community 15 - "FeatureRecognizer Module"
Cohesion: 0.12
Nodes (13): CornerCalloutJigResult, CornerCalloutRenderer, CornerFeatureLeaderJig, Database, double, Editor, JigPrompts, ObjectId (+5 more)

### Community 16 - "CadAuto Core Geometry"
Cohesion: 0.14
Nodes (4): HoleCalloutCluster, List, ExtensionLineBreakRange, CadAuto.Core.Rules

### Community 19 - "HoleFeature Module"
Cohesion: 0.11
Nodes (5): Datum2D, SlotFeature2D, DimensionKind, DimensionReadingLevel, Func

### Community 20 - "HoleCalloutPlan Module"
Cohesion: 0.12
Nodes (10): DimensionDeduplicationItem, DimensionDeduplicationRules, PartitionCandidate, double, HashSet, IList, int, List (+2 more)

### Community 22 - ".RecognizeOutlineSlotFeatures"
Cohesion: 0.36
Nodes (8): DeferredDim, DimSide, PlacedDim, TextBounds, bool, double, int, string

### Community 23 - "DimensionDiagnosticReport"
Cohesion: 0.25
Nodes (5): DimensionCandidateDiagnostic, DimensionDiagnosticReport, IEnumerable, List, FeatureDiagnosticCounts

### Community 24 - "FeatureRecognizer2D.cs"
Cohesion: 0.33
Nodes (4): SlotArcCandidate, SlotLineCandidate, CadAuto.Core.Recognition, CadAuto.Core.Tests

### Community 25 - "StackingLayerItem Module"
Cohesion: 0.07
Nodes (23): DimensionSide, DimensionLayoutItem, DimensionLayoutRules, ExtensionLineBreakCandidate, IndexedLayoutItem, LayoutBlock, StackingLayerItem, TextSlideCandidate (+15 more)

### Community 26 - ".VerticalExtensionOverlapsOutlineSegment"
Cohesion: 0.18
Nodes (9): GeneratedAnnotation, ObjectId, AutoFixDimOutputScope, DiagnosticBounds, DiagnosticFileIdentity, CadAuto.CadAdapter.Recognition, CadAuto.CadAdapter.Mapping, CadAuto.CadAdapter (+1 more)

### Community 27 - "Commands Module"
Cohesion: 0.07
Nodes (32): HoleFeature, ObjectId, Point3d, DiameterCalloutJig, NativeDiameterDimensioner, BlockTableRecord, Database, Document (+24 more)

### Community 28 - "NativeDiameterDimensioner Module"
Cohesion: 0.15
Nodes (7): FilletFeature, Point2d, Editor, IEnumerable, IList, Point2d, Point3d

### Community 29 - ".HorizontalProbeCrossesInterior"
Cohesion: 0.11
Nodes (8): DimensionType, DimensionPlanCadItem, Point3d, DimensionPlanCadMapper, IEnumerable, DiameterJigResult, CadAuto.CadAdapter.Model, CadAuto.CadAdapter.Rendering

### Community 30 - "DimensionDrawer Module"
Cohesion: 0.07
Nodes (27): GeometryCollector, OutlineEntityCandidate, Arc, Editor, Entity, IList, Line, List (+19 more)

### Community 31 - "DatumDefinition Module"
Cohesion: 0.14
Nodes (13): 1. 销对之间的孔 → 功能孔, 2. 同侧结构段对齐（方案 1）, 3. OutlineSegment 复述 overall, 4. 结构宽/高 vs overall — 规则过大已修正, 5. 文档与测试, Handoff — 尺寸规则改进（2026-07-20）, 仓库与分支, 工作约定 (+5 more)

### Community 32 - "DimensionLayoutRules.cs"
Cohesion: 0.13
Nodes (14): BlockReference, SlotArcCandidate, SlotLineCandidate, ThreadArcInfo, ThreadMinorCircleInfo, Circle, Editor, IEnumerable (+6 more)

### Community 35 - "DimensionDrawer"
Cohesion: 0.14
Nodes (7): A, B, DimensionDrawer, BlockTableRecord, List, Transaction, DeferredDim

### Community 36 - "SlotArcCandidate Module"
Cohesion: 0.16
Nodes (3): Point3d, PlacedDim, TextBounds

### Community 37 - "FeatureRecognizer2D"
Cohesion: 0.19
Nodes (5): ISet, IList, ISet, SlotArcCandidate, SlotLineCandidate

### Community 38 - ".CreateDiagnosticRunContext"
Cohesion: 0.20
Nodes (5): DiagnosticRunContext, List, DiagnosticBounds, DiagnosticFileIdentity, SortedDictionary

### Community 39 - "CadAuto Core Planning"
Cohesion: 0.11
Nodes (18): A. Overall partition 误删 / 漏删, B. 外框碎段 OutlineSegment + 结构双开, C. StructureOverallPartition 过宽（修“其它图崩了”）, D. U 槽链对齐, E. Phase 2 — `default(Point2D)==(0,0)` 哨兵, F. Phase 3 — Complementary Snap 收紧, graphify, HANDOFF 2026-07-21 — Overall partition / envelope / slot chain / Point2D snap / complementary snap (+10 more)

### Community 40 - "DimensionDrawer Module"
Cohesion: 0.16
Nodes (5): FilletFeature2D, OutlineFeature2D, List, IList, IEnumerable

### Community 43 - "FeatureRecognizer"
Cohesion: 0.31
Nodes (3): Entity, Polyline, Polyline2d

### Community 45 - ".AppendDimensionDiagnostics"
Cohesion: 0.39
Nodes (3): DiagnosticRunContext, KeyValuePair, StringBuilder

### Community 46 - ".RecognizeOutlineSlotFeatures"
Cohesion: 0.15
Nodes (8): OutlineArc, ObjectId, Point2d, FeatureRecognizer, Arc, bool, double, Vector2d

### Community 49 - ".DrawLinearDimensions"
Cohesion: 0.11
Nodes (13): AutoFixDimOutputScope, CadToCoreModelMapper, IEnumerable, List, DatumDefinition, HoleKind, SlotFeature, ObjectId (+5 more)

### Community 50 - "AUTOFIXDIM Agent Rules"
Cohesion: 0.06
Nodes (42): AUTOFIXDIM Agent Rules, Annotation Product Invariants, Explicit Validation Authorization, Graphify-First Codebase Navigation, Safe File Operations, Deployment and Manual Verification, Four-Direction Automated Regression, Manual AutoCAD Verification Flow (+34 more)

### Community 52 - "PluginEntry"
Cohesion: 0.29
Nodes (3): AutoFixtureDim, IExtensionApplication, PluginEntry

### Community 55 - "DimensionPlanner.cs"
Cohesion: 0.15
Nodes (4): IgnoredPoint, LooseHoleLineGroup, StructurePoint, CadAuto.Core.Planning

### Community 73 - "DebugAnnotationRenderer Module"
Cohesion: 0.11
Nodes (14): Entity, DebugAnnotationRenderer, Color, Database, ObjectId, Point2d, Point3d, string (+6 more)

### Community 105 - "Test Has Property"
Cohesion: 0.83
Nodes (3): Get-PhysicalRank(), Test-DimensionSelector(), Test-HasProperty()

## Knowledge Gaps
- **63 isolated node(s):** `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests`, `ExtensionLineBreakCandidate`, `AutoFixDimOutputScope` (+58 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **8 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `DimensionRuleConfig` connect `Commands Module` to `DimensionLayoutRules.cs`, `OutlineFeature2D Module`, `DimensionDrawer`, `GeometryCollector Module`, `FeatureRecognizer2D Module`, `Assert Module`, `Segment2D`, `DimensionDrawer Module`, `StructureSuppressionRules Module`, `DimensionDrawer Module`, `.RecognizeOutlineSlotFeatures`, `FeatureRecognizer Module`, `CadAuto Core Geometry`, `.DrawLinearDimensions`, `HoleCalloutPlan Module`, `StackingLayerItem Module`, `DimensionDrawer Module`?**
  _High betweenness centrality (0.226) - this node is a cross-community bridge._
- **Why does `DimensionPlanner` connect `OutlineFeature2D Module` to `Point2D Module`, `DimensionPlanner`, `.VerticalExtensionOverlapsOutlineSegment`, `FeatureRecognizer2D Module`, `DimensionDrawer Module`, `StructureSuppressionRules Module`, `FeatureRecognizer Module`, `HoleFeature Module`, `HoleCalloutPlan Module`, `DimensionPlanner.cs`, `Commands Module`?**
  _High betweenness centrality (0.136) - this node is a cross-community bridge._
- **Why does `DimensionDrawer` connect `DimensionDrawer` to `SlotArcCandidate Module`, `DebugAnnotationRenderer Module`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `DimSide Module`, `.DrawLinearDimensions`, `OutlineSegment Module`, `HoleCalloutPlan Module`, `.RecognizeOutlineSlotFeatures`, `DimensionDiagnosticReport`, `StackingLayerItem Module`, `Commands Module`, `NativeDiameterDimensioner Module`, `.HorizontalProbeCrossesInterior`?**
  _High betweenness centrality (0.125) - this node is a cross-community bridge._
- **What connects `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests` to the rest of the system?**
  _63 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Point2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.07668231611893583 - nodes in this community are weakly interconnected._
- **Should `OutlineFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.07787246547164267 - nodes in this community are weakly interconnected._
- **Should `DimensionPlan Module` be split into smaller, more focused modules?**
  _Cohesion score 0.14482758620689656 - nodes in this community are weakly interconnected._