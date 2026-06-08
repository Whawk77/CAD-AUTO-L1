using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private List<List<SlotFeature>> GroupSlotsByRadius(IEnumerable<SlotFeature> slots)
        {
            var groups = new List<List<SlotFeature>>();
            if (slots == null)
            {
                return groups;
            }

            foreach (var slot in slots.Where(s => s != null && s.Radius > _config.GeometryTolerance))
            {
                var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(slot.Radius, 1.0) * 0.02);
                var group = groups.FirstOrDefault(g =>
                    g[0].IsSingleArcSlot == slot.IsSingleArcSlot
                    && Math.Abs(g[0].Radius - slot.Radius) <= tolerance);
                if (group == null)
                {
                    group = new List<SlotFeature>();
                    groups.Add(group);
                }

                group.Add(slot);
            }

            return groups
                .OrderBy(g => g[0].Radius)
                .ToList();
        }

        private string FormatSlotRadiusCallout(IList<SlotFeature> slots)
        {
            var slot = slots[0];
            var baseText = (slot.IsSingleArcSlot ? "R" : "2-R") + _config.FormatNumber(slot.Radius);
            return slots.Count > 1
                ? slots.Count.ToString(CultureInfo.InvariantCulture) + "x" + baseText
                : baseText;
        }

        private ChamferFeature PickChamferLeaderTarget(IList<ChamferFeature> features, OutlineFeature outline)
        {
            var centerX = (outline.MinX + outline.MaxX) / 2.0;
            var centerY = (outline.MinY + outline.MaxY) / 2.0;
            return features
                .OrderByDescending(f => DistanceSquared(Midpoint(f.StartPoint, f.EndPoint), new Point2d(centerX, centerY)))
                .First();
        }

        private FilletFeature PickFilletLeaderTarget(IList<FilletFeature> features, OutlineFeature outline)
        {
            var centerX = (outline.MinX + outline.MaxX) / 2.0;
            var centerY = (outline.MinY + outline.MaxY) / 2.0;
            return features
                .OrderByDescending(f => DistanceSquared(GetArcLeaderTarget(f), new Point2d(centerX, centerY)))
                .First();
        }

        private void AddCornerFeatureLeader(OutlineFeature outline, Point2d target, string text, int groupIndex, bool strictOutside)
        {
            var leaderOffset = Scale(_config.LeaderOffset + groupIndex * 3.0);
            var textGap = Scale(1.2);
            var arrowPoint = new Point3d(target.X, target.Y, 0.0);
            var placement = PickCornerLeaderPlacement(outline, target, leaderOffset, textGap, groupIndex, text, strictOutside);
            var landingPoint = new Point3d(placement.Landing.X, placement.Landing.Y, 0.0);
            var textPoint = new Point3d(placement.Text.X, placement.Text.Y, 0.0);

            AddCornerLeader(arrowPoint, landingPoint, textPoint, text);
        }

        private CornerLeaderPlacement PickCornerLeaderPlacement(
            OutlineFeature outline,
            Point2d target,
            double leaderOffset,
            double textGap,
            int groupIndex,
            string text,
            bool strictOutside)
        {
            var centerX = (outline.MinX + outline.MaxX) / 2.0;
            var centerY = (outline.MinY + outline.MaxY) / 2.0;
            var sx = target.X < centerX ? -1.0 : 1.0;
            var sy = target.Y < centerY ? -1.0 : 1.0;
            var stagger = Scale(groupIndex * 3.0);
            var directions = new List<Vector2d>
            {
                new Vector2d(0.0, sy),
                new Vector2d(sx, sy).GetNormal(),
                new Vector2d(-sx, sy).GetNormal(),
                new Vector2d(sx, 0.0),
                new Vector2d(-sx, 0.0),
                new Vector2d(sx, -sy).GetNormal(),
                new Vector2d(-sx, -sy).GetNormal(),
                new Vector2d(0.0, -sy)
            };
            double[] distanceFactors = strictOutside
                ? new[] { 0.85, 1.0, 1.2, 1.45, 1.75, 2.1, 2.5 }
                : new[] { 1.0, 1.25, 1.5, 2.0, 2.6 };

            var placements = directions
                .SelectMany(d => distanceFactors.Select(f => BuildCornerLeaderPlacement(target, d, leaderOffset * f, textGap, stagger)))
                .ToList();

            if (strictOutside)
            {
                var outsidePlacements = placements
                    .Where(p => IsCornerLeaderOutsideOutline(outline, target, p))
                    .ToList();
                if (outsidePlacements.Count > 0)
                {
                    placements = outsidePlacements;
                }
            }

            return placements
                .OrderBy(p => ScoreCornerLeaderPlacement(outline, target, p, text))
                .First();
        }

        private bool IsCornerLeaderOutsideOutline(OutlineFeature outline, Point2d target, CornerLeaderPlacement placement)
        {
            if (IsPointInsideOutlineByRayCast(placement.Landing.X, placement.Landing.Y, outline))
            {
                return false;
            }

            if (IsPointInsideOutlineByRayCast(placement.Text.X, placement.Text.Y, outline))
            {
                return false;
            }

            var midpoint = Midpoint(target, placement.Landing);
            return !IsPointInsideOutlineByRayCast(midpoint.X, midpoint.Y, outline);
        }

        private static CornerLeaderPlacement BuildCornerLeaderPlacement(Point2d target, Vector2d direction, double leaderOffset, double textGap, double stagger)
        {
            var landing = new Point2d(
                target.X + direction.X * leaderOffset,
                target.Y + direction.Y * leaderOffset + Math.Sign(direction.Y) * stagger);
            var textDirectionX = Math.Abs(direction.X) <= 1e-9 ? -1.0 : Math.Sign(direction.X);
            var text = new Point2d(
                landing.X + textDirectionX * textGap,
                landing.Y);
            return new CornerLeaderPlacement(landing, text, textDirectionX < 0.0);
        }

        private double ScoreCornerLeaderPlacement(OutlineFeature outline, Point2d target, CornerLeaderPlacement placement, string textValue)
        {
            var score = 0.0;
            var text = placement.Text;
            var margin = Scale(_config.FirstDimOffset + _config.TextHeight * 3.0);
            var textBounds = ComputeCornerLeaderTextBounds(placement, textValue);
            var centerY = (outline.MinY + outline.MaxY) / 2.0;
            var targetNearTop = target.Y >= centerY;
            var targetNearBottom = target.Y < centerY;

            if (text.X > outline.MinX && text.X < outline.MaxX && text.Y > outline.MinY && text.Y < outline.MaxY)
            {
                score += 50000.0;
            }

            if (IsPointInsideOutlineByRayCast(placement.Landing.X, placement.Landing.Y, outline))
            {
                score += 50000.0;
            }

            if (IsPointInsideOutlineByRayCast(text.X, text.Y, outline))
            {
                score += 50000.0;
            }

            var leaderMidpoint = Midpoint(target, placement.Landing);
            if (IsPointInsideOutlineByRayCast(leaderMidpoint.X, leaderMidpoint.Y, outline))
            {
                score += 50000.0;
            }

            if (targetNearTop && text.Y < target.Y - _config.GeometryTolerance)
            {
                score += 8000.0;
            }

            if (targetNearBottom && text.Y > target.Y + _config.GeometryTolerance)
            {
                score += 8000.0;
            }

            if (_leftDims.Count > 0 && text.X < outline.MinX && text.X > outline.MinX - margin * 2.5)
            {
                score += 400.0;
            }

            if (_rightDims.Count > 0 && text.X > outline.MaxX && text.X < outline.MaxX + margin * 2.5)
            {
                score += 400.0;
            }

            if (_bottomDims.Count > 0 && text.Y < outline.MinY && text.Y > outline.MinY - margin * 2.5)
            {
                score += 400.0;
            }

            if (_topDims.Count > 0 && text.Y > outline.MaxY && text.Y < outline.MaxY + margin * 2.5)
            {
                score += 400.0;
            }

            foreach (var obstacle in _linearDimTextObstacles)
            {
                if (BoundsOverlap(textBounds, obstacle, Scale(0.8)))
                {
                    score += 3500.0;
                }
            }

            var preferredDistance = Scale(_config.LeaderOffset * 1.35);
            var actualDistance = placement.Landing.GetDistanceTo(target);
            score += Math.Abs(actualDistance - preferredDistance) * 40.0;
            score += actualDistance * 2.0;
            score += Math.Abs(text.X - placement.Landing.X) * 0.01;
            score += Math.Abs(text.Y - placement.Landing.Y) * 0.01;
            return score;
        }

        private TextBounds ComputeCornerLeaderTextBounds(Point2d textPoint, string text)
        {
            var textHeight = GetDimStyleTextHeight(_diameterCalloutDimStyleId);
            var width = Math.Max((text ?? string.Empty).Length, 2) * textHeight * 0.75;
            var halfHeight = textHeight * 0.65;
            return new TextBounds
            {
                MinX = textPoint.X,
                MaxX = textPoint.X + width,
                MinY = textPoint.Y - halfHeight,
                MaxY = textPoint.Y + halfHeight
            };
        }

        private TextBounds ComputeCornerLeaderTextBounds(CornerLeaderPlacement placement, string text)
        {
            var textHeight = GetDimStyleTextHeight(_diameterCalloutDimStyleId);
            var width = Math.Max((text ?? string.Empty).Length, 2) * textHeight * 0.75;
            var halfHeight = textHeight * 0.65;
            return new TextBounds
            {
                MinX = placement.TextExtendsLeft ? placement.Text.X - width : placement.Text.X,
                MaxX = placement.TextExtendsLeft ? placement.Text.X : placement.Text.X + width,
                MinY = placement.Text.Y - halfHeight,
                MaxY = placement.Text.Y + halfHeight
            };
        }

        private static bool BoundsOverlap(TextBounds a, TextBounds b, double gap)
        {
            return a.MinX <= b.MaxX + gap
                && a.MaxX >= b.MinX - gap
                && a.MinY <= b.MaxY + gap
                && a.MaxY >= b.MinY - gap;
        }

        private sealed class CornerLeaderPlacement
        {
            public CornerLeaderPlacement(Point2d landing, Point2d text, bool textExtendsLeft)
            {
                Landing = landing;
                Text = text;
                TextExtendsLeft = textExtendsLeft;
            }

            public Point2d Landing { get; }
            public Point2d Text { get; }
            public bool TextExtendsLeft { get; }
        }

        private static Point2d GetArcLeaderTarget(FilletFeature feature)
        {
            var startVector = feature.StartPoint - feature.Center;
            var endVector = feature.EndPoint - feature.Center;
            var combined = startVector + endVector;
            if (combined.Length <= 1e-9)
            {
                combined = startVector;
            }

            var direction = combined.GetNormal();
            return feature.Center + direction * feature.Radius;
        }

        private static double DistanceSquared(Point2d a, Point2d b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}
