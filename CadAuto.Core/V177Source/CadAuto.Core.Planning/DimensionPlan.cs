using System;
using System.Collections.Generic;

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
		}
	}

	public void CaptureFinalDimensions()
	{
		Diagnostics.FinalDimensions.Clear();
		foreach (PlannedDimension dimension in Dimensions)
		{
			if (!_diagnosticByDimension.TryGetValue(dimension, out var value))
			{
				value = AddDiagnosticCandidate(dimension);
			}
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
			PlacementSide = dimension.Side.ToString(),
			Priority = GetDiagnosticPriority(dimension),
			IsSuppressed = false,
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
			PlacementSide = source.PlacementSide,
			Priority = source.Priority,
			IsSuppressed = source.IsSuppressed,
			SuppressedReason = source.SuppressedReason,
			Orientation = source.Orientation,
			DebugRole = source.DebugRole,
			OverrideText = source.OverrideText
		};
	}

	private static double GetDiagnosticValue(PlannedDimension dimension)
	{
		if (dimension.Orientation == DimensionOrientation.Vertical)
		{
			return Math.Abs(dimension.SecondPoint.Y - dimension.FirstPoint.Y);
		}
		if (dimension.Orientation == DimensionOrientation.Horizontal)
		{
			return Math.Abs(dimension.SecondPoint.X - dimension.FirstPoint.X);
		}
		return dimension.FirstPoint.DistanceTo(dimension.SecondPoint);
	}

	private static int GetDiagnosticPriority(PlannedDimension dimension)
	{
		switch (dimension.Kind)
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
