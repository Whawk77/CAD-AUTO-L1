using CadAuto.Core.Geometry;

namespace CadAuto.Core.Model
{
    public enum HoleKind2D
    {
        Normal,
        Pin,
        Thread,
        Slot
    }

    public sealed class HoleFeature2D
    {
        public Point2D Center { get; set; }
        public double Diameter { get; set; }
        public string SourceKey { get; set; }
        public string FitTolerance { get; set; }
        public string ThreadCallout { get; set; }
        public HoleKind2D Kind { get; set; }

        public bool IsPinHole
        {
            get { return Kind == HoleKind2D.Pin; }
        }

        public bool IsThreadHole
        {
            get { return Kind == HoleKind2D.Thread; }
        }

        public bool IsSlotPoint
        {
            get { return Kind == HoleKind2D.Slot; }
        }
    }
}
