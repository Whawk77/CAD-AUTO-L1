using CadAuto.Core.Geometry;

namespace CadAuto.Core.Model
{
    public sealed class SlotFeature2D
    {
        public string GroupId { get; set; }
        public Point2D FirstCenter { get; set; }
        public Point2D SecondCenter { get; set; }
        public double Radius { get; set; }
        public double CenterDistance { get; set; }
        public bool IsSingleArcSlot { get; set; }
        public bool IsVertical { get; set; }
        public Point2D ArcLeaderTarget { get; set; }
    }
}
