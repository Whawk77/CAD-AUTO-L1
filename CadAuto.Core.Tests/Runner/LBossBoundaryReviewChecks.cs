using System;
using System.Linq;
using System.Reflection;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
	internal static partial class Program
	{
		// Source geometry from run 20261022124708636; dimensions are checked by the user-confirmed yellow path.
		private static OutlineFeature2D LBossBoundaryOutline()
		{
			var outline = new OutlineFeature2D();
			AddSegment(outline, new Point2D(145321.32929728058, -17274.441690090658), new Point2D(145321.3292972578, -17264.441690164553), "boss-step");
			AddSegment(outline, new Point2D(145321.32929728058, -17274.441690090658), new Point2D(145293.32929728058, -17274.441690154497), "outer-shelf");
			AddSegment(outline, new Point2D(145321.32929726466, -17264.441690169144), new Point2D(145296.32929728748, -17264.44169022615), "yellow-tread");
			AddSegment(outline, new Point2D(145321.32929723046, -17249.44169009525), new Point2D(145321.32929726466, -17264.441690169144), "boss-upper-wall");
			AddSegment(outline, new Point2D(145288.32929723046, -17249.44169017049), new Point2D(145321.32929723046, -17249.44169009525), "boss-outer-face");
			AddSegment(outline, new Point2D(145291.32929731123, -17274.859114459665), new Point2D(145291.32929729886, -17269.441690237545), "yellow-wall");
			AddSegment(outline, new Point2D(145288.32929709594, -17190.441690336826), new Point2D(145288.32929723046, -17249.44169017049), "stem-upper");
			AddSegment(outline, new Point2D(145288.3292972989, -17279.44169017049), new Point2D(145288.32929732167, -17289.441690336826), "stem-lower");
			AddSegment(outline, new Point2D(145298.32929707316, -17180.441690314023), new Point2D(145288.32929709594, -17190.441690336826), "chamfer-inner");
			outline.Chamfers.Add(new ChamferFeature2D { StartPoint = new Point2D(145298.32929707316, -17180.441690314023), EndPoint = new Point2D(145288.32929709594, -17190.441690336826), Value = 9.9999999772117, DeltaX = 9.9999999772117, DeltaY = 10.000000022802851 });
			AddSegment(outline, new Point2D(145288.32929732167, -17289.441690336826), new Point2D(145263.3292973217, -17289.441690393818), "foot");
			AddSegment(outline, new Point2D(145520.32929703643, -17180.441689807885), new Point2D(145298.32929707316, -17180.441690314023), "arm-bottom");
			AddSegment(outline, new Point2D(145263.32929707316, -17180.441690393825), new Point2D(145263.32929732167, -17289.441690393818), "left");
			AddSegment(outline, new Point2D(145520.32929704137, -17180.441689807885), new Point2D(145520.3292969844, -17155.441689807885), "arm-tip");
			AddSegment(outline, new Point2D(145263.32929707316, -17180.441690393825), new Point2D(145288.32929701614, -17155.441690336826), "chamfer-outer");
			outline.Chamfers.Add(new ChamferFeature2D { StartPoint = new Point2D(145263.32929707316, -17180.441690393825), EndPoint = new Point2D(145288.32929701614, -17155.441690336826), Value = 24.999999942985596, DeltaX = 24.999999942985596, DeltaY = 25.000000056999852 });
			AddSegment(outline, new Point2D(145520.32929697944, -17155.441689807885), new Point2D(145288.32929701614, -17155.441690336826), "top");
			{ var arc = new Arc2D { Start = new Point2D(145293.32929728064, -17274.441690154497), End = new Point2D(145291.3292973044, -17274.85911445413), Center = new Point2D(145293.329297292, -17279.441690154497), Radius = 5, Bulge = 0.10324371591838082, SourceKey = "outer-fillet-a" };
			  outline.Arcs.Add(arc); outline.Fillets.Add(new FilletFeature2D { StartPoint = arc.Start, EndPoint = arc.End, Center = arc.Center, Radius = arc.Radius, SourceArc = arc }); }
			{ var arc = new Arc2D { Start = new Point2D(145296.3292972875, -17264.44169022615), End = new Point2D(145291.32929729886, -17269.441690237538), Center = new Point2D(145296.32929729886, -17269.44169022615), Radius = 5, Bulge = 0.4142135623752261, SourceKey = "yellow-fillet" };
			  outline.Arcs.Add(arc); outline.Fillets.Add(new FilletFeature2D { StartPoint = arc.Start, EndPoint = arc.End, Center = arc.Center, Radius = arc.Radius, SourceArc = arc }); }
			{ var arc = new Arc2D { Start = new Point2D(145291.32929731128, -17274.85911445873), End = new Point2D(145288.3292972989, -17279.441690170483), Center = new Point2D(145293.3292972989, -17279.441690159096), Radius = 5, Bulge = 0.2982166280551727, SourceKey = "outer-fillet-b" };
			  outline.Arcs.Add(arc); outline.Fillets.Add(new FilletFeature2D { StartPoint = arc.Start, EndPoint = arc.End, Center = arc.Center, Radius = arc.Radius, SourceArc = arc }); }
			outline.MinX = 145263.32929707316; outline.MinY = -17289.441690393818; outline.MaxX = 145520.32929704137; outline.MaxY = -17155.441689807885;
			return outline;
		}

        private static void LBossBoundaryYellowWidthAndDerivedFaceInAllDirections()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            var planner = new DimensionPlanner(config);
            foreach (bool mirrored in new[] { false, true })
            foreach (bool reversed in new[] { false, true })
            foreach (double scale in new[] { 1.0, 1.4 })
            for (int quarter = 0; quarter < 4; quarter++)
            {
                var outline = LBossBoundaryOutline();
                var origin = new Point2D(outline.MinX, outline.MinY);
                Func<Point2D, Point2D> transform = p => {
                    var q = TransformPoint(new Point2D((mirrored ? -1 : 1) * (p.X - origin.X) * scale,
                        (p.Y - origin.Y) * scale), quarter * Math.PI / 2);
                    return new Point2D(q.X + 145263.32929707, q.Y - 17289.44169039);
                };
                var source = outline; outline = new OutlineFeature2D();
                foreach (var s in source.Segments) { AddSegment(outline, transform(reversed ? s.End : s.Start), transform(reversed ? s.Start : s.End), s.SourceKey); }
                if (reversed) { outline.Segments.Reverse(); }
                foreach (var a in source.Arcs)
                {
                    var arc = new Arc2D { Start = transform(reversed ? a.End : a.Start), End = transform(reversed ? a.Start : a.End),
                        Center = transform(a.Center), Radius = a.Radius * scale, Bulge = a.Bulge * (mirrored ? -1 : 1) * (reversed ? -1 : 1), SourceKey = a.SourceKey };
                    outline.Arcs.Add(arc); outline.Fillets.Add(new FilletFeature2D { StartPoint = arc.Start, EndPoint = arc.End, Center = arc.Center, Radius = arc.Radius, SourceArc = arc });
                }
                foreach (var c in source.Chamfers) { outline.Chamfers.Add(new ChamferFeature2D { StartPoint = transform(c.StartPoint), EndPoint = transform(c.EndPoint), Value = c.Value * scale, DeltaX = c.DeltaX * scale, DeltaY = c.DeltaY * scale }); }
                outline.MinX = outline.Segments.Min(s => s.MinX); outline.MaxX = outline.Segments.Max(s => s.MaxX);
                outline.MinY = outline.Segments.Min(s => s.MinY); outline.MaxY = outline.Segments.Max(s => s.MaxY);
                var plan = planner.CreateDimensionPlan(outline, Datum2D.FromOutline(outline), new HoleFeature2D[0], new SlotFeature2D[0], CoordinateFrame2D.Identity);
                string context = " quarter=" + quarter + " mirror=" + mirrored + " reverse=" + reversed + " scale=" + scale;
                var width = plan.Dimensions.SingleOrDefault(d => (d.TopologyEvidence ?? "").Contains("Relation=RecoveredInnerBossGroove"));
                Assert(width != null, "retain the width of the connected internal tread-fillet-wall path" + context);
                var innerWall = source.Segments.Single(s => s.SourceKey == "yellow-wall");
                var rightWall = source.Segments.Single(s => s.SourceKey == "boss-step");
                double expected = Math.Abs(innerWall.Start.X - rightWall.Start.X) * scale;
                Assert(Math.Abs(GetSpan(width) - expected) <= config.GeometryTolerance && width.AttachmentValidity == true
                    && new[] { "yellow-tread", "yellow-fillet", "yellow-wall" }.All(s => width.SourceGeometryIds.Contains(s)),
                    "use both real walls, including the full fillet instead of the outer straight remainder" + context);
                Assert(OutlineGeometryQuery.IsPointOnBoundary(width.FirstPoint, outline, config.GeometryTolerance)
                    && OutlineGeometryQuery.IsPointOnBoundary(width.SecondPoint, outline, config.GeometryTolerance), "the yellow width must keep real endpoints" + context);
                var crosses = typeof(DimensionPlanner).GetMethod("ExtensionCrossesOutline", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(!(bool)crosses.Invoke(planner, new object[] { width.FirstPoint, width.Orientation, width.Side, outline })
                    && !(bool)crosses.Invoke(planner, new object[] { width.SecondPoint, width.Orientation, width.Side, outline }), "yellow-width extension lines must clear the contour" + context);
                Assert(!plan.Dimensions.Any(d => d.SourceKey == "boss-outer-face")
                    && plan.Diagnostics.DimensionCandidates.Any(d => d.SourceFeatureId == "boss-outer-face"
                        && d.SuppressedReason == "Structure.RawBossFaceCoveredByEnvelopePosition" && d.TopologyEvidence.Contains("AdjacentIntervalsPartitionBossLocation")),
                    "the outer face must be derived from the live cumulative location and real envelope arm" + context);
                Assert(!plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|")), "coverage providers must remain live" + context);
            }
        }

        private static void LBossBoundaryCoveragePreservesIndependentRequirements()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            var planner = new DimensionPlanner(config);
            var apply = typeof(DimensionPlanner).GetMethod("SuppressRawBossFacesCoveredByEnvelopePosition", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (string variant in new[] { "covered", "text", "hole", "different-source", "missing-location", "invalid-location", "missing-arm", "missing-topology", "missing-return" })
            {
                var outline = LBossBoundaryOutline();
                var plan = planner.CreateDimensionPlan(outline, Datum2D.FromOutline(outline), new HoleFeature2D[0], new SlotFeature2D[0], CoordinateFrame2D.Identity);
                var edge = outline.Segments.Single(s => s.SourceKey == "boss-outer-face");
                var raw = new PlannedDimension { Kind = DimensionKind.Normal, Role = DimensionCandidateRole.OutlineSegment,
                    OwnerKind = DimensionCandidateOwnerKind.Outline, DebugRole = "OutlineSegment", Orientation = DimensionOrientation.Horizontal,
                    Side = DimensionSide.Top, FirstPoint = edge.Start, SecondPoint = edge.End, SourceKey = edge.SourceKey,
                    SourceGeometryIds = new System.Collections.Generic.List<string> { edge.SourceKey } };
                var location = plan.Dimensions.Single(d => (d.TopologyEvidence ?? "").Contains("Relation=RecoveredBossPosition"));
                if (variant == "text") { raw.OverrideText = "33 H7"; }
                if (variant == "hole") { raw.Role = DimensionCandidateRole.Hole; raw.OwnerKind = DimensionCandidateOwnerKind.HoleGroup; }
                if (variant == "different-source") { raw.SourceKey = "independent-33"; raw.SourceGeometryIds.Clear(); raw.SourceGeometryIds.Add(raw.SourceKey); }
                if (variant == "missing-location") { plan.Dimensions.Remove(location); }
                if (variant == "invalid-location") { location.AttachmentValidity = false; }
                if (variant == "missing-arm") { plan.Dimensions.RemoveAll(d => d.Role == DimensionCandidateRole.Structure && d.Orientation == raw.Orientation && Math.Abs(GetSpan(d) - 25) <= config.GeometryTolerance); }
                if (variant == "missing-topology") { plan.Diagnostics.OutlineTopology.Status = "NoExteriorFace"; }
                if (variant == "missing-return") { plan.Diagnostics.OutlineTopology.BoundaryPath.RemoveAll(s => s.EdgeId == plan.Diagnostics.OutlineTopology.Edges.Single(e => e.SourceKey == "outer-fillet-a").Id); }
                plan.Add(raw); apply.Invoke(planner, new object[] { plan, outline });
                Assert(plan.Dimensions.Contains(raw) == (variant != "covered"), "only suppress a topology-proven raw boss face: " + variant);
                if (variant == "covered")
                {
                    plan.Dimensions.Remove(location); plan.CaptureFinalDimensions();
                    Assert(plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|")), "audit a subsequently lost cumulative-position provider");
                }
            }
        }

        private static void LBossBoundaryMissingInnerPathCannotInventGrooveWidth()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            foreach (string variant in new[] { "missing-arc", "missing-wall", "unrecognized-arc", "non-tangent-arc", "major-arc" })
            {
                var outline = LBossBoundaryOutline();
                var arc = outline.Arcs.Single(a => a.SourceKey == "yellow-fillet");
                if (variant == "missing-arc") { outline.Arcs.Remove(arc); }
                if (variant == "missing-wall") { outline.Segments.RemoveAll(s => s.SourceKey == "yellow-wall"); }
                if (variant == "unrecognized-arc") { outline.Fillets.RemoveAll(f => f.SourceArc == arc); }
                if (variant == "non-tangent-arc") { arc.Center = new Point2D(arc.Center.X + 0.3, arc.Center.Y); }
                if (variant == "major-arc") { arc.Bulge = 1 / arc.Bulge; }
                var plan = new DimensionPlanner(config).CreateOutlinePlan(outline, CoordinateFrame2D.Identity);
                Assert(!plan.Dimensions.Any(d => (d.TopologyEvidence ?? "").Contains("Relation=RecoveredInnerBossGroove")), "an incomplete or invalid internal path must not supply a recovered width: " + variant);
            }
        }
    }
}
