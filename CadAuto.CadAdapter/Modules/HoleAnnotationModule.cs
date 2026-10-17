using System.Collections.Generic;
using CadAuto.CadAdapter.Mapping;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning.Modules;

namespace CadAuto.CadAdapter.Modules;

public sealed class HoleAnnotationModule : IAnnotationModule
{
	private readonly HoleAnnotationPlanner _planner = new HoleAnnotationPlanner();

	public string Id => AnnotationModuleIds.Hole;

	public void Recognize(CadAnnotationSession session)
	{
		if (session == null)
		{
			return;
		}
		if (session.Core == null && session.Outline != null)
		{
			OutlineFeature2D outline = CadToCoreModelMapper.ToCoreOutline(session.Outline);
			Datum2D datum = session.Datum == null ? null : CadToCoreModelMapper.ToCoreDatum(session.Datum);
			session.Core = AnnotationSession.FromOutline(session.Config, outline, datum);
		}
		if (session.Core == null)
		{
			return;
		}
		if (session.Datum != null)
		{
			session.Core.Datum = CadToCoreModelMapper.ToCoreDatum(session.Datum);
		}
		session.Core.Holes = session.Holes == null
			? new List<HoleFeature2D>()
			: CadToCoreModelMapper.ToCoreHoles(session.Holes);
		session.Core.Slots = session.Slots == null
			? new List<SlotFeature2D>()
			: CadToCoreModelMapper.ToCoreSlots(session.Slots);
		session.Core.Centerlines = session.Centerlines ?? new List<Segment2D>();
	}

	public ModulePlan Plan(CadAnnotationSession session)
	{
		Recognize(session);
		if (session?.Core == null)
		{
			return new ModulePlan { ModuleId = Id };
		}
		return _planner.Plan(session.Core);
	}

	public void Render(CadAnnotationSession session, ModulePlan plan)
	{
		if (session?.Drawer == null || plan?.LinearPlan == null)
		{
			return;
		}
		session.Drawer.DrawDimensionPlan(plan.LinearPlan, plan.LinearPlan.Diagnostics);
		session.Drawer.FlushStackedDimensions(session.Outline);
		plan.LinearPlan.SynchronizeFinalPlacementSides();
		session.Core?.Occupancy?.RecordAll(plan.LinearPlan.Dimensions);
	}
}
