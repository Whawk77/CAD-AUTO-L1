using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private static bool CanIgnoreTextConflictWithLooseChain(DeferredDim candidate, DeferredDim existing)
        {
            return candidate.LooseChainId != 0 || existing.LooseChainId != 0;
        }

        private void PromoteLooseSideLevel(
            List<List<(DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB)>> layers,
            int targetLevel)
        {
            while (layers.Count <= targetLevel)
            {
                layers.Add(new List<(DeferredDim, double, double, double, double)>());
            }

            for (int level = 0; level < layers.Count; level++)
            {
                if (level == targetLevel)
                {
                    continue;
                }

                for (int i = layers[level].Count - 1; i >= 0; i--)
                {
                    if (layers[level][i].Dim.LooseChainId == 0)
                    {
                        continue;
                    }

                    var item = layers[level][i];
                    layers[level].RemoveAt(i);
                    layers[targetLevel].Add(item);
                }
            }
        }

        private void AlignDimensionsBySharedExtensionLines(
            List<List<(DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB)>> layers,
            DimSide side,
            OutlineFeature outline,
            double textHeight,
            double gap,
            double firstOffset,
            double perLevelSpacing,
            bool isHorizontal)
        {
            for (int sourceLevel = 0; sourceLevel < layers.Count - 1; sourceLevel++)
            {
                for (int i = layers[sourceLevel].Count - 1; i >= 0; i--)
                {
                    var item = layers[sourceLevel][i];
                    if (item.Dim.DimType == DimensionType.HoleLocation)
                    {
                        continue;
                    }

                    if (HasArrowEndpointTouch(item, layers, sourceLevel, i, side, outline, firstOffset, perLevelSpacing, isHorizontal))
                    {
                        continue;
                    }

                    var targetLevel = FindSharedExtensionAlignmentLevel(
                        item,
                        layers,
                        sourceLevel,
                        side,
                        outline,
                        textHeight,
                        gap,
                        firstOffset,
                        perLevelSpacing,
                        isHorizontal);

                    if (targetLevel <= sourceLevel)
                    {
                        continue;
                    }

                    layers[sourceLevel].RemoveAt(i);
                    layers[targetLevel].Add(item);
                }
            }
        }

        private bool HasArrowEndpointTouch(
            (DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB) candidate,
            List<List<(DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB)>> layers,
            int candidateLevel,
            int candidateIndex,
            DimSide side,
            OutlineFeature outline,
            double firstOffset,
            double perLevelSpacing,
            bool isHorizontal)
        {
            var candidateOffset = firstOffset + candidateLevel * perLevelSpacing;
            var candidateDimLinePoint = GetDimLinePoint(candidate.Dim, side, outline, candidateOffset);
            for (int level = 0; level < layers.Count; level++)
            {
                var otherOffset = firstOffset + level * perLevelSpacing;
                for (int index = 0; index < layers[level].Count; index++)
                {
                    if (level == candidateLevel && index == candidateIndex)
                    {
                        continue;
                    }

                    var other = layers[level][index];
                    var otherDimLinePoint = GetDimLinePoint(other.Dim, side, outline, otherOffset);
                    if (ArrowEndpointTouches(candidate.Dim.XLine1, candidateDimLinePoint, other.Dim.XLine1, otherDimLinePoint, isHorizontal)
                        || ArrowEndpointTouches(candidate.Dim.XLine1, candidateDimLinePoint, other.Dim.XLine2, otherDimLinePoint, isHorizontal)
                        || ArrowEndpointTouches(candidate.Dim.XLine2, candidateDimLinePoint, other.Dim.XLine1, otherDimLinePoint, isHorizontal)
                        || ArrowEndpointTouches(candidate.Dim.XLine2, candidateDimLinePoint, other.Dim.XLine2, otherDimLinePoint, isHorizontal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ArrowEndpointTouches(
            Point3d featureA,
            Point3d dimLineA,
            Point3d featureB,
            Point3d dimLineB,
            bool isHorizontal)
        {
            var arrowA = isHorizontal
                ? new Point3d(featureA.X, dimLineA.Y, 0.0)
                : new Point3d(dimLineA.X, featureA.Y, 0.0);
            var arrowB = isHorizontal
                ? new Point3d(featureB.X, dimLineB.Y, 0.0)
                : new Point3d(dimLineB.X, featureB.Y, 0.0);

            return arrowA.DistanceTo(arrowB) <= _config.GeometryTolerance;
        }

        private int FindSharedExtensionAlignmentLevel(
            (DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB) candidate,
            List<List<(DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB)>> layers,
            int sourceLevel,
            DimSide side,
            OutlineFeature outline,
            double textHeight,
            double gap,
            double firstOffset,
            double perLevelSpacing,
            bool isHorizontal)
        {
            for (int targetLevel = sourceLevel + 1; targetLevel < layers.Count; targetLevel++)
            {
                var targetOffset = firstOffset + targetLevel * perLevelSpacing;
                if (TextCoversOutline(candidate.Dim, side, targetOffset, textHeight, outline, isHorizontal))
                {
                    continue;
                }

                bool sharesExtension = false;
                bool placementOk = true;
                foreach (var existing in layers[targetLevel])
                {
                    if (HasSharedExtensionLine(candidate.Dim, existing.Dim, side, outline, targetOffset))
                    {
                        sharesExtension = true;
                    }

                    if (HasStrictArrowConflict(candidate.ArrA, candidate.ArrB, existing.ArrA, existing.ArrB)
                        || !AreCompatible(existing.TxtA, existing.TxtB, candidate.TxtA, candidate.TxtB, gap))
                    {
                        placementOk = false;
                        break;
                    }
                }

                if (sharesExtension && placementOk)
                {
                    return targetLevel;
                }
            }

            return sourceLevel;
        }

        private bool HasSharedExtensionLine(DeferredDim a, DeferredDim b, DimSide side, OutlineFeature outline, double targetOffset)
        {
            if (side == DimSide.Bottom || side == DimSide.Top)
            {
                var lineY = side == DimSide.Bottom
                    ? outline.MinY - targetOffset
                    : outline.MaxY + targetOffset;
                return ExtensionLineOverlapsAtCoordinate(a.XLine1.X, a.XLine1.Y, lineY, b.XLine1.X, b.XLine1.Y, lineY)
                    || ExtensionLineOverlapsAtCoordinate(a.XLine1.X, a.XLine1.Y, lineY, b.XLine2.X, b.XLine2.Y, lineY)
                    || ExtensionLineOverlapsAtCoordinate(a.XLine2.X, a.XLine2.Y, lineY, b.XLine1.X, b.XLine1.Y, lineY)
                    || ExtensionLineOverlapsAtCoordinate(a.XLine2.X, a.XLine2.Y, lineY, b.XLine2.X, b.XLine2.Y, lineY);
            }

            var lineX = side == DimSide.Left
                ? outline.MinX - targetOffset
                : outline.MaxX + targetOffset;
            return ExtensionLineOverlapsAtCoordinate(a.XLine1.Y, a.XLine1.X, lineX, b.XLine1.Y, b.XLine1.X, lineX)
                || ExtensionLineOverlapsAtCoordinate(a.XLine1.Y, a.XLine1.X, lineX, b.XLine2.Y, b.XLine2.X, lineX)
                || ExtensionLineOverlapsAtCoordinate(a.XLine2.Y, a.XLine2.X, lineX, b.XLine1.Y, b.XLine1.X, lineX)
                || ExtensionLineOverlapsAtCoordinate(a.XLine2.Y, a.XLine2.X, lineX, b.XLine2.Y, b.XLine2.X, lineX);
        }

        private bool ExtensionLineOverlapsAtCoordinate(
            double coordinateA,
            double featureA,
            double lineA,
            double coordinateB,
            double featureB,
            double lineB)
        {
            if (Math.Abs(coordinateA - coordinateB) > _config.GeometryTolerance)
            {
                return false;
            }

            var a1 = Math.Min(featureA, lineA);
            var a2 = Math.Max(featureA, lineA);
            var b1 = Math.Min(featureB, lineB);
            var b2 = Math.Max(featureB, lineB);
            return a1 <= b2 + _config.GeometryTolerance && b1 <= a2 + _config.GeometryTolerance;
        }

        private bool HasStrictArrowConflict(double aA, double aB, double bA, double bB)
        {
            var tol = _config.GeometryTolerance;
            bool aEndInB = (bA + tol < aA && aA < bB - tol) || (bA + tol < aB && aB < bB - tol);
            bool bEndInA = (aA + tol < bA && bA < aB - tol) || (aA + tol < bB && bB < aB - tol);
            return aEndInB || bEndInA;
        }

        private static bool AreCompatible(double aA, double aB, double bA, double bB, double gap)
        {
            return aB + gap <= bA || bB + gap <= aA;
        }
    }
}
