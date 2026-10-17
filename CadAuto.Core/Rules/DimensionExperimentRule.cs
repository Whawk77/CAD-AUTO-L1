namespace CadAuto.Core.Rules;

/// <summary>
/// Research-only, single-rule ablations. None preserves the existing product behavior.
/// Not a flags enum: combinations are deliberately unsupported. Safety validators remain active.
/// </summary>
public enum DimensionExperimentRule
{
    None = 0,
    NonPinNearestDatum,
    PinPairGrouping,
    FunctionalHoleAssignment,
    SelectedCenterlineAttachment,
    MirroredDeduplication,
    MeasuredDeduplication,
    OutlineOverallPartition,
    LocalEnvelopeSuppression,
    BoundaryInformationCompletion,
    SharedExtensionAlignment,
    ParallelDatumCompaction
}
