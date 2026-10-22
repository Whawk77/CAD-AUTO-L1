using System;
using System.Collections.Generic;
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
		private static OutlineFeature2D NotchLocationOutline(double start = 75, double width = 50)
		{
			var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 215, MaxY = 100 };
			AddSegment(o, new Point2D(start - 3, 0), new Point2D(start, 3), "notch-cut-left");
			AddSegment(o, new Point2D(0, 0), new Point2D(start - 3, 0), "bottom-left");
			AddSegment(o, new Point2D(start + width, 3), new Point2D(start, 3), "notch-floor");
			AddSegment(o, new Point2D(0, 0), new Point2D(0, 100), "left");
			AddSegment(o, new Point2D(start + width + 3, 0), new Point2D(start + width, 3), "notch-cut-right");
			AddSegment(o, new Point2D(0, 100), new Point2D(120, 100), "top");
			AddSegment(o, new Point2D(start + width + 3, 0), new Point2D(205, 0), "bottom-right");
			AddSegment(o, new Point2D(120, 100), new Point2D(120, 62), "inner-wall");
			AddSegment(o, new Point2D(205, 0), new Point2D(215, 10), "outer-cut");
			AddSegment(o, new Point2D(215, 10), new Point2D(215, 42), "right");
			AddSegment(o, new Point2D(215, 42), new Point2D(140, 42), "platform");
			foreach (var edge in o.Segments.Where(e => e.SourceKey.Contains("cut")))
			{
				o.Chamfers.Add(new ChamferFeature2D { StartPoint = edge.Start, EndPoint = edge.End, Value = edge.LengthX,
					DeltaX = edge.LengthX, DeltaY = edge.LengthY, SourceSegment = edge });
			}
			var arc = new Arc2D { Start = new Point2D(120, 62), End = new Point2D(140, 42), Center = new Point2D(140, 62),
				Radius = 20, Bulge = Math.Tan(Math.PI / 8), SourceKey = "inner-fillet" };
			o.Arcs.Add(arc);
			o.Fillets.Add(new FilletFeature2D { StartPoint = arc.Start, EndPoint = arc.End, Center = arc.Center, Radius = arc.Radius, SourceArc = arc });
			return o;
		}

		private static void NotchLocationUsesRealEndpointsInAllDirections()
		{
			var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
			var planner = new DimensionPlanner(config);
			foreach (bool mirrored in new[] { false, true })
			foreach (bool reversed in new[] { false, true })
			foreach (bool translated in new[] { false, true })
			foreach (bool confirmedRight in new[] { false, true })
			foreach (double scale in new[] { 1.0, 1.4 })
			for (int variant = 0; variant < 2; variant++)
			for (int quarter = 0; quarter < 4; quarter++)
			{
				double start = variant == 0 ? 75 : 82, width = variant == 0 ? 50 : 42;
				double angle = quarter * Math.PI / 2;
				Func<Point2D, Point2D> vector = p => TransformPoint(new Point2D((mirrored ? -p.X : p.X) * scale, p.Y * scale), angle);
				Func<Point2D, Point2D> transform = p => { var q = vector(p); return translated ? new Point2D(q.X + 143816.24120684, q.Y - 16846.66647170) : q; };
				var source = NotchLocationOutline(start, width); var outline = new OutlineFeature2D();
				foreach (var edge in source.Segments)
				{
					AddSegment(outline, transform(reversed ? edge.End : edge.Start), transform(reversed ? edge.Start : edge.End), edge.SourceKey);
				}
				if (reversed) { outline.Segments.Reverse(); }
				foreach (var chamfer in source.Chamfers)
				{
					outline.Chamfers.Add(new ChamferFeature2D { StartPoint = transform(chamfer.StartPoint), EndPoint = transform(chamfer.EndPoint),
						Value = chamfer.Value * scale, DeltaX = chamfer.DeltaX * scale, DeltaY = chamfer.DeltaY * scale });
				}
				foreach (var arc in source.Arcs)
				{
					var transformed = new Arc2D { Start = transform(arc.Start), End = transform(arc.End), Center = transform(arc.Center),
						Radius = arc.Radius * scale, Bulge = mirrored ? -arc.Bulge : arc.Bulge, SourceKey = arc.SourceKey };
					outline.Arcs.Add(transformed);
					outline.Fillets.Add(new FilletFeature2D { StartPoint = transformed.Start, EndPoint = transformed.End,
						Center = transformed.Center, Radius = transformed.Radius, SourceArc = transformed });
				}
				outline.MinX = outline.Segments.Min(e => e.MinX); outline.MaxX = outline.Segments.Max(e => e.MaxX);
				outline.MinY = outline.Segments.Min(e => e.MinY); outline.MaxY = outline.Segments.Max(e => e.MaxY);
				var datum = Datum2D.FromOutline(outline);
				if (confirmedRight) { datum.StructureDatumMarkers.Add(StructureDatumRules.CreateInsertionMarker("CadAider_国标粗糙度32", "right-block",
					transform(new Point2D(215, 20)), vector(new Point2D(0, 1)), vector(new Point2D(-1, 0)), outline, config.GeometryTolerance)); }
				var plan = planner.CreateDimensionPlan(outline, datum, new HoleFeature2D[0], new SlotFeature2D[0], CoordinateFrame2D.Identity);
				var target = transform(new Point2D(confirmedRight ? start + width : start, 3));
				var orientation = quarter % 2 == 0 ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
				var requiredLocation = new PlannedDimension { Orientation = orientation,
					FirstPoint = transform(confirmedRight ? new Point2D(215, 10) : new Point2D(0, 0)), SecondPoint = target,
					Side = new[] { DimensionSide.Bottom, DimensionSide.Right, DimensionSide.Top, DimensionSide.Left }[quarter] };
				var positions = plan.Dimensions.Where(d => StructureDatumRules.IsCommonDatumRule(d.RuleId) && d.Orientation == orientation
					&& d.SecondPoint.DistanceTo(target) <= config.GeometryTolerance).ToList();
				var locationProof = typeof(DimensionPlanner).GetMethod("FindBoundaryInformationProof", BindingFlags.Instance | BindingFlags.NonPublic)
					.Invoke(planner, new object[] { plan, outline, requiredLocation, true, null });
				Assert(locationProof != null && positions.Count <= 1 && plan.Dimensions.Count(d => d.RuleId == "StructureDatum.NotchLocation") <= 1,
					"one real notch location must be supplied or reused @" + quarter * 90 + " mirrored=" + mirrored + " confirmedRight=" + confirmedRight
					+ " reversed=" + reversed + " translated=" + translated + " scale=" + scale + " start=" + start + "; target=" + target
					+ "; actual=" + string.Join("|", plan.Dimensions.Select(d => d.RuleId + ":" + d.FirstPoint + "->" + d.SecondPoint)));
				var position = positions.FirstOrDefault() ?? requiredLocation;
				Assert(Math.Abs(GetSpan(position) - (confirmedRight ? 215 - start - width : start) * scale) <= config.GeometryTolerance,
					"honor a confirmed datum, otherwise select a clear real boundary instead of fixing a WCS side");
				Assert(position.SecondPoint.DistanceTo(target) <= config.GeometryTolerance,
					"locate the real notch endpoint instead of its projection onto the bottom envelope");
				Assert(OutlineGeometryQuery.IsPointOnBoundary(position.FirstPoint, outline, config.GeometryTolerance)
					&& OutlineGeometryQuery.IsPointOnBoundary(position.SecondPoint, outline, config.GeometryTolerance), "both grips must lie on real geometry");
				var crosses = typeof(DimensionPlanner).GetMethod("ExtensionCrossesOutline", BindingFlags.Instance | BindingFlags.NonPublic);
				Assert(!(bool)crosses.Invoke(planner, new object[] { position.FirstPoint, position.Orientation, position.Side, outline })
					&& !(bool)crosses.Invoke(planner, new object[] { position.SecondPoint, position.Orientation, position.Side, outline }), "location extensions must clear the contour");
				var requiredWidth = new PlannedDimension { Orientation = orientation, FirstPoint = transform(new Point2D(start, 3)), SecondPoint = transform(new Point2D(start + width, 3)) };
				var proof = typeof(DimensionPlanner).GetMethod("FindBoundaryInformationProof", BindingFlags.Instance | BindingFlags.NonPublic)
					.Invoke(planner, new object[] { plan, outline, requiredWidth, true, null });
				Assert(proof != null, "preserve the real notch width directly or through live cumulative positions at the same real stations");
				Assert(!plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)), "new notch location must survive the full plan pipeline");
			}
		}

		private static void NotchLocationReusesProvenConstraintsAndAuditsMissingProviders()
		{
			var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
			var planner = new DimensionPlanner(config); var outline = NotchLocationOutline();
			var graph = DimensionPlanner.InspectOutlineTopology(outline, config.GeometryTolerance);
			var ownership = new StructureBoundaryEvidence(graph, config.GeometryTolerance);
			var features = new StructureFeatureExtractor(config).Extract(outline, ownership);
			var plan = new DimensionPlan { AnnotationCaseSnapshot = new AnnotationCaseSnapshot(features, outline, ""), Diagnostics = { OutlineTopology = graph } };
			Func<Point2D, Point2D, string, PlannedDimension> dimension = (a, b, key) => new PlannedDimension { Kind = DimensionKind.Normal,
				Role = DimensionCandidateRole.Structure, OwnerKind = DimensionCandidateOwnerKind.Outline, Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom, FirstPoint = a, SecondPoint = b, SourceKey = key, SourceGeometryIds = new List<string> { key },
				RequiresOuterBoundarySupport = true, AttachmentValidity = true };
			var floor = dimension(new Point2D(75, 3), new Point2D(125, 3), "notch-floor"); plan.Add(floor);
			var locatingFace = dimension(new Point2D(0, 0), new Point2D(72, 0), "bottom-left"); plan.Add(locatingFace);
			var apply = typeof(DimensionPlanner).GetMethod("EnsureNotchOpeningLocations", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert(apply != null, "the shared notch location completion pass must exist");
			var datum = Datum2D.FromOutline(outline);
			apply.Invoke(planner, new object[] { plan, outline, datum });
			Assert(!plan.Dimensions.Any(d => d.RuleId == "StructureDatum.NotchLocation"), "72 plus the connected real C3 already locates this notch; do not add a redundant 75");
			plan.Dimensions.Remove(locatingFace);
			var equalValue = dimension(new Point2D(140, 42), new Point2D(215, 42), "platform"); plan.Add(equalValue);
			var hole = dimension(new Point2D(0, 0), new Point2D(75, 3), "functional"); hole.Kind = DimensionKind.HoleLocation;
			for (int repeat = 0; repeat < 2; repeat++) { apply.Invoke(planner, new object[] { plan, outline, datum }); }
			var position = plan.Dimensions.Single(d => d.RuleId == "StructureDatum.NotchLocation");
			Assert(Math.Abs(GetSpan(position) - 75) <= config.GeometryTolerance && plan.Dimensions.Contains(equalValue) && plan.Dimensions.Contains(floor),
				"equal values on independent real stations must remain, and repeated completion must not duplicate the location");
			plan.Add(hole); apply.Invoke(planner, new object[] { plan, outline, datum });
			Assert(plan.Dimensions.Contains(hole), "notch completion must preserve hole positioning");
			plan.CaptureFinalDimensions();
			Assert(!plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)), "the live location satisfies its requirement");
			plan.Dimensions.Remove(position); plan.CaptureFinalDimensions();
			Assert(plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)), "lost final location providers must be reported");
			plan.Dimensions.Remove(floor); apply.Invoke(planner, new object[] { plan, outline, datum });
			Assert(!plan.Dimensions.Any(d => d.RuleId == "StructureDatum.NotchLocation"), "a stale recognized feature with no retained width must not create an orphan location");
		}
	}
}
