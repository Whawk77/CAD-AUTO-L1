namespace CadAuto.Core.Planning;

public sealed class FeatureDiagnosticCounts
{
	public int OutlineCount { get; set; }

	public int HoleCount { get; set; }

	public int PinHoleCount { get; set; }

	public int ThreadHoleCount { get; set; }

	public int SlotCount { get; set; }

	public int ChamferCount { get; set; }

	public int FilletCount { get; set; }
}
