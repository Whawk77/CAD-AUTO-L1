using CadAuto.Core.Planning.Modules;

namespace CadAuto.CadAdapter.Modules;

public interface IAnnotationModule
{
	string Id { get; }

	void Recognize(CadAnnotationSession session);

	ModulePlan Plan(CadAnnotationSession session);

	void Render(CadAnnotationSession session, ModulePlan plan);
}
