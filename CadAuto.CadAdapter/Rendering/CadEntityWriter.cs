using System;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
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
	}

	public void AddRotatedDimension(double rotation, Point3d xLine1, Point3d xLine2, Point3d dimLinePoint, string overrideText, bool useSegmentedExtensionLines, bool useCustomTextPosition = false, Point3d customTextPosition = default(Point3d))
	{
		RotatedDimension rotatedDimension = new RotatedDimension(rotation, xLine1, xLine2, dimLinePoint, overrideText ?? string.Empty, _dimStyleId);
		rotatedDimension.SetDatabaseDefaults(_database);
		rotatedDimension.Layer = _annotationLayer;
		rotatedDimension.DimensionStyle = _dimStyleId;
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
