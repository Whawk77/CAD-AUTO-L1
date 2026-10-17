namespace CadAuto.Core.Planning.Modules;

public interface IAnnotationPlanner
{
	string Id { get; }

	ModulePlan Plan(AnnotationSession session);
}
