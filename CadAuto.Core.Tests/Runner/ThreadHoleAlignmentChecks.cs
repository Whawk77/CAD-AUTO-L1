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
        private static void ThreadHoleHorizontalChainAlignsAdjacentDimensions()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateRectangle(120, 65);
            var datum = Datum2D.FromOutline(outline);
            var holes = new[]
            {
                CreateHole(15, 15, 9, HoleKind2D.Normal),
                CreateHole(105, 15, 9, HoleKind2D.Normal),
                CreateHole(40, 45, 5, HoleKind2D.Thread),
                CreateHole(80, 45, 5, HoleKind2D.Thread)
            };
            foreach (var hole in holes) { AddCenterlineCross(datum, hole.Center, "C" + hole.Center.X); }
            var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, holes);
            var chain = plan.Dimensions.Where(d => d.DebugRole == "ThreadHoleChainH").OrderBy(GetSpan).ToList();
            Assert(chain.Count == 2 && Math.Abs(GetSpan(chain[0]) - 25) < 0.001 && Math.Abs(GetSpan(chain[1]) - 40) < 0.001,
                "水平螺纹孔链应保留 25 和 40");
            Assert(!string.IsNullOrEmpty(chain[0].AlignmentKey) && chain[0].AlignmentKey == chain[1].AlignmentKey
                && chain[0].Side == chain[1].Side, "相邻孔链必须共享对齐键和方向");
            var items = chain.Select(d => new DimensionLayoutItem { Kind = d.Kind, FirstPoint = d.FirstPoint, SecondPoint = d.SecondPoint,
                Span = GetSpan(d), AlignmentKey = d.AlignmentKey, AlignmentPriority = d.AlignmentPriority, ReadingLevel = d.ReadingLevel }).ToList();
            var placements = new DimensionLayoutRules(config).CreateStackingPlan(items, chain[0].Side, outline, 3.5, 3, 10, 6.5, isHorizontal: true);
            Assert(placements.Count == 2 && placements.All(p => p.LayoutBlockType == "RootedAlignmentLane")
                && placements.Select(p => p.LayoutBlockId).Distinct().Count() == 1
                && placements.Select(p => p.DimLineCoordinateOverride).Distinct().Count() == 1,
                "25 和 40 应处于同一对齐块及尺寸线");
        }
    }
}
