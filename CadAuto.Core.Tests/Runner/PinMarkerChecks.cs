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
        private static void PinMarkerCapturedConcentricCirclesKeepOnly10H7()
        {
            // ASD6 run 20261025124834525; source SHA256 21BEABE1585B7DFBBF13F017001EA2F1047D108CF22DFA77977E5140F03B32B2.
            var inner = new Circle2D { Center = new Point2D(-18768.368176918328, -21075.736784968554), Radius = 5, SourceKey = "4BAFEC" };
            var outer = new Circle2D { Center = new Point2D(-18768.368176918335, -21075.736784994864), Radius = 5.50000000000409, SourceKey = "4BAFEB" };
            var marker = new Circle2D { Center = inner.Center, Radius = 5 };
            var config = DimensionRuleConfig.CreateDefault();
            foreach (var circles in new[] { new[] { outer, inner }, new[] { inner, outer } })
            {
                int match = PinHoleRules.FindMatchingCircle(circles, marker, config.GeometryTolerance);
                Assert(match >= 0 && circles[match] == inner, "marker radius must select the real R5 circle regardless of selection order");
                var holes = circles.Select((circle, index) => new HoleFeature2D {
                    Center = circle.Center, Diameter = circle.Diameter, SourceKey = circle.SourceKey,
                    Kind = index == match ? HoleKind2D.Pin : HoleKind2D.Normal,
                    FitTolerance = index == match ? config.PinHoleFitToleranceText : string.Empty }).ToList();
                var plans = new HoleCalloutPlanner(config).CreatePlans(holes, holes[match]);
                Assert(plans.Count == 2 && plans.Sum(p => p.Holes.Count) == 2, "both source diameters must remain available");
                Assert(plans.Single(p => p.Kind == HoleCalloutKind.Pin).Text == config.FormatHoleCallout(10, 1, config.PinHoleFitToleranceText), "only diameter 10 gets H7");
                Assert(plans.Single(p => p.Kind == HoleCalloutKind.Normal).Text == config.FormatHoleCallout(11, 1, string.Empty), "outer diameter 11 remains without pin tolerance");
            }
        }

        private static void PinMarkerRadiusSelectsSourceRatherThanSmallestCircle()
        {
            var circles = new[] { new Circle2D { Center = new Point2D(10, 20), Radius = 5 },
                new Circle2D { Center = new Point2D(10, 20), Radius = 5.5 } };
            var marker = new Circle2D { Center = circles[0].Center, Radius = 5.5 };
            Assert(PinHoleRules.FindMatchingCircle(circles, marker, 0.001) == 1, "a real outer-sized marker must select the outer circle");
            marker.Radius = 4;
            Assert(PinHoleRules.FindMatchingCircle(circles, marker, 0.001) == -1, "inconsistent marker size must not guess the smallest hole");
        }

        private static void PinMarkerMatchingPreservesTransformedAndSeparateHoles()
        {
            foreach (double scale in new[] { 0.1, 1.0, 10.0 })
            foreach (double angle in new[] { 0.0, 0.37, Math.PI / 2 })
            foreach (double mirror in new[] { -1.0, 1.0 })
            {
                var center = new Point2D(1234 + mirror * 20 * scale * Math.Cos(angle), -5678 + 20 * scale * Math.Sin(angle));
                var circles = new[] { new Circle2D { Center = center, Radius = 5.5 * scale },
                    new Circle2D { Center = center, Radius = 5 * scale },
                    new Circle2D { Center = new Point2D(center.X + 20 * scale, center.Y), Radius = 5 * scale } };
                var marker = new Circle2D { Center = center, Radius = 5 * scale };
                Assert(PinHoleRules.FindMatchingCircle(circles, marker, 0.001) == 1, "rotation, mirror, scale and same-sized foreign holes must preserve ownership");
                marker.Center = circles[2].Center;
                Assert(PinHoleRules.FindMatchingCircle(circles, marker, 0.001) == 2, "each marker must select its own physical location");
            }
        }

        private static void PinMarkerMatchingRejectsAmbiguousAndInvalidEvidence()
        {
            var circle = new Circle2D { Center = new Point2D(0, 0), Radius = 5 };
            var outer = new Circle2D { Center = circle.Center, Radius = 5.5 };
            var marker = new Circle2D { Center = circle.Center, Radius = 0 };
            Assert(PinHoleRules.FindMatchingCircle(new[] { outer, circle }, marker, 0.001) == -1, "a marker without radius cannot resolve concentric circles");
            Assert(PinHoleRules.FindMatchingCircle(new[] { circle }, marker, 0.001) == 0, "a unique existing circle still supports an unsized marker");
            marker.Center = new Point2D(0.7, 0);
            var small = new Circle2D { Center = circle.Center, Radius = 0.4 };
            Assert(PinHoleRules.FindMatchingCircle(new[] { small }, marker, 0.001) == 0, "the legacy one-unit fallback remains for a unique circle");
            marker.Center = circle.Center; marker.Radius = 5;
            Assert(PinHoleRules.FindMatchingCircle(new[] { circle, circle }, marker, 0.001) == -1, "duplicate source evidence is ambiguous");
            foreach (double tolerance in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
                Assert(PinHoleRules.FindMatchingCircle(new[] { circle }, marker, tolerance) == -1, "invalid tolerance cannot prove a match");
            marker.Radius = double.NaN;
            Assert(PinHoleRules.FindMatchingCircle(new[] { circle }, marker, 0.001) == -1, "invalid marker radius is rejected");
            Assert(PinHoleRules.FindMatchingCircle(new[] { circle }, null, 0.001) == -1, "an unmarked circle cannot become a pin");
        }
    }
}
