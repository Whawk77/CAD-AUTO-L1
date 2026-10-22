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
		private static OutlineFeature2D ChamferConstraintOutline()
		{
			var outline = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 179, MaxY = 85.5 };
			var points = new[] { new Point2D(0, 85.5), new Point2D(149, 85.5), new Point2D(159, 75.5),
				new Point2D(159, 55), new Point2D(179, 55), new Point2D(179, 0), new Point2D(139, 0),
				new Point2D(139, 65.5), new Point2D(0, 65.5) };
			for (int i = 0; i < points.Length; i++) { AddSegment(outline, points[i], points[(i + 1) % points.Length], "constraint-" + i); }
			outline.Chamfers.Add(new ChamferFeature2D { StartPoint = points[1], EndPoint = points[2], Value = 10,
				DeltaX = 10, DeltaY = 10, SourceSegment = outline.Segments[1] });
			return outline;
		}

		private static void ChamferConstraintRemovesDerivedPositionsInAllDirections()
		{
			var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
			var planner = new DimensionPlanner(config);
			foreach (bool confirmed in new[] { false, true })
			foreach (bool translated in new[] { false, true })
			foreach (bool reversed in new[] { false, true })
			for (int quarter = 0; quarter < 4; quarter++)
			{
				double angle = quarter * Math.PI / 2;
				var offset = translated ? new Point2D(142317.709134, -10697.612336) : new Point2D(0, 0);
				Func<Point2D, Point2D> transform = p => { var q = TransformPoint(p, angle); return new Point2D(q.X + offset.X, q.Y + offset.Y); };
				var outline = new OutlineFeature2D();
				foreach (var e in ChamferConstraintOutline().Segments) { AddSegment(outline, transform(e.Start), transform(e.End), e.SourceKey); }
				outline.MinX = outline.Segments.Min(e => e.MinX); outline.MaxX = outline.Segments.Max(e => e.MaxX);
				outline.MinY = outline.Segments.Min(e => e.MinY); outline.MaxY = outline.Segments.Max(e => e.MaxY);
				outline.Chamfers.Add(new ChamferFeature2D { StartPoint = transform(new Point2D(149, 85.5)), EndPoint = transform(new Point2D(159, 75.5)),
					Value = 10, DeltaX = 10, DeltaY = 10, SourceSegment = outline.Segments[1] });
				if (reversed) { outline.Segments.Reverse(); }
				var datum = Datum2D.FromOutline(outline);
				if (confirmed)
				{
					datum.StructureDatumMarkers.Add(StructureDatumRules.CreateInsertionMarker("CadAider_国标粗糙度32", "top", transform(new Point2D(5.5, 85.5)),
						TransformPoint(new Point2D(1, 0), angle), TransformPoint(new Point2D(0, 1), angle), outline, config.GeometryTolerance));
					datum.StructureDatumMarkers.Add(StructureDatumRules.CreateInsertionMarker("CadAider_国标粗糙度32", "right", transform(new Point2D(179, 4.5)),
						TransformPoint(new Point2D(0, 1), angle), TransformPoint(new Point2D(-1, 0), angle), outline, config.GeometryTolerance));
				}
				var plan = planner.CreateDimensionPlan(outline, datum, new HoleFeature2D[0], new SlotFeature2D[0], CoordinateFrame2D.Identity);
				var orientation = quarter % 2 == 0 ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
				Func<Point2D, double> axis = p => quarter % 2 == 0 ? p.X : p.Y;
				double a = axis(transform(new Point2D(159, 55))), b = axis(transform(new Point2D(179, 55)));
				Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.Normal && d.Orientation == orientation
					&& Math.Abs(Math.Min(axis(d.FirstPoint), axis(d.SecondPoint)) - Math.Min(a, b)) <= config.GeometryTolerance
					&& Math.Abs(Math.Max(axis(d.FirstPoint), axis(d.SecondPoint)) - Math.Max(a, b)) <= config.GeometryTolerance),
					"the real face position 20 must survive the chamfer-derived position removal");
				var cutPoint = transform(new Point2D(149, 85.5));
				Assert(!plan.Dimensions.Any(d => d.Kind == DimensionKind.Normal && d.Orientation == orientation
					&& (d.FirstPoint.DistanceTo(cutPoint) <= config.GeometryTolerance || d.SecondPoint.DistanceTo(cutPoint) <= config.GeometryTolerance)),
					"neither legacy 149 nor datum-rooted 30 may redundantly locate the chamfer cut point");
				Assert(plan.Diagnostics.DimensionCandidates.Any(d => d.SuppressedReason == "Structure.ChamferDerivedPositionCovered")
					&& !plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)),
					"removal must record a topology proof and retain transitive live coverage");
				Console.WriteLine("Chamfer constraints @" + quarter * 90 + " confirmed=" + confirmed + " translated=" + translated + " reversed=" + reversed);
			}
		}

		private static void ChamferConstraintPreservesIndependentDimensionsAndAuditsProviders()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var outline = ChamferConstraintOutline();
			var plan = new DimensionPlan();
			plan.Diagnostics.OutlineTopology = planner.CreateOutlinePlan(outline, CoordinateFrame2D.Identity).Diagnostics.OutlineTopology;
			Func<Point2D, Point2D, PlannedDimension> dimension = (a, b) => new PlannedDimension { Kind = DimensionKind.Normal,
				Role = DimensionCandidateRole.Structure, OwnerKind = DimensionCandidateOwnerKind.Outline, Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Top, FirstPoint = a, SecondPoint = b, SourceKey = "constraint-0", AttachmentValidity = true,
				RequiresOuterBoundarySupport = true, DebugRole = "TopStructWidth" };
			var overall = dimension(new Point2D(0, 85.5), new Point2D(179, 55));
			overall.Kind = DimensionKind.OverallWidth; overall.Role = DimensionCandidateRole.Overall; plan.Add(overall);
			var position = dimension(new Point2D(179, 55), new Point2D(159, 55)); plan.Add(position);
			var derived = dimension(new Point2D(179, 55), new Point2D(149, 85.5));
			derived.RuleId = StructureDatumRules.ConfirmedHorizontalDatumRule; derived.DatumFamilyKey = "confirmed-right"; plan.Add(derived);
			typeof(DimensionPlan).GetMethod("RegisterStepDatumRequirement", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(plan, new object[] { derived, config.GeometryTolerance });
			var legacy = dimension(new Point2D(0, 85.5), new Point2D(149, 85.5)); plan.Add(legacy);
			Assert(plan.TrySuppressCoveredStructure(legacy, "StructureDatum.CoveredByConfirmedDatum", new[] { overall, derived }, null, "Test=Original149"),
				"the legacy dimension must depend on overall and the datum-rooted position before further coverage");
			var protectedDimensions = new List<PlannedDimension>();
			var tolerance = dimension(derived.FirstPoint, derived.SecondPoint); tolerance.OverrideText = "30%%p0.02"; protectedDimensions.Add(tolerance);
			var independent = dimension(derived.FirstPoint, derived.SecondPoint); independent.RuleId = "IndependentFeature.Width"; protectedDimensions.Add(independent);
			protectedDimensions.Add(dimension(new Point2D(179, 0), new Point2D(149, 0)));
			protectedDimensions.Add(dimension(new Point2D(169, 0), new Point2D(149, 0)));
			foreach (var role in new[] { DimensionCandidateRole.Hole, DimensionCandidateRole.Pin, DimensionCandidateRole.Slot })
			{
				var functional = dimension(derived.FirstPoint, derived.SecondPoint); functional.Role = role;
				functional.OwnerKind = DimensionCandidateOwnerKind.HoleGroup; protectedDimensions.Add(functional);
			}
			foreach (var item in protectedDimensions) { plan.Add(item); }
			var apply = typeof(DimensionPlanner).GetMethod("SuppressChamferDerivedStructurePositions", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert(apply != null, "the shared chamfer constraint pass must exist");
			var chamfer = outline.Chamfers[0]; outline.Chamfers.Clear();
			apply.Invoke(planner, new object[] { plan, outline });
			Assert(plan.Dimensions.Contains(derived), "an unrecognized slope is not a known chamfer constraint");
			outline.Chamfers.Add(chamfer); plan.Dimensions.Remove(position);
			apply.Invoke(planner, new object[] { plan, outline });
			Assert(plan.Dimensions.Contains(derived), "an unrelated numeric 20 cannot replace the missing real face position");
			plan.Add(position); apply.Invoke(planner, new object[] { plan, outline });
			Assert(!plan.Dimensions.Contains(derived) && protectedDimensions.All(d => plan.Dimensions.Contains(d)),
				"only the proven derived position may be removed; retained=" + plan.Dimensions.Contains(derived)
				+ "; protectedMissing=" + string.Join(",", protectedDimensions.Where(d => !plan.Dimensions.Contains(d)).Select(d => d.RuleId + ":" + d.Role)));
			Assert(derived.SourceGeometryIds.Contains("constraint-1"), "the actual chamfer edge must be recorded in the coverage proof");
			plan.CaptureFinalDimensions();
			Assert(!plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)),
				"149 covered by overall and 30, then 30 covered by 20 and chamfer, is a complete live dependency chain");
			plan.Dimensions.Remove(position); plan.CaptureFinalDimensions();
			Assert(plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)),
				"losing the final real face position must still report incomplete coverage");
		}
	}
}
