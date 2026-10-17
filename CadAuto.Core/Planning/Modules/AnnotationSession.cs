using System.Collections.Generic;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning.Modules;

public sealed class AnnotationSession
{
	public DimensionRuleConfig Config { get; set; }

	public OutlineFeature2D Outline { get; set; }

	public IOutlineFacts OutlineFacts { get; set; }

	public Datum2D Datum { get; set; }

	public IList<HoleFeature2D> Holes { get; set; }

	public IList<SlotFeature2D> Slots { get; set; }

	public IList<Segment2D> Centerlines { get; set; }

	public CoordinateFrame2D Frame { get; set; }

	public LaneOccupancy Occupancy { get; set; }

	public string CommandName { get; set; }

	public static AnnotationSession FromOutline(DimensionRuleConfig config, OutlineFeature2D outline, Datum2D datum)
	{
		DimensionRuleConfig resolved = config ?? DimensionRuleConfig.CreateDefault();
		Datum2D resolvedDatum = datum ?? (outline == null ? null : Datum2D.FromOutline(outline));
		IOutlineFacts facts = CadAuto.Core.Planning.Modules.OutlineFacts.From(outline, resolved.GeometryTolerance);
		return new AnnotationSession
		{
			Config = resolved,
			Outline = outline,
			OutlineFacts = facts,
			Datum = resolvedDatum,
			Holes = new List<HoleFeature2D>(),
			Slots = new List<SlotFeature2D>(),
			Centerlines = new List<Segment2D>(),
			Frame = null,
			Occupancy = null,
			CommandName = string.Empty
		};
	}
}
