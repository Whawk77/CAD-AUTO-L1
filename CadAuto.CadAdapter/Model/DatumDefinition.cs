namespace CadAuto.CadAdapter.Model;

public sealed class DatumDefinition
{
	public double BaseX { get; set; }

	public double BaseY { get; set; }

	public HoleFeature DatumHole { get; set; }

	public double? DatumHoleLocationBaseX { get; set; }

	public double? DatumHoleLocationBaseY { get; set; }

	public bool DatumHoleLocationUseToleranceX { get; set; }

	public bool DatumHoleLocationUseToleranceY { get; set; }

	public static DatumDefinition FromOutline(OutlineFeature outline)
	{
		return new DatumDefinition
		{
			BaseX = outline.MinX,
			BaseY = outline.MinY
		};
	}
}
