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
		private static OutlineFeature2D RoundedStepGrooveOutline()
		{
			var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 338, MaxY = 201.5 };
			var points = new[] { new Point2D(0, 201.5), new Point2D(300, 201.5), new Point2D(305, 196.5),
				new Point2D(305, 25), new Point2D(310, 20), new Point2D(338, 20), new Point2D(338, 0),
				new Point2D(275, 0), new Point2D(265, 10), new Point2D(265, 101.5), new Point2D(260, 106.5),
				new Point2D(177.45, 106.5), new Point2D(172.45, 101.5), new Point2D(0, 101.5) };
			for (int i = 0; i < points.Length; i++)
			{
				var a = points[i]; var b = points[(i + 1) % points.Length];
				if (i == 3 || i == 9)
				{
					var arc = new Arc2D { Start = a, End = b, Center = i == 3 ? new Point2D(310, 25) : new Point2D(260, 101.5),
						Radius = 5, Bulge = Math.Tan(Math.PI / 8), SourceKey = "rounded-" + i };
					o.Arcs.Add(arc);
					o.Fillets.Add(new FilletFeature2D { StartPoint = a, EndPoint = b, Center = arc.Center, Radius = 5, SourceArc = arc });
					continue;
				}
				AddSegment(o, a, b, "rounded-" + i);
				if (i == 1 || i == 7 || i == 11)
				{
					o.Chamfers.Add(new ChamferFeature2D { StartPoint = a, EndPoint = b,
						Value = Math.Abs(a.X - b.X), DeltaX = Math.Abs(a.X - b.X), DeltaY = Math.Abs(a.Y - b.Y) });
				}
			}
			return o;
		}

		private static void NotchLocationRecoversRoundedGrooveInAllDirections()
		{
			var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
			var planner = new DimensionPlanner(config);
			foreach (bool mirrored in new[] { false, true })
			foreach (bool reversed in new[] { false, true })
			foreach (bool confirmedRight in new[] { false, true })
			foreach (double scale in new[] { 1.0, 1.4 })
			for (int quarter = 0; quarter < 4; quarter++)
			{
				Func<Point2D, Point2D> vector = p => TransformPoint(new Point2D((mirrored ? -p.X : p.X) * scale, p.Y * scale), quarter * Math.PI / 2);
				Func<Point2D, Point2D> transform = p => { var q = vector(p); return new Point2D(q.X + 142575.92111861, q.Y - 16851.23784314); };
				var source = RoundedStepGrooveOutline(); var outline = new OutlineFeature2D();
				foreach (var e in source.Segments) { AddSegment(outline, transform(reversed ? e.End : e.Start), transform(reversed ? e.Start : e.End), e.SourceKey); }
				if (reversed) { outline.Segments.Reverse(); }
				foreach (var c in source.Chamfers) { outline.Chamfers.Add(new ChamferFeature2D { StartPoint = transform(c.StartPoint), EndPoint = transform(c.EndPoint), Value = c.Value * scale, DeltaX = c.DeltaX * scale, DeltaY = c.DeltaY * scale }); }
				foreach (var a in source.Arcs)
				{
					var arc = new Arc2D { Start = transform(a.Start), End = transform(a.End), Center = transform(a.Center), Radius = a.Radius * scale,
						Bulge = mirrored ? -a.Bulge : a.Bulge, SourceKey = a.SourceKey };
					outline.Arcs.Add(arc); outline.Fillets.Add(new FilletFeature2D { StartPoint = arc.Start, EndPoint = arc.End, Center = arc.Center, Radius = arc.Radius, SourceArc = arc });
				}
				outline.MinX = outline.Segments.Min(e => e.MinX); outline.MaxX = outline.Segments.Max(e => e.MaxX);
				outline.MinY = outline.Segments.Min(e => e.MinY); outline.MaxY = outline.Segments.Max(e => e.MaxY);
				var datum = Datum2D.FromOutline(outline);
				if (confirmedRight) { datum.StructureDatumMarkers.Add(StructureDatumRules.CreateInsertionMarker("CadAider_国标粗糙度32", "right-block",
					transform(new Point2D(338, 10)), vector(new Point2D(0, 1)), vector(new Point2D(-1, 0)), outline, config.GeometryTolerance)); }
				var plan = planner.CreateDimensionPlan(outline, datum, new HoleFeature2D[0], new SlotFeature2D[0], CoordinateFrame2D.Identity);
				string context = " @" + quarter * 90 + " mirrored=" + mirrored + " reversed=" + reversed + " scale=" + scale + " confirmed=" + confirmedRight;
				var recovered = plan.AnnotationCaseSnapshot.Features.Where(f => f.Keep && f.SourceKey != null && f.SourceKey.StartsWith("StepGroove:")).ToList();
				Assert(recovered.Count > 0 && recovered.All(f => f.RequiresOuterBoundarySupport && f.BoundaryMembership == "Boundary"
					&& f.BoundaryAttachmentValidity == true && f.SupportSourceKeys.Contains("rounded-10")), "prove the recovered groove on the real floor and corner" + context);
				var orientation = quarter % 2 == 0 ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
				var requiredWidth = new PlannedDimension { Orientation = orientation, FirstPoint = transform(new Point2D(177.45, 106.5)), SecondPoint = transform(new Point2D(265, 101.5)) };
				var requiredLocation = new PlannedDimension { Orientation = orientation, FirstPoint = transform(new Point2D(338, 0)), SecondPoint = transform(new Point2D(265, 10)) };
				var proof = typeof(DimensionPlanner).GetMethod("FindBoundaryInformationProof", BindingFlags.Instance | BindingFlags.NonPublic);
				Assert(proof.Invoke(planner, new object[] { plan, outline, requiredWidth, true, null }) != null, "preserve the recovered width through real live constraints" + context);
				Assert(proof.Invoke(planner, new object[] { plan, outline, requiredLocation, true, null }) != null, "the groove must have a live location as well as its width" + context);
				Assert(!plan.Dimensions.Any(d => d.Kind == DimensionKind.Normal && d.Role == DimensionCandidateRole.Structure
					&& d.Orientation == orientation && d.SourceKey == "rounded-10" && string.IsNullOrEmpty(d.RuleId)),
					"the same-source truncated floor must not survive beside the recovered width, even on the opposite side" + context);
				var crosses = typeof(DimensionPlanner).GetMethod("ExtensionCrossesOutline", BindingFlags.Instance | BindingFlags.NonPublic);
				foreach (var d in plan.Dimensions.Where(d => d.SourceKey != null && (d.SourceKey.StartsWith("StepGroove:") || d.RuleId == StructureDatumRules.NotchLocationRule)))
				{
					Assert(d.AttachmentValidity == true && OutlineGeometryQuery.IsPointOnBoundary(d.FirstPoint, outline, config.GeometryTolerance)
						&& OutlineGeometryQuery.IsPointOnBoundary(d.SecondPoint, outline, config.GeometryTolerance), "no floating recovered groove grips" + context);
					Assert(!(bool)crosses.Invoke(planner, new object[] { d.FirstPoint, d.Orientation, d.Side, outline })
						&& !(bool)crosses.Invoke(planner, new object[] { d.SecondPoint, d.Orientation, d.Side, outline }), "recovered groove and location extensions must clear the contour" + context);
				}
				Assert(plan.Diagnostics.OutlineInformationStatus != "NotEvaluated" && !plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|")), "audit the recovered groove location" + context);
			}
		}

		private static void NotchLocationRejectsUnprovenRecoveredGrooves()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = RoundedStepGrooveOutline(); outline.Arcs.RemoveAt(1); outline.Fillets.RemoveAt(1);
			var features = new StructureFeatureExtractor(config).Extract(outline);
			var grooves = features.Where(f => f.SourceKey != null && f.SourceKey.StartsWith("StepGroove:")).ToList();
			Assert(grooves.Count > 0 && grooves.All(f => f.RequiresOuterBoundarySupport && !f.Keep), "an open contour must not retain a synthetic recovered groove without exterior proof");
			outline = RoundedStepGrooveOutline();
			var ownership = new StructureBoundaryEvidence(DimensionPlanner.InspectOutlineTopology(outline, config.GeometryTolerance), config.GeometryTolerance);
			foreach (double maximum in new[] { 265.0, 305.0 })
			{
				var feature = new StructureFeature { Axis = StructureFeatureAxis.Horizontal, Kind = StructureFeatureKind.RealStep,
					T0 = 172.45, T1 = maximum, CrossPosition = 106.5, SourceKey = "StepGroove:proof-test" };
				ownership.AttachRecoveredGroove(feature, 20);
				ownership.ApplyAttachmentEvidence(new[] { feature }); StructureBoundaryEvidence.EnforceSelection(new[] { feature });
				Assert(feature.Keep == (maximum == 265), "prove a connected chamfer-floor-fillet walk, but never jump to the remote wall at the same projected axis");
				if (feature.Keep) { Assert(feature.SupportSourceKeys.Contains("rounded-11") && feature.SupportSourceKeys.Contains("rounded-9"), "record both real corner sources as well as the floor"); }
			}
		}

		private static void NotchLocationDropsOnlyProvenSameSourceFloors()
		{
			var config = DimensionRuleConfig.CreateDefault(); var outline = RoundedStepGrooveOutline();
			var apply = typeof(StructureMeasurementSelector).GetMethod("ApplyNestedIntervalPreference", BindingFlags.NonPublic | BindingFlags.Static);
			foreach (string variant in new[] { "covered", "different-source", "different-level", "outside-interval", "unproven", "invalid-attachment", "not-kept" })
			{
				var groove = new StructureFeatureExtractor(config).Extract(outline).Single(f => f.SourceKey != null && f.SourceKey.StartsWith("StepGroove:"));
				groove.PreferredSide = DimensionSide.Top;
				var floor = outline.Segments.Single(e => e.SourceKey == "rounded-10");
				var inner = new StructureFeature { Axis = groove.Axis, Kind = StructureFeatureKind.RealStep, IsInset = true,
					T0 = floor.MinX, T1 = floor.MaxX, CrossPosition = floor.Start.Y, FirstPoint = floor.End, SecondPoint = floor.Start,
					PreferredSide = DimensionSide.Bottom, SourceKey = floor.SourceKey };
				if (variant == "different-source") { inner.SourceKey = "independent-floor"; }
				if (variant == "different-level") { inner.CrossPosition += 10; }
				if (variant == "outside-interval") { inner.T0 = groove.T0 - 10; }
				if (variant == "unproven") { groove.BoundaryEvidence = null; }
				if (variant == "invalid-attachment") { groove.BoundaryAttachmentValidity = false; }
				if (variant == "not-kept") { groove.Keep = false; }
				apply.Invoke(null, new object[] { new System.Collections.Generic.List<StructureFeature> { inner, groove }, groove.Axis, outline, config.GeometryTolerance, null });
				Assert(inner.Keep == (variant != "covered"), "cross-side floor coverage requires the same real source, level, interval and a retained proven groove: " + variant);
				if (!inner.Keep) { Assert(inner.SuppressReason == "RecoveredGrooveCoversSameSourceFloor", "record the geometric coverage instead of treating equal numbers as duplicates"); }
			}
		}
	}
}
