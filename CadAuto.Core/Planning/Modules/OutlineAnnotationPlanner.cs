using CadAuto.Core.Planning;

namespace CadAuto.Core.Planning.Modules;

public sealed class OutlineAnnotationPlanner : IAnnotationPlanner
{
	public string Id => AnnotationModuleIds.Outline;

	public ModulePlan Plan(AnnotationSession session)
	{
		if (session == null || session.Outline == null)
		{
			return new ModulePlan { ModuleId = Id };
		}
		DimensionPlanner planner = new DimensionPlanner(session.Config);
		DimensionPlan linear = session.Frame == null || session.Frame.IsIdentity
			? planner.CreateOutlinePlan(session.Outline)
			: planner.CreateOutlinePlan(session.Outline, session.Frame);
		return new ModulePlan
		{
			ModuleId = Id,
			LinearPlan = linear
		};
	}
}
