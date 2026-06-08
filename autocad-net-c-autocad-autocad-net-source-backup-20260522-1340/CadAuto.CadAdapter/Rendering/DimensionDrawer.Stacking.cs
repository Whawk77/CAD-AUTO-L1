using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        public void FlushStackedDimensions(OutlineFeature outline)
        {
            var textHeight = GetDimStyleTextHeight(_dimStyleId);
            var perLevelSpacing = textHeight + Scale(_config.DimTextClearance);

            RebalanceVerticalHoleLocationSides();
            SuppressMirroredHorizontalDuplicates();
            SuppressMirroredVerticalDuplicates();

            if (ShouldFlushDiagnosticSide(DimSide.Bottom))
            {
                FlushSide(_bottomDims, DimSide.Bottom, outline, perLevelSpacing);
            }

            if (ShouldFlushDiagnosticSide(DimSide.Top))
            {
                FlushSide(_topDims, DimSide.Top, outline, perLevelSpacing);
            }

            if (ShouldFlushDiagnosticSide(DimSide.Left))
            {
                FlushSide(_leftDims, DimSide.Left, outline, perLevelSpacing);
            }

            if (ShouldFlushDiagnosticSide(DimSide.Right))
            {
                FlushSide(_rightDims, DimSide.Right, outline, perLevelSpacing);
            }
        }

        private bool ShouldFlushDiagnosticSide(DimSide side)
        {
            return !_diagnosticsEnabled
                || _diagnosticSide == DiagnosticDimensionSide.All
                || MatchesDiagnosticSide(side);
        }

        private void FlushSide(List<DeferredDim> dims, DimSide side, OutlineFeature outline, double perLevelSpacing)
        {
            if (dims.Count == 0)
            {
                return;
            }

            var textHeight = GetDimStyleTextHeight(_dimStyleId);
            var gap = textHeight * 0.5;
            var isHorizontal = side == DimSide.Bottom || side == DimSide.Top;
            dims.Sort((a, b) => a.Span.CompareTo(b.Span));
            SuppressDuplicateMeasuredDimensions(dims, isHorizontal);

            var firstOffset = Scale(_config.FirstDimOffset);
            var layers = new List<List<(DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB)>>();
            var looseChainLevels = new Dictionary<int, int>();
            int? looseSideLevel = null;

            foreach (var dim in dims)
            {
                var textBox = ComputeTextInterval(dim, isHorizontal, textHeight);
                var arrow = ComputeArrowInterval(dim, isHorizontal);

                int minLevel = 0;
                for (int li = 0; li < layers.Count; li++)
                {
                    foreach (var existing in layers[li])
                    {
                        if (HasStrictArrowConflict(arrow.A, arrow.B, existing.ArrA, existing.ArrB))
                        {
                            if (li + 1 > minLevel) minLevel = li + 1;
                        }
                    }
                }

                int chainLevel;
                int targetLevel = dim.ForceOuterLevel ? System.Math.Max(minLevel, layers.Count) : minLevel;
                if (isHorizontal
                    && !dim.ForceOuterLevel
                    && dim.LooseChainId != 0
                    && looseChainLevels.TryGetValue(dim.LooseChainId, out chainLevel))
                {
                    targetLevel = System.Math.Max(chainLevel, minLevel);
                }
                if (isHorizontal && !dim.ForceOuterLevel && dim.LooseChainId != 0 && looseSideLevel.HasValue)
                {
                    targetLevel = System.Math.Max(looseSideLevel.Value, minLevel);
                }

                while (true)
                {
                    double offset = firstOffset + targetLevel * perLevelSpacing;
                    if (TextCoversOutline(dim, side, offset, textHeight, outline, isHorizontal))
                    {
                        targetLevel++;
                        continue;
                    }

                    if (targetLevel < layers.Count)
                    {
                        bool textOk = true;
                        foreach (var existing in layers[targetLevel])
                        {
                            if (isHorizontal && dim.LooseChainId != 0 && existing.Dim.LooseChainId != 0)
                            {
                                continue;
                            }

                            if (isHorizontal
                                && CanIgnoreTextConflictWithLooseChain(dim, existing.Dim)
                                && !HasStrictArrowConflict(arrow.A, arrow.B, existing.ArrA, existing.ArrB))
                            {
                                continue;
                            }

                            if (!AreCompatible(existing.TxtA, existing.TxtB, textBox.A, textBox.B, gap))
                            {
                                textOk = false;
                                break;
                            }
                        }
                        if (!textOk)
                        {
                            targetLevel++;
                            continue;
                        }
                    }

                    while (layers.Count <= targetLevel)
                    {
                        layers.Add(new List<(DeferredDim, double, double, double, double)>());
                    }
                    break;
                }

                if (isHorizontal && dim.LooseChainId != 0)
                {
                    PromoteLooseSideLevel(layers, targetLevel);
                    looseChainLevels[dim.LooseChainId] = targetLevel;
                    looseSideLevel = targetLevel;
                }

                layers[targetLevel].Add((dim, textBox.A, textBox.B, arrow.A, arrow.B));
            }

            AlignDimensionsBySharedExtensionLines(layers, side, outline, textHeight, gap, firstOffset, perLevelSpacing, isHorizontal);

            var placedDims = new List<PlacedDim>();

            for (int level = 0; level < layers.Count; level++)
            {
                double offset = firstOffset + level * perLevelSpacing;

                foreach (var item in layers[level])
                {
                    var dim = item.Dim;
                    var dimLinePoint = GetDimLinePoint(dim, side, outline, offset);

                    placedDims.Add(new PlacedDim
                    {
                        Dim = dim,
                        Side = side,
                        DimLinePoint = dimLinePoint,
                        TextBounds = ComputePlacedTextBounds(dim, dimLinePoint, isHorizontal, textHeight),
                        UsesLocalBoundary = TryGetLocalDimLineCoordinate(dim, side, outline, offset, out _)
                    });
                }
            }

            if (_diagnosticsEnabled)
            {
                AssignDebugIndexes(placedDims);
            }

            AdjustVerticalHoleLocationTextPositions(placedDims, textHeight, gap);
            foreach (var placed in placedDims)
            {
                _linearDimTextObstacles.Add(placed.TextBounds);
            }

            foreach (var placed in placedDims)
            {
                AddRotatedDimension(
                    placed.Dim.Rotation,
                    placed.Dim.XLine1,
                    placed.Dim.XLine2,
                    placed.DimLinePoint,
                    placed.Dim.OverrideText,
                    useSegmentedExtensionLines: false,
                    placed.HasCustomTextPosition,
                    placed.TextPosition);

                if (_diagnosticsEnabled)
                {
                    AddDimensionDebugLabel(placed, isHorizontal, textHeight);
                }
            }
        }
    }
}
