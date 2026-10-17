using CadAuto.Core.Planning.Modules;

namespace CadAuto.CadAdapter.Modules;

public static class AnnotationModuleRunner
{
	public static ModulePlan Run(IAnnotationModule module, CadAnnotationSession session)
	{
		if (module == null)
		{
			return null;
		}
		module.Recognize(session);
		ModulePlan plan = module.Plan(session);
		module.Render(session, plan);
		return plan;
	}
}
