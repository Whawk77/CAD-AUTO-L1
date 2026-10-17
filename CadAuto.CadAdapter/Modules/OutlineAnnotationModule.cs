using CadAuto.CadAdapter.Mapping;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Planning.Modules;

namespace CadAuto.CadAdapter.Modules;

public sealed class OutlineAnnotationModule : IAnnotationModule
{
	private readonly OutlineAnnotationPlanner _planner = new OutlineAnnotationPlanner();

	public string Id => AnnotationModuleIds.Outline;

	public void Recognize(CadAnnotationSession session)
	{
		if (session == null || session.Outline == null)
		{
			return;
		}
		if (session.Core != null && session.Core.Outline != null)
		{
			return;
		}
		OutlineFeature2D outline = CadToCoreModelMapper.ToCoreOutline(session.Outline);
		session.Core = AnnotationSession.FromOutline(session.Config, outline, session.Core?.Datum);
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
