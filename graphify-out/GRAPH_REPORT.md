# Graph Report - D:\work\AI\project\autocad-dim  (2026-07-20)

## Corpus Check
- 184 files · ~153,272 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2854 nodes · 9662 edges · 119 communities (105 shown, 14 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 342 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

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
- CadEntityWriter Module
- CornerCalloutRenderer Module
- DimensionDrawer Module
- AnnotationMetadata Module
- DeferredDim Module
- DimSide Module
- CadEntityWriter Module
- Point3d Module
- AUTOFIXDIM Agent Rules
- DimensionType Module
- DimensionDrawer Module
- Get Break Ranges For Extension
- Segment2D Module
- DimensionPlan Module
- DimensionDeduplicationRules Module
- DimensionDrawer Module
- CornerCalloutRenderer Module
- Append Dimension Diagnostics
- Point3d Module
- CadToCoreModelMapper Module
- DimensionRuleConfig Module
- Graphify Workflow
- DimensionRuleConfig Module
- Recognize Outline
- HoleDiameterLeaderRenderer Module
- Select Vertical Hole Location Rebalance
- DimensionDrawer Module
- DimensionPlanCadItem Module
- Dimension Rules
- Incremental Graph Update
- DebugAnnotationRenderer Module
- DebugAnnotationRenderer Module
- Ag1 Module
- AnnotationMetadata Module
- OutlineArc Module
- Recognize Outline From Segments
- AUTOFIXDIM Project Handoff
- Recognition Rules
- Resolve Dim Style
- CadToCoreModelMapper Module
- AnnotationPreview Module
- Resolve Dim Style
- FeatureRecognizer2D Module
- DimensionExtensionLineRenderer Module
- OutlineArc Module
- DimensionExtensionLineRenderer Module
- Top Bottom Left Right Workflow
- PlacedDim Module
- Point2D Module
- run cad test ps1
- Build Hole Callout Plans For
- Graphify Query Reference
- Annotation Product Invariants
- Resolve Annotation Layer
- OutlineSegment Module
- DimensionLayoutRules Module
- Resolve Annotation Layer
- Graphify Export Reference
- GitHub and Merge Reference
- OutlineSelection Module
- Segment2D Module
- Test Has Property
- From Outline
- Include Module
- Include Module

## God Nodes (most connected - your core abstractions)
1. `OutlineFeature2D` - 359 edges
2. `Point2D` - 347 edges
3. `OutlineFeature` - 291 edges
4. `DimensionPlanner` - 231 edges
5. `DimensionPlanner` - 205 edges
6. `Segment2D` - 156 edges
7. `HoleFeature2D` - 147 edges
8. `DimensionPlan` - 113 edges
9. `DimensionDrawer` - 111 edges
10. `HoleFeature` - 110 edges

## Surprising Connections (you probably didn't know these)
- `Pin-Group Datum Chain` --semantically_similar_to--> `Pin-First Hole Positioning`  [INFERRED] [semantically similar]
  PROJECT_HANDOFF.md → docs/DimensionRules.md
- `Graph Health Check` --semantically_similar_to--> `Structured Report Validation`  [INFERRED] [semantically similar]
  .codex/skills/graphify/SKILL.md → docs/DimensionLayoutRegression.md
- `Annotation Product Invariants` --semantically_similar_to--> `Real Geometry Attachment Rules`  [INFERRED] [semantically similar]
  AGENTS.md → docs/DimensionRules.md
- `Legacy Dimension and Recognition Rules` --semantically_similar_to--> `Annotation Product Invariants`  [INFERRED] [semantically similar]
  AGENTS.old.md → AGENTS.md
- `Loose-Hole Layout Focus` --semantically_similar_to--> `Loose-Hole Planning Model`  [INFERRED] [semantically similar]
  HANDOFF.md → PROJECT_HANDOFF.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Graphify Build Pipeline** — _codex_skills_graphify_skill_structural_extraction, _codex_skills_graphify_skill_semantic_extraction, _codex_skills_graphify_skill_community_detection, _codex_skills_graphify_skill_graph_outputs [EXTRACTED 1.00]
- **AUTOFIXDIM Annotation Rule System** — docs_recognitionrules_recognition_rules, docs_dimensionrules_dimension_rules, agents_annotation_product_invariants, docs_environment_source_map [INFERRED 0.85]
- **Four-Direction Regression Evidence Chain** — docs_dimensionlayoutregression_dl01_fixture, docs_dimensionlayoutregression_trace_and_report_archiving, docs_dimensionlayoutregression_structured_report_validation, docs_dimensionlayoutregression_visual_image_review, docs_dimensionlayoutregression_completion_gate [EXTRACTED 1.00]

## Communities (119 total, 14 thin omitted)

### Community 0 - "Point2D Module"
Cohesion: 0.05
Nodes (11): Point2D, FilletFeature2D, PlannedDimension, DimensionPlanner, IList, int, List, OutlineFeature2D (+3 more)

### Community 1 - "OutlineFeature2D Module"
Cohesion: 0.06
Nodes (9): IgnoredPoint, StructurePoint, OutlineFeature2D, List, DimensionPlanner, FunctionalHoleGroupPlan, IList, int (+1 more)

### Community 2 - "HoleFeature2D Module"
Cohesion: 0.05
Nodes (18): LooseHoleLocationPlan, HoleFeature2D, PinGroupPlan, List, FunctionalHoleGroupPlan, IgnoredPoint, LooseHoleLineGroup, LooseHoleLocationPlan (+10 more)

### Community 3 - "GeometryCollector Module"
Cohesion: 0.06
Nodes (30): GeometryCollector, OutlineEntityCandidate, Arc, Editor, Entity, IList, LayerTableRecord, Line (+22 more)

### Community 4 - "DimensionPlan Module"
Cohesion: 0.07
Nodes (9): Datum2D, SlotFeature2D, DimensionKind, DimensionPlan, Func, Func, IEnumerable, DimensionReadingLevel (+1 more)

### Community 5 - "FeatureRecognizer2D Module"
Cohesion: 0.05
Nodes (33): AutoCadGeometryConverter, Arc, Circle, Line, Point2d, Point3d, Polyline, AutoCadGeometryConverter (+25 more)

### Community 6 - "DimensionDrawer Module"
Cohesion: 0.05
Nodes (30): FilletFeature, CornerLeaderPlacement, DimensionDrawer, IEnumerable, IList, List, Point2d, Vector2d (+22 more)

### Community 7 - "Assert Module"
Cohesion: 0.08
Nodes (3): Action, Program, int

### Community 8 - "Commands Module"
Cohesion: 0.07
Nodes (25): DimensionPlanRenderer, BlockTableRecord, Database, double, List, ObjectId, Point3d, string (+17 more)

### Community 9 - "DimensionDrawer Module"
Cohesion: 0.08
Nodes (29): Bounds, DimensionDrawer, A, B, DeferredDim, DimSide, Point3d, TextBounds (+21 more)

### Community 10 - "DimensionDrawer Module"
Cohesion: 0.14
Nodes (8): DimensionDrawer, DeferredDim, DimSide, IgnoredPoint, IList, List, Point2d, Point3d

### Community 11 - "StructureSuppressionRules Module"
Cohesion: 0.08
Nodes (3): ChamferFeature2D, StructureSuppressionRules, IEnumerable

### Community 12 - "DimensionDrawer Module"
Cohesion: 0.08
Nodes (9): TextBounds, PlacedDim, DimensionDrawer, BlockTableRecord, Color, Entity, ObjectId, Transaction (+1 more)

### Community 13 - "FeatureRecognizer Module"
Cohesion: 0.11
Nodes (18): FeatureRecognizer, SlotArcCandidate, SlotLineCandidate, ThreadArcInfo, ThreadMinorCircleInfo, Arc, BlockReference, bool (+10 more)

### Community 14 - "OutlineFeature Module"
Cohesion: 0.12
Nodes (7): OutlineFeature, Point2d, Entity, Point2d, Polyline, Polyline2d, Vector2d

### Community 15 - "FeatureRecognizer Module"
Cohesion: 0.13
Nodes (15): FeatureRecognizer, SlotArcCandidate, SlotLineCandidate, ThreadArcInfo, ThreadMinorCircleInfo, Arc, BlockReference, bool (+7 more)

### Community 16 - "CadAuto Core Geometry"
Cohesion: 0.08
Nodes (11): RenderItem, HoleKind2D, Arc2D, Circle2D, ChamferFeature2D, HoleFeature2D, HoleKind2D, SlotFeature2D (+3 more)

### Community 17 - "OutlineSegment Module"
Cohesion: 0.10
Nodes (7): ChamferFeature, OutlineSegment, Point2d, DimensionDrawer, DimSide, IEnumerable, Point2d

### Community 18 - "CadAuto CadAdapter Model"
Cohesion: 0.07
Nodes (12): DiameterJigResult, ChamferFeature, Point2d, DimensionType, FilletFeature, Point2d, HoleKind, CornerCalloutJigResult (+4 more)

### Community 19 - "HoleFeature Module"
Cohesion: 0.17
Nodes (12): HoleFeature, Point3d, DimensionDrawer, DeferredDim, DimSide, IEnumerable, IList, List (+4 more)

### Community 20 - "HoleCalloutPlan Module"
Cohesion: 0.14
Nodes (14): HoleCalloutKind, HoleCalloutPlan, List, HoleCalloutCluster, HoleCalloutPlanner, IEnumerable, IList, List (+6 more)

### Community 21 - "DimensionLayoutRules Module"
Cohesion: 0.14
Nodes (7): DimensionLayoutRules, IEnumerable, DimensionTextPlacementItem, DimensionTextSlidePlacement, TextBounds2D, ExtensionLineBreakCandidate, TextSlideCandidate

### Community 22 - "StructureEndpointRules Module"
Cohesion: 0.14
Nodes (3): StructureEndpointRules, IEnumerable, IList

### Community 23 - "DimensionSide Module"
Cohesion: 0.14
Nodes (3): DimensionSide, DimensionLayoutItem, DimensionStackingPlacement

### Community 24 - "FeatureRecognizer2D Module"
Cohesion: 0.13
Nodes (3): FeatureRecognizer2D, IEnumerable, IList

### Community 25 - "StackingLayerItem Module"
Cohesion: 0.16
Nodes (6): IList, List, IDictionary, IndexedLayoutItem, LayoutBlock, StackingLayerItem

### Community 26 - "NativeDiameterDimensioner Module"
Cohesion: 0.13
Nodes (19): DiameterCalloutJig, NativeDiameterDimensioner, BlockTableRecord, Database, DiametricDimension, Document, double, Editor (+11 more)

### Community 27 - "Commands Module"
Cohesion: 0.14
Nodes (6): AutoFixDimOutputScope, DiagnosticDimensionSide, Commands, CommandMethod, Editor, string

### Community 28 - "NativeDiameterDimensioner Module"
Cohesion: 0.13
Nodes (18): DiameterCalloutJig, NativeDiameterDimensioner, BlockTableRecord, Database, DiametricDimension, Document, double, Editor (+10 more)

### Community 29 - "DimensionDrawer Module"
Cohesion: 0.14
Nodes (11): DimensionDrawer, Color, DeferredDim, DimSide, IEnumerable, IList, Point2d, Point3d (+3 more)

### Community 30 - "DimensionDrawer Module"
Cohesion: 0.20
Nodes (10): DimensionDrawer, DeferredDim, IgnoredPoint, IList, Point2d, StructurePoint, IEnumerable, IgnoredPoint (+2 more)

### Community 31 - "DatumDefinition Module"
Cohesion: 0.21
Nodes (8): DatumDefinition, DimensionDrawer, DimSide, IEnumerable, IList, List, PinGroupPlan, Point3d

### Community 32 - "DimensionRuleConfig Module"
Cohesion: 0.10
Nodes (7): CoreModelMapper, AutoFixtureDim, IExtensionApplication, PluginEntry, DimensionRuleConfig, Dictionary, PluginEntry

### Community 33 - "SlotFeature Module"
Cohesion: 0.25
Nodes (7): SlotFeature, DimensionDrawer, Func, IEnumerable, IList, List, Point3d

### Community 34 - "DimensionDrawer Module"
Cohesion: 0.21
Nodes (7): DimensionDrawer, DeferredDim, DimSide, IEnumerable, IList, List, Point2d

### Community 35 - "CadAuto Core Rules"
Cohesion: 0.10
Nodes (9): DimSide, TextBounds, ExtensionLineBreakRange, CadAuto.CadAdapter.Recognition, CadAuto.CadAdapter.Mapping, CadAuto.Core.Rules, CadAuto.CadAdapter.Environment, CadAuto.CadAdapter.Collection (+1 more)

### Community 36 - "SlotArcCandidate Module"
Cohesion: 0.18
Nodes (5): ISet, SlotArcCandidate, SlotLineCandidate, ISet, ISet

### Community 37 - "Flush Side"
Cohesion: 0.19
Nodes (12): DimensionDrawer, ArrA, ArrB, Dim, List, Point3d, TxtA, TxtB (+4 more)

### Community 38 - "DimensionDrawer Module"
Cohesion: 0.12
Nodes (9): DimensionDrawer, BlockTableRecord, Color, Database, Entity, IList, ObjectId, Point3d (+1 more)

### Community 39 - "CadAuto Core Planning"
Cohesion: 0.08
Nodes (10): DimensionKind, DimensionOrientation, IgnoredPoint, LooseHoleLineGroup, StructurePoint, DimensionSide, HoleCalloutKind, PinGroupPlan (+2 more)

### Community 40 - "DimensionDrawer Module"
Cohesion: 0.20
Nodes (9): DimensionDrawer, DeferredDim, DimSide, IEnumerable, IgnoredPoint, IList, List, Point2d (+1 more)

### Community 42 - "CadEntityWriter Module"
Cohesion: 0.13
Nodes (12): CadEntityWriter, BlockTableRecord, bool, Color, Database, double, Entity, IList (+4 more)

### Community 43 - "CornerCalloutRenderer Module"
Cohesion: 0.12
Nodes (15): CornerCalloutJigResult, CornerCalloutRenderer, CornerFeatureLeaderJig, bool, Database, double, Editor, JigPrompts (+7 more)

### Community 44 - "DimensionDrawer Module"
Cohesion: 0.14
Nodes (20): DeferredDim, DimSide, FunctionalHoleGroupPlan, IgnoredPoint, LooseHoleLineGroup, LooseHoleMacroGroup, PinGroupPlan, PlacedDim (+12 more)

### Community 45 - "AnnotationMetadata Module"
Cohesion: 0.17
Nodes (11): AnnotationMetadata, GeneratedAnnotation, Database, Entity, IEnumerable, List, ObjectId, string (+3 more)

### Community 46 - "DeferredDim Module"
Cohesion: 0.21
Nodes (4): DeferredDim, A, B, List

### Community 48 - "CadEntityWriter Module"
Cohesion: 0.15
Nodes (10): CadEntityWriter, BlockTableRecord, Color, Database, double, Entity, ObjectId, Point3d (+2 more)

### Community 49 - "Point3d Module"
Cohesion: 0.18
Nodes (5): Editor, IEnumerable, IList, Point2d, Point3d

### Community 50 - "AUTOFIXDIM Agent Rules"
Cohesion: 0.13
Nodes (19): AUTOFIXDIM Agent Rules, Explicit Validation Authorization, Graphify-First Codebase Navigation, Safe File Operations, Deployment and Manual Verification, Four-Direction Automated Regression, Manual AutoCAD Verification Flow, Manual CAD Smoke Test (+11 more)

### Community 51 - "DimensionType Module"
Cohesion: 0.33
Nodes (6): DimensionType, DimensionDrawer, DimSide, Point3d, DimensionPlanCadItem, Point3d

### Community 52 - "DimensionDrawer Module"
Cohesion: 0.27
Nodes (6): DimensionDrawer, IEnumerable, IList, List, PinGroupPlan, FunctionalHoleGroupPlan

### Community 53 - "Get Break Ranges For Extension"
Cohesion: 0.26
Nodes (10): DimensionDrawer, A, B, IList, List, PlacedDim, Point3d, TextBounds (+2 more)

### Community 55 - "DimensionPlan Module"
Cohesion: 0.22
Nodes (6): DimensionOrientation, DimensionCandidateDiagnostic, DimensionPlan, Dictionary, int, List

### Community 56 - "DimensionDeduplicationRules Module"
Cohesion: 0.23
Nodes (3): DimensionDeduplicationItem, DimensionDeduplicationRules, Tuple

### Community 57 - "DimensionDrawer Module"
Cohesion: 0.25
Nodes (4): DimensionDrawer, DeferredDim, IList, Point3d

### Community 58 - "CornerCalloutRenderer Module"
Cohesion: 0.14
Nodes (12): CornerCalloutRenderer, CornerFeatureLeaderJig, Database, double, Editor, JigPrompts, ObjectId, Point2d (+4 more)

### Community 59 - "Append Dimension Diagnostics"
Cohesion: 0.24
Nodes (5): DimensionDiagnosticReport, IEnumerable, List, FeatureDiagnosticCounts, StringBuilder

### Community 60 - "Point3d Module"
Cohesion: 0.19
Nodes (6): Polyline3d, Entity, Line, Point3d, Polyline, Polyline2d

### Community 61 - "CadToCoreModelMapper Module"
Cohesion: 0.18
Nodes (7): HoleKind, CadToCoreModelMapper, IEnumerable, List, HoleFeature, ObjectId, Point3d

### Community 63 - "Graphify Workflow"
Cohesion: 0.15
Nodes (14): Extraction Confidence Taxonomy, Deterministic Node IDs, Semantic Extraction Specification, Knowledge Graph Hyperedges, Semantic Similarity Edges, Graphify Transcription Reference, Whisper Media Transcription, Community Detection (+6 more)

### Community 66 - "Recognize Outline"
Cohesion: 0.33
Nodes (3): Entity, Polyline, Polyline2d

### Community 67 - "HoleDiameterLeaderRenderer Module"
Cohesion: 0.22
Nodes (9): HoleDiameterLeaderRenderer, HoleLeaderPoints, bool, Database, IList, ObjectId, Point3d, string (+1 more)

### Community 69 - "DimensionDrawer Module"
Cohesion: 0.35
Nodes (3): DimensionDrawer, DeferredDim, DimSide

### Community 70 - "DimensionPlanCadItem Module"
Cohesion: 0.23
Nodes (6): DimensionPlanCadItem, DimensionPlanCadMapper, IEnumerable, Point3d, DimensionPlanCadMapper, IEnumerable

### Community 71 - "Dimension Rules"
Cohesion: 0.20
Nodes (12): Dimension Rules, Functional and Loose Hole Dimensions, Local Boundary Placement Rules, Overall Dimension Priority, Pin-First Hole Positioning, Side-Specific Structure Dimension Flows, Dimension Styles and Callouts, LB51 Test Baseline (+4 more)

### Community 72 - "Incremental Graph Update"
Cohesion: 0.20
Nodes (11): Graphify Add and Watch Reference, Folder Watcher, URL Ingestion, Native CLAUDE.md Integration, Graphify Hooks Reference, Post-Commit Graph Rebuild Hook, Cluster-Only Refresh, Deleted Source Pruning (+3 more)

### Community 73 - "DebugAnnotationRenderer Module"
Cohesion: 0.22
Nodes (7): DebugAnnotationRenderer, Color, Database, ObjectId, Point2d, Point3d, string

### Community 74 - "DebugAnnotationRenderer Module"
Cohesion: 0.25
Nodes (7): DebugAnnotationRenderer, Color, Database, ObjectId, Point2d, Point3d, string

### Community 75 - "Ag1 Module"
Cohesion: 0.45
Nodes (4): BlockTableRecord, Database, ObjectId, Transaction

### Community 76 - "AnnotationMetadata Module"
Cohesion: 0.31
Nodes (5): AnnotationMetadata, Database, Entity, string, Transaction

### Community 79 - "AUTOFIXDIM Project Handoff"
Cohesion: 0.20
Nodes (10): Dimension Stacking and Deduplication, Fixture-Part Annotation Plugin Goal, Legacy Command Surface, ASD Diagnostic Labels, Dimension Suppression and Stacking, Post-Linear Interactive Callout Pipeline, Manual AutoCAD Verification, Pin-Group Datum Chain (+2 more)

### Community 80 - "Recognition Rules"
Cohesion: 0.20
Nodes (10): Chamfer Recognition, Datum Recognition, Recognizer and Layout Diagnostics Separation, Fillet Recognition, Hole Recognition, Main Outline Selection, Recognition Rules, U-Slot Recognition (+2 more)

### Community 81 - "Resolve Dim Style"
Cohesion: 0.33
Nodes (5): DimStyleManager, Database, ObjectId, string, Transaction

### Community 82 - "CadToCoreModelMapper Module"
Cohesion: 0.36
Nodes (3): CadToCoreModelMapper, IEnumerable, List

### Community 83 - "AnnotationPreview Module"
Cohesion: 0.22
Nodes (6): AnnotationPreview, bool, IList, CadAuto.CadAdapter.Preview, IDisposable, IntegerCollection

### Community 84 - "Resolve Dim Style"
Cohesion: 0.33
Nodes (5): DimStyleManager, Database, ObjectId, string, Transaction

### Community 85 - "FeatureRecognizer2D Module"
Cohesion: 0.22
Nodes (6): SlotArcCandidate, SlotLineCandidate, SlotArcCandidate, SlotLineCandidate, CadAuto.Core.Recognition, CadAuto.Core.Tests

### Community 86 - "DimensionExtensionLineRenderer Module"
Cohesion: 0.29
Nodes (5): DimensionExtensionLineRenderer, Database, IList, string, Tuple

### Community 87 - "OutlineArc Module"
Cohesion: 0.21
Nodes (6): OutlineArc, ObjectId, Point2d, SlotFeature, ObjectId, Point2d

### Community 88 - "DimensionExtensionLineRenderer Module"
Cohesion: 0.29
Nodes (5): DimensionExtensionLineRenderer, Database, IList, string, Tuple

### Community 89 - "Top Bottom Left Right Workflow"
Cohesion: 0.29
Nodes (7): Graph Health Check, Graphify Output Artifacts, Regression Completion Gate, Top Bottom Left Right Workflow, Structured Report Validation, Trace and Report Archiving, Visual PNG Review

### Community 90 - "PlacedDim Module"
Cohesion: 0.43
Nodes (7): DeferredDim, PlacedDim, bool, double, int, string, TextBounds

### Community 93 - "Build Hole Callout Plans For"
Cohesion: 0.38
Nodes (4): DimensionRuleConfig, Document, IEnumerable, IList

### Community 94 - "Graphify Query Reference"
Cohesion: 0.33
Nodes (6): Breadth-First Graph Traversal, Constrained Vocabulary Expansion, Depth-First Graph Traversal, Graphify Query Reference, Saved Query Feedback Loop, Graph Work Reflections

### Community 95 - "Annotation Product Invariants"
Cohesion: 0.33
Nodes (6): Annotation Product Invariants, Legacy Dimension and Recognition Rules, Legacy LB Test DLL Flow, Legacy Loose-Hole Global-Boundary Rule, Legacy AUTOFIXDIM Project Rules, Real Geometry Attachment Rules

### Community 96 - "Resolve Annotation Layer"
Cohesion: 0.33
Nodes (4): LayerManager, Database, string, Transaction

### Community 97 - "OutlineSegment Module"
Cohesion: 0.33
Nodes (3): OutlineSegment, ObjectId, Point2d

### Community 98 - "DimensionLayoutRules Module"
Cohesion: 0.33
Nodes (5): ExtensionLineBreakCandidate, IndexedLayoutItem, LayoutBlock, StackingLayerItem, TextSlideCandidate

### Community 99 - "Resolve Annotation Layer"
Cohesion: 0.40
Nodes (4): LayerManager, Database, string, Transaction

### Community 100 - "Graphify Export Reference"
Cohesion: 0.50
Nodes (4): Graphify Export Reference, Graphify MCP Server, Optional Graph Exports, Token Reduction Benchmark

### Community 101 - "GitHub and Merge Reference"
Cohesion: 0.50
Nodes (4): Cross-Repository Graph Merge, GitHub and Merge Reference, GitHub Repository Clone, Monorepo Subgraph Merge

### Community 103 - "OutlineSelection Module"
Cohesion: 0.50
Nodes (3): OutlineSelection, List, ObjectId

### Community 105 - "Test Has Property"
Cohesion: 0.83
Nodes (3): Get-PhysicalRank(), Test-DimensionSelector(), Test-HasProperty()

## Ambiguous Edges - Review These
- `2026-05-28 Loose-Hole Boundary Decision` → `Local Boundary Placement Rules`  [AMBIGUOUS]
  PROJECT_HANDOFF.md · relation: conceptually_related_to

## Knowledge Gaps
- **50 isolated node(s):** `DiameterJigResult`, `DimSide`, `DimensionType`, `HoleKind`, `CornerCalloutJigResult` (+45 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **14 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `2026-05-28 Loose-Hole Boundary Decision` and `Local Boundary Placement Rules`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `OutlineFeature` connect `OutlineFeature Module` to `GeometryCollector Module`, `DimensionDrawer Module`, `Commands Module`, `DimensionDrawer Module`, `DimensionDrawer Module`, `DimensionDrawer Module`, `OutlineSegment Module`, `HoleFeature Module`, `Commands Module`, `DimensionDrawer Module`, `DatumDefinition Module`, `SlotFeature Module`, `DimensionDrawer Module`, `Flush Side`, `DimensionDrawer Module`, `DimensionDrawer Module`, `Point2d Module`, `DimSide Module`, `Point3d Module`, `DimensionDrawer Module`, `DimensionDrawer Module`, `CadToCoreModelMapper Module`, `Recognize Outline`, `HoleDiameterLeaderRenderer Module`, `DimensionDrawer Module`, `OutlineArc Module`, `CadToCoreModelMapper Module`, `Build Hole Callout Plans For`, `From Outline`?**
  _High betweenness centrality (0.255) - this node is a cross-community bridge._
- **Why does `DimensionRuleConfig` connect `DimensionRuleConfig Module` to `Point2D Module`, `OutlineFeature2D Module`, `GeometryCollector Module`, `FeatureRecognizer2D Module`, `StructureSuppressionRules Module`, `DimensionDrawer Module`, `FeatureRecognizer Module`, `FeatureRecognizer Module`, `HoleCalloutPlan Module`, `DimensionLayoutRules Module`, `StructureEndpointRules Module`, `FeatureRecognizer2D Module`, `NativeDiameterDimensioner Module`, `NativeDiameterDimensioner Module`, `DimensionRuleConfig Module`, `DimensionDrawer Module`, `CadEntityWriter Module`, `CornerCalloutRenderer Module`, `CadEntityWriter Module`, `DimensionDeduplicationRules Module`, `CornerCalloutRenderer Module`, `DimensionRuleConfig Module`, `HoleDiameterLeaderRenderer Module`?**
  _High betweenness centrality (0.152) - this node is a cross-community bridge._
- **Why does `OutlineFeature2D` connect `OutlineFeature2D Module` to `Point2D Module`, `HoleFeature2D Module`, `DimensionPlan Module`, `FeatureRecognizer2D Module`, `Assert Module`, `Commands Module`, `StructureSuppressionRules Module`, `Recognize Outline From Segments`, `DimSide Module`, `CadAuto Core Geometry`, `CadToCoreModelMapper Module`, `Segment2D Module`, `DimensionSide Module`, `FeatureRecognizer2D Module`, `StackingLayerItem Module`, `StructureEndpointRules Module`, `CadToCoreModelMapper Module`?**
  _High betweenness centrality (0.110) - this node is a cross-community bridge._
- **What connects `DiameterJigResult`, `DimSide`, `DimensionType` to the rest of the system?**
  _50 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Point2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.04974528019178903 - nodes in this community are weakly interconnected._
- **Should `OutlineFeature2D Module` be split into smaller, more focused modules?**
  _Cohesion score 0.05745707552463248 - nodes in this community are weakly interconnected._