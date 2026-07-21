using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;

namespace CadAuto.CadAdapter;

public static class AutoCadGeometryConverter
{
	public static Point2D ToCore(Point2d point)
	{
		return new Point2D(point.X, point.Y);
	}

	public static Point2D ToCore(Point3d point)
	{
		return new Point2D(point.X, point.Y);
	}

	public static Point3d ToCad(Point2D point)
	{
		return new Point3d(point.X, point.Y, 0.0);
	}

	public static Circle2D ToCore(Circle circle)
	{
		if (circle == null)
		{
			throw new ArgumentNullException("circle");
		}
		return new Circle2D
		{
			Center = ToCore(circle.Center),
			Radius = circle.Radius,
			SourceKey = circle.ObjectId.ToString()
		};
	}

	public static Arc2D ToCore(Arc arc)
	{
		if (arc == null)
		{
			throw new ArgumentNullException("arc");
		}
		return new Arc2D
		{
			Start = ToCore(arc.StartPoint),
			End = ToCore(arc.EndPoint),
			Center = ToCore(arc.Center),
			Radius = arc.Radius,
			SourceKey = arc.ObjectId.ToString()
		};
	}

	public static Segment2D ToCore(Line line)
	{
		if (line == null)
		{
			throw new ArgumentNullException("line");
		}
		return new Segment2D(ToCore(line.StartPoint), ToCore(line.EndPoint))
		{
			SourceKey = line.ObjectId.ToString()
		};
	}

	public static OutlineFeature2D ToCoreOutline(Polyline polyline)
	{
		if (polyline == null)
		{
			throw new ArgumentNullException("polyline");
		}
		OutlineFeature2D outlineFeature2D = new OutlineFeature2D
		{
			MinX = double.MaxValue,
			MinY = double.MaxValue,
			MaxX = double.MinValue,
			MaxY = double.MinValue
		};
		int numberOfVertices = polyline.NumberOfVertices;
		for (int i = 0; i < numberOfVertices; i++)
		{
			Point2D point2D = ToCore(polyline.GetPoint2dAt(i));
			outlineFeature2D.Vertices.Add(point2D);
			Include(outlineFeature2D, point2D);
		}
		for (int j = 0; j < numberOfVertices; j++)
		{
			int index = (j + 1) % numberOfVertices;
			Point2D start = ToCore(polyline.GetPoint2dAt(j));
			Point2D end = ToCore(polyline.GetPoint2dAt(index));
			double bulgeAt = polyline.GetBulgeAt(j);
			if (Math.Abs(bulgeAt) <= 1E-09)
			{
				outlineFeature2D.Segments.Add(new Segment2D(start, end)
				{
					SourceKey = polyline.ObjectId.ToString()
				});
				continue;
			}
			Arc2D item = CreateBulgeArc(start, end, bulgeAt, polyline.ObjectId.ToString());
			outlineFeature2D.Arcs.Add(item);
			outlineFeature2D.Segments.Add(new Segment2D(start, end)
			{
				SourceKey = polyline.ObjectId.ToString(),
				IsArcChord = true
			});
		}
		return outlineFeature2D;
	}

	private static Arc2D CreateBulgeArc(Point2D start, Point2D end, double bulge, string sourceKey)
	{
		double num = start.DistanceTo(end);
		double num2 = 4.0 * Math.Atan(bulge);
		double num3 = Math.Abs(num / (2.0 * Math.Sin(num2 / 2.0)));
		double num4 = (start.X + end.X) * 0.5;
		double num5 = (start.Y + end.Y) * 0.5;
		double num6 = end.X - start.X;
		double num7 = end.Y - start.Y;
		double num8 = Math.Sqrt(num6 * num6 + num7 * num7);
		double num9 = Math.Sqrt(Math.Max(num3 * num3 - num * num * 0.25, 0.0));
		double num10 = ((bulge >= 0.0) ? 1.0 : (-1.0));
		Point2D center = new Point2D(num4 - num10 * num7 / num8 * num9, num5 + num10 * num6 / num8 * num9);
		return new Arc2D
		{
			Start = start,
			End = end,
			Center = center,
			Radius = num3,
			Bulge = bulge,
			SourceKey = sourceKey
		};
	}

	private static void Include(OutlineFeature2D outline, Point2D point)
	{
		outline.MinX = Math.Min(outline.MinX, point.X);
		outline.MaxX = Math.Max(outline.MaxX, point.X);
		outline.MinY = Math.Min(outline.MinY, point.Y);
		outline.MaxY = Math.Max(outline.MaxY, point.Y);
	}
}
