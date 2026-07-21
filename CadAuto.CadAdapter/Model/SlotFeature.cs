using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadAuto.CadAdapter.Model;

public sealed class SlotFeature
{
	public string GroupId { get; set; }

	public Point2d FirstCenter { get; set; }

	public Point2d SecondCenter { get; set; }

	public double Radius { get; set; }

	public double CenterDistance { get; set; }

	public ObjectId FirstArcId { get; set; } = ObjectId.Null;

	public ObjectId SecondArcId { get; set; } = ObjectId.Null;

	public ObjectId FirstLineId { get; set; } = ObjectId.Null;

	public ObjectId SecondLineId { get; set; } = ObjectId.Null;

	public bool IsSingleArcSlot { get; set; }

	public bool IsVertical { get; set; }

	public Point2d ArcLeaderTarget { get; set; }

	public Point2d LeaderArcCenter => FirstCenter;

	public Point2d LeaderArcTarget
	{
		get
		{
			if (IsSingleArcSlot)
			{
				return ArcLeaderTarget;
			}
			Vector2d vector2d = FirstCenter - SecondCenter;
			if (vector2d.Length <= 1E-09)
			{
				vector2d = Vector2d.XAxis;
			}
			vector2d = vector2d.GetNormal();
			return FirstCenter + vector2d * Radius;
		}
	}
}
