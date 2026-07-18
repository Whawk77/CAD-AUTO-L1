using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Mapping;
using CadAuto.CadAdapter.Model;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Rendering;

public sealed class DimensionDrawer
{
	private enum DimSide
	{
		Bottom,
		Top,
		Left,
		Right
	}

	private struct DeferredDim
	{
		public double Rotation;

		public Point3d XLine1;

		public Point3d XLine2;

		public string OverrideText;

		public double Span;

		public DimensionType DimType;

		public bool UseSegmentedExtensionLines;

		public bool ForceOuterLevel;

		public int LooseChainId;

		public string AlignmentKey;

		public int AlignmentPriority;

		public bool PreferLocalBoundary;

		public bool PreferFeatureLocalPlacement;

		public bool PreservePreferredSide;

		public string DebugOwner;

		public string DebugRole;

		public int DebugIndex;
	}

	private struct PlacedDim
	{
		public DeferredDim Dim;

		public DimSide Side;

		public Point3d DimLinePoint;

		public TextBounds TextBounds;

		public bool HasCustomTextPosition;

		public Point3d TextPosition;

		public bool UsesLocalBoundary;
	}

	private struct TextBounds
	{
		public double MinX;

		public double MaxX;

		public double MinY;

		public double MaxY;
	}

	private readonly DimensionRuleConfig _config;

	private readonly ObjectId _dimStyleId;

	private readonly ObjectId _diameterCalloutDimStyleId;

	private readonly double _dimScale;

	private readonly bool _diagnosticsEnabled;

	private readonly DiagnosticDimensionSide _diagnosticSide;

	private readonly CadEntityWriter _entityWriter;

	private readonly CornerCalloutRenderer _cornerCalloutRenderer;

	private readonly DebugAnnotationRenderer _debugAnnotationRenderer;

	private readonly DimensionExtensionLineRenderer _extensionLineRenderer;

	private readonly DimensionDeduplicationRules _dimensionDeduplicationRules;

	private readonly DimensionLayoutRules _dimensionLayoutRules;

	private readonly DimensionPlanCadMapper _dimensionPlanMapper = new DimensionPlanCadMapper();

	private readonly Transaction _transaction;

	private readonly BlockTableRecord _space;

	private readonly List<DeferredDim> _bottomDims = new List<DeferredDim>();

	private readonly List<DeferredDim> _topDims = new List<DeferredDim>();

	private readonly List<DeferredDim> _leftDims = new List<DeferredDim>();

	private readonly List<DeferredDim> _rightDims = new List<DeferredDim>();

	private readonly List<TextBounds> _linearDimTextObstacles = new List<TextBounds>();

	private void AddRotatedDimension(double rotation, Point3d xLine1, Point3d xLine2, Point3d dimLinePoint, string overrideText, bool useSegmentedExtensionLines, bool useCustomTextPosition = false, Point3d customTextPosition = default(Point3d))
	{
		_entityWriter.AddRotatedDimension(rotation, xLine1, xLine2, dimLinePoint, overrideText, useSegmentedExtensionLines, useCustomTextPosition, customTextPosition);
	}

	private ObjectId GetDimStyleTextStyle(ObjectId dimStyleId)
	{
		return _entityWriter.GetDimStyleTextStyle(dimStyleId);
	}

	private string GetCurrentLayerName()
	{
		return _entityWriter.GetCurrentLayerName();
	}

	private Color GetCurrentEntityColor()
	{
		return _entityWriter.GetCurrentEntityColor();
	}

	private Color GetDimStyleTextColor(ObjectId dimStyleId)
	{
		return _entityWriter.GetDimStyleTextColor(dimStyleId);
	}

	private double GetDimStyleTextHeight(ObjectId dimStyleId)
	{
		return _entityWriter.GetDimStyleTextHeight(dimStyleId);
	}

	private double GetDimStyleArrowSize(ObjectId dimStyleId)
	{
		return _entityWriter.GetDimStyleArrowSize(dimStyleId);
	}

	private int GetDimStyleLinearPrecision(ObjectId dimStyleId)
	{
		return _entityWriter.GetDimStyleLinearPrecision(dimStyleId);
	}

	private static string FormatNumberWithPrecision(double value, int precision)
	{
		double num = Math.Round(value, precision);
		double num2 = Math.Round(num);
		if (Math.Abs(num - num2) <= 1E-09)
		{
			return num2.ToString("0", CultureInfo.InvariantCulture);
		}
		string text = ((precision <= 0) ? "0" : ("0." + new string('0', precision)));
		return num.ToString(text, CultureInfo.InvariantCulture);
	}

	private void Append(Entity entity)
	{
		_entityWriter.Append(entity);
	}

	private double Scale(double value)
	{
		return _entityWriter.Scale(value);
	}

	private void SuppressMirroredHorizontalDuplicates()
	{
		for (int num = _topDims.Count - 1; num >= 0; num--)
		{
			DeferredDim top = _topDims[num];
			if (CanSuppressMirroredHorizontalDimension(top) && _bottomDims.Any((DeferredDim bottom) => CanSuppressMirroredHorizontalDimension(bottom) && IsDuplicate(top, bottom, isHorizontal: true)))
			{
				_topDims.RemoveAt(num);
			}
		}
		SuppressMirroredHoleRelatedDuplicates(_bottomDims, _topDims, isHorizontal: true);
	}

	private bool CanSuppressMirroredHorizontalDimension(DeferredDim dim)
	{
		return _dimensionDeduplicationRules.CanSuppressMirroredNormalDimension(ToDeduplicationItem(dim));
	}

	private void SuppressMirroredVerticalDuplicates()
	{
		for (int num = _rightDims.Count - 1; num >= 0; num--)
		{
			DeferredDim right = _rightDims[num];
			if (CanSuppressMirroredVerticalDimension(right) && _leftDims.Any((DeferredDim left) => CanSuppressMirroredVerticalDimension(left) && IsDuplicate(right, left, isHorizontal: false)))
			{
				_rightDims.RemoveAt(num);
			}
		}
		SuppressMirroredHoleRelatedDuplicates(_leftDims, _rightDims, isHorizontal: false);
	}

	private bool CanSuppressMirroredVerticalDimension(DeferredDim dim)
	{
		return _dimensionDeduplicationRules.CanSuppressMirroredNormalDimension(ToDeduplicationItem(dim));
	}

	private void SuppressMirroredHoleRelatedDuplicates(List<DeferredDim> primaryDims, List<DeferredDim> secondaryDims, bool isHorizontal)
	{
		for (int num = secondaryDims.Count - 1; num >= 0; num--)
		{
			DeferredDim deferredDim = secondaryDims[num];
			for (int num2 = primaryDims.Count - 1; num2 >= 0; num2--)
			{
				DeferredDim deferredDim2 = primaryDims[num2];
				if (CanSuppressMirroredHoleRelatedDimension(deferredDim2, deferredDim) && IsSameMeasuredDimension(deferredDim2, deferredDim, isHorizontal))
				{
					if (CompareDuplicatePreference(deferredDim, deferredDim2) > 0)
					{
						primaryDims.RemoveAt(num2);
					}
					else
					{
						secondaryDims.RemoveAt(num);
					}
					break;
				}
			}
		}
	}

	private bool CanSuppressMirroredHoleRelatedDimension(DeferredDim a, DeferredDim b)
	{
		return _dimensionDeduplicationRules.CanSuppressMirroredHoleRelatedDimension(ToDeduplicationItem(a), ToDeduplicationItem(b));
	}

	private void SuppressDuplicateMeasuredDimensions(List<DeferredDim> dims, bool isHorizontal)
	{
		for (int i = 0; i < dims.Count; i++)
		{
			int num = i;
			for (int j = i + 1; j < dims.Count; j++)
			{
				if (IsSameMeasuredDimension(dims[i], dims[j], isHorizontal) && CompareDuplicatePreference(dims[j], dims[num]) > 0)
				{
					num = j;
				}
			}
			if (num != i)
			{
				DeferredDim value = dims[num];
				dims[num] = dims[i];
				dims[i] = value;
			}
			for (int num2 = dims.Count - 1; num2 > i; num2--)
			{
				if (IsSameMeasuredDimension(dims[i], dims[num2], isHorizontal))
				{
					dims.RemoveAt(num2);
				}
			}
		}
	}

	private bool IsSameMeasuredDimension(DeferredDim a, DeferredDim b, bool isHorizontal)
	{
		return _dimensionDeduplicationRules.IsSameMeasuredDimension(ToDeduplicationItem(a), ToDeduplicationItem(b), isHorizontal);
	}

	private bool IsDuplicate(DeferredDim a, DeferredDim b, bool isHorizontal)
	{
		return _dimensionDeduplicationRules.IsDuplicate(ToDeduplicationItem(a), ToDeduplicationItem(b), isHorizontal);
	}

	private int CompareDuplicatePreference(DeferredDim a, DeferredDim b)
	{
		return _dimensionDeduplicationRules.CompareDuplicatePreference(ToDeduplicationItem(a), ToDeduplicationItem(b));
	}

	private void AssignDebugIndexes(IList<PlacedDim> placedDims)
	{
		Dictionary<string, List<int>> dictionary = new Dictionary<string, List<int>>();
		for (int i = 0; i < placedDims.Count; i++)
		{
			PlacedDim placed = placedDims[i];
			string key = GetDebugOwnerText(placed.Dim) + "|" + GetDebugRoleText(placed.Dim) + "|" + GetDebugBoundaryText(placed) + "|" + GetDebugSideText(placed.Side);
			if (!dictionary.TryGetValue(key, out var value))
			{
				value = (dictionary[key] = new List<int>());
			}
			value.Add(i);
		}
		foreach (List<int> value3 in dictionary.Values)
		{
			List<int> list2 = (from index in value3
				orderby GetDebugIndexSortKey(placedDims[index]), placedDims[index].Dim.Span
				select index).ThenBy((int result) => result).ToList();
			for (int num = 0; num < list2.Count; num++)
			{
				PlacedDim value2 = placedDims[list2[num]];
				value2.Dim.DebugIndex = num + 1;
				placedDims[list2[num]] = value2;
			}
		}
	}

	private static double GetDebugIndexSortKey(PlacedDim placed)
	{
		switch (placed.Side)
		{
		case DimSide.Left:
		case DimSide.Right:
			return 0.0 - placed.DimLinePoint.Y;
		case DimSide.Bottom:
		case DimSide.Top:
			return placed.DimLinePoint.X;
		default:
			return 0.0;
		}
	}

	private void AddDimensionDebugLabel(PlacedDim placed, bool isHorizontal, double textHeight)
	{
		string text = BuildDimensionDebugLabel(placed);
		if (!string.IsNullOrEmpty(text))
		{
			double num = Math.Max(textHeight * 0.38, Scale(1.2));
			double num2 = num * 2.2;
			Point3d dimLinePoint = placed.DimLinePoint;
			dimLinePoint = ((!isHorizontal) ? ((placed.Side == DimSide.Right) ? new Point3d(dimLinePoint.X + num2, dimLinePoint.Y, 0.0) : new Point3d(dimLinePoint.X - num2, dimLinePoint.Y, 0.0)) : ((placed.Side == DimSide.Top) ? new Point3d(dimLinePoint.X, dimLinePoint.Y + num2, 0.0) : new Point3d(dimLinePoint.X, dimLinePoint.Y - num2, 0.0)));
			_debugAnnotationRenderer.AddDimensionLabel(text, dimLinePoint, num, GetDebugLabelColor(placed.Dim));
		}
	}

	private string BuildDimensionDebugLabel(PlacedDim placed)
	{
		string debugOwnerText = GetDebugOwnerText(placed.Dim);
		string text = GetDebugRoleText(placed.Dim);
		if (placed.Dim.DebugIndex > 0)
		{
			text += placed.Dim.DebugIndex.ToString(CultureInfo.InvariantCulture);
		}
		return debugOwnerText + "|" + text + "|" + GetDebugBoundaryText(placed) + "|" + GetDebugSideText(placed.Side);
	}

	private static string GetDebugBoundaryText(PlacedDim placed)
	{
		return placed.UsesLocalBoundary ? "LB" : "GB";
	}

	private string GetDebugOwnerText(DeferredDim dim)
	{
		if (!string.IsNullOrEmpty(dim.DebugOwner))
		{
			return dim.DebugOwner;
		}
		if (dim.DimType == DimensionType.DatumHoleLocationX || dim.DimType == DimensionType.DatumHoleLocationY)
		{
			return "DAT";
		}
		return "GEN";
	}

	private string GetDebugRoleText(DeferredDim dim)
	{
		string text = (string.IsNullOrEmpty(dim.DebugRole) ? dim.DimType.ToString() : dim.DebugRole);
		return text switch
		{
			"PinDistance" => "PD",
			"PinGroupDistance" => "GD",
			"FunctionalHole" => "FH",
			"LooseHole" => "LH",
			"DatumX" => "DX",
			"DatumY" => "DY",
			"DatumHoleLocationX" => "DX",
			"DatumHoleLocationY" => "DY",
			"OverallWidth" => "OW",
			"OverallHeight" => "OH",
			"HoleLocation" => "HL",
			"Normal" => "N",
			"TopStructWidth" => "TSW",
			"BottomStructWidth" => "BSW",
			"LeftStructHeight" => "LSH",
			"RightStructHeight" => "RSH",
			"SlotCenter" => "SC",
			"SlotChainH" => "SCH",
			"SlotChainV" => "SCV",
			"SlotDatumH" => "SDH",
			"SlotDatumV" => "SDV",
			"SlotPinRef" => "SPR",
			"SingleArcSlotDatum" => "SAS",
			_ => text,
		};
	}

	private static string GetDebugSideText(DimSide side)
	{
		return side switch
		{
			DimSide.Bottom => "B",
			DimSide.Top => "T",
			DimSide.Left => "L",
			DimSide.Right => "R",
			_ => side.ToString(),
		};
	}

	private Color GetDebugLabelColor(DeferredDim dim)
	{
		short colorIndex;
		switch (GetDebugRoleText(dim))
		{
		case "PD":
			colorIndex = 1;
			break;
		case "GD":
			colorIndex = 6;
			break;
		case "FH":
			colorIndex = 4;
			break;
		case "LH":
			colorIndex = 2;
			break;
		case "OW":
		case "OH":
			colorIndex = 3;
			break;
		default:
			colorIndex = 8;
			break;
		}
		return Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
	}

	private bool TryGetLocalDimLineCoordinate(DeferredDim dim, DimSide side, OutlineFeature outline, double offset, out double coordinate)
	{
		return _dimensionLayoutRules.TryGetLocalDimLineCoordinate(ToLayoutItem(dim), ToCoreDimensionSide(side), ToCoreOutlineOrNull(outline), offset, out coordinate);
	}

	private bool TryGetDimensionLocalBoundary(DeferredDim dim, DimSide side, OutlineFeature outline, out double boundary)
	{
		return _dimensionLayoutRules.TryGetDimensionLocalBoundary(ToLayoutItem(dim), ToCoreDimensionSide(side), ToCoreOutlineOrNull(outline), out boundary);
	}

	private bool DimensionLineEntersOutlineInterior(DeferredDim dim, DimSide side, double coordinate, OutlineFeature outline)
	{
		return _dimensionLayoutRules.DimensionLineEntersOutlineInterior(ToLayoutItem(dim), ToCoreDimensionSide(side), coordinate, ToCoreOutlineOrNull(outline));
	}

	private bool IsPointOnAnyOutlineSegment(Point2d point, OutlineFeature outline)
	{
		return _dimensionLayoutRules.IsPointOnAnyOutlineSegment(new Point2D(point.X, point.Y), ToCoreOutlineOrNull(outline));
	}

	private bool TryGetLocalHoleLocationBoundary(DeferredDim dim, DimSide side, OutlineFeature outline, out double boundary)
	{
		return _dimensionLayoutRules.TryGetLocalHoleLocationBoundary(ToLayoutItem(dim), ToCoreDimensionSide(side), ToCoreOutlineOrNull(outline), out boundary);
	}

	private bool CanUseLocalDimensionBoundary(DeferredDim dim)
	{
		return _dimensionLayoutRules.CanUseLocalDimensionBoundary(ToLayoutItem(dim));
	}

	private double IntervalOverlap(double firstMin, double firstMax, double secondMin, double secondMax)
	{
		return _dimensionLayoutRules.IntervalOverlap(firstMin, firstMax, secondMin, secondMax);
	}

	private TextBounds ComputePlacedTextBounds(DeferredDim dim, Point3d dimLinePoint, bool isHorizontal, double textHeight)
	{
		TextBounds2D bounds = _dimensionLayoutRules.ComputePlacedTextBounds(ToLayoutItem(dim), new Point2D(dimLinePoint.X, dimLinePoint.Y), isHorizontal, textHeight);
		return ToTextBounds(bounds);
	}

	private (double A, double B) ComputeTextInterval(DeferredDim dim, bool isHorizontal, double textHeight)
	{
		Tuple<double, double> tuple = _dimensionLayoutRules.ComputeTextInterval(ToLayoutItem(dim), isHorizontal, textHeight);
		return (A: tuple.Item1, B: tuple.Item2);
	}

	private double GetDimensionTextLength(DeferredDim dim, double textHeight)
	{
		return _dimensionLayoutRules.GetDimensionTextLength(ToLayoutItem(dim), textHeight);
	}

	private string GetDimensionText(DeferredDim dim)
	{
		return _dimensionLayoutRules.GetDimensionText(ToLayoutItem(dim));
	}

	private bool TextCoversOutline(DeferredDim dim, DimSide side, double offset, double textHeight, OutlineFeature outline, bool isHorizontal)
	{
		return _dimensionLayoutRules.TextCoversOutline(ToLayoutItem(dim), ToCoreDimensionSide(side), offset, textHeight, ToCoreOutlineOrNull(outline), isHorizontal);
	}

	private bool IsGlobalBoundaryCoordinate(double coordinate, DimSide side, OutlineFeature outline)
	{
		return _dimensionLayoutRules.IsGlobalBoundaryCoordinate(coordinate, ToCoreDimensionSide(side), ToCoreOutlineOrNull(outline));
	}

	public void FlushStackedDimensions(OutlineFeature outline)
	{
		double dimStyleTextHeight = GetDimStyleTextHeight(_dimStyleId);
		double perLevelSpacing = dimStyleTextHeight + Scale(_config.DimTextClearance);
		RebalanceVerticalHoleLocationSides();
		SuppressMirroredHorizontalDuplicates();
		SuppressMirroredVerticalDuplicates();
		if (ShouldFlushDiagnosticSide(DimSide.Bottom))
		{
			FlushSide(_bottomDims, DimSide.Bottom, outline, perLevelSpacing);
		}
		if (ShouldFlushDiagnosticSide(DimSide.Top))
		{
			FlushSide(_topDims, DimSide.Top, outline, perLevelSpacing);
		}
		if (ShouldFlushDiagnosticSide(DimSide.Left))
		{
			FlushSide(_leftDims, DimSide.Left, outline, perLevelSpacing);
		}
		if (ShouldFlushDiagnosticSide(DimSide.Right))
		{
			FlushSide(_rightDims, DimSide.Right, outline, perLevelSpacing);
		}
	}

	private bool ShouldFlushDiagnosticSide(DimSide side)
	{
		return !_diagnosticsEnabled || _diagnosticSide == DiagnosticDimensionSide.All || MatchesDiagnosticSide(side);
	}

	private bool MatchesDiagnosticSide(DimSide side)
	{
		return (side == DimSide.Bottom && _diagnosticSide == DiagnosticDimensionSide.Bottom) || (side == DimSide.Top && _diagnosticSide == DiagnosticDimensionSide.Top) || (side == DimSide.Left && _diagnosticSide == DiagnosticDimensionSide.Left) || (side == DimSide.Right && _diagnosticSide == DiagnosticDimensionSide.Right);
	}

	private void FlushSide(List<DeferredDim> dims, DimSide side, OutlineFeature outline, double perLevelSpacing)
	{
		if (dims.Count == 0)
		{
			return;
		}
		double dimStyleTextHeight = GetDimStyleTextHeight(_dimStyleId);
		double gap = dimStyleTextHeight * 0.5;
		double arrowSize = GetDimStyleArrowSize(_dimStyleId);
		double textArrowClearance = Math.Max(gap, Scale(_config.DimTextClearance));
		bool isHorizontal = side == DimSide.Bottom || side == DimSide.Top;
		IList<DimensionStackingPlacement> placements = CreateSideStackingPlacements(dims, side, outline, dimStyleTextHeight, gap, perLevelSpacing, isHorizontal);
		List<PlacedDim> list = BuildPlacedDimensions(dims, side, outline, placements, isHorizontal, dimStyleTextHeight);
		if (_diagnosticsEnabled)
		{
			AssignDebugIndexes(list);
		}
		AdjustShortLocalDimensionTextPositions(list, dimStyleTextHeight, arrowSize, textArrowClearance);
		foreach (PlacedDim item in list)
		{
			_linearDimTextObstacles.Add(item.TextBounds);
		}
		RenderPlacedDimensions(list, isHorizontal, dimStyleTextHeight);
	}

	private IList<DimensionStackingPlacement> CreateSideStackingPlacements(List<DeferredDim> dims, DimSide side, OutlineFeature outline, double textHeight, double gap, double perLevelSpacing, bool isHorizontal)
	{
		List<DeferredDim> stableSpanOrder = dims
			.Select((DeferredDim dim, int generationOrder) => new { Dim = dim, GenerationOrder = generationOrder })
			.OrderBy(item => item.Dim.Span)
			.ThenBy(item => item.GenerationOrder)
			.Select(item => item.Dim)
			.ToList();
		dims.Clear();
		dims.AddRange(stableSpanOrder);
		SuppressDuplicateMeasuredDimensions(dims, isHorizontal);
		double num = Scale(_config.FirstDimOffset);
		num += GetExistingGeneratedDimensionReservedOffset(side, outline, num, perLevelSpacing, isHorizontal);
		List<DimensionLayoutItem> list = new List<DimensionLayoutItem>();
		foreach (DeferredDim dim in dims)
		{
			list.Add(ToLayoutItem(dim));
		}
		return _dimensionLayoutRules.CreateStackingPlan(list, ToCoreDimensionSide(side), ToCoreOutlineOrNull(outline), textHeight, gap, num, perLevelSpacing, isHorizontal);
	}

	private double GetExistingGeneratedDimensionReservedOffset(DimSide side, OutlineFeature outline, double firstOffset, double perLevelSpacing, bool isHorizontal)
	{
		if (_space == null || outline == null || perLevelSpacing <= _config.GeometryTolerance)
		{
			return 0.0;
		}
		double num = 0.0;
		foreach (ObjectId item in _space)
		{
			RotatedDimension rotatedDimension = _transaction.GetObject(item, OpenMode.ForRead, openErased: false) as RotatedDimension;
			if (!(rotatedDimension == null) && AnnotationMetadata.IsMarked(rotatedDimension) && IsExistingDimensionOnSide(rotatedDimension, side, outline, isHorizontal, out var offset) && offset > num)
			{
				num = offset;
			}
		}
		if (num <= _config.GeometryTolerance)
		{
			return 0.0;
		}
		int num2 = (int)Math.Floor(Math.Max(0.0, num - firstOffset) / perLevelSpacing) + 1;
		return (double)num2 * perLevelSpacing;
	}

	private static bool IsExistingDimensionOnSide(RotatedDimension dimension, DimSide side, OutlineFeature outline, bool isHorizontal, out double offset)
	{
		offset = 0.0;
		if (!IsExistingDimensionRelevantToOutline(dimension, outline))
		{
			return false;
		}
		Point3d dimLinePoint = dimension.DimLinePoint;
		if (isHorizontal)
		{
			if (!(Math.Abs(Math.Sin(dimension.Rotation)) < 0.5))
			{
				return false;
			}
			if (side == DimSide.Bottom && dimLinePoint.Y <= outline.MinY)
			{
				offset = outline.MinY - dimLinePoint.Y;
				return true;
			}
			if (side == DimSide.Top && dimLinePoint.Y >= outline.MaxY)
			{
				offset = dimLinePoint.Y - outline.MaxY;
				return true;
			}
			return false;
		}
		if (!(Math.Abs(Math.Cos(dimension.Rotation)) < 0.5))
		{
			return false;
		}
		if (side == DimSide.Left && dimLinePoint.X <= outline.MinX)
		{
			offset = outline.MinX - dimLinePoint.X;
			return true;
		}
		if (side == DimSide.Right && dimLinePoint.X >= outline.MaxX)
		{
			offset = dimLinePoint.X - outline.MaxX;
			return true;
		}
		return false;
	}

	private static bool IsExistingDimensionRelevantToOutline(RotatedDimension dimension, OutlineFeature outline)
	{
		double padding = Math.Max(1.0, Math.Max(outline.Width, outline.Height) * 0.02);
		return IsPointNearOutlineBounds(dimension.XLine1Point, outline, padding) || IsPointNearOutlineBounds(dimension.XLine2Point, outline, padding);
	}

	private static bool IsPointNearOutlineBounds(Point3d point, OutlineFeature outline, double padding)
	{
		return point.X >= outline.MinX - padding && point.X <= outline.MaxX + padding && point.Y >= outline.MinY - padding && point.Y <= outline.MaxY + padding;
	}

	private List<PlacedDim> BuildPlacedDimensions(List<DeferredDim> dims, DimSide side, OutlineFeature outline, IList<DimensionStackingPlacement> placements, bool isHorizontal, double textHeight)
	{
		List<PlacedDim> list = new List<PlacedDim>();
		foreach (DimensionStackingPlacement placement in placements)
		{
			DeferredDim dim = dims[placement.Index];
			Point3d dimLinePoint = GetDimLinePoint(dim, side, outline, placement.Offset);
			if (placement.DimLineCoordinateOverride.HasValue)
			{
				dimLinePoint = isHorizontal
					? new Point3d(dimLinePoint.X, placement.DimLineCoordinateOverride.Value, 0.0)
					: new Point3d(placement.DimLineCoordinateOverride.Value, dimLinePoint.Y, 0.0);
			}
			list.Add(new PlacedDim
			{
				Dim = dim,
				Side = side,
				DimLinePoint = dimLinePoint,
				TextBounds = ComputePlacedTextBounds(dim, dimLinePoint, isHorizontal, textHeight),
				UsesLocalBoundary = TryGetLocalDimLineCoordinate(dim, side, outline, placement.Offset, out var _)
			});
		}
		return list;
	}

	private void RenderPlacedDimensions(IList<PlacedDim> placedDims, bool isHorizontal, double textHeight)
	{
		foreach (PlacedDim placedDim in placedDims)
		{
			AddRotatedDimension(placedDim.Dim.Rotation, placedDim.Dim.XLine1, placedDim.Dim.XLine2, placedDim.DimLinePoint, placedDim.Dim.OverrideText, useSegmentedExtensionLines: false, placedDim.HasCustomTextPosition, placedDim.TextPosition);
			if (_diagnosticsEnabled)
			{
				AddDimensionDebugLabel(placedDim, isHorizontal, textHeight);
			}
		}
	}

	private void AdjustShortLocalDimensionTextPositions(IList<PlacedDim> placedDims, double textHeight, double arrowSize, double clearance)
	{
		List<DimensionTextPlacementItem> placedDimensions = placedDims.Select((PlacedDim placed) => new DimensionTextPlacementItem
		{
			Dimension = ToLayoutItem(placed.Dim),
			Side = ToCoreDimensionSide(placed.Side),
			DimLinePoint = new Point2D(placed.DimLinePoint.X, placed.DimLinePoint.Y),
			TextBounds = ToCoreTextBounds(placed.TextBounds)
		}).ToList();
		List<DimensionTextSlidePlacement> list = _dimensionLayoutRules.SelectShortLocalDimensionTextSlides(placedDimensions, _linearDimTextObstacles.Select(ToCoreTextBounds), textHeight, arrowSize, clearance);
		foreach (DimensionTextSlidePlacement item in list)
		{
			PlacedDim value = placedDims[item.Index];
			value.HasCustomTextPosition = true;
			value.TextPosition = new Point3d(item.TextPosition.X, item.TextPosition.Y, 0.0);
			value.TextBounds = ToTextBounds(item.TextBounds);
			placedDims[item.Index] = value;
		}
	}

	public DimensionDrawer(Database db, Transaction tr, BlockTableRecord space, DimensionRuleConfig config, ObjectId dimStyleId, ObjectId diameterCalloutDimStyleId, double dimScale, string annotationLayer, string groupId, bool diagnosticsEnabled = false, DiagnosticDimensionSide diagnosticSide = DiagnosticDimensionSide.All)
	{
		_config = config;
		_dimStyleId = dimStyleId;
		_diameterCalloutDimStyleId = diameterCalloutDimStyleId;
		_dimScale = ((dimScale <= 0.0) ? 1.0 : dimScale);
		_diagnosticsEnabled = diagnosticsEnabled;
		_diagnosticSide = diagnosticSide;
		_transaction = tr;
		_space = space;
		_dimensionDeduplicationRules = new DimensionDeduplicationRules(config);
		_dimensionLayoutRules = new DimensionLayoutRules(config);
		_entityWriter = new CadEntityWriter(db, tr, space, config, dimStyleId, _dimScale, annotationLayer, groupId);
		_cornerCalloutRenderer = new CornerCalloutRenderer(db, _entityWriter, config, diameterCalloutDimStyleId, annotationLayer);
		_debugAnnotationRenderer = new DebugAnnotationRenderer(db, _entityWriter, _entityWriter.GetDimStyleTextStyle(dimStyleId), annotationLayer);
		_extensionLineRenderer = new DimensionExtensionLineRenderer(db, _entityWriter, annotationLayer);
	}

	public void DrawDimensionPlan(DimensionPlan plan)
	{
		if (plan == null)
		{
			return;
		}
		foreach (DimensionPlanCadItem item in _dimensionPlanMapper.Map(plan))
		{
			AddPlannedDimension(item);
		}
	}

	private void AddPlannedDimension(DimensionPlanCadItem dimension)
	{
		if (dimension != null)
		{
			DeferredDim dim = new DeferredDim
			{
				Rotation = dimension.Rotation,
				XLine1 = dimension.FirstPoint,
				XLine2 = dimension.SecondPoint,
				OverrideText = (dimension.OverrideText ?? string.Empty),
				Span = dimension.Span,
				DimType = dimension.DimensionType,
				ForceOuterLevel = dimension.ForceOuterLevel,
				UseSegmentedExtensionLines = dimension.UseSegmentedExtensionLines,
				LooseChainId = dimension.ChainId,
				AlignmentKey = dimension.AlignmentKey,
				AlignmentPriority = dimension.AlignmentPriority,
				PreferLocalBoundary = dimension.PreferLocalBoundary,
				PreferFeatureLocalPlacement = dimension.PreferFeatureLocalPlacement,
				PreservePreferredSide = dimension.PreservePreferredSide,
				DebugOwner = dimension.DebugOwner,
				DebugRole = dimension.DebugRole
			};
			AddPlannedDimensionToSide(dim, dimension.Side);
		}
	}

	private void AddPlannedDimensionToSide(DeferredDim dim, DimensionSide side)
	{
		switch (side)
		{
		case DimensionSide.Top:
			_topDims.Add(dim);
			break;
		case DimensionSide.Left:
			if (dim.Rotation == Math.PI / 2.0)
			{
				DimSide dimSide2 = ChooseVerticalNormalDimensionSide(dim, DimSide.Left);
				if (dimSide2 == DimSide.Right)
				{
					_rightDims.Add(dim);
					break;
				}
			}
			_leftDims.Add(dim);
			break;
		case DimensionSide.Right:
			if (dim.Rotation == Math.PI / 2.0)
			{
				DimSide dimSide = ChooseVerticalNormalDimensionSide(dim, DimSide.Right);
				if (dimSide == DimSide.Left)
				{
					_leftDims.Add(dim);
					break;
				}
			}
			_rightDims.Add(dim);
			break;
		default:
			_bottomDims.Add(dim);
			break;
		}
	}

	private DimSide ChooseVerticalNormalDimensionSide(DeferredDim dim, DimSide preferredSide)
	{
		if (dim.DimType != DimensionType.HoleLocation)
		{
			return preferredSide;
		}
		if (preferredSide != DimSide.Left && preferredSide != DimSide.Right)
		{
			return preferredSide;
		}
		DimSide dimSide = ((preferredSide == DimSide.Left) ? DimSide.Right : DimSide.Left);
		List<DeferredDim> source = ((preferredSide == DimSide.Left) ? _leftDims : _rightDims);
		List<DeferredDim> source2 = ((dimSide == DimSide.Left) ? _leftDims : _rightDims);
		return ToAdapterDimSide(_dimensionLayoutRules.ChooseVerticalHoleLocationSide(ToLayoutItem(dim), ToCoreDimensionSide(preferredSide), source.Select(ToLayoutItem), source2.Select(ToLayoutItem), _dimScale));
	}

	private void RebalanceVerticalHoleLocationSides()
	{
		RebalanceVerticalHoleLocationSide(_rightDims, _leftDims);
		RebalanceVerticalHoleLocationSide(_leftDims, _rightDims);
	}

	private void RebalanceVerticalHoleLocationSide(List<DeferredDim> sourceDims, List<DeferredDim> targetDims)
	{
		List<VerticalRebalanceMove> list = _dimensionLayoutRules.SelectVerticalHoleLocationRebalanceMoves(sourceDims.Select(ToLayoutItem).ToList(), targetDims.Select(ToLayoutItem).ToList(), _dimScale);
		foreach (VerticalRebalanceMove item in list)
		{
			targetDims.Add(sourceDims[item.SourceIndex]);
			sourceDims.RemoveAt(item.SourceIndex);
		}
	}

	public void DrawCornerFeatureLeadersWithJig(Editor editor, OutlineFeature outline)
	{
		ApplyChamferTextPrecision(outline);
		double dimStyleTextHeight = GetDimStyleTextHeight(_diameterCalloutDimStyleId);
		foreach (IGrouping<string, ChamferFeature> item in from c in outline.Chamfers
			group c by c.Text into g
			orderby g.Key
			select g)
		{
			List<ChamferFeature> list = item.ToList();
			if (list.Count != 0)
			{
				ChamferFeature chamferFeature = PickChamferLeaderTarget(list, outline);
				Point2d target = Midpoint(chamferFeature.StartPoint, chamferFeature.EndPoint);
				string text = FormatRepeatedFeatureText(item.Key, list.Count);
				Point3d textPoint;
				switch (_cornerCalloutRenderer.PromptLeaderPoint(editor, target, text, dimStyleTextHeight, out textPoint))
				{
				case CornerCalloutJigResult.Skip:
					editor.WriteMessage("\nSkipped corner callout {0}.", text);
					break;
				case CornerCalloutJigResult.Cancel:
					editor.WriteMessage("\nCorner callout placement canceled.");
					return;
				default:
					AddManualCornerFeatureLeader(target, textPoint, text);
					break;
				}
			}
		}
		foreach (IGrouping<string, FilletFeature> item2 in from f in outline.Fillets
			group f by f.Text into g
			orderby g.Key
			select g)
		{
			List<FilletFeature> list2 = item2.ToList();
			if (list2.Count != 0)
			{
				FilletFeature feature = PickFilletLeaderTarget(list2, outline);
				Point2d arcLeaderTarget = GetArcLeaderTarget(feature);
				string text2 = FormatRepeatedFeatureText(item2.Key, list2.Count);
				Point3d textPoint2;
				switch (_cornerCalloutRenderer.PromptLeaderPoint(editor, arcLeaderTarget, text2, dimStyleTextHeight, out textPoint2))
				{
				case CornerCalloutJigResult.Skip:
					editor.WriteMessage("\nSkipped corner callout {0}.", text2);
					break;
				case CornerCalloutJigResult.Cancel:
					editor.WriteMessage("\nCorner callout placement canceled.");
					return;
				default:
					AddManualFilletRadiusDimension(feature, textPoint2, text2);
					break;
				}
			}
		}
	}

	private void ApplyChamferTextPrecision(OutlineFeature outline)
	{
		if (outline == null || outline.Chamfers.Count == 0)
		{
			return;
		}
		int dimStyleLinearPrecision = GetDimStyleLinearPrecision(_diameterCalloutDimStyleId);
		foreach (ChamferFeature chamfer in outline.Chamfers)
		{
			double value = ((chamfer.Value > _config.GeometryTolerance) ? chamfer.Value : Math.Max(chamfer.DeltaX, chamfer.DeltaY));
			chamfer.Text = "C" + FormatNumberWithPrecision(value, dimStyleLinearPrecision);
		}
	}

	public void DrawSlotRadiusLeadersWithJig(Editor editor, IEnumerable<SlotFeature> slots)
	{
		if (editor == null || slots == null)
		{
			return;
		}
		double dimStyleTextHeight = GetDimStyleTextHeight(_diameterCalloutDimStyleId);
		foreach (List<SlotFeature> item in GroupSlotsByRadius(slots))
		{
			if (item.Count != 0)
			{
				SlotFeature slotFeature = item[0];
				Point2d leaderArcCenter = slotFeature.LeaderArcCenter;
				string text = FormatSlotRadiusCallout(item);
				Point3d textPoint;
				switch (_cornerCalloutRenderer.PromptLeaderPoint(editor, leaderArcCenter, text, dimStyleTextHeight, out textPoint))
				{
				case CornerCalloutJigResult.Skip:
					editor.WriteMessage("\nSkipped U slot radius callout {0}.", text);
					break;
				case CornerCalloutJigResult.Cancel:
					editor.WriteMessage("\nU slot radius callout placement canceled.");
					return;
				default:
					AddManualSlotRadiusDimension(slotFeature, textPoint, text);
					break;
				}
			}
		}
	}

	private void AddManualCornerFeatureLeader(Point2d target, Point3d textPoint, string text)
	{
		Point3d arrowPoint = new Point3d(target.X, target.Y, 0.0);
		AddCornerLeader(arrowPoint, textPoint, textPoint, text);
	}

	private void AddManualFilletRadiusDimension(FilletFeature feature, Point3d textPoint, string text)
	{
		Point2d arcLeaderTarget = GetArcLeaderTarget(feature);
		Point3d centerPoint = new Point3d(feature.Center.X, feature.Center.Y, 0.0);
		Point3d chordPoint = new Point3d(arcLeaderTarget.X, arcLeaderTarget.Y, 0.0);
		_cornerCalloutRenderer.AddRadialDimension(centerPoint, chordPoint, textPoint, Scale(_config.LeaderOffset), text, useCustomTextPosition: true);
	}

	private void AddManualSlotRadiusDimension(SlotFeature slot, Point3d textPoint, string text)
	{
		Point2d leaderArcCenter = slot.LeaderArcCenter;
		Point3d arrowPoint = new Point3d(leaderArcCenter.X, leaderArcCenter.Y, 0.0);
		AddCornerLeader(arrowPoint, textPoint, textPoint, text);
	}

	private static string FormatRepeatedFeatureText(string text, int count)
	{
		return (count > 1) ? (count.ToString(CultureInfo.InvariantCulture) + "-" + text) : text;
	}

	private void AddCornerLeader(Point3d arrowPoint, Point3d landingPoint, Point3d textPoint, string text)
	{
		_cornerCalloutRenderer.AddLeader(arrowPoint, landingPoint, textPoint, text);
	}

	private List<List<SlotFeature>> GroupSlotsByRadius(IEnumerable<SlotFeature> slots)
	{
		List<List<SlotFeature>> list = new List<List<SlotFeature>>();
		if (slots == null)
		{
			return list;
		}
		foreach (SlotFeature slot in slots.Where((SlotFeature s) => s != null && s.Radius > _config.GeometryTolerance))
		{
			double tolerance = Math.Max(_config.GeometryTolerance, Math.Max(slot.Radius, 1.0) * 0.02);
			List<SlotFeature> list2 = list.FirstOrDefault((List<SlotFeature> g) => g[0].IsSingleArcSlot == slot.IsSingleArcSlot && Math.Abs(g[0].Radius - slot.Radius) <= tolerance);
			if (list2 == null)
			{
				list2 = new List<SlotFeature>();
				list.Add(list2);
			}
			list2.Add(slot);
		}
		return list.OrderBy((List<SlotFeature> g) => g[0].Radius).ToList();
	}

	private string FormatSlotRadiusCallout(IList<SlotFeature> slots)
	{
		SlotFeature slotFeature = slots[0];
		string text = (slotFeature.IsSingleArcSlot ? "R" : "2-R") + _config.FormatNumber(slotFeature.Radius);
		return (slots.Count > 1) ? (slots.Count.ToString(CultureInfo.InvariantCulture) + "x" + text) : text;
	}

	private ChamferFeature PickChamferLeaderTarget(IList<ChamferFeature> features, OutlineFeature outline)
	{
		double centerX = (outline.MinX + outline.MaxX) / 2.0;
		double centerY = (outline.MinY + outline.MaxY) / 2.0;
		return features.OrderByDescending((ChamferFeature f) => DistanceSquared(Midpoint(f.StartPoint, f.EndPoint), new Point2d(centerX, centerY))).First();
	}

	private FilletFeature PickFilletLeaderTarget(IList<FilletFeature> features, OutlineFeature outline)
	{
		double centerX = (outline.MinX + outline.MaxX) / 2.0;
		double centerY = (outline.MinY + outline.MaxY) / 2.0;
		return features.OrderByDescending((FilletFeature f) => DistanceSquared(GetArcLeaderTarget(f), new Point2d(centerX, centerY))).First();
	}

	private static Point2d GetArcLeaderTarget(FilletFeature feature)
	{
		Vector2d vector2d = feature.StartPoint - feature.Center;
		Vector2d vector2d2 = feature.EndPoint - feature.Center;
		Vector2d vector2d3 = vector2d + vector2d2;
		if (vector2d3.Length <= 1E-09)
		{
			vector2d3 = vector2d;
		}
		Vector2d normal = vector2d3.GetNormal();
		return feature.Center + normal * feature.Radius;
	}

	private static double DistanceSquared(Point2d a, Point2d b)
	{
		double num = a.X - b.X;
		double num2 = a.Y - b.Y;
		return num * num + num2 * num2;
	}

	private static DimensionSide ToCoreDimensionSide(DimSide side)
	{
		return side switch
		{
			DimSide.Top => DimensionSide.Top,
			DimSide.Left => DimensionSide.Left,
			DimSide.Right => DimensionSide.Right,
			_ => DimensionSide.Bottom,
		};
	}

	private static DimSide ToAdapterDimSide(DimensionSide side)
	{
		return side switch
		{
			DimensionSide.Top => DimSide.Top,
			DimensionSide.Left => DimSide.Left,
			DimensionSide.Right => DimSide.Right,
			_ => DimSide.Bottom,
		};
	}

	private static DimensionLayoutItem ToLayoutItem(DeferredDim dim)
	{
		return new DimensionLayoutItem
		{
			FirstPoint = new Point2D(dim.XLine1.X, dim.XLine1.Y),
			SecondPoint = new Point2D(dim.XLine2.X, dim.XLine2.Y),
			OverrideText = dim.OverrideText,
			Span = dim.Span,
			Kind = ToDimensionKind(dim.DimType),
			LooseChainId = dim.LooseChainId,
			AlignmentKey = dim.AlignmentKey,
			AlignmentPriority = dim.AlignmentPriority,
			PreferLocalBoundary = dim.PreferLocalBoundary,
			ForceOuterLevel = dim.ForceOuterLevel,
			PreferFeatureLocalPlacement = dim.PreferFeatureLocalPlacement,
			PreservePreferredSide = dim.PreservePreferredSide
		};
	}

	private static DimensionDeduplicationItem ToDeduplicationItem(DeferredDim dim)
	{
		return new DimensionDeduplicationItem
		{
			FirstPoint = new Point2D(dim.XLine1.X, dim.XLine1.Y),
			SecondPoint = new Point2D(dim.XLine2.X, dim.XLine2.Y),
			OverrideText = dim.OverrideText,
			Span = dim.Span,
			Kind = ToDimensionKind(dim.DimType),
			ForceOuterLevel = dim.ForceOuterLevel,
			DebugRole = dim.DebugRole
		};
	}

	private static DimensionTextPlacementItem ToTextPlacementItem(PlacedDim placed)
	{
		return new DimensionTextPlacementItem
		{
			Dimension = ToLayoutItem(placed.Dim),
			Side = ToCoreDimensionSide(placed.Side),
			DimLinePoint = new Point2D(placed.DimLinePoint.X, placed.DimLinePoint.Y),
			TextBounds = ToCoreTextBounds(placed.TextBounds)
		};
	}

	private static TextBounds2D ToCoreTextBounds(TextBounds bounds)
	{
		return new TextBounds2D
		{
			MinX = bounds.MinX,
			MaxX = bounds.MaxX,
			MinY = bounds.MinY,
			MaxY = bounds.MaxY
		};
	}

	private static TextBounds ToTextBounds(TextBounds2D bounds)
	{
		return new TextBounds
		{
			MinX = bounds.MinX,
			MaxX = bounds.MaxX,
			MinY = bounds.MinY,
			MaxY = bounds.MaxY
		};
	}

	private static OutlineFeature2D ToCoreOutlineOrNull(OutlineFeature outline)
	{
		return (outline == null) ? null : CadToCoreModelMapper.ToCoreOutline(outline);
	}

	private static DimensionKind ToDimensionKind(DimensionType type)
	{
		return type switch
		{
			DimensionType.PinDistance => DimensionKind.PinDistance,
			DimensionType.PinGroupDistance => DimensionKind.PinGroupDistance,
			DimensionType.DatumHoleLocationX => DimensionKind.DatumHoleLocationX,
			DimensionType.DatumHoleLocationY => DimensionKind.DatumHoleLocationY,
			DimensionType.OverallWidth => DimensionKind.OverallWidth,
			DimensionType.OverallHeight => DimensionKind.OverallHeight,
			DimensionType.HoleLocation => DimensionKind.HoleLocation,
			_ => DimensionKind.Normal,
		};
	}

	private Point3d GetDimLinePoint(DeferredDim dim, DimSide side, OutlineFeature outline, double offset)
	{
		Point2D dimLinePoint = _dimensionLayoutRules.GetDimLinePoint(ToLayoutItem(dim), ToCoreDimensionSide(side), ToCoreOutlineOrNull(outline), offset);
		return new Point3d(dimLinePoint.X, dimLinePoint.Y, 0.0);
	}

	private double GetDimLineCoordinate(DeferredDim dim, DimSide side, OutlineFeature outline, double offset)
	{
		return _dimensionLayoutRules.GetDimLineCoordinate(ToLayoutItem(dim), ToCoreDimensionSide(side), ToCoreOutlineOrNull(outline), offset);
	}

	private (double A, double B) ComputeArrowInterval(DeferredDim dim, bool isHorizontal)
	{
		if (isHorizontal)
		{
			return (A: Math.Min(dim.XLine1.X, dim.XLine2.X), B: Math.Max(dim.XLine1.X, dim.XLine2.X));
		}
		return (A: Math.Min(dim.XLine1.Y, dim.XLine2.Y), B: Math.Max(dim.XLine1.Y, dim.XLine2.Y));
	}

	private static Point2d Midpoint(Point2d a, Point2d b)
	{
		return new Point2d((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
	}

	private static Point3d Midpoint(Point3d a, Point3d b)
	{
		return new Point3d((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);
	}
}
