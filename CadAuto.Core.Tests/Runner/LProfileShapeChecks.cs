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
        // 实图 run 20261102010053897（平移到原点，几何不变）：102×68 L 型，内角处有一个深 2 的小台阶，三个 R3。
        private static OutlineFeature2D CreateLocalStepLProfileFixture()
        {
            var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 102, MaxY = 68 };
            AddSegment(o, new Point2D(102, 45), new Point2D(32, 45), "680C0D");
            AddSegment(o, new Point2D(32, 45), new Point2D(32, 43), "680C0C");
            AddSegment(o, new Point2D(32, 43), new Point2D(28, 43), "680C0B");
            AddSegment(o, new Point2D(25, 40), new Point2D(25, 3), "680BFD");
            AddSegment(o, new Point2D(22, 0), new Point2D(0, 0), "680BFF");
            AddSegment(o, new Point2D(0, 0), new Point2D(0, 68), "680C08");
            AddSegment(o, new Point2D(0, 68), new Point2D(99, 68), "680C09");
            AddSegment(o, new Point2D(102, 65), new Point2D(102, 45), "680C0E");
            o.Arcs.Add(new Arc2D { Start = new Point2D(28, 43), End = new Point2D(25, 40), Center = new Point2D(28, 40), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "680BFC" });
            o.Arcs.Add(new Arc2D { Start = new Point2D(22, 0), End = new Point2D(25, 3), Center = new Point2D(22, 3), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "680BFE" });
            o.Arcs.Add(new Arc2D { Start = new Point2D(102, 65), End = new Point2D(99, 68), Center = new Point2D(99, 65), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "680C0A" });
            return o;
        }

        // 实图 run 20261102003401989（平移到原点）：110×80 L 型，C10 倒角、三个 R3，顶边被一条内部竖线截断。
        private static OutlineFeature2D CreateBranchSplitLProfileFixture()
        {
            const double cut = 52.41110187;
            var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 110, MaxY = 80 };
            AddSegment(o, new Point2D(107, 60), new Point2D(cut, 60), "682345");
            AddSegment(o, new Point2D(23, 60), new Point2D(cut, 60), "68233C");
            AddSegment(o, new Point2D(cut, 80), new Point2D(cut, 60), "68233B");
            AddSegment(o, new Point2D(110, 63), new Point2D(110, 80), "682343");
            AddSegment(o, new Point2D(110, 80), new Point2D(cut, 80), "682344");
            AddSegment(o, new Point2D(cut, 80), new Point2D(10, 80), "68233A");
            AddSegment(o, new Point2D(20, 3), new Point2D(20, 57), "682338");
            AddSegment(o, new Point2D(10, 80), new Point2D(0, 70), "682340");
            AddSegment(o, new Point2D(0, 70), new Point2D(0, 0), "682339");
            AddSegment(o, new Point2D(0, 0), new Point2D(17, 0), "68233F");
            o.Arcs.Add(new Arc2D { Start = new Point2D(107, 60), End = new Point2D(110, 63), Center = new Point2D(107, 63), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "682342" });
            o.Arcs.Add(new Arc2D { Start = new Point2D(23, 60), End = new Point2D(20, 57), Center = new Point2D(23, 57), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "68233D" });
            o.Arcs.Add(new Arc2D { Start = new Point2D(17, 0), End = new Point2D(20, 3), Center = new Point2D(17, 3), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "68233E" });
            return o;
        }

        // 正交 L 型，内角处一个台阶：竖臂厚 25、横臂厚 23；台阶两个方向的深度为 depthY（沿竖向）和 7（沿横向）。
        private static OutlineFeature2D CreateSteppedCornerOutline(double depthY)
        {
            var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 102, MaxY = 68 };
            Point2D[] p =
            {
                new Point2D(0, 0), new Point2D(25, 0), new Point2D(25, 45 - depthY), new Point2D(32, 45 - depthY),
                new Point2D(32, 45), new Point2D(102, 45), new Point2D(102, 68), new Point2D(0, 68)
            };
            for (int i = 0; i < p.Length; i++) { AddSegment(o, p[i], p[(i + 1) % p.Length], "s" + i); }
            return o;
        }

        private static LProfileShape ClassifyShape(OutlineFeature2D outline)
        {
            double tol = DimensionRuleConfig.CreateDefault().GeometryTolerance;
            return LProfileShapeRules.Classify(DimensionPlanner.InspectOutlineTopology(outline, tol), tol, DimensionRuleConfig.LProfileLocalStepMaxDepthRatio);
        }

        private static void LProfileShapeRulesClassifyVirtualLProfile()
        {
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                string at = " angle=" + angle + " mirror=" + mirror;
                var step = ClassifyShape(TransformCapturedSelection(CreateLocalStepLProfileFixture(), angle, mirror, false));
                Assert(step.Kind == LProfileShapeKind.LocalStep, "a shallow step at the inner corner is absorbed into the virtual L corner; " + step.Evidence + at);
                var branch = ClassifyShape(TransformCapturedSelection(CreateBranchSplitLProfileFixture(), angle, mirror, false));
                Assert(branch.Kind == LProfileShapeKind.Plain, "a face cut by an internal line is still one face, and C10/R3 corners are completed; " + branch.Evidence + at);
                var riser = ClassifyShape(TransformCapturedSelection(CreateStepRiserChamferFixture(), angle, mirror, false));
                Assert(!riser.IsLProfile, "chamfers between parallel faces are step risers, so the 155.5x95 part is not an L; " + riser.Evidence + at);
            }
            // 较浅方向的深度 min(depthY, 7) 与较薄臂厚 23 比较，阈值 25% 即 5.75。
            Assert(ClassifyShape(CreateSteppedCornerOutline(5.7)).Kind == LProfileShapeKind.LocalStep, "a 5.7 deep step is within 25% of the 23 arm");
            var deep = ClassifyShape(CreateSteppedCornerOutline(5.8));
            Assert(!deep.IsLProfile && deep.Evidence.Contains("StepTooDeep"), "a 5.8 deep step exceeds 25% of the 23 arm; " + deep.Evidence);
            var stair = ClassifyShape(CreateSteppedCornerOutline(20));
            Assert(!stair.IsLProfile, "a real two-level staircase is not an L; " + stair.Evidence);
            var rectangle = ClassifyShape(FragmentRectangle(new Point2D(0, 0), new Point2D(10, 0)));
            Assert(!rectangle.IsLProfile, "a rectangle has no concave corner; " + rectangle.Evidence);
        }

        private static void GrooveWallsMustBeOnOuterBoundary()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            double tol = config.GeometryTolerance;
            var outline = CreateBranchSplitLProfileFixture();
            var evidence = new StructureBoundaryEvidence(DimensionPlanner.InspectOutlineTopology(outline, tol), tol);
            var internalWall = new StructureFeature { Axis = StructureFeatureAxis.Horizontal, T0 = 20, T1 = 52.41110187, CrossPosition = 60 };
            Assert(evidence.RejectGrooveWithoutBoundaryWalls(internalWall, 6.4, 1.0)
                && internalWall.BoundaryMembership == "NonBoundary" && internalWall.BoundaryEvidence.Contains("Reason=GrooveWallNotOnBoundary|Wall=52.411"),
                "a groove whose wall is the internal line is rejected; evidence=" + internalWall.BoundaryEvidence);
            var realWalls = new StructureFeature { Axis = StructureFeatureAxis.Horizontal, T0 = 20, T1 = 110, CrossPosition = 60 };
            Assert(!evidence.RejectGrooveWithoutBoundaryWalls(realWalls, 6.4, 1.0), "a groove between two outer-boundary faces is kept");

            var mismatches = new System.Collections.Generic.List<string>();
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                var o = TransformCapturedSelection(CreateBranchSplitLProfileFixture(), angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                var plan = new DimensionPlanner(config).CreateOutlinePlan(o);
                var values = plan.Dimensions.Select(d => Math.Round(GetSpan(d), 3)).OrderBy(v => v).ToArray();
                if (!values.SequenceEqual(new[] { 20.0, 20, 80, 110 })) { mismatches.Add(angle + (mirror ? "M" : "") + "=[" + string.Join(",", values) + "]"); }
            }
            Assert(mismatches.Count == 0, "the internal line must not create groove or notch dimensions in any placement; " + string.Join(" ; ", mismatches));
        }

        // 实图 run 20261102234708589（平移到原点）：50×57 矩形外轮廓，内部 4 条通长横线（y=54、37、34、3）和
        // y=37~54 之间的两条竖线 x=15、35（E11、E9）。外轮廓只有 4 条边，内部线都不是外轮廓。
        private static OutlineFeature2D CreateInternalPocketRectangleFixture()
        {
            var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 50, MaxY = 57 };
            var n = new System.Collections.Generic.Dictionary<int, Point2D>
            {
                [0] = new Point2D(0, 57), [1] = new Point2D(50, 57), [2] = new Point2D(50, 54), [3] = new Point2D(0, 54),
                [4] = new Point2D(50, 37), [5] = new Point2D(35, 54), [6] = new Point2D(15, 54), [7] = new Point2D(0, 37),
                [8] = new Point2D(35, 37), [9] = new Point2D(50, 34), [10] = new Point2D(15, 37), [11] = new Point2D(0, 34),
                [12] = new Point2D(50, 3), [13] = new Point2D(0, 3), [14] = new Point2D(50, 0), [15] = new Point2D(0, 0)
            };
            var edges = new[]
            {
                (0, 1, "6826DF"), (2, 1, "6826DE"), (0, 3, "6826DD"), (2, 4, "6826DC"), (2, 5, "6826DB"), (3, 6, "6826CD"),
                (3, 7, "6826CB"), (8, 4, "6826DA"), (4, 9, "6826D8"), (5, 8, "6826CA"), (6, 5, "6826C7"), (6, 10, "6826C8"),
                (7, 11, "6826D9"), (10, 7, "6826CC"), (8, 10, "6826C9"), (9, 12, "6826D4"), (9, 11, "6826D2"), (11, 13, "6826D3"),
                (12, 13, "6826D1"), (12, 14, "6826D0"), (13, 15, "6826CF"), (14, 15, "6826CE")
            };
            foreach (var e in edges) { AddSegment(o, n[e.Item1], n[e.Item2], e.Item3); }
            return o;
        }

        private static void InternalLinesInsideRectangleCreateNoStructureDimensions()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            double tol = config.GeometryTolerance;
            var outline = CreateInternalPocketRectangleFixture();
            var evidence = new StructureBoundaryEvidence(DimensionPlanner.InspectOutlineTopology(outline, tol), tol);
            var features = new StructureFeatureExtractor(config).Extract(outline, evidence);
            new StructureMeasurementSelector(config).Select(features, outline, evidence);
            evidence.ApplyAttachmentEvidence(features, outline);
            StructureBoundaryEvidence.EnforceSelection(features);
            string dump = string.Join(" ; ", features.Select(f => f.Axis + "/" + f.Kind + "/" + f.PreferredSide + " span=" + Math.Round(f.Span, 3)
                + " cross=" + Math.Round(f.CrossPosition, 3) + " keep=" + f.Keep + " req=" + f.RequiresOuterBoundarySupport + " mem=" + f.BoundaryMembership
                + " att=" + f.BoundaryAttachmentValidity + " src=" + f.SourceKey + " sup=" + string.Join(",", f.SupportSourceKeys)
                + " why=" + f.SuppressReason + " ev=" + f.BoundaryEvidence));
            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline, CoordinateFrame2D.Identity, allowLProfileQuarterTurn: true);
            var values = plan.Dimensions.Select(d => Math.Round(GetSpan(d), 3)).OrderBy(v => v).ToArray();
            Assert(values.SequenceEqual(new[] { 50.0, 57 }), "internal lines must not create structure dimensions; dims=["
                + string.Join(",", plan.Dimensions.Select(d => d.DebugRole + ":" + Math.Round(GetSpan(d), 3) + " " + d.TopologyEvidence)) + "] features=" + dump);
        }

        private static void LProfileRulesOwnVirtualLProfilesFourWay()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            var mismatches = new System.Collections.Generic.List<string>();
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                foreach (var fixture in new[] { Tuple.Create("branch", CreateBranchSplitLProfileFixture()), Tuple.Create("step", CreateLocalStepLProfileFixture()) })
                {
                    var o = TransformCapturedSelection(fixture.Item2, angle, mirror, false);
                    new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                    var plan = new DimensionPlanner(config).CreateOutlinePlan(o, CoordinateFrame2D.Identity, allowLProfileQuarterTurn: true);
                    bool quarterTurn = plan.Diagnostics.Warnings.Any(w => w.StartsWith("CoordinateFrame.LProfileQuarterTurn|", StringComparison.Ordinal));
                    // 两个夹具的较长内侧臂面在 0° 时都是水平的，所以只有 90°、270° 需要换坐标系。
                    Assert(quarterTurn == (angle == 90 || angle == 270) && quarterTurn == !plan.CoordinateFrame.IsIdentity,
                        "only placements whose longer inner arm face is vertical switch to the quarter-turn frame; " + fixture.Item1 + " angle=" + angle + " mirror=" + mirror);
                    string at = " " + fixture.Item1 + " angle=" + angle + " mirror=" + mirror + " dims="
                        + string.Join(",", plan.Dimensions.Select(d => (d.RuleId ?? d.DebugRole) + "/" + d.DebugRole + ":" + Math.Round(GetSpan(d), 3)
                            + "@" + Math.Round(d.FirstPoint.X, 2) + "," + Math.Round(d.FirstPoint.Y, 2) + "-" + Math.Round(d.SecondPoint.X, 2) + "," + Math.Round(d.SecondPoint.Y, 2)))
                        + " warn=" + string.Join(" ; ", plan.Diagnostics.Warnings.Where(w => w.Contains("Notch") || w.StartsWith("StepDatum") || w.StartsWith("StructureBoundary")).Select(w => w.Length > 300 ? w.Substring(0, 300) : w));
                    Assert(plan.Diagnostics.Warnings.Any(w => w.StartsWith("LProfile.Applied|", StringComparison.Ordinal)), "the L profile rule must apply" + at);
                    Assert(!plan.Dimensions.Any(d => StructureDatumRules.IsBaselineRule(d.RuleId)),
                        "the datum baseline rule must not replace L profile dimensions" + at);
                    var values = plan.Dimensions.Select(d => Math.Round(GetSpan(d), 3)).OrderBy(v => v).ToArray();
                    double[] expected = fixture.Item1 == "branch" ? new[] { 20.0, 20, 80, 110 } : new[] { 23.0, 25, 25, 32, 68, 102 };
                    if (!values.SequenceEqual(expected)) { mismatches.Add(fixture.Item1 + "@" + angle + (mirror ? "M" : "") + "=[" + string.Join(",", values) + "]" + at); }
                }
            }
            Assert(mismatches.Count == 0, "mismatches: " + string.Join(" ; ", mismatches));
        }

        // 生产入口（CreateDimensionPlan）带孔时，8 种摆法下 L 型尺寸一致，孔的定位尺寸换回 WCS 后仍落在孔心上。
        private static void LProfileQuarterTurnFrameKeepsHolesEightWay()
        {
            var config = DimensionRuleConfig.CreateDefault(); config.UseFeatureFirstStructurePipeline = true;
            var hole = new Point2D(60, 56.5);
            var mismatches = new System.Collections.Generic.List<string>();
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                string at = " angle=" + angle + " mirror=" + mirror;
                var o = TransformCapturedSelection(CreateLocalStepLProfileFixture(), angle, mirror, false);
                new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(o);
                Point2D center = TransformCapturedSelectionPoint(hole, angle, mirror);
                var holes = new[] { CreateHole(center.X, center.Y, 6.6, HoleKind2D.Thread) };
                var plan = new DimensionPlanner(config).CreateDimensionPlan(o, Datum2D.FromOutline(o), holes, new SlotFeature2D[0],
                    CoordinateFrame2D.Identity, allowLProfileQuarterTurn: true);
                Assert(plan.CoordinateFrame.IsIdentity == !(angle == 90 || angle == 270), "frame choice" + at);
                Assert(plan.Diagnostics.Warnings.Any(w => w.StartsWith("LProfile.Applied|", StringComparison.Ordinal)), "the L profile rule must apply" + at);
                var structure = plan.Dimensions.Where(d => d.Role != DimensionCandidateRole.Hole && d.Role != DimensionCandidateRole.Pin)
                    .Select(d => Math.Round(GetSpan(d), 3)).OrderBy(v => v).ToArray();
                if (!structure.SequenceEqual(new[] { 23.0, 25, 25, 32, 68, 102 })) { mismatches.Add("structure" + at + "=[" + string.Join(",", structure) + "]"); }
                var holeDims = plan.Dimensions.Where(d => d.Role == DimensionCandidateRole.Hole).ToList();
                bool touchesCenter = holeDims.Count == 2 && holeDims.All(d =>
                    plan.CoordinateFrame.ToWorld(d.FirstPoint).DistanceTo(center) < 1E-6 || plan.CoordinateFrame.ToWorld(d.SecondPoint).DistanceTo(center) < 1E-6);
                if (!touchesCenter)
                {
                    mismatches.Add("holes" + at + "=[" + string.Join(",", plan.Dimensions.Select(d => d.Role + ":" + Math.Round(GetSpan(d), 3)
                        + "@" + plan.CoordinateFrame.ToWorld(d.FirstPoint) + "-" + plan.CoordinateFrame.ToWorld(d.SecondPoint))) + "]");
                }
            }
            Assert(mismatches.Count == 0, "mismatches: " + string.Join(" ; ", mismatches));
        }
    }
}
