using System;
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
        private static OutlineFeature2D FragmentRectangle(params Point2D[] bottom)
        {
            // 底边由给定点串组成，其余三边完整；10×5 矩形。
            var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 10, MaxY = 5 };
            for (int i = 0; i + 1 < bottom.Length; i++) { AddSegment(o, bottom[i], bottom[i + 1], "b" + i); }
            AddSegment(o, new Point2D(10, 0), new Point2D(10, 5), "right");
            AddSegment(o, new Point2D(10, 5), new Point2D(0, 5), "top");
            AddSegment(o, new Point2D(0, 5), new Point2D(0, 0), "left");
            return o;
        }

        private static Arc2D[] SplitArcInTwo(Arc2D arc)
        {
            double sweep = 4 * Math.Atan(arc.Bulge);
            double a0 = Math.Atan2(arc.Start.Y - arc.Center.Y, arc.Start.X - arc.Center.X);
            var mid = new Point2D(arc.Center.X + arc.Radius * Math.Cos(a0 + sweep / 2), arc.Center.Y + arc.Radius * Math.Sin(a0 + sweep / 2));
            double bulge = Math.Tan(sweep / 8);
            return new[]
            {
                new Arc2D { Start = arc.Start, End = mid, Center = arc.Center, Radius = arc.Radius, Bulge = bulge, SourceKey = arc.SourceKey + "a" },
                new Arc2D { Start = mid, End = arc.End, Center = arc.Center, Radius = arc.Radius, Bulge = bulge, SourceKey = arc.SourceKey + "b" }
            };
        }

        private static void OutlineFragmentRulesMergeOnlyCoincidentContinuations()
        {
            double tol = DimensionRuleConfig.CreateDefault().GeometryTolerance;
            Func<OutlineFeature2D, int> bottomCount = o => o.Segments.Count(s => s.IsHorizontal(tol) && Math.Abs(s.MinY) <= 0.01);

            var two = FragmentRectangle(new Point2D(0, 0), new Point2D(4, 0), new Point2D(10, 0));
            var r = OutlineFragmentRules.Merge(two, tol);
            var merged = r.Outline.Segments.Single(s => s.IsHorizontal(tol) && Math.Abs(s.MinY) <= tol);
            Assert(r.Records.Count == 1 && merged.Length > 10 - tol && merged.SourceKeys.SequenceEqual(new[] { "b0", "b1" })
                && r.Outline.Segments.Count == 4 && two.Segments.Count == 5,
                "a line split in two becomes one edge with both sources, without editing the input outline");

            var three = FragmentRectangle(new Point2D(10, 0), new Point2D(7, 0), new Point2D(3, 0), new Point2D(0, 0));
            Assert(bottomCount(OutlineFragmentRules.Merge(three, tol).Outline) == 1, "a line split in three becomes one edge");

            var branch = FragmentRectangle(new Point2D(0, 0), new Point2D(4, 0), new Point2D(10, 0));
            AddSegment(branch, new Point2D(4, 0), new Point2D(4, 3), "branch");
            Assert(bottomCount(OutlineFragmentRules.Merge(branch, tol).Outline) == 2, "a third line at the join is a branch and blocks merging");

            var crossing = FragmentRectangle(new Point2D(0, 0), new Point2D(4, 0), new Point2D(10, 0));
            AddSegment(crossing, new Point2D(4, -1), new Point2D(4, 3), "through");
            Assert(bottomCount(OutlineFragmentRules.Merge(crossing, tol).Outline) == 2, "a line passing through the join also blocks merging");

            var turn = FragmentRectangle(new Point2D(0, 0), new Point2D(4, 0), new Point2D(10, 0.02));
            Assert(OutlineFragmentRules.Merge(turn, tol).Records.Count == 0, "a real 0.02 turn is not merged");

            var drift = FragmentRectangle(new Point2D(0, 0), new Point2D(3, 0.0008), new Point2D(6, 0.0032), new Point2D(10, 0));
            Assert(OutlineFragmentRules.Merge(drift, tol).Outline.Segments.Count(s => s.SourceKeys != null || s.SourceKey.StartsWith("b")) > 1,
                "slowly drifting fragments cannot be straightened beyond tolerance");

            var gap = FragmentRectangle(new Point2D(0, 0), new Point2D(4, 0));
            AddSegment(gap, new Point2D(4.01, 0), new Point2D(10, 0), "b1");
            Assert(OutlineFragmentRules.Merge(gap, tol).Records.Count == 0, "fragments separated by a gap are not merged");

            var tangent = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 10, MaxY = 10 };
            AddSegment(tangent, new Point2D(0, 0), new Point2D(7, 0), "line");
            tangent.Arcs.Add(new Arc2D { Start = new Point2D(7, 0), End = new Point2D(10, 3), Center = new Point2D(7, 3), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "arc" });
            Assert(OutlineFragmentRules.Merge(tangent, tol).Records.Count == 0, "a line meeting a tangent arc is a real fillet boundary");

            var plain = FragmentRectangle(new Point2D(0, 0), new Point2D(10, 0));
            Assert(ReferenceEquals(OutlineFragmentRules.Merge(plain, tol).Outline, plain), "an outline without fragments is returned unchanged");

            // 圆角画成两段 45° 圆弧，并带有生产识别器生成的弦线。
            var rounded = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 10, MaxY = 10 };
            AddSegment(rounded, new Point2D(0, 0), new Point2D(7, 0), "bottom");
            AddSegment(rounded, new Point2D(10, 3), new Point2D(10, 10), "right");
            AddSegment(rounded, new Point2D(10, 10), new Point2D(0, 10), "top");
            AddSegment(rounded, new Point2D(0, 10), new Point2D(0, 0), "left");
            var whole = new Arc2D { Start = new Point2D(7, 0), End = new Point2D(10, 3), Center = new Point2D(7, 3), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "R3" };
            foreach (var piece in SplitArcInTwo(whole))
            {
                rounded.Arcs.Add(piece);
                rounded.Segments.Add(new Segment2D(piece.Start, piece.End) { SourceKey = piece.SourceKey, IsArcChord = true });
                rounded.Fillets.Add(new FilletFeature2D { Center = piece.Center, Radius = 3, StartPoint = piece.Start, EndPoint = piece.End, SourceArc = piece, Text = "R3" });
            }
            var ra = OutlineFragmentRules.Merge(rounded, tol).Outline;
            var arc = ra.Arcs.Single();
            Assert(arc.Start.DistanceTo(whole.Start) <= tol && arc.End.DistanceTo(whole.End) <= tol && Math.Abs(arc.Bulge - whole.Bulge) <= 1e-9
                && arc.SourceKeys.SequenceEqual(new[] { "R3a", "R3b" }),
                "two concentric arc fragments become one 90 degree arc");
            Assert(ra.Segments.Count(s => s.IsArcChord) == 1 && ra.Fillets.Count == 1 && ReferenceEquals(ra.Fillets[0].SourceArc, arc)
                && ra.Fillets[0].StartPoint.DistanceTo(whole.Start) <= tol && ra.Fillets[0].EndPoint.DistanceTo(whole.End) <= tol,
                "the chords and fillet features follow the merged arc");

            var other = new OutlineFeature2D();
            var pieces = SplitArcInTwo(whole);
            pieces[1].Radius = 3.5;
            other.Arcs.AddRange(pieces);
            Assert(OutlineFragmentRules.Merge(other, tol).Records.Count == 0, "arcs with different radii are not merged");
        }

        private static void OutlineFragmentSplitChamferAndFilletUseLProfileFourWay()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            foreach (bool splitFillet in new[] { false, true })
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                var fixture = CreateSplitBoundaryFixture();
                if (splitFillet)
                {
                    var fillet = fixture.Arcs.Single(a => a.SourceKey == "685B7F");
                    fixture.Arcs.Remove(fillet);
                    fixture.Arcs.AddRange(SplitArcInTwo(fillet));
                }
                var o = TransformCapturedSelection(fixture, angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                var plan = new DimensionPlanner(config).CreateOutlinePlan(o);
                string context = " split=" + splitFillet + " angle=" + angle + " mirror=" + mirror + " dims="
                    + string.Join(",", plan.Dimensions.Select(d => (d.RuleId ?? d.DebugRole) + ":" + Math.Round(GetSpan(d), 3)))
                    + " warn=" + string.Join(";", plan.Diagnostics.Warnings.Where(w => w.StartsWith("LProfile.NotApplied") || w.StartsWith("OutlineFragment")));
                Assert(plan.Diagnostics.Warnings.Any(w => w.StartsWith("OutlineFragment.Merged|Kind=Line|") && w.Contains("685B95") && w.Contains("685B96")),
                    "the split chamfer must be merged and recorded" + context);
                Assert(!splitFillet || plan.Diagnostics.Warnings.Any(w => w.StartsWith("OutlineFragment.Merged|Kind=Arc|") && w.Contains("685B7Fa")),
                    "the split fillet must be merged and recorded" + context);
                var values = plan.Dimensions.Select(GetSpan).OrderBy(v => v).ToList();
                Assert(plan.Dimensions.Any(d => d.RuleId == "LProfile.VerticalArmWidth" && Math.Abs(GetSpan(d) - 20) < 0.001)
                    && plan.Dimensions.Any(d => d.RuleId == "LProfile.HorizontalArmHeight" && Math.Abs(GetSpan(d) - 20) < 0.001)
                    && values.Count == 4 && values.Zip(new[] { 20.0, 20, 50, 59 }, (a, b) => Math.Abs(a - b) < 0.001).All(x => x),
                    "the L profile rule must measure both arm thicknesses with the overall dimensions" + context);
                Assert(!plan.Diagnostics.DimensionCandidates.Any(c => Math.Abs(c.Value - 0.80851896) < 0.001 || Math.Abs(c.Value - 0.1652569) < 0.001),
                    "fragment lengths at a collinear join must not become candidates" + context);
            }
        }
    }
}
