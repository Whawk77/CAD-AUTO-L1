using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoFixtureDim
{
    public sealed class DimensionRuleConfig
    {
        public Dictionary<double, string> HoleFitTolerance { get; } = new Dictionary<double, string>();
        public Dictionary<double, string> CenterDistanceTolerance { get; } = new Dictionary<double, string>();
        public Dictionary<double, string> ThreadMinorDiameterCallout { get; } = new Dictionary<double, string>();

        public double GeometryTolerance { get; set; }
        public double TextHeight { get; set; }
        public double ArrowSize { get; set; }
        public double FirstDimOffset { get; set; }
        public double DimTextClearance { get; set; }
        public double LeaderOffset { get; set; }
        public double ThreadArcAngleToleranceDegrees { get; set; }
        public double ThreadMinorDiameterTolerance { get; set; }
        public string DatumHoleLocationToleranceText { get; set; }
        public bool DatumHoleLocationDefaultUseTolerance { get; set; }
        public string PinCenterDistanceToleranceText { get; set; }
        public string PinGroupDistanceToleranceText { get; set; }
        public string PinHoleFitToleranceText { get; set; }

        public static DimensionRuleConfig CreateDefault()
        {
            var config = new DimensionRuleConfig
            {
                GeometryTolerance = 0.001,
                TextHeight = 2.5,
                ArrowSize = 2.5,
                FirstDimOffset = 10.0,
                DimTextClearance = 3.0,
                LeaderOffset = 12.0,
                ThreadArcAngleToleranceDegrees = 10.0,
                ThreadMinorDiameterTolerance = 0.25,
                DatumHoleLocationToleranceText = "<>\u00B10.05",
                DatumHoleLocationDefaultUseTolerance = false,
                PinCenterDistanceToleranceText = "\u00B10.02",
                PinGroupDistanceToleranceText = "\u00B10.05",
                PinHoleFitToleranceText = "H7"
            };

            config.CenterDistanceTolerance[30.0] = "\u00B10.02";
            config.ThreadMinorDiameterCallout[4.917] = "M6";
            config.ThreadMinorDiameterCallout[5.0] = "M6";
            config.ThreadMinorDiameterCallout[6.647] = "M8";
            config.ThreadMinorDiameterCallout[6.8] = "M8";
            config.ThreadMinorDiameterCallout[8.376] = "M10";
            config.ThreadMinorDiameterCallout[8.5] = "M10";
            config.ThreadMinorDiameterCallout[10.106] = "M12";
            config.ThreadMinorDiameterCallout[10.2] = "M12";
            config.ThreadMinorDiameterCallout[13.835] = "M16";
            config.ThreadMinorDiameterCallout[14.0] = "M16";
            return config;
        }

        public string GetHoleFitTolerance(double diameter)
        {
            return TryFindByTolerance(HoleFitTolerance, diameter, out var value) ? value : string.Empty;
        }

        public string GetCenterDistanceTolerance(double distance)
        {
            return TryFindByTolerance(CenterDistanceTolerance, distance, out var value) ? value : string.Empty;
        }

        public string FormatHoleCallout(double diameter, int count)
        {
            return FormatHoleCallout(diameter, count, GetHoleFitTolerance(diameter));
        }

        public string FormatHoleCallout(double diameter, int count, string fitTolerance)
        {
            var diameterText = "%%c" + FormatNumber(diameter) + (fitTolerance ?? string.Empty);
            return count > 1 ? count.ToString(CultureInfo.InvariantCulture) + "-" + diameterText : diameterText;
        }

        public string FormatCenterDistanceOverride(double distance)
        {
            var tolerance = GetCenterDistanceTolerance(distance);
            return string.IsNullOrEmpty(tolerance) ? string.Empty : FormatNumber(distance) + tolerance;
        }

        public string FormatPinCenterDistanceOverride(double distance)
        {
            return FormatNumber(distance) + (PinCenterDistanceToleranceText ?? string.Empty);
        }

        public string FormatPinGroupDistanceOverride(double distance)
        {
            return FormatNumber(distance) + (PinGroupDistanceToleranceText ?? string.Empty);
        }

        public string FormatThreadCallout(double diameter)
        {
            return "M" + FormatNumber(diameter);
        }

        public string GetThreadCalloutForMinorDiameter(double diameter)
        {
            foreach (var pair in ThreadMinorDiameterCallout)
            {
                if (Math.Abs(pair.Key - diameter) <= ThreadMinorDiameterTolerance)
                {
                    return pair.Value;
                }
            }

            return string.Empty;
        }

        public string FormatThreadCallout(double diameter, int count)
        {
            var text = FormatThreadCallout(diameter);
            return count > 1 ? count.ToString(CultureInfo.InvariantCulture) + "-" + text : text;
        }

        public string FormatThreadCallout(double diameter, int count, string explicitCallout)
        {
            var text = string.IsNullOrEmpty(explicitCallout) ? FormatThreadCallout(diameter) : explicitCallout;
            return count > 1 ? count.ToString(CultureInfo.InvariantCulture) + "-" + text : text;
        }

        public string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private bool TryFindByTolerance(Dictionary<double, string> rules, double actual, out string value)
        {
            foreach (var pair in rules)
            {
                if (Math.Abs(pair.Key - actual) <= GeometryTolerance)
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }
    }
}
