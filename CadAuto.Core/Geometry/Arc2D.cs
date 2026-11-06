namespace CadAuto.Core.Geometry;

public sealed class Arc2D
{
	public Point2D Start { get; set; }

	public Point2D End { get; set; }

	public Point2D Center { get; set; }

	public double Radius { get; set; }

	public double Bulge { get; set; }

	public string SourceKey { get; set; }

	// 由多段同心、同半径、同转向的圆弧碎段合并而成时，记录全部原图元来源；未合并时为 null。
	public System.Collections.Generic.List<string> SourceKeys { get; set; }
}
