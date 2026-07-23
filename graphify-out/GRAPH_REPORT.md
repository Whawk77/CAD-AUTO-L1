# Graph Report - 2026-07-22-6315c23d  (2026-07-22)

## Corpus Check
- 113 files · ~90,427 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1574 nodes · 5686 edges · 67 communities (56 shown, 11 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 361 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `d004442c`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Point2D Module
- OutlineFeature2D Module
- HoleFeature2D Module
- GeometryCollector Module
- DimensionPlan Module
- FeatureRecognizer2D Module
- DimensionDrawer Module
- Assert Module
- Commands Module
- DimensionDrawer Module
- DimensionDrawer Module
- StructureSuppressionRules Module
- DimensionDrawer Module
- FeatureRecognizer Module
- OutlineFeature Module
- FeatureRecognizer Module
- CadAuto Core Geometry
- OutlineSegment Module
- CadAuto CadAdapter Model
- HoleFeature Module
- HoleCalloutPlan Module
- DimensionLayoutRules Module
- StructureEndpointRules Module
- DimensionSide Module
- FeatureRecognizer2D Module
- StackingLayerItem Module
- NativeDiameterDimensioner Module
- Commands Module
- NativeDiameterDimensioner Module
- DimensionDrawer Module
- DimensionDrawer Module
- DatumDefinition Module
- DimensionRuleConfig Module
- SlotFeature Module
- DimensionDrawer Module
- Point3d
- SlotArcCandidate Module
- Flush Side
- DimensionDrawer Module
- CadAuto Core Planning
- DimensionDrawer Module
- .RecognizeSlotFeatures
- .ResolveAnnotationLayer
- DimensionExtensionLineRenderer
- DimensionPlanner.cs
- AnnotationMetadata Module
- DatumDefinition
- DimSide Module
- DimensionExtensionLineRenderer
- FeatureRecognizer2D.cs
- AUTOFIXDIM Agent Rules
- P5.cs
- When invoked
- ChamferFeature
- P4.cs
- DimensionPlanCadItem
- DebugAnnotationRenderer Module
- run cad test ps1
- CadAuto Core csproj
- Test Has Property

## God Nodes (most connected - your core abstractions)
1. `OutlineFeature2D` - 253 edges
2. `DimensionPlanner` - 252 edges
3. `Point2D` - 231 edges
4. `Program` - 124 edges
5. `Segment2D` - 115 edges
6. `DimensionDrawer` - 113 edges
7. `DimensionLayoutRules` - 94 edges
8. `DimensionPlan` - 87 edges
9. `FeatureRecognizer` - 82 edges
10. `DimensionSide` - 82 edges

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

## Communities (67 total, 11 thin omitted)

### Community 0 - "Point2D Module"
Cohesion: 0.10
Nodes (11): HoleFeature2D, FunctionalHoleGroupPlan, LooseHoleLineGroup, LooseHoleLocationPlan, LooseHoleMacroGroup, PinGroupPlan, List, FunctionalHoleGroupPlan (+3 more)

### Community 1 - "OutlineFeature2D Module"
Cohesion: 0.11
Nodes (3): IList, List, PlannedDimension

### Community 3 - "GeometryCollector Module"
Cohesion: 0.17
Nodes (8): AutoFixDimOutputScope, DiagnosticDimensionSide, Commands, Editor, IEnumerable, IList, string, StringBuilder

### Community 4 - "DimensionPlan Module"
Cohesion: 0.11
Nodes (3): OutlineFeature2D, List, IList

### Community 5 - "FeatureRecognizer2D Module"
Cohesion: 0.10
Nodes (15): AutoCadGeometryConverter, Arc, Circle, Line, Point2d, Point3d, Polyline, Arc2D (+7 more)

### Community 7 - "Assert Module"
Cohesion: 0.05
Nodes (3): Action, Program, int

### Community 8 - "Commands Module"
Cohesion: 0.18
Nodes (8): OutlineSegment, ObjectId, Point2d, FeatureRecognizer, bool, double, Point2d, Vector2d

### Community 9 - "DimensionDrawer Module"
Cohesion: 0.14
Nodes (7): A, B, DimensionDrawer, BlockTableRecord, List, Transaction, DeferredDim

### Community 10 - "DimensionDrawer Module"
Cohesion: 0.11
Nodes (7): DimensionCandidateDiagnostic, DimensionOrientation, DimensionPlan, Dictionary, int, List, Tuple

### Community 11 - "StructureSuppressionRules Module"
Cohesion: 0.09
Nodes (4): ChamferFeature2D, FilletFeature2D, StructureSuppressionRules, IEnumerable

### Community 12 - "DimensionDrawer Module"
Cohesion: 0.13
Nodes (10): CadEntityWriter, BlockTableRecord, Color, Database, double, ObjectId, string, Transaction (+2 more)

### Community 13 - "FeatureRecognizer Module"
Cohesion: 0.17
Nodes (3): Datum2D, DimensionKind, DimensionReadingLevel

### Community 14 - "OutlineFeature Module"
Cohesion: 0.11
Nodes (18): 10. 一句话总结, 1. 问题陈述, 2. 现状代码（缺陷点）, 3. 目标行为, 4.1 共删谓词（新）, 4.2 伪代码, 4.3 不改动的部分, 4.4 注释同步 (+10 more)

### Community 15 - "FeatureRecognizer Module"
Cohesion: 0.12
Nodes (13): CornerCalloutJigResult, CornerCalloutRenderer, CornerFeatureLeaderJig, Database, double, Editor, JigPrompts, ObjectId (+5 more)

### Community 16 - "CadAuto Core Geometry"
Cohesion: 0.15
Nodes (4): CadAuto.Core.Geometry, CadAuto.Core.Model, P, P

### Community 17 - "OutlineSegment Module"
Cohesion: 0.17
Nodes (9): DeferredDim, PlacedDim, TextBounds, bool, double, int, Point3d, string (+1 more)

### Community 18 - "CadAuto CadAdapter Model"
Cohesion: 0.40
Nodes (3): GeneratedAnnotation, ObjectId, CadAuto.CadAdapter

### Community 19 - "HoleFeature Module"
Cohesion: 0.16
Nodes (10): DimStyleManager, Database, ObjectId, string, Transaction, LayerManager, Database, string (+2 more)

### Community 20 - "HoleCalloutPlan Module"
Cohesion: 0.11
Nodes (11): DimensionDeduplicationItem, DimensionDeduplicationRules, PartitionCandidate, double, IList, int, List, Tuple (+3 more)

### Community 21 - "DimensionLayoutRules Module"
Cohesion: 0.14
Nodes (12): BlockReference, SlotFeature, ObjectId, Point2d, Circle, Editor, IEnumerable, IList (+4 more)

### Community 22 - "StructureEndpointRules Module"
Cohesion: 0.14
Nodes (5): Point2D, StructureEndpointRules, IEnumerable, StructureEndpointSpan, IEquatable

### Community 23 - "DimensionSide Module"
Cohesion: 0.29
Nodes (4): DimensionDiagnosticReport, IEnumerable, List, FeatureDiagnosticCounts

### Community 24 - "FeatureRecognizer2D Module"
Cohesion: 0.11
Nodes (5): ExtensionLineBreakRange, CadAuto.Core.Rules, CadAuto.Core.Planning, P, P

### Community 25 - "StackingLayerItem Module"
Cohesion: 0.07
Nodes (23): DimensionSide, DimensionLayoutItem, DimensionLayoutRules, ExtensionLineBreakCandidate, IndexedLayoutItem, LayoutBlock, StackingLayerItem, TextSlideCandidate (+15 more)

### Community 26 - "NativeDiameterDimensioner Module"
Cohesion: 0.16
Nodes (6): Entity, Line, Point3d, Polyline, Polyline2d, Polyline3d

### Community 27 - "Commands Module"
Cohesion: 0.06
Nodes (34): HoleFeature, ObjectId, Point3d, DiameterCalloutJig, NativeDiameterDimensioner, BlockTableRecord, Database, Document (+26 more)

### Community 28 - "NativeDiameterDimensioner Module"
Cohesion: 0.15
Nodes (6): FilletFeature, Point2d, Editor, IEnumerable, Point2d, Document

### Community 29 - "DimensionDrawer Module"
Cohesion: 0.20
Nodes (3): SlotFeature2D, IEnumerable, Func

### Community 30 - "DimensionDrawer Module"
Cohesion: 0.11
Nodes (18): GeometryCollector, OutlineEntityCandidate, Arc, Editor, Entity, IList, Line, List (+10 more)

### Community 31 - "DatumDefinition Module"
Cohesion: 0.14
Nodes (13): 1. 销对之间的孔 → 功能孔, 2. 同侧结构段对齐（方案 1）, 3. OutlineSegment 复述 overall, 4. 结构宽/高 vs overall — 规则过大已修正, 5. 文档与测试, Handoff — 尺寸规则改进（2026-07-20）, 仓库与分支, 工作约定 (+5 more)

### Community 32 - "DimensionRuleConfig Module"
Cohesion: 0.29
Nodes (3): AutoFixtureDim, IExtensionApplication, PluginEntry

### Community 33 - "SlotFeature Module"
Cohesion: 0.17
Nodes (5): ISet, IList, ISet, SlotArcCandidate, SlotLineCandidate

### Community 34 - "DimensionDrawer Module"
Cohesion: 0.31
Nodes (3): Entity, Polyline, Polyline2d

### Community 35 - "Point3d"
Cohesion: 0.42
Nodes (4): BlockTableRecord, Database, ObjectId, Transaction

### Community 38 - "DimensionDrawer Module"
Cohesion: 0.17
Nodes (5): CadToCoreModelMapper, IEnumerable, List, HoleKind, HoleKind2D

### Community 39 - "CadAuto Core Planning"
Cohesion: 0.11
Nodes (18): A. Overall partition 误删 / 漏删, B. 外框碎段 OutlineSegment + 结构双开, C. StructureOverallPartition 过宽（修“其它图崩了”）, D. U 槽链对齐, E. Phase 2 — `default(Point2D)==(0,0)` 哨兵, F. Phase 3 — Complementary Snap 收紧, graphify, HANDOFF 2026-07-21 — Overall partition / envelope / slot chain / Point2D snap / complementary snap (+10 more)

### Community 40 - "DimensionDrawer Module"
Cohesion: 0.18
Nodes (4): DimSide, AutoFixDimOutputScope, CadAuto.CadAdapter.Mapping, CadAuto.CadAdapter.Model

### Community 41 - ".RecognizeSlotFeatures"
Cohesion: 0.20
Nodes (4): OutlineArc, ObjectId, Point2d, Arc

### Community 44 - "DimensionPlanner.cs"
Cohesion: 0.15
Nodes (4): Circle2D, Segment2D, DrawingGeometry, List

### Community 45 - "AnnotationMetadata Module"
Cohesion: 0.23
Nodes (9): AnnotationMetadata, Database, Entity, IEnumerable, List, string, Transaction, DateTime (+1 more)

### Community 47 - "DimSide Module"
Cohesion: 0.21
Nodes (5): OutlineFeature, List, Point2d, DimSide, RotatedDimension

### Community 48 - "DimensionExtensionLineRenderer"
Cohesion: 0.29
Nodes (5): DimensionExtensionLineRenderer, Database, IList, string, Tuple

### Community 49 - "FeatureRecognizer2D.cs"
Cohesion: 0.33
Nodes (4): SlotArcCandidate, SlotLineCandidate, CadAuto.Core.Recognition, CadAuto.Core.Tests

### Community 50 - "AUTOFIXDIM Agent Rules"
Cohesion: 0.06
Nodes (42): AUTOFIXDIM Agent Rules, Annotation Product Invariants, Explicit Validation Authorization, Graphify-First Codebase Navigation, Safe File Operations, Deployment and Manual Verification, Four-Direction Automated Regression, Manual AutoCAD Verification Flow (+34 more)

### Community 51 - "P5.cs"
Cohesion: 0.33
Nodes (6): SlotArcCandidate, SlotLineCandidate, ThreadArcInfo, ThreadMinorCircleInfo, Point3d, CadAuto.CadAdapter.Recognition

### Community 52 - "When invoked"
Cohesion: 0.22
Nodes (8): Explain, Graphify, Help, Path, Query, Rules, Update code graph, When invoked

### Community 57 - "DimensionPlanCadItem"
Cohesion: 0.16
Nodes (7): DimensionType, DimensionPlanCadItem, Point3d, DimensionPlanCadMapper, IEnumerable, DiameterJigResult, CadAuto.CadAdapter.Rendering

### Community 73 - "DebugAnnotationRenderer Module"
Cohesion: 0.15
Nodes (10): Entity, Point3d, DebugAnnotationRenderer, Color, Database, ObjectId, Point2d, Point3d (+2 more)

### Community 105 - "Test Has Property"
Cohesion: 0.83
Nodes (3): Get-PhysicalRank(), Test-DimensionSelector(), Test-HasProperty()

## Knowledge Gaps
- **69 isolated node(s):** `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests`, `ExtensionLineBreakCandidate`, `AutoFixDimOutputScope` (+64 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **11 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `DimensionRuleConfig` connect `Commands Module` to `HoleFeature2D Module`, `GeometryCollector Module`, `FeatureRecognizer2D Module`, `DimensionDrawer Module`, `Assert Module`, `Commands Module`, `DimensionDrawer Module`, `StructureSuppressionRules Module`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `HoleCalloutPlan Module`, `DimensionLayoutRules Module`, `StructureEndpointRules Module`, `StackingLayerItem Module`, `NativeDiameterDimensioner Module`, `DimensionDrawer Module`?**
  _High betweenness centrality (0.216) - this node is a cross-community bridge._
- **Why does `DimensionDrawer` connect `DimensionDrawer Module` to `GeometryCollector Module`, `SlotArcCandidate Module`, `DimensionDrawer Module`, `DebugAnnotationRenderer Module`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `DimSide Module`, `OutlineSegment Module`, `DimensionExtensionLineRenderer`, `HoleCalloutPlan Module`, `DimensionSide Module`, `DimensionPlanCadItem`, `Commands Module`, `NativeDiameterDimensioner Module`, `StackingLayerItem Module`?**
  _High betweenness centrality (0.132) - this node is a cross-community bridge._
- **Why does `DimensionPlanner` connect `HoleFeature2D Module` to `Point2D Module`, `OutlineFeature2D Module`, `DimensionPlan Module`, `Flush Side`, `FeatureRecognizer2D Module`, `Assert Module`, `DimensionDrawer Module`, `StructureSuppressionRules Module`, `DimensionPlanner.cs`, `FeatureRecognizer Module`, `HoleCalloutPlan Module`, `P4.cs`, `StructureEndpointRules Module`, `Commands Module`, `DimensionDrawer Module`?**
  _High betweenness centrality (0.126) - this node is a cross-community bridge._
- **What connects `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests` to the rest of the system?**
  _69 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Point2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.10030165912518854 - nodes in this community are weakly interconnected._
- **Should `OutlineFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.11041229909154437 - nodes in this community are weakly interconnected._
- **Should `HoleFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.09672830725462304 - nodes in this community are weakly interconnected._