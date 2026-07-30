using System;
using System.Collections.Generic;

namespace CadAuto.Core.Planning;

public enum DimensionCandidateRole
{
	Unknown,
	Overall,
	Structure,
	OutlineSegment,
	Hole,
	Slot,
	Datum,
	Pin
}

public enum DimensionCandidateOwnerKind
{
	Unknown,
	Outline,
	HoleGroup,
	PinGroup,
	Slot,
	Datum
}

public enum DimensionCandidateDecision
{
	Candidate,
	Selected,
	Suppressed,
	Skipped,
	NotSelected
}

internal static class DimensionCandidateSemantics
{
	internal static void ApplyLegacyMappings(PlannedDimension dimension)
	{
		if (dimension == null)
		{
			return;
		}
		if (dimension.Role == DimensionCandidateRole.Unknown)
		{
			dimension.Role = GetRole(dimension.Kind, dimension.DebugRole);
		}
		if (dimension.OwnerKind == DimensionCandidateOwnerKind.Unknown)
		{
			dimension.OwnerKind = GetOwnerKind(dimension.Role, dimension.DebugOwner);
		}
		dimension.SourceGeometryIds = MergeSourceGeometryIds(
			dimension.SourceGeometryIds,
			string.IsNullOrEmpty(dimension.SourceKey) ? null : new[] { dimension.SourceKey });
		dimension.TopologyEvidence = dimension.TopologyEvidence ?? string.Empty;
		dimension.RuleId = dimension.RuleId ?? string.Empty;
	}

	internal static DimensionCandidateRole GetRole(DimensionKind kind, string debugRole)
	{
		switch (kind)
		{
		case DimensionKind.OverallWidth:
		case DimensionKind.OverallHeight:
			return DimensionCandidateRole.Overall;
		case DimensionKind.DatumHoleLocationX:
		case DimensionKind.DatumHoleLocationY:
			return DimensionCandidateRole.Datum;
		case DimensionKind.PinGroupDistance:
		case DimensionKind.PinDistance:
			return DimensionCandidateRole.Pin;
		case DimensionKind.HoleLocation:
		case DimensionKind.HoleDiameter:
			return DimensionCandidateRole.Hole;
		}

		switch (debugRole ?? string.Empty)
		{
		case "OverallWidth":
		case "OverallHeight":
			return DimensionCandidateRole.Overall;
		case "BottomStructWidth":
		case "TopStructWidth":
		case "LeftStructHeight":
		case "RightStructHeight":
			return DimensionCandidateRole.Structure;
		case "OutlineSegment":
			return DimensionCandidateRole.OutlineSegment;
		case "HoleDatumX":
		case "HoleDatumY":
		case "HoleChainH":
		case "HoleChainV":
		case "ThreadHoleChainH":
		case "ThreadHoleChainV":
		case "FunctionalHole":
		case "LooseHole":
			return DimensionCandidateRole.Hole;
		case "SlotCenter":
		case "SlotChainH":
		case "SlotChainV":
		case "SlotDatumH":
		case "SlotDatumV":
		case "SlotPinRef":
		case "SingleArcSlotDatum":
			return DimensionCandidateRole.Slot;
		case "DatumX":
		case "DatumY":
			return DimensionCandidateRole.Datum;
		case "PinDistance":
		case "PinGroupDistance":
			return DimensionCandidateRole.Pin;
		default:
			return DimensionCandidateRole.Unknown;
		}
	}

	internal static DimensionCandidateOwnerKind GetOwnerKind(DimensionCandidateRole role, string debugOwner)
	{
		if (!string.IsNullOrEmpty(debugOwner)
			&& debugOwner.StartsWith("PG", StringComparison.Ordinal))
		{
			return DimensionCandidateOwnerKind.PinGroup;
		}

		switch (role)
		{
		case DimensionCandidateRole.Overall:
		case DimensionCandidateRole.Structure:
		case DimensionCandidateRole.OutlineSegment:
			return DimensionCandidateOwnerKind.Outline;
		case DimensionCandidateRole.Hole:
			return DimensionCandidateOwnerKind.HoleGroup;
		case DimensionCandidateRole.Pin:
			return DimensionCandidateOwnerKind.PinGroup;
		case DimensionCandidateRole.Slot:
			return DimensionCandidateOwnerKind.Slot;
		case DimensionCandidateRole.Datum:
			return DimensionCandidateOwnerKind.Datum;
		default:
			return DimensionCandidateOwnerKind.Unknown;
		}
	}

	internal static List<string> MergeSourceGeometryIds(List<string> existing, IEnumerable<string> additions)
	{
		var result = new List<string>();
		AddDistinctSourceGeometryIds(result, existing);
		AddDistinctSourceGeometryIds(result, additions);
		return result;
	}

	private static void AddDistinctSourceGeometryIds(List<string> target, IEnumerable<string> sourceGeometryIds)
	{
		if (sourceGeometryIds == null)
		{
			return;
		}

		foreach (string sourceGeometryId in sourceGeometryIds)
		{
			if (!string.IsNullOrEmpty(sourceGeometryId)
				&& !target.Contains(sourceGeometryId))
			{
				target.Add(sourceGeometryId);
			}
		}
	}
}
