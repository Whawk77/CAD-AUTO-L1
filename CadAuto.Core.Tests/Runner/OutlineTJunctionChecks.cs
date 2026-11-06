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
        private static void OutlineTJunctionCapturedEndpointsConnectToRealLine()
        {
            var outline = CreateCapturedTJunctionOutline();
            var bar = outline.Segments.Single(s => s.SourceKey == "6850E4");
            foreach (string key in new[] { "6850E3", "6850DA" })
            {
                var endpoint = outline.Segments.Single(s => s.SourceKey == key).Start;
                Assert(endpoint.DistanceTo(bar.Start) > 0.01 && endpoint.DistanceTo(bar.End) > 0.01,
                    "captured neck endpoints do not match either bar endpoint");
                Assert(OutlineGeometryQuery.IsPointOnSegment(endpoint, bar, 0.01),
                    "captured T-junction must connect to the finite real line interior");
                Assert(OutlineGeometryQuery.IsPointOnSegment(endpoint, bar, 0.001),
                    "captured contact also satisfies the existing planner geometry tolerance");
            }
        }

        private static void OutlineTJunctionRejectsGapsExtensionsAndCrossings()
        {
            var bar = new Segment2D(new Point2D(0, 0), new Point2D(20, 0));
            foreach (var point in new[] { new Point2D(-1, 0), new Point2D(21, 0), new Point2D(10, 0.02) })
                Assert(!OutlineGeometryQuery.IsPointOnSegment(point, bar, 0.01),
                    "a line extension or gap beyond the existing tolerance cannot join components");
            Assert(OutlineGeometryQuery.IsPointOnSegment(new Point2D(10, 0.009), bar, 0.01),
                "the existing 0.01 connection tolerance is preserved");
            var crossing = new Segment2D(new Point2D(10, -1), new Point2D(10, 1));
            Assert(!OutlineGeometryQuery.IsPointOnSegment(crossing.Start, bar, 0.01)
                && !OutlineGeometryQuery.IsPointOnSegment(crossing.End, bar, 0.01)
                && !OutlineGeometryQuery.IsPointOnSegment(bar.Start, crossing, 0.01)
                && !OutlineGeometryQuery.IsPointOnSegment(bar.End, crossing, 0.01),
                "a crossing without an existing endpoint cannot invent a connection node");
        }

        private static void OutlineTJunctionCapturedPartKeepsCompleteExterior()
        {
            foreach (double rotation in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            foreach (bool reverse in new[] { false, true })
            {
                var outline = TransformLProfileFixture(CreateCapturedTJunctionOutline(), rotation, mirror);
                if (reverse) outline.Segments.Reverse();
                var originals = outline.Segments.Select(s => Tuple.Create(s, s.Start, s.End)).ToList();
                var graph = DimensionPlanner.InspectOutlineTopology(outline, 0.001);
                string context = " rotation=" + rotation + " mirror=" + mirror + " reverse=" + reverse;
                Assert(graph.Status == "UniqueExteriorWalk" && graph.IsClosedWalk && !graph.HasRepeatedBoundaryEdges,
                    "captured part must have one complete simple exterior" + context);
                var seam = graph.Edges.Where(e => e.SourceKeys.Contains("6850E4")).ToList();
                Assert(seam.Count == 3 && seam.Count(e => e.IsBoundary) == 2,
                    "T-junctions split the bar into two exterior shoulders and one internal seam" + context);
                Assert(seam.Where(e => e.IsBoundary).All(e => Math.Abs(e.Start.DistanceTo(e.End) - 1) < 0.001)
                    && Math.Abs(seam.Single(e => !e.IsBoundary).Start.DistanceTo(seam.Single(e => !e.IsBoundary).End) - 18) < 0.001,
                    "real one-unit shoulders survive while the interior eighteen-unit seam stays internal" + context);
                Assert(graph.Nodes.All(n => originals.Any(s => s.Item2.DistanceTo(n.Point) < 0.001 || s.Item3.DistanceTo(n.Point) < 0.001)),
                    "exterior analysis must use existing source endpoints only" + context);
                Assert(originals.All(s => s.Item1.Start.Equals(s.Item2) && s.Item1.End.Equals(s.Item3)),
                    "source geometry and endpoints remain unchanged" + context);
                double width = rotation % 180 == 0 ? 20 : 95.000006571632,
                    height = rotation % 180 == 0 ? 95.000006571632 : 20;
                var plan = new DimensionPlanner(DimensionRuleConfig.CreateDefault()).CreateOutlinePlan(outline);
                Assert(Math.Abs(GetSpan(plan.Dimensions.Single(d => d.DebugRole == "OverallWidth")) - width) < 0.001
                    && Math.Abs(GetSpan(plan.Dimensions.Single(d => d.DebugRole == "OverallHeight")) - height) < 0.001,
                    "overall dimensions must cover the complete captured part rather than the lower rectangle" + context);
            }
        }

        private static OutlineFeature2D CreateCapturedTJunctionOutline()
        {
            // Captured source DWG SHA256 8759E902B751F7873E97544909F61990D58B0159B5DC7602762065364FA808A5.
            var outline = new OutlineFeature2D();
            AddSegment(outline, new Point2D(103891.34109435845, 8643.8111739031028), new Point2D(103891.34109435845, 8668.8111739031028), "6850D8");
            AddSegment(outline, new Point2D(103890.34109435845, 8668.8111796503326), new Point2D(103890.34109435845, 8678.8111805149056), "6850DA");
            AddSegment(outline, new Point2D(103891.34109435845, 8678.8111805149056), new Point2D(103890.34109435845, 8678.8111805149056), "6850DC");
            AddSegment(outline, new Point2D(103872.34109435845, 8678.8111805149056), new Point2D(103871.34109435845, 8678.8111805149056), "6850DF");
            AddSegment(outline, new Point2D(103871.34109435845, 8681.0619194346436), new Point2D(103871.34109435845, 8678.8111805149056), "6850E0");
            AddSegment(outline, new Point2D(103871.34109435845, 8681.0619194346436), new Point2D(103891.34109435845, 8681.0619194346436), "6850E1");
            AddSegment(outline, new Point2D(103891.34109435845, 8681.0619194346436), new Point2D(103891.34109435845, 8678.8111805149056), "6850E2");
            AddSegment(outline, new Point2D(103872.34109435845, 8668.8111796503326), new Point2D(103872.34109435845, 8678.8111805149056), "6850E3");
            AddSegment(outline, new Point2D(103871.34109435845, 8668.8111739031028), new Point2D(103891.34109435845, 8668.8111739031028), "6850E4");
            AddSegment(outline, new Point2D(103871.34109435845, 8643.8111734154318), new Point2D(103891.34109435845, 8643.8111734154318), "6850E5");
            AddSegment(outline, new Point2D(103871.34109435845, 8643.8111739031028), new Point2D(103871.34109435845, 8668.8111739031028), "6850E6");
            AddSegment(outline, new Point2D(103891.34109435845, 8681.0619194346436), new Point2D(103891.34109435845, 8733.811179987064), "6850E8");
            AddSegment(outline, new Point2D(103891.34109435845, 8738.811179987064), new Point2D(103891.34109435845, 8733.811179987064), "6850E9");
            AddSegment(outline, new Point2D(103871.34109435845, 8681.0619194346436), new Point2D(103871.34109435845, 8733.811179987064), "6850EA");
            AddSegment(outline, new Point2D(103871.34109435845, 8738.811179987064), new Point2D(103871.34109435845, 8733.811179987064), "6850EB");
            AddSegment(outline, new Point2D(103871.34109435845, 8738.811179987064), new Point2D(103891.34109435845, 8738.811179987064), "6850EC");
            AddSegment(outline, new Point2D(103871.34109435845, 8733.811179987064), new Point2D(103891.34109435845, 8733.811179987064), "6850ED");
            outline.MinX = outline.Segments.Min(s => s.MinX);
            outline.MinY = outline.Segments.Min(s => s.MinY);
            outline.MaxX = outline.Segments.Max(s => s.MaxX);
            outline.MaxY = outline.Segments.Max(s => s.MaxY);
            return outline;
        }
    }
}
