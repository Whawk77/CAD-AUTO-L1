using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadAuto.CadAdapter.Rendering;

public sealed class DebugAnnotationRenderer
{
	private readonly Database _database;

	private readonly CadEntityWriter _writer;

	private readonly ObjectId _textStyleId;

	private readonly string _annotationLayer;

	public DebugAnnotationRenderer(Database database, CadEntityWriter writer, ObjectId textStyleId, string annotationLayer)
	{
		_database = database;
		_writer = writer;
		_textStyleId = textStyleId;
		_annotationLayer = annotationLayer;
	}

	public void AddDimensionLabel(string label, Point3d point, double textHeight, Color color)
	{
		AddDimensionLabel(label, point, point, textHeight, color);
	}

	public void AddDimensionLabel(string label, Point3d point, Point3d anchor, double textHeight, Color color)
	{
		if (!string.IsNullOrEmpty(label))
		{
			if (point.DistanceTo(anchor) > 1E-08)
			{
				AddLine(point, anchor, color);
			}
			MText mText = new MText();
			mText.SetDatabaseDefaults(_database);
			mText.Contents = label;
			mText.TextHeight = textHeight;
			mText.TextStyleId = _textStyleId;
			mText.Location = point;
			mText.Attachment = AttachmentPoint.MiddleCenter;
			mText.Layer = _annotationLayer;
			mText.Color = color;
			_writer.Append(mText);
		}
	}

	public void AddPointLabel(Point2d point, string label, short colorIndex, double xOffset, double yOffset, double textHeight)
	{
		if (!string.IsNullOrEmpty(label))
		{
			Point3d point3d = new Point3d(point.X + xOffset, point.Y + yOffset, 0.0);
			Point3d end = new Point3d(point.X, point.Y, 0.0);
			AddLine(point3d, end, colorIndex);
			MText mText = new MText();
			mText.SetDatabaseDefaults(_database);
			mText.Contents = label;
			mText.TextHeight = textHeight;
			mText.TextStyleId = _textStyleId;
			mText.Location = point3d;
			mText.Attachment = AttachmentPoint.MiddleCenter;
			mText.Layer = _annotationLayer;
			mText.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
			_writer.Append(mText);
		}
	}

	public void AddLine(Point3d start, Point3d end, short colorIndex)
	{
		AddLine(start, end, Color.FromColorIndex(ColorMethod.ByAci, colorIndex));
	}

	private void AddLine(Point3d start, Point3d end, Color color)
	{
		Line line = new Line(start, end);
		line.SetDatabaseDefaults(_database);
		line.Layer = _annotationLayer;
		line.Color = color;
		_writer.Append(line);
	}
}
