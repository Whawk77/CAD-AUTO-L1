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
        private static void PinMarkerPriorityCapturedBackPinCompletesPair()
        {
            // ASD run 20261027181040450: back marker 684E1A has 277.1808/82.8192-degree arcs, no Circle.
            foreach (bool reverse in new[] { false, true })
            {
                var circles = new[] {
                    new Circle2D { Center = new Point2D(103238.25609783638, 8534.911242711216), Radius = 4.5, SourceKey = "684E0E" },
                    new Circle2D { Center = new Point2D(103238.256102596, 8519.91124271122), Radius = 4, SourceKey = "684E0F" },
                    new Circle2D { Center = new Point2D(103238.25610735487, 8504.91124271122), Radius = 4.5, SourceKey = "684E10" }
                }.ToList();
                var markers = new[] {
                    new Circle2D { Center = circles[1].Center, Radius = 4, SourceKey = "684E19" },
                    new Circle2D { Center = new Point2D(103238.2560930775, 8549.911242711216), Radius = 4, SourceKey = "684E1A" }
                };
                if (reverse) { circles.Reverse(); Array.Reverse(markers); }
                var pins = markers.Select(marker => PinHoleRules.ResolveMarkedCircle(circles, marker, 0.001)).ToList();
                Assert(pins.All(pin => pin != null), "both selected markers must resolve a pin, including the back marker without a Circle");
                foreach (var pin in pins.Where(pin => !circles.Contains(pin))) circles.Add(pin);
                var config = DimensionRuleConfig.CreateDefault();
                var holes = circles.Select(circle => new HoleFeature2D {
                    Center = new Point2D(circle.Center.X - 103228.25609213, circle.Center.Y - 8494.91123954),
                    Diameter = circle.Diameter, SourceKey = circle.SourceKey,
                    Kind = pins.Contains(circle) ? HoleKind2D.Pin : HoleKind2D.Normal,
                    FitTolerance = pins.Contains(circle) ? config.PinHoleFitToleranceText : string.Empty }).ToList();
                var datumPin = holes.Single(hole => hole.SourceKey == "684E0F");
                var planner = new DimensionPlanner(config);
                var groups = planner.BuildPinGroupPlan(holes, datumPin);
                Assert(groups.Count == 1 && groups[0].Pins.Count == 2 && groups[0].MemberHoles.Count == 4,
                    "the captured part must retain two pins and two separate normal holes in one pin group");
                Assert(groups[0].Pins.Any(pin => pin.SourceKey == "684E1A" && Math.Abs(pin.Diameter - 8) < 0.001),
                    "the recovered back pin keeps its actual marker identity and measured diameter");
                var outline = CreateRectangle(20, 78);
                var datum = Datum2D.FromOutline(outline);
                datum.DatumHole = datumPin;
                var plan = planner.CreateDimensionPlan(outline, datum, holes);
                Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.PinDistance && Math.Abs(GetSpan(d) - 30) < 0.001),
                    "the real pin pair must generate its thirty-unit pin spacing");
                var callouts = new HoleCalloutPlanner(config).CreatePlans(holes, datumPin);
                Assert(callouts.Count == 2 && callouts.Single(p => p.Kind == HoleCalloutKind.Pin).Holes.Count == 2
                    && callouts.Single(p => p.Kind == HoleCalloutKind.Normal).Holes.Count == 2,
                    "two pin holes and two normal holes retain their own diameters and kinds");
            }
        }

        private static void PinMarkerPriorityMeasuredMarkerSurvivesMissingCircle()
        {
            foreach (double scale in new[] { 0.1, 1.0, 10.0 })
            foreach (double angle in new[] { 0.0, 0.37, Math.PI / 2 })
            foreach (double mirror in new[] { -1.0, 1.0 })
            {
                var marker = new Circle2D { Center = new Point2D(1234 + mirror * 20 * scale * Math.Cos(angle), -5678 + 20 * scale * Math.Sin(angle)),
                    Radius = 4 * scale, SourceKey = "back-marker" };
                var other = new Circle2D { Center = new Point2D(marker.Center.X + 15 * scale, marker.Center.Y), Radius = 4 * scale };
                var concentric = new Circle2D { Center = marker.Center, Radius = 4.5 * scale };
                foreach (var circles in new[] { new Circle2D[0], new[] { other }, new[] { concentric, other } })
                {
                    var pin = PinHoleRules.ResolveMarkedCircle(circles, marker, 0.001);
                    Assert(ReferenceEquals(pin, marker) && pin.SourceKey == "back-marker",
                        "a measured marker proves its own pin without borrowing a nearby or differently sized circle");
                }
            }
        }

        private static void PinMarkerPriorityKeepsExistingSourcesAndSeparateDiameters()
        {
            var inner = new Circle2D { Center = new Point2D(10, 20), Radius = 5, SourceKey = "inner" };
            var outer = new Circle2D { Center = inner.Center, Radius = 5.5, SourceKey = "outer" };
            var marker = new Circle2D { Center = inner.Center, Radius = 5, SourceKey = "marker" };
            Assert(ReferenceEquals(PinHoleRules.ResolveMarkedCircle(new[] { outer, inner }, marker, 0.001), inner),
                "a unique matching Circle remains the diameter source ahead of the marker bounds");
            Assert(ReferenceEquals(PinHoleRules.ResolveMarkedCircle(new[] { inner, inner, outer }, marker, 0.001), marker),
                "duplicate circle evidence cannot erase a pin independently proved by its measured marker");
            marker.Radius = 0;
            Assert(ReferenceEquals(PinHoleRules.ResolveMarkedCircle(new[] { inner }, marker, 0.001), inner),
                "an unsized marker can still use a unique existing Circle");
        }

        private static void PinMarkerPriorityRejectsUnmeasuredGeometry()
        {
            var marker = new Circle2D { Center = new Point2D(10, 20), Radius = 4 };
            foreach (double tolerance in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
                Assert(PinHoleRules.ResolveMarkedCircle(null, marker, tolerance) == null, "invalid tolerance cannot create a measured pin");
            Assert(PinHoleRules.ResolveMarkedCircle(null, null, 0.001) == null, "an unmarked location cannot become a pin");
            foreach (double radius in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
            {
                marker.Radius = radius;
                Assert(PinHoleRules.ResolveMarkedCircle(null, marker, 0.001) == null, "missing or invalid size must not invent a pin diameter");
            }
            marker.Radius = 4;
            marker.Center = new Point2D(double.NaN, 20);
            Assert(PinHoleRules.ResolveMarkedCircle(null, marker, 0.001) == null, "nonfinite marker geometry is rejected");
        }
    }
}
