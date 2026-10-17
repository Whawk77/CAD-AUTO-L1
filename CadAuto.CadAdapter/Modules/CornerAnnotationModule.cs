using CadAuto.CadAdapter.Model;
using CadAuto.Core.Planning.Modules;

namespace CadAuto.CadAdapter.Modules;

public sealed class CornerAnnotationModule : IAnnotationModule
{
	public string Id => AnnotationModuleIds.Corner;

	public void Recognize(CadAnnotationSession session)
	{
	}

	public ModulePlan Plan(CadAnnotationSession session)
	{
		OutlineFeature outline = session?.Outline;
		return new ModulePlan
		{
			ModuleId = Id,
			CornerChamferCount = outline?.Chamfers?.Count ?? 0,
			CornerFilletCount = outline?.Fillets?.Count ?? 0
		};
	}

	public void Render(CadAnnotationSession session, ModulePlan plan)
	{
		if (session?.Drawer == null || session.Editor == null || session.Outline == null)
		{
			return;
		}
		if ((plan?.CornerChamferCount ?? 0) == 0 && (plan?.CornerFilletCount ?? 0) == 0)
		{
			return;
		}
		session.Drawer.DrawCornerFeatureLeadersWithJig(session.Editor, session.Outline);
	}
}
