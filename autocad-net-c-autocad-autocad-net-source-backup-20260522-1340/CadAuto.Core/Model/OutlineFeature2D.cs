using System.Collections.Generic;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Model
{
    public sealed class OutlineFeature2D
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }

        public List<Point2D> Vertices { get; private set; }
        public List<Segment2D> Segments { get; private set; }
        public List<Arc2D> Arcs { get; private set; }
        public List<ChamferFeature2D> Chamfers { get; private set; }
        public List<FilletFeature2D> Fillets { get; private set; }

        public OutlineFeature2D()
        {
            Vertices = new List<Point2D>();
            Segments = new List<Segment2D>();
            Arcs = new List<Arc2D>();
            Chamfers = new List<ChamferFeature2D>();
            Fillets = new List<FilletFeature2D>();
        }

        public double Width { get { return MaxX - MinX; } }
        public double Height { get { return MaxY - MinY; } }
    }
}
