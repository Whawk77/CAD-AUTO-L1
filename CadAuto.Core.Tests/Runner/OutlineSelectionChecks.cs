using System;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        private static void OutlineSelectionCapturedTAndSlotsPreferExterior()
        {
            // Selected source geometry from DWG E2162629ACA108418E5EE13A2C257C8769F2387057AD80D47039DA53075C6E59.
            foreach (double angle in new[] { 0.0, 90.0, 180.0, 270.0 })
            foreach (bool mirror in new[] { false, true })
            foreach (bool reverse in new[] { false, true })
            {
                var outer = TransformCapturedSelection(CreateCapturedSelectionOuter(), angle, mirror, reverse);
                var left = TransformCapturedSelection(CreateCapturedSelectionSlot(17.5), angle, mirror, reverse);
                var right = TransformCapturedSelection(CreateCapturedSelectionSlot(42.5), angle, mirror, reverse);
                var input = new[] { right, outer, left };
                var selected = OutlineSelectionRules.SelectSinglePartExterior(input, 0.01, out _);
                Assert(selected == 1,
                    "the real T-connected exterior must enclose both slots, independent of direction and order");
                var center = TransformCapturedSelectionPoint(new Point2D(29.999998778, 55.000000874), angle, mirror);
                Assert(OutlineSelectionRules.ContainsPoint(outer, center, 0.001) == true,
                    "the actual selected pin marker must remain inside the chosen exterior");
                Assert(OutlineSelectionRules.ContainsPoint(right, center, 0.001) == false,
                    "the pin marker is not inside the wrongly chosen right slot");
                var arcCenter = TransformCapturedSelectionPoint(new Point2D(17.499999215, 45.000000328), angle, mirror);
                Assert(OutlineSelectionRules.ContainsPoint(left, arcCenter, 0.001) == true,
                    "an arc-center scanline remains inside even when aligned with an arc endpoint");
            }
        }

        private static void OutlineSelectionPicksSinglePartExterior()
        {
            var concave = new OutlineFeature2D { MinX = 0, MaxX = 10, MinY = 0, MaxY = 10 };
            AddSegment(concave, new Point2D(0, 0), new Point2D(10, 0), "bottom");
            AddSegment(concave, new Point2D(10, 0), new Point2D(10, 3), "right");
            AddSegment(concave, new Point2D(10, 3), new Point2D(3, 3), "notch-bottom");
            AddSegment(concave, new Point2D(3, 3), new Point2D(3, 10), "notch-left");
            AddSegment(concave, new Point2D(3, 10), new Point2D(0, 10), "top");
            AddSegment(concave, new Point2D(0, 10), new Point2D(0, 0), "left");
            var inside = CreateRectangleAt(0.5, 4, 1.5, 3);
            var inNotch = CreateRectangleAt(5, 5, 3, 3);
            var crossing = CreateRectangleAt(2, 2, 6, 6);
            var separate = CreateRectangleAt(20, 0, 10, 10);
            // ASD 每次框选单一零件（用户 2026-11-03 确认）：外包络最大的闭合线组是外轮廓，其余线组都是内部结构，不再要求点选。
            // 原先 inNotch、crossing、separate、null、openOutside 这几种情况会要求用户点选。
            Func<OutlineFeature2D[], int> select = input => OutlineSelectionRules.SelectSinglePartExterior(input, 0.001, out _);
            Assert(select(new[] { concave, inside }) == 0, "an actual enclosed contour belongs to its exterior");
            Assert(select(new[] { inNotch, concave }) == 1, "a contour in a concave cutout is structure of the same part");
            Assert(select(new[] { crossing, concave }) == 1, "a crossing closed contour is structure of the same part");
            Assert(select(new[] { concave, separate }) == 0, "equal envelopes keep the first closed component");
            Assert(select(new[] { null, concave }) == 1, "an uninspectable component does not block the exterior choice");
            var openOutside = new OutlineFeature2D();
            AddSegment(openOutside, new Point2D(12, 0), new Point2D(12, 5), "open-outside");
            var openInside = new OutlineFeature2D();
            AddSegment(openInside, new Point2D(1, 4), new Point2D(1, 8), "open-inside");
            Assert(select(new[] { openOutside, concave }) == 1, "open geometry never becomes the exterior");
            Assert(select(new[] { concave, openInside }) == 0, "an internal open feature does not require a separate outline choice");
            Assert(select(new[] { openInside, openOutside }) == -1, "without a closed component the caller falls back to the legacy rule");

            // 实图 run 20261103000252033：50×51 外轮廓，三个从底面打进去的孔，孔壁线下端落在底边中间（T 字连接），各自成组。
            var part = CreateRectangleAt(0, 0, 50, 51);
            var input2 = new System.Collections.Generic.List<OutlineFeature2D>();
            foreach (double x in new[] { 10.0, 25.0, 40.0 })
            {
                var walls = new OutlineFeature2D();
                AddSegment(walls, new Point2D(x - 3.3, 0), new Point2D(x - 3.3, 17), "wall-l" + x);
                AddSegment(walls, new Point2D(x - 3.3, 17), new Point2D(x + 3.3, 17), "wall-b" + x);
                AddSegment(walls, new Point2D(x + 3.3, 17), new Point2D(x + 3.3, 0), "wall-r" + x);
                input2.Add(walls);
            }
            input2.Insert(1, part);
            int chosen = OutlineSelectionRules.SelectSinglePartExterior(input2, 0.01, out var decisions);
            Assert(chosen == 1 && decisions.Count == 4 && decisions[1].EndsWith("|Decision=Exterior", StringComparison.Ordinal)
                && decisions.Where((d, i) => i != 1).All(d => d.Contains("|Closed=False|") && d.EndsWith("|Decision=InternalOpen", StringComparison.Ordinal)),
                "hole walls T-connected to the bottom edge are internal structure; decisions=" + string.Join(" ; ", decisions));
            Assert(OutlineSelectionRules.ContainsPoint(concave, new Point2D(2, 5), 0.001) == true
                && OutlineSelectionRules.ContainsPoint(concave, new Point2D(5, 5), 0.001) == false
                && OutlineSelectionRules.ContainsPoint(concave, new Point2D(3, 5), 0.001) == false,
                "pin marker location must use the real boundary, including concave and boundary cases");
            concave.Segments.RemoveAt(0);
            Assert(!OutlineSelectionRules.ContainsPoint(concave, new Point2D(2, 5), 0.001).HasValue,
                "an open boundary is reported as unverified");
        }

        private static OutlineFeature2D CreateCapturedSelectionOuter()
        {
            var outline = new OutlineFeature2D { MinX = 0, MaxX = 50, MinY = 0, MaxY = 75 };
            AddSegment(outline, new Point2D(44.999997903, 75.000001530), new Point2D(14.999997903, 75.000000219), "685D94");
            AddSegment(outline, new Point2D(9.999998122, 70), new Point2D(10.000000745, 10), "685D95");
            AddSegment(outline, new Point2D(10.000001006, 0), new Point2D(10.000000569, 10), "685D96");
            AddSegment(outline, new Point2D(0.000000216, -0.000000437), new Point2D(-0.000000003, 4.999999563), "685DA3");
            AddSegment(outline, new Point2D(-0.000000003, 4.999999563), new Point2D(5.000000085, 4.999999781), "685DA4");
            AddSegment(outline, new Point2D(0.000000216, -0.000000437), new Point2D(5.000000304, -0.000000219), "685DA5");
            AddSegment(outline, new Point2D(5.000000304, -0.000000219), new Point2D(5.000000085, 4.999999781), "685DA6");
            AddSegment(outline, new Point2D(5.000000304, -0.000000219), new Point2D(10.000000304, 0), "685DAB");
            AddSegment(outline, new Point2D(5.000000085, 4.999999781), new Point2D(10.000000085, 5), "685DAC");
            AddSegment(outline, new Point2D(50.000001006, 0.000001748), new Point2D(10.000001006, 0), "685DAD");
            AddSegment(outline, new Point2D(14.999997903, 75.000000219), new Point2D(9.999998122, 70), "685DAE");
            AddSegment(outline, new Point2D(50.000001006, 0.000001748), new Point2D(50.000000569, 10.000001748), "685DAF");
            AddSegment(outline, new Point2D(50.000000745, 10.000001748), new Point2D(10.000000745, 10), "685DB0");
            AddSegment(outline, new Point2D(49.999998122, 70.000001748), new Point2D(50.000000745, 10.000001748), "685DB1");
            AddSegment(outline, new Point2D(44.999997903, 75.000001530), new Point2D(49.999998122, 70.000001748), "685DB2");
            return outline;
        }

        private static OutlineFeature2D CreateCapturedSelectionSlot(double x)
        {
            bool right = x > 20;
            double dx = right ? 25 : 0, dy = right ? 0.000001093 : 0;
            var outline = new OutlineFeature2D { MinX = x - 3.5, MaxX = x + 3.5, MinY = 41.5, MaxY = 68.5 };
            AddSegment(outline, new Point2D(13.999998340 + dx, 65.000000175 + dy), new Point2D(13.999999215 + dx, 45.000000175 + dy), right ? "685D9E" : "685D9A");
            AddSegment(outline, new Point2D(20.999999215 + dx, 45.000000481 + dy), new Point2D(20.999998340 + dx, 65.000000481 + dy), right ? "685D9C" : "685D98");
            outline.Arcs.Add(new Arc2D { Start = new Point2D(13.999999215 + dx, 45.000000175 + dy), End = new Point2D(20.999999215 + dx, 45.000000481 + dy),
                Center = new Point2D(17.499999215 + dx, 45.000000328 + dy), Radius = 3.5, Bulge = 1, SourceKey = right ? "685D9B" : "685D97" });
            outline.Arcs.Add(new Arc2D { Start = new Point2D(20.999998340 + dx, 65.000000481 + dy), End = new Point2D(13.999998340 + dx, 65.000000175 + dy),
                Center = new Point2D(17.499998340 + dx, 65.000000328 + dy), Radius = 3.5, Bulge = 1, SourceKey = right ? "685D9D" : "685D99" });
            return outline;
        }

        private static Point2D TransformCapturedSelectionPoint(Point2D point, double degrees, bool mirror)
        {
            double x = mirror ? -point.X : point.X;
            double radians = degrees * Math.PI / 180;
            return new Point2D(x * Math.Cos(radians) - point.Y * Math.Sin(radians),
                x * Math.Sin(radians) + point.Y * Math.Cos(radians));
        }

        private static OutlineFeature2D TransformCapturedSelection(OutlineFeature2D source, double degrees, bool mirror, bool reverse)
        {
            var result = new OutlineFeature2D();
            foreach (var s in source.Segments)
                AddSegment(result, TransformCapturedSelectionPoint(reverse ? s.End : s.Start, degrees, mirror),
                    TransformCapturedSelectionPoint(reverse ? s.Start : s.End, degrees, mirror), s.SourceKey);
            foreach (var arc in source.Arcs)
                result.Arcs.Add(new Arc2D { Start = TransformCapturedSelectionPoint(reverse ? arc.End : arc.Start, degrees, mirror),
                    End = TransformCapturedSelectionPoint(reverse ? arc.Start : arc.End, degrees, mirror),
                    Center = TransformCapturedSelectionPoint(arc.Center, degrees, mirror), Radius = arc.Radius,
                    Bulge = arc.Bulge * (mirror ? -1 : 1) * (reverse ? -1 : 1), SourceKey = arc.SourceKey });
            Assert(OutlineGeometryQuery.TryGetEnvelope(result, 0.001, out OutlineEnvelope2D envelope), "transformed fixture has an envelope");
            result.MinX = envelope.MinX; result.MaxX = envelope.MaxX; result.MinY = envelope.MinY; result.MaxY = envelope.MaxY;
            return result;
        }
    }
}
