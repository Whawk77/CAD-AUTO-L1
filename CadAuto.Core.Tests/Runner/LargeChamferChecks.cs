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
        // Real selected outline from run 20261030131728033, translated without changing its geometry.
        private static OutlineFeature2D CreateLargeChamferFixture()
        {
            var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 99.999998624, MaxY = 93.588378257 };
            AddSegment(o, new Point2D(0, 92.588378257), new Point2D(1.000000031, 93.588378257), "684D62");
            AddSegment(o, new Point2D(19.000000031, 93.588378257), new Point2D(1.000000031, 93.588378257), "684D60");
            AddSegment(o, new Point2D(20, 92.588378257), new Point2D(0, 92.588378257), "684D5E");
            AddSegment(o, new Point2D(0, 92.588378257), new Point2D(0, 35), "684D5D");
            AddSegment(o, new Point2D(20, 92.588378257), new Point2D(19.000000031, 93.588378257), "684D61");
            AddSegment(o, new Point2D(20, 45), new Point2D(20, 92.588378257), "684D59");
            AddSegment(o, new Point2D(0, 35), new Point2D(35, 0), "684D5C");
            AddSegment(o, new Point2D(45, 20), new Point2D(20, 45), "684D5A");
            AddSegment(o, new Point2D(35, 0), new Point2D(99.999998624, 0), "684D58");
            AddSegment(o, new Point2D(99.999998624, 20), new Point2D(45, 20), "684D57");
            AddSegment(o, new Point2D(99.999998624, 0), new Point2D(99.999998624, 20), "684D5B");
            return o;
        }

        private static OutlineFeature2D CreateSingleChamferPlate(double legX, double legY)
        {
            var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 100, MaxY = 80 };
            AddSegment(o, new Point2D(0, legY), new Point2D(legX, 0), "SLOPE");
            AddSegment(o, new Point2D(legX, 0), new Point2D(100, 0), "BOTTOM");
            AddSegment(o, new Point2D(100, 0), new Point2D(100, 80), "RIGHT");
            AddSegment(o, new Point2D(100, 80), new Point2D(0, 80), "TOP");
            AddSegment(o, new Point2D(0, 80), new Point2D(0, legY), "LEFT");
            return o;
        }

        private static void ChamferLegsMustMatchWithinAbsoluteTolerance()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                string at = " angle=" + angle + " mirror=" + mirror;
                var accepted = TransformCapturedSelection(CreateSingleChamferPlate(35, 34.995), angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(accepted);
                Assert(accepted.Chamfers.Count(c => c.SourceSegment != null && c.SourceSegment.SourceKey == "SLOPE") == 1,
                    "legs within 0.01 are a drawn 45-degree chamfer" + at);
                // 33.25 is 5% short of 35, which the old relative rule accepted and labeled C35.
                var rejected = TransformCapturedSelection(CreateSingleChamferPlate(35, 33.25), angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(rejected);
                Assert(!rejected.Chamfers.Any(c => c.SourceSegment != null && c.SourceSegment.SourceKey == "SLOPE"),
                    "legs differing by 1.75 are a body slope, not a chamfer" + at);
                var plan = new DimensionPlanner(config).CreateOutlinePlan(rejected);
                // The slope ends are located by the straight edges: 100-65=35 and 80-46.75=33.25.
                Assert(plan.Diagnostics.OutlineInformationStatus == "CompleteForRecognizedBoundaryRequirements"
                    && plan.Dimensions.Any(d => d.SourceGeometryIds.Contains("BOTTOM") && Math.Abs(GetSpan(d) - 65) < 0.001)
                    && plan.Dimensions.Any(d => d.SourceGeometryIds.Contains("LEFT") && Math.Abs(GetSpan(d) - 46.75) < 0.001),
                    "an unrecognized slope stays fully defined by its adjacent straight edges" + at);
            }
        }

        // Real selected outline from run 20261030003336130, translated without changing its geometry.
        private static OutlineFeature2D CreateStepRiserChamferFixture()
        {
            var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 155.500002186, MaxY = 94.999774816 };
            AddSegment(o, new Point2D(38.999998995, 88.000021654), new Point2D(126.499997055, 88.000025479), "6838C5");
            AddSegment(o, new Point2D(133.499996749, 94.999773854), new Point2D(126.499997055, 88.000025479), "6838C4");
            AddSegment(o, new Point2D(38.999999563, 75.000001705), new Point2D(38.999998995, 88.000021654), "6838C2");
            AddSegment(o, new Point2D(133.499996749, 94.999773854), new Point2D(155.499998689, 94.999774816), "6838A9");
            AddSegment(o, new Point2D(29, 65.000001268), new Point2D(38.999999563, 75.000001705), "6838C3");
            AddSegment(o, new Point2D(155.500002186, 14.999774816), new Point2D(155.499998689, 94.999774816), "6838AA");
            AddSegment(o, new Point2D(0, 65), new Point2D(29, 65.000001268), "6838C1");
            AddSegment(o, new Point2D(155.500002186, 14.999774816), new Point2D(35.000002186, 14.999769549), "6838AB");
            AddSegment(o, new Point2D(0.000002841, 0), new Point2D(0, 65), "6838A8");
            AddSegment(o, new Point2D(35.000002186, 14.999769549), new Point2D(20.000002841, 0.000000874), "6838A6");
            AddSegment(o, new Point2D(20.000002841, 0.000000874), new Point2D(0.000002841, 0), "6838A7");
            return o;
        }

        private static void StepRiserChamfersBetweenParallelFacesAreRecognized()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            // Rotating by 90 degrees turns the horizontal-horizontal risers into vertical-vertical ones.
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                string at = " angle=" + angle + " mirror=" + mirror;
                var o = TransformCapturedSelection(CreateStepRiserChamferFixture(), angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                foreach (var expected in new[] { Tuple.Create("6838C4", 7.0), Tuple.Create("6838C3", 10.0), Tuple.Create("6838A6", 15.0) })
                {
                    Assert(o.Chamfers.Count(c => c.SourceSegment != null && c.SourceSegment.SourceKey == expected.Item1
                        && Math.Abs(c.Value - expected.Item2) < 0.001) == 1,
                        "a 45-degree edge is a chamfer between perpendicular faces or between two offset parallel faces; source="
                        + expected.Item1 + " C" + expected.Item2 + at);
                }
                var plan = new DimensionPlanner(config).CreateOutlinePlan(o);
                Assert(!plan.Dimensions.Any(d => d.DebugRole == "BoundaryObliqueProjection"
                    && d.SourceGeometryIds.Any(id => id == "6838C4" || id == "6838C3" || id == "6838A6")),
                    "recognized chamfers use leader callouts, not slope projections" + at);
                // The per-datum layout is checked by DatumBaselinePlannerReplacesStepFaceLengths.
                string dims = "dims=[" + string.Join(",", plan.Dimensions.Select(d => d.Orientation.ToString()[0] + GetSpan(d).ToString("0.###"))) + "]";
                Assert(new[] { 155.5, 95.0 }.All(span => plan.Dimensions.Any(d => Math.Abs(GetSpan(d) - span) < 0.01))
                    && !plan.Dimensions.Any(d => Math.Abs(GetSpan(d) - 87.5) < 0.01),
                    "the overall dimensions stay and the top face is located from the datum instead of by its own length 87.5; " + dims + at);
            }
        }

        private static void AcuteDovetailSlopeIsNotChamfer()
        {
            var config = DimensionRuleConfig.CreateDefault();
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 60, MaxY = 20 };
                AddSegment(o, new Point2D(0, 0), new Point2D(60, 0), "BOTTOM");
                AddSegment(o, new Point2D(60, 0), new Point2D(60, 10), "RIGHT");
                AddSegment(o, new Point2D(60, 10), new Point2D(40, 10), "SHELF");
                AddSegment(o, new Point2D(40, 10), new Point2D(50, 20), "SLOPE");
                AddSegment(o, new Point2D(50, 20), new Point2D(0, 20), "TOP");
                AddSegment(o, new Point2D(0, 20), new Point2D(0, 0), "LEFT");
                o = TransformCapturedSelection(o, angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                Assert(!o.Chamfers.Any(c => c.SourceSegment != null && c.SourceSegment.SourceKey == "SLOPE"),
                    "a 45-degree slope whose parallel face runs underneath it forms an acute tip, not a chamfer; angle=" + angle + " mirror=" + mirror);
            }
        }

        private static void LargeAxisChamfersUseLeaderNotLinearProjection()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                string at = " angle=" + angle + " mirror=" + mirror;
                var o = TransformCapturedSelection(CreateLargeChamferFixture(), angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                foreach (var expected in new[] { Tuple.Create("684D5C", 35.0), Tuple.Create("684D5A", 25.0),
                    Tuple.Create("684D62", 1.0), Tuple.Create("684D61", 1.0) })
                {
                    Assert(o.Chamfers.Count(c => c.SourceSegment != null && c.SourceSegment.SourceKey == expected.Item1
                        && Math.Abs(c.Value - expected.Item2) < 0.001) == 1,
                        "a 45-degree edge between horizontal and vertical edges is a chamfer regardless of size; source="
                        + expected.Item1 + " C" + expected.Item2 + at);
                }
                var plan = new DimensionPlanner(config).CreateOutlinePlan(o);
                // Arm widths may cite the slope as supporting evidence; only dimensions measuring the chamfer itself are wrong.
                var onChamfer = plan.Dimensions.Where(d => d.Role != DimensionCandidateRole.Overall
                    && (d.DebugRole == "BoundaryObliqueProjection"
                        || new[] { "684D5C", "684D5A" }.Contains(d.SourceGeometryIds.FirstOrDefault()))).ToList();
                Assert(onChamfer.Count == 0, "chamfer edges use leader callouts, not linear dimensions; found=["
                    + string.Join(",", onChamfer.Select(d => d.DebugRole + ":" + GetSpan(d).ToString("0.###") + ":" + string.Join("+", d.SourceGeometryIds))) + "]" + at);
                Assert(!plan.Dimensions.Any(d => d.SourceGeometryIds.Contains("684D58") && Math.Abs(GetSpan(d) - 65) < 0.001),
                    "the straight edge stops at the chamfer cut point and is covered by overall 100" + at);
                Assert(plan.Dimensions.Count(d => d.Role == DimensionCandidateRole.Overall) == 2
                    && plan.Dimensions.Any(d => d.Role == DimensionCandidateRole.Overall && Math.Abs(GetSpan(d) - 99.999998624) < 0.001)
                    && plan.Dimensions.Any(d => d.Role == DimensionCandidateRole.Overall && Math.Abs(GetSpan(d) - 93.588378257) < 0.001),
                    "both overall dimensions remain" + at);
            }
        }
    }
}
