using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Recognition;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        private static readonly (bool MinimumX, bool MinimumY)[] ExpectedDatumCornerOrder = { (true, true), (true, false), (false, true), (false, false) };

        private static OutlineFeature2D CreateClosedPolyline(string prefix, params Point2D[] points)
        {
            var o = new OutlineFeature2D();
            for (int i = 0; i < points.Length; i++) { AddSegment(o, points[i], points[(i + 1) % points.Length], prefix + i); }
            return o;
        }

        private static OutlineFeature2D CreateFourChamferPlate()
        {
            return CreateClosedPolyline("C5-", new Point2D(5, 0), new Point2D(95, 0), new Point2D(100, 5), new Point2D(100, 75),
                new Point2D(95, 80), new Point2D(5, 80), new Point2D(0, 75), new Point2D(0, 5));
        }

        // diagnostics/chamfer-rule-case-20261030/falling-step-chamfer.dxf
        private static OutlineFeature2D CreateFallingStepChamferFixture()
        {
            return CreateClosedPolyline("fall-", new Point2D(0, 0), new Point2D(100, 0), new Point2D(100, 40), new Point2D(85, 55),
                new Point2D(85, 80), new Point2D(65, 80), new Point2D(50, 95), new Point2D(0, 95));
        }

        private static OutlineFeature2D CreateAsymmetricStair()
        {
            return CreateClosedPolyline("stair-", new Point2D(0, 0), new Point2D(120, 0), new Point2D(120, 90), new Point2D(80, 90),
                new Point2D(80, 60), new Point2D(30, 60), new Point2D(30, 20), new Point2D(0, 20));
        }

        // The asymmetric stair with R5 on its inner corner (80,60) and outer corner (30,60).
        private static OutlineFeature2D CreateFilletedStair()
        {
            var o = new OutlineFeature2D();
            AddSegment(o, new Point2D(0, 0), new Point2D(120, 0), "fstair-0");
            AddSegment(o, new Point2D(120, 0), new Point2D(120, 90), "fstair-1");
            AddSegment(o, new Point2D(120, 90), new Point2D(80, 90), "fstair-2");
            AddSegment(o, new Point2D(80, 90), new Point2D(80, 65), "fstair-3");
            AddSegment(o, new Point2D(75, 60), new Point2D(35, 60), "fstair-4");
            AddSegment(o, new Point2D(30, 55), new Point2D(30, 20), "fstair-5");
            AddSegment(o, new Point2D(30, 20), new Point2D(0, 20), "fstair-6");
            AddSegment(o, new Point2D(0, 20), new Point2D(0, 0), "fstair-7");
            o.Arcs.Add(new Arc2D { Start = new Point2D(75, 60), End = new Point2D(80, 65), Center = new Point2D(75, 65), Radius = 5,
                Bulge = Math.Tan(Math.PI / 8), SourceKey = "fstair-inner-R5" });
            o.Arcs.Add(new Arc2D { Start = new Point2D(35, 60), End = new Point2D(30, 55), Center = new Point2D(35, 55), Radius = 5,
                Bulge = Math.Tan(Math.PI / 8), SourceKey = "fstair-outer-R5" });
            return o;
        }

        // Two C5 risers in a row: the second level follows from the first, so rule 2 must chain.
        private static OutlineFeature2D CreateTwoLevelChamferStep()
        {
            return CreateClosedPolyline("twoC5-", new Point2D(0, 0), new Point2D(100, 0), new Point2D(100, 20), new Point2D(60, 20),
                new Point2D(55, 25), new Point2D(40, 25), new Point2D(35, 30), new Point2D(0, 30));
        }

        private static OutlineFeature2D CreateRoundedEndStep()
        {
            var o = CreateClosedPolyline("round-", new Point2D(80, 10), new Point2D(40, 10), new Point2D(40, 40), new Point2D(0, 40),
                new Point2D(0, 0), new Point2D(80, 0));
            o.Segments.RemoveAt(o.Segments.Count - 1);
            o.Arcs.Add(new Arc2D { Start = new Point2D(80, 0), End = new Point2D(80, 10), Center = new Point2D(80, 5), Radius = 5,
                Bulge = 1, SourceKey = "round-R5" });
            return o;
        }

        private static string FormatDistances(IEnumerable<double> values)
        {
            return "[" + string.Join(",", values.Select(v => v.ToString("0.###"))) + "]";
        }

        private static OutlineFeature2D CreateLowerLeftFilletPlate()
        {
            var o = new OutlineFeature2D();
            AddSegment(o, new Point2D(5, 0), new Point2D(100, 0), "BOTTOM");
            AddSegment(o, new Point2D(100, 0), new Point2D(100, 80), "RIGHT");
            AddSegment(o, new Point2D(100, 80), new Point2D(0, 80), "TOP");
            AddSegment(o, new Point2D(0, 80), new Point2D(0, 5), "LEFT");
            o.Arcs.Add(new Arc2D { Start = new Point2D(0, 5), End = new Point2D(5, 0), Center = new Point2D(5, 5), Radius = 5,
                Bulge = Math.Tan(Math.PI / 8), SourceKey = "R5" });
            return o;
        }

        private static List<(bool MinimumX, bool MinimumY)> TransformedEnvelopeCorners(OutlineFeature2D transformed,
            IEnumerable<Point2D> corners, double angle, bool mirror, double tolerance)
        {
            return corners.Select(p => TransformCapturedSelectionPoint(p, angle, mirror))
                .Select(p => (MinimumX: Math.Abs(p.X - transformed.MinX) <= tolerance, MinimumY: Math.Abs(p.Y - transformed.MinY) <= tolerance))
                .ToList();
        }

        private static void RealDatumCornerPrefersLowerLeftUnderTransforms()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            var planner = new DimensionPlanner(config);
            double tol = config.GeometryTolerance;
            var cases = new[]
            {
                Tuple.Create("step-riser", CreateStepRiserChamferFixture(), new[] { new Point2D(0, 0), new Point2D(155.500002186, 94.999774816) }),
                Tuple.Create("large-chamfer", CreateLargeChamferFixture(), new[] { new Point2D(99.999998624, 0) }),
                Tuple.Create("lower-left-fillet", CreateLowerLeftFilletPlate(), new[] { new Point2D(0, 80), new Point2D(100, 0), new Point2D(100, 80) }),
                Tuple.Create("four-chamfers", CreateFourChamferPlate(), new Point2D[0]),
            };
            foreach (var fixture in cases)
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                string at = " " + fixture.Item1 + " angle=" + angle + " mirror=" + mirror;
                var o = TransformCapturedSelection(fixture.Item2, angle, mirror, false);
                var graph = planner.CreateOutlinePlan(o).Diagnostics.OutlineTopology;
                var real = TransformedEnvelopeCorners(o, fixture.Item3, angle, mirror, tol);
                bool found = DimensionPlanner.TryFindRealDatumCorner(graph, tol, null, null, out var corner);
                if (real.Count == 0)
                {
                    Assert(!found, "corners cut by chamfers have no real datum corner; found=" + corner + at);
                    continue;
                }
                var expected = ExpectedDatumCornerOrder.First(c => real.Contains(c));
                Assert(found && corner == expected, "the datum is the first uncut envelope corner in lower-left, upper-left, lower-right, upper-right order; expected="
                    + expected + " actual=" + (found ? corner.ToString() : "none") + at);
                foreach (bool requiredX in new[] { true, false })
                {
                    var allowed = ExpectedDatumCornerOrder.Where(c => c.MinimumX == requiredX && real.Contains(c)).ToList();
                    bool constrained = DimensionPlanner.TryFindRealDatumCorner(graph, tol, requiredX, null, out var match);
                    Assert(constrained == allowed.Count > 0 && (!constrained || match == allowed[0]),
                        "a block-confirmed X side limits the corner to that side; requiredX=" + requiredX + " actual=" + (constrained ? match.ToString() : "none") + at);
                }
                var cut = ExpectedDatumCornerOrder.Where(c => !real.Contains(c)).ToList();
                foreach (var missing in cut)
                {
                    Assert(!DimensionPlanner.TryFindRealDatumCorner(graph, tol, missing.MinimumX, missing.MinimumY, out _),
                        "a corner removed by a chamfer or fillet, or absent from the outline, is never a datum corner; corner=" + missing + at);
                }
            }
        }

        private static void StepDatumMeasuresFromRealCornerWithoutBlocks()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            var planner = new DimensionPlanner(config);
            double tol = config.GeometryTolerance;
            var source = new OutlineFeature2D();
            var points = new[] { new Point2D(0, 0), new Point2D(150, 0), new Point2D(150, 150), new Point2D(100, 150),
                new Point2D(100, 100), new Point2D(50, 100), new Point2D(50, 50), new Point2D(0, 50) };
            for (int i = 0; i < points.Length; i++) { AddSegment(source, points[i], points[(i + 1) % points.Length], "stair-" + i); }
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                string at = " angle=" + angle + " mirror=" + mirror;
                var o = TransformCapturedSelection(source, angle, mirror, false);
                var plan = planner.CreateOutlinePlan(o);
                var real = TransformedEnvelopeCorners(o, new[] { points[0], points[1], points[2] }, angle, mirror, tol);
                var expected = ExpectedDatumCornerOrder.First(c => real.Contains(c));
                double datumX = expected.MinimumX ? o.MinX : o.MaxX;
                double datumY = expected.MinimumY ? o.MinY : o.MaxY;
                var rooted = plan.Dimensions.Where(d => StructureDatumRules.IsCommonDatumRule(d.RuleId)).ToList();
                Assert(rooted.Any(d => d.Orientation == DimensionOrientation.Horizontal) && rooted.Any(d => d.Orientation == DimensionOrientation.Vertical)
                    && rooted.All(d => d.Orientation == DimensionOrientation.Horizontal
                        ? Math.Abs(d.FirstPoint.X - datumX) <= tol : Math.Abs(d.FirstPoint.Y - datumY) <= tol),
                    "without blocks both axes measure from the first real corner; expected=" + expected + " dims=["
                    + string.Join(",", rooted.Select(d => d.Orientation.ToString()[0] + "@" + d.FirstPoint.X.ToString("0.###") + "," + d.FirstPoint.Y.ToString("0.###"))) + "]" + at);
            }
        }

        // Per real corner of the source: distances along source X, then source Y, each ending with the overall span.
        private static (string Name, OutlineFeature2D Source, (Point2D Corner, double[] X, double[] Y)[] Datums)[] DatumBaselineCases()
        {
            var stairDatums = new[]
            {
                (new Point2D(0, 0), new[] { 30, 80, 120.0 }, new[] { 20, 60, 90.0 }),
                (new Point2D(120, 0), new[] { 40, 90, 120.0 }, new[] { 20, 60, 90.0 }),
                (new Point2D(120, 90), new[] { 40, 90, 120.0 }, new[] { 30, 70, 90.0 }),
            };
            return new (string Name, OutlineFeature2D Source, (Point2D Corner, double[] X, double[] Y)[] Datums)[]
            {
                ("step-riser", CreateStepRiserChamferFixture(), new[]
                {
                    (new Point2D(0, 0), new[] { 35, 39, 133.5, 155.5 }, new[] { 65, 88, 95.0 }),
                    (new Point2D(155.500002186, 94.999774816), new[] { 29, 116.5, 135.5, 155.5 }, new[] { 30, 80, 95.0 }),
                }),
                ("falling-step", CreateFallingStepChamferFixture(), new[]
                {
                    (new Point2D(0, 0), new[] { 65, 85, 100.0 }, new[] { 55, 80, 95.0 }),
                    (new Point2D(0, 95), new[] { 65, 85, 100.0 }, new[] { 55, 95.0 }),
                    (new Point2D(100, 0), new[] { 50, 100.0 }, new[] { 55, 80, 95.0 }),
                }),
                ("stair", CreateAsymmetricStair(), stairDatums),
                ("filleted-stair", CreateFilletedStair(), stairDatums),
                ("two-level-C5", CreateTwoLevelChamferStep(), new[]
                {
                    (new Point2D(0, 0), new[] { 40, 60, 100.0 }, new[] { 20, 30.0 }),
                    (new Point2D(0, 30), new[] { 40, 60, 100.0 }, new[] { 30.0 }),
                    (new Point2D(100, 0), new[] { 45, 65, 100.0 }, new[] { 20, 30.0 }),
                }),
            };
        }

        private static ((bool MinimumX, bool MinimumY) Corner, double[] X, double[] Y) ExpectedDatumBaseline(
            (Point2D Corner, double[] X, double[] Y)[] datums, OutlineFeature2D transformed, double angle, bool mirror, double tolerance)
        {
            var real = TransformedEnvelopeCorners(transformed, datums.Select(d => d.Corner), angle, mirror, tolerance);
            var corner = ExpectedDatumCornerOrder.First(k => real.Contains(k));
            var datum = datums[real.IndexOf(corner)];
            bool swapped = angle == 90 || angle == 270;
            return (corner, swapped ? datum.Y : datum.X, swapped ? datum.X : datum.Y);
        }

        private static void DatumBaselineRulesMatchExpectedLayers()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            double tol = config.GeometryTolerance;
            foreach (var fixture in DatumBaselineCases())
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                string at = " " + fixture.Name + " angle=" + angle + " mirror=" + mirror;
                var o = TransformCapturedSelection(fixture.Source, angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                var graph = DimensionPlanner.InspectOutlineTopology(o, tol);
                var (expectedCorner, expectedX, expectedY) = ExpectedDatumBaseline(fixture.Datums, o, angle, mirror, tol);
                Assert(DimensionPlanner.TryFindRealDatumCorner(graph, tol, null, null, out var corner) && corner == expectedCorner,
                    "the datum is the first real corner; expected=" + expectedCorner + " actual=" + corner + at);
                Assert(DatumBaselineRules.TryPlan(graph, o.Chamfers, o.Fillets, corner.MinimumX, corner.MinimumY, tol, out var layout, out string reason),
                    "a step outline whose slopes and arcs are recognized corner features gets a baseline layout; reason=" + reason + at);
                var actualX = layout.AlongX.Targets.Select(t => t.Distance).Concat(new[] { layout.AlongX.Span }).ToList();
                var actualY = layout.AlongY.Targets.Select(t => t.Distance).Concat(new[] { layout.AlongY.Span }).ToList();
                Assert(actualX.Count == expectedX.Length && actualX.Zip(expectedX, (a, e) => Math.Abs(a - e) <= 0.01).All(ok => ok)
                    && actualY.Count == expectedY.Length && actualY.Zip(expectedY, (a, e) => Math.Abs(a - e) <= 0.01).All(ok => ok),
                    "each face layer is measured once from the datum, chamfer-fixed layers are skipped, and a chamfer between parallel faces is located by its far end; expected X="
                    + FormatDistances(expectedX) + " Y=" + FormatDistances(expectedY) + " actual X=" + FormatDistances(actualX) + " Y=" + FormatDistances(actualY) + at);
            }
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                var o = TransformCapturedSelection(CreateRoundedEndStep(), angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                var graph = DimensionPlanner.InspectOutlineTopology(o, tol);
                Assert(!DatumBaselineRules.TryPlan(graph, o.Chamfers, o.Fillets, true, true, tol, out _, out string reason)
                    && reason != null && reason.Contains("round-R5"),
                    "an arc joining two parallel faces is a rounded end, not a step corner; reason=" + reason + " angle=" + angle + " mirror=" + mirror);
            }
        }

        private static void DatumBaselinePlannerReplacesStepFaceLengths()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            double tol = config.GeometryTolerance;
            foreach (var fixture in DatumBaselineCases())
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                string at = " " + fixture.Name + " angle=" + angle + " mirror=" + mirror;
                var o = TransformCapturedSelection(fixture.Source, angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                var plan = new DimensionPlanner(config).CreateOutlinePlan(o);
                var expected = ExpectedDatumBaseline(fixture.Datums, o, angle, mirror, tol);
                foreach (bool horizontal in new[] { true, false })
                {
                    var dims = plan.Dimensions.Where(d => d.Orientation == (horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical)).ToList();
                    var spans = dims.Select(GetSpan).OrderBy(v => v).ToList();
                    var expectedSpans = horizontal ? expected.X : expected.Y;
                    double datum = horizontal ? expected.Corner.MinimumX ? o.MinX : o.MaxX : expected.Corner.MinimumY ? o.MinY : o.MaxY;
                    Assert(spans.Count == expectedSpans.Length && spans.Zip(expectedSpans, (a, e) => Math.Abs(a - e) <= 0.01).All(ok => ok)
                        && dims.Where(d => d.Kind == DimensionKind.Normal).All(d => StructureDatumRules.IsBaselineRule(d.RuleId)
                            && Math.Abs((horizontal ? d.FirstPoint.X : d.FirstPoint.Y) - datum) <= tol),
                        "the plan keeps the overall dimension plus one datum-edge distance per face layer, and no face lengths; expected="
                        + FormatDistances(expectedSpans) + " actual=[" + string.Join(",", dims.Select(d => (d.DebugRole ?? d.Kind.ToString()) + ":"
                        + GetSpan(d).ToString("0.###"))) + "] axis=" + (horizontal ? "X" : "Y") + at);
                }
                var pending = plan.Diagnostics.Warnings.Where(w => w.Contains("NoClearBaselineAttachment")
                    || w.StartsWith("StructureCoverage.", StringComparison.Ordinal)).ToList();
                Assert(pending.Count == 0 && plan.Diagnostics.OutlineInformationStatus != "PendingConfirmation",
                    "baseline dimensions attach to real points and every replaced face length stays covered; status="
                    + plan.Diagnostics.OutlineInformationStatus + " warnings=" + string.Join(" ; ", pending) + at);
            }
        }
    }
}
