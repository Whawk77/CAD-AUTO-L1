using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AutoFixtureDim
{
    public enum HoleKind
    {
        Normal,
        Pin,
        Thread,
        Slot
    }

    public enum DimensionType
    {
        Normal,
        OverallWidth,
        OverallHeight,
        HoleDiameter,
        PinDistance,
        PinGroupDistance,
        HoleLocation,
        DatumHoleLocationX,
        DatumHoleLocationY
    }

    public sealed class HoleFeature
    {
        public Point3d Center { get; set; }
        public double Diameter { get; set; }
        public ObjectId SourceId { get; set; }
        public ObjectId CircleId { get; set; }
        public string FitTolerance { get; set; }
        public string ThreadCallout { get; set; }
        public HoleKind HoleKind { get; set; }

        public bool IsPinHole
        {
            get { return HoleKind == HoleKind.Pin; }
            set { HoleKind = value ? HoleKind.Pin : HoleKind.Normal; }
        }

        public bool IsThreadHole
        {
            get { return HoleKind == HoleKind.Thread; }
        }

        public bool IsSlotPoint
        {
            get { return HoleKind == HoleKind.Slot; }
        }
    }

    public sealed class SlotFeature
    {
        public string GroupId { get; set; }
        public Point2d FirstCenter { get; set; }
        public Point2d SecondCenter { get; set; }
        public double Radius { get; set; }
        public double CenterDistance { get; set; }
        public ObjectId FirstArcId { get; set; } = ObjectId.Null;
        public ObjectId SecondArcId { get; set; } = ObjectId.Null;
        public ObjectId FirstLineId { get; set; } = ObjectId.Null;
        public ObjectId SecondLineId { get; set; } = ObjectId.Null;
        public bool IsSingleArcSlot { get; set; }
        public bool IsVertical { get; set; }
        public Point2d ArcLeaderTarget { get; set; }

        public Point2d LeaderArcCenter
        {
            get { return FirstCenter; }
        }

        public Point2d LeaderArcTarget
        {
            get
            {
                if (IsSingleArcSlot)
                {
                    return ArcLeaderTarget;
                }

                var direction = FirstCenter - SecondCenter;
                if (direction.Length <= 1e-9)
                {
                    direction = Vector2d.XAxis;
                }

                direction = direction.GetNormal();
                return FirstCenter + direction * Radius;
            }
        }
    }

    public sealed class OutlineFeature
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public List<Point2d> Vertices { get; } = new List<Point2d>();
        public List<OutlineSegment> Segments { get; } = new List<OutlineSegment>();
        public List<OutlineArc> Arcs { get; } = new List<OutlineArc>();
        public List<ChamferFeature> Chamfers { get; } = new List<ChamferFeature>();
        public List<FilletFeature> Fillets { get; } = new List<FilletFeature>();

        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
    }

    public sealed class OutlineSegment
    {
        public Point2d Start { get; set; }
        public Point2d End { get; set; }
        public ObjectId SourceId { get; set; } = ObjectId.Null;
        public bool IsArcChord { get; set; }

        public bool IsHorizontal(double tolerance)
        {
            return System.Math.Abs(Start.Y - End.Y) <= tolerance;
        }

        public bool IsVertical(double tolerance)
        {
            return System.Math.Abs(Start.X - End.X) <= tolerance;
        }

        public double MinX => System.Math.Min(Start.X, End.X);
        public double MaxX => System.Math.Max(Start.X, End.X);
        public double MinY => System.Math.Min(Start.Y, End.Y);
        public double MaxY => System.Math.Max(Start.Y, End.Y);
        public double LengthX => MaxX - MinX;
        public double LengthY => MaxY - MinY;
        public double Length => Start.GetDistanceTo(End);
    }

    public sealed class OutlineArc
    {
        public Point2d Start { get; set; }
        public Point2d End { get; set; }
        public Point2d Center { get; set; }
        public double Radius { get; set; }
        public ObjectId SourceId { get; set; } = ObjectId.Null;
        public double Bulge { get; set; }
    }

    public sealed class ChamferFeature
    {
        public Point2d StartPoint { get; set; }
        public Point2d EndPoint { get; set; }
        public double Length { get; set; }
        public double DeltaX { get; set; }
        public double DeltaY { get; set; }
        public double Value { get; set; }
        public string Text { get; set; }
        public OutlineSegment SourceSegment { get; set; }
        public double Confidence { get; set; }
    }

    public sealed class FilletFeature
    {
        public Point2d Center { get; set; }
        public double Radius { get; set; }
        public Point2d StartPoint { get; set; }
        public Point2d EndPoint { get; set; }
        public string Text { get; set; }
        public OutlineArc SourceArc { get; set; }
        public double Confidence { get; set; }
    }

    public sealed class OutlineSelection
    {
        public ObjectId PrimaryPolylineId { get; set; } = ObjectId.Null;
        public List<ObjectId> EntityIds { get; } = new List<ObjectId>();
        public List<ObjectId> SelectedIds { get; } = new List<ObjectId>();

        public bool HasPrimaryPolyline => !PrimaryPolylineId.IsNull;
    }

    public sealed class DatumDefinition
    {
        public double BaseX { get; set; }
        public double BaseY { get; set; }
        public HoleFeature DatumHole { get; set; }
        public double? DatumHoleLocationBaseX { get; set; }
        public double? DatumHoleLocationBaseY { get; set; }
        public bool DatumHoleLocationUseToleranceX { get; set; }
        public bool DatumHoleLocationUseToleranceY { get; set; }

        public static DatumDefinition FromOutline(OutlineFeature outline)
        {
            return new DatumDefinition
            {
                BaseX = outline.MinX,
                BaseY = outline.MinY
            };
        }
    }
}
