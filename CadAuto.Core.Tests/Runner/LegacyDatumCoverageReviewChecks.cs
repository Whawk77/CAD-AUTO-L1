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
		private static OutlineFeature2D LegacyDatumCoverageOutline(bool split)
		{
			var outline = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 150, MaxY = 150 };
			var points = split
				? new[] { new Point2D(0, 0), new Point2D(50, 0), new Point2D(150, 0), new Point2D(150, 50),
					new Point2D(150, 100), new Point2D(150, 150), new Point2D(100, 150), new Point2D(100, 100),
					new Point2D(50, 100), new Point2D(50, 50), new Point2D(0, 50) }
				: new[] { new Point2D(0, 0), new Point2D(150, 0), new Point2D(150, 150), new Point2D(100, 150),
					new Point2D(100, 100), new Point2D(50, 100), new Point2D(50, 50), new Point2D(0, 50) };
			var sources = split ? new[] { "base", "base", "base", "seam", "top", "top", "top", "middle", "middle", "left", "base" }
				: Enumerable.Range(0, points.Length).Select(i => "arm-" + i).ToArray();
			for (int i = 0; i < points.Length; i++) { AddSegment(outline, points[i], points[(i + 1) % points.Length], sources[i]); }
			return outline;
		}

		private static void LegacyDatumCoverageRemovesArmComplementsInAllDirections()
		{
			var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
			var planner = new DimensionPlanner(config);
			foreach (bool split in new[] { false, true })
			foreach (bool translated in new[] { false, true })
			foreach (bool reversed in new[] { false, true })
			foreach (double scale in new[] { 1.0, 1.4 })
			for (int quarter = 0; quarter < 4; quarter++)
			{
				double angle = quarter * Math.PI / 2;
				var offset = translated ? new Point2D(141800.39280021, -7104.27146554) : new Point2D(0, 0);
				Func<Point2D, Point2D> transform = p => { var q = TransformPoint(new Point2D(p.X * scale, p.Y * scale), angle); return new Point2D(q.X + offset.X, q.Y + offset.Y); };
				var outline = new OutlineFeature2D();
				foreach (var edge in LegacyDatumCoverageOutline(split).Segments) { AddSegment(outline, transform(edge.Start), transform(edge.End), edge.SourceKey); }
				outline.MinX = outline.Segments.Min(e => e.MinX); outline.MaxX = outline.Segments.Max(e => e.MaxX);
				outline.MinY = outline.Segments.Min(e => e.MinY); outline.MaxY = outline.Segments.Max(e => e.MaxY);
				if (reversed) { outline.Segments.Reverse(); }
				var datum = Datum2D.FromOutline(outline);
				datum.StructureDatumMarkers.Add(StructureDatumRules.CreateInsertionMarker("CadAider_国标粗糙度32", "top-block", transform(new Point2D(120, 150)),
					TransformPoint(new Point2D(1, 0), angle), TransformPoint(new Point2D(0, 1), angle), outline, config.GeometryTolerance));
				datum.StructureDatumMarkers.Add(StructureDatumRules.CreateInsertionMarker("CadAider_国标粗糙度32", "left-block", transform(new Point2D(0, 25)),
					TransformPoint(new Point2D(0, 1), angle), TransformPoint(new Point2D(-1, 0), angle), outline, config.GeometryTolerance));
				var plan = planner.CreateDimensionPlan(outline, datum, new HoleFeature2D[0], new SlotFeature2D[0], CoordinateFrame2D.Identity);
				Assert(!plan.Dimensions.Any(d => d.RuleId == "LProfile.HorizontalArmHeight" || d.RuleId == "LProfile.VerticalArmWidth"),
					"confirmed cumulative dimensions must replace both legacy arm complements @" + quarter * 90 + " scale=" + scale + " split=" + split);
				Assert(plan.Dimensions.Count == 6 && plan.Dimensions.Count(d => d.Role == DimensionCandidateRole.Overall) == 2,
					"retain two overall dimensions and four independent datum positions @" + quarter * 90 + " scale=" + scale + " split=" + split
					+ "; actual=" + string.Join(",", plan.Dimensions.Select(d => d.Orientation + ":" + GetSpan(d) + ":" + d.RuleId)));
				foreach (var orientation in new[] { DimensionOrientation.Horizontal, DimensionOrientation.Vertical })
				{
					var positions = plan.Dimensions.Where(d => d.Orientation == orientation && StructureDatumRules.IsCommonDatumRule(d.RuleId)).ToList();
					Assert(positions.Count == 2 && positions.Any(d => Math.Abs(GetSpan(d) - 50 * scale) <= config.GeometryTolerance)
						&& positions.Any(d => Math.Abs(GetSpan(d) - 100 * scale) <= config.GeometryTolerance),
						"legitimate equal-valued cumulative positions must remain in both axes");
				}
				var newlyCovered = plan.Diagnostics.DimensionCandidates.Where(d => d.SuppressedReason == "StructureDatum.CoveredByConfirmedDatum").Select(d => d.Id).ToList();
				Assert(!plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)
					&& newlyCovered.Any(id => w.Contains("|CandidateId=" + id + "|") || w.EndsWith("|CandidateId=" + id, StringComparison.Ordinal))),
					"new datum replacements must retain live providers; pre-existing overall-side warnings remain baseline issues");
				Console.WriteLine("LegacyDatumCoverage @" + quarter * 90 + " scale=" + scale + " split=" + split + " translated=" + translated + " reversed=" + reversed);
			}
		}

		private static void LegacyDatumCoveragePreservesIndependentRequirementsAndAuditsProviders()
		{
			var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
			var planner = new DimensionPlanner(config); var outline = LegacyDatumCoverageOutline(true);
			var plan = new DimensionPlan { Diagnostics = { OutlineTopology = planner.CreateOutlinePlan(outline, CoordinateFrame2D.Identity).Diagnostics.OutlineTopology } };
			Func<Point2D, Point2D, DimensionOrientation, string, PlannedDimension> dimension = (a, b, orientation, rule) => new PlannedDimension {
				Kind = DimensionKind.Normal, Role = DimensionCandidateRole.Structure, OwnerKind = DimensionCandidateOwnerKind.Outline,
				Orientation = orientation, Side = orientation == DimensionOrientation.Horizontal ? DimensionSide.Top : DimensionSide.Left,
				FirstPoint = a, SecondPoint = b, SourceKey = "base", RuleId = rule, RequiresOuterBoundarySupport = true,
				AttachmentValidity = true, DebugRole = orientation == DimensionOrientation.Horizontal ? "TopStructWidth" : "LeftStructHeight" };
			var overallX = dimension(new Point2D(0, 0), new Point2D(150, 0), DimensionOrientation.Horizontal, "LProfile.OverallWidth");
			overallX.Kind = DimensionKind.OverallWidth; overallX.Role = DimensionCandidateRole.Overall; plan.Add(overallX);
			var overallY = dimension(new Point2D(150, 0), new Point2D(150, 150), DimensionOrientation.Vertical, "LProfile.OverallHeight");
			overallY.Kind = DimensionKind.OverallHeight; overallY.Role = DimensionCandidateRole.Overall; plan.Add(overallY);
			var positionX = dimension(new Point2D(0, 50), new Point2D(50, 100), DimensionOrientation.Horizontal, StructureDatumRules.HorizontalCommonDatumRule); plan.Add(positionX);
			var positionY = dimension(new Point2D(100, 150), new Point2D(0, 50), DimensionOrientation.Vertical, StructureDatumRules.VerticalCommonDatumRule); plan.Add(positionY);
			var lower = dimension(new Point2D(0, 0), new Point2D(0, 50), DimensionOrientation.Vertical, "LProfile.HorizontalArmHeight"); plan.Add(lower);
			var upper = dimension(new Point2D(150, 50), new Point2D(50, 100), DimensionOrientation.Horizontal, "LProfile.VerticalArmWidth"); upper.AttachmentValidity = null; plan.Add(upper);
			var protectedDimensions = new List<PlannedDimension>();
			var wrongDatum = dimension(upper.FirstPoint, upper.SecondPoint, upper.Orientation, StructureDatumRules.HorizontalCommonDatumRule); protectedDimensions.Add(wrongDatum);
			var tolerance = dimension(lower.FirstPoint, lower.SecondPoint, lower.Orientation, lower.RuleId); tolerance.OverrideText = "50%%p0.02"; protectedDimensions.Add(tolerance);
			var forced = dimension(upper.FirstPoint, upper.SecondPoint, upper.Orientation, upper.RuleId); forced.ForceOuterLevel = true; protectedDimensions.Add(forced);
			var independent = dimension(upper.FirstPoint, upper.SecondPoint, upper.Orientation, "IndependentFeature.Width"); protectedDimensions.Add(independent);
			var floating = dimension(new Point2D(30, 30), upper.SecondPoint, upper.Orientation, upper.RuleId); protectedDimensions.Add(floating);
			foreach (var role in new[] { DimensionCandidateRole.Hole, DimensionCandidateRole.Pin, DimensionCandidateRole.Slot })
			{
				var functional = dimension(upper.FirstPoint, upper.SecondPoint, upper.Orientation, upper.RuleId); functional.Role = role;
				functional.OwnerKind = DimensionCandidateOwnerKind.HoleGroup; protectedDimensions.Add(functional);
			}
			foreach (var item in protectedDimensions) { plan.Add(item); }
			var datum = Datum2D.FromOutline(outline);
			var apply = typeof(DimensionPlanner).GetMethod("ApplyConfirmedStructureDatumDimensions", BindingFlags.Instance | BindingFlags.NonPublic);
			apply.Invoke(planner, new object[] { plan, datum });
			Assert(plan.Dimensions.Contains(lower) && plan.Dimensions.Contains(upper), "without confirmed datum blocks, legacy dimensions must remain available");
			datum.StructureDatumMarkers.Add(StructureDatumRules.CreateInsertionMarker("CadAider_国标粗糙度32", "top-block", new Point2D(120, 150), new Point2D(1, 0), new Point2D(0, 1), outline, config.GeometryTolerance));
			datum.StructureDatumMarkers.Add(StructureDatumRules.CreateInsertionMarker("CadAider_国标粗糙度32", "left-block", new Point2D(0, 25), new Point2D(0, 1), new Point2D(-1, 0), outline, config.GeometryTolerance));
			plan.Dimensions.Remove(positionX); apply.Invoke(planner, new object[] { plan, datum });
			Assert(plan.Dimensions.Contains(upper), "a numeric match or a different boundary station must not replace the missing horizontal provider");
			plan.Add(positionX); apply.Invoke(planner, new object[] { plan, datum });
			Assert(!plan.Dimensions.Contains(lower) && !plan.Dimensions.Contains(upper) && protectedDimensions.All(d => plan.Dimensions.Contains(d)),
				"remove proven ordinary arm complements while retaining tolerances, functional dimensions and independent rules");
			Assert(plan.Dimensions.Contains(positionX) && plan.Dimensions.Contains(positionY), "live datum providers must remain selected");
			plan.CaptureFinalDimensions();
			Assert(!plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)), "record valid covering providers");
			plan.Dimensions.Remove(positionY); plan.CaptureFinalDimensions();
			Assert(plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)), "removing a final provider must report lost coverage");
		}
	}
}
