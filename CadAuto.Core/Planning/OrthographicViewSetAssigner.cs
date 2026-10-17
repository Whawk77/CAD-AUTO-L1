using System;
using System.Collections.Generic;
using System.Linq;

namespace CadAuto.Core.Planning;

public enum OrthographicViewRole
{
	Unassigned,
	Front,
	Top,
	Left
}

public enum PartExtentKind
{
	Length,
	Height,
	Width
}

public sealed class OrthographicViewFrame
{
	public string Id { get; set; }

	public double MinX { get; set; }

	public double MinY { get; set; }

	public double MaxX { get; set; }

	public double MaxY { get; set; }

	public int PinHoleCount { get; set; }

	public OrthographicViewRole Role { get; set; }

	public double Width => MaxX - MinX;

	public double Height => MaxY - MinY;

	public double CenterX => (MinX + MaxX) / 2.0;

	public double CenterY => (MinY + MaxY) / 2.0;
}

public sealed class ViewExtentOwnership
{
	public string ViewId { get; set; }

	public OrthographicViewRole Role { get; set; }

	public bool KeepHorizontalOverall { get; set; }

	public bool KeepVerticalOverall { get; set; }

	public bool KeepPinDimensions { get; set; }

	public PartExtentKind HorizontalExtent { get; set; }

	public PartExtentKind VerticalExtent { get; set; }
}

public sealed class OrthographicViewSetAssignment
{
	public List<ViewExtentOwnership> Views { get; } = new List<ViewExtentOwnership>();

	public string LengthOwnerViewId { get; set; }

	public string HeightOwnerViewId { get; set; }

	public string WidthOwnerViewId { get; set; }

	public string PinOwnerViewId { get; set; }

	public ViewExtentOwnership Find(string viewId)
	{
		return Views.FirstOrDefault((ViewExtentOwnership view) => string.Equals(view.ViewId, viewId, StringComparison.Ordinal));
	}
}

/// <summary>
/// Trial assignment for an already drawn two- or three-view part.
/// First-angle placement is used only as a tie break: the front view sits above the top view
/// and to the left of the left view. Each part extent and the pin chain is owned by one view.
/// </summary>
public static class OrthographicViewSetAssigner
{
	public const string RuleId = "ThreeViewExtentOwnership";

	public static OrthographicViewSetAssignment Assign(IReadOnlyList<OrthographicViewFrame> views, double tolerance)
	{
		if (views == null)
		{
			throw new ArgumentNullException("views");
		}
		List<OrthographicViewFrame> list = views.Where((OrthographicViewFrame view) => view != null).ToList();
		if (list.Count < 2 || list.Count > 3)
		{
			throw new InvalidOperationException("三视图试做需要 2 或 3 个视图外轮廓。");
		}
		if (list.Select((OrthographicViewFrame view) => view.Id).Distinct(StringComparer.Ordinal).Count() != list.Count
			|| list.Any((OrthographicViewFrame view) => string.IsNullOrEmpty(view.Id)))
		{
			throw new InvalidOperationException("每个视图需要互不相同的标识。");
		}
		double tol = Math.Max(Math.Abs(tolerance), 1E-6);
		if (list.Any((OrthographicViewFrame view) => view.Width <= tol || view.Height <= tol))
		{
			throw new InvalidOperationException("视图外轮廓的长或高过小，无法对应零件尺寸。");
		}
		bool anyRole = list.Any((OrthographicViewFrame view) => view.Role != OrthographicViewRole.Unassigned);
		bool allRoles = list.All((OrthographicViewFrame view) => view.Role != OrthographicViewRole.Unassigned);
		if (anyRole && !allRoles)
		{
			throw new InvalidOperationException("视图角色必须全部指定，或全部留空后按投影尺寸推断。");
		}
		if (!allRoles)
		{
			if (list.Count == 3)
			{
				InferThree(list, tol);
			}
			else
			{
				InferTwo(list, tol);
			}
		}
		else if (list.Select((OrthographicViewFrame view) => view.Role).Distinct().Count() != list.Count)
		{
			throw new InvalidOperationException("同一零件里每个视图角色只能出现一次。");
		}
		return BuildOwnership(list);
	}

	public static int Apply(DimensionPlan plan, ViewExtentOwnership ownership)
	{
		if (plan == null || ownership == null)
		{
			return 0;
		}
		int removed = 0;
		foreach (PlannedDimension dimension in plan.Dimensions.ToList())
		{
			string reason = SuppressionReason(dimension, ownership);
			if (reason == null)
			{
				continue;
			}
			plan.MarkSuppressed(dimension, reason, RuleId, null, ownership.ViewId);
			plan.Dimensions.Remove(dimension);
			plan.Diagnostics.FinalDimensions.RemoveAll((DimensionCandidateDiagnostic diagnostic) => diagnostic.Id == dimension.DiagnosticId);
			removed++;
		}
		return removed;
	}

	public static string RoleName(OrthographicViewRole role)
	{
		switch (role)
		{
		case OrthographicViewRole.Front:
			return "主视图";
		case OrthographicViewRole.Top:
			return "俯视图";
		case OrthographicViewRole.Left:
			return "左视图";
		default:
			return "未定视图";
		}
	}

	public static string ExtentName(PartExtentKind kind)
	{
		switch (kind)
		{
		case PartExtentKind.Length:
			return "长";
		case PartExtentKind.Height:
			return "高";
		default:
			return "宽";
		}
	}

	private static string SuppressionReason(PlannedDimension dimension, ViewExtentOwnership ownership)
	{
		if (dimension == null)
		{
			return null;
		}
		if (dimension.Kind == DimensionKind.OverallWidth && !ownership.KeepHorizontalOverall)
		{
			return "CrossViewOverall:" + ownership.HorizontalExtent;
		}
		if (dimension.Kind == DimensionKind.OverallHeight && !ownership.KeepVerticalOverall)
		{
			return "CrossViewOverall:" + ownership.VerticalExtent;
		}
		if (!ownership.KeepPinDimensions && IsPinChain(dimension))
		{
			return "CrossViewPinChain";
		}
		return null;
	}

	private static bool IsPinChain(PlannedDimension dimension)
	{
		return dimension.Kind == DimensionKind.PinDistance
			|| dimension.Kind == DimensionKind.PinGroupDistance
			|| dimension.Kind == DimensionKind.DatumHoleLocationX
			|| dimension.Kind == DimensionKind.DatumHoleLocationY
			|| dimension.Role == DimensionCandidateRole.Pin;
	}

	private static void InferThree(List<OrthographicViewFrame> views, double tolerance)
	{
		var candidates = new List<Tuple<OrthographicViewFrame, OrthographicViewFrame, OrthographicViewFrame>>();
		foreach (OrthographicViewFrame front in views)
		{
			foreach (OrthographicViewFrame top in views)
			{
				if (ReferenceEquals(front, top) || !Near(front.Width, top.Width, tolerance))
				{
					continue;
				}
				foreach (OrthographicViewFrame left in views)
				{
					if (ReferenceEquals(front, left) || ReferenceEquals(top, left) || !Near(front.Height, left.Height, tolerance))
					{
						continue;
					}
					if (Near(top.Height, left.Width, tolerance))
					{
						candidates.Add(Tuple.Create(front, top, left));
					}
				}
			}
		}
		if (candidates.Count == 0)
		{
			throw new InvalidOperationException("三张外轮廓对不上长对正、高平齐、宽相等。请确认每个视图是一条闭合轮廓，且外形尺寸一致。");
		}
		Tuple<OrthographicViewFrame, OrthographicViewFrame, OrthographicViewFrame> chosen = candidates
			.OrderByDescending((Tuple<OrthographicViewFrame, OrthographicViewFrame, OrthographicViewFrame> candidate) => ScoreThree(candidate.Item1, candidate.Item2, candidate.Item3))
			.ThenBy((Tuple<OrthographicViewFrame, OrthographicViewFrame, OrthographicViewFrame> candidate) => candidate.Item1.Id, StringComparer.Ordinal)
			.First();
		chosen.Item1.Role = OrthographicViewRole.Front;
		chosen.Item2.Role = OrthographicViewRole.Top;
		chosen.Item3.Role = OrthographicViewRole.Left;
	}

	private static int ScoreThree(OrthographicViewFrame front, OrthographicViewFrame top, OrthographicViewFrame left)
	{
		int score = 0;
		if (front.PinHoleCount > top.PinHoleCount && front.PinHoleCount > left.PinHoleCount)
		{
			score += 100;
		}
		if (top.CenterY < front.CenterY)
		{
			score += 10;
		}
		if (left.CenterX > front.CenterX)
		{
			score += 10;
		}
		return score;
	}

	private static void InferTwo(List<OrthographicViewFrame> views, double tolerance)
	{
		OrthographicViewFrame first = views[0];
		OrthographicViewFrame second = views[1];
		bool sameWidth = Near(first.Width, second.Width, tolerance);
		bool sameHeight = Near(first.Height, second.Height, tolerance);
		if (sameWidth && !sameHeight)
		{
			AssignLengthPair(first, second);
			return;
		}
		if (sameHeight && !sameWidth)
		{
			AssignHeightPair(first, second);
			return;
		}
		if (sameWidth && sameHeight)
		{
			double verticalGap = Math.Abs(first.CenterY - second.CenterY);
			double horizontalGap = Math.Abs(first.CenterX - second.CenterX);
			if (verticalGap >= horizontalGap)
			{
				AssignLengthPair(first, second);
			}
			else
			{
				AssignHeightPair(first, second);
			}
			return;
		}
		bool firstIsTop = Near(first.Height, second.Width, tolerance) && !Near(second.Height, first.Width, tolerance);
		bool secondIsTop = Near(second.Height, first.Width, tolerance) && !Near(first.Height, second.Width, tolerance);
		if (firstIsTop)
		{
			first.Role = OrthographicViewRole.Top;
			second.Role = OrthographicViewRole.Left;
			return;
		}
		if (secondIsTop)
		{
			second.Role = OrthographicViewRole.Top;
			first.Role = OrthographicViewRole.Left;
			return;
		}
		throw new InvalidOperationException("两张外轮廓对不上长对正、高平齐或宽相等。");
	}

	private static void AssignLengthPair(OrthographicViewFrame first, OrthographicViewFrame second)
	{
		OrthographicViewFrame front = ChooseFront(first, second, preferHigher: true);
		front.Role = OrthographicViewRole.Front;
		(ReferenceEquals(front, first) ? second : first).Role = OrthographicViewRole.Top;
	}

	private static void AssignHeightPair(OrthographicViewFrame first, OrthographicViewFrame second)
	{
		OrthographicViewFrame front = ChooseFront(first, second, preferHigher: false);
		front.Role = OrthographicViewRole.Front;
		(ReferenceEquals(front, first) ? second : first).Role = OrthographicViewRole.Left;
	}

	private static OrthographicViewFrame ChooseFront(OrthographicViewFrame first, OrthographicViewFrame second, bool preferHigher)
	{
		bool firstHasPins = first.PinHoleCount > 0;
		bool secondHasPins = second.PinHoleCount > 0;
		if (firstHasPins != secondHasPins)
		{
			return firstHasPins ? first : second;
		}
		if (preferHigher)
		{
			return first.CenterY >= second.CenterY ? first : second;
		}
		return first.CenterX <= second.CenterX ? first : second;
	}

	private static OrthographicViewSetAssignment BuildOwnership(List<OrthographicViewFrame> views)
	{
		OrthographicViewFrame front = views.FirstOrDefault((OrthographicViewFrame view) => view.Role == OrthographicViewRole.Front);
		OrthographicViewFrame top = views.FirstOrDefault((OrthographicViewFrame view) => view.Role == OrthographicViewRole.Top);
		OrthographicViewFrame left = views.FirstOrDefault((OrthographicViewFrame view) => view.Role == OrthographicViewRole.Left);
		if (front == null && top == null && left == null)
		{
			throw new InvalidOperationException("没有可用的视图角色。");
		}
		string lengthOwner = (front ?? top)?.Id;
		string heightOwner = (front ?? left)?.Id;
		string widthOwner = (top ?? left)?.Id;
		string pinOwner = null;
		List<OrthographicViewFrame> pinViews = views.Where((OrthographicViewFrame view) => view.PinHoleCount > 0).ToList();
		if (pinViews.Count > 0)
		{
			int maxPins = pinViews.Max((OrthographicViewFrame view) => view.PinHoleCount);
			pinOwner = pinViews
				.Where((OrthographicViewFrame view) => view.PinHoleCount == maxPins)
				.OrderBy((OrthographicViewFrame view) => RoleRank(view.Role))
				.ThenBy((OrthographicViewFrame view) => view.Id, StringComparer.Ordinal)
				.First()
				.Id;
		}
		OrthographicViewSetAssignment assignment = new OrthographicViewSetAssignment
		{
			LengthOwnerViewId = lengthOwner,
			HeightOwnerViewId = heightOwner,
			WidthOwnerViewId = widthOwner,
			PinOwnerViewId = pinOwner
		};
		foreach (OrthographicViewFrame view in views)
		{
			PartExtentKind horizontal;
			PartExtentKind vertical;
			switch (view.Role)
			{
			case OrthographicViewRole.Front:
				horizontal = PartExtentKind.Length;
				vertical = PartExtentKind.Height;
				break;
			case OrthographicViewRole.Top:
				horizontal = PartExtentKind.Length;
				vertical = PartExtentKind.Width;
				break;
			case OrthographicViewRole.Left:
				horizontal = PartExtentKind.Width;
				vertical = PartExtentKind.Height;
				break;
			default:
				throw new InvalidOperationException("视图 " + view.Id + " 没有角色。");
			}
			assignment.Views.Add(new ViewExtentOwnership
			{
				ViewId = view.Id,
				Role = view.Role,
				HorizontalExtent = horizontal,
				VerticalExtent = vertical,
				KeepHorizontalOverall = view.Id == (horizontal == PartExtentKind.Length ? lengthOwner : widthOwner),
				KeepVerticalOverall = view.Id == (vertical == PartExtentKind.Height ? heightOwner : widthOwner),
				KeepPinDimensions = view.Id == pinOwner
			});
		}
		return assignment;
	}

	private static int RoleRank(OrthographicViewRole role)
	{
		switch (role)
		{
		case OrthographicViewRole.Front:
			return 0;
		case OrthographicViewRole.Top:
			return 1;
		case OrthographicViewRole.Left:
			return 2;
		default:
			return 3;
		}
	}

	private static bool Near(double left, double right, double tolerance)
	{
		return Math.Abs(left - right) <= tolerance;
	}
}
