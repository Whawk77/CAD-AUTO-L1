# Graph Report - L1-grok  (2026-07-21)

## Corpus Check
- 111 files · ~86,158 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1518 nodes · 5545 edges · 64 communities (53 shown, 11 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 346 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `5d65f090`
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
- P2.cs
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
1. `OutlineFeature2D` - 250 edges
2. `DimensionPlanner` - 246 edges
3. `Point2D` - 227 edges
4. `Program` - 117 edges
5. `Segment2D` - 114 edges
6. `DimensionDrawer` - 113 edges
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

## Communities (64 total, 11 thin omitted)

### Community 0 - "Point2D Module"
Cohesion: 0.09
Nodes (15): HoleFeature2D, FunctionalHoleGroupPlan, IgnoredPoint, LooseHoleLineGroup, LooseHoleLocationPlan, LooseHoleMacroGroup, StructurePoint, IEnumerable (+7 more)

### Community 2 - "HoleFeature2D Module"
Cohesion: 0.09
Nodes (8): DimensionCandidateDiagnostic, DimensionKind, DimensionOrientation, DimensionPlan, Dictionary, int, List, DimensionReadingLevel

### Community 3 - "GeometryCollector Module"
Cohesion: 0.14
Nodes (9): DiagnosticDimensionSide, CommandMethod, Commands, Document, Editor, IEnumerable, IList, string (+1 more)

### Community 4 - "DimensionPlan Module"
Cohesion: 0.22
Nodes (5): DimStyleManager, Database, ObjectId, string, Transaction

### Community 5 - "FeatureRecognizer2D Module"
Cohesion: 0.10
Nodes (15): AutoCadGeometryConverter, Arc, Circle, Line, Point2d, Point3d, Polyline, Arc2D (+7 more)

### Community 6 - "DimensionDrawer Module"
Cohesion: 0.17
Nodes (3): Segment2D, FeatureRecognizer2D, IEnumerable

### Community 7 - "Assert Module"
Cohesion: 0.06
Nodes (3): Action, Program, int

### Community 8 - "Commands Module"
Cohesion: 0.17
Nodes (8): OutlineFeature, List, Point2d, OutlineSegment, ObjectId, Point2d, Point2d, Vector2d

### Community 9 - "DimensionDrawer Module"
Cohesion: 0.18
Nodes (4): A, B, List, DeferredDim

### Community 10 - "DimensionDrawer Module"
Cohesion: 0.17
Nodes (3): DimensionSide, IList, StructureEndpointSpan

### Community 11 - "StructureSuppressionRules Module"
Cohesion: 0.10
Nodes (3): ChamferFeature2D, StructureSuppressionRules, IEnumerable

### Community 12 - "DimensionDrawer Module"
Cohesion: 0.12
Nodes (10): CadEntityWriter, BlockTableRecord, Color, Database, double, ObjectId, string, Transaction (+2 more)

### Community 14 - "OutlineFeature Module"
Cohesion: 0.20
Nodes (3): ChamferFeature, Point2d, CadAuto.CadAdapter.Model

### Community 15 - "FeatureRecognizer Module"
Cohesion: 0.12
Nodes (13): CornerCalloutJigResult, CornerCalloutRenderer, CornerFeatureLeaderJig, Database, double, Editor, JigPrompts, ObjectId (+5 more)

### Community 16 - "CadAuto Core Geometry"
Cohesion: 0.13
Nodes (6): Circle2D, DrawingGeometry, List, FilletFeature2D, CadAuto.Core.Geometry, CadAuto.Core.Model

### Community 17 - "OutlineSegment Module"
Cohesion: 0.12
Nodes (3): DimensionPlanner, int, Tuple

### Community 18 - "CadAuto CadAdapter Model"
Cohesion: 0.18
Nodes (7): GeneratedAnnotation, ObjectId, AutoFixDimOutputScope, CadAuto.CadAdapter.Recognition, CadAuto.CadAdapter.Mapping, CadAuto.CadAdapter, CadAuto.CadAdapter.Collection

### Community 19 - "HoleFeature Module"
Cohesion: 0.25
Nodes (5): LayerManager, Database, string, Transaction, CadAuto.CadAdapter.Environment

### Community 20 - "HoleCalloutPlan Module"
Cohesion: 0.11
Nodes (10): DimensionDeduplicationItem, DimensionDeduplicationRules, PartitionCandidate, double, IList, int, List, Tuple (+2 more)

### Community 21 - "DimensionLayoutRules Module"
Cohesion: 0.11
Nodes (17): BlockReference, SlotFeature, ObjectId, Point2d, SlotArcCandidate, SlotLineCandidate, ThreadArcInfo, ThreadMinorCircleInfo (+9 more)

### Community 23 - "DimensionSide Module"
Cohesion: 0.13
Nodes (5): DimensionDiagnosticReport, IEnumerable, List, FeatureDiagnosticCounts, CadAuto.Core.Planning

### Community 24 - "FeatureRecognizer2D Module"
Cohesion: 0.14
Nodes (3): ExtensionLineBreakRange, CadAuto.Core.Rules, P

### Community 25 - "StackingLayerItem Module"
Cohesion: 0.06
Nodes (22): DimensionLayoutItem, DimensionLayoutRules, ExtensionLineBreakCandidate, IndexedLayoutItem, LayoutBlock, StackingLayerItem, TextSlideCandidate, IEnumerable (+14 more)

### Community 26 - "NativeDiameterDimensioner Module"
Cohesion: 0.14
Nodes (10): BlockTableRecord, Database, Entity, Line, ObjectId, Point3d, Polyline, Polyline2d (+2 more)

### Community 27 - "Commands Module"
Cohesion: 0.07
Nodes (32): HoleFeature, ObjectId, Point3d, DiameterCalloutJig, NativeDiameterDimensioner, BlockTableRecord, Database, Document (+24 more)

### Community 28 - "NativeDiameterDimensioner Module"
Cohesion: 0.15
Nodes (7): FilletFeature, Point2d, Editor, IEnumerable, IList, Point2d, Point3d

### Community 29 - "DimensionDrawer Module"
Cohesion: 0.15
Nodes (3): Datum2D, SlotFeature2D, Func

### Community 30 - "DimensionDrawer Module"
Cohesion: 0.11
Nodes (17): GeometryCollector, OutlineEntityCandidate, Arc, Editor, Entity, IList, Line, List (+9 more)

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
Cohesion: 0.25
Nodes (6): FeatureRecognizer, bool, double, Entity, Polyline, Polyline2d

### Community 35 - "Point3d"
Cohesion: 0.36
Nodes (8): DeferredDim, DimSide, PlacedDim, TextBounds, bool, double, int, string

### Community 36 - "SlotArcCandidate Module"
Cohesion: 0.16
Nodes (3): Point3d, PlacedDim, TextBounds

### Community 37 - "Flush Side"
Cohesion: 0.09
Nodes (6): Point2D, OutlineFeature2D, List, IEquatable, IgnoredPoint, StructurePoint

### Community 38 - "DimensionDrawer Module"
Cohesion: 0.12
Nodes (7): AutoFixDimOutputScope, CadToCoreModelMapper, IEnumerable, List, DatumDefinition, HoleKind, HoleKind2D

### Community 40 - "DimensionDrawer Module"
Cohesion: 0.24
Nodes (5): DimensionType, DimensionPlanCadItem, Point3d, DimensionPlanCadMapper, IEnumerable

### Community 41 - ".RecognizeSlotFeatures"
Cohesion: 0.27
Nodes (4): OutlineArc, ObjectId, Point2d, Arc

### Community 45 - "AnnotationMetadata Module"
Cohesion: 0.23
Nodes (9): AnnotationMetadata, Database, Entity, IEnumerable, List, string, Transaction, DateTime (+1 more)

### Community 47 - "DimSide Module"
Cohesion: 0.16
Nodes (5): DimensionDrawer, BlockTableRecord, Transaction, DimSide, RotatedDimension

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
- **11 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `DimensionRuleConfig` connect `Commands Module` to `DimensionDrawer Module`, `GeometryCollector Module`, `DimensionPlan Module`, `FeatureRecognizer2D Module`, `DimensionDrawer Module`, `Assert Module`, `DimensionDrawer Module`, `StructureSuppressionRules Module`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `DimSide Module`, `OutlineSegment Module`, `HoleCalloutPlan Module`, `DimensionLayoutRules Module`, `StructureEndpointRules Module`, `FeatureRecognizer2D Module`, `StackingLayerItem Module`, `DimensionDrawer Module`?**
  _High betweenness centrality (0.227) - this node is a cross-community bridge._
- **Why does `DimensionPlanner` connect `OutlineSegment Module` to `Point2D Module`, `OutlineFeature2D Module`, `HoleFeature2D Module`, `Flush Side`, `DimensionDrawer Module`, `Assert Module`, `FeatureRecognizer2D Module`, `DimensionDrawer Module`, `StructureSuppressionRules Module`, `FeatureRecognizer Module`, `HoleCalloutPlan Module`, `StructureEndpointRules Module`, `Commands Module`, `DimensionDrawer Module`?**
  _High betweenness centrality (0.130) - this node is a cross-community bridge._
- **Why does `DimensionDrawer` connect `DimSide Module` to `GeometryCollector Module`, `Point3d`, `SlotArcCandidate Module`, `DimensionDrawer Module`, `DimensionDrawer Module`, `DebugAnnotationRenderer Module`, `DimensionDrawer Module`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `HoleCalloutPlan Module`, `DimensionSide Module`, `StackingLayerItem Module`, `Commands Module`, `NativeDiameterDimensioner Module`?**
  _High betweenness centrality (0.128) - this node is a cross-community bridge._
- **What connects `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests` to the rest of the system?**
  _38 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Point2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.08683853459972862 - nodes in this community are weakly interconnected._
- **Should `OutlineFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.14285714285714285 - nodes in this community are weakly interconnected._
- **Should `HoleFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.09085213032581453 - nodes in this community are weakly interconnected._