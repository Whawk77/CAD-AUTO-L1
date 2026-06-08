using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed class DimensionExtensionLineRenderer
    {
        private readonly Database _database;
        private readonly CadEntityWriter _writer;
        private readonly string _annotationLayer;

        public DimensionExtensionLineRenderer(
            Database database,
            CadEntityWriter writer,
            string annotationLayer)
        {
            _database = database;
            _writer = writer;
            _annotationLayer = annotationLayer;
        }

        public void AddSegmentedLine(
            double fixedCoord,
            double startVariable,
            double endVariable,
            IList<Tuple<double, double>> breakRanges,
            bool vertical,
            double tolerance)
        {
            var min = Math.Min(startVariable, endVariable);
            var max = Math.Max(startVariable, endVariable);
            var ranges = (breakRanges ?? new List<Tuple<double, double>>())
                .Select(r => new { A = Math.Max(min, Math.Min(r.Item1, r.Item2)), B = Math.Min(max, Math.Max(r.Item1, r.Item2)) })
                .Where(r => r.B > r.A + tolerance)
                .OrderBy(r => r.A)
                .ToList();

            var cursor = min;
            foreach (var range in ranges)
            {
                AddLineSegment(fixedCoord, cursor, range.A, vertical, tolerance);
                if (range.B > cursor)
                {
                    cursor = range.B;
                }
            }

            AddLineSegment(fixedCoord, cursor, max, vertical, tolerance);
        }

        private void AddLineSegment(double fixedCoord, double a, double b, bool vertical, double tolerance)
        {
            if (b - a <= tolerance)
            {
                return;
            }

            var start = vertical ? new Point3d(fixedCoord, a, 0.0) : new Point3d(a, fixedCoord, 0.0);
            var end = vertical ? new Point3d(fixedCoord, b, 0.0) : new Point3d(b, fixedCoord, 0.0);
            var line = new Line(start, end);
            line.SetDatabaseDefaults(_database);
            line.Layer = _annotationLayer;
            _writer.Append(line);
        }
    }
}
