using CadAuto.Core.Geometry;

namespace CadAuto.Core.Model
{
    public sealed class FilletFeature2D
    {
        public Point2D Center { get; set; }
        public double Radius { get; set; }
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public string Text { get; set; }
        public Arc2D SourceArc { get; set; }
        public double Confidence { get; set; }
    }
}
