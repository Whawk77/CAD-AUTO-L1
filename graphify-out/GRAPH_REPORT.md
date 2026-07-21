# Graph Report - L1-grok  (2026-07-21)

## Corpus Check
- 111 files · ~84,506 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1511 nodes · 5510 edges · 62 communities (52 shown, 10 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 339 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `cab29799`
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
- AnnotationMetadata Module
- DimSide Module
- FeatureRecognizer2D.cs
- AUTOFIXDIM Agent Rules
- P5.cs
- When invoked
- HoleCalloutPlanner.cs
- DebugAnnotationRenderer Module
- run cad test ps1
- CadAuto Core csproj
- Test Has Property

## God Nodes (most connected - your core abstractions)
1. `OutlineFeature2D` - 249 edges
2. `DimensionPlanner` - 245 edges
3. `Point2D` - 227 edges
4. `Segment2D` - 114 edges
5. `DimensionDrawer` - 113 edges
6. `Program` - 111 edges
7. `DimensionLayoutRules` - 94 edges
8. `DimensionPlan` - 84 edges
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

## Communities (62 total, 10 thin omitted)

### Community 0 - "Point2D Module"
Cohesion: 0.09
Nodes (13): HoleFeature2D, FunctionalHoleGroupPlan, LooseHoleLineGroup, LooseHoleLocationPlan, LooseHoleMacroGroup, List, Tuple, PinGroupPlan (+5 more)

### Community 2 - "HoleFeature2D Module"
Cohesion: 0.12
Nodes (7): DimensionKind, DimensionOrientation, DimensionPlan, Dictionary, int, List, DimensionReadingLevel

### Community 3 - "GeometryCollector Module"
Cohesion: 0.13
Nodes (9): AutoFixDimOutputScope, DiagnosticDimensionSide, CommandMethod, Commands, Document, Editor, IEnumerable, IList (+1 more)

### Community 4 - "DimensionPlan Module"
Cohesion: 0.42
Nodes (4): BlockTableRecord, Database, ObjectId, Transaction

### Community 5 - "FeatureRecognizer2D Module"
Cohesion: 0.10
Nodes (15): AutoCadGeometryConverter, Arc, Circle, Line, Point2d, Point3d, Polyline, Arc2D (+7 more)

### Community 6 - "DimensionDrawer Module"
Cohesion: 0.16
Nodes (3): Segment2D, FeatureRecognizer2D, IEnumerable

### Community 7 - "Assert Module"
Cohesion: 0.06
Nodes (3): Action, Program, int

### Community 8 - "Commands Module"
Cohesion: 0.17
Nodes (8): OutlineFeature, List, Point2d, OutlineSegment, ObjectId, Point2d, Point2d, Vector2d

### Community 9 - "DimensionDrawer Module"
Cohesion: 0.14
Nodes (5): DimensionDrawer, BlockTableRecord, List, Transaction, RotatedDimension

### Community 11 - "StructureSuppressionRules Module"
Cohesion: 0.08
Nodes (4): ChamferFeature2D, FilletFeature2D, StructureSuppressionRules, IEnumerable

### Community 12 - "DimensionDrawer Module"
Cohesion: 0.13
Nodes (10): CadEntityWriter, BlockTableRecord, Color, Database, double, ObjectId, string, Transaction (+2 more)

### Community 13 - "FeatureRecognizer Module"
Cohesion: 0.17
Nodes (3): IList, PlannedDimension, IList

### Community 15 - "FeatureRecognizer Module"
Cohesion: 0.10
Nodes (15): CornerCalloutJigResult, CornerCalloutRenderer, CornerFeatureLeaderJig, Database, double, Editor, JigPrompts, ObjectId (+7 more)

### Community 16 - "CadAuto Core Geometry"
Cohesion: 0.11
Nodes (7): Circle2D, DrawingGeometry, List, CadAuto.Core.Geometry, CadAuto.Core.Model, P, P

### Community 18 - "CadAuto CadAdapter Model"
Cohesion: 0.22
Nodes (3): ChamferFeature, Point2d, CadAuto.CadAdapter.Model

### Community 20 - "HoleCalloutPlan Module"
Cohesion: 0.12
Nodes (10): DimensionDeduplicationItem, DimensionDeduplicationRules, PartitionCandidate, double, IList, int, List, Tuple (+2 more)

### Community 21 - "DimensionLayoutRules Module"
Cohesion: 0.11
Nodes (18): BlockReference, SlotFeature, ObjectId, Point2d, SlotArcCandidate, SlotLineCandidate, ThreadArcInfo, ThreadMinorCircleInfo (+10 more)

### Community 22 - "StructureEndpointRules Module"
Cohesion: 0.13
Nodes (5): OutlineFeature2D, List, StructureEndpointRules, IEnumerable, StructureEndpointSpan

### Community 23 - "DimensionSide Module"
Cohesion: 0.13
Nodes (6): DimensionCandidateDiagnostic, DimensionDiagnosticReport, IEnumerable, List, FeatureDiagnosticCounts, CadAuto.Core.Planning

### Community 24 - "FeatureRecognizer2D Module"
Cohesion: 0.14
Nodes (3): ExtensionLineBreakRange, CadAuto.Core.Rules, P

### Community 25 - "StackingLayerItem Module"
Cohesion: 0.08
Nodes (19): DimensionSide, DimensionLayoutItem, DimensionLayoutRules, LayoutBlock, IEnumerable, IList, List, Tuple (+11 more)

### Community 26 - "NativeDiameterDimensioner Module"
Cohesion: 0.16
Nodes (6): Entity, Line, Point3d, Polyline, Polyline2d, Polyline3d

### Community 27 - "Commands Module"
Cohesion: 0.07
Nodes (32): HoleFeature, ObjectId, Point3d, DiameterCalloutJig, NativeDiameterDimensioner, BlockTableRecord, Database, Document (+24 more)

### Community 28 - "NativeDiameterDimensioner Module"
Cohesion: 0.23
Nodes (4): FilletFeature, Point2d, Point2d, Point3d

### Community 29 - "DimensionDrawer Module"
Cohesion: 0.14
Nodes (4): Datum2D, SlotFeature2D, IEnumerable, Func

### Community 30 - "DimensionDrawer Module"
Cohesion: 0.07
Nodes (23): GeometryCollector, OutlineEntityCandidate, Arc, Editor, Entity, IList, Line, List (+15 more)

### Community 31 - "DatumDefinition Module"
Cohesion: 0.14
Nodes (13): 1. 销对之间的孔 → 功能孔, 2. 同侧结构段对齐（方案 1）, 3. OutlineSegment 复述 overall, 4. 结构宽/高 vs overall — 规则过大已修正, 5. 文档与测试, Handoff — 尺寸规则改进（2026-07-20）, 仓库与分支, 工作约定 (+5 more)

### Community 32 - "DimensionRuleConfig Module"
Cohesion: 0.11
Nodes (10): GeneratedAnnotation, ObjectId, AutoFixDimOutputScope, CadAuto.CadAdapter.Recognition, CadAuto.CadAdapter.Mapping, CadAuto.CadAdapter, AutoFixtureDim, CadAuto.CadAdapter.Collection (+2 more)

### Community 33 - "SlotFeature Module"
Cohesion: 0.21
Nodes (4): IList, ISet, SlotArcCandidate, SlotLineCandidate

### Community 34 - "DimensionDrawer Module"
Cohesion: 0.31
Nodes (3): Entity, Polyline, Polyline2d

### Community 35 - "Point3d"
Cohesion: 0.36
Nodes (8): DeferredDim, DimSide, PlacedDim, TextBounds, bool, double, int, string

### Community 36 - "SlotArcCandidate Module"
Cohesion: 0.14
Nodes (3): Point3d, PlacedDim, TextBounds

### Community 37 - "Flush Side"
Cohesion: 0.09
Nodes (4): Point2D, IgnoredPoint, StructurePoint, IEquatable

### Community 39 - "CadAuto Core Planning"
Cohesion: 0.40
Nodes (4): ExtensionLineBreakCandidate, IndexedLayoutItem, StackingLayerItem, TextSlideCandidate

### Community 40 - "DimensionDrawer Module"
Cohesion: 0.16
Nodes (7): DimensionType, DimensionPlanCadItem, Point3d, DimensionPlanCadMapper, IEnumerable, DiameterJigResult, CadAuto.CadAdapter.Rendering

### Community 41 - ".RecognizeSlotFeatures"
Cohesion: 0.17
Nodes (7): OutlineArc, ObjectId, Point2d, FeatureRecognizer, Arc, bool, double

### Community 42 - ".ResolveAnnotationLayer"
Cohesion: 0.16
Nodes (10): DimStyleManager, Database, ObjectId, string, Transaction, LayerManager, Database, string (+2 more)

### Community 45 - "AnnotationMetadata Module"
Cohesion: 0.23
Nodes (9): AnnotationMetadata, Database, Entity, IEnumerable, List, string, Transaction, DateTime (+1 more)

### Community 47 - "DimSide Module"
Cohesion: 0.23
Nodes (3): IList, DeferredDim, DimSide

### Community 49 - "FeatureRecognizer2D.cs"
Cohesion: 0.33
Nodes (4): SlotArcCandidate, SlotLineCandidate, CadAuto.Core.Recognition, CadAuto.Core.Tests

### Community 50 - "AUTOFIXDIM Agent Rules"
Cohesion: 0.06
Nodes (42): AUTOFIXDIM Agent Rules, Annotation Product Invariants, Explicit Validation Authorization, Graphify-First Codebase Navigation, Safe File Operations, Deployment and Manual Verification, Four-Direction Automated Regression, Manual AutoCAD Verification Flow (+34 more)

### Community 52 - "When invoked"
Cohesion: 0.22
Nodes (8): Explain, Graphify, Help, Path, Query, Rules, Update code graph, When invoked

### Community 73 - "DebugAnnotationRenderer Module"
Cohesion: 0.11
Nodes (14): Entity, DebugAnnotationRenderer, Color, Database, ObjectId, Point2d, Point3d, string (+6 more)

### Community 105 - "Test Has Property"
Cohesion: 0.83
Nodes (3): Get-PhysicalRank(), Test-DimensionSelector(), Test-HasProperty()

## Knowledge Gaps
- **38 isolated node(s):** `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests`, `ExtensionLineBreakCandidate`, `AutoFixDimOutputScope` (+33 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **10 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `DimensionRuleConfig` connect `Commands Module` to `GeometryCollector Module`, `FeatureRecognizer2D Module`, `DimensionDrawer Module`, `Assert Module`, `.RecognizeSlotFeatures`, `DimensionDrawer Module`, `StructureSuppressionRules Module`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `OutlineSegment Module`, `HoleCalloutPlan Module`, `DimensionLayoutRules Module`, `StructureEndpointRules Module`, `FeatureRecognizer2D Module`, `StackingLayerItem Module`, `DimensionDrawer Module`?**
  _High betweenness centrality (0.225) - this node is a cross-community bridge._
- **Why does `DimensionPlanner` connect `OutlineSegment Module` to `Point2D Module`, `OutlineFeature2D Module`, `HoleFeature2D Module`, `Flush Side`, `DimensionDrawer Module`, `FeatureRecognizer2D Module`, `DimensionDrawer Module`, `StructureSuppressionRules Module`, `FeatureRecognizer Module`, `HoleCalloutPlan Module`, `StructureEndpointRules Module`, `Commands Module`, `DimensionDrawer Module`?**
  _High betweenness centrality (0.140) - this node is a cross-community bridge._
- **Why does `DimensionDrawer` connect `DimensionDrawer Module` to `GeometryCollector Module`, `Point3d`, `SlotArcCandidate Module`, `DimensionDrawer Module`, `DebugAnnotationRenderer Module`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `DimSide Module`, `HoleFeature Module`, `HoleCalloutPlan Module`, `DimensionSide Module`, `StackingLayerItem Module`, `Commands Module`, `NativeDiameterDimensioner Module`?**
  _High betweenness centrality (0.132) - this node is a cross-community bridge._
- **What connects `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests` to the rest of the system?**
  _38 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Point2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.088841882601798 - nodes in this community are weakly interconnected._
- **Should `HoleFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.11707317073170732 - nodes in this community are weakly interconnected._
- **Should `GeometryCollector Module` be split into smaller, more focused modules?**
  _Cohesion score 0.13277310924369748 - nodes in this community are weakly interconnected._