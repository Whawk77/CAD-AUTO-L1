using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Environment;
using CadAuto.Core.Geometry;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Rendering;

public sealed class CadEntityWriter
{
	private readonly Database _database;

	private readonly Transaction _transaction;

	private readonly BlockTableRecord _space;

	private readonly DimensionRuleConfig _config;

	private readonly ObjectId _dimStyleId;

	private readonly double _dimScale;

	private readonly string _annotationLayer;

	private readonly string _groupId;

	private readonly string _kind;

	public CadEntityWriter(Database database, Transaction transaction, BlockTableRecord space, DimensionRuleConfig config, ObjectId dimStyleId, double dimScale, string annotationLayer, string groupId, string kind)
	{
		_database = database;
		_transaction = transaction;
		_space = space;
		_config = config;
		_dimStyleId = dimStyleId;
		_dimScale = ((dimScale <= 0.0) ? 1.0 : dimScale);
		_annotationLayer = annotationLayer;
		_groupId = groupId;
		_kind = kind ?? AnnotationMetadata.KindDimension;
		AnnotationMetadata.EnsureRegApp(database, transaction);
	}

	public void AddRotatedDimension(double rotation, Point3d xLine1, Point3d xLine2, Point3d dimLinePoint, string overrideText, bool useSegmentedExtensionLines, bool useCustomTextPosition = false, Point3d customTextPosition = default(Point3d), bool addDatumRoughness = false, IList<Segment2D> outlineSegments = null)
	{
		RotatedDimension rotatedDimension = new RotatedDimension(rotation, xLine1, xLine2, dimLinePoint, overrideText ?? string.Empty, _dimStyleId);
		rotatedDimension.SetDatabaseDefaults(_database);
		rotatedDimension.Layer = _annotationLayer;
		rotatedDimension.DimensionStyle = _dimStyleId;
		DimStyleManager.ApplyGeneratedDimensionColors(rotatedDimension);
		if (useSegmentedExtensionLines)
		{
			rotatedDimension.Dimse1 = true;
			rotatedDimension.Dimse2 = true;
		}
		Append(rotatedDimension);
		if (useCustomTextPosition)
		{
			rotatedDimension.UsingDefaultTextPosition = false;
			rotatedDimension.TextPosition = customTextPosition;
			rotatedDimension.RecomputeDimensionBlock(forceUpdate: true);
		}
		if (addDatumRoughness)
		{
			InsertDatumRoughnessBlock(rotatedDimension, outlineSegments);
		}
	}

	private void InsertDatumRoughnessBlock(RotatedDimension dimension, IList<Segment2D> outlineSegments)
	{
		BlockTable blocks = (BlockTable)_transaction.GetObject(_database.BlockTableId, OpenMode.ForRead);
		double scale = dimension.Dimscale > 0.0 ? dimension.Dimscale : _dimScale;
		dimension.RecomputeDimensionBlock(forceUpdate: true);
		List<Segment2D> renderedLines = new List<Segment2D>();
		using (DBObjectCollection parts = new DBObjectCollection())
		{
			try
			{
				// Inspect temporary rendered parts; keep the original dimension intact.
				dimension.Explode(parts);
				foreach (DBObject part in parts)
				{
					if (part is Line line)
					{
						renderedLines.Add(new Segment2D(new Point2D(line.StartPoint.X, line.StartPoint.Y),
							new Point2D(line.EndPoint.X, line.EndPoint.Y)));
					}
				}
			}
			catch (Autodesk.AutoCAD.Runtime.Exception ex)
			{
				Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n无法读取基准尺寸界线，已跳过粗糙度并保留定位尺寸: {0}", ex.Message);
				return;
			}
			finally
			{
				foreach (DBObject part in parts)
				{
					part.Dispose();
				}
			}
		}
		Point2D datumPoint = new Point2D(dimension.XLine1Point.X, dimension.XLine1Point.Y);
		if (dimension.Dimse1 || !DimensionLayoutRules.TryGetDatumRoughnessPlacement(
			datumPoint,
			dimension.Rotation, renderedLines,
			_config.GeometryTolerance, out var point, out var rotation, out Segment2D extension))
		{
			Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n基准点侧尺寸界线无可见线段，已跳过基准尺寸粗糙度。");
			return;
		}
		double textHeight = dimension.Dimtxt > 0.0 ? dimension.Dimtxt * scale : Scale(_config.TextHeight);
		string visibleText = VisibleDimensionText(dimension);
		double symbolMargin = textHeight * 1.6;
		double textHalfAlong = Math.Max(visibleText.Length, 1) * textHeight * 0.45 + symbolMargin;
		double textHalfAcross = textHeight * 0.7 + symbolMargin;
		point = DimensionLayoutRules.ClearDatumRoughnessFromDimensionText(
			point,
			extension,
			new Point2D(dimension.TextPosition.X, dimension.TextPosition.Y),
			dimension.TextRotation,
			textHalfAlong,
			textHalfAcross);
		Point2D measuredPoint = new Point2D(dimension.XLine2Point.X, dimension.XLine2Point.Y);
		rotation = DimensionLayoutRules.ApplyDatumRoughnessEndRotation(
			rotation, datumPoint, measuredPoint, extension, outlineSegments, _config.GeometryTolerance);
		string blockName = DimensionLayoutRules.SelectDatumRoughnessBlockName(
			datumPoint,
			measuredPoint,
			extension,
			outlineSegments,
			_config.GeometryTolerance);
		if (!blocks.Has(blockName))
		{
			Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n未找到块 {0}，已跳过基准尺寸粗糙度，定位尺寸保留。", blockName);
			return;
		}
		BlockReference block = new BlockReference(new Point3d(point.X, point.Y, dimension.XLine1Point.Z), blocks[blockName]);
		block.SetDatabaseDefaults(_database);
		block.Layer = _annotationLayer;
		block.Color = DimStyleManager.ByLayerColor;
		block.ScaleFactors = new Scale3d(scale);
		block.Rotation = rotation;
		Append(block);
	}

	private static string VisibleDimensionText(RotatedDimension dimension)
	{
		string text = dimension.DimensionText ?? string.Empty;
		System.Text.StringBuilder visible = new System.Text.StringBuilder();
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] == '\\')
			{
				int end = text.IndexOf(';', i);
				i = end < 0 ? text.Length : end;
				continue;
			}
			if (i + 1 < text.Length && text[i] == '<' && text[i + 1] == '>')
			{
				visible.Append(dimension.Measurement.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
				i++;
				continue;
			}
			visible.Append(text[i]);
		}
		return visible.Length == 0 ? dimension.Measurement.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : visible.ToString();
	}

	public ObjectId GetDimStyleTextStyle(ObjectId dimStyleId)
	{
		DimStyleTableRecord dimStyleTableRecord = _transaction.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
		if (dimStyleTableRecord != null && !dimStyleTableRecord.Dimtxsty.IsNull)
		{
			return dimStyleTableRecord.Dimtxsty;
		}
		return _database.Textstyle;
	}

	public string GetCurrentLayerName()
	{
		LayerTableRecord layerTableRecord = _transaction.GetObject(_database.Clayer, OpenMode.ForRead) as LayerTableRecord;
		return (layerTableRecord != null) ? layerTableRecord.Name : _annotationLayer;
	}

	public Color GetCurrentEntityColor()
	{
		string text = Convert.ToString(Application.GetSystemVariable("CECOLOR"));
		if (string.IsNullOrWhiteSpace(text))
		{
			return _database.Cecolor;
		}
		text = text.Trim();
		if (string.Equals(text, "BYLAYER", StringComparison.OrdinalIgnoreCase))
		{
			return Color.FromColorIndex(ColorMethod.ByLayer, 256);
		}
		if (string.Equals(text, "BYBLOCK", StringComparison.OrdinalIgnoreCase))
		{
			return Color.FromColorIndex(ColorMethod.ByBlock, 0);
		}
		if (short.TryParse(text, out var result))
		{
			return Color.FromColorIndex(ColorMethod.ByAci, result);
		}
		switch (text.ToUpperInvariant())
		{
		case "RED":
		case "红":
			return Color.FromColorIndex(ColorMethod.ByAci, 1);
		case "YELLOW":
		case "黄":
			return Color.FromColorIndex(ColorMethod.ByAci, 2);
		case "GREEN":
		case "绿":
			return Color.FromColorIndex(ColorMethod.ByAci, 3);
		case "CYAN":
		case "青":
			return Color.FromColorIndex(ColorMethod.ByAci, 4);
		case "BLUE":
		case "蓝":
			return Color.FromColorIndex(ColorMethod.ByAci, 5);
		case "MAGENTA":
		case "洋红":
			return Color.FromColorIndex(ColorMethod.ByAci, 6);
		case "WHITE":
		case "白":
			return Color.FromColorIndex(ColorMethod.ByAci, 7);
		default:
			return _database.Cecolor;
		}
	}

	public Color GetDimStyleTextColor(ObjectId dimStyleId)
	{
		DimStyleTableRecord dimStyleTableRecord = _transaction.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
		if (dimStyleTableRecord != null && dimStyleTableRecord.Dimclrt != null)
		{
			return dimStyleTableRecord.Dimclrt;
		}
		return Color.FromColorIndex(ColorMethod.ByLayer, 256);
	}

	public double GetDimStyleTextHeight(ObjectId dimStyleId)
	{
		DimStyleTableRecord dimStyleTableRecord = _transaction.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
		if (dimStyleTableRecord != null && dimStyleTableRecord.Dimtxt > _config.GeometryTolerance)
		{
			return dimStyleTableRecord.Dimtxt * _dimScale;
		}
		return Scale(_config.TextHeight);
	}

	public double GetDimStyleArrowSize(ObjectId dimStyleId)
	{
		DimStyleTableRecord dimStyleTableRecord = _transaction.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
		if (dimStyleTableRecord != null && dimStyleTableRecord.Dimasz > _config.GeometryTolerance)
		{
			return dimStyleTableRecord.Dimasz * _dimScale;
		}
		return Scale(_config.ArrowSize);
	}

	public int GetDimStyleLinearPrecision(ObjectId dimStyleId)
	{
		DimStyleTableRecord dimStyleTableRecord = _transaction.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
		if (dimStyleTableRecord == null)
		{
			return 3;
		}
		return Math.Max(0, Math.Min(8, dimStyleTableRecord.Dimdec));
	}

	public void Append(Entity entity)
	{
		_space.AppendEntity(entity);
		_transaction.AddNewlyCreatedDBObject(entity, add: true);
		AnnotationMetadata.Mark(entity, _groupId, _kind);
	}

	public double Scale(double value)
	{
		return value * _dimScale;
	}
}
