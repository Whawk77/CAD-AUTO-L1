using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private void SuppressMirroredHorizontalDuplicates()
        {
            for (int i = _topDims.Count - 1; i >= 0; i--)
            {
                var top = _topDims[i];
                if (!CanSuppressMirroredHorizontalDimension(top))
                {
                    continue;
                }

                if (_bottomDims.Any(bottom =>
                    CanSuppressMirroredHorizontalDimension(bottom)
                        && IsDuplicate(top, bottom, isHorizontal: true)))
                {
                    _topDims.RemoveAt(i);
                }
            }

            SuppressMirroredHoleRelatedDuplicates(_bottomDims, _topDims, isHorizontal: true);
        }

        private static bool CanSuppressMirroredHorizontalDimension(DeferredDim dim)
        {
            return dim.DimType == DimensionType.Normal && !dim.ForceOuterLevel;
        }

        private void SuppressMirroredVerticalDuplicates()
        {
            for (int i = _rightDims.Count - 1; i >= 0; i--)
            {
                var right = _rightDims[i];
                if (!CanSuppressMirroredVerticalDimension(right))
                {
                    continue;
                }

                if (_leftDims.Any(left =>
                    CanSuppressMirroredVerticalDimension(left)
                        && IsDuplicate(right, left, isHorizontal: false)))
                {
                    _rightDims.RemoveAt(i);
                }
            }

            SuppressMirroredHoleRelatedDuplicates(_leftDims, _rightDims, isHorizontal: false);
        }

        private static bool CanSuppressMirroredVerticalDimension(DeferredDim dim)
        {
            return dim.DimType == DimensionType.Normal && !dim.ForceOuterLevel;
        }

        private void SuppressMirroredHoleRelatedDuplicates(List<DeferredDim> primaryDims, List<DeferredDim> secondaryDims, bool isHorizontal)
        {
            for (int i = secondaryDims.Count - 1; i >= 0; i--)
            {
                var secondary = secondaryDims[i];
                for (int j = primaryDims.Count - 1; j >= 0; j--)
                {
                    var primary = primaryDims[j];
                    if (!CanSuppressMirroredHoleRelatedDimension(primary, secondary)
                        || !IsSameMeasuredDimension(primary, secondary, isHorizontal))
                    {
                        continue;
                    }

                    if (CompareDuplicatePreference(secondary, primary) > 0)
                    {
                        primaryDims.RemoveAt(j);
                    }
                    else
                    {
                        secondaryDims.RemoveAt(i);
                    }

                    break;
                }
            }
        }

        private static bool CanSuppressMirroredHoleRelatedDimension(DeferredDim a, DeferredDim b)
        {
            return !a.ForceOuterLevel
                && !b.ForceOuterLevel
                && (a.DimType == DimensionType.HoleLocation || b.DimType == DimensionType.HoleLocation);
        }

        private void SuppressDuplicateMeasuredDimensions(List<DeferredDim> dims, bool isHorizontal)
        {
            for (int i = 0; i < dims.Count; i++)
            {
                var bestIndex = i;
                for (int j = i + 1; j < dims.Count; j++)
                {
                    if (!IsSameMeasuredDimension(dims[i], dims[j], isHorizontal))
                    {
                        continue;
                    }

                    if (CompareDuplicatePreference(dims[j], dims[bestIndex]) > 0)
                    {
                        bestIndex = j;
                    }
                }

                if (bestIndex != i)
                {
                    var best = dims[bestIndex];
                    dims[bestIndex] = dims[i];
                    dims[i] = best;
                }

                for (int j = dims.Count - 1; j > i; j--)
                {
                    if (IsSameMeasuredDimension(dims[i], dims[j], isHorizontal))
                    {
                        dims.RemoveAt(j);
                    }
                }
            }
        }

        private bool IsSameMeasuredDimension(DeferredDim a, DeferredDim b, bool isHorizontal)
        {
            var aArrow = ComputeArrowInterval(a, isHorizontal);
            var bArrow = ComputeArrowInterval(b, isHorizontal);
            if (Math.Abs(aArrow.A - bArrow.A) > _config.GeometryTolerance)
            {
                return false;
            }

            if (Math.Abs(aArrow.B - bArrow.B) > _config.GeometryTolerance)
            {
                return false;
            }

            return Math.Abs(a.Span - b.Span) <= _config.GeometryTolerance;
        }

        private bool IsDuplicate(DeferredDim a, DeferredDim b, bool isHorizontal)
        {
            double a1, a2, b1, b2;
            if (isHorizontal)
            {
                a1 = Math.Min(a.XLine1.X, a.XLine2.X);
                a2 = Math.Max(a.XLine1.X, a.XLine2.X);
                b1 = Math.Min(b.XLine1.X, b.XLine2.X);
                b2 = Math.Max(b.XLine1.X, b.XLine2.X);
            }
            else
            {
                a1 = Math.Min(a.XLine1.Y, a.XLine2.Y);
                a2 = Math.Max(a.XLine1.Y, a.XLine2.Y);
                b1 = Math.Min(b.XLine1.Y, b.XLine2.Y);
                b2 = Math.Max(b.XLine1.Y, b.XLine2.Y);
            }

            if (Math.Abs(a1 - b1) > _config.GeometryTolerance) return false;
            if (Math.Abs(a2 - b2) > _config.GeometryTolerance) return false;
            var textA = a.OverrideText ?? string.Empty;
            var textB = b.OverrideText ?? string.Empty;
            return string.Equals(textA, textB, StringComparison.Ordinal);
        }

        private int CompareDuplicatePreference(DeferredDim a, DeferredDim b)
        {
            var toleranceA = TryExtractTolerance(a.OverrideText);
            var toleranceB = TryExtractTolerance(b.OverrideText);
            if (toleranceA.HasValue && !toleranceB.HasValue)
            {
                return 1;
            }

            if (!toleranceA.HasValue && toleranceB.HasValue)
            {
                return -1;
            }

            if (toleranceA.HasValue && toleranceB.HasValue)
            {
                var toleranceCompare = toleranceB.Value.CompareTo(toleranceA.Value);
                if (toleranceCompare != 0)
                {
                    return toleranceCompare;
                }
            }

            var typeCompare = GetDimensionPreferenceRank(b).CompareTo(GetDimensionPreferenceRank(a));
            if (typeCompare != 0)
            {
                return typeCompare;
            }

            if (a.ForceOuterLevel != b.ForceOuterLevel)
            {
                return a.ForceOuterLevel ? 1 : -1;
            }

            var hasTextA = !string.IsNullOrWhiteSpace(a.OverrideText);
            var hasTextB = !string.IsNullOrWhiteSpace(b.OverrideText);
            if (hasTextA != hasTextB)
            {
                return hasTextA ? 1 : -1;
            }

            return 0;
        }

        private static int GetDimensionPreferenceRank(DeferredDim dim)
        {
            switch (dim.DimType)
            {
                case DimensionType.PinDistance:
                    return 0;
                case DimensionType.PinGroupDistance:
                    return 1;
                case DimensionType.DatumHoleLocationX:
                case DimensionType.DatumHoleLocationY:
                    return 2;
                case DimensionType.OverallWidth:
                case DimensionType.OverallHeight:
                    return 3;
                case DimensionType.HoleLocation:
                case DimensionType.Normal:
                    return 4;
                default:
                    return 5;
            }
        }

        private static double? TryExtractTolerance(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var index = text.IndexOf('\u00B1');
            if (index < 0)
            {
                index = text.IndexOf('\u5364');
            }

            if (index < 0 || index >= text.Length - 1)
            {
                return null;
            }

            var start = index + 1;
            while (start < text.Length && char.IsWhiteSpace(text[start]))
            {
                start++;
            }

            var end = start;
            while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.'))
            {
                end++;
            }

            if (end <= start)
            {
                return null;
            }

            double value;
            if (double.TryParse(
                text.Substring(start, end - start),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
            {
                return value;
            }

            return null;
        }
    }
}
