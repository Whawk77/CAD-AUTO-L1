using System.Collections.Generic;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;

namespace CadAuto.Core.Planning
{
    public enum DimensionKind
    {
        Normal,
        OverallWidth,
        OverallHeight,
        HoleDiameter,
        PinDistance,
        PinGroupDistance,
        HoleLocation,
        DatumHoleLocationX,
        DatumHoleLocationY,
        Chamfer,
        Fillet
    }

    public enum DimensionSide
    {
        Bottom,
        Top,
        Left,
        Right
    }

    public enum DimensionOrientation
    {
        Horizontal,
        Vertical,
        Radius,
        Diameter,
        Leader
    }

    public sealed class PlannedDimension
    {
        public DimensionKind Kind { get; set; }
        public DimensionOrientation Orientation { get; set; }
        public DimensionSide Side { get; set; }
        public Point2D FirstPoint { get; set; }
        public Point2D SecondPoint { get; set; }
        public string OverrideText { get; set; }
        public string SourceKey { get; set; }
        public bool ForceOuterLevel { get; set; }
        public bool PreferLocalBoundary { get; set; }
        public int ChainId { get; set; }
        public string DebugOwner { get; set; }
        public string DebugRole { get; set; }
    }

    public sealed class PinGroupPlan
    {
        public int GroupIndex { get; set; }
        public HoleFeature2D BasePin { get; set; }
        public List<HoleFeature2D> Pins { get; private set; }
        public List<HoleFeature2D> MemberHoles { get; private set; }
        public DimensionSide HorizontalSide { get; set; }
        public DimensionSide VerticalSide { get; set; }

        public PinGroupPlan()
        {
            Pins = new List<HoleFeature2D>();
            MemberHoles = new List<HoleFeature2D>();
            HorizontalSide = DimensionSide.Bottom;
            VerticalSide = DimensionSide.Left;
        }
    }

    public sealed class DimensionPlan
    {
        public List<PlannedDimension> Dimensions { get; private set; }
        public List<PinGroupPlan> PinGroups { get; private set; }

        public DimensionPlan()
        {
            Dimensions = new List<PlannedDimension>();
            PinGroups = new List<PinGroupPlan>();
        }

        public void Add(PlannedDimension dimension)
        {
            if (dimension != null)
            {
                Dimensions.Add(dimension);
            }
        }
    }
}
