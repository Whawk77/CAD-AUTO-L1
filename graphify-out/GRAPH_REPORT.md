# Graph Report - L1-grok  (2026-07-21)

## Corpus Check
- 104 files · ~77,185 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1444 nodes · 5328 edges · 57 communities (48 shown, 9 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 330 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `667a29e6`
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
- CadAuto Core Rules
- SlotArcCandidate Module
- Flush Side
- DimensionDrawer Module
- CadAuto Core Planning
- DimensionDrawer Module
- Point2d Module
- AnnotationMetadata Module
- DimSide Module
- AUTOFIXDIM Agent Rules
- DebugAnnotationRenderer Module
- Resolve Dim Style
- run cad test ps1
- Test Has Property

## God Nodes (most connected - your core abstractions)
1. `OutlineFeature2D` - 249 edges
2. `DimensionPlanner` - 236 edges
3. `Point2D` - 227 edges
4. `Segment2D` - 114 edges
5. `DimensionDrawer` - 113 edges
6. `Program` - 94 edges
7. `DimensionLayoutRules` - 94 edges
8. `DimensionPlan` - 83 edges
9. `FeatureRecognizer` - 82 edges
10. `HoleFeature2D` - 81 edges

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

## Communities (57 total, 9 thin omitted)

### Community 0 - "Point2D Module"
Cohesion: 0.09
Nodes (9): DimensionKind, DimensionOrientation, DimensionPlan, Dictionary, int, List, DimensionReadingLevel, PlannedDimension (+1 more)

### Community 1 - "OutlineFeature2D Module"
Cohesion: 0.08
Nodes (6): Point2D, FilletFeature2D, OutlineFeature2D, List, StructureEndpointSpan, IEquatable

### Community 2 - "HoleFeature2D Module"
Cohesion: 0.12
Nodes (3): DimensionPlanner, int, Tuple

### Community 3 - "GeometryCollector Module"
Cohesion: 0.13
Nodes (9): AutoFixDimOutputScope, DiagnosticDimensionSide, CommandMethod, Commands, Document, Editor, IEnumerable, IList (+1 more)

### Community 4 - "DimensionPlan Module"
Cohesion: 0.16
Nodes (4): Datum2D, SlotFeature2D, IEnumerable, Func

### Community 5 - "FeatureRecognizer2D Module"
Cohesion: 0.11
Nodes (15): AutoCadGeometryConverter, Arc, Circle, Line, Point2d, Point3d, Polyline, Arc2D (+7 more)

### Community 7 - "Assert Module"
Cohesion: 0.07
Nodes (3): Action, Program, int

### Community 8 - "Commands Module"
Cohesion: 0.17
Nodes (7): ChamferFeature, Point2d, OutlineSegment, ObjectId, Point2d, Point2d, Vector2d

### Community 9 - "DimensionDrawer Module"
Cohesion: 0.17
Nodes (7): A, B, DimensionDrawer, BlockTableRecord, List, Transaction, DeferredDim

### Community 11 - "StructureSuppressionRules Module"
Cohesion: 0.09
Nodes (4): Segment2D, ChamferFeature2D, StructureSuppressionRules, IEnumerable

### Community 12 - "DimensionDrawer Module"
Cohesion: 0.13
Nodes (10): CadEntityWriter, BlockTableRecord, Color, Database, double, ObjectId, string, Transaction (+2 more)

### Community 13 - "FeatureRecognizer Module"
Cohesion: 0.11
Nodes (17): BlockReference, SlotFeature, ObjectId, Point2d, SlotArcCandidate, SlotLineCandidate, ThreadArcInfo, ThreadMinorCircleInfo (+9 more)

### Community 14 - "OutlineFeature Module"
Cohesion: 0.19
Nodes (6): FeatureRecognizer, bool, double, Entity, Polyline, Polyline2d

### Community 15 - "FeatureRecognizer Module"
Cohesion: 0.10
Nodes (15): CornerCalloutJigResult, CornerCalloutRenderer, CornerFeatureLeaderJig, Database, double, Editor, JigPrompts, ObjectId (+7 more)

### Community 16 - "CadAuto Core Geometry"
Cohesion: 0.12
Nodes (7): Circle2D, DrawingGeometry, List, HoleCalloutCluster, List, CadAuto.Core.Geometry, CadAuto.Core.Model

### Community 17 - "OutlineSegment Module"
Cohesion: 0.19
Nodes (5): ISet, IList, ISet, SlotArcCandidate, SlotLineCandidate

### Community 18 - "CadAuto CadAdapter Model"
Cohesion: 0.12
Nodes (8): DimensionType, DimensionPlanCadItem, Point3d, DimensionPlanCadMapper, IEnumerable, DiameterJigResult, CadAuto.CadAdapter.Model, CadAuto.CadAdapter.Rendering

### Community 19 - "HoleFeature Module"
Cohesion: 0.12
Nodes (10): HoleFeature2D, FunctionalHoleGroupPlan, LooseHoleLocationPlan, LooseHoleMacroGroup, IList, List, FunctionalHoleGroupPlan, LooseHoleLineGroup (+2 more)

### Community 20 - "HoleCalloutPlan Module"
Cohesion: 0.20
Nodes (3): DimensionDeduplicationItem, DimensionDeduplicationRules, Tuple

### Community 23 - "DimensionSide Module"
Cohesion: 0.13
Nodes (6): DimensionCandidateDiagnostic, DimensionDiagnosticReport, IEnumerable, List, FeatureDiagnosticCounts, CadAuto.Core.Planning

### Community 24 - "FeatureRecognizer2D Module"
Cohesion: 0.12
Nodes (6): SlotArcCandidate, SlotLineCandidate, ExtensionLineBreakRange, CadAuto.Core.Recognition, CadAuto.Core.Rules, CadAuto.Core.Tests

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
Cohesion: 0.21
Nodes (4): FilletFeature, Point2d, Point2d, Point3d

### Community 30 - "DimensionDrawer Module"
Cohesion: 0.07
Nodes (23): GeometryCollector, OutlineEntityCandidate, Arc, Editor, Entity, IList, Line, List (+15 more)

### Community 31 - "DatumDefinition Module"
Cohesion: 0.14
Nodes (13): 1. 销对之间的孔 → 功能孔, 2. 同侧结构段对齐（方案 1）, 3. OutlineSegment 复述 overall, 4. 结构宽/高 vs overall — 规则过大已修正, 5. 文档与测试, Handoff — 尺寸规则改进（2026-07-20）, 仓库与分支, 工作约定 (+5 more)

### Community 32 - "DimensionRuleConfig Module"
Cohesion: 0.29
Nodes (3): AutoFixtureDim, IExtensionApplication, PluginEntry

### Community 33 - "SlotFeature Module"
Cohesion: 0.42
Nodes (4): BlockTableRecord, Database, ObjectId, Transaction

### Community 34 - "DimensionDrawer Module"
Cohesion: 0.24
Nodes (4): OutlineArc, ObjectId, Point2d, Arc

### Community 35 - "CadAuto Core Rules"
Cohesion: 0.18
Nodes (7): GeneratedAnnotation, ObjectId, AutoFixDimOutputScope, CadAuto.CadAdapter.Recognition, CadAuto.CadAdapter.Mapping, CadAuto.CadAdapter, CadAuto.CadAdapter.Collection

### Community 37 - "Flush Side"
Cohesion: 0.36
Nodes (8): DeferredDim, DimSide, PlacedDim, TextBounds, bool, double, int, string

### Community 39 - "CadAuto Core Planning"
Cohesion: 0.40
Nodes (4): ExtensionLineBreakCandidate, IndexedLayoutItem, StackingLayerItem, TextSlideCandidate

### Community 40 - "DimensionDrawer Module"
Cohesion: 0.50
Nodes (3): IgnoredPoint, LooseHoleLineGroup, StructurePoint

### Community 45 - "AnnotationMetadata Module"
Cohesion: 0.23
Nodes (9): AnnotationMetadata, Database, Entity, IEnumerable, List, string, Transaction, DateTime (+1 more)

### Community 47 - "DimSide Module"
Cohesion: 0.18
Nodes (6): OutlineFeature, List, Point2d, IList, DimSide, RotatedDimension

### Community 50 - "AUTOFIXDIM Agent Rules"
Cohesion: 0.06
Nodes (42): AUTOFIXDIM Agent Rules, Annotation Product Invariants, Explicit Validation Authorization, Graphify-First Codebase Navigation, Safe File Operations, Deployment and Manual Verification, Four-Direction Automated Regression, Manual AutoCAD Verification Flow (+34 more)

### Community 73 - "DebugAnnotationRenderer Module"
Cohesion: 0.11
Nodes (14): Entity, DebugAnnotationRenderer, Color, Database, ObjectId, Point2d, Point3d, string (+6 more)

### Community 81 - "Resolve Dim Style"
Cohesion: 0.16
Nodes (10): DimStyleManager, Database, ObjectId, string, Transaction, LayerManager, Database, string (+2 more)

### Community 105 - "Test Has Property"
Cohesion: 0.83
Nodes (3): Get-PhysicalRank(), Test-DimensionSelector(), Test-HasProperty()

## Knowledge Gaps
- **30 isolated node(s):** `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests`, `ExtensionLineBreakCandidate`, `AutoFixDimOutputScope` (+25 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **9 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `DimensionRuleConfig` connect `Commands Module` to `HoleFeature2D Module`, `GeometryCollector Module`, `FeatureRecognizer2D Module`, `DimensionDrawer Module`, `Assert Module`, `DimensionDrawer Module`, `StructureSuppressionRules Module`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `OutlineFeature Module`, `FeatureRecognizer Module`, `DimSide Module`, `HoleCalloutPlan Module`, `StructureEndpointRules Module`, `FeatureRecognizer2D Module`, `StackingLayerItem Module`, `DimensionDrawer Module`?**
  _High betweenness centrality (0.233) - this node is a cross-community bridge._
- **Why does `DimensionDrawer` connect `DimensionDrawer Module` to `GeometryCollector Module`, `SlotArcCandidate Module`, `Flush Side`, `DebugAnnotationRenderer Module`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `DimSide Module`, `CadAuto CadAdapter Model`, `HoleCalloutPlan Module`, `DimensionLayoutRules Module`, `DimensionSide Module`, `StackingLayerItem Module`, `Commands Module`, `NativeDiameterDimensioner Module`?**
  _High betweenness centrality (0.140) - this node is a cross-community bridge._
- **Why does `DimensionPlanner` connect `HoleFeature2D Module` to `Point2D Module`, `OutlineFeature2D Module`, `DimensionPlan Module`, `FeatureRecognizer2D Module`, `DimensionDrawer Module`, `DimensionDrawer Module`, `StructureSuppressionRules Module`, `HoleFeature Module`, `HoleCalloutPlan Module`, `StructureEndpointRules Module`, `Commands Module`, `DimensionDrawer Module`?**
  _High betweenness centrality (0.127) - this node is a cross-community bridge._
- **What connects `DimSide`, `DiameterJigResult`, `CadAuto.Core.Tests` to the rest of the system?**
  _30 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Point2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.08832425892316999 - nodes in this community are weakly interconnected._
- **Should `OutlineFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.07932310946589106 - nodes in this community are weakly interconnected._
- **Should `HoleFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.12012012012012012 - nodes in this community are weakly interconnected._