using System;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Recognition;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        // Real selected outline from run 20261030100649806, translated without changing its geometry.
        private static OutlineFeature2D CreateSplitBoundaryFixture(double split = 0.80851896)
        {
            var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 58.999978264, MaxY = 50.000002549 };
            var a = new Point2D(57.999978273, 50.000002549);
            var b = new Point2D(58.999978264, 49.000002646);
            var join = new Point2D(a.X + split, a.Y - split);
            AddSegment(o, a, join, "685B95");
            AddSegment(o, join, b, "685B96");
            AddSegment(o, b, new Point2D(58.999978106, 31.165259549), "685B93");
            AddSegment(o, new Point2D(58.999978106, 31.165259549), new Point2D(58.999978105, 31.000002646), "685B92");
            AddSegment(o, new Point2D(58.999978105, 31.000002646), new Point2D(57.999978096, 30.000002549), "685B94");
            AddSegment(o, new Point2D(57.999978096, 30.000002549), new Point2D(23.000000879, 30.000001011), "685B8A");
            AddSegment(o, new Point2D(20.000001011, 27.000000879), new Point2D(20.000002066, 3.000000879), "685B7D");
            AddSegment(o, new Point2D(17.000002198, 0.000000747), new Point2D(0.000002198, 0), "685B81");
            AddSegment(o, new Point2D(0.000002198, 0), new Point2D(0, 50), "685B80");
            AddSegment(o, new Point2D(0, 50), a, "685B8B");
            AddSegment(o, new Point2D(57.999978096, 30.000002549), new Point2D(57.999978106, 31.165259558), "685B8C");
            AddSegment(o, new Point2D(57.999978106, 31.165259558), new Point2D(57.999978265, 49.191483665), "685B8D");
            AddSegment(o, new Point2D(57.999978265, 49.191483665), a, "685B8E");
            o.Arcs.Add(new Arc2D { Start = new Point2D(17.000002198, 0.000000747), End = new Point2D(20.000002066, 3.000000879),
                Center = new Point2D(17.000002066, 3.000000747), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "685B7E" });
            o.Arcs.Add(new Arc2D { Start = new Point2D(23.000000879, 30.000001011), End = new Point2D(20.000001011, 27.000000879),
                Center = new Point2D(23.000001011, 27.000001011), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "685B7F" });
            return o;
        }

        private static void SplitBoundaryChamferUsesContinuousRealEdges()
        {
            var config = DimensionRuleConfig.CreateDefault();
            foreach (double split in new[] { 0.3, 0.80851896, 0.95 })
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                var o = TransformCapturedSelection(CreateSplitBoundaryFixture(split), angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                var a = TransformCapturedSelectionPoint(new Point2D(57.999978273, 50.000002549), angle, mirror);
                var b = TransformCapturedSelectionPoint(new Point2D(58.999978264, 49.000002646), angle, mirror);
                Assert(o.Chamfers.Count(c => (c.StartPoint.DistanceTo(a) <= 0.001 && c.EndPoint.DistanceTo(b) <= 0.001)
                    || (c.StartPoint.DistanceTo(b) <= 0.001 && c.EndPoint.DistanceTo(a) <= 0.001)) == 1,
                    "continuous verified exterior fragments must define one complete chamfer; split=" + split + " angle=" + angle + " mirror=" + mirror);
                Assert(o.Chamfers.Count == 2, "the two real chamfer locations must stay distinct");
            }
        }

        private static void SplitBoundaryPlanUsesFeatureSpansAndFreeSide()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                var o = TransformCapturedSelection(CreateSplitBoundaryFixture(), angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                var plan = new DimensionPlanner(config).CreateOutlinePlan(o);
                Assert(plan.Diagnostics.OutlineTopology.Status == "UniqueExteriorWalk", "fixture needs real exterior topology");
                Assert(!plan.Dimensions.Any(d => d.DebugRole == "BoundaryObliqueProjection"
                    && d.SourceGeometryIds.Any(id => id == "685B95" || id == "685B96")),
                    "recognized continuous chamfer fragments are not independent body slopes");
                Assert(!plan.Dimensions.Any(d => d.SourceGeometryIds.Contains("685B92") && Math.Abs(GetSpan(d) - 0.1652569) < 0.001),
                    "a degree-two collinear join cannot become a separate feature dimension");
                Assert(plan.Dimensions.Count(d => d.Role == DimensionCandidateRole.Overall) == 2,
                    "both overall dimensions remain");
                foreach (string source in new[] { "685B81", "685B93" })
                {
                    var evidence = new StructureBoundaryEvidence(plan.Diagnostics.OutlineTopology, config.GeometryTolerance);
                    bool horizontal = (source == "685B81") == (angle % 180 == 0);
                    Assert(plan.Dimensions.Any(d => d.Role == DimensionCandidateRole.Structure && d.SourceGeometryIds.Contains(source)
                        && d.Orientation == (horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical)
                        && Math.Abs(GetSpan(d) - 20) < 0.001 && d.AttachmentValidity == true
                        && evidence.ValidateBoundaryAttachments(d.FirstPoint, d.SecondPoint) == true),
                        "the full dimension must survive planning with real boundary endpoints; source=" + source + " angle=" + angle + " mirror=" + mirror);
                }
                if (angle == 0 && !mirror)
                {
                    var ledge = plan.Dimensions.FirstOrDefault(d => d.SourceGeometryIds.Contains("685B8A") && Math.Abs(GetSpan(d) - 35) < 0.001);
                    if (ledge != null) Assert(ledge.Side == DimensionSide.Bottom && !ledge.TopologyEvidence.Contains("ExtensionOverlapUnresolved"),
                        "the real ledge uses the exterior side rather than crossing the solid");
                }
            }
        }

        private static void SplitBoundaryChamferKeepsOtherSegmentsOfSamePolyline()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var o = CreateSplitBoundaryFixture();
            foreach (var segment in o.Segments.Where(s => s.SourceKey == "685B95" || s.SourceKey == "685B96" || s.SourceKey == "685B94"))
                segment.SourceKey = "shared-polyline";
            new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
            var split = o.Chamfers.Single(c => c.SourceGeometryIds.Contains("shared-polyline"));
            var other = o.Segments.Single(s => s.SourceKey == "shared-polyline" && s.Start.Y < 40);
            Assert(o.Chamfers.Count == 2 && !SplitBoundaryChamferRules.ContainsSegment(split, other, config.GeometryTolerance),
                "another chamfer on the same polyline remains distinct from the split chamfer");
        }

        private static void SplitBoundaryExpandedEndpointsRequireRealCornerPath()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                var o = TransformCapturedSelection(CreateSplitBoundaryFixture(), angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                var graph = DimensionPlanner.InspectOutlineTopology(o, config.GeometryTolerance);
                var evidence = new StructureBoundaryEvidence(graph, config.GeometryTolerance);
                var features = new StructureFeatureExtractor(config).Extract(o, evidence);
                foreach (string source in new[] { "685B81", "685B93" })
                {
                    var f = features.Single(x => x.SourceKey != null && x.SourceKey.EndsWith("+Expanded") && x.SupportSourceKeys.Contains(source));
                    Assert(f.BoundaryAttachmentValidity == true && Math.Abs(f.Span - 20) < 0.001
                        && evidence.ValidateBoundaryAttachments(f.FirstPoint, f.SecondPoint) == true
                        && f.BoundaryEvidence.Contains("ExpandedAcrossRecognizedCorner") && f.SupportSourceKeys.Count > 1,
                        "a fillet/chamfer expansion needs a unique connected path and real endpoints; source=" + source + " angle=" + angle + " mirror=" + mirror);
                }
                o.Fillets.Clear();
                var unproven = new StructureFeatureExtractor(config).Extract(o, evidence).Single(x => x.SourceKey == "685B81+Expanded");
                Assert(unproven.BoundaryAttachmentValidity == false && !unproven.Keep,
                    "a virtual corner cannot be restored without the recognized real fillet");
            }
        }

        private static void SplitBoundaryBodySlopeKeepsFullOwnedProjections()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                var original = CreateObliqueBoundaryInformationFixture();
                var slope = original.Segments.Single(s => s.SourceKey == "B87");
                var join = new Point2D(slope.Start.X * 0.63 + slope.End.X * 0.37, slope.Start.Y * 0.63 + slope.End.Y * 0.37);
                original.Segments.Remove(slope);
                AddSegment(original, slope.Start, join, "B87a");
                AddSegment(original, join, slope.End, "B87b");
                var o = TransformCapturedSelection(original, angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                var plan = new DimensionPlanner(config).CreateOutlinePlan(o);
                var projections = plan.Dimensions.Where(d => d.DebugRole == "BoundaryObliqueProjection"
                    && (d.SourceGeometryIds.Contains("B87a") || d.SourceGeometryIds.Contains("B87b"))).ToList();
                Assert(projections.Count == 2 && projections.All(d => d.SourceGeometryIds.Contains("B87a") && d.SourceGeometryIds.Contains("B87b")
                    && d.AttachmentValidity == true) && projections.Select(GetSpan).OrderBy(x => x)
                    .Zip(new[] { 15.0, 25.0 }, (a, b) => Math.Abs(a - b) < 0.001).All(x => x),
                    "a continuous real body slope retains both full, source-owned projections; angle=" + angle + " mirror=" + mirror);
            }
        }

        private static void SplitBoundaryAxisEdgeKeepsSameInformation()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                var original = CreateObliqueBoundaryInformationFixture();
                var edge = original.Segments.Single(s => s.SourceKey == "B8E");
                var join = new Point2D(edge.Start.X * 0.63 + edge.End.X * 0.37, edge.Start.Y * 0.63 + edge.End.Y * 0.37);
                original.Segments.Remove(edge);
                AddSegment(original, edge.Start, join, "B8Ea"); AddSegment(original, join, edge.End, "B8Eb");
                var o = TransformCapturedSelection(original, angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                var plan = new DimensionPlanner(config).CreateOutlinePlan(o);
                var expected = new[] { 15.0, 20, 20, 25, 50, 62.022, 65, 89 }.OrderBy(v => v).ToList();
                var values = plan.Dimensions.Select(GetSpan).OrderBy(v => v).ToList();
                Assert(values.Count == expected.Count && values.Zip(expected, (a, b) => Math.Abs(a - b) < 0.001).All(x => x),
                    "a split straight boundary must preserve the same proven measurements, angle=" + angle + " mirror=" + mirror
                        + " values=" + string.Join(",", values));
            }
        }

        private static void SplitBoundaryRejectsForeignRealAnchor()
        {
            var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 30, MaxY = 20 };
            AddSegment(o, new Point2D(0, 0), new Point2D(9, 0), "seed");
            AddSegment(o, new Point2D(10, 1), new Point2D(10, 10), "side");
            AddSegment(o, new Point2D(10, 10), new Point2D(20, 10), "notch");
            AddSegment(o, new Point2D(20, 10), new Point2D(20, 0), "other-side");
            AddSegment(o, new Point2D(20, 0), new Point2D(30, 0), "other-foot");
            AddSegment(o, new Point2D(30, 0), new Point2D(30, 20), "right");
            AddSegment(o, new Point2D(30, 20), new Point2D(0, 20), "top");
            AddSegment(o, new Point2D(0, 20), new Point2D(0, 0), "left");
            o.Arcs.Add(new Arc2D { Start = new Point2D(9, 0), End = new Point2D(10, 1), Center = new Point2D(9, 1),
                Radius = 1, Bulge = Math.Tan(Math.PI / 8), SourceKey = "arc" });
            var config = DimensionRuleConfig.CreateDefault();
            new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
            var graph = DimensionPlanner.InspectOutlineTopology(o, config.GeometryTolerance);
            var evidence = new StructureBoundaryEvidence(graph, config.GeometryTolerance);
            var feature = new StructureFeature { Axis = StructureFeatureAxis.Horizontal, Kind = StructureFeatureKind.RealStep,
                CrossPosition = 0, T0 = 10, T1 = 20, FirstPoint = new Point2D(20, 0), SecondPoint = new Point2D(10, 0),
                SourceKey = "seed+Expanded", BoundaryMembership = "Boundary", RequiresOuterBoundarySupport = true };
            feature.SupportSourceKeys.Add("seed");
            Assert(graph.Status == "UniqueExteriorWalk" && o.Fillets.Count == 1
                && evidence.ValidateBoundaryAttachments(feature.FirstPoint, feature.FirstPoint) == true
                && evidence.ValidateBoundaryAttachments(feature.SecondPoint, feature.SecondPoint) == false,
                "the foreign anchor is real, and the other endpoint needs a real fillet connection");
            evidence.ApplyAttachmentEvidence(new[] { feature }, o);
            Assert(feature.BoundaryAttachmentValidity == false && !(feature.BoundaryEvidence ?? "").Contains("ExpandedAcrossRecognizedCorner"),
                "a real endpoint on another disconnected face cannot be adopted by this source edge");
        }

        private static void SplitBoundaryDoesNotMergeBranchesOrRealTurns()
        {
            var config = DimensionRuleConfig.CreateDefault();
            foreach (bool branch in new[] { false, true })
            {
                var o = CreateSplitBoundaryFixture();
                var left = o.Segments.Single(s => s.SourceKey == "685B95");
                var right = o.Segments.Single(s => s.SourceKey == "685B96");
                if (branch) AddSegment(o, left.End, new Point2D(left.End.X - 0.3, left.End.Y - 0.3), "independent-branch");
                else { left.End = new Point2D(left.End.X, left.End.Y - 0.02); right.Start = left.End; }
                var graph = DimensionPlanner.InspectOutlineTopology(o, config.GeometryTolerance);
                Assert(graph.Status == "UniqueExteriorWalk", "the fixture must still have a unique exterior");
                if (branch) Assert(graph.Nodes.Any(n => n.Degree > 2 && n.Point.DistanceTo(left.End) < config.GeometryTolerance),
                    "the branch must be present at the original joining point");
                Assert(!BoundaryLineRuns.Find(graph, config.GeometryTolerance).Any(r =>
                    r.Edges.Any(e => e.SourceKey == "685B95") && r.Edges.Any(e => e.SourceKey == "685B96")),
                    "an independent branch or genuine turn must not be merged as an incidental join");
            }
        }
    }
}
