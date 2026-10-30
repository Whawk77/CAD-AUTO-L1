using System;
using CadAuto.Core.Geometry;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        private static void ExtensionLengthLimitKeepsBoundaryAndLongMeasuredValues()
        {
            foreach (double angle in new[] { 0.0, Math.PI / 2, Math.PI, Math.PI * 1.5, 0.37 })
            foreach (double length in new[] { 1999.9, 2000.0, 2000.01 })
            {
                Func<double, double, Point2D> map = (x, y) => new Point2D(
                    -16460 + x * Math.Cos(angle) - y * Math.Sin(angle),
                    -22414 + x * Math.Sin(angle) + y * Math.Cos(angle));
                bool rejected = DimensionExtensionLineRules.ExceedsLengthLimit(
                    map(0, 0), map(5000, 0), map(2500, length), angle, out double actual);
                Assert(rejected == (length > 2000), "limit must follow extension length, not the 5000 mm measured value");
                Assert(Math.Abs(actual - length) < 1E-8, "rotation and translation must preserve perpendicular length");
            }
        }

        private static void ExtensionLengthLimitChecksEachEndAfterFinalPlacement()
        {
            var first = new Point2D(0, 0);
            var second = new Point2D(30, 1500);
            Assert(!DimensionExtensionLineRules.ExceedsLengthLimit(first, second, new Point2D(15, 1000), 0, out _),
                "an initial short placement must remain valid");
            Assert(DimensionExtensionLineRules.ExceedsLengthLimit(first, second, new Point2D(15, 2001), 0, out _),
                "final outward movement must reject an overlong first extension");
            Assert(DimensionExtensionLineRules.ExceedsLengthLimit(second, first, new Point2D(15, 2001), 0, out _),
                "an overlong second extension must also reject the whole dimension");
            Assert(DimensionExtensionLineRules.ExceedsLengthLimit(first, second, new Point2D(15, -2001), 0, out _),
                "the opposite placement side must use absolute length");
        }

        private static void ExtensionLengthLimitRejectsNonFiniteGeometry()
        {
            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var valid = new Point2D(0, 0);
                Assert(DimensionExtensionLineRules.ExceedsLengthLimit(new Point2D(invalid, 0), valid, valid, 0, out _),
                    "invalid first attachment must not be rendered");
                Assert(DimensionExtensionLineRules.ExceedsLengthLimit(valid, new Point2D(0, invalid), valid, 0, out _),
                    "invalid second attachment must not be rendered");
                Assert(DimensionExtensionLineRules.ExceedsLengthLimit(valid, valid, new Point2D(0, invalid), 0, out _),
                    "invalid final placement must not be rendered");
                Assert(DimensionExtensionLineRules.ExceedsLengthLimit(valid, valid, valid, invalid, out _),
                    "invalid rotation must not be rendered");
            }
        }

        private static void ExtensionLengthLimitSuppressionPreservesOtherCandidates()
        {
            var report = new DimensionDiagnosticReport();
            var rejected = new DimensionCandidateDiagnostic { Id = 1, Value = 5000, IsSelected = true, SourceFeatureId = "PG1" };
            var retained = new DimensionCandidateDiagnostic { Id = 2, Value = 5000, IsSelected = true, SourceFeatureId = "PG2" };
            report.DimensionCandidates.Add(rejected);
            report.DimensionCandidates.Add(retained);
            report.FinalDimensions.Add(rejected);
            report.FinalDimensions.Add(retained);
            Assert(DimensionExtensionLineRules.ExceedsLengthLimit(new Point2D(0, 0), new Point2D(5000, 0),
                new Point2D(2500, 2001), 0, out _), "fixture must exceed the extension limit");
            report.RecordRenderSuppressed(rejected.Id, "ExtensionLineLengthLimit");
            Assert(rejected.IsSuppressed && !rejected.IsSelected && rejected.DecisionStatus == "RenderSuppressed",
                "diagnostics must record why the dimension was not emitted");
            Assert(report.FinalDimensions.Count == 1 && report.FinalDimensions[0] == retained && retained.IsSelected && !retained.IsSuppressed,
                "another group with the same dimension value must remain selected");
        }
    }
}
