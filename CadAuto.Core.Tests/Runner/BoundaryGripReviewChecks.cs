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
		private static void BoundaryGripFollowsConnectedCollinearFaces()
		{
			var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
			var planner = new DimensionPlanner(config);
			var resolve = typeof(DimensionPlanner).GetMethod("ResolveStructureExtensionAttachments", BindingFlags.Instance | BindingFlags.NonPublic);
			foreach (bool mirrored in new[] { false, true })
			foreach (bool reversed in new[] { false, true })
			foreach (double startHeight in new[] { 50.0, 75.0 })
			for (int quarter = 0; quarter < 4; quarter++)
			{
				double angle = quarter * Math.PI / 2;
				Func<Point2D, Point2D> transform = p => {
					var q = TransformPoint(new Point2D(mirrored ? -p.X : p.X, p.Y), angle);
					return new Point2D(q.X + 141800.39280021, q.Y - 7104.27146554);
				};
				var outline = new OutlineFeature2D();
				foreach (var edge in LegacyDatumCoverageOutline(true).Segments)
				{
					AddSegment(outline, transform(reversed ? edge.End : edge.Start), transform(reversed ? edge.Start : edge.End), edge.SourceKey);
				}
				outline.MinX = outline.Segments.Min(e => e.MinX); outline.MaxX = outline.Segments.Max(e => e.MaxX);
				outline.MinY = outline.Segments.Min(e => e.MinY); outline.MaxY = outline.Segments.Max(e => e.MaxY);
				var plan = new DimensionPlan { Diagnostics = { OutlineTopology = planner.CreateOutlinePlan(outline, CoordinateFrame2D.Identity).Diagnostics.OutlineTopology } };
				var dimension = new PlannedDimension { Kind = DimensionKind.Normal, Role = DimensionCandidateRole.Structure,
					OwnerKind = DimensionCandidateOwnerKind.Outline, FirstPoint = transform(new Point2D(150, startHeight)), SecondPoint = transform(new Point2D(50, 50)),
					Orientation = quarter % 2 == 0 ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical,
					Side = new[] { DimensionSide.Top, DimensionSide.Left, DimensionSide.Bottom, DimensionSide.Right }[quarter] };
				plan.Add(dimension);
				var functional = new PlannedDimension { Kind = dimension.Kind, Role = DimensionCandidateRole.Hole,
					OwnerKind = DimensionCandidateOwnerKind.HoleGroup, Orientation = dimension.Orientation, Side = dimension.Side,
					FirstPoint = dimension.FirstPoint, SecondPoint = dimension.SecondPoint };
				plan.Add(functional);
				resolve.Invoke(planner, new object[] { plan, outline });
				Assert(dimension.FirstPoint.DistanceTo(transform(new Point2D(150, 150))) <= config.GeometryTolerance
					&& dimension.SecondPoint.DistanceTo(transform(new Point2D(50, 100))) <= config.GeometryTolerance,
					"follow multiple connected collinear real edges toward the chosen side, including a start at a collinear join @" + quarter * 90);
				Assert(Math.Abs(GetSpan(dimension) - 100) <= config.GeometryTolerance && dimension.TopologyEvidence.Contains("EquivalentAxisAttachment=Endpoint1"),
					"preserve the measured interval and record the real attachment replacement");
				Assert(functional.FirstPoint.DistanceTo(transform(new Point2D(150, startHeight))) <= config.GeometryTolerance,
					"structure grip repair must leave hole-group positioning untouched");
			}
			var notched = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 100, MaxY = 100 };
			var points = new[] { new Point2D(0, 0), new Point2D(100, 0), new Point2D(100, 30), new Point2D(80, 30),
				new Point2D(80, 60), new Point2D(100, 60), new Point2D(100, 100), new Point2D(0, 100) };
			for (int i = 0; i < points.Length; i++) { AddSegment(notched, points[i], points[(i + 1) % points.Length], "notch-" + i); }
			var blockedPlan = new DimensionPlan { Diagnostics = { OutlineTopology = planner.CreateOutlinePlan(notched, CoordinateFrame2D.Identity).Diagnostics.OutlineTopology } };
			var blocked = new PlannedDimension { Kind = DimensionKind.Normal, Role = DimensionCandidateRole.Structure, OwnerKind = DimensionCandidateOwnerKind.Outline,
				Orientation = DimensionOrientation.Horizontal, Side = DimensionSide.Top, FirstPoint = new Point2D(100, 0), SecondPoint = new Point2D(0, 100) };
			blockedPlan.Add(blocked); resolve.Invoke(planner, new object[] { blockedPlan, notched });
			Assert(blocked.FirstPoint.DistanceTo(new Point2D(100, 0)) <= config.GeometryTolerance && blocked.TopologyEvidence.Contains("ExtensionOverlapUnresolved"),
				"a same-coordinate endpoint across a boundary gap must not be borrowed");
		}

		private static void BoundaryGripPlanningRetainsClearCommonDatumDimensions()
		{
			var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
			var planner = new DimensionPlanner(config);
			var crosses = typeof(DimensionPlanner).GetMethod("ExtensionCrossesOutline", BindingFlags.Instance | BindingFlags.NonPublic);
			foreach (bool split in new[] { false, true })
			foreach (bool translated in new[] { false, true })
			foreach (bool reversed in new[] { false, true })
			foreach (double scale in new[] { 1.0, 1.4 })
			for (int quarter = 0; quarter < 4; quarter++)
			{
				double angle = quarter * Math.PI / 2;
				Func<Point2D, Point2D> transform = p => {
					var q = TransformPoint(new Point2D(p.X * scale, p.Y * scale), angle);
					return translated ? new Point2D(q.X + 141800.39280021, q.Y - 7104.27146554) : q;
				};
				var outline = new OutlineFeature2D();
				foreach (var edge in LegacyDatumCoverageOutline(split).Segments)
				{
					AddSegment(outline, transform(reversed ? edge.End : edge.Start), transform(reversed ? edge.Start : edge.End), edge.SourceKey);
				}
				if (reversed) { outline.Segments.Reverse(); }
				outline.MinX = outline.Segments.Min(e => e.MinX); outline.MaxX = outline.Segments.Max(e => e.MaxX);
				outline.MinY = outline.Segments.Min(e => e.MinY); outline.MaxY = outline.Segments.Max(e => e.MaxY);
				var plan = planner.CreateDimensionPlan(outline, Datum2D.FromOutline(outline), new HoleFeature2D[0], new SlotFeature2D[0], CoordinateFrame2D.Identity);
				var structure = plan.Dimensions.Where(d => d.Kind == DimensionKind.Normal && d.Role == DimensionCandidateRole.Structure).ToList();
				Assert(structure.All(d => !(bool)crosses.Invoke(planner, new object[] { d.FirstPoint, d.Orientation, d.Side, outline })
					&& !(bool)crosses.Invoke(planner, new object[] { d.SecondPoint, d.Orientation, d.Side, outline })),
					"selected structure extension lines must clear the real contour @" + quarter * 90 + " split=" + split
					+ "; actual=" + string.Join("|", structure.Select(d => d.RuleId + ":" + d.Orientation + ":" + d.Side
						+ ":" + d.FirstPoint + "->" + d.SecondPoint + ":" + d.TopologyEvidence)));
				foreach (var provider in structure.Where(d => StructureDatumRules.IsCommonDatumRule(d.RuleId)))
				{
					Assert(!structure.Any(d => d != provider && d.Orientation == provider.Orientation && d.Side == provider.Side
						&& ((d.FirstPoint.DistanceTo(provider.FirstPoint) <= config.GeometryTolerance && d.SecondPoint.DistanceTo(provider.SecondPoint) <= config.GeometryTolerance)
							|| (d.SecondPoint.DistanceTo(provider.FirstPoint) <= config.GeometryTolerance && d.FirstPoint.DistanceTo(provider.SecondPoint) <= config.GeometryTolerance))),
						"a legacy structure duplicate must not compete with the same real common-datum anchors in rendering");
				}
				Assert(structure.Any(d => StructureDatumRules.IsCommonDatumRule(d.RuleId) && Math.Abs(GetSpan(d) - 100 * scale) <= config.GeometryTolerance),
					"retain the cumulative position rather than suppressing all equal values");
			}
		}
	}
}
