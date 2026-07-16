using System;
using System.Collections.Generic;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Planning;

public sealed class DimensionPlan
{
	private int _nextDiagnosticId;

	private readonly Dictionary<PlannedDimension, DimensionCandidateDiagnostic> _diagnosticByDimension;

	public List<PlannedDimension> Dimensions { get; private set; }

	public List<PinGroupPlan> PinGroups { get; private set; }

	public DimensionDiagnosticReport Diagnostics { get; private set; }

	public DimensionPlan()
	{
		_nextDiagnosticId = 1;
		_diagnosticByDimension = new Dictionary<PlannedDimension, DimensionCandidateDiagnostic>();
		Dimensions = new List<PlannedDimension>();
		PinGroups = new List<PinGroupPlan>();
		Diagnostics = new DimensionDiagnosticReport();
	}

	public void Add(PlannedDimension dimension)
	{
		if (dimension != null)
		{
			Dimensions.Add(dimension);
			AddDiagnosticCandidate(dimension);
		}
	}

	public void MarkSuppressed(PlannedDimension dimension, string reason)
	{
		if (dimension != null)
		{
			if (!_diagnosticByDimension.TryGetValue(dimension, out var value))
			{
				value = AddDiagnosticCandidate(dimension);
			}
			value.IsSuppressed = true;
			value.SuppressedReason = reason ?? string.Empty;
			value.IsSelected = false;
			value.DecisionStatus = "Suppressed";
			value.DecisionReason = reason ?? string.Empty;
		}
	}

	public void AddSkippedDimension(DimensionKind kind, DimensionOrientation orientation, DimensionSide side, Point2D requestedFirstPoint, Point2D secondPoint, string reason, string debugRole, string debugOwner = null)
	{
		string text = reason ?? string.Empty;
		Diagnostics.DimensionCandidates.Add(new DimensionCandidateDiagnostic
		{
			Id = _nextDiagnosticId++,
			Kind = kind.ToString(),
			SourceFeatureId = debugOwner ?? debugRole ?? string.Empty,
			Value = GetDiagnosticValue(orientation, requestedFirstPoint, secondPoint),
			FirstPointX = requestedFirstPoint.X,
			FirstPointY = requestedFirstPoint.Y,
			SecondPointX = secondPoint.X,
			SecondPointY = secondPoint.Y,
			MeasurementMinimum = GetMeasurementMinimum(orientation, requestedFirstPoint, secondPoint),
			MeasurementMaximum = GetMeasurementMaximum(orientation, requestedFirstPoint, secondPoint),
			PlacementSide = side.ToString(),
			Priority = GetDiagnosticPriority(kind),
			IsSuppressed = false,
			IsSelected = false,
			IsAttachmentValid = false,
			DecisionStatus = "Skipped",
			DecisionReason = text,
			SuppressedReason = string.Empty,
			Orientation = orientation.ToString(),
			DebugRole = debugRole ?? string.Empty,
			OverrideText = string.Empty
		});
	}

	public void CaptureFinalDimensions()
	{
		Diagnostics.FinalDimensions.Clear();
		foreach (DimensionCandidateDiagnostic candidate in Diagnostics.DimensionCandidates)
		{
			if (!candidate.IsSuppressed && !string.Equals(candidate.DecisionStatus, "Skipped", StringComparison.Ordinal))
			{
				candidate.IsSelected = false;
				candidate.DecisionStatus = "NotSelected";
				candidate.DecisionReason = "NotPresentInFinalPlan";
			}
		}
		foreach (PlannedDimension dimension in Dimensions)
		{
			if (!_diagnosticByDimension.TryGetValue(dimension, out var value))
			{
				value = AddDiagnosticCandidate(dimension);
			}
			value.IsSelected = true;
			value.DecisionStatus = "Selected";
			value.DecisionReason = IsOverall(dimension.Kind) ? "RequiredOverallDimension" : "RetainedAfterSuppression";
			Diagnostics.FinalDimensions.Add(CloneDiagnostic(value));
		}
	}

	private DimensionCandidateDiagnostic AddDiagnosticCandidate(PlannedDimension dimension)
	{
		DimensionCandidateDiagnostic dimensionCandidateDiagnostic = new DimensionCandidateDiagnostic
		{
			Id = _nextDiagnosticId++,
			Kind = dimension.Kind.ToString(),
			SourceFeatureId = (dimension.SourceKey ?? dimension.DebugOwner ?? dimension.DebugRole ?? string.Empty),
			Value = GetDiagnosticValue(dimension),
			FirstPointX = dimension.FirstPoint.X,
			FirstPointY = dimension.FirstPoint.Y,
			SecondPointX = dimension.SecondPoint.X,
			SecondPointY = dimension.SecondPoint.Y,
			MeasurementMinimum = GetMeasurementMinimum(dimension),
			MeasurementMaximum = GetMeasurementMaximum(dimension),
			PlacementSide = dimension.Side.ToString(),
			Priority = GetDiagnosticPriority(dimension),
			IsSuppressed = false,
			IsSelected = false,
			IsAttachmentValid = true,
			DecisionStatus = "Candidate",
			DecisionReason = "Generated",
			SuppressedReason = string.Empty,
			Orientation = dimension.Orientation.ToString(),
			DebugRole = (dimension.DebugRole ?? string.Empty),
			OverrideText = (dimension.OverrideText ?? string.Empty)
		};
		_diagnosticByDimension[dimension] = dimensionCandidateDiagnostic;
		Diagnostics.DimensionCandidates.Add(dimensionCandidateDiagnostic);
		return dimensionCandidateDiagnostic;
	}

	private static DimensionCandidateDiagnostic CloneDiagnostic(DimensionCandidateDiagnostic source)
	{
		return new DimensionCandidateDiagnostic
		{
			Id = source.Id,
			Kind = source.Kind,
			SourceFeatureId = source.SourceFeatureId,
			Value = source.Value,
			FirstPointX = source.FirstPointX,
			FirstPointY = source.FirstPointY,
			SecondPointX = source.SecondPointX,
			SecondPointY = source.SecondPointY,
			MeasurementMinimum = source.MeasurementMinimum,
			MeasurementMaximum = source.MeasurementMaximum,
			PlacementSide = source.PlacementSide,
			Priority = source.Priority,
			IsSuppressed = source.IsSuppressed,
			IsSelected = source.IsSelected,
			IsAttachmentValid = source.IsAttachmentValid,
			DecisionStatus = source.DecisionStatus,
			DecisionReason = source.DecisionReason,
			SuppressedReason = source.SuppressedReason,
			Orientation = source.Orientation,
			DebugRole = source.DebugRole,
			OverrideText = source.OverrideText
		};
	}

	private static double GetMeasurementMinimum(PlannedDimension dimension)
	{
		return GetMeasurementMinimum(dimension.Orientation, dimension.FirstPoint, dimension.SecondPoint);
	}

	private static double GetMeasurementMaximum(PlannedDimension dimension)
	{
		return GetMeasurementMaximum(dimension.Orientation, dimension.FirstPoint, dimension.SecondPoint);
	}

	private static double GetMeasurementMinimum(DimensionOrientation orientation, Point2D firstPoint, Point2D secondPoint)
	{
		return orientation == DimensionOrientation.Vertical ? Math.Min(firstPoint.Y, secondPoint.Y) : Math.Min(firstPoint.X, secondPoint.X);
	}

	private static double GetMeasurementMaximum(DimensionOrientation orientation, Point2D firstPoint, Point2D secondPoint)
	{
		return orientation == DimensionOrientation.Vertical ? Math.Max(firstPoint.Y, secondPoint.Y) : Math.Max(firstPoint.X, secondPoint.X);
	}

	private static bool IsOverall(DimensionKind kind)
	{
		return kind == DimensionKind.OverallWidth || kind == DimensionKind.OverallHeight;
	}

	private static double GetDiagnosticValue(PlannedDimension dimension)
	{
		return GetDiagnosticValue(dimension.Orientation, dimension.FirstPoint, dimension.SecondPoint);
	}

	private static double GetDiagnosticValue(DimensionOrientation orientation, Point2D firstPoint, Point2D secondPoint)
	{
		if (orientation == DimensionOrientation.Vertical)
		{
			return Math.Abs(secondPoint.Y - firstPoint.Y);
		}
		if (orientation == DimensionOrientation.Horizontal)
		{
			return Math.Abs(secondPoint.X - firstPoint.X);
		}
		return firstPoint.DistanceTo(secondPoint);
	}

	private static int GetDiagnosticPriority(PlannedDimension dimension)
	{
		return GetDiagnosticPriority(dimension.Kind);
	}

	private static int GetDiagnosticPriority(DimensionKind kind)
	{
		switch (kind)
		{
		case DimensionKind.OverallWidth:
		case DimensionKind.OverallHeight:
			return 1000;
		case DimensionKind.DatumHoleLocationX:
		case DimensionKind.DatumHoleLocationY:
			return 900;
		case DimensionKind.PinGroupDistance:
			return 800;
		case DimensionKind.PinDistance:
			return 700;
		case DimensionKind.HoleLocation:
			return 600;
		case DimensionKind.Normal:
			return 500;
		case DimensionKind.HoleDiameter:
			return 400;
		case DimensionKind.Chamfer:
		case DimensionKind.Fillet:
			return 300;
		default:
			return 0;
		}
	}
}
