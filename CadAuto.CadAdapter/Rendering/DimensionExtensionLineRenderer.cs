using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Environment;

namespace CadAuto.CadAdapter.Rendering;

public sealed class DimensionExtensionLineRenderer
{
	private readonly Database _database;

	private readonly CadEntityWriter _writer;

	private readonly string _annotationLayer;

	public DimensionExtensionLineRenderer(Database database, CadEntityWriter writer, string annotationLayer)
	{
		_database = database;
		_writer = writer;
		_annotationLayer = annotationLayer;
	}

	public void AddSegmentedLine(double fixedCoord, double startVariable, double endVariable, IList<Tuple<double, double>> breakRanges, bool vertical, double tolerance)
	{
		double min = Math.Min(startVariable, endVariable);
		double max = Math.Max(startVariable, endVariable);
		var list = (from r in breakRanges ?? new List<Tuple<double, double>>()
			select new
			{
				A = Math.Max(min, Math.Min(r.Item1, r.Item2)),
				B = Math.Min(max, Math.Max(r.Item1, r.Item2))
			} into r
			where r.B > r.A + tolerance
			orderby r.A
			select r).ToList();
		double num = min;
		foreach (var item in list)
		{
			AddLineSegment(fixedCoord, num, item.A, vertical, tolerance);
			if (item.B > num)
			{
				num = item.B;
			}
		}
		AddLineSegment(fixedCoord, num, max, vertical, tolerance);
	}

	private void AddLineSegment(double fixedCoord, double a, double b, bool vertical, double tolerance)
	{
		if (!(b - a <= tolerance))
		{
			Point3d pointer = (vertical ? new Point3d(fixedCoord, a, 0.0) : new Point3d(a, fixedCoord, 0.0));
			Point3d pointer2 = (vertical ? new Point3d(fixedCoord, b, 0.0) : new Point3d(b, fixedCoord, 0.0));
			Line line = new Line(pointer, pointer2);
			line.SetDatabaseDefaults(_database);
			line.Layer = _annotationLayer;
			line.Color = DimStyleManager.ByLayerColor;
			_writer.Append(line);
		}
	}
}
