using System.Collections.Generic;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Planning.Modules;

public sealed class HoleAnnotationPlanner : IAnnotationPlanner
{
	public string Id => AnnotationModuleIds.Hole;

	public ModulePlan Plan(AnnotationSession session)
	{
		if (session == null || session.Outline == null)
		{
			return new ModulePlan { ModuleId = Id };
		}
		DimensionPlanner planner = new DimensionPlanner(session.Config)
		{
			LaneOccupancy = session.Occupancy,
			Centerlines = session.Centerlines
		};
		IEnumerable<HoleFeature2D> holes = session.Holes ?? new HoleFeature2D[0];
		IEnumerable<SlotFeature2D> slots = session.Slots ?? new SlotFeature2D[0];
		DimensionPlan linear = session.Frame == null || session.Frame.IsIdentity
			? planner.CreateHolePlan(session.Outline, session.Datum, holes, slots)
			: planner.CreateHolePlan(session.Outline, session.Datum, holes, slots, session.Frame);
		return new ModulePlan
		{
			ModuleId = Id,
			LinearPlan = linear,
			HoleCallouts = new HoleCalloutPlanner(session.Config).CreatePlans(holes, session.Datum?.DatumHole)
		};
	}
}
