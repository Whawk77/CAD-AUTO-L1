# Graph Report - L1-grok  (2026-07-21)

## Corpus Check
- 105 files · ~82,709 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1482 nodes · 5447 edges · 63 communities (55 shown, 8 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 334 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `be972012`
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
- DimensionRuleConfig
- AnnotationMetadata Module
- AutoCadGeometryConverter.cs
- DimSide Module
- FeatureRecognizer2D.cs
- AUTOFIXDIM Agent Rules
- When invoked
- DebugAnnotationRenderer Module
- Resolve Dim Style
- run cad test ps1
- Test Has Property

## God Nodes (most connected - your core abstractions)
1. `OutlineFeature2D` - 249 edges
2. `DimensionPlanner` - 245 edges
3. `Point2D` - 227 edges
4. `Segment2D` - 114 edges
5. `DimensionDrawer` - 113 edges
6. `Program` - 108 edges
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

## Communities (63 total, 8 thin omitted)

### Community 0 - "Point2D Module"
Cohesion: 0.13
Nodes (8): FunctionalHoleGroupPlan, IgnoredPoint, LooseHoleLineGroup, LooseHoleMacroGroup, StructurePoint, List, LooseHoleLineGroup, LooseHoleMacroGroup

### Community 2 - "HoleFeature2D Module"
Cohesion: 0.13
Nodes (6): DimensionCandidateDiagnostic, DimensionOrientation, DimensionPlan, Dictionary, int, List

### Community 3 - "GeometryCollector Module"
Cohesion: 0.17
Nodes (5): CommandMethod, Commands, Editor, IList, string

### Community 4 - "DimensionPlan Module"
Cohesion: 0.16
Nodes (7): AutoFixDimOutputScope, CadToCoreModelMapper, IEnumerable, List, DatumDefinition, DiagnosticDimensionSide, IEnumerable

### Community 5 - "FeatureRecognizer2D Module"
Cohesion: 0.10
Nodes (18): AutoCadGeometryConverter, Arc, Circle, Line, Point2d, Point3d, Polyline, Arc2D (+10 more)

### Community 7 - "Assert Module"
Cohesion: 0.07
Nodes (3): Action, Program, int

### Community 8 - "Commands Module"
Cohesion: 0.16
Nodes (10): ChamferFeature, Point2d, OutlineFeature, List, Point2d, OutlineSegment, ObjectId, Point2d (+2 more)

### Community 9 - "DimensionDrawer Module"
Cohesion: 0.14
Nodes (5): DimensionDrawer, BlockTableRecord, List, Transaction, RotatedDimension

### Community 10 - "DimensionDrawer Module"
Cohesion: 0.16
Nodes (3): Point2D, IEnumerable, IEquatable

### Community 11 - "StructureSuppressionRules Module"
Cohesion: 0.08
Nodes (4): ChamferFeature2D, FilletFeature2D, StructureSuppressionRules, IEnumerable

### Community 12 - "DimensionDrawer Module"
Cohesion: 0.13
Nodes (10): CadEntityWriter, BlockTableRecord, Color, Database, double, ObjectId, string, Transaction (+2 more)

### Community 13 - "FeatureRecognizer Module"
Cohesion: 0.16
Nodes (3): IList, PlannedDimension, IList

### Community 14 - "OutlineFeature Module"
Cohesion: 0.22
Nodes (8): SlotFeature, ObjectId, Point2d, SlotArcCandidate, SlotLineCandidate, ThreadArcInfo, ThreadMinorCircleInfo, ObjectId

### Community 15 - "FeatureRecognizer Module"
Cohesion: 0.10
Nodes (15): CornerCalloutJigResult, CornerCalloutRenderer, CornerFeatureLeaderJig, Database, double, Editor, JigPrompts, ObjectId (+7 more)

### Community 16 - "CadAuto Core Geometry"
Cohesion: 0.14
Nodes (3): BoundingBox2D, CadAuto.Core.Geometry, CadAuto.Core.Model

### Community 17 - "OutlineSegment Module"
Cohesion: 0.12
Nodes (4): DimensionPlanner, int, Tuple, HashSet

### Community 18 - "CadAuto CadAdapter Model"
Cohesion: 0.15
Nodes (6): DimSide, AutoFixDimOutputScope, CadAuto.CadAdapter.Recognition, CadAuto.CadAdapter.Mapping, CadAuto.CadAdapter.Model, CadAuto.CadAdapter.Collection

### Community 19 - "HoleFeature Module"
Cohesion: 0.12
Nodes (6): HoleFeature2D, LooseHoleLocationPlan, PinGroupPlan, List, FunctionalHoleGroupPlan, LooseHoleLocationPlan

### Community 20 - "HoleCalloutPlan Module"
Cohesion: 0.17
Nodes (4): DimensionDeduplicationItem, DimensionDeduplicationRules, IList, Tuple

### Community 21 - "DimensionLayoutRules Module"
Cohesion: 0.17
Nodes (9): BlockReference, Circle, Editor, IEnumerable, IList, Point3d, Transaction, ThreadArcInfo (+1 more)

### Community 22 - "StructureEndpointRules Module"
Cohesion: 0.17
Nodes (3): Segment2D, StructureEndpointRules, StructureEndpointSpan

### Community 23 - "DimensionSide Module"
Cohesion: 0.12
Nodes (5): DimensionDiagnosticReport, IEnumerable, List, FeatureDiagnosticCounts, CadAuto.Core.Planning

### Community 25 - "StackingLayerItem Module"
Cohesion: 0.08
Nodes (19): DimensionSide, DimensionLayoutItem, DimensionLayoutRules, LayoutBlock, IEnumerable, IList, List, Tuple (+11 more)

### Community 26 - "NativeDiameterDimensioner Module"
Cohesion: 0.14
Nodes (10): BlockTableRecord, Database, Entity, Line, ObjectId, Point3d, Polyline, Polyline2d (+2 more)

### Community 27 - "Commands Module"
Cohesion: 0.07
Nodes (34): HoleFeature, ObjectId, Point3d, DiameterCalloutJig, NativeDiameterDimensioner, BlockTableRecord, Database, Document (+26 more)

### Community 28 - "NativeDiameterDimensioner Module"
Cohesion: 0.29
Nodes (3): FilletFeature, Point2d, Point2d

### Community 29 - "DimensionDrawer Module"
Cohesion: 0.13
Nodes (6): Datum2D, SlotFeature2D, DimensionKind, IEnumerable, DimensionReadingLevel, Func

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
Cohesion: 0.18
Nodes (5): ISet, IList, ISet, SlotArcCandidate, SlotLineCandidate

### Community 34 - "DimensionDrawer Module"
Cohesion: 0.25
Nodes (6): FeatureRecognizer, bool, double, Entity, Polyline, Polyline2d

### Community 35 - "Point3d"
Cohesion: 0.17
Nodes (9): DeferredDim, PlacedDim, TextBounds, bool, double, int, Point3d, string (+1 more)

### Community 39 - "CadAuto Core Planning"
Cohesion: 0.40
Nodes (4): ExtensionLineBreakCandidate, IndexedLayoutItem, StackingLayerItem, TextSlideCandidate

### Community 40 - "DimensionDrawer Module"
Cohesion: 0.16
Nodes (7): DimensionType, DimensionPlanCadItem, Point3d, DimensionPlanCadMapper, IEnumerable, DiameterJigResult, CadAuto.CadAdapter.Rendering

### Community 41 - ".RecognizeSlotFeatures"
Cohesion: 0.20
Nodes (4): OutlineArc, ObjectId, Point2d, Arc

### Community 42 - ".ResolveAnnotationLayer"
Cohesion: 0.25
Nodes (5): LayerManager, Database, string, Transaction, CadAuto.CadAdapter.Environment

### Community 43 - "DimensionExtensionLineRenderer"
Cohesion: 0.29
Nodes (5): DimensionExtensionLineRenderer, Database, IList, string, Tuple

### Community 45 - "AnnotationMetadata Module"
Cohesion: 0.19
Nodes (10): AnnotationMetadata, Database, Entity, IEnumerable, List, string, Transaction, Document (+2 more)

### Community 46 - "AutoCadGeometryConverter.cs"
Cohesion: 0.40
Nodes (3): GeneratedAnnotation, ObjectId, CadAuto.CadAdapter

### Community 47 - "DimSide Module"
Cohesion: 0.21
Nodes (4): A, B, DeferredDim, DimSide

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
Cohesion: 0.15
Nodes (10): Entity, Point3d, DebugAnnotationRenderer, Color, Database, ObjectId, Point2d, Point3d (+2 more)

### Community 81 - "Resolve Dim Style"
Cohesion: 0.31
Nodes (5): DimStyleManager, Database, ObjectId, string, Transaction

### Community 105 - "Test Has Property"
Cohesion: 0.83
Nodes (3): Get-PhysicalRank(), Test-DimensionSelector(), Test-HasProperty()

## Knowledge Gaps
- **36 isolated node(s):** `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests`, `ExtensionLineBreakCandidate`, `AutoFixDimOutputScope` (+31 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **8 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `DimensionRuleConfig` connect `Commands Module` to `DimensionDrawer Module`, `GeometryCollector Module`, `DimensionPlan Module`, `FeatureRecognizer2D Module`, `DimensionDrawer Module`, `Assert Module`, `DimensionDrawer Module`, `StructureSuppressionRules Module`, `DimensionDrawer Module`, `AnnotationMetadata Module`, `FeatureRecognizer Module`, `OutlineSegment Module`, `HoleCalloutPlan Module`, `DimensionLayoutRules Module`, `StructureEndpointRules Module`, `FeatureRecognizer2D Module`, `StackingLayerItem Module`, `DimensionDrawer Module`?**
  _High betweenness centrality (0.240) - this node is a cross-community bridge._
- **Why does `DimensionPlanner` connect `OutlineSegment Module` to `Point2D Module`, `OutlineFeature2D Module`, `HoleFeature2D Module`, `Flush Side`, `FeatureRecognizer2D Module`, `DimensionDrawer Module`, `StructureSuppressionRules Module`, `FeatureRecognizer Module`, `HoleFeature Module`, `HoleCalloutPlan Module`, `StructureEndpointRules Module`, `Commands Module`, `DimensionDrawer Module`?**
  _High betweenness centrality (0.148) - this node is a cross-community bridge._
- **Why does `DimensionDrawer` connect `DimensionDrawer Module` to `Point3d`, `DimensionPlan Module`, `SlotArcCandidate Module`, `GeometryCollector Module`, `DimensionDrawer Module`, `DebugAnnotationRenderer Module`, `DimensionExtensionLineRenderer`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `DimSide Module`, `CadAuto CadAdapter Model`, `HoleCalloutPlan Module`, `DimensionSide Module`, `StackingLayerItem Module`, `Commands Module`, `NativeDiameterDimensioner Module`?**
  _High betweenness centrality (0.132) - this node is a cross-community bridge._
- **What connects `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests` to the rest of the system?**
  _36 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Point2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.13438735177865613 - nodes in this community are weakly interconnected._
- **Should `OutlineFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.14603174603174604 - nodes in this community are weakly interconnected._
- **Should `HoleFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.1251778093883357 - nodes in this community are weakly interconnected._