using System.Collections.Generic;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;

namespace AutoFixtureDim
{
    internal static class CoreModelMapper
    {
        public static CadAuto.Core.Rules.DimensionRuleConfig ToCoreConfig(DimensionRuleConfig source)
        {
            var target = CadAuto.Core.Rules.DimensionRuleConfig.CreateDefault();
            target.GeometryTolerance = source.GeometryTolerance;
            target.TextHeight = source.TextHeight;
            target.ArrowSize = source.ArrowSize;
            target.FirstDimOffset = source.FirstDimOffset;
            target.DimTextClearance = source.DimTextClearance;
            target.LeaderOffset = source.LeaderOffset;
            target.ThreadArcAngleToleranceDegrees = source.ThreadArcAngleToleranceDegrees;
            target.ThreadMinorDiameterTolerance = source.ThreadMinorDiameterTolerance;
            target.DatumHoleLocationToleranceText = source.DatumHoleLocationToleranceText;
            target.DatumHoleLocationDefaultUseTolerance = source.DatumHoleLocationDefaultUseTolerance;
            target.PinCenterDistanceToleranceText = source.PinCenterDistanceToleranceText;
            target.PinGroupDistanceToleranceText = source.PinGroupDistanceToleranceText;
            target.PinHoleFitToleranceText = source.PinHoleFitToleranceText;

            target.HoleFitTolerance.Clear();
            foreach (var pair in source.HoleFitTolerance)
            {
                target.HoleFitTolerance[pair.Key] = pair.Value;
            }

            target.CenterDistanceTolerance.Clear();
            foreach (var pair in source.CenterDistanceTolerance)
            {
                target.CenterDistanceTolerance[pair.Key] = pair.Value;
            }

            target.ThreadMinorDiameterCallout.Clear();
            foreach (var pair in source.ThreadMinorDiameterCallout)
            {
                target.ThreadMinorDiameterCallout[pair.Key] = pair.Value;
            }

            return target;
        }

        public static OutlineFeature2D ToCoreOutline(OutlineFeature source)
        {
            var target = new OutlineFeature2D
            {
                MinX = source.MinX,
                MaxX = source.MaxX,
                MinY = source.MinY,
                MaxY = source.MaxY
            };

            foreach (var vertex in source.Vertices)
            {
                target.Vertices.Add(new Point2D(vertex.X, vertex.Y));
            }

            foreach (var segment in source.Segments)
            {
                target.Segments.Add(new Segment2D(
                    new Point2D(segment.Start.X, segment.Start.Y),
                    new Point2D(segment.End.X, segment.End.Y))
                {
                    SourceKey = segment.SourceId.ToString(),
                    IsArcChord = segment.IsArcChord
                });
            }

            foreach (var arc in source.Arcs)
            {
                target.Arcs.Add(new Arc2D
                {
                    Start = new Point2D(arc.Start.X, arc.Start.Y),
                    End = new Point2D(arc.End.X, arc.End.Y),
                    Center = new Point2D(arc.Center.X, arc.Center.Y),
                    Radius = arc.Radius,
                    Bulge = arc.Bulge,
                    SourceKey = arc.SourceId.ToString()
                });
            }

            foreach (var chamfer in source.Chamfers)
            {
                target.Chamfers.Add(new ChamferFeature2D
                {
                    StartPoint = new Point2D(chamfer.StartPoint.X, chamfer.StartPoint.Y),
                    EndPoint = new Point2D(chamfer.EndPoint.X, chamfer.EndPoint.Y),
                    Length = chamfer.Length,
                    DeltaX = chamfer.DeltaX,
                    DeltaY = chamfer.DeltaY,
                    Value = chamfer.Value,
                    Text = chamfer.Text,
                    Confidence = chamfer.Confidence
                });
            }

            foreach (var fillet in source.Fillets)
            {
                target.Fillets.Add(new FilletFeature2D
                {
                    Center = new Point2D(fillet.Center.X, fillet.Center.Y),
                    Radius = fillet.Radius,
                    StartPoint = new Point2D(fillet.StartPoint.X, fillet.StartPoint.Y),
                    EndPoint = new Point2D(fillet.EndPoint.X, fillet.EndPoint.Y),
                    Text = fillet.Text,
                    Confidence = fillet.Confidence
                });
            }

            return target;
        }

        public static Datum2D ToCoreDatum(DatumDefinition source)
        {
            return new Datum2D
            {
                BaseX = source.BaseX,
                BaseY = source.BaseY,
                DatumHole = source.DatumHole == null ? null : ToCoreHole(source.DatumHole),
                DatumHoleLocationBaseX = source.DatumHoleLocationBaseX,
                DatumHoleLocationBaseY = source.DatumHoleLocationBaseY,
                DatumHoleLocationUseToleranceX = source.DatumHoleLocationUseToleranceX,
                DatumHoleLocationUseToleranceY = source.DatumHoleLocationUseToleranceY
            };
        }

        public static List<HoleFeature2D> ToCoreHoles(IEnumerable<HoleFeature> source)
        {
            var holes = new List<HoleFeature2D>();
            foreach (var hole in source)
            {
                holes.Add(ToCoreHole(hole));
            }

            return holes;
        }

        private static HoleFeature2D ToCoreHole(HoleFeature source)
        {
            return new HoleFeature2D
            {
                Center = new Point2D(source.Center.X, source.Center.Y),
                Diameter = source.Diameter,
                SourceKey = source.SourceId.ToString(),
                FitTolerance = source.FitTolerance,
                ThreadCallout = source.ThreadCallout,
                Kind = ToCoreHoleKind(source.HoleKind)
            };
        }

        private static HoleKind2D ToCoreHoleKind(HoleKind source)
        {
            switch (source)
            {
                case HoleKind.Pin:
                    return HoleKind2D.Pin;
                case HoleKind.Thread:
                    return HoleKind2D.Thread;
                case HoleKind.Slot:
                    return HoleKind2D.Slot;
                default:
                    return HoleKind2D.Normal;
            }
        }
    }
}
