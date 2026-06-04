using System.Collections.Generic;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Model
{
    public sealed class DrawingGeometry
    {
        public OutlineFeature2D Outline { get; set; }
        public List<Circle2D> Circles { get; private set; }
        public List<Arc2D> Arcs { get; private set; }
        public List<Segment2D> Segments { get; private set; }

        public DrawingGeometry()
        {
            Circles = new List<Circle2D>();
            Arcs = new List<Arc2D>();
            Segments = new List<Segment2D>();
        }
    }
}
