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
		private static void StepDatumBlocksCheckedBeforeChamferRuleExit()
		{
			var config = DimensionRuleConfig.CreateDefault();
			config.UseFeatureFirstStructurePipeline = true;
			var planner = new DimensionPlanner(config);
			var baseline = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 179, MaxY = 85.5 };
			var points = new[] { new Point2D(0, 85.5), new Point2D(149, 85.5), new Point2D(159, 75.5),
				new Point2D(159, 55), new Point2D(179, 55), new Point2D(179, 0), new Point2D(139, 0),
				new Point2D(139, 65.5), new Point2D(0, 65.5) };
			for (int i = 0; i < points.Length; i++) { AddSegment(baseline, points[i], points[(i + 1) % points.Length], "column-" + i); }
			foreach (bool translated in new[] { false, true })
			for (int quarter = 0; quarter < 4; quarter++)
			{
				double angle = quarter * Math.PI / 2;
				var offset = translated ? new Point2D(142317.709134, -10697.612336) : new Point2D(0, 0);
				Func<Point2D, Point2D> transform = p => { var rotated = TransformPoint(p, angle); return new Point2D(rotated.X + offset.X, rotated.Y + offset.Y); };
				var outline = new OutlineFeature2D();
				foreach (var edge in baseline.Segments) { AddSegment(outline, transform(edge.Start), transform(edge.End), edge.SourceKey); }
				outline.MinX = outline.Segments.Min(e => e.MinX); outline.MaxX = outline.Segments.Max(e => e.MaxX);
				outline.MinY = outline.Segments.Min(e => e.MinY); outline.MaxY = outline.Segments.Max(e => e.MaxY);
				outline.Chamfers.Add(new ChamferFeature2D { StartPoint = transform(points[1]), EndPoint = transform(points[2]),
					Value = 10, DeltaX = 10, DeltaY = 10, SourceSegment = outline.Segments[1] });
				Func<string, Point2D, bool, bool, StructureDatumMarker2D> marker = (source, point, vertical, dynamic) =>
					StructureDatumRules.CreateInsertionMarker("CadAider_国标粗糙度32", source, transform(point),
						TransformPoint(vertical ? new Point2D(0, 1) : new Point2D(1, 0), angle),
						TransformPoint(vertical ? new Point2D(-1, 0) : new Point2D(0, 1), angle), outline, config.GeometryTolerance, dynamic);
				Func<Datum2D, DimensionPlan> run = inputDatum => planner.CreateDimensionPlan(outline, inputDatum, new HoleFeature2D[0], new SlotFeature2D[0], CoordinateFrame2D.Identity);
				var noBlocks = run(Datum2D.FromOutline(outline));
				Assert(noBlocks.Diagnostics.StructureDatumChecks.Count(c => c.Contains("|Decision=Missing|")) == 2,
					"both datum axes must be checked before the chamfer exits the step rules");
				Assert(!noBlocks.Dimensions.Any(d => (d.RuleId ?? string.Empty).StartsWith("StructureDatum.", StringComparison.Ordinal)),
					"missing blocks must not invent a fixed structure datum");
				var datum = Datum2D.FromOutline(outline);
				var bottom = marker("bottom-block", new Point2D(159, 0), false, false);
				datum.StructureDatumMarkers.Add(bottom);
				datum.StructureDatumMarkers.Add(marker("right-block", new Point2D(179, 20), true, false));
				var valid = run(datum);
				Assert(valid.Diagnostics.StructureDatumChecks.Count(c => c.Contains("|Decision=Confirmed|")) == 2
					&& valid.Diagnostics.StructureDatumChecks.Any(c => c.Contains("Source=bottom-block") && c.Contains("|Validation=Valid|"))
					&& valid.Diagnostics.StructureDatumChecks.Any(c => c.Contains("Source=right-block") && c.Contains("|Validation=Valid|")),
					"valid horizontal and vertical datum blocks must be reported on chamfered geometry");
				Assert(datum.BaseX == outline.MinX && datum.BaseY == outline.MinY,
					"structure datum preference must not change hole datum coordinates");
				foreach (bool horizontal in new[] { true, false })
				{
					string axisName = horizontal ? "X" : "Y";
					string decision = valid.Diagnostics.StructureDatumChecks.Single(c => c.StartsWith("Axis=" + axisName + "|Decision=Confirmed|", StringComparison.Ordinal));
					double coordinate = horizontal ? decision.EndsWith("Side=Left") ? outline.MinX : outline.MaxX
						: decision.EndsWith("Side=Bottom") ? outline.MinY : outline.MaxY;
					var rooted = valid.Dimensions.Where(d => (d.RuleId ?? string.Empty).StartsWith("StructureDatum.", StringComparison.Ordinal)
						&& d.Orientation == (horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical)).ToList();
					Assert(rooted.Count > 0 && rooted.All(d => Math.Abs((horizontal ? d.FirstPoint.X : d.FirstPoint.Y) - coordinate) <= config.GeometryTolerance
						&& d.AttachmentValidity == true && GetSpan(d) > config.GeometryTolerance),
						"confirmed blocks must root structure positions without requiring a step classification");
				}
				Assert(!valid.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)),
					"all replaced structure intervals must retain their live datum providers");
				var rejectedDatum = Datum2D.FromOutline(outline);
				rejectedDatum.StructureDatumMarkers.Add(marker("near-miss", new Point2D(159, -2 * config.GeometryTolerance), false, false));
				rejectedDatum.StructureDatumMarkers.Add(marker("dynamic", new Point2D(159, 0), false, true));
				var rejected = run(rejectedDatum);
				Assert(rejected.Diagnostics.StructureDatumChecks.Count(c => c.Contains("|Validation=Rejected|")) == 2
					&& !rejected.Diagnostics.StructureDatumChecks.Any(c => c.Contains("|Decision=Confirmed|"))
					&& !rejected.Dimensions.Any(d => (d.RuleId ?? string.Empty).StartsWith("StructureDatum.", StringComparison.Ordinal)),
					"off-boundary and unverified dynamic blocks must not confirm a datum");
				var conflictDatum = Datum2D.FromOutline(outline);
				conflictDatum.StructureDatumMarkers.Add(bottom);
				conflictDatum.StructureDatumMarkers.Add(marker("top-block", new Point2D(20, 85.5), false, false));
				var conflict = run(conflictDatum);
				string axis = bottom.ContactKind == "BlockInsertion" ? "Y" : "X";
				Assert(conflict.Diagnostics.StructureDatumChecks.Any(c => c.StartsWith("Axis=" + axis + "|Decision=Conflict|", StringComparison.Ordinal))
					&& conflict.Diagnostics.Warnings.Any(w => w.Contains("|Axis=" + axis + "|Reason=Conflicting")),
					"opposite datum blocks must request confirmation even when no step dimensions apply");
				if (axis == "X") { conflictDatum.StructureDatumMinimumX = true; } else { conflictDatum.StructureDatumMinimumY = true; }
				var confirmed = run(conflictDatum);
				Assert(confirmed.Diagnostics.StructureDatumChecks.Any(c => c.StartsWith("Axis=" + axis + "|Decision=Confirmed|", StringComparison.Ordinal)
					&& c.Contains("|Reason=UserConfirmedBoundaryBlock|")), "the explicit conflict choice must be honored on the next plan");
				Console.WriteLine("StepDatum early check @" + quarter * 90 + " translated=" + translated + " valid/rejected/conflict/confirmed");
			}
		}

		private static void ConfirmedStructureDatumPreservesIndependentDimensions()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var outline = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 100, MaxY = 100 };
			var points = new[] { new Point2D(0, 0), new Point2D(100, 0), new Point2D(100, 60), new Point2D(70, 100), new Point2D(0, 100) };
			for (int i = 0; i < points.Length; i++) { AddSegment(outline, points[i], points[(i + 1) % points.Length], "body-" + i); }
			var initial = planner.CreateOutlinePlan(outline, CoordinateFrame2D.Identity);
			var plan = new DimensionPlan();
			plan.Diagnostics.OutlineTopology = initial.Diagnostics.OutlineTopology;
			foreach (var overall in initial.Dimensions.Where(d => d.Role == DimensionCandidateRole.Overall)) { plan.Add(overall); }
			var datum = Datum2D.FromOutline(outline);
			datum.StructureDatumMarkers.Add(StructureDatumRules.CreateInsertionMarker("CadAider_国标粗糙度32", "right-block", new Point2D(100, 20),
				new Point2D(0, 1), new Point2D(-1, 0), outline, config.GeometryTolerance));
			Func<PlannedDimension> width = () => new PlannedDimension { Kind = DimensionKind.Normal, Role = DimensionCandidateRole.Structure,
				OwnerKind = DimensionCandidateOwnerKind.Outline, Orientation = DimensionOrientation.Horizontal, Side = DimensionSide.Top,
				FirstPoint = points[4], SecondPoint = points[3], SourceKey = "body-3", AttachmentValidity = true, DebugRole = "TopStructWidth" };
			var original = width(); plan.Add(original);
			var tolerance = width(); tolerance.OverrideText = "70%%p0.02"; plan.Add(tolerance);
			var feature = width(); feature.RuleId = "IndependentFeature.Width"; plan.Add(feature);
			var internalSpan = width(); internalSpan.FirstPoint = new Point2D(20, 20); plan.Add(internalSpan);
			var protectedDimensions = new List<PlannedDimension> { tolerance, feature, internalSpan };
			foreach (var role in new[] { DimensionCandidateRole.Hole, DimensionCandidateRole.Pin, DimensionCandidateRole.Slot, DimensionCandidateRole.Datum })
			{
				var dimension = width(); dimension.Role = role; dimension.OwnerKind = DimensionCandidateOwnerKind.HoleGroup;
				dimension.FirstPoint = new Point2D(20, 20); dimension.SecondPoint = new Point2D(50, 20);
				plan.Add(dimension); protectedDimensions.Add(dimension);
			}
			var coveredProvider = width(); coveredProvider.FirstPoint = points[0]; coveredProvider.SecondPoint = new Point2D(50, 0);
			plan.Add(coveredProvider); protectedDimensions.Add(coveredProvider);
			var covered = width(); plan.Add(covered);
			Assert(plan.TrySuppressCoveredStructure(covered, "Test.ExistingCoverage", new[] { coveredProvider }, null, "Test=LiveProvider"),
				"the existing coverage provider must be registered before datum preference");
			var apply = typeof(DimensionPlanner).GetMethod("ApplyConfirmedStructureDatumDimensions", BindingFlags.Instance | BindingFlags.NonPublic);
			apply.Invoke(planner, new object[] { plan, datum });
			var rooted = plan.Dimensions.Single(d => d.RuleId == StructureDatumRules.ConfirmedHorizontalDatumRule);
			Assert(!plan.Dimensions.Contains(original) && rooted.FirstPoint.X == 100 && rooted.SecondPoint.DistanceTo(points[3]) <= config.GeometryTolerance
				&& Math.Abs(GetSpan(rooted) - 30) <= config.GeometryTolerance && rooted.SourceGeometryIds.Contains("right-block"),
				"a sloping body must express its accepted structure target from the confirmed right datum");
			Assert(protectedDimensions.All(d => plan.Dimensions.Contains(d)) && plan.Dimensions.Count(d => Math.Abs(GetSpan(d) - 30) <= config.GeometryTolerance) == 5,
				"equal-valued hole, pin, slot and datum dimensions, explicit tolerances, independent features and live providers must survive");
			Assert(rooted.SourceGeometryIds.Contains("body-3") && rooted.SourceGeometryIds.Contains("body-1")
				&& OutlineGeometryQuery.IsPointOnBoundary(rooted.FirstPoint, outline, config.GeometryTolerance),
				"both datum support and real target geometry must remain traceable");
			var suppression = new StructureSuppressionRules(config);
			Assert(!suppression.TopExtensionCrossesOutline(rooted.FirstPoint, outline, config.FirstDimOffset)
				&& !suppression.TopExtensionCrossesOutline(rooted.SecondPoint, outline, config.FirstDimOffset), "datum extensions must remain clear");
			int count = plan.Dimensions.Count;
			apply.Invoke(planner, new object[] { plan, datum });
			Assert(plan.Dimensions.Count == count, "rechecking confirmed datums must not duplicate providers");
			plan.CaptureFinalDimensions();
			Assert(!plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)), "replacements must retain complete live coverage");
			plan.Dimensions.Remove(rooted); plan.CaptureFinalDimensions();
			Assert(plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)), "a later lost datum provider must be reported");
		}

		private static void StepDatumGeometryRechoosesDatumAfterRotation()
		{
			var config = DimensionRuleConfig.CreateDefault();
			config.UseFeatureFirstStructurePipeline = true;
			var planner = new DimensionPlanner(config);
			var rules = new StructureSuppressionRules(config);
			foreach (bool translated in new[] { false, true })
			foreach (bool reversed in new[] { false, true })
			for (int quarter = 0; quarter < 4; quarter++)
			{
				var baseline = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 150, MaxY = 150 };
				var points = new[] { new Point2D(0, 0), new Point2D(150, 0), new Point2D(150, 150), new Point2D(100, 150),
					new Point2D(100, 100), new Point2D(50, 100), new Point2D(50, 50), new Point2D(0, 50) };
				for (int i = 0; i < points.Length; i++) { AddSegment(baseline, points[i], points[(i + 1) % points.Length], "rotate-datum-" + i); }
				var outline = TransformOutline(baseline, quarter * Math.PI / 2);
				if (translated)
				{
					var shifted = new OutlineFeature2D { MinX = outline.MinX + 141800, MaxX = outline.MaxX + 141800,
						MinY = outline.MinY - 7104, MaxY = outline.MaxY - 7104 };
					foreach (var edge in outline.Segments) { AddSegment(shifted, new Point2D(edge.Start.X + 141800, edge.Start.Y - 7104),
						new Point2D(edge.End.X + 141800, edge.End.Y - 7104), edge.SourceKey); }
					outline = shifted;
				}
				if (reversed) { outline.Segments.Reverse(); }
				var plan = planner.CreateOutlinePlan(outline, CoordinateFrame2D.Identity);
				var horizontal = plan.Dimensions.Where(d => d.RuleId == StructureDatumRules.HorizontalCommonDatumRule).ToList();
				var vertical = plan.Dimensions.Where(d => d.RuleId == StructureDatumRules.VerticalCommonDatumRule).ToList();
				double expectedX = quarter < 2 ? outline.MaxX : outline.MinX;
				double expectedY = quarter == 0 || quarter == 3 ? outline.MinY : outline.MaxY;
				Assert(horizontal.Count == 2 && vertical.Count == 2
					&& horizontal.All(d => Math.Abs(d.FirstPoint.X - expectedX) <= config.GeometryTolerance)
					&& vertical.All(d => Math.Abs(d.FirstPoint.Y - expectedY) <= config.GeometryTolerance),
					"rotation " + quarter + " must rechoose the real datum with the shortest clear extension, independent of translation/selection order");
				Assert(horizontal.Concat(vertical).All(d => d.AttachmentValidity == true
					&& OutlineGeometryQuery.IsPointOnBoundary(d.FirstPoint, outline, config.GeometryTolerance)
					&& OutlineGeometryQuery.IsPointOnBoundary(d.SecondPoint, outline, config.GeometryTolerance)
					&& !(d.Orientation == DimensionOrientation.Horizontal
						? d.Side == DimensionSide.Top ? rules.TopExtensionCrossesOutline(d.FirstPoint, outline, config.FirstDimOffset)
							: rules.BottomExtensionCrossesOutline(d.FirstPoint, outline, config.FirstDimOffset)
						: d.Side == DimensionSide.Left ? rules.LeftExtensionCrossesOutline(d.FirstPoint, outline, config.FirstDimOffset)
							: rules.RightExtensionCrossesOutline(d.FirstPoint, outline, config.FirstDimOffset))),
					"dynamic datums must retain real and clear reference anchors");
				Console.WriteLine("StepDatum dynamic @" + quarter * 90 + " translated=" + translated + " reversed=" + reversed
					+ " X=" + (quarter < 2 ? "Right" : "Left") + " Y=" + (quarter == 0 || quarter == 3 ? "Bottom" : "Top"));
			}
		}

		private static void StepDatumCrossSideCumulativeCoverageProtectsHoleGroups()
		{
			var config = DimensionRuleConfig.CreateDefault();
			config.UseFeatureFirstStructurePipeline = true;
			var planner = new DimensionPlanner(config);
			foreach (bool split in new[] { false, true })
			foreach (bool reversed in new[] { false, true })
			foreach (bool translated in new[] { false, true })
			for (int quarter = 0; quarter < 4; quarter++)
			{
				var baseline = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 150, MaxY = 150 };
				var points = split
					? new[] { new Point2D(0, 0), new Point2D(50, 0), new Point2D(150, 0), new Point2D(150, 50),
						new Point2D(150, 100), new Point2D(150, 150), new Point2D(100, 150), new Point2D(100, 100),
						new Point2D(50, 100), new Point2D(50, 50), new Point2D(0, 50) }
					: new[] { new Point2D(0, 0), new Point2D(150, 0), new Point2D(150, 150), new Point2D(100, 150),
						new Point2D(100, 100), new Point2D(50, 100), new Point2D(50, 50), new Point2D(0, 50) };
				var sources = split ? new[] { "base", "base", "base", "right-seam", "top", "top", "top", "middle", "middle", "left", "base" }
					: Enumerable.Range(0, points.Length).Select(i => "edge-" + i).ToArray();
				for (int i = 0; i < points.Length; i++) { AddSegment(baseline, points[i], points[(i + 1) % points.Length], sources[i]); }
				var outline = TransformOutline(baseline, quarter * Math.PI / 2);
				if (translated)
				{
					foreach (var edge in outline.Segments)
					{
						edge.Start = new Point2D(edge.Start.X + 141800, edge.Start.Y - 7104);
						edge.End = new Point2D(edge.End.X + 141800, edge.End.Y - 7104);
					}
					outline.MinX += 141800; outline.MaxX += 141800; outline.MinY -= 7104; outline.MaxY -= 7104;
				}
				var ledge = outline.Segments[split ? 7 : 4];
				if (reversed) { outline.Segments.Reverse(); }
				bool horizontal = quarter % 2 == 0;
				var orientation = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
				Func<Point2D, double> axis = p => horizontal ? p.X : p.Y;
				var full = planner.CreateDimensionPlan(outline, Datum2D.FromOutline(outline), new HoleFeature2D[0], new SlotFeature2D[0], CoordinateFrame2D.Identity);
				var providers = full.Dimensions.Where(d => StructureDatumRules.IsCommonDatumRule(d.RuleId) && d.Orientation == orientation).ToList();
				Assert(providers.Count == 2 && providers.All(d => d.AttachmentValidity == true)
					&& providers[0].FirstPoint.DistanceTo(providers[1].FirstPoint) <= config.GeometryTolerance
					&& providers.Select(d => d.DatumFamilyKey).Distinct().Count() == 1
					&& providers.Select(d => axis(d.SecondPoint)).OrderBy(x => x)
					.SequenceEqual(new[] { axis(ledge.Start), axis(ledge.End) }.OrderBy(x => x)),
					"both live cumulative dimensions must locate the actual middle step endpoints");
				Assert(!full.Dimensions.Any(d => d.Kind == DimensionKind.Normal && d.OwnerKind == DimensionCandidateOwnerKind.Outline
					&& !StructureDatumRules.IsCommonDatumRule(d.RuleId) && d.Orientation == orientation
					&& Math.Abs(Math.Min(axis(d.FirstPoint), axis(d.SecondPoint)) - Math.Min(axis(ledge.Start), axis(ledge.End))) <= config.GeometryTolerance
					&& Math.Abs(Math.Max(axis(d.FirstPoint), axis(d.SecondPoint)) - Math.Max(axis(ledge.Start), axis(ledge.End))) <= config.GeometryTolerance),
					"middle step covered by the cumulative difference must disappear across placement sides, rotation=" + quarter + " split=" + split);
				var plan = new DimensionPlan();
				plan.Diagnostics.OutlineTopology = full.Diagnostics.OutlineTopology;
				var side = providers[0].Side == DimensionSide.Top ? DimensionSide.Bottom : providers[0].Side == DimensionSide.Bottom ? DimensionSide.Top
					: providers[0].Side == DimensionSide.Left ? DimensionSide.Right : DimensionSide.Left;
				var removed = new List<PlannedDimension>();
				var kept = new List<PlannedDimension>();
				foreach (string kind in new[] { "structure", "raw", "tolerance", "forced", "pin-A", "pin-B", "pin-C", "functional", "slot", "unrelated", "near", "invalid" })
				{
					var candidate = new PlannedDimension { Kind = DimensionKind.Normal, Role = DimensionCandidateRole.Structure,
						OwnerKind = DimensionCandidateOwnerKind.Outline, Orientation = orientation, Side = side,
						FirstPoint = ledge.Start, SecondPoint = ledge.End, SourceKey = ledge.SourceKey,
						SourceGeometryIds = new List<string> { ledge.SourceKey }, AttachmentValidity = true,
						DebugRole = horizontal ? "BottomStructWidth" : "LeftStructHeight" };
					if (kind == "raw") { candidate.Role = DimensionCandidateRole.OutlineSegment; candidate.DebugRole = "OutlineSegment"; }
					if (kind == "tolerance") { candidate.OverrideText = "<>±0.02"; }
					if (kind == "forced") { candidate.ForceOuterLevel = true; }
					if (kind.StartsWith("pin-", StringComparison.Ordinal))
					{
						candidate.Kind = kind == "pin-A" ? (horizontal ? DimensionKind.DatumHoleLocationX : DimensionKind.DatumHoleLocationY)
							: kind == "pin-B" ? DimensionKind.PinGroupDistance : DimensionKind.PinDistance;
						candidate.Role = kind == "pin-A" ? DimensionCandidateRole.Datum : DimensionCandidateRole.Pin;
						candidate.OwnerKind = DimensionCandidateOwnerKind.PinGroup;
					}
					if (kind == "functional") { candidate.Kind = DimensionKind.HoleLocation; candidate.Role = DimensionCandidateRole.Hole; candidate.OwnerKind = DimensionCandidateOwnerKind.PinGroup; }
					if (kind == "slot") { candidate.Role = DimensionCandidateRole.Slot; candidate.OwnerKind = DimensionCandidateOwnerKind.Slot; }
					if (kind == "unrelated")
					{
						candidate.FirstPoint = new Point2D(candidate.FirstPoint.X + (horizontal ? 0 : 5), candidate.FirstPoint.Y + (horizontal ? 5 : 0));
						candidate.SecondPoint = new Point2D(candidate.SecondPoint.X + (horizontal ? 0 : 5), candidate.SecondPoint.Y + (horizontal ? 5 : 0));
					}
					if (kind == "near") { candidate.SecondPoint = new Point2D(candidate.SecondPoint.X + (horizontal ? 2 * config.GeometryTolerance : 0), candidate.SecondPoint.Y + (horizontal ? 0 : 2 * config.GeometryTolerance)); }
					if (kind == "invalid") { candidate.AttachmentValidity = false; }
					plan.Add(candidate);
					(kind == "structure" || kind == "raw" ? removed : kept).Add(candidate);
				}
				typeof(DimensionPlanner).GetMethod(horizontal ? "ApplyHorizontalStepDatumDimensions" : "ApplyVerticalStepDatumDimensions", BindingFlags.Instance | BindingFlags.NonPublic)
					.Invoke(planner, new object[] { plan, outline, Datum2D.FromOutline(outline) });
				Assert(removed.All(d => !plan.Dimensions.Contains(d)), "both ordinary structure and raw segments must be removed by cross-side coverage");
				Assert(kept.All(d => plan.Dimensions.Contains(d)),
					"cross-side coverage must protect pin A/B/C, functional/slot, tolerance, unrelated equal values, near matches and invalid evidence");
				var live = plan.Dimensions.Where(d => StructureDatumRules.IsCommonDatumRule(d.RuleId)).ToList();
				Assert(live.Count == 2 && removed.All(d => plan.Diagnostics.DimensionCandidates.Single(c => c.Id == d.DiagnosticId)
					.TopologyEvidence.Contains("|CoveredByIds=" + string.Join(",", live.Select(p => p.DiagnosticId)) + "|")),
					"the difference proof must record both live cumulative providers");
				plan.CaptureFinalDimensions(); full.CaptureFinalDimensions();
				Assert(!plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)),
					"both providers of the new cross-side replacements must remain live");
				Console.WriteLine("StepDatum cross-side @" + quarter * 90 + " split=" + split + " translated=" + translated + " reversed=" + reversed + " protected=" + kept.Count);
			}
		}

		private static void StepDatumInsertionEvidenceRejectsUnverifiedTransforms()
		{
			var outline = CreateStepDatumReviewOutline();
			foreach (string name in new[] { "CadAider_日标2个角", "CadAider_国标粗糙度32下", "CadAider_国标粗糙度32" })
			foreach (double angle in new[] { 0.0, Math.PI / 4, Math.PI / 2, Math.PI })
			foreach (bool mirror in new[] { false, true })
			{
				var x = new Point2D(Math.Cos(angle) * (mirror ? -1 : 1), Math.Sin(angle) * (mirror ? -1 : 1));
				var y = new Point2D(-Math.Sin(angle), Math.Cos(angle));
				var marker = StructureDatumRules.CreateInsertionMarker(name, "block", new Point2D(130, 0), x, y, outline, 0.001);
				Assert(string.IsNullOrEmpty(marker.ValidationFailure) == (angle == 0 || angle == Math.PI),
					"only horizontal insertion anchors with verified axes may establish this datum");
			}
			var detached = StructureDatumRules.CreateInsertionMarker("CadAider_国标粗糙度32", "offset-block", new Point2D(130, -1),
				new Point2D(1, 0), new Point2D(0, 1), outline, 0.001);
			Assert(detached.ValidationFailure == "InsertionNotOnHorizontalBoundary", "a remote glyph endpoint cannot replace a detached insertion anchor");
			var dynamic = StructureDatumRules.CreateInsertionMarker("CadAider_日标2个角", "dynamic-block", new Point2D(130, 0),
				new Point2D(1, 0), new Point2D(0, 1), outline, 0.001, dynamicBlock: true);
			Assert(dynamic.ValidationFailure == "DynamicBlockContactUnverified", "unsupported dynamic parameters must remain unconfirmed");
			var datum = Datum2D.FromOutline(outline);
			datum.StructureDatumMarkers.Add(dynamic);
			var transformed = CoordinateFrameModelTransform.ToLocal(datum, new CoordinateFrame2D(new Point2D(10, 20), new Point2D(1, 0)));
			Assert(transformed.StructureDatumMarkers[0].ValidationFailure == dynamic.ValidationFailure
				&& transformed.StructureDatumMarkers[0].ContactKind == dynamic.ContactKind,
				"coordinate conversion must preserve rejected evidence");
		}

		private static void StepDatumCoverageTracksRawEdgesAndProtectsLegalEqualValues()
		{
			foreach (bool reverse in new[] { false, true })
			{
				var outline = CreateStepDatumReviewOutline();
				if (reverse) { outline.Segments.Reverse(); }
				var plan = CreateStepDatumReviewPlan(outline);
				var raw = CreateStepDatumReviewCandidate("review-8", new Point2D(0, 15), new Point2D(0, 35), true);
				var structure = CreateStepDatumReviewCandidate("review-8", raw.FirstPoint, raw.SecondPoint);
				var tolerance = CreateStepDatumReviewCandidate("review-8", raw.FirstPoint, raw.SecondPoint);
				tolerance.OverrideText = "<>±0.02";
				var rawTolerance = CreateStepDatumReviewCandidate("review-8", raw.FirstPoint, raw.SecondPoint, true);
				rawTolerance.OverrideText = "<>±0.02";
				var functional = CreateStepDatumReviewCandidate("review-8", raw.FirstPoint, raw.SecondPoint);
				functional.Kind = DimensionKind.HoleLocation;
				functional.Role = DimensionCandidateRole.Hole;
				functional.OwnerKind = DimensionCandidateOwnerKind.HoleGroup;
				functional.DebugRole = "FunctionalHole";
				var unrelated = CreateStepDatumReviewCandidate("mount-boss", new Point2D(150, 15), new Point2D(150, 35));
				unrelated.SourceGeometryIds.Add("review-8");
				plan.Add(raw); plan.Add(structure); plan.Add(tolerance); plan.Add(rawTolerance); plan.Add(functional); plan.Add(unrelated);
				InvokeStepDatumReviewPass(plan, outline);
				Assert(!plan.Dimensions.Contains(raw) && !plan.Dimensions.Contains(structure), "proven local arm heights must be replaced");
				Assert(plan.Dimensions.Contains(tolerance) && plan.Dimensions.Contains(rawTolerance)
					&& plan.Dimensions.Contains(functional) && plan.Dimensions.Contains(unrelated),
					"a legal equal-valued feature and a toleranced span must remain despite a shared support ID");
				var providers = plan.Dimensions.Where(d => d.RuleId == StructureDatumRules.VerticalCommonDatumRule).ToList();
				Assert(providers.Count == 3 && providers.All(d => d.FirstPoint.Y == 50)
					&& providers.Select(d => d.SecondPoint.Y).OrderBy(y => y).SequenceEqual(new[] { 10.0, 15.0, 35.0 }),
					"target features and common datum must be independent of selection order");
				Assert(plan.Diagnostics.DimensionCandidates.Single(d => d.Id == raw.DiagnosticId).TopologyEvidence.Contains("CoveredByIds="),
					"raw outline suppression must record its actual providers");
				var missing = providers.First(d => plan.IsCoverageProvider(d));
				plan.MarkSuppressed(missing, "Test.RemoveProvider");
				plan.Dimensions.Remove(missing);
				plan.CaptureFinalDimensions();
				Assert(plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)
					&& w.Contains("CandidateId=" + raw.DiagnosticId + "|")), "raw coverage gaps must remain visible after provider loss");
			}
		}

		private static void StepDatumReplacesLegacyStructureDimensions()
		{
			var config = DimensionRuleConfig.CreateDefault();
			config.UseFeatureFirstStructurePipeline = true;
			var planner = new DimensionPlanner(config);
			foreach (var baseline in new[] { CreateStair3Outline(), BuildTest2BottomContourOutline(mirrored: false), BuildTest2BottomContourOutline(mirrored: true) })
			foreach (double angle in new[] { 0.0, Math.PI / 2, Math.PI, 3 * Math.PI / 2 })
			{
				var outline = angle == 0 ? baseline : TransformOutline(baseline, angle);
				foreach (bool withHoles in new[] { false, true })
				{
					var plan = withHoles ? planner.CreateDimensionPlan(outline, Datum2D.FromOutline(outline), new HoleFeature2D[0])
						: planner.CreateOutlinePlan(outline);
					var providers = plan.Dimensions.Where(d => StructureDatumRules.IsCommonDatumRule(d.RuleId)).ToList();
					Assert(providers.Count >= 4, "the staircase must retain its horizontal and vertical common datum providers");
					Assert(!plan.Dimensions.Any(d => !StructureDatumRules.IsCommonDatumRule(d.RuleId)
						&& d.Kind == DimensionKind.Normal && d.Role == DimensionCandidateRole.Structure
						&& providers.Any(p => p.Orientation == d.Orientation && p.Side == d.Side
							&& ((p.FirstPoint.DistanceTo(d.FirstPoint) <= config.GeometryTolerance && p.SecondPoint.DistanceTo(d.SecondPoint) <= config.GeometryTolerance)
								|| (p.FirstPoint.DistanceTo(d.SecondPoint) <= config.GeometryTolerance && p.SecondPoint.DistanceTo(d.FirstPoint) <= config.GeometryTolerance)))),
						"common datum providers must replace legacy structure dimensions of the same real endpoints");
					Assert(plan.Diagnostics.DimensionCandidates.Where(c => c.IsSuppressed && c.TopologyEvidence.Contains("Relation=SameRealEndpointPair"))
						.All(c => providers.Any(p => c.TopologyEvidence.Contains("|CoveredByIds=" + p.DiagnosticId + "|"))),
						"each exact replacement must record a live final provider");
					if (angle == 0 && !withHoles)
					{
						Console.WriteLine("StepDatum replacement: " + string.Join(";", plan.Dimensions.Select(d =>
							"D" + d.DiagnosticId + "=" + GetSpan(d).ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + ":" + d.RuleId)));
					}
				}
			}
			var reviewOutline = CreateStepDatumReviewOutline();
			var reviewPlan = CreateStepDatumReviewPlan(reviewOutline);
			InvokeStepDatumReviewPass(reviewPlan, reviewOutline);
			var provider = reviewPlan.Dimensions.First(d => d.RuleId == StructureDatumRules.VerticalCommonDatumRule);
			var removed = new List<PlannedDimension>();
			var kept = new List<PlannedDimension>();
			foreach (string kind in new[] { "legacy", "raw", "missing-source", "tolerance", "forced", "functional", "invalid", "independent", "near", "side", "axis", "datum" })
			{
				var candidate = CreateStepDatumReviewCandidate("legacy-source", provider.SecondPoint, provider.FirstPoint, kind == "raw");
				candidate.RuleId = "LProfile.HorizontalArmHeight";
				if (kind == "missing-source") { candidate.SourceKey = null; candidate.SourceGeometryIds.Clear(); }
				if (kind == "tolerance") { candidate.OverrideText = "<>±0.02"; }
				if (kind == "forced") { candidate.ForceOuterLevel = true; }
				if (kind == "functional") { candidate.Kind = DimensionKind.HoleLocation; candidate.Role = DimensionCandidateRole.Hole; }
				if (kind == "invalid") { candidate.AttachmentValidity = false; }
				if (kind == "independent") { candidate.FirstPoint = new Point2D(150, candidate.FirstPoint.Y); candidate.SecondPoint = new Point2D(150, candidate.SecondPoint.Y); }
				if (kind == "near") { candidate.SecondPoint = new Point2D(candidate.SecondPoint.X + 2 * config.GeometryTolerance, candidate.SecondPoint.Y); }
				if (kind == "side") { candidate.Side = DimensionSide.Right; }
				if (kind == "axis") { candidate.Orientation = DimensionOrientation.Horizontal; }
				if (kind == "datum") { candidate.RuleId = StructureDatumRules.VerticalCommonDatumRule; candidate.DatumFamilyKey = "another-datum"; }
				reviewPlan.Add(candidate);
				(kind == "legacy" || kind == "raw" || kind == "missing-source" ? removed : kept).Add(candidate);
			}
			typeof(DimensionPlanner).GetMethod("SuppressStepDatumDuplicateStructureDimensions", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(planner, new object[] { reviewPlan, new List<PlannedDimension> { provider }, "Test|RealBoundary" });
			Assert(removed.All(d => !reviewPlan.Dimensions.Contains(d)) && kept.All(d => reviewPlan.Dimensions.Contains(d)),
				"only matching un-toleranced structure dimensions may be replaced; preserve independent features and datum identities");
			Assert(removed.All(d => reviewPlan.Diagnostics.DimensionCandidates.Single(c => c.Id == d.DiagnosticId)
				.TopologyEvidence.Contains("|CoveredByIds=" + provider.DiagnosticId + "|")),
				"legacy replacements must record the provider even when their original source is missing");
			reviewPlan.CaptureFinalDimensions();
			Assert(!reviewPlan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)),
				"exact replacement must retain its live provider");
		}

		private static void StepDatumProvidersSurviveRepeatedDeduplication()
		{
			foreach (string text in new[] { "", "<>±0.01" })
			{
				var outline = CreateStepDatumReviewOutline();
				var plan = CreateStepDatumReviewPlan(outline);
				plan.Add(CreateStepDatumReviewCandidate("review-8", new Point2D(0, 15), new Point2D(0, 35), true));
				InvokeStepDatumReviewPass(plan, outline);
				var provider = plan.Dimensions.First(d => d.RuleId == StructureDatumRules.VerticalCommonDatumRule && plan.IsCoverageProvider(d));
				var duplicate = new PlannedDimension { Kind = provider.Kind, Role = provider.Role, OwnerKind = provider.OwnerKind,
					Orientation = provider.Orientation, Side = provider.Side, FirstPoint = provider.FirstPoint, SecondPoint = provider.SecondPoint,
					RuleId = provider.RuleId, SourceKey = provider.SourceKey, DatumFamilyKey = provider.DatumFamilyKey,
					SourceGeometryIds = new List<string>(provider.SourceGeometryIds), AttachmentValidity = true, DebugRole = provider.DebugRole };
				plan.Add(duplicate);
				plan.Dimensions.Remove(duplicate);
				plan.Dimensions.Insert(0, duplicate);
				var independent = CreateStepDatumReviewCandidate("independent-feature", new Point2D(150, provider.FirstPoint.Y), new Point2D(150, provider.SecondPoint.Y));
				independent.OverrideText = text;
				plan.Add(independent);
				var planner = new DimensionPlanner(DimensionRuleConfig.CreateDefault());
				var pass = typeof(DimensionPlanner).GetMethod("SuppressDuplicateMeasuredDimensions", BindingFlags.Instance | BindingFlags.NonPublic);
				for (int i = 0; i < 3; i++) { pass.Invoke(planner, new object[] { plan, DimensionSide.Left, false }); }
				plan.CaptureFinalDimensions();
				Assert(plan.Dimensions.Contains(provider) && !plan.Dimensions.Contains(duplicate) && plan.Dimensions.Contains(independent),
					"keep the live datum provider and independent same-interval dimension; remove only the same-identity duplicate");
				Assert(!plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)),
					"repeated measured deduplication must preserve dependency completeness");
			}
		}

		private static void StepDatumDisconnectedGroupsRequireConfirmation()
		{
			var outline = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 100, MaxY = 50 };
			var points = new[] { new Point2D(50, 50), new Point2D(100, 50), new Point2D(100, 0), new Point2D(60, 0),
				new Point2D(60, 10), new Point2D(40, 10), new Point2D(40, 20), new Point2D(30, 20),
				new Point2D(30, 15), new Point2D(0, 15), new Point2D(0, 35), new Point2D(50, 35) };
			for (int i = 0; i < points.Length; i++) { AddSegment(outline, points[i], points[(i + 1) % points.Length], "separate-" + i); }
			var plan = CreateStepDatumReviewPlan(outline);
			var legal = CreateStepDatumReviewCandidate("separate-9", new Point2D(0, 15), new Point2D(0, 35));
			plan.Add(legal);
			InvokeStepDatumReviewPass(plan, outline, new Point2D(80, 50));
			Assert(plan.Dimensions.Contains(legal) && !plan.Dimensions.Any(d => d.RuleId == StructureDatumRules.VerticalCommonDatumRule)
				&& plan.Diagnostics.Warnings.Any(w => w.Contains("Reason=NoUniqueConnectedStepGroup")),
				"separate non-monotone step groups cannot borrow one another's boundary evidence");
		}

		private static void ExtensionCrossingChecksRealArcsOnFourSides()
		{
			var rules = new StructureSuppressionRules(DimensionRuleConfig.CreateDefault());
			var outline = new OutlineFeature2D { MinX = -5, MinY = -5, MaxX = 5, MaxY = 5 };
			outline.Arcs.Add(new Arc2D { Start = new Point2D(0, -5), End = new Point2D(0, 5), Center = new Point2D(0, 0), Radius = 5, Bulge = 1, SourceKey = "semicircle" });
			Assert(rules.LeftExtensionCrossesOutline(new Point2D(10, 0), outline, 10)
				&& rules.RightExtensionCrossesOutline(new Point2D(-10, 0), outline, 10)
				&& rules.TopExtensionCrossesOutline(new Point2D(3, -10), outline, 10)
				&& rules.BottomExtensionCrossesOutline(new Point2D(3, 10), outline, 10), "all four directions must detect the real arc sweep");
			Assert(!rules.LeftExtensionCrossesOutline(new Point2D(0, 0), outline, 10)
				&& !rules.RightExtensionCrossesOutline(new Point2D(5, 0), outline, 10)
				&& !rules.TopExtensionCrossesOutline(new Point2D(10, -10), outline, 10)
				&& !rules.BottomExtensionCrossesOutline(new Point2D(10, 10), outline, 10), "missing arc sweep and the origin contact must not become false intersections");
		}

		private static OutlineFeature2D CreateStepDatumReviewOutline()
		{
			var outline = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 168, MaxY = 50 };
			var points = new[] { new Point2D(85, 50), new Point2D(163, 50), new Point2D(168, 45), new Point2D(168, 0),
				new Point2D(113, 0), new Point2D(113, 10), new Point2D(108, 10), new Point2D(108, 15), new Point2D(0, 15), new Point2D(0, 35), new Point2D(85, 35) };
			for (int i = 0; i < points.Length; i++) { if (i != 1) { AddSegment(outline, points[i], points[(i + 1) % points.Length], "review-" + i); } }
			outline.Arcs.Add(new Arc2D { Start = points[1], End = points[2], Center = new Point2D(163, 45), Radius = 5, Bulge = -Math.Tan(Math.PI / 8), SourceKey = "R5" });
			AddSegment(outline, new Point2D(150, 15), new Point2D(150, 35), "mount-boss");
			return outline;
		}

		private static DimensionPlan CreateStepDatumReviewPlan(OutlineFeature2D outline)
		{
			var plan = new DimensionPlan();
			plan.Diagnostics.OutlineTopology = new DimensionPlanner(DimensionRuleConfig.CreateDefault()).CreateOutlinePlan(outline, CoordinateFrame2D.Identity).Diagnostics.OutlineTopology;
			plan.Add(new PlannedDimension { Kind = DimensionKind.OverallHeight, Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left, FirstPoint = new Point2D(outline.MaxX, outline.MinY), SecondPoint = new Point2D(outline.MaxX, outline.MaxY), AttachmentValidity = true });
			return plan;
		}

		private static PlannedDimension CreateStepDatumReviewCandidate(string source, Point2D first, Point2D second, bool raw = false) => new PlannedDimension {
			Kind = DimensionKind.Normal, Role = raw ? DimensionCandidateRole.OutlineSegment : DimensionCandidateRole.Structure,
			OwnerKind = DimensionCandidateOwnerKind.Outline, Orientation = DimensionOrientation.Vertical, Side = DimensionSide.Left,
			FirstPoint = first, SecondPoint = second, SourceKey = source, SourceGeometryIds = new List<string> { source },
			DebugRole = raw ? "OutlineSegment" : "LeftStructHeight", AttachmentValidity = true };

		private static void InvokeStepDatumReviewPass(DimensionPlan plan, OutlineFeature2D outline, Point2D? contact = null)
		{
			var datum = Datum2D.FromOutline(outline);
			datum.StructureDatumMarkers.Add(StructureDatumRules.CreateInsertionMarker("CadAider_日标2个角", "top-block",
				contact ?? new Point2D(130, 50), new Point2D(1, 0), new Point2D(0, 1), outline, 0.001));
			typeof(DimensionPlanner).GetMethod("ApplyVerticalStepDatumDimensions", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(new DimensionPlanner(DimensionRuleConfig.CreateDefault()), new object[] { plan, outline, datum });
		}
	}
}
