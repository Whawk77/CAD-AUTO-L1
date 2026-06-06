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
    }
}
