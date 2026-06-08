using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        public void DrawSlotDimensions(OutlineFeature outline, DatumDefinition datum, IList<IList<HoleFeature>> rows, IEnumerable<SlotFeature> slots)
        {
            if (slots == null)
            {
                return;
            }

            var slotList = slots.Where(s => s != null).ToList();
            var hasPinHoles = rows != null && rows.SelectMany(r => r).Any(h => h.IsPinHole);
            if (!hasPinHoles)
            {
                foreach (var slot in slotList)
                {
                    DrawSlotCenterDistanceDimension(slot);
                }

                DrawSlotContinuousLocationsFromDatum(outline, datum, slotList);
                return;
            }

            foreach (var slot in slotList)
            {
                DrawSlotCenterDistanceDimension(slot);
            }

            DrawSlotAnchorLocations(datum, rows, slotList);
        }

        private void DrawSlotContinuousLocationsFromDatum(OutlineFeature outline, DatumDefinition datum, IList<SlotFeature> slots)
        {
            if (datum == null || slots == null || slots.Count == 0)
            {
                return;
            }

            var verticalSlots = slots.Where(IsVerticalSlot).ToList();
            var horizontalSlots = slots.Where(s => !IsVerticalSlot(s)).ToList();

            AddVerticalSlotContinuousDimensions(datum, verticalSlots);
            AddHorizontalSlotContinuousDimensions(outline, datum, horizontalSlots);
        }

        private void AddVerticalSlotContinuousDimensions(DatumDefinition datum, IList<SlotFeature> slots)
        {
            foreach (var group in GroupSlotAnchorsByCoordinate(slots, datum, p => p.Y))
            {
                var ordered = UniquePointsByCoordinate(group.OrderBy(p => p.X), p => p.X);
                AddHorizontalChainFromDatum(datum.BaseX, ordered, "SlotChainH");
                if (ordered.Count > 0)
                {
                    AddVerticalDimFromY(datum.BaseY, PickNearestPointByX(ordered, datum.BaseX), string.Empty, DimensionType.Normal, "SlotDatumV");
                }
            }
        }

        private void AddHorizontalSlotContinuousDimensions(OutlineFeature outline, DatumDefinition datum, IList<SlotFeature> slots)
        {
            foreach (var group in GroupSlotAnchorsByCoordinate(slots, datum, p => p.X))
            {
                var ordered = UniquePointsByCoordinate(group.OrderBy(p => p.Y), p => p.Y);
                AddVerticalChainFromDatum(datum.BaseY, ordered, "SlotChainV");
                if (ordered.Count > 0)
                {
                    var target = PickNearestPointByY(ordered, datum.BaseY);
                    var slot = FindSlotByAnchor(slots, target);
                    if (slot != null && slot.IsSingleArcSlot && outline != null)
                    {
                        AddSingleArcSlotHorizontalDatumDimension(outline, datum, slot, target);
                    }
                    else
                    {
                        AddHorizontalDimFromX(datum.BaseX, target, string.Empty, DimensionType.Normal, "SlotDatumH");
                    }
                }
            }
        }

        private void AddVerticalChainFromDatum(double datumY, IList<Point3d> ordered, string debugRole)
        {
            if (ordered == null || ordered.Count == 0)
            {
                return;
            }

            AddVerticalDimFromY(datumY, ordered[0], string.Empty, DimensionType.Normal, debugRole);
            for (int i = 1; i < ordered.Count; i++)
            {
                AddVerticalDimFromPoint(ordered[i - 1], ordered[i], string.Empty, DimensionType.Normal, debugRole);
            }
        }

        private void AddHorizontalChainFromDatum(double datumX, IList<Point3d> ordered, string debugRole)
        {
            if (ordered == null || ordered.Count == 0)
            {
                return;
            }

            AddHorizontalDimFromX(datumX, ordered[0], string.Empty, DimensionType.Normal, debugRole);
            for (int i = 1; i < ordered.Count; i++)
            {
                AddHorizontalDimFromPoint(ordered[i - 1], ordered[i], string.Empty, DimensionType.Normal, debugRole);
            }
        }

        private List<List<Point3d>> GroupSlotCentersByCoordinate(IEnumerable<SlotFeature> slots, Func<Point3d, double> coordinate)
        {
            var groups = new List<List<Point3d>>();
            if (slots == null)
            {
                return groups;
            }

            foreach (var point in slots.SelectMany(GetSlotCenterPoints))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(coordinate(g[0]) - coordinate(point)) <= _config.GeometryTolerance);
                if (group == null)
                {
                    group = new List<Point3d>();
                    groups.Add(group);
                }

                group.Add(point);
            }

            return groups;
        }

        private List<List<Point3d>> GroupSlotAnchorsByCoordinate(
            IEnumerable<SlotFeature> slots,
            DatumDefinition datum,
            Func<Point3d, double> coordinate)
        {
            var groups = new List<List<Point3d>>();
            if (slots == null || datum == null)
            {
                return groups;
            }

            foreach (var point in slots.Select(s => PickSlotAnchorPoint(s, datum)))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(coordinate(g[0]) - coordinate(point)) <= _config.GeometryTolerance);
                if (group == null)
                {
                    group = new List<Point3d>();
                    groups.Add(group);
                }

                group.Add(point);
            }

            return groups;
        }

        private List<Point3d> UniquePointsByCoordinate(IEnumerable<Point3d> points, Func<Point3d, double> coordinate)
        {
            var unique = new List<Point3d>();
            foreach (var point in points)
            {
                if (unique.Count == 0 || Math.Abs(coordinate(unique[unique.Count - 1]) - coordinate(point)) > _config.GeometryTolerance)
                {
                    unique.Add(point);
                }
            }

            return unique;
        }

        private SlotFeature FindSlotByAnchor(IEnumerable<SlotFeature> slots, Point3d anchor)
        {
            if (slots == null)
            {
                return null;
            }

            return slots.FirstOrDefault(s =>
                s != null
                && Math.Abs(s.FirstCenter.X - anchor.X) <= _config.GeometryTolerance
                && Math.Abs(s.FirstCenter.Y - anchor.Y) <= _config.GeometryTolerance);
        }

        private void AddSingleArcSlotHorizontalDatumDimension(
            OutlineFeature outline,
            DatumDefinition datum,
            SlotFeature slot,
            Point3d center)
        {
            var from = FindOutlinePointAtX(outline, datum.BaseX, center.Y);
            var to = GetSingleArcSlotHorizontalGripPoint(slot, from.Y);
            var span = Math.Abs(center.X - datum.BaseX);
            if (span <= _config.GeometryTolerance)
            {
                return;
            }

            AddHorizontalDimFromPoint(from, to, _config.FormatNumber(span), DimensionType.Normal, "SingleArcSlotDatum");
        }

        private Point3d FindOutlinePointAtX(OutlineFeature outline, double x, double preferredY)
        {
            var candidates = new List<Point2d>();
            foreach (var vertex in outline.Vertices)
            {
                if (Math.Abs(vertex.X - x) <= _config.GeometryTolerance)
                {
                    candidates.Add(vertex);
                }
            }

            foreach (var segment in outline.Segments.Where(s => s.IsVertical(_config.GeometryTolerance)))
            {
                if (Math.Abs(segment.Start.X - x) > _config.GeometryTolerance)
                {
                    continue;
                }

                var y = Math.Max(segment.MinY, Math.Min(segment.MaxY, preferredY));
                candidates.Add(new Point2d(x, y));
            }

            var best = candidates
                .OrderBy(p => Math.Abs(p.Y - preferredY))
                .FirstOrDefault();
            return ToPoint3d(best, x, preferredY);
        }

        private Point3d GetSingleArcSlotHorizontalGripPoint(SlotFeature slot, double preferredY)
        {
            var upper = new Point3d(slot.FirstCenter.X, slot.FirstCenter.Y + slot.Radius, 0.0);
            var lower = new Point3d(slot.FirstCenter.X, slot.FirstCenter.Y - slot.Radius, 0.0);
            return Math.Abs(upper.Y - preferredY) <= Math.Abs(lower.Y - preferredY) ? upper : lower;
        }

        private static IEnumerable<Point3d> GetSlotCenterPoints(SlotFeature slot)
        {
            if (slot.IsSingleArcSlot)
            {
                yield return new Point3d(slot.FirstCenter.X, slot.FirstCenter.Y, 0.0);
                yield break;
            }

            yield return new Point3d(slot.FirstCenter.X, slot.FirstCenter.Y, 0.0);
            yield return new Point3d(slot.SecondCenter.X, slot.SecondCenter.Y, 0.0);
        }

        private static bool IsVerticalSlot(SlotFeature slot)
        {
            if (slot.IsSingleArcSlot)
            {
                return slot.IsVertical;
            }

            return Math.Abs(slot.FirstCenter.Y - slot.SecondCenter.Y) >= Math.Abs(slot.FirstCenter.X - slot.SecondCenter.X);
        }

        private static Point3d PickNearestPointByY(IEnumerable<Point3d> points, double y)
        {
            return points.OrderBy(p => Math.Abs(p.Y - y)).First();
        }

        private static Point3d PickNearestPointByX(IEnumerable<Point3d> points, double x)
        {
            return points.OrderBy(p => Math.Abs(p.X - x)).First();
        }

        private void DrawSlotCenterDistanceDimension(SlotFeature slot)
        {
            if (slot == null || slot.CenterDistance <= _config.GeometryTolerance)
            {
                return;
            }

            var first = new Point3d(slot.FirstCenter.X, slot.FirstCenter.Y, 0.0);
            var second = new Point3d(slot.SecondCenter.X, slot.SecondCenter.Y, 0.0);
            var dx = Math.Abs(first.X - second.X);
            var dy = Math.Abs(first.Y - second.Y);
            if (dx <= _config.GeometryTolerance && dy <= _config.GeometryTolerance)
            {
                return;
            }

            if (dx >= dy)
            {
                AddHorizontalDimFromPoint(first, second, string.Empty, DimensionType.Normal, "SlotCenter");
            }
            else
            {
                AddVerticalDimFromPoint(first, second, string.Empty, DimensionType.Normal, "SlotCenter");
            }
        }

        private void DrawSlotAnchorLocations(DatumDefinition datum, IList<IList<HoleFeature>> rows, IList<SlotFeature> slots)
        {
            if (datum == null || rows == null || slots == null || slots.Count == 0)
            {
                return;
            }

            var holes = rows.SelectMany(r => r).ToList();
            var pinGroups = BuildPinGroupPlan(holes, datum.DatumHole);
            if (pinGroups.Count == 0)
            {
                foreach (var slot in slots)
                {
                    var anchor = PickSlotAnchorPoint(slot, datum);
                    AddHorizontalDimFromX(datum.BaseX, anchor, string.Empty, DimensionType.Normal, "SlotDatumH");
                    AddVerticalDimFromY(datum.BaseY, anchor, string.Empty, DimensionType.Normal, "SlotDatumV");
                }

                return;
            }

            var referencePins = pinGroups
                .SelectMany(g => g.Pins.Select(p => new { Pin = p, IsGroupBase = IsSameHole(p, g.BasePin) }))
                .ToList();

            foreach (var slot in slots)
            {
                var anchor = PickSlotAnchorPoint(slot, datum);
                var reference = referencePins
                    .OrderBy(r => DistanceSquared(r.Pin.Center, anchor))
                    .ThenByDescending(r => r.IsGroupBase ? 1 : 0)
                    .FirstOrDefault();
                if (reference == null)
                {
                    continue;
                }

                AddHorizontalDim(reference.Pin.Center, anchor, string.Empty, DimensionType.Normal, "SlotPinRef");
                AddVerticalDim(reference.Pin.Center, anchor, string.Empty, DimensionType.Normal, "SlotPinRef");
            }
        }

        private Point3d PickSlotAnchorPoint(SlotFeature slot, DatumDefinition datum)
        {
            var first = new Point3d(slot.FirstCenter.X, slot.FirstCenter.Y, 0.0);
            if (slot.IsSingleArcSlot)
            {
                return first;
            }

            var second = new Point3d(slot.SecondCenter.X, slot.SecondCenter.Y, 0.0);
            var dx = Math.Abs(first.X - second.X);
            var dy = Math.Abs(first.Y - second.Y);

            if (dx >= dy)
            {
                return Math.Abs(first.X - datum.BaseX) <= Math.Abs(second.X - datum.BaseX) ? first : second;
            }

            return Math.Abs(first.Y - datum.BaseY) <= Math.Abs(second.Y - datum.BaseY) ? first : second;
        }
    }
}
