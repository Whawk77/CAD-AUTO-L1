using System.Collections.Generic;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Planning.Modules;

public sealed class ModulePlan
{
	public string ModuleId { get; set; }

	public DimensionPlan LinearPlan { get; set; }

	public IList<HoleCalloutPlan> HoleCallouts { get; set; }

	public int CornerChamferCount { get; set; }

	public int CornerFilletCount { get; set; }
}
