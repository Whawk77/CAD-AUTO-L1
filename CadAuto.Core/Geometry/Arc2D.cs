namespace CadAuto.Core.Geometry
{
    public sealed class Arc2D
    {
        public Point2D Start { get; set; }
        public Point2D End { get; set; }
        public Point2D Center { get; set; }
        public double Radius { get; set; }
        public double Bulge { get; set; }
        public string SourceKey { get; set; }
    }
}
