using System;
using CadAuto.Core.Geometry;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        private static void SplitCircleHoleCapturedLeftViewClosesThreeHoles()
        {
            // test.dwg SHA256 D31074DA7D5721A785BFF6F7506965D1F091615060DFD9745B1E69DE3236653C.
            // Selected DRAWING/HIDDEN pairs: DB6C09/DB6C05, DB6C0A/DB6C00, DB6C0B/DB6BFF.
            double[][] captured = {
                new[] {141379.62750827265, -34797.25716648712, 3.32349999999997, 3.14159260986856, 6.28318526347291},
                new[] {141409.62750827265, -34797.25716779848, 3.32349999999997, 3.14159260986856, 6.28318526348018},
                new[] {141394.62750827262, -34797.25716714272, 4.0, 3.14159260989039, 6.28318526345836}
            };
            foreach (var c in captured)
            {
                var front = SplitCircleArc(c[0], c[1], c[2], c[3], c[4]);
                var back = SplitCircleArc(c[0], c[1], c[2], c[4], c[3]);
                Assert(FullCircleArcRules.AreComplementarySemicircles(front, back, 0.001), "captured halves close a real hole");
                Assert(FullCircleArcRules.AreComplementarySemicircles(back, front, 0.001), "selection order is irrelevant");
            }
            Assert(FullCircleArcRules.AreComplementarySemicircles(
                SplitCircleArc(0, 0, 3.3235, 0, Math.PI), SplitCircleArc(0, 0, 3.3235, Math.PI, 0), 0.001), "minor diameter proof supports the same M8 confirmation as a Circle");
            Assert(DimensionRuleConfig.CreateDefault().FormatThreadCallout(8) == "M8", "thread label uses major diameter, not minor radius");
        }

        private static void SplitCircleHolePreservesRotatedScaledAndMirroredPairs()
        {
            foreach (double radius in new[] { 0.4, 4.0, 40.0 })
            foreach (double angle in new[] { 0.0, 0.37, Math.PI, 1.5 * Math.PI })
            {
                var first = SplitCircleArc(12345, -67890, radius, angle, angle + Math.PI);
                var second = SplitCircleArc(12345, -67890, radius, angle + Math.PI, angle);
                Assert(FullCircleArcRules.AreComplementarySemicircles(first, second, 0.001), "rigid transforms and scale preserve closure");
                var start = first.Start; first.Start = first.End; first.End = start; first.Bulge = -first.Bulge;
                start = second.Start; second.Start = second.End; second.End = start; second.Bulge = -second.Bulge;
                Assert(FullCircleArcRules.AreComplementarySemicircles(first, second, 0.001), "reversed winding preserves closure");
            }
        }

        private static void SplitCircleHoleRejectsOpenDuplicateAndUnrelatedArcs()
        {
            var first = SplitCircleArc(10, 40, 4, 0, Math.PI);
            Assert(!FullCircleArcRules.AreComplementarySemicircles(first, first, 0.001), "duplicate half is not a circle");
            Assert(!FullCircleArcRules.AreComplementarySemicircles(first, SplitCircleArc(40, 40, 4, Math.PI, 0), 0.001), "same diameter different centre is not a circle");
            Assert(!FullCircleArcRules.AreComplementarySemicircles(first, SplitCircleArc(10, 40, 4.1, Math.PI, 0), 0.001), "mismatched radius is not a circle");
            Assert(!FullCircleArcRules.AreComplementarySemicircles(first, SplitCircleArc(10, 40, 4, Math.PI, 2 * Math.PI - 0.01), 0.001), "open gap remains unrecognized");
            Assert(!FullCircleArcRules.AreComplementarySemicircles(first, SplitCircleArc(10, 40, 4, Math.PI, 2 * Math.PI + 0.01), 0.001), "overlapping sweeps do not prove closure");
            var opposite = SplitCircleArc(10, 40, 4, Math.PI, 0); opposite.Bulge = -opposite.Bulge;
            Assert(!FullCircleArcRules.AreComplementarySemicircles(first, opposite, 0.001), "opposite bulge with reversed endpoints covers the same half");
            opposite = SplitCircleArc(10, 40, 4, Math.PI, 0); opposite.Bulge = 0.5;
            Assert(!FullCircleArcRules.AreComplementarySemicircles(first, opposite, 0.001), "endpoint coincidence alone does not prove a semicircle");
        }

        private static void SplitCircleHoleRejectsInvalidEvidence()
        {
            var first = SplitCircleArc(0, 0, 4, 0, Math.PI);
            var second = SplitCircleArc(0, 0, 4, Math.PI, 0);
            foreach (double tolerance in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
                Assert(!FullCircleArcRules.AreComplementarySemicircles(first, second, tolerance), "invalid tolerance cannot prove a hole");
            Assert(!FullCircleArcRules.AreComplementarySemicircles(first, null, 0.001), "one half cannot prove a hole");
            second.Radius = double.PositiveInfinity;
            Assert(!FullCircleArcRules.AreComplementarySemicircles(first, second, 0.001), "infinite radius rejected");
            second = SplitCircleArc(0, 0, 4, Math.PI, 0); second.Center = new Point2D(double.NaN, 0);
            Assert(!FullCircleArcRules.AreComplementarySemicircles(first, second, 0.001), "non-finite centre rejected");
        }

        private static Arc2D SplitCircleArc(double x, double y, double radius, double start, double end)
        {
            double sweep = end - start;
            while (sweep <= 0) sweep += 2 * Math.PI;
            return new Arc2D { Center = new Point2D(x, y), Radius = radius,
                Start = new Point2D(x + radius * Math.Cos(start), y + radius * Math.Sin(start)),
                End = new Point2D(x + radius * Math.Cos(end), y + radius * Math.Sin(end)), Bulge = Math.Tan(sweep / 4) };
        }
    }
}
