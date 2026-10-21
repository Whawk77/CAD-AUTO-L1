using System;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        private static void CompositeOutlineNormalizesExistingEndpointsOnly()
        {
            var outline = CreateCompositeOutlineFixture(47, 25, 5);
            var originals = outline.Segments.Select(s => Tuple.Create(s, s.Start, s.End)).ToList();
            var graph = DimensionPlanner.InspectOutlineTopology(outline, 0.001);
            Assert(graph.Status == "UniqueExteriorWalk" && graph.IsClosedWalk && !graph.HasRepeatedBoundaryEdges,
                "overlapping shared edges must produce one simple exterior walk");
            Assert(originals.All(s => outline.Segments.Contains(s.Item1) && s.Item1.Start.Equals(s.Item2) && s.Item1.End.Equals(s.Item3)),
                "analysis must preserve all source entities and their endpoints");
            Assert(graph.Edges.Any(e => e.SourceKeys.Contains("arm-top") && e.SourceKeys.Contains("tower-bottom")),
                "deduplicated seam must retain both source identities");
            var ownership = new StructureBoundaryEvidence(graph, 0.001);
            Assert(ownership.GetMembership(outline.Segments.Single(s => s.SourceKey == "arm-top")) == "NonBoundary",
                "a long edge partly inside the joined feature cannot be treated as wholly exterior");
            Assert(ownership.GetMembership(outline.Segments.Single(s => s.SourceKey == "foot-seam")) == "NonBoundary",
                "an internal cross seam cannot define an outer step");
            Assert(ownership.GetMembership(new Segment2D(new Point2D(0, 0), new Point2D(0, 29))) == "Boundary",
                "adjacent exterior pieces must cover a continuous source interval");
            Assert(ownership.GetMembership(new Segment2D(new Point2D(0, -1), new Point2D(0, 29))) == "Unknown",
                "partial coverage cannot certify a source interval with a missing end");
            Assert(graph.Nodes.All(n => originals.Any(s => s.Item2.DistanceTo(n.Point) < 0.001 || s.Item3.DistanceTo(n.Point) < 0.001)
                || outline.Arcs.Any(a => a.Start.DistanceTo(n.Point) < 0.001 || a.End.DistanceTo(n.Point) < 0.001)),
                "normalization must not invent intersection nodes");
        }

        private static void CompositeOutlineKeepsFeatureWidthDropsSeamFragments()
        {
            foreach (int variant in new[] { 0, 1 })
            foreach (double rotation in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                double width = variant == 0 ? 47 : 53, towerWidth = variant == 0 ? 25 : 23, seamHeight = variant == 0 ? 5 : 7;
                var outline = TransformLProfileFixture(CreateCompositeOutlineFixture(width, towerWidth, seamHeight), rotation, mirror);
                var arc = outline.Arcs.Single(a => Math.Abs(a.Radius - 5) < 0.001);
                outline.Fillets.Add(new FilletFeature2D { StartPoint = arc.Start, EndPoint = arc.End, Center = arc.Center, Radius = arc.Radius });
                var config = DimensionRuleConfig.CreateDefault();
                config.UseFeatureFirstStructurePipeline = true;
                var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);
                var tower = outline.Segments.Single(s => s.SourceKey == "tower-top");
                var seam = outline.Segments.Single(s => s.SourceKey == "left-foot");
                var tangent = outline.Segments.Single(s => s.SourceKey == "arm-top");
                string context = " rotation=" + rotation + " mirror=" + mirror + " variant=" + variant
                    + " final=" + string.Join(",", plan.Dimensions.Select(d => d.DebugRole + ":" + GetSpan(d).ToString("0.###") + ":" + d.SourceKey))
                    + " candidates=" + string.Join("\n", plan.Diagnostics.DimensionCandidates.Select(d => d.Id + ":" + d.DebugRole + ":" + d.Value + ":" + string.Join(",", d.SourceGeometryIds) + ":" + d.DecisionReason + ":" + d.SuppressedReason))
                    + " features=" + string.Join("\n", plan.AnnotationCaseSnapshot.Features.Select(f => f.SourceKey + ":" + f.Span + ":" + f.Keep + ":" + f.SuppressReason + ":" + f.CrossPosition));
                Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.Normal && CompositeIntervalMatches(d, tower)),
                    "independent tower width must survive envelope cleanup" + context);
                Assert(!plan.Dimensions.Any(d => d.Kind == DimensionKind.Normal && CompositeIntervalMatches(d, tangent)),
                    "tangent-to-envelope span crossing an internal seam must not return" + context);
                Assert(!plan.Dimensions.Any(d => d.Kind == DimensionKind.Normal && CompositeIntervalMatches(d, seam)),
                    "continuous exterior side must not emit the internal seam height" + context);
                var foot = outline.Segments.Single(s => s.SourceKey == "foot-bottom");
                Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.Normal && CompositeIntervalMatches(d, foot)),
                    "orthogonal real foot width must not be removed with the seam height" + context);
                if (rotation == 0 && !mirror)
                {
                    var height = outline.Segments.Single(s => s.SourceKey == "tower-left");
                    Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.Normal && CompositeIntervalMatches(d, height)
                        && (d.FirstPoint.Equals(tangent.End) || d.SecondPoint.Equals(tangent.End))),
                        "tower height must keep its measurement and attach to the clear end of the real ledge" + context);
                }
            }
        }

        private static void CompositeOutlineUsesClearSelectedHoleAxis()
        {
            var outline = CreateCompositeOutlineFixture(47, 25, 5);
            var datum = Datum2D.FromOutline(outline);
            datum.HoleCenterlineEndpoints.Add(new CenterlineEndpoint2D { Point = new Point2D(34.5, 46.313), SourceGeometryId = "hole-stub" });
            datum.HoleCenterlineEndpoints.Add(new CenterlineEndpoint2D { Point = new Point2D(34.5, 51.687), SourceGeometryId = "hole-stub" });
            datum.HoleCenterlineEndpoints.Add(new CenterlineEndpoint2D { Point = new Point2D(34.5, 31.5), SourceGeometryId = "long-axis" });
            datum.HoleCenterlineEndpoints.Add(new CenterlineEndpoint2D { Point = new Point2D(34.5, 61.5), SourceGeometryId = "long-axis" });
            var config = DimensionRuleConfig.CreateDefault();
            config.UseFeatureFirstStructurePipeline = true;
            var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum,
                new[] { CreateHole(34.5, 49, 4.2, HoleKind2D.Thread) });
            var location = plan.Dimensions.Single(d => d.DebugRole == "HoleDatumX");
            Assert(location.Side == DimensionSide.Top && Math.Abs(GetSpan(location) - 34.5) < 0.001
                && location.SecondPoint.Equals(new Point2D(34.5, 61.5))
                && location.SourceGeometryIds.Contains("long-axis")
                && location.TopologyEvidence.Contains("ClearOverlappingSelectedAxis"),
                "hole position must keep its axis and use the selected endpoint beyond the obstructing contour; actual="
                + location.Side + ":" + location.SecondPoint + ":" + location.TopologyEvidence);
        }

        private static bool CompositeIntervalMatches(PlannedDimension d, Segment2D segment)
        {
            bool horizontal = segment.LengthX > segment.LengthY;
            if (d.Orientation != (horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical)) { return false; }
            double a = horizontal ? d.FirstPoint.X : d.FirstPoint.Y, b = horizontal ? d.SecondPoint.X : d.SecondPoint.Y;
            double c = horizontal ? segment.MinX : segment.MinY, e = horizontal ? segment.MaxX : segment.MaxY;
            return Math.Abs(Math.Min(a, b) - c) < 0.001 && Math.Abs(Math.Max(a, b) - e) < 0.001;
        }

        private static void CompositeOutlineTangentSuppressionProtectsIndependentIntent()
        {
            foreach (string variant in new[] { "plain", "tolerance", "rule", "noCornerEvidence", "unknown", "unidentifiedSeam" })
            {
                var outline = CreateCompositeOutlineFixture(47, 25, 5);
                if (variant == "unidentifiedSeam") { outline.Segments.Single(s => s.SourceKey == "arm-top").SourceKey = null; }
                var arc = outline.Arcs[0];
                if (variant != "noCornerEvidence")
                {
                    outline.Fillets.Add(new FilletFeature2D { StartPoint = arc.Start, EndPoint = arc.End, Center = arc.Center, Radius = arc.Radius });
                }
                var edge = outline.Segments.Single(s => s.SourceKey == "arm-bottom");
                var candidate = new PlannedDimension { Kind = DimensionKind.Normal, Orientation = DimensionOrientation.Horizontal,
                    Side = DimensionSide.Bottom, Role = DimensionCandidateRole.Structure, OwnerKind = DimensionCandidateOwnerKind.Outline,
                    DebugRole = "BottomStructWidth", SourceKey = edge.SourceKey, FirstPoint = edge.Start, SecondPoint = edge.End,
                    RequiresOuterBoundarySupport = true, AttachmentValidity = true };
                candidate.SourceGeometryIds.Add(edge.SourceKey);
                if (variant == "tolerance") { candidate.OverrideText = "37±0.02"; }
                if (variant == "rule") { candidate.RuleId = "IndependentFeatureRequirement"; }
                var plan = new DimensionPlan();
                plan.Diagnostics.OutlineTopology = DimensionPlanner.InspectOutlineTopology(outline, 0.001);
                if (variant == "unknown") { plan.Diagnostics.OutlineTopology.Status = "Unavailable"; }
                plan.Add(candidate);
                typeof(DimensionPlanner).GetMethod("SuppressTangentSpansAcrossJoinedSeams",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(new DimensionPlanner(DimensionRuleConfig.CreateDefault()), new object[] { plan, outline });
                Assert(plan.Dimensions.Contains(candidate) == (variant != "plain"),
                    "tangent-span cleanup must preserve independent text/rules and insufficient evidence: " + variant);
            }
        }

        private static OutlineFeature2D CreateCompositeOutlineFixture(double width, double towerWidth, double seamHeight)
        {
            var outline = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = width, MaxY = 59 };
            AddSegment(outline, new Point2D(5, 0), new Point2D(5, seamHeight), "right-foot");
            AddSegment(outline, new Point2D(5, 0), new Point2D(0, 0), "foot-bottom");
            AddSegment(outline, new Point2D(5, seamHeight), new Point2D(5, 29), "inner-leg");
            AddSegment(outline, new Point2D(5, seamHeight), new Point2D(0, seamHeight), "foot-seam");
            AddSegment(outline, new Point2D(0, 0), new Point2D(0, seamHeight), "left-foot");
            AddSegment(outline, new Point2D(0, 29), new Point2D(0, seamHeight), "outer-leg");
            AddSegment(outline, new Point2D(width, 34), new Point2D(10, 34), "arm-bottom");
            AddSegment(outline, new Point2D(width, 39), new Point2D(width, 34), "arm-right");
            AddSegment(outline, new Point2D(width, 39), new Point2D(10, 39), "arm-top");
            AddSegment(outline, new Point2D(width - towerWidth, 39), new Point2D(width, 39), "tower-bottom");
            AddSegment(outline, new Point2D(width, 59), new Point2D(width, 39), "tower-right");
            AddSegment(outline, new Point2D(width - towerWidth, 59), new Point2D(width - towerWidth, 39), "tower-left");
            AddSegment(outline, new Point2D(width - towerWidth, 59), new Point2D(width, 59), "tower-top");
            outline.Arcs.Add(new Arc2D { Start = new Point2D(10, 34), End = new Point2D(5, 29), Center = new Point2D(10, 29), Radius = 5, Bulge = Math.Tan(Math.PI / 8), SourceKey = "inner-arc" });
            outline.Arcs.Add(new Arc2D { Start = new Point2D(10, 39), End = new Point2D(0, 29), Center = new Point2D(10, 29), Radius = 10, Bulge = Math.Tan(Math.PI / 8), SourceKey = "outer-arc" });
            return outline;
        }
    }
}
