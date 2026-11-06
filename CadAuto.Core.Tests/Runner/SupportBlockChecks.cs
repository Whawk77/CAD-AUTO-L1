using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        // 实图 run 20261104222304978（L型测试_1，平移到原点）：77.53×65 倒 L 型托压块。
        // 左端是"倒角 + 斜边 + 倒角"，内侧 0.27 处有一组平行线（图纸本来就有），竖腿两处 R3。
        private static OutlineFeature2D CreateSupportBlockLFixture()
        {
            const double bx = 110436.51189623466, by = 5336.485630658852;
            Func<double, double, Point2D> p = (x, y) => new Point2D(x - bx, y - by);
            var n = new Dictionary<int, Point2D>
            {
                [0] = p(110438.06340420443, 5382.48193742717), [1] = p(110439.23250621227, 5381.485627809383),
                [2] = p(110436.51189623466, 5400.489318049722), [3] = p(110438.33686813086, 5382.4819377074637),
                [4] = p(110437.33605766649, 5401.4856277266572), [5] = p(110436.7853601611, 5400.4893183300173),
                [6] = p(110439.450957985, 5381.5574668193858), [7] = p(110439.53635115274, 5381.4856281208158),
                [8] = p(110437.63749289153, 5401.48562803562), [9] = p(110437.73395640623, 5401.4856280412678),
                [10] = p(110491.04277522636, 5381.4856303940433), [11] = p(110514.04277434367, 5401.485631409143),
                [12] = p(110514.04277721244, 5336.485631409143), [13] = p(110494.04277535877, 5378.4856305264484),
                [14] = p(110494.04277708003, 5339.4856305264475), [15] = p(110497.04277721244, 5336.4856306588517)
            };
            var o = new OutlineFeature2D();
            var lines = new[]
            {
                (0, 1, "685054"), (0, 2, "68504F"), (0, 3, "68504E"), (2, 4, "685052"), (2, 5, "685050"), (3, 6, "685053"),
                (3, 5, "68504D"), (1, 7, "685049"), (4, 8, "685045"), (5, 8, "685051"), (6, 9, "68504C"), (7, 6, "68504B"),
                (7, 10, "68504A"), (9, 8, "685046"), (9, 11, "685044"), (11, 12, "68503E"), (13, 14, "685042"), (12, 15, "68503F")
            };
            foreach (var e in lines) { AddSegment(o, n[e.Item1], n[e.Item2], e.Item3); }
            o.Arcs.Add(new Arc2D { Start = n[13], End = n[10], Center = new Point2D(n[10].X, n[13].Y), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "685040" });
            o.Arcs.Add(new Arc2D { Start = n[14], End = n[15], Center = new Point2D(n[15].X, n[14].Y), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "685041" });
            o.MinX = 0; o.MinY = 0; o.MaxX = n[12].X; o.MaxY = n[11].Y;
            return o;
        }

        // 实图 run 20261105182011543（L型测试，平移到原点）：50×31.56 倒 T 型托压块。
        // 横梁 y=11.56~31.56，凸台 x=5~25 往下伸到 y=0；凸台底两角 C0.5，凸台与横梁的两个内角 R3，横梁右下角 R3。
        // 凸台底边上方 0.5 处有一条内部横线（T 字连接到两侧倒角端点）。
        private static OutlineFeature2D CreateSupportBlockTFixture()
        {
            const double h = 11.56, top = 31.56;
            var n = new Dictionary<int, Point2D>
            {
                [0] = new Point2D(24.5, 0), [1] = new Point2D(25, 0.5), [2] = new Point2D(5.5, 0), [3] = new Point2D(5, 0.5),
                [4] = new Point2D(25, h - 3), [5] = new Point2D(5, h - 3), [6] = new Point2D(28, h), [7] = new Point2D(47, h),
                [8] = new Point2D(0, h), [9] = new Point2D(2, h), [10] = new Point2D(0, top), [11] = new Point2D(50, h + 3),
                [12] = new Point2D(50, top)
            };
            var o = new OutlineFeature2D { MinX = 0, MinY = 0, MaxX = 50, MaxY = top };
            var lines = new[]
            {
                (0, 1, "676BA6"), (2, 0, "676BA3"), (3, 1, "676BA2"), (1, 4, "676BA1"), (2, 3, "676BA5"), (5, 3, "676BA0"),
                (6, 7, "676B93"), (8, 9, "676B9C"), (10, 8, "676B97"), (11, 12, "676B9F"), (12, 10, "676B94")
            };
            foreach (var e in lines) { AddSegment(o, n[e.Item1], n[e.Item2], e.Item3); }
            // 两个内角 R3 的圆心在横梁下方外侧，横梁右下角 R3 的圆心在零件内部。
            o.Arcs.Add(new Arc2D { Start = n[4], End = n[6], Center = new Point2D(28, h - 3), Radius = 3, Bulge = -Math.Tan(Math.PI / 8), SourceKey = "676B95" });
            o.Arcs.Add(new Arc2D { Start = n[9], End = n[5], Center = new Point2D(2, h - 3), Radius = 3, Bulge = -Math.Tan(Math.PI / 8), SourceKey = "676B99" });
            o.Arcs.Add(new Arc2D { Start = n[7], End = n[11], Center = new Point2D(47, h + 3), Radius = 3, Bulge = Math.Tan(Math.PI / 8), SourceKey = "676B96" });
            return o;
        }

        private static DimensionRuleConfig CreateSupportBlockConfig()
        {
            var config = DimensionRuleConfig.CreateDefault();
            config.UseFeatureFirstStructurePipeline = true;
            config.SupportBlockMode = true;
            return config;
        }

        private static string DumpDims(DimensionPlan plan)
            => string.Join(",", plan.Dimensions.Select(d => (d.RuleId ?? d.DebugRole) + "/" + d.Side + ":" + Math.Round(GetSpan(d), 3)))
                + " warn=" + string.Join(" ; ", plan.Diagnostics.Warnings.Where(w => w.StartsWith("SupportBlock", StringComparison.Ordinal)));

        // 托压块规则对两张实图在 8 种摆法下都只出整体和结构尺寸，没有斜边、倒角、碎段尺寸。
        private static void SupportBlockKeepsOverallAndStructureEightWay()
        {
            var config = CreateSupportBlockConfig();
            var mismatches = new List<string>();
            var cases = new[]
            {
                // L 型：总宽 77.53、总高 65、横梁厚 20、竖腿宽 20。
                Tuple.Create("L", CreateSupportBlockLFixture(), new[] { 20.0, 20, 65, 77.531 }),
                // 倒 T 型（T 型规则）：总宽 50、总高 31.56、横梁厚 20、凸台宽 20、从左边到凸台右边 25；不标 5 和 11.56。
                Tuple.Create("T", CreateSupportBlockTFixture(), new[] { 20.0, 20, 25, 31.56, 50 })
            };
            foreach (var c in cases)
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            {
                var o = TransformCapturedSelection(c.Item2, angle, mirror, false);
                var plan = new DimensionPlanner(config).CreateOutlinePlan(o, CoordinateFrame2D.Identity, allowLProfileQuarterTurn: true);
                var values = plan.Dimensions.Select(d => Math.Round(GetSpan(d), 3)).OrderBy(v => v).ToArray();
                if (!values.SequenceEqual(c.Item3))
                {
                    mismatches.Add(c.Item1 + "@" + angle + (mirror ? "M" : "") + "=[" + string.Join(",", values) + "] " + DumpDims(plan));
                }
            }
            Assert(mismatches.Count == 0, "support block mismatches: " + string.Join(" ; ", mismatches));

            // 0° 摆放：20、25 在下，50 在上（底部留给 TY 引线），31.56 在左，横梁厚 20 在右；25 从左边量起。
            var t = new DimensionPlanner(config).CreateOutlinePlan(CreateSupportBlockTFixture(), CoordinateFrame2D.Identity, allowLProfileQuarterTurn: true);
            string dump = DumpDims(t);
            Assert(t.Dimensions.Where(d => d.Orientation == DimensionOrientation.Horizontal && d.Kind == DimensionKind.Normal).All(d => d.Side == DimensionSide.Bottom), "T boss dims below; " + dump);
            Assert(t.Dimensions.Single(d => d.Kind == DimensionKind.OverallWidth).Side == DimensionSide.Top, "T overall length on the beam side; " + dump);
            Assert(t.Dimensions.Single(d => d.Kind == DimensionKind.OverallHeight).Side == DimensionSide.Left, "T overall height on the left; " + dump);
            Assert(t.Dimensions.Single(d => d.RuleId == "SupportBlock.T.BeamThickness").Side == DimensionSide.Right, "T beam thickness on the right; " + dump);
            var location = t.Dimensions.Single(d => d.RuleId == "SupportBlock.T.BossLocation");
            Assert(Math.Abs(Math.Min(location.FirstPoint.X, location.SecondPoint.X)) < 1e-6, "T boss location starts at the left edge; " + dump);
            // 不是 T 型的托压块（L 型）不走 T 型规则。
            var l = new DimensionPlanner(config).CreateOutlinePlan(CreateSupportBlockLFixture(), CoordinateFrame2D.Identity, allowLProfileQuarterTurn: true);
            Assert(l.Dimensions.All(d => d.RuleId == null || !d.RuleId.StartsWith("SupportBlock.T.", StringComparison.Ordinal)), "L is not classified as T; " + DumpDims(l));
        }

        // TY 标注避让：和 ASD 尺寸重叠时整组移到尺寸外面 gap 处；不重叠、或只和另一方向贴边的尺寸挨着时不动；重复计算不累加。
        private static void SupportBlockCalloutShiftsOutsideOverlappingDimensions()
        {
            Func<double, double, double, double, TextBounds2D> box = (x0, y0, x1, y1) => new TextBounds2D { MinX = x0, MinY = y0, MaxX = x1, MaxY = y1 };
            // 包络 0..50 × 0..31.56；底部 ASD 尺寸到 y=-18；TY 文字在 y=-14..-10，粗糙度块在 y=-20..-15。
            var dims = new[] { box(5, -12, 25, -8), box(0, -18.5, 25, -14), box(-12, 0, -8, 31.56) };
            var callout = new[] { box(30, -14, 45, -10), box(20, -20, 28, -15) };
            var shift = SupportBlockCalloutLayout.ComputeShift(callout, dims, 0, 0, 50, 31.56, 12, 0.001);
            Assert(shift != null && shift.Side == DimensionSide.Bottom && Math.Abs(shift.DeltaY - (-18.5 - 12 - (-10))) < 1e-9 && shift.DeltaX == 0,
                "overlapping callout moves below the lowest dimension by gap; " + shift?.Evidence + " dy=" + shift?.DeltaY);
            var moved = callout.Select(b => box(b.MinX, b.MinY + shift.DeltaY, b.MaxX, b.MaxY + shift.DeltaY)).ToArray();
            Assert(SupportBlockCalloutLayout.ComputeShift(moved, dims, 0, 0, 50, 31.56, 12, 0.001) == null, "a second ASD run does not move the callout again");
            var clear = new[] { box(30, -40, 45, -36) };
            Assert(SupportBlockCalloutLayout.ComputeShift(clear, dims, 0, 0, 50, 31.56, 12, 0.001) == null, "a callout clear of dimensions stays");
            // 只碰到左侧竖向尺寸贴着底边的端头，不算这一侧的尺寸。
            var nearLeft = new[] { box(-14, -6, -9, -0.5) };
            var leftDims = new[] { box(-12, -0.2, -8, 31.56) };
            Assert(SupportBlockCalloutLayout.ComputeShift(nearLeft, leftDims, 0, 0, 50, 31.56, 12, 0.001) == null
                || SupportBlockCalloutLayout.ComputeShift(nearLeft, leftDims, 0, 0, 50, 31.56, 12, 0.001).Side != DimensionSide.Bottom,
                "a vertical dimension ending at the envelope does not push a bottom callout");
        }

        // 托压块模式照常标孔；矩形托压块只出总宽、总高；虚拟轮廓只有横竖边；不开托压块模式时 L 型判定不变。
        private static void SupportBlockKeepsHolesAndLeavesNormalRulesUnchanged()
        {
            var config = CreateSupportBlockConfig();
            var o = CreateSupportBlockLFixture();
            var holes = new[] { CreateHole(67.53, 25, 6.6, HoleKind2D.Thread) };
            var plan = new DimensionPlanner(config).CreateDimensionPlan(o, Datum2D.FromOutline(o), holes, new SlotFeature2D[0],
                CoordinateFrame2D.Identity, allowLProfileQuarterTurn: true);
            var structure = plan.Dimensions.Where(d => d.Role != DimensionCandidateRole.Hole && d.Role != DimensionCandidateRole.Pin)
                .Select(d => Math.Round(GetSpan(d), 3)).OrderBy(v => v).ToArray();
            Assert(structure.SequenceEqual(new[] { 20.0, 20, 65, 77.531 }), "outline dims with a hole; " + DumpDims(plan));
            Assert(plan.Dimensions.Count(d => d.Role == DimensionCandidateRole.Hole) == 2, "the hole is still located in support block mode; " + DumpDims(plan));

            var rectangle = new DimensionPlanner(config).CreateOutlinePlan(CreateRectangleAt(0, 0, 40, 25));
            var rectValues = rectangle.Dimensions.Select(d => Math.Round(GetSpan(d), 3)).OrderBy(v => v).ToArray();
            Assert(rectValues.SequenceEqual(new[] { 25.0, 40 }), "a rectangular support block only gets overall dimensions; [" + string.Join(",", rectValues) + "]");

            double tol = config.GeometryTolerance;
            foreach (var fixture in new[] { CreateSupportBlockLFixture(), CreateSupportBlockTFixture() })
            {
                var graph = DimensionPlanner.InspectOutlineTopology(fixture, tol);
                var virtualOutline = SupportBlockRules.BuildOrthogonalOutline(graph, tol, out string evidence);
                Assert(virtualOutline != null && virtualOutline.Arcs.Count == 0
                    && virtualOutline.Segments.All(s => s.IsHorizontal(tol) || s.IsVertical(tol)), "the virtual outline has only orthogonal faces; " + evidence);
            }
            var normal = LProfileShapeRules.Classify(DimensionPlanner.InspectOutlineTopology(o, tol), tol, DimensionRuleConfig.LProfileLocalStepMaxDepthRatio);
            Assert(!normal.IsLProfile && normal.Evidence.Contains("ConsecutiveCornerTreatments"),
                "without support block mode the chamfered slanted end still blocks the L rule; " + normal.Evidence);
        }
    }
}
