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
        private static void ThreeViewProjectionRealPlannerKeepsThreeOverallExtents()
        {
            var views = ProjectionFixture();
            foreach (var view in views)
            {
                var f = view.Frame;
                view.Plan = PlanRectangle(DimensionRuleConfig.CreateDefault(), f.MinX, f.MinY, f.MaxX, f.MaxY);
            }
            var coverage = OrthographicViewSetAssigner.FindProjectedCoverage(views.Reverse().ToArray(), 0.001);
            Assert(coverage.Count == 3, "real planner must cover length, height and thickness, including outer-level totals");
            foreach (var view in views)
                OrthographicViewSetAssigner.SuppressRenderedDuplicates(view, coverage, (v, d) => true);
            Assert(views.Sum(v => v.Plan.Dimensions.Count(d => d.Kind == DimensionKind.OverallWidth || d.Kind == DimensionKind.OverallHeight)) == 3,
                "three projected overall extents remain");
            AssertHasDimension(views[0].Plan, DimensionKind.OverallWidth, DimensionOrientation.Horizontal, 113, "front length");
            AssertHasDimension(views[0].Plan, DimensionKind.OverallHeight, DimensionOrientation.Vertical, 55, "front height");
            AssertHasDimension(views[1].Plan, DimensionKind.OverallHeight, DimensionOrientation.Vertical, 20, "top thickness");
        }

        private static void ThreeViewProjectionLocal50KeepsOtherAxisAndUniqueStep()
        {
            foreach (double offset in new[] { 0.0, 10000.0 })
            {
                var views = ProjectionFixture(offset);
                var front50 = ProjectionDimension(views[0], 0, 105, 0, 155, false, offset);
                var horizontal50 = ProjectionDimension(views[0], 63, 100, 113, 100, true, offset);
                var side50 = ProjectionDimension(views[2], 155, 155, 155, 105, false, offset);
                var unique5 = ProjectionDimension(views[2], 150, 100, 150, 105, false, offset);
                var coverage = OrthographicViewSetAssigner.FindProjectedCoverage(views.Reverse().ToArray(), 0.001);
                Assert(coverage.Count == 1 && ReferenceEquals(coverage[0].Provider, front50), "same height interval has one owner");
                Assert(OrthographicViewSetAssigner.SuppressRenderedDuplicates(views[2], coverage, (v, d) => true) == 1, "side height50 removed");
                Assert(views[0].Plan.Dimensions.Contains(horizontal50) && views[2].Plan.Dimensions.Contains(unique5), "other axis50 and unique step remain");
                Assert(!views[2].Plan.Dimensions.Contains(side50), "duplicate removed from final plan");
                var diagnostic = views[2].Plan.Diagnostics.DimensionCandidates.Single(d => d.Id == side50.DiagnosticId);
                Assert(diagnostic.IsSuppressed && diagnostic.RuleId == "Test.LocalStraight"
                    && diagnostic.TopologyEvidence.Contains("CoveredByView=front") && diagnostic.TopologyEvidence.Contains("CoveredById="), "owner evidence retains generation rule");
                Assert(OrthographicViewSetAssigner.SuppressRenderedDuplicates(views[2], coverage, (v, d) => true) == 0, "idempotent suppression");
            }
        }

        private static void ThreeViewProjectionThicknessUsesReversedTopAxis()
        {
            var views = ProjectionFixture();
            views[1] = ProjectionView("top", OrthographicViewRole.Top,
                0,30, 113,30, 113,50, 0,50, 0,48, 5,48, 5,38, 0,38);
            views[2] = ProjectionView("left", OrthographicViewRole.Left,
                150,100, 158,100, 158,105, 168,105, 168,100, 170,100,
                170,155, 162,155, 162,150, 152,150, 152,155, 150,155);
            ProjectionDimension(views[1], 5,48, 5,38, false);
            var matching = ProjectionDimension(views[2], 152,150, 162,150, true);
            var different = ProjectionDimension(views[2], 158,105, 168,105, true);
            var coverage = OrthographicViewSetAssigner.FindProjectedCoverage(views, 0.001);
            Assert(coverage.Count == 1 && ReferenceEquals(coverage[0].Target, matching) && coverage[0].Extent == PartExtentKind.Width,
                "depth [2,12] matches reversed top axis; same span at [8,18] is independent");
            OrthographicViewSetAssigner.SuppressRenderedDuplicates(views[2], coverage, (v, d) => true);
            Assert(views[2].Plan.Dimensions.Contains(different), "equal-valued different interval remains");
        }

        private static void ThreeViewProjectionProtectedSemanticsAndMissingEvidenceRemain()
        {
            Action<PlannedDimension>[] protect = {
                d => d.OverrideText = "50%%p0.05", d => d.DatumFamilyKey = "datum-A", d => d.ForceOuterLevel = true,
                d => d.Kind = DimensionKind.HoleLocation, d => d.Kind = DimensionKind.PinDistance,
                d => d.Role = DimensionCandidateRole.Slot, d => d.RuleId = "OtherFeature",
                d => d.AttachmentValidity = false, d => d.SourceGeometryIds.Clear(),
                d => d.SecondPoint = new Point2D(160, 154)
            };
            foreach (var mutation in protect)
            {
                var views = ProjectionFixture();
                ProjectionDimension(views[0], 0,105, 0,155, false);
                mutation(ProjectionDimension(views[2], 155,105, 155,155, false));
                Assert(OrthographicViewSetAssigner.FindProjectedCoverage(views, 0.001).Count == 0, "protected or unproven feature remains");
            }
            var otherInterval = ProjectionFixture();
            otherInterval[2] = ProjectionView("left", OrthographicViewRole.Left, 150,100, 170,100, 170,155, 155,155, 155,150, 150,150);
            ProjectionDimension(otherInterval[0], 0,105, 0,155, false);
            ProjectionDimension(otherInterval[2], 150,100, 150,150, false);
            Assert(OrthographicViewSetAssigner.FindProjectedCoverage(otherInterval, 0.001).Count == 0, "height [0,50] must not replace height [5,55]");
        }

        private static void ThreeViewProjectionRequiresUniqueRenderedProvider()
        {
            foreach (int failure in new[] { 0, 1, 2, 3, 4 })
            {
                var views = ProjectionFixture();
                var owner = ProjectionDimension(views[0], 0,105, 0,155, false);
                var target = ProjectionDimension(views[2], 155,105, 155,155, false);
                if (failure == 3) ProjectionDimension(views[0], 0,155, 0,105, false);
                if (failure == 4) views[0].Plan.Dimensions.Remove(owner);
                var coverage = OrthographicViewSetAssigner.FindProjectedCoverage(views, 0.001);
                if (failure == 1) views[0].Plan.Dimensions.Remove(owner);
                if (failure == 2) owner.AttachmentValidity = false;
                Assert(OrthographicViewSetAssigner.SuppressRenderedDuplicates(views[2], coverage, (v, d) => failure != 0) == 0,
                    "unrendered, removed, invalid, ambiguous or missing owner cannot cover target");
                Assert(views[2].Plan.Dimensions.Contains(target), "backup retained");
            }
        }

        private static void ThreeViewProjectionUnverifiedAlignmentRemains()
        {
            foreach (int failure in new[] { 0, 1, 2, 3 })
            {
                var views = ProjectionFixture();
                views[0].Plan = PlanRectangle(DimensionRuleConfig.CreateDefault(), 0,100,113,155);
                if (failure == 0) views[1] = ProjectionView("top", OrthographicViewRole.Top, 0.2,30, 113.2,30, 113.2,50, 0.2,50);
                if (failure == 1) views[1] = ProjectionView("top", OrthographicViewRole.Top, 0,30, 113.4,30, 113.4,50, 0,50);
                if (failure == 2) views[1] = ProjectionView("top", OrthographicViewRole.Top, 0,200, 113,200, 113,220, 0,220);
                var f = views[1].Frame;
                views[1].Plan = PlanRectangle(DimensionRuleConfig.CreateDefault(), f.MinX,f.MinY,f.MaxX,f.MaxY);
                if (failure == 3) views[1].Plan.Diagnostics.OutlineTopology = null;
                var coverage = OrthographicViewSetAssigner.FindProjectedCoverage(views, 0.001);
                Assert(!coverage.Any(c => ReferenceEquals(c.TargetView, views[1])), "misaligned, differently scaled, unsupported arrangement or unproven topology remains");
                Assert(views[1].Plan.Diagnostics.Warnings.Any(w => w.StartsWith("CrossView.PendingHumanConfirmation")), "uncertainty is reported");
            }
            bool rejected = false;
            try { OrthographicViewSetAssigner.FindProjectedCoverage(ProjectionFixture(), double.NaN); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Assert(rejected, "invalid geometry tolerance is rejected");
        }

        private static OrthographicViewPlan[] ProjectionFixture(double offset = 0)
        {
            return new[] {
                ProjectionView("front", OrthographicViewRole.Front, ShiftProjection(offset, 0,105, 63,105, 63,100, 113,100, 113,155, 0,155)),
                ProjectionView("top", OrthographicViewRole.Top, ShiftProjection(offset, 0,30, 113,30, 113,50, 0,50)),
                ProjectionView("left", OrthographicViewRole.Left, ShiftProjection(offset, 150,100, 170,100, 170,155, 155,155, 155,105, 150,105))
            };
        }

        private static double[] ShiftProjection(double offset, params double[] coordinates) => coordinates.Select(x => x + offset).ToArray();

        private static OrthographicViewPlan ProjectionView(string id, OrthographicViewRole role, params double[] coordinates)
        {
            var outline = new OutlineFeature2D();
            var points = Enumerable.Range(0, coordinates.Length / 2).Select(i => new Point2D(coordinates[i * 2], coordinates[i * 2 + 1])).ToArray();
            outline.MinX = points.Min(p => p.X); outline.MaxX = points.Max(p => p.X);
            outline.MinY = points.Min(p => p.Y); outline.MaxY = points.Max(p => p.Y);
            for (int i = 0; i < points.Length; i++) AddSegment(outline, points[i], points[(i + 1) % points.Length], id + "-edge-" + i);
            var generated = new DimensionPlanner(DimensionRuleConfig.CreateDefault()).CreateOutlinePlan(outline, CoordinateFrame2D.Identity);
            var plan = new DimensionPlan();
            plan.Diagnostics.OutlineTopology = generated.Diagnostics.OutlineTopology;
            return new OrthographicViewPlan { Frame = new OrthographicViewFrame { Id = id, Role = role,
                MinX = outline.MinX, MaxX = outline.MaxX, MinY = outline.MinY, MaxY = outline.MaxY }, Plan = plan };
        }

        private static PlannedDimension ProjectionDimension(OrthographicViewPlan view, double x0, double y0, double x1, double y1, bool horizontal, double offset = 0)
        {
            var dimension = new PlannedDimension { Kind = DimensionKind.Normal, Role = DimensionCandidateRole.Structure,
                OwnerKind = DimensionCandidateOwnerKind.Outline, RuleId = "Test.LocalStraight", AttachmentValidity = true,
                Orientation = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical,
                Side = horizontal ? DimensionSide.Bottom : DimensionSide.Left,
                FirstPoint = new Point2D(x0 + offset, y0 + offset), SecondPoint = new Point2D(x1 + offset, y1 + offset) };
            dimension.SourceGeometryIds.AddRange(view.Plan.Diagnostics.OutlineTopology.Edges.Where(e => e.IsBoundary).Select(e => e.SourceKey));
            view.Plan.Add(dimension);
            view.Plan.CaptureFinalDimensions();
            return dimension;
        }
    }
}
