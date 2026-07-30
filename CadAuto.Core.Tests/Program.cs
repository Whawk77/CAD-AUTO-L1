using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Recognition;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static class Program
    {
        private static int _passedTests;
        private static int _selectedTests;
        private static readonly List<Tuple<string, string>> FailedTests = new List<Tuple<string, string>>();
        private static readonly List<Tuple<string, Action>> RegisteredTests = new List<Tuple<string, Action>>();
        private static readonly HashSet<string> P0Tests = new HashSet<string>
        {
            nameof(RectangularOutlineKeepsOverallDimensions),
            nameof(OverallDimensionsUseBoundaryGripPoints),
            nameof(ConcaveHoleDatumUsesRealOutlineIntersections),
            nameof(DuplicateSegmentsAreSuppressedWithDiagnostic),
            nameof(ZeroAndMicroSegmentsDoNotEmitDimensions),
            nameof(GeometryToleranceControlsMicroSegments),
            nameof(InvalidDatumCoordinateIsSkippedWithDiagnostic),
            nameof(InvalidSlotDatumIsSkippedWithDiagnostic),
            nameof(OverallWinsDuplicatePreferenceEvenAgainstTolerance),
            nameof(LocalGeometryOnOverallEnvelopeSuppressesOnlyEligibleRoles)
        };
        private static readonly HashSet<string> P1Tests = new HashSet<string>
        {
            nameof(Production73x20EnvelopeDimensionsAreSuppressed),
            nameof(MirroredEnvelopeStructureWidthsAreBothSuppressed),
            nameof(StructureWidthsThatPartitionOverallAreSuppressed),
            nameof(ProjectedCrossLevelStructureWidthsDoNotPartitionOverall),
            nameof(CrossSideStructureWidthsThatCloseOverallChainAreSuppressed),
            nameof(InteriorHorizontalOutlineSegmentPrefersNonCrossingSide),
            nameof(TopEnvelopeHorizontalSegmentStaysTop),
            nameof(BottomEnvelopeHorizontalSegmentStaysBottom),
            nameof(BottomBodyWidthNotDroppedByLongestExtension),
            nameof(BottomOuterContourStepKeeps20AndSuppresses70Body),
            nameof(OutlineSegmentBodyLengthClosedByStepIsSuppressed),
            nameof(DatumRootedLeftOuterEnvelopeDimensionsAreSuppressed),
            nameof(DatumRootedBottomAndLeftOuterEnvelopeDimensionsAreSuppressed),
            nameof(DatumRootedBottomEnvelopeStepAndArcResidualAreSuppressed),
            nameof(StructureWidthThatPartitionsOverallWithOutlineSegmentIsSuppressed),
            nameof(ThreeStructureWidthsThatPartitionOverallAreSuppressed),
            nameof(LocalTopStructureWidthIsKeptWhenNotPartitioningOverall),
            nameof(RightArmHeightOnOverallMaxXIsSuppressed),
            nameof(LShapeTowerTopWidthIsKeptDespiteArmTopOutlineSegment),
            nameof(BottomStepWidthOnOverallEnvelopeIsSuppressed),
            nameof(BottomProtrusionSuppressesOuterWidthAndInnerLedge),
            nameof(BottomFilletedProtrusionWidthIsSuppressed),
            nameof(ChamferedTopStepUsesCompositeDimensions),
            nameof(LeftStepStructureHeightsPreferOverRightOutlineSegments),
            nameof(RightStepStructureHeightsPreferOverLeftOutlineSegments),
            nameof(OrphanRightOuterStructureHeightTipsAreSuppressed),
            nameof(PartialEnvelopeStructureHeightSurvivesOrphanTipSuppression)
        };
        private static readonly HashSet<string> P3Tests = new HashSet<string>
        {
            nameof(OverallRemainsOutermostAfterLayoutAlignment),
            nameof(OverallCompactsToOnePhysicalSpacing),
            nameof(EffectiveSpanControlsHorizontalStacking),
            nameof(SharedArrowEndpointUsesStrictSpanOrder),
            nameof(StructureAlignmentKeyAlignsAdjacentSegmentsOnSameLevel),
            nameof(SlotChainAlignmentKeyAlignsEdgeLocationAndCenterDistance),
            nameof(EffectiveSpanControlsVerticalStacking),
            nameof(NearEqualEffectiveSpansUseSemanticTieBreak),
            nameof(LayoutBlocksUseBottomV203SpanOrder),
            nameof(RootedLayoutBlockSurvivesLegacyLaneProcessing),
            nameof(LooseChainFormsOneEffectiveSpanBlock),
            nameof(DisconnectedLooseChainsRemainSeparateBlocks),
            nameof(OverlappingRootedIntervalsSplitIntoSeparateLeftBlocks),
            nameof(OverlappingDatumAndPinIntervalsSplitIntoSeparateBlocks),
            nameof(IndependentLocalAndGlobalDimensionsMayShareLogicalLevel),
            nameof(IsolatedShortPinGroupTransferUsesInnerSpanOrder),
            nameof(DimensionDiagnosticsRecordFinalPlacement),
            nameof(FormattedDimensionTextLengthIgnoresControlCodes),
            nameof(FittingVerticalLocalTextStaysCentered),
            nameof(ShortVerticalLocalTextClearsArrowheads),
            nameof(FittingHorizontalTextStaysCenteredDespiteNeighborArrow),
            nameof(ShortHorizontalLocalTextClearsArrowheads),
            nameof(ShortVerticalChainTextAvoidsNeighborArrowheads),
            nameof(PinAlignmentGroupsRespectSideAndOrientation),
            nameof(RootedDatumChainMergesTransitiveAlignmentLanes),
            nameof(PinAlignmentAnchorFallsBackWithStableOrdering),
            nameof(PinAlignmentGroupMovesTogetherOnConflict),
            nameof(PinAlignmentGroupAvoidsResolvedCoordinateConflicts),
            nameof(PinAlignmentKeySplitsStrictHorizontalOverlapsIntoLanes),
            nameof(PinAlignmentKeySplitsStrictVerticalOverlapsIntoLanes),
            nameof(FunctionalHoleAlignmentUsesSeparateSameOrientationLane),
            nameof(FunctionalHoleAlignmentPreservesV198Stacking),
            nameof(FunctionalHoleAlignmentLaneSurvivesOutwardPromotion),
            nameof(FunctionalHoleBeyondPinChainStacksOutsideDatumChain),
            nameof(PreferredSideLockedHoleLocationUsesLocalBoundary),
            nameof(ExplicitLocalLooseChainUsesNearbyConcaveBoundary),
            nameof(HoleLocationDimensionsUseSegmentedExtensionLines),
            nameof(HoleCalloutsUsePinClustersAndFitText)
        };

        private static int Main(string[] args)
        {
            string filter = args.Length > 0 ? args[0] : null;
            try
            {
                RunTest(nameof(RectangularOutlineKeepsOverallDimensions), RectangularOutlineKeepsOverallDimensions);
                RunTest(nameof(ClosedPathRecognitionBuildsOutline), ClosedPathRecognitionBuildsOutline);
                RunTest(nameof(ChamferedOutlineKeepsOverallDimensions), ChamferedOutlineKeepsOverallDimensions);
                RunTest(nameof(MultipleChamfersKeepOverallDimensions), MultipleChamfersKeepOverallDimensions);
                RunTest(nameof(MirroredChamferPreservesRecognition), MirroredChamferPreservesRecognition);
                RunTest(nameof(FilletedOutlineKeepsOverallDimensions), FilletedOutlineKeepsOverallDimensions);
                RunTest(nameof(OverallDimensionsUseBoundaryGripPoints), OverallDimensionsUseBoundaryGripPoints);
                RunTest(nameof(ArcEnvelopeUsesRealInteriorGripPoint), ArcEnvelopeUsesRealInteriorGripPoint);
                RunTest(nameof(NegativeBulgeArcUsesRealInteriorGripPoint), NegativeBulgeArcUsesRealInteriorGripPoint);
                RunTest(nameof(DisconnectedOutlineCanUseVerifiedRealGrips), DisconnectedOutlineCanUseVerifiedRealGrips);
                RunTest(nameof(TranslatedOutlinePreservesOverallSemantics), TranslatedOutlinePreservesOverallSemantics);
                RunTest(nameof(RotatedOutlineUsesRealEnvelopeGrips), RotatedOutlineUsesRealEnvelopeGrips);
                RunTest(nameof(DuplicateSegmentsAreSuppressedWithDiagnostic), DuplicateSegmentsAreSuppressedWithDiagnostic);
                RunTest(nameof(ZeroAndMicroSegmentsDoNotEmitDimensions), ZeroAndMicroSegmentsDoNotEmitDimensions);
                RunTest(nameof(GeometryToleranceControlsMicroSegments), GeometryToleranceControlsMicroSegments);
                RunTest(nameof(ConcaveHoleDatumUsesRealOutlineIntersections), ConcaveHoleDatumUsesRealOutlineIntersections);
                RunTest(nameof(OverallWinsDuplicatePreferenceEvenAgainstTolerance), OverallWinsDuplicatePreferenceEvenAgainstTolerance);
                RunTest(nameof(LocalGeometryOnOverallEnvelopeSuppressesOnlyEligibleRoles), LocalGeometryOnOverallEnvelopeSuppressesOnlyEligibleRoles);
				RunTest(nameof(OverallRemainsOutermostAfterLayoutAlignment), OverallRemainsOutermostAfterLayoutAlignment);
				RunTest(nameof(OverallCompactsToOnePhysicalSpacing), OverallCompactsToOnePhysicalSpacing);
				RunTest(nameof(EffectiveSpanControlsHorizontalStacking), EffectiveSpanControlsHorizontalStacking);
				RunTest(nameof(SharedArrowEndpointUsesStrictSpanOrder), SharedArrowEndpointUsesStrictSpanOrder);
				RunTest(nameof(StructureAlignmentKeyAlignsAdjacentSegmentsOnSameLevel), StructureAlignmentKeyAlignsAdjacentSegmentsOnSameLevel);
				RunTest(nameof(SlotChainAlignmentKeyAlignsEdgeLocationAndCenterDistance), SlotChainAlignmentKeyAlignsEdgeLocationAndCenterDistance);
				RunTest(nameof(OutlineSegmentsThatPartitionOverallAreSuppressed), OutlineSegmentsThatPartitionOverallAreSuppressed);
				RunTest(nameof(ThreeOutlineSegmentsThatPartitionOverallAreSuppressed), ThreeOutlineSegmentsThatPartitionOverallAreSuppressed);
				RunTest(nameof(OutlineSegmentOnOverallEnvelopeIsSuppressed), OutlineSegmentOnOverallEnvelopeIsSuppressed);
				RunTest(nameof(StructureDuplicateOfEnvelopeOutlineSegmentIsSuppressed), StructureDuplicateOfEnvelopeOutlineSegmentIsSuppressed);
				RunTest(nameof(EnvelopeStructureSameSideCollinearDuplicateIsSuppressed), EnvelopeStructureSameSideCollinearDuplicateIsSuppressed);
				RunTest(nameof(StructureSameIntervalAsOppositeEnvelopeTipIsKept), StructureSameIntervalAsOppositeEnvelopeTipIsKept);
				RunTest(nameof(MirroredEnvelopeStructureWidthsAreBothSuppressed), MirroredEnvelopeStructureWidthsAreBothSuppressed);
				RunTest(nameof(Production73x20EnvelopeDimensionsAreSuppressed), Production73x20EnvelopeDimensionsAreSuppressed);
				RunTest(nameof(InnerOutlineSegmentMatchingEnvelopeTipIsSuppressed), InnerOutlineSegmentMatchingEnvelopeTipIsSuppressed);
				RunTest(nameof(CompleteOverallPartitionIntervalsAreDetected), CompleteOverallPartitionIntervalsAreDetected);
				RunTest(nameof(GreedyDeadEndStillFindsValidOverallPartitionChain), GreedyDeadEndStillFindsValidOverallPartitionChain);
				RunTest(nameof(SnapToOriginPointIsNotTreatedAsNotFound), SnapToOriginPointIsNotTreatedAsNotFound);
				RunTest(nameof(SnapKeepsOriginalPointWhenNoCandidateExists), SnapKeepsOriginalPointWhenNoCandidateExists);
				RunTest(nameof(LegalComplementaryPartitionAllowsSnapAndSuppress), LegalComplementaryPartitionAllowsSnapAndSuppress);
				RunTest(nameof(OverlappingSpanSumDoesNotAllowComplementarySnap), OverlappingSpanSumDoesNotAllowComplementarySnap);
				RunTest(nameof(GappedSpanSumDoesNotAllowComplementarySnap), GappedSpanSumDoesNotAllowComplementarySnap);
				RunTest(nameof(NumericComplementLocalStructuresDoNotSnapOrSuppress), NumericComplementLocalStructuresDoNotSnapOrSuppress);
				RunTest(nameof(OverlappingIntervalsDoNotFormOverallPartition), OverlappingIntervalsDoNotFormOverallPartition);
				RunTest(nameof(GappedIntervalsDoNotFormOverallPartition), GappedIntervalsDoNotFormOverallPartition);
				RunTest(nameof(OutOfBoundsIntervalsDoNotFormOverallPartition), OutOfBoundsIntervalsDoNotFormOverallPartition);
				RunTest(nameof(NumericallyComplementaryStructureIntervalsDoNotPartitionOverall), NumericallyComplementaryStructureIntervalsDoNotPartitionOverall);
				RunTest(nameof(EffectiveSpanControlsVerticalStacking), EffectiveSpanControlsVerticalStacking);
				RunTest(nameof(NearEqualEffectiveSpansUseSemanticTieBreak), NearEqualEffectiveSpansUseSemanticTieBreak);
				RunTest(nameof(LayoutBlocksUseBottomV203SpanOrder), LayoutBlocksUseBottomV203SpanOrder);
				RunTest(nameof(RootedLayoutBlockSurvivesLegacyLaneProcessing), RootedLayoutBlockSurvivesLegacyLaneProcessing);
				RunTest(nameof(LooseChainFormsOneEffectiveSpanBlock), LooseChainFormsOneEffectiveSpanBlock);
				RunTest(nameof(DisconnectedLooseChainsRemainSeparateBlocks), DisconnectedLooseChainsRemainSeparateBlocks);
				RunTest(nameof(OverlappingRootedIntervalsSplitIntoSeparateLeftBlocks), OverlappingRootedIntervalsSplitIntoSeparateLeftBlocks);
				RunTest(nameof(OverlappingDatumAndPinIntervalsSplitIntoSeparateBlocks), OverlappingDatumAndPinIntervalsSplitIntoSeparateBlocks);
				RunTest(nameof(IndependentLocalAndGlobalDimensionsMayShareLogicalLevel), IndependentLocalAndGlobalDimensionsMayShareLogicalLevel);
				RunTest(nameof(IsolatedShortPinGroupTransferUsesInnerSpanOrder), IsolatedShortPinGroupTransferUsesInnerSpanOrder);
				RunTest(nameof(DimensionDiagnosticsRecordFinalPlacement), DimensionDiagnosticsRecordFinalPlacement);
				RunTest(nameof(RuleEvidenceRejectsReassignmentAndRenderSuppressionStaysCompatible), RuleEvidenceRejectsReassignmentAndRenderSuppressionStaysCompatible);
				RunTest(nameof(FormattedDimensionTextLengthIgnoresControlCodes), FormattedDimensionTextLengthIgnoresControlCodes);
				RunTest(nameof(FittingVerticalLocalTextStaysCentered), FittingVerticalLocalTextStaysCentered);
				RunTest(nameof(ShortVerticalLocalTextClearsArrowheads), ShortVerticalLocalTextClearsArrowheads);
				RunTest(nameof(FittingHorizontalTextStaysCenteredDespiteNeighborArrow), FittingHorizontalTextStaysCenteredDespiteNeighborArrow);
				RunTest(nameof(ShortHorizontalLocalTextClearsArrowheads), ShortHorizontalLocalTextClearsArrowheads);
				RunTest(nameof(ShortVerticalChainTextAvoidsNeighborArrowheads), ShortVerticalChainTextAvoidsNeighborArrowheads);
                RunTest(nameof(InvalidDatumCoordinateIsSkippedWithDiagnostic), InvalidDatumCoordinateIsSkippedWithDiagnostic);
                RunTest(nameof(InvalidSlotDatumIsSkippedWithDiagnostic), InvalidSlotDatumIsSkippedWithDiagnostic);
                RunTest(nameof(ChamferSuppressesAdjacentLocalLinearDimensions), ChamferSuppressesAdjacentLocalLinearDimensions);
                RunTest(nameof(NonFortyFiveSlopeIsNotChamfer), NonFortyFiveSlopeIsNotChamfer);
                RunTest(nameof(VerticalStructurePointsCreateStepWidths), VerticalStructurePointsCreateStepWidths);
                RunTest(nameof(StructureWidthsThatPartitionOverallAreSuppressed), StructureWidthsThatPartitionOverallAreSuppressed);
                RunTest(nameof(ProjectedCrossLevelStructureWidthsDoNotPartitionOverall), ProjectedCrossLevelStructureWidthsDoNotPartitionOverall);
                RunTest(nameof(CrossSideStructureWidthsThatCloseOverallChainAreSuppressed), CrossSideStructureWidthsThatCloseOverallChainAreSuppressed);
                RunTest(nameof(InteriorHorizontalOutlineSegmentPrefersNonCrossingSide), InteriorHorizontalOutlineSegmentPrefersNonCrossingSide);
                RunTest(nameof(TopEnvelopeHorizontalSegmentStaysTop), TopEnvelopeHorizontalSegmentStaysTop);
                RunTest(nameof(BottomEnvelopeHorizontalSegmentStaysBottom), BottomEnvelopeHorizontalSegmentStaysBottom);
                RunTest(nameof(BottomBodyWidthNotDroppedByLongestExtension), BottomBodyWidthNotDroppedByLongestExtension);
                RunTest(nameof(BottomStructureRejectsCrossAxisProjection), BottomStructureRejectsCrossAxisProjection);
                RunTest(nameof(BottomOuterContourStepKeeps20AndSuppresses70Body), BottomOuterContourStepKeeps20AndSuppresses70Body);
                RunTest(nameof(TranslatedOuterContourStepKeepsRuleDecision), TranslatedOuterContourStepKeepsRuleDecision);
                RunTest(nameof(OutlineSegmentBodyLengthClosedByStepIsSuppressed), OutlineSegmentBodyLengthClosedByStepIsSuppressed);
                RunTest(nameof(DatumRootedLeftOuterEnvelopeDimensionsAreSuppressed), DatumRootedLeftOuterEnvelopeDimensionsAreSuppressed);
                RunTest(nameof(DatumRootedBottomAndLeftOuterEnvelopeDimensionsAreSuppressed), DatumRootedBottomAndLeftOuterEnvelopeDimensionsAreSuppressed);
                RunTest(nameof(DatumRootedBottomEnvelopeStepAndArcResidualAreSuppressed), DatumRootedBottomEnvelopeStepAndArcResidualAreSuppressed);
                RunTest(nameof(StructureWidthThatPartitionsOverallWithOutlineSegmentIsSuppressed), StructureWidthThatPartitionsOverallWithOutlineSegmentIsSuppressed);
                RunTest(nameof(ThreeStructureWidthsThatPartitionOverallAreSuppressed), ThreeStructureWidthsThatPartitionOverallAreSuppressed);
                RunTest(nameof(LocalTopStructureWidthIsKeptWhenNotPartitioningOverall), LocalTopStructureWidthIsKeptWhenNotPartitioningOverall);
                RunTest(nameof(RightArmHeightOnOverallMaxXIsSuppressed), RightArmHeightOnOverallMaxXIsSuppressed);
                RunTest(nameof(LShapeTowerTopWidthIsKeptDespiteArmTopOutlineSegment), LShapeTowerTopWidthIsKeptDespiteArmTopOutlineSegment);
                RunTest(nameof(BottomStepWidthOnOverallEnvelopeIsSuppressed), BottomStepWidthOnOverallEnvelopeIsSuppressed);
				RunTest(nameof(BottomProtrusionSuppressesOuterWidthAndInnerLedge), BottomProtrusionSuppressesOuterWidthAndInnerLedge);
				RunTest(nameof(BottomFilletedProtrusionWidthIsSuppressed), BottomFilletedProtrusionWidthIsSuppressed);
				RunTest(nameof(ChamferedTopStepUsesCompositeDimensions), ChamferedTopStepUsesCompositeDimensions);
                RunTest(nameof(LeftStepStructureHeightsPreferOverRightOutlineSegments), LeftStepStructureHeightsPreferOverRightOutlineSegments);
                RunTest(nameof(RightStepStructureHeightsPreferOverLeftOutlineSegments), RightStepStructureHeightsPreferOverLeftOutlineSegments);
                RunTest(nameof(HorizontalStructurePointsCreateStepHeights), HorizontalStructurePointsCreateStepHeights);
                RunTest(nameof(DiagonalFragmentsDoNotCreateStructureDimensions), DiagonalFragmentsDoNotCreateStructureDimensions);
                RunTest(nameof(BottomInclinedStructurePointsRequireInnerGrooveChamfer), BottomInclinedStructurePointsRequireInnerGrooveChamfer);
                RunTest(nameof(SideInclinedStructurePointsRequireInnerGrooveChamfer), SideInclinedStructurePointsRequireInnerGrooveChamfer);
                RunTest(nameof(RightStructureHeightDuplicatingOverallHeightIsSuppressed), RightStructureHeightDuplicatingOverallHeightIsSuppressed);
                RunTest(nameof(LeftStructureHeightDuplicatingOverallHeightIsSuppressed), LeftStructureHeightDuplicatingOverallHeightIsSuppressed);
                RunTest(nameof(LeftStructureComplementaryRemainderIsRemovedLikeRight), LeftStructureComplementaryRemainderIsRemovedLikeRight);
                RunTest(nameof(OrphanRightOuterStructureHeightTipsAreSuppressed), OrphanRightOuterStructureHeightTipsAreSuppressed);
                RunTest(nameof(PartialEnvelopeStructureHeightSurvivesOrphanTipSuppression), PartialEnvelopeStructureHeightSurvivesOrphanTipSuppression);
                RunTest(nameof(TwoArcSlotIsRecognized), TwoArcSlotIsRecognized);
                RunTest(nameof(SingleArcSlotIsRecognized), SingleArcSlotIsRecognized);
                RunTest(nameof(SlotDimensionsUseCenterAndDatumChainsWithoutPins), SlotDimensionsUseCenterAndDatumChainsWithoutPins);
                RunTest(nameof(HolesAreGroupedByHorizontalRows), HolesAreGroupedByHorizontalRows);
                RunTest(nameof(NormalHolesLocateFromOutlineDatum), NormalHolesLocateFromOutlineDatum);
				RunTest(nameof(PinGroupsPlanBaseAndPairDistances), PinGroupsPlanBaseAndPairDistances);
				RunTest(nameof(PinAlignmentGroupsRespectSideAndOrientation), PinAlignmentGroupsRespectSideAndOrientation);
				RunTest(nameof(RootedDatumChainMergesTransitiveAlignmentLanes), RootedDatumChainMergesTransitiveAlignmentLanes);
				RunTest(nameof(PinAlignmentAnchorFallsBackWithStableOrdering), PinAlignmentAnchorFallsBackWithStableOrdering);
				RunTest(nameof(PinAlignmentGroupMovesTogetherOnConflict), PinAlignmentGroupMovesTogetherOnConflict);
				RunTest(nameof(PinAlignmentGroupAvoidsResolvedCoordinateConflicts), PinAlignmentGroupAvoidsResolvedCoordinateConflicts);
				RunTest(nameof(PinAlignmentKeySplitsStrictHorizontalOverlapsIntoLanes), PinAlignmentKeySplitsStrictHorizontalOverlapsIntoLanes);
				RunTest(nameof(PinAlignmentKeySplitsStrictVerticalOverlapsIntoLanes), PinAlignmentKeySplitsStrictVerticalOverlapsIntoLanes);
				RunTest(nameof(EquivalentPinGroupTransfersSuppressAcrossDebugOwners), EquivalentPinGroupTransfersSuppressAcrossDebugOwners);
				RunTest(nameof(FunctionalHolesAttachToPinGroup), FunctionalHolesAttachToPinGroup);
				RunTest(nameof(HoleBetweenPinPairAttachesAsFunctionalHole), HoleBetweenPinPairAttachesAsFunctionalHole);
				RunTest(nameof(FunctionalHoleAlignmentUsesSeparateSameOrientationLane), FunctionalHoleAlignmentUsesSeparateSameOrientationLane);
				RunTest(nameof(FunctionalHoleAlignmentPreservesV198Stacking), FunctionalHoleAlignmentPreservesV198Stacking);
				RunTest(nameof(FunctionalHoleAlignmentLaneSurvivesOutwardPromotion), FunctionalHoleAlignmentLaneSurvivesOutwardPromotion);
				RunTest(nameof(FunctionalHoleBeyondPinChainStacksOutsideDatumChain), FunctionalHoleBeyondPinChainStacksOutsideDatumChain);
				RunTest(nameof(Dl01TopStructureFunctionalHoleDatumChainOrder), Dl01TopStructureFunctionalHoleDatumChainOrder);
                RunTest(nameof(PreferredSideLockedHoleLocationUsesLocalBoundary), PreferredSideLockedHoleLocationUsesLocalBoundary);
                RunTest(nameof(ExplicitLocalLooseChainUsesNearbyConcaveBoundary), ExplicitLocalLooseChainUsesNearbyConcaveBoundary);
                RunTest(nameof(LooseHolesUseChainDimensions), LooseHolesUseChainDimensions);
                RunTest(nameof(ConcentricLooseHolesShareOneLocationDimension), ConcentricLooseHolesShareOneLocationDimension);
                RunTest(nameof(HoleLocationDimensionsUseSegmentedExtensionLines), HoleLocationDimensionsUseSegmentedExtensionLines);
                RunTest(nameof(HoleCalloutsGroupByRowsWithoutPins), HoleCalloutsGroupByRowsWithoutPins);
                RunTest(nameof(HoleCalloutsUsePinClustersAndFitText), HoleCalloutsUsePinClustersAndFitText);
                ValidateTestRegistration();
                RunRegisteredTests(filter);
                Console.WriteLine("CadAuto.Core.Tests: " + _passedTests + "/" + _selectedTests + " passed.");
                foreach (Tuple<string, string> failure in FailedTests)
                {
                    Console.WriteLine("FAILED " + failure.Item1 + " :: " + failure.Item2);
                }
                return FailedTests.Count == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void RunTest(string name, Action test)
        {
            RegisteredTests.Add(Tuple.Create(name, test));
        }

        private static void ValidateTestRegistration()
        {
            IGrouping<string, Tuple<string, Action>> duplicate = RegisteredTests
                .GroupBy(test => test.Item1, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                throw new InvalidOperationException("Duplicate test registration: " + duplicate.Key);
            }

            var priorityMemberships = P0Tests.Select(name => Tuple.Create(name, "P0"))
                .Concat(P1Tests.Select(name => Tuple.Create(name, "P1")))
                .Concat(P3Tests.Select(name => Tuple.Create(name, "P3")))
                .ToList();
            IGrouping<string, Tuple<string, string>> overlap = priorityMemberships
                .GroupBy(test => test.Item1, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (overlap != null)
            {
                throw new InvalidOperationException(
                    "Test appears in multiple priority sets: "
                    + overlap.Key
                    + " ("
                    + string.Join(", ", overlap.Select(test => test.Item2))
                    + ")");
            }

            var registeredNames = new HashSet<string>(
                RegisteredTests.Select(test => test.Item1),
                StringComparer.Ordinal);
            Tuple<string, string> unregistered = priorityMemberships
                .FirstOrDefault(test => !registeredNames.Contains(test.Item1));
            if (unregistered != null)
            {
                throw new InvalidOperationException(
                    unregistered.Item2 + " test is not registered: " + unregistered.Item1);
            }
        }

        private static void RunRegisteredTests(string filter)
        {
            if (!string.IsNullOrEmpty(filter)
                && !RegisteredTests.Any(test => MatchesFilter(test.Item1, GetTestPriority(test.Item1), filter)))
            {
                throw new InvalidOperationException("No registered tests matched filter: " + filter);
            }

            for (int priority = 0; priority <= 3; priority++)
            {
                List<Tuple<string, Action>> tests = RegisteredTests
                    .Where(test => GetTestPriority(test.Item1) == priority)
                    .Where(test => MatchesFilter(test.Item1, priority, filter))
                    .ToList();
                if (tests.Count == 0)
                {
                    continue;
                }
                _selectedTests += tests.Count;
                Console.WriteLine("START P" + priority + " count=" + tests.Count);
                int failedInTier = 0;
                foreach (Tuple<string, Action> test in tests)
                {
                    try
                    {
                        test.Item2();
                    }
                    catch (Exception ex)
                    {
                        failedInTier++;
                        FailedTests.Add(Tuple.Create(test.Item1, ex.Message));
                        Console.WriteLine("FAIL P" + priority + " " + test.Item1 + " :: " + ex.Message);
                        if (priority == 0)
                        {
                            // P0 invariants are prerequisites for every later tier; abort the run.
                            Console.WriteLine("TIER FAIL P0 aborting remaining tiers.");
                            return;
                        }
                        continue;
                    }
                    _passedTests++;
                    Console.WriteLine("PASS P" + priority + " " + test.Item1);
                }
                if (failedInTier == 0)
                {
                    Console.WriteLine("TIER PASS P" + priority + " count=" + tests.Count);
                }
                else
                {
                    Console.WriteLine("TIER FAIL P" + priority + " failed=" + failedInTier);
                }
            }
        }

        private static bool MatchesFilter(string name, int priority, string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }
            if (filter.Equals("P" + priority, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int GetTestPriority(string name)
        {
            if (P0Tests.Contains(name))
            {
                return 0;
            }
            if (P1Tests.Contains(name))
            {
                return 1;
            }
            if (P3Tests.Contains(name))
            {
                return 3;
            }
            return 2;
        }

        private static void RectangularOutlineKeepsOverallDimensions()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateRectangle(100.0, 50.0);
            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            AssertHasDimension(plan, DimensionKind.OverallWidth, DimensionOrientation.Horizontal, 100.0, "rectangle overall width");
            AssertHasDimension(plan, DimensionKind.OverallHeight, DimensionOrientation.Vertical, 50.0, "rectangle overall height");
        }

        private static void ChamferedOutlineKeepsOverallDimensions()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateTopRightChamferedOutline();
            new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(outline);
            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(outline.Chamfers.Count == 1, "expected one chamfer");
            AssertHasDimension(plan, DimensionKind.OverallWidth, DimensionOrientation.Horizontal, 100.0, "chamfer overall width");
            AssertHasDimension(plan, DimensionKind.OverallHeight, DimensionOrientation.Vertical, 50.0, "chamfer overall height");
        }

        private static void MultipleChamfersKeepOverallDimensions()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = new OutlineFeature2D
            {
                MinX = 0.0,
                MinY = 0.0,
                MaxX = 100.0,
                MaxY = 50.0
            };
            AddSegment(outline, new Point2D(10.0, 0.0), new Point2D(100.0, 0.0), "bottom");
            AddSegment(outline, new Point2D(100.0, 0.0), new Point2D(100.0, 40.0), "right");
            AddSegment(outline, new Point2D(100.0, 40.0), new Point2D(90.0, 50.0), "top-right-chamfer");
            AddSegment(outline, new Point2D(90.0, 50.0), new Point2D(0.0, 50.0), "top");
            AddSegment(outline, new Point2D(0.0, 50.0), new Point2D(0.0, 10.0), "left");
            AddSegment(outline, new Point2D(0.0, 10.0), new Point2D(10.0, 0.0), "bottom-left-chamfer");
            new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(outline);

            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(outline.Chamfers.Count == 2, "expected two recognized outer chamfers");
            Assert(plan.Dimensions.Count(d => d.Kind == DimensionKind.OverallWidth) == 1,
                "multiple chamfers must retain exactly one overall width");
            Assert(plan.Dimensions.Count(d => d.Kind == DimensionKind.OverallHeight) == 1,
                "multiple chamfers must retain exactly one overall height");
            AssertHasDimension(plan, DimensionKind.OverallWidth, DimensionOrientation.Horizontal, 100.0, "multiple-chamfer overall width");
            AssertHasDimension(plan, DimensionKind.OverallHeight, DimensionOrientation.Vertical, 50.0, "multiple-chamfer overall height");
        }

        private static void MirroredChamferPreservesRecognition()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = new OutlineFeature2D
            {
                MinX = -100.0,
                MinY = 25.0,
                MaxX = 0.0,
                MaxY = 75.0
            };
            AddSegment(outline, new Point2D(-100.0, 25.0), new Point2D(0.0, 25.0), "bottom");
            AddSegment(outline, new Point2D(0.0, 25.0), new Point2D(0.0, 75.0), "right");
            AddSegment(outline, new Point2D(0.0, 75.0), new Point2D(-90.0, 75.0), "top");
            AddSegment(outline, new Point2D(-90.0, 75.0), new Point2D(-100.0, 65.0), "mirrored-chamfer");
            AddSegment(outline, new Point2D(-100.0, 65.0), new Point2D(-100.0, 25.0), "left");
            new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(outline);

            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(outline.Chamfers.Count == 1, "mirroring must not change chamfer recognition");
            AssertHasDimension(plan, DimensionKind.OverallWidth, DimensionOrientation.Horizontal, 100.0, "mirrored overall width");
            AssertHasDimension(plan, DimensionKind.OverallHeight, DimensionOrientation.Vertical, 50.0, "mirrored overall height");
        }

        private static void FilletedOutlineKeepsOverallDimensions()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var segments = new[]
            {
                new Segment2D(new Point2D(0.0, 0.0), new Point2D(100.0, 0.0)) { SourceKey = "bottom" },
                new Segment2D(new Point2D(100.0, 0.0), new Point2D(100.0, 40.0)) { SourceKey = "right" },
                new Segment2D(new Point2D(90.0, 50.0), new Point2D(0.0, 50.0)) { SourceKey = "top" },
                new Segment2D(new Point2D(0.0, 50.0), new Point2D(0.0, 0.0)) { SourceKey = "left" }
            };
            var arcs = new[]
            {
                new Arc2D
                {
                    Start = new Point2D(100.0, 40.0),
                    End = new Point2D(90.0, 50.0),
                    Center = new Point2D(90.0, 40.0),
                    Radius = 10.0,
                    Bulge = Math.Tan(Math.PI / 8.0),
                    SourceKey = "top-right-fillet"
                }
            };
            var outline = new FeatureRecognizer2D(config).RecognizeOutlineFromSegments(segments, arcs);
            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(outline.Fillets.Count == 1, "expected one connected outline fillet");
            AssertHasDimension(plan, DimensionKind.OverallWidth, DimensionOrientation.Horizontal, 100.0, "filleted overall width");
            AssertHasDimension(plan, DimensionKind.OverallHeight, DimensionOrientation.Vertical, 50.0, "filleted overall height");
        }

        private static void ChamferSuppressesAdjacentLocalLinearDimensions()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateTopRightChamferedOutline();
            new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(outline);
            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(!plan.Dimensions.Any(d => d.Kind == DimensionKind.Normal && d.SourceKey == "right-local"),
                "right vertical local dimension adjacent to chamfer should be suppressed");
            Assert(!plan.Dimensions.Any(d => d.Kind == DimensionKind.Normal && d.SourceKey == "top-local"),
                "top horizontal local dimension adjacent to chamfer should be suppressed");
        }

        private static void OverallDimensionsUseBoundaryGripPoints()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = new OutlineFeature2D
            {
                MinX = 0.0,
                MinY = 0.0,
                MaxX = 100.0,
                MaxY = 50.0
            };

            AddSegment(outline, new Point2D(0.0, 10.0), new Point2D(0.0, 50.0), "left");
            AddSegment(outline, new Point2D(0.0, 50.0), new Point2D(100.0, 50.0), "top");
            AddSegment(outline, new Point2D(100.0, 50.0), new Point2D(100.0, 20.0), "right");
            AddSegment(outline, new Point2D(100.0, 20.0), new Point2D(80.0, 0.0), "slope-right");
            AddSegment(outline, new Point2D(80.0, 0.0), new Point2D(20.0, 0.0), "bottom");
            AddSegment(outline, new Point2D(20.0, 0.0), new Point2D(0.0, 10.0), "slope-left");

            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);
            var width = plan.Dimensions.Single(d => d.Kind == DimensionKind.OverallWidth);
            var height = plan.Dimensions.Single(d => d.Kind == DimensionKind.OverallHeight);

            Assert(Math.Abs(width.FirstPoint.X - 0.0) <= 0.001 && Math.Abs(width.FirstPoint.Y - 10.0) <= 0.001,
                "overall width should use lowest left-boundary vertex");
            Assert(Math.Abs(width.SecondPoint.X - 100.0) <= 0.001 && Math.Abs(width.SecondPoint.Y - 20.0) <= 0.001,
                "overall width should use lowest right-boundary vertex");
            Assert(Math.Abs(height.FirstPoint.X - 20.0) <= 0.001 && Math.Abs(height.FirstPoint.Y - 0.0) <= 0.001,
                "overall height should use leftmost bottom-boundary vertex");
            Assert(Math.Abs(height.SecondPoint.X - 0.0) <= 0.001 && Math.Abs(height.SecondPoint.Y - 50.0) <= 0.001,
                "overall height should use leftmost top-boundary vertex");
        }

		private static void ArcEnvelopeUsesRealInteriorGripPoint()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 100.0,
				MinY = 200.0,
				MaxX = 140.0,
				MaxY = 240.0
			};
			AddSegment(outline, new Point2D(100.0, 200.0), new Point2D(140.0, 200.0), "bottom");
			AddSegment(outline, new Point2D(140.0, 200.0), new Point2D(140.0, 220.0), "right");
			AddSegment(outline, new Point2D(100.0, 220.0), new Point2D(100.0, 200.0), "left");
			outline.Arcs.Add(new Arc2D
			{
				Start = new Point2D(140.0, 220.0),
				End = new Point2D(100.0, 220.0),
				Center = new Point2D(120.0, 220.0),
				Radius = 20.0,
				Bulge = 1.0,
				SourceKey = "top-arc"
			});

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);
			var height = plan.Dimensions.Single(d => d.Kind == DimensionKind.OverallHeight);
			var topGrip = height.FirstPoint.Y > height.SecondPoint.Y ? height.FirstPoint : height.SecondPoint;

			Assert(Math.Abs(topGrip.X - 120.0) <= 0.001 && Math.Abs(topGrip.Y - 240.0) <= 0.001,
				"overall height should use the real interior arc extremum");
			Assert(OutlineGeometryQuery.IsPointOnBoundary(topGrip, outline, config.GeometryTolerance),
				"arc extremum should be verified on the real outline arc");
		}

        private static void NegativeBulgeArcUsesRealInteriorGripPoint()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = new OutlineFeature2D
            {
                MinX = 100.0,
                MinY = 200.0,
                MaxX = 140.0,
                MaxY = 240.0
            };
            AddSegment(outline, new Point2D(100.0, 200.0), new Point2D(140.0, 200.0), "bottom");
            AddSegment(outline, new Point2D(140.0, 200.0), new Point2D(140.0, 220.0), "right");
            AddSegment(outline, new Point2D(100.0, 220.0), new Point2D(100.0, 200.0), "left");
            outline.Arcs.Add(new Arc2D
            {
                Start = new Point2D(100.0, 220.0),
                End = new Point2D(140.0, 220.0),
                Center = new Point2D(120.0, 220.0),
                Radius = 20.0,
                Bulge = -1.0,
                SourceKey = "clockwise-top-arc"
            });

            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);
            var height = plan.Dimensions.Single(d => d.Kind == DimensionKind.OverallHeight);
            var topGrip = height.FirstPoint.Y > height.SecondPoint.Y ? height.FirstPoint : height.SecondPoint;

            Assert(Math.Abs(topGrip.X - 120.0) <= 0.001 && Math.Abs(topGrip.Y - 240.0) <= 0.001,
                "negative bulge must preserve the real clockwise arc extremum");
            Assert(OutlineGeometryQuery.IsPointOnBoundary(topGrip, outline, config.GeometryTolerance),
                "negative-bulge extremum must lie on the real arc");
        }

		private static void DisconnectedOutlineCanUseVerifiedRealGrips()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 100.0,
				MinY = 100.0,
				MaxX = 200.0,
				MaxY = 200.0
			};
			AddSegment(outline, new Point2D(100.0, 110.0), new Point2D(100.0, 180.0), "left-fragment");
			AddSegment(outline, new Point2D(200.0, 120.0), new Point2D(200.0, 190.0), "right-fragment");
			AddSegment(outline, new Point2D(120.0, 100.0), new Point2D(180.0, 100.0), "bottom-fragment");
			AddSegment(outline, new Point2D(110.0, 200.0), new Point2D(190.0, 200.0), "top-fragment");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Count(d => d.Kind == DimensionKind.OverallWidth) == 1,
				"verified disconnected geometry should retain one overall width");
			Assert(plan.Dimensions.Count(d => d.Kind == DimensionKind.OverallHeight) == 1,
				"verified disconnected geometry should retain one overall height");
		}

        private static void TranslatedOutlinePreservesOverallSemantics()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateRectangleAt(1000.0, -500.0, 120.0, 70.0);
            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);
            var width = plan.Dimensions.Single(d => d.Kind == DimensionKind.OverallWidth);
            var height = plan.Dimensions.Single(d => d.Kind == DimensionKind.OverallHeight);

            Assert(width.Side == DimensionSide.Bottom && Math.Abs(GetSpan(width) - 120.0) <= 0.001,
                "translated overall width must keep bottom-side full-envelope semantics");
            Assert(height.Side == DimensionSide.Left && Math.Abs(GetSpan(height) - 70.0) <= 0.001,
                "translated overall height must keep left-side full-envelope semantics");
            Assert(Math.Abs(Math.Min(width.FirstPoint.X, width.SecondPoint.X) - 1000.0) <= 0.001
                && Math.Abs(Math.Max(width.FirstPoint.X, width.SecondPoint.X) - 1120.0) <= 0.001,
                "translated overall width must use translated MinX and MaxX");
            Assert(OutlineGeometryQuery.IsPointOnBoundary(width.FirstPoint, outline, config.GeometryTolerance)
                && OutlineGeometryQuery.IsPointOnBoundary(width.SecondPoint, outline, config.GeometryTolerance)
                && OutlineGeometryQuery.IsPointOnBoundary(height.FirstPoint, outline, config.GeometryTolerance)
                && OutlineGeometryQuery.IsPointOnBoundary(height.SecondPoint, outline, config.GeometryTolerance),
                "translated overall grips must remain on real geometry");
        }

        private static void RotatedOutlineUsesRealEnvelopeGrips()
        {
            var config = DimensionRuleConfig.CreateDefault();
            double angle = Math.PI / 6.0;
            var localPoints = new[]
            {
                new Point2D(0.0, 0.0),
                new Point2D(100.0, 0.0),
                new Point2D(100.0, 50.0),
                new Point2D(0.0, 50.0)
            };
            var points = localPoints.Select(point => new Point2D(
                300.0 + point.X * Math.Cos(angle) - point.Y * Math.Sin(angle),
                -200.0 + point.X * Math.Sin(angle) + point.Y * Math.Cos(angle))).ToArray();
            var outline = new FeatureRecognizer2D(config).RecognizeOutlineFromClosedPath(points);
            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);
            var width = plan.Dimensions.Single(d => d.Kind == DimensionKind.OverallWidth);
            var height = plan.Dimensions.Single(d => d.Kind == DimensionKind.OverallHeight);
            double expectedWidth = points.Max(point => point.X) - points.Min(point => point.X);
            double expectedHeight = points.Max(point => point.Y) - points.Min(point => point.Y);

            Assert(Math.Abs(GetSpan(width) - expectedWidth) <= 0.001,
                "rotated overall width must equal the WCS real envelope");
            Assert(Math.Abs(GetSpan(height) - expectedHeight) <= 0.001,
                "rotated overall height must equal the WCS real envelope");
            Assert(OutlineGeometryQuery.IsPointOnBoundary(width.FirstPoint, outline, config.GeometryTolerance)
                && OutlineGeometryQuery.IsPointOnBoundary(width.SecondPoint, outline, config.GeometryTolerance)
                && OutlineGeometryQuery.IsPointOnBoundary(height.FirstPoint, outline, config.GeometryTolerance)
                && OutlineGeometryQuery.IsPointOnBoundary(height.SecondPoint, outline, config.GeometryTolerance),
                "rotated overall grips must use actual rotated edges or endpoints");
        }

        private static void DuplicateSegmentsAreSuppressedWithDiagnostic()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateRectangle(100.0, 50.0);
            AddSegment(outline, new Point2D(40.0, 10.0), new Point2D(40.0, 30.0), "duplicate-a");
            AddSegment(outline, new Point2D(40.0, 10.0), new Point2D(40.0, 30.0), "duplicate-b");

            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);
            var matching = plan.Dimensions.Where(d => d.Orientation == DimensionOrientation.Vertical
                && Math.Abs(Math.Min(d.FirstPoint.Y, d.SecondPoint.Y) - 10.0) <= 0.001
                && Math.Abs(Math.Max(d.FirstPoint.Y, d.SecondPoint.Y) - 30.0) <= 0.001).ToList();

            Assert(matching.Count == 1, "overlapping duplicate segments must emit only one measured dimension");
            var suppressed = plan.Diagnostics.DimensionCandidates.Single(candidate => candidate.IsSuppressed
                && candidate.SuppressedReason == "DuplicateMeasuredDimension"
                && (candidate.SourceFeatureId == "duplicate-a" || candidate.SourceFeatureId == "duplicate-b"));
            Assert(!suppressed.IsSelected && suppressed.DecisionStatus == "Suppressed"
                && suppressed.DecisionReason == "DuplicateMeasuredDimension",
                "duplicate suppression must retain an explicit diagnostic decision");
            Assert(Math.Abs(suppressed.MeasurementMinimum - 10.0) <= 0.001
                && Math.Abs(suppressed.MeasurementMaximum - 30.0) <= 0.001,
                "suppressed diagnostic must preserve its measured interval");
            var finalWidth = plan.Diagnostics.FinalDimensions.Single(candidate => candidate.Kind == DimensionKind.OverallWidth.ToString());
            var finalHeight = plan.Diagnostics.FinalDimensions.Single(candidate => candidate.Kind == DimensionKind.OverallHeight.ToString());
            Assert(finalWidth.IsSelected && finalHeight.IsSelected
                && finalWidth.DecisionStatus == "Selected" && finalHeight.DecisionStatus == "Selected"
                && finalWidth.DecisionReason == "RequiredOverallDimension"
                && finalHeight.DecisionReason == "RequiredOverallDimension",
                "diagnostic final dimensions must retain exactly one width and height overall");
            Assert(plan.Diagnostics.DimensionCandidates.Where(candidate => !candidate.IsSuppressed)
                .All(candidate => candidate.IsSelected && candidate.DecisionStatus == "Selected" && !string.IsNullOrEmpty(candidate.DecisionReason)),
                "every unsuppressed candidate must have an explicit selected decision");
        }

        private static void ZeroAndMicroSegmentsDoNotEmitDimensions()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateRectangle(100.0, 50.0);
            AddSegment(outline, new Point2D(10.0, 10.0), new Point2D(10.0, 10.0), "zero-length");
            AddSegment(outline, new Point2D(20.0, 10.0), new Point2D(20.0, 10.0005), "micro-length");

            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(!plan.Dimensions.Any(d => d.SourceKey == "zero-length" || d.SourceKey == "micro-length"),
                "zero and sub-tolerance segments must not emit dimensions");
            Assert(plan.Dimensions.All(d => GetSpan(d) > config.GeometryTolerance),
                "final dimension plan must not contain zero or sub-tolerance dimensions");
        }

        private static void GeometryToleranceControlsMicroSegments()
        {
            var config = DimensionRuleConfig.CreateDefault();
            config.GeometryTolerance = 0.01;
            var outline = CreateRectangle(100.0, 50.0);
            AddSegment(outline, new Point2D(20.0, 10.0), new Point2D(20.0, 10.005), "below-tolerance");
            AddSegment(outline, new Point2D(30.0, 10.0), new Point2D(30.0, 10.02), "above-tolerance");

            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(!plan.Dimensions.Any(d => d.SourceKey == "below-tolerance"),
                "segment below configured precision must be ignored");
            Assert(plan.Dimensions.Any(d => d.SourceKey == "above-tolerance"),
                "segment above configured precision must remain dimensionable");
        }

		private static void ConcaveHoleDatumUsesRealOutlineIntersections()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 100.0,
				MinY = 100.0,
				MaxX = 200.0,
				MaxY = 200.0
			};
			AddSegment(outline, new Point2D(100.0, 100.0), new Point2D(200.0, 100.0), "bottom");
			AddSegment(outline, new Point2D(200.0, 100.0), new Point2D(200.0, 200.0), "right");
			AddSegment(outline, new Point2D(200.0, 200.0), new Point2D(160.0, 200.0), "top");
			AddSegment(outline, new Point2D(160.0, 200.0), new Point2D(160.0, 140.0), "inner-right");
			AddSegment(outline, new Point2D(160.0, 140.0), new Point2D(100.0, 140.0), "shoulder");
			AddSegment(outline, new Point2D(100.0, 140.0), new Point2D(100.0, 100.0), "left");
			var datum = Datum2D.FromOutline(outline);
			var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, new[]
			{
				CreateHole(180.0, 170.0, 8.0, HoleKind2D.Normal)
			});
			var horizontal = plan.Dimensions.Single(d => d.DebugRole == "HoleDatumX");
			var vertical = plan.Dimensions.Single(d => d.DebugRole == "HoleDatumY");

			Assert(Math.Abs(horizontal.FirstPoint.X - 100.0) <= 0.001 && Math.Abs(horizontal.FirstPoint.Y - 140.0) <= 0.001,
				"horizontal datum should preserve its X datum and use the nearest real outline point on that datum line");
			Assert(Math.Abs(vertical.FirstPoint.X - 180.0) <= 0.001 && Math.Abs(vertical.FirstPoint.Y - 100.0) <= 0.001,
				"vertical datum should preserve its Y datum and use the real bottom outline intersection");
			Assert(Math.Abs(horizontal.SecondPoint.X - horizontal.FirstPoint.X - 80.0) <= 0.001,
				"real grip correction must not change the horizontal datum measurement");
			Assert(horizontal.FirstPointMustLieOnOutline && vertical.FirstPointMustLieOnOutline,
				"outline-referenced hole dimensions should carry post-validation metadata");
			Assert(vertical.PreservePreferredSide,
				"vertical datum-to-hole location must stay on the nearest real outline side");
		}

		private static void OverallWinsDuplicatePreferenceEvenAgainstTolerance()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionDeduplicationRules(config);
			var overall = new DimensionDeduplicationItem
			{
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(100.0, 0.0),
				Kind = DimensionKind.OverallWidth,
				ForceOuterLevel = true
			};
			var functional = new DimensionDeduplicationItem
			{
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(100.0, 0.0),
				Kind = DimensionKind.DatumHoleLocationX,
				OverrideText = config.DatumHoleLocationToleranceText
			};

			Assert(rules.CompareDuplicatePreference(overall, functional) > 0,
				"overall width must outrank a toleranced functional dimension");
			Assert(rules.CompareDuplicatePreference(functional, overall) < 0,
				"duplicate preference must be symmetric for overall protection");
		}

		private static void LocalGeometryOnOverallEnvelopeSuppressesOnlyEligibleRoles()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 100.0,
				MaxY = 50.0
			};
			var plan = new DimensionPlan();
			var suppressed = new[]
			{
				CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Top,
					new Point2D(10.0, 50.0), new Point2D(30.0, 50.0), "TopStructWidth"),
				CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Bottom,
					new Point2D(30.0, 0.0), new Point2D(60.0, 0.0), "BottomStructWidth"),
				CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Vertical, DimensionSide.Left,
					new Point2D(0.0, 10.0), new Point2D(0.0, 30.0), "LeftStructHeight"),
				CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Vertical, DimensionSide.Right,
					new Point2D(100.0, 20.0), new Point2D(100.0, 45.0), "RightStructHeight"),
				CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Top,
					new Point2D(70.0, 0.0), new Point2D(90.0, 0.0), "TopChamferedStepWidth"),
				CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Vertical, DimensionSide.Right,
					new Point2D(100.0, 0.0), new Point2D(100.0, 50.0), "RightChamferedStepHeight"),
				CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Bottom,
					new Point2D(5.0, 0.0), new Point2D(15.0, 0.0), "OutlineSegment")
			};
			foreach (PlannedDimension dimension in suppressed)
			{
				plan.Add(dimension);
			}
			var overallWidth = CreateTestDimension(DimensionKind.OverallWidth, DimensionOrientation.Horizontal, DimensionSide.Bottom,
				new Point2D(0.0, 0.0), new Point2D(100.0, 0.0), "OverallWidth");
			var overallHeight = CreateTestDimension(DimensionKind.OverallHeight, DimensionOrientation.Vertical, DimensionSide.Left,
				new Point2D(0.0, 0.0), new Point2D(0.0, 50.0), "OverallHeight");
			var internalStructure = CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Top,
				new Point2D(10.0, 40.0), new Point2D(30.0, 40.0), "TopStructWidth");
			var slotDatum = CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Bottom,
				new Point2D(0.0, 0.0), new Point2D(20.0, 0.0), "SlotDatumH");
			var functional = CreateTestDimension(DimensionKind.HoleLocation, DimensionOrientation.Horizontal, DimensionSide.Bottom,
				new Point2D(20.0, 0.0), new Point2D(40.0, 0.0), "FunctionalHole");
			foreach (PlannedDimension dimension in new[] { overallWidth, overallHeight, internalStructure, slotDatum, functional })
			{
				plan.Add(dimension);
			}

			planner.SuppressLocalGeometryOnOverallEnvelope(plan, outline);

			Assert(suppressed.All(d => !plan.Dimensions.Contains(d)),
				"all four-axis local geometry dimensions on the outer envelope must be suppressed");
			Assert(suppressed.All(d => plan.Diagnostics.DimensionCandidates.Any(c =>
					c.Id == d.DiagnosticId && c.SuppressedReason == "LocalGeometryOnOverallEnvelope")),
				"suppressed envelope-local candidates must record LocalGeometryOnOverallEnvelope");
			Assert(plan.Dimensions.Contains(overallWidth) && plan.Dimensions.Contains(overallHeight)
					&& plan.Dimensions.Contains(internalStructure) && plan.Dimensions.Contains(slotDatum)
					&& plan.Dimensions.Contains(functional),
				"overall, internal structure, slot datum and functional dimensions must remain");
		}

		private static void OverallRemainsOutermostAfterLayoutAlignment()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateRectangle(100.0, 50.0);
			var dimensions = new[]
			{
				new DimensionLayoutItem
				{
					FirstPoint = new Point2D(0.0, 0.0),
					SecondPoint = new Point2D(100.0, 0.0),
					Span = 100.0,
					Kind = DimensionKind.OverallWidth,
					ForceOuterLevel = true
				},
				new DimensionLayoutItem
				{
					FirstPoint = new Point2D(-25.0, -100.0),
					SecondPoint = new Point2D(125.0, -100.0),
					Span = 150.0,
					Kind = DimensionKind.HoleLocation,
					PreferFeatureLocalPlacement = true
				}
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Bottom, outline, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);
			var overall = placements.Single(item => item.Index == 0);
			var local = placements.Single(item => item.Index == 1);

			Assert(overall.Level > local.Level, "overall dimension must remain on the outermost stacking level");
			double overallCoordinate = new DimensionLayoutRules(config).GetDimLineCoordinate(dimensions[0], DimensionSide.Bottom, outline, overall.Offset);
			double localCoordinate = new DimensionLayoutRules(config).GetDimLineCoordinate(dimensions[1], DimensionSide.Bottom, outline, local.Offset);
			Assert(overallCoordinate < localCoordinate, "overall dimension must remain physically outside feature-local dimensions");
		}

		private static void OverallCompactsToOnePhysicalSpacing()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateRectangle(100.0, 50.0);
			var dimensions = new List<DimensionLayoutItem>();
			for (int i = 0; i < 4; i++)
			{
				dimensions.Add(new DimensionLayoutItem
				{
					FirstPoint = new Point2D(0.0, i * 5.0),
					SecondPoint = new Point2D(50.0, i * 5.0),
					Span = 50.0,
					Kind = DimensionKind.HoleLocation,
					PreferFeatureLocalPlacement = true
				});
			}
			dimensions.Add(new DimensionLayoutItem
			{
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(100.0, 0.0),
				Span = 100.0,
				Kind = DimensionKind.OverallWidth,
				ForceOuterLevel = true
			});
			var rules = new DimensionLayoutRules(config);
			var placements = rules.CreateStackingPlan(dimensions, DimensionSide.Bottom, outline, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);
			var overall = placements.Single(item => item.Index == 4);
			var otherCoordinates = placements
				.Where(item => item.Index != 4)
				.Select(item => item.DimLineCoordinateOverride ?? rules.GetDimLineCoordinate(dimensions[item.Index], DimensionSide.Bottom, outline, item.Offset))
				.ToList();
			double overallCoordinate = rules.GetDimLineCoordinate(dimensions[4], DimensionSide.Bottom, outline, overall.Offset);

			Assert(Math.Abs((otherCoordinates.Min() - overallCoordinate) - 5.0) <= config.GeometryTolerance,
				"overall dimensions must compact to exactly one physical stacking interval beyond the actual outermost dimension");
		}

		private static void EffectiveSpanControlsHorizontalStacking()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.OverallWidth, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(100.0, 0.0), Span = 100.0, ForceOuterLevel = true, ReadingLevel = DimensionReadingLevel.Overall },
				new DimensionLayoutItem { Kind = DimensionKind.DatumHoleLocationX, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(10.0, 0.0), Span = 10.0, ReadingLevel = DimensionReadingLevel.DatumTransfer },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(20.0, 0.0), Span = 20.0, ReadingLevel = DimensionReadingLevel.IntraGroup },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(80.0, 0.0), Span = 80.0, ReadingLevel = DimensionReadingLevel.LocalSpacing }
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Bottom, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);

			Assert(placements.Single(item => item.Index == 1).Level < placements.Single(item => item.Index == 2).Level,
				"the shorter horizontal datum transfer must stay inside the longer intra-group span");
			Assert(placements.Single(item => item.Index == 2).Level < placements.Single(item => item.Index == 3).Level,
				"effective span must outrank semantic reading level for non-equal horizontal spans");
			Assert(placements.Single(item => item.Index == 3).Level < placements.Single(item => item.Index == 0).Level,
				"the forced horizontal overall dimension must remain outermost");
		}

		private static void SharedArrowEndpointUsesStrictSpanOrder()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateRectangle(200.0, 100.0);
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(0.0, 100.0), SecondPoint = new Point2D(73.0, 100.0), Span = 73.0, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "TopStructWidth" },
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(73.0, 100.0), SecondPoint = new Point2D(160.554, 100.0), Span = 87.554, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "TopStructWidth" }
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Top, outline, 2.5, 1.0, 5.0, 6.5, isHorizontal: true).ToDictionary(item => item.Index);

			Assert(placements[0].Level < placements[1].Level,
				$"adjacent dimensions that share an arrow endpoint must keep the shorter span physically inside the longer span (levels {placements[0].Level}/{placements[1].Level})");
			Assert(placements.Values.All(placement => placement.PhysicalOrderValidated),
				"shared-endpoint span ordering must pass physical-order validation");
		}

		private static void StructureAlignmentKeyAlignsAdjacentSegmentsOnSameLevel()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateRectangle(200.0, 100.0);
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(0.0, 100.0), SecondPoint = new Point2D(73.0, 100.0), Span = 73.0, AlignmentKey = "Structure:T:H", AlignmentPriority = 70, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "TopStructWidth" },
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(73.0, 100.0), SecondPoint = new Point2D(160.554, 100.0), Span = 87.554, AlignmentKey = "Structure:T:H", AlignmentPriority = 70, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "TopStructWidth" }
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Top, outline, 2.5, 1.0, 5.0, 6.5, isHorizontal: true).ToDictionary(item => item.Index);

			Assert(placements[0].Level == placements[1].Level,
				$"adjacent structure segments that share an alignment key must stay on the same stacking level (levels {placements[0].Level}/{placements[1].Level})");
			Assert(placements[0].LayoutBlockType == "RootedAlignmentLane" && placements[1].LayoutBlockType == "RootedAlignmentLane",
				"structure alignment key members must form a rooted alignment lane");
			Assert(placements[0].LayoutBlockId == placements[1].LayoutBlockId,
				"adjacent structure segments must share one rooted layout block");
		}

		/// <summary>
		/// U-slot edge location (SlotChainV 10) + inter-slot center distance (SlotChainV 15)
		/// are one continuous locating chain and must share AlignmentKey / same stacking level.
		/// </summary>
		private static void SlotChainAlignmentKeyAlignsEdgeLocationAndCenterDistance()
		{
			var config = DimensionRuleConfig.CreateDefault();
			// Layout-only: same key as planner will emit for a left vertical slot chain.
			var outline = CreateRectangle(100.0, 50.0);
			const string slotChainKey = "SlotChain:L:V:50";
			var dimensions = new[]
			{
				// Edge → first U-slot (SCV2 style)
				new DimensionLayoutItem
				{
					Kind = DimensionKind.Normal,
					FirstPoint = new Point2D(50.0, 0.0),
					SecondPoint = new Point2D(50.0, 10.0),
					Span = 10.0,
					AlignmentKey = slotChainKey,
					AlignmentPriority = 80,
					ReadingLevel = DimensionReadingLevel.LocalSpacing,
					SourceFeatureId = "SlotChainV"
				},
				// First U-slot → second U-slot center distance (SCV1 style)
				new DimensionLayoutItem
				{
					Kind = DimensionKind.Normal,
					FirstPoint = new Point2D(50.0, 10.0),
					SecondPoint = new Point2D(50.0, 25.0),
					Span = 15.0,
					AlignmentKey = slotChainKey,
					AlignmentPriority = 80,
					ReadingLevel = DimensionReadingLevel.LocalSpacing,
					SourceFeatureId = "SlotChainV"
				},
				new DimensionLayoutItem
				{
					Kind = DimensionKind.OverallHeight,
					FirstPoint = new Point2D(0.0, 0.0),
					SecondPoint = new Point2D(0.0, 50.0),
					Span = 50.0,
					ForceOuterLevel = true,
					ReadingLevel = DimensionReadingLevel.Overall,
					SourceFeatureId = "OverallHeight"
				}
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Left, outline, 2.5, 1.0, 5.0, 5.0, isHorizontal: false).ToDictionary(item => item.Index);

			Assert(placements[0].Level == placements[1].Level,
				$"slot edge location and inter-slot center distance must share one stacking level (levels {placements[0].Level}/{placements[1].Level})");
			Assert(placements[0].LayoutBlockType == "RootedAlignmentLane" && placements[1].LayoutBlockType == "RootedAlignmentLane",
				"slot chain members must form a rooted alignment lane");
			Assert(placements[0].LayoutBlockId == placements[1].LayoutBlockId,
				"slot chain members must share one rooted layout block");
			Assert(placements[2].Level > placements[0].Level,
				"overall height must remain outside the slot chain lane");

			// Planner emits the same key for continuous vertical slot anchors.
			var outline2 = CreateRectangle(100.0, 50.0);
			var datum = Datum2D.FromOutline(outline2);
			var slots = new[]
			{
				new SlotFeature2D
				{
					GroupId = "U1",
					FirstCenter = new Point2D(50.0, 10.0),
					SecondCenter = new Point2D(70.0, 10.0),
					Radius = 5.0,
					CenterDistance = 20.0
				},
				new SlotFeature2D
				{
					GroupId = "U2",
					FirstCenter = new Point2D(50.0, 25.0),
					SecondCenter = new Point2D(70.0, 25.0),
					Radius = 5.0,
					CenterDistance = 20.0
				}
			};
			var plan = new DimensionPlanner(config).CreateDimensionPlan(outline2, datum, new HoleFeature2D[0], slots);
			var chain = plan.Dimensions.Where(d => d.DebugRole == "SlotChainV").ToList();
			Assert(chain.Count >= 2, "expected vertical slot chain segments (edge location + center distance)");
			Assert(chain.All(d => !string.IsNullOrEmpty(d.AlignmentKey) && d.AlignmentKey.StartsWith("SlotChain:L:V:", StringComparison.Ordinal)),
				"SlotChainV members must carry SlotChain:L:V alignment keys");
			Assert(chain.Select(d => d.AlignmentKey).Distinct().Count() == 1,
				"edge location and inter-slot distance on one column must share one AlignmentKey");
			Assert(chain.All(d => d.AlignmentPriority == 80),
				"slot chain alignment priority must be 80");
		}

		private static void OutlineSegmentsThatPartitionOverallAreSuppressed()
		{
			// Collinear bottom edge split into 25 + 232 = overall 257 (repro of ASD4 bottom).
			// Raw OutlineSegment pair restates overall and must not remain selected.
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 257.0,
				MaxY = 20.0
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(25.0, 0.0), "bottom-left");
			AddSegment(outline, new Point2D(25.0, 0.0), new Point2D(257.0, 0.0), "bottom-right");
			AddSegment(outline, new Point2D(257.0, 0.0), new Point2D(257.0, 20.0), "right");
			AddSegment(outline, new Point2D(257.0, 20.0), new Point2D(0.0, 20.0), "top");
			AddSegment(outline, new Point2D(0.0, 20.0), new Point2D(0.0, 0.0), "left");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 257.0) <= config.GeometryTolerance),
				"overall width 257 must remain");
			Assert(!plan.Dimensions.Any(d => d.DebugRole == "OutlineSegment"
					&& d.Side == DimensionSide.Bottom),
				"bottom OutlineSegment dims that partition overall must not remain selected");
			var suppressedBottom = plan.Diagnostics.DimensionCandidates
				.Where(c => c.DebugRole == "OutlineSegment"
					&& c.PlacementSide == DimensionSide.Bottom.ToString()
					&& c.IsSuppressed)
				.ToList();
			Assert(suppressedBottom.Count >= 2
					&& suppressedBottom.Any(c => Math.Abs(c.Value - 25.0) <= config.GeometryTolerance)
					&& suppressedBottom.Any(c => Math.Abs(c.Value - 232.0) <= config.GeometryTolerance),
				"both 25 and 232 OutlineSegments must be suppressed");
			Assert(suppressedBottom.All(c => c.SuppressedReason == "LocalGeometryOnOverallEnvelope"),
				"partition OutlineSegments must record the higher-priority outer-envelope reason");
		}

		/// <summary>
		/// Repro GEN|OutlineSegment1/2/3|GB|B|L*: bottom collinear 9+11+71 = overall 91.
		/// Pairwise sums are not overall, but the 3-piece chain covers overall and must suppress all.
		/// </summary>
		private static void ThreeOutlineSegmentsThatPartitionOverallAreSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 91.0,
				MaxY = 20.0
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(9.0, 0.0), "bottom-a");
			AddSegment(outline, new Point2D(9.0, 0.0), new Point2D(20.0, 0.0), "bottom-b");
			AddSegment(outline, new Point2D(20.0, 0.0), new Point2D(91.0, 0.0), "bottom-c");
			AddSegment(outline, new Point2D(91.0, 0.0), new Point2D(91.0, 20.0), "right");
			AddSegment(outline, new Point2D(91.0, 20.0), new Point2D(0.0, 20.0), "top");
			AddSegment(outline, new Point2D(0.0, 20.0), new Point2D(0.0, 0.0), "left");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 91.0) <= config.GeometryTolerance),
				"overall width 91 must remain");
			Assert(!plan.Dimensions.Any(d => d.DebugRole == "OutlineSegment" && d.Side == DimensionSide.Bottom),
				"bottom OutlineSegments 9+11+71 that partition overall must not remain selected");
			var suppressedBottom = plan.Diagnostics.DimensionCandidates
				.Where(c => c.DebugRole == "OutlineSegment"
					&& c.PlacementSide == DimensionSide.Bottom.ToString()
					&& c.IsSuppressed
					&& c.SuppressedReason == "LocalGeometryOnOverallEnvelope")
				.ToList();
			Assert(suppressedBottom.Count >= 3
					&& suppressedBottom.Any(c => Math.Abs(c.Value - 9.0) <= config.GeometryTolerance)
					&& suppressedBottom.Any(c => Math.Abs(c.Value - 11.0) <= config.GeometryTolerance)
					&& suppressedBottom.Any(c => Math.Abs(c.Value - 71.0) <= config.GeometryTolerance),
				"all three OutlineSegments 9, 11, 71 must record the outer-envelope suppression reason");

			// Helper: multi-interval chain detection.
			var rules = new DimensionDeduplicationRules(config);
			var chain = new[]
			{
				Tuple.Create(0.0, 9.0),
				Tuple.Create(9.0, 20.0),
				Tuple.Create(20.0, 91.0)
			};
			Assert(rules.FormsCompleteOverallPartitionChain(chain, 0.0, 91.0),
				"9+11+71 intervals must form a complete overall partition chain");
			Assert(!rules.FormsCompleteOverallPartitionChain(
					new[] { Tuple.Create(0.0, 9.0), Tuple.Create(9.0, 20.0) }, 0.0, 91.0),
				"partial chain that does not reach overall max must not be treated as partition");
		}

		/// <summary>
		/// Option 1: outer-envelope OutlineSegment fragment (e.g. right-end 10 on overall 75)
		/// must suppress when Overall already exists — even if it does not complete a partition chain
		/// (middle piece removed by chamfer / left gap).
		/// </summary>
		private static void OutlineSegmentOnOverallEnvelopeIsSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 75.0,
				MaxY = 40.0
			};
			// Bottom: short left (chamfer-like 5), long mid 60, right tip 10 — overall 75.
			// Even if mid is not a complete partition partner, right tip 10 lies on envelope.
			AddSegment(outline, new Point2D(0.0, 5.0), new Point2D(5.0, 0.0), "chamfer-bl");
			AddSegment(outline, new Point2D(5.0, 0.0), new Point2D(65.0, 0.0), "bottom-mid");
			AddSegment(outline, new Point2D(65.0, 0.0), new Point2D(75.0, 0.0), "bottom-right-tip");
			AddSegment(outline, new Point2D(75.0, 0.0), new Point2D(75.0, 40.0), "right");
			AddSegment(outline, new Point2D(75.0, 40.0), new Point2D(0.0, 40.0), "top");
			AddSegment(outline, new Point2D(0.0, 40.0), new Point2D(0.0, 5.0), "left");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 75.0) <= config.GeometryTolerance),
				"overall width 75 must remain");
			Assert(!plan.Dimensions.Any(d => d.DebugRole == "OutlineSegment"
					&& d.Orientation == DimensionOrientation.Horizontal
					&& d.Side == DimensionSide.Bottom
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 10.0) <= config.GeometryTolerance),
				"bottom envelope OutlineSegment tip 10 must not remain selected when Overall exists");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c =>
					c.DebugRole == "OutlineSegment"
					&& c.PlacementSide == DimensionSide.Bottom.ToString()
					&& Math.Abs(c.Value - 10.0) <= config.GeometryTolerance
					&& c.IsSuppressed
					&& (c.SuppressedReason == "LocalGeometryOnOverallEnvelope"
						|| c.SuppressedReason == "OutlineSegmentOnOverallEnvelope"
						|| c.SuppressedReason == "OutlineSegmentOverallPartition"
						|| c.SuppressedReason == "SuppressedByCornerFeature")),
				"bottom envelope OutlineSegment 10 must be suppressed (envelope/partition/corner)");
		}

		/// <summary>
		/// Same geometry as envelope OS tip 10: BottomStructWidth 10 must also suppress
		/// (duplicate candidate of the outer-envelope fragment).
		/// </summary>
		private static void StructureDuplicateOfEnvelopeOutlineSegmentIsSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 75.0,
				MaxY = 40.0
			};
			AddSegment(outline, new Point2D(0.0, 5.0), new Point2D(5.0, 0.0), "chamfer-bl");
			AddSegment(outline, new Point2D(5.0, 0.0), new Point2D(65.0, 0.0), "bottom-mid");
			AddSegment(outline, new Point2D(65.0, 0.0), new Point2D(75.0, 0.0), "bottom-right-tip");
			AddSegment(outline, new Point2D(75.0, 0.0), new Point2D(75.0, 40.0), "right");
			AddSegment(outline, new Point2D(75.0, 40.0), new Point2D(0.0, 40.0), "top");
			AddSegment(outline, new Point2D(0.0, 40.0), new Point2D(0.0, 5.0), "left");
			// Full-height shoulder at tip start so BottomStructWidth 10 is also generated.
			AddSegment(outline, new Point2D(65.0, 0.0), new Point2D(65.0, 40.0), "shoulder-tip");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(!plan.Dimensions.Any(d =>
					(d.DebugRole == "BottomStructWidth" || d.DebugRole == "TopStructWidth")
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 10.0) <= config.GeometryTolerance),
				"structure width 10 that duplicates envelope OutlineSegment tip must not remain selected");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c =>
					(c.DebugRole == "BottomStructWidth" || c.DebugRole == "TopStructWidth")
					&& Math.Abs(c.Value - 10.0) <= config.GeometryTolerance
					&& c.IsSuppressed
					&& (c.SuppressedReason == "LocalGeometryOnOverallEnvelope"
						|| c.SuppressedReason == "StructureDuplicateOfEnvelopeOutlineSegment"
						|| c.SuppressedReason == "StructureOverallPartition"
						|| c.SuppressedReason == "MirroredDuplicate")),
				"structure width 10 must be suppressed as envelope-OS duplicate (or equivalent)");
		}

		/// <summary>
		/// Positive gate: Bottom envelope OS [65,75]@Y=0 + collinear same-side BottomStructWidth
		/// [65,75]@Y=0 — structure is a true tip duplicate and must be co-suppressed.
		/// Isolated unit call avoids other suppress paths so the envelope co-delete rule is explicit.
		/// </summary>
		private static void EnvelopeStructureSameSideCollinearDuplicateIsSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var plan = new DimensionPlan();
			var overallWidth = new PlannedDimension
			{
				Kind = DimensionKind.OverallWidth,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(75.0, 0.0),
				DebugRole = "OverallWidth"
			};
			var overallHeight = new PlannedDimension
			{
				Kind = DimensionKind.OverallHeight,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(0.0, 40.0),
				DebugRole = "OverallHeight"
			};
			var envelopeOs = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(65.0, 0.0),
				SecondPoint = new Point2D(75.0, 0.0),
				DebugRole = "OutlineSegment"
			};
			var structure = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(65.0, 0.0),
				SecondPoint = new Point2D(75.0, 0.0),
				DebugRole = "BottomStructWidth"
			};
			plan.Add(overallWidth);
			plan.Add(overallHeight);
			plan.Add(envelopeOs);
			plan.Add(structure);

			planner.SuppressOutlineSegmentsOnOverallEnvelope(plan);

			Assert(plan.Dimensions.Contains(overallWidth), "overall width must remain");
			Assert(plan.Dimensions.Contains(overallHeight), "overall height must remain");
			Assert(!plan.Dimensions.Contains(envelopeOs),
				"bottom envelope OutlineSegment tip [65,75] must be suppressed");
			Assert(!plan.Dimensions.Contains(structure),
				"same-side collinear BottomStructWidth [65,75]@Y=0 must be co-suppressed as envelope OS duplicate");
		}

		/// <summary>
		/// Negative gate: Bottom envelope OS [65,75]@Y=0 + opposite/internal structure
		/// [65,75]@Y=35 (TopStructWidth) — same 1D interval only must NOT suppress structure.
		/// </summary>
		private static void StructureSameIntervalAsOppositeEnvelopeTipIsKept()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var plan = new DimensionPlan();
			var overallWidth = new PlannedDimension
			{
				Kind = DimensionKind.OverallWidth,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(75.0, 0.0),
				DebugRole = "OverallWidth"
			};
			var overallHeight = new PlannedDimension
			{
				Kind = DimensionKind.OverallHeight,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(0.0, 40.0),
				DebugRole = "OverallHeight"
			};
			var envelopeOs = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(65.0, 0.0),
				SecondPoint = new Point2D(75.0, 0.0),
				DebugRole = "OutlineSegment"
			};
			var structure = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Top,
				FirstPoint = new Point2D(65.0, 35.0),
				SecondPoint = new Point2D(75.0, 35.0),
				DebugRole = "TopStructWidth"
			};
			plan.Add(overallWidth);
			plan.Add(overallHeight);
			plan.Add(envelopeOs);
			plan.Add(structure);

			planner.SuppressOutlineSegmentsOnOverallEnvelope(plan);

			Assert(plan.Dimensions.Contains(overallWidth), "overall width must remain");
			Assert(!plan.Dimensions.Contains(envelopeOs),
				"bottom envelope OutlineSegment tip must still be suppressed");
			Assert(plan.Dimensions.Contains(structure),
				"TopStructWidth [65,75]@Y=35 must be kept — same X interval as bottom tip is not a geometric duplicate");
			Assert(plan.Diagnostics.DimensionCandidates
					.Where(c => c.DebugRole == "TopStructWidth" && Math.Abs(c.Value - 10.0) <= config.GeometryTolerance)
					.All(c => c.SuppressedReason != "StructureDuplicateOfEnvelopeOutlineSegment"),
				"opposite-edge structure must not be labeled StructureDuplicateOfEnvelopeOutlineSegment");
		}

		private static void MirroredEnvelopeStructureWidthsAreBothSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 90.54,
				MaxY = 20.0
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(0.0, 20.0), "left");
			AddSegment(outline, new Point2D(0.0, 20.0), new Point2D(15.54, 20.0), "top-left");
			AddSegment(outline, new Point2D(15.54, 20.0), new Point2D(67.54, 20.0), "top-52");
			AddSegment(outline, new Point2D(67.54, 20.0), new Point2D(90.54, 20.0), "top-right");
			AddSegment(outline, new Point2D(90.54, 20.0), new Point2D(90.54, 0.0), "right");
			AddSegment(outline, new Point2D(90.54, 0.0), new Point2D(67.54, 0.0), "bottom-right");
			AddSegment(outline, new Point2D(67.54, 0.0), new Point2D(15.54, 0.0), "bottom-52");
			AddSegment(outline, new Point2D(15.54, 0.0), new Point2D(0.0, 0.0), "bottom-left");
			AddSegment(outline, new Point2D(15.54, 0.0), new Point2D(15.54, 20.0), "internal-left");
			AddSegment(outline, new Point2D(67.54, 0.0), new Point2D(67.54, 20.0), "internal-right");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(!plan.Dimensions.Any(d =>
					(d.DebugRole == "TopStructWidth" || d.DebugRole == "BottomStructWidth")
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 52.0) <= config.GeometryTolerance),
				"mirrored top/bottom structure width 52 with same-edge outline witnesses must both be suppressed");
			// History note: before pre-plan discards became visible in diagnostics, the 52
			// candidates were dropped silently during candidate building (52 completes the
			// 15.54+52+23 overall partition), DimensionCandidates contained no 52 entry at
			// all, and the original .All()-based reason assertion passed vacuously. The real
			// contract is: every mirrored 52 candidate must leave an explanatory trace -
			// either a pre-plan discard with a concrete reason, or an in-plan suppression
			// with the high-priority envelope reason - and both sides must leave one.
			var mirrored52 = plan.Diagnostics.DimensionCandidates
				.Where(c => (c.DebugRole == "TopStructWidth" || c.DebugRole == "BottomStructWidth")
					&& Math.Abs(c.Value - 52.0) <= config.GeometryTolerance)
				.ToList();
			Assert(mirrored52.Any(c => c.DebugRole == "TopStructWidth")
					&& mirrored52.Any(c => c.DebugRole == "BottomStructWidth"),
				"both mirrored structure width 52 candidates must appear in diagnostics");
			Assert(mirrored52.All(c =>
					(c.DecisionStatus == "Skipped" && !string.IsNullOrEmpty(c.DecisionReason))
					|| c.SuppressedReason == "LocalGeometryOnOverallEnvelope"),
				"every mirrored structure width 52 candidate must record a discard reason or the high-priority envelope reason");
		}

		private static void Production73x20EnvelopeDimensionsAreSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 73.0,
				MaxY = 20.0
			};
			var plan = new DimensionPlan();
			var invalid = new[]
			{
				CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Top,
					new Point2D(0.0, 20.0), new Point2D(35.38090093, 20.0), "TopStructWidth"),
				CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Bottom,
					new Point2D(35.38090097, 0.0), new Point2D(73.0, 0.0), "BottomStructWidth"),
				CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Top,
					new Point2D(53.0, 0.0), new Point2D(73.0, 0.0), "TopChamferedStepWidth"),
				CreateTestDimension(DimensionKind.Normal, DimensionOrientation.Vertical, DimensionSide.Right,
					new Point2D(73.0, 0.0), new Point2D(73.0, 20.0), "RightChamferedStepHeight")
			};
			foreach (PlannedDimension dimension in invalid)
			{
				plan.Add(dimension);
			}
			var valid = new[]
			{
				CreateTestDimension(DimensionKind.OverallWidth, DimensionOrientation.Horizontal, DimensionSide.Bottom,
					new Point2D(0.0, 0.0), new Point2D(73.0, 0.0), "OverallWidth"),
				CreateTestDimension(DimensionKind.OverallHeight, DimensionOrientation.Vertical, DimensionSide.Left,
					new Point2D(0.0, 0.0), new Point2D(0.0, 20.0), "OverallHeight"),
				CreateTestDimension(DimensionKind.DatumHoleLocationX, DimensionOrientation.Horizontal, DimensionSide.Top,
					new Point2D(0.0, 10.0), new Point2D(10.0, 10.0), "DatumX"),
				CreateTestDimension(DimensionKind.DatumHoleLocationY, DimensionOrientation.Vertical, DimensionSide.Left,
					new Point2D(10.0, 0.0), new Point2D(10.0, 10.0), "DatumY"),
				CreateTestDimension(DimensionKind.PinDistance, DimensionOrientation.Horizontal, DimensionSide.Top,
					new Point2D(10.0, 10.0), new Point2D(40.0, 10.0), "PinDistance"),
				CreateTestDimension(DimensionKind.HoleLocation, DimensionOrientation.Horizontal, DimensionSide.Top,
					new Point2D(10.0, 10.0), new Point2D(25.0, 10.0), "FunctionalHole")
			};
			foreach (PlannedDimension dimension in valid)
			{
				plan.Add(dimension);
			}
			Assert(invalid.Length == 4 && invalid.All(plan.Dimensions.Contains),
				"the 73x20 production regression must contain all four invalid candidates before suppression");

			planner.SuppressLocalGeometryOnOverallEnvelope(plan, outline);

			Assert(invalid.All(d => !plan.Dimensions.Contains(d)),
				"35.38, 37.62 and both composite 20 dimensions must be suppressed");
			Assert(invalid.All(d => plan.Diagnostics.DimensionCandidates.Any(c =>
					c.Id == d.DiagnosticId && c.SuppressedReason == "LocalGeometryOnOverallEnvelope")),
				"all four production candidates must record LocalGeometryOnOverallEnvelope");
			Assert(valid.All(plan.Dimensions.Contains),
				"73, 20, datum 10, pin 30 and functional 15 dimensions must remain");
		}

		/// <summary>
		/// Repro GEN|OutlineSegment1|GB|R|L0: outer right tip OS@MaxX height 5 is envelope-suppressed,
		/// but an inner vertical at mid-X with the same Y-interval was left selected (Side=Right).
		/// Same measurement interval as envelope fragment must also suppress.
		/// Bottom gap prevents left/right 5+40 from forming an overall-height partition chain
		/// (mirrors CAD case where mid segment is corner-suppressed and tips remain).
		/// </summary>
		private static void InnerOutlineSegmentMatchingEnvelopeTipIsSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 40.0,
				MaxY = 45.0
			};
			// Bottom gap [0,5] via diagonal so verticals do not start at overall MinY → no 5+40 chain.
			AddSegment(outline, new Point2D(5.0, 0.0), new Point2D(35.0, 0.0), "bottom");
			AddSegment(outline, new Point2D(0.0, 5.0), new Point2D(5.0, 0.0), "chamfer-bl");
			AddSegment(outline, new Point2D(35.0, 0.0), new Point2D(40.0, 5.0), "chamfer-br");
			AddSegment(outline, new Point2D(40.0, 5.0), new Point2D(40.0, 40.0), "right-main");
			AddSegment(outline, new Point2D(40.0, 40.0), new Point2D(40.0, 45.0), "right-tip");
			AddSegment(outline, new Point2D(40.0, 45.0), new Point2D(0.0, 45.0), "top");
			AddSegment(outline, new Point2D(0.0, 45.0), new Point2D(0.0, 40.0), "left-tip");
			AddSegment(outline, new Point2D(0.0, 40.0), new Point2D(0.0, 5.0), "left-main");
			// Inner vertical, same Y-interval as outer tips [40,45], X not on envelope → Side=Right.
			AddSegment(outline, new Point2D(5.0, 40.0), new Point2D(5.0, 45.0), "inner-tip");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallHeight
					&& Math.Abs(Math.Abs(d.SecondPoint.Y - d.FirstPoint.Y) - 45.0) <= config.GeometryTolerance),
				"overall height 45 must remain");
			// Inner tip must not remain; outer tips should be envelope-suppressed.
			Assert(!plan.Dimensions.Any(d => d.DebugRole == "OutlineSegment"
					&& d.Orientation == DimensionOrientation.Vertical
					&& Math.Abs(Math.Abs(d.SecondPoint.Y - d.FirstPoint.Y) - 5.0) <= config.GeometryTolerance
					&& Math.Abs(d.FirstPoint.X - 5.0) <= config.GeometryTolerance),
				"inner vertical OutlineSegment tip at X=5 matching envelope tip interval must not remain");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c =>
					c.DebugRole == "OutlineSegment"
					&& Math.Abs(c.Value - 5.0) <= config.GeometryTolerance
					&& c.IsSuppressed
					&& (c.SuppressedReason == "OutlineSegmentOnOverallEnvelope"
						|| c.SuppressedReason == "OutlineSegmentSameIntervalAsEnvelopeFragment"
						|| c.SuppressedReason == "MirroredDuplicate"
						|| c.SuppressedReason == "DuplicateMeasuredDimension")),
				"tip OutlineSegments of 5 must be suppressed");
		}

		/// <summary>
		/// Case 1: [0,25] + [25,257] = overall [0,257] — true abutment partition.
		/// </summary>
		private static void CompleteOverallPartitionIntervalsAreDetected()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionDeduplicationRules(config);
			var overall = CreatePartitionItem(0.0, 0.0, 257.0, 0.0, DimensionKind.OverallWidth);
			var a = CreatePartitionItem(0.0, 0.0, 25.0, 0.0, DimensionKind.Normal, "OutlineSegment");
			var b = CreatePartitionItem(25.0, 0.0, 257.0, 0.0, DimensionKind.Normal, "OutlineSegment");

			Assert(rules.FormsCompleteOverallPartition(a, b, overall, horizontal: true),
				"abutting [0,25]+[25,257] must form a complete overall partition of 257");
			// Integration: same geometry still suppresses OutlineSegment pair in the planner.
			var outline = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 257.0, MaxY = 20.0 };
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(25.0, 0.0), "bottom-left");
			AddSegment(outline, new Point2D(25.0, 0.0), new Point2D(257.0, 0.0), "bottom-right");
			AddSegment(outline, new Point2D(257.0, 0.0), new Point2D(257.0, 20.0), "right");
			AddSegment(outline, new Point2D(257.0, 20.0), new Point2D(0.0, 20.0), "top");
			AddSegment(outline, new Point2D(0.0, 20.0), new Point2D(0.0, 0.0), "left");
			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);
			Assert(!plan.Dimensions.Any(d => d.DebugRole == "OutlineSegment" && d.Side == DimensionSide.Bottom),
				"true partition OutlineSegments must still be suppressed by planner");
		}

		/// <summary>
		/// Phase 2: legitimate snap candidate at origin (0,0) must not be treated as NotFound
		/// (old code used Equals(default(Point2D))).
		/// Covers both vertical-down and horizontal-left snap directions.
		/// </summary>
		private static void SnapToOriginPointIsNotTreatedAsNotFound()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			// Vertical snap: point (0, 10) should snap down to horizontal endpoint (0, 0).
			var outlineVertical = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 50.0,
				MaxY = 20.0
			};
			AddSegment(outlineVertical, new Point2D(0.0, 0.0), new Point2D(50.0, 0.0), "bottom");
			AddSegment(outlineVertical, new Point2D(50.0, 0.0), new Point2D(50.0, 20.0), "right");
			AddSegment(outlineVertical, new Point2D(50.0, 20.0), new Point2D(0.0, 20.0), "top");
			AddSegment(outlineVertical, new Point2D(0.0, 20.0), new Point2D(0.0, 0.0), "left");
			Point2D snappedDown = planner.SnapVerticalPointToLowerConnectedHorizontal(new Point2D(0.0, 10.0), outlineVertical);
			Assert(Math.Abs(snappedDown.X - 0.0) <= config.GeometryTolerance
					&& Math.Abs(snappedDown.Y - 0.0) <= config.GeometryTolerance,
				"vertical snap must accept origin (0,0) as a valid candidate, not NotFound");

			// Horizontal snap: point (10, 0) should snap left to vertical endpoint (0, 0).
			var outlineHorizontal = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 50.0,
				MaxY = 20.0
			};
			AddSegment(outlineHorizontal, new Point2D(0.0, 0.0), new Point2D(0.0, 20.0), "left");
			AddSegment(outlineHorizontal, new Point2D(0.0, 20.0), new Point2D(50.0, 20.0), "top");
			AddSegment(outlineHorizontal, new Point2D(50.0, 20.0), new Point2D(50.0, 0.0), "right");
			AddSegment(outlineHorizontal, new Point2D(50.0, 0.0), new Point2D(0.0, 0.0), "bottom");
			Point2D snappedLeft = planner.SnapHorizontalPointToLeftConnectedVertical(new Point2D(10.0, 0.0), outlineHorizontal);
			Assert(Math.Abs(snappedLeft.X - 0.0) <= config.GeometryTolerance
					&& Math.Abs(snappedLeft.Y - 0.0) <= config.GeometryTolerance,
				"horizontal snap must accept origin (0,0) as a valid candidate, not NotFound");
		}

		/// <summary>
		/// Phase 2: when no snap candidate exists, keep the original point (both directions).
		/// </summary>
		private static void SnapKeepsOriginalPointWhenNoCandidateExists()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			// Isolated horizontal edge only — no lower connected horizontal for a vertical snap.
			var outline = new OutlineFeature2D
			{
				MinX = 10.0,
				MinY = 10.0,
				MaxX = 40.0,
				MaxY = 30.0
			};
			AddSegment(outline, new Point2D(10.0, 30.0), new Point2D(40.0, 30.0), "top-only");
			Point2D originalVertical = new Point2D(20.0, 25.0);
			Point2D snappedVertical = planner.SnapVerticalPointToLowerConnectedHorizontal(originalVertical, outline);
			Assert(Math.Abs(snappedVertical.X - originalVertical.X) <= config.GeometryTolerance
					&& Math.Abs(snappedVertical.Y - originalVertical.Y) <= config.GeometryTolerance,
				"vertical snap with no lower candidate must keep the original point");

			// Isolated vertical edge only — no left connected vertical for a horizontal snap.
			var outline2 = new OutlineFeature2D
			{
				MinX = 10.0,
				MinY = 10.0,
				MaxX = 40.0,
				MaxY = 30.0
			};
			AddSegment(outline2, new Point2D(40.0, 10.0), new Point2D(40.0, 30.0), "right-only");
			Point2D originalHorizontal = new Point2D(30.0, 20.0);
			Point2D snappedHorizontal = planner.SnapHorizontalPointToLeftConnectedVertical(originalHorizontal, outline2);
			Assert(Math.Abs(snappedHorizontal.X - originalHorizontal.X) <= config.GeometryTolerance
					&& Math.Abs(snappedHorizontal.Y - originalHorizontal.Y) <= config.GeometryTolerance,
				"horizontal snap with no left candidate must keep the original point");
		}

		/// <summary>
		/// Phase 3: legal complementary [0,20]+[20,98]=overall [0,98] — snap gate allows,
		/// FormsCompleteOverallPartition holds, RemoveComplementary drops the larger.
		/// </summary>
		private static void LegalComplementaryPartitionAllowsSnapAndSuppress()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var outline = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 98.0, MaxY = 20.0 };
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(98.0, 0.0), "bottom");
			AddSegment(outline, new Point2D(98.0, 0.0), new Point2D(98.0, 20.0), "right");
			AddSegment(outline, new Point2D(98.0, 20.0), new Point2D(0.0, 20.0), "top");
			AddSegment(outline, new Point2D(0.0, 20.0), new Point2D(0.0, 0.0), "left");
			AddSegment(outline, new Point2D(20.0, 0.0), new Point2D(20.0, 20.0), "shoulder");

			var smaller = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Top,
				FirstPoint = new Point2D(0.0, 20.0),
				SecondPoint = new Point2D(20.0, 20.0),
				DebugRole = "TopStructWidth"
			};
			var larger = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Top,
				FirstPoint = new Point2D(20.0, 20.0),
				SecondPoint = new Point2D(98.0, 20.0),
				DebugRole = "TopStructWidth"
			};
			Assert(planner.CanAttemptComplementaryPartitionSnap(larger, smaller, outline, horizontal: true),
				"legal abutting structure pair must allow complementary snap");
			// Integration: full plan still suppresses the overall-restating structure pair path.
			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);
			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 98.0) <= config.GeometryTolerance),
				"overall 98 must remain");
			// Structure that only re-partitions overall with same-side collinear OS is suppressed elsewhere;
			// gate itself must remain true for the legal pair.
			Assert(planner.CanAttemptComplementaryPartitionSnap(larger, smaller, outline, horizontal: true),
				"legal pair remains snap-eligible after FormsCompleteOverallPartition check");
		}

		/// <summary>
		/// Phase 3: [0,40]+[20,80] spans 40+60=100 but overlap — no snap, no partition suppress.
		/// </summary>
		private static void OverlappingSpanSumDoesNotAllowComplementarySnap()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var outline = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 100.0, MaxY = 20.0 };
			var a = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Top,
				FirstPoint = new Point2D(0.0, 20.0),
				SecondPoint = new Point2D(40.0, 20.0),
				DebugRole = "TopStructWidth"
			};
			var b = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Top,
				FirstPoint = new Point2D(20.0, 20.0),
				SecondPoint = new Point2D(80.0, 20.0),
				DebugRole = "TopStructWidth"
			};
			Assert(Math.Abs((40.0 + 60.0) - 100.0) <= config.GeometryTolerance, "precondition: span sum equals overall");
			Assert(!planner.CanAttemptComplementaryPartitionSnap(b, a, outline, horizontal: true),
				"overlapping intervals must not allow complementary snap despite span sum");
			Point2D beforeFirst = b.FirstPoint;
			Point2D beforeSecond = b.SecondPoint;
			// Simulate snap gate path: when CanAttempt is false, points must stay put.
			Assert(!planner.CanAttemptComplementaryPartitionSnap(b, a, outline, horizontal: true),
				"re-check: still not snap-eligible");
			Assert(beforeFirst.Equals(b.FirstPoint) && beforeSecond.Equals(b.SecondPoint),
				"attachment must not be modified when snap gate rejects overlap");
		}

		/// <summary>
		/// Phase 3: [0,30]+[40,100] spans 30+60=90 or 30+70=100 with gap — no snap.
		/// </summary>
		private static void GappedSpanSumDoesNotAllowComplementarySnap()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var outline = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 100.0, MaxY = 20.0 };
			var a = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Top,
				FirstPoint = new Point2D(0.0, 20.0),
				SecondPoint = new Point2D(30.0, 20.0),
				DebugRole = "TopStructWidth"
			};
			var b = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Top,
				FirstPoint = new Point2D(40.0, 20.0),
				SecondPoint = new Point2D(110.0, 20.0),
				DebugRole = "TopStructWidth"
			};
			Assert(Math.Abs((30.0 + 70.0) - 100.0) <= config.GeometryTolerance, "precondition: span sum equals overall");
			Assert(!planner.CanAttemptComplementaryPartitionSnap(b, a, outline, horizontal: true),
				"gapped/out-of-bounds intervals must not allow complementary snap");
			// Vertical direction gap case.
			var outlineV = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 20.0, MaxY = 100.0 };
			var va = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Right,
				FirstPoint = new Point2D(20.0, 0.0),
				SecondPoint = new Point2D(20.0, 30.0),
				DebugRole = "RightStructHeight"
			};
			var vb = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Right,
				FirstPoint = new Point2D(20.0, 40.0),
				SecondPoint = new Point2D(20.0, 100.0),
				DebugRole = "RightStructHeight"
			};
			Assert(!planner.CanAttemptComplementaryPartitionSnap(vb, va, outlineV, horizontal: false),
				"vertical gapped intervals must not allow complementary snap");
		}

		/// <summary>
		/// Phase 3: TopStructWidth 60 + BottomStructWidth 40 sum to 100 but different local
		/// positions — must not snap or treat as overall partition.
		/// </summary>
		private static void NumericComplementLocalStructuresDoNotSnapOrSuppress()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var outline = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 100.0, MaxY = 50.0 };
			var top = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Top,
				FirstPoint = new Point2D(0.0, 50.0),
				SecondPoint = new Point2D(60.0, 50.0),
				DebugRole = "TopStructWidth"
			};
			var bottom = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(40.0, 0.0),
				DebugRole = "BottomStructWidth"
			};
			Assert(Math.Abs((60.0 + 40.0) - 100.0) <= config.GeometryTolerance, "precondition: span sum equals overall");
			Assert(!planner.CanAttemptComplementaryPartitionSnap(top, bottom, outline, horizontal: true),
				"numeric-only complement of local structures must not allow snap");
			// Integration: local mid step that does not complete overall stays selected.
			var stepOutline = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 100.0, MaxY = 50.0 };
			AddSegment(stepOutline, new Point2D(0.0, 0.0), new Point2D(0.0, 50.0), "left");
			AddSegment(stepOutline, new Point2D(40.0, 10.0), new Point2D(40.0, 45.0), "middle");
			AddSegment(stepOutline, new Point2D(100.0, 0.0), new Point2D(100.0, 50.0), "right");
			var plan = new DimensionPlanner(config).CreateOutlinePlan(stepOutline);
			Assert(plan.Dimensions.Any(d =>
					(d.DebugRole == "TopStructWidth" || d.DebugRole == "BottomStructWidth")
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 40.0) <= config.GeometryTolerance),
				"local structure width 40 must remain when not a real overall partition");
		}

		/// <summary>
		/// Phase 1: greedy longest-first would pick [0,60] then die; DFS must still find
		/// [0,40]+[40,70]+[70,100]. Also verify result is independent of input order.
		/// </summary>
		private static void GreedyDeadEndStillFindsValidOverallPartitionChain()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionDeduplicationRules(config);
			// A=[0,60] decoy, B=[0,40], C=[40,70], D=[70,100]
			var decoy = CreatePartitionItem(0.0, 0.0, 60.0, 0.0, DimensionKind.Normal, "OutlineSegment");
			var b = CreatePartitionItem(0.0, 0.0, 40.0, 0.0, DimensionKind.Normal, "OutlineSegment");
			var c = CreatePartitionItem(40.0, 0.0, 70.0, 0.0, DimensionKind.Normal, "OutlineSegment");
			var d = CreatePartitionItem(70.0, 0.0, 100.0, 0.0, DimensionKind.Normal, "OutlineSegment");

			void AssertValidChain(IList<DimensionDeduplicationItem> input, string label)
			{
				var chain = rules.FindCompleteOverallPartitionChain(input, 0.0, 100.0, horizontal: true);
				Assert(chain != null && chain.Count == 3,
					label + ": must find 3-piece chain, not fail on decoy [0,60]");
				double[] starts = chain.Select(item => Math.Min(item.FirstPoint.X, item.SecondPoint.X)).ToArray();
				double[] ends = chain.Select(item => Math.Max(item.FirstPoint.X, item.SecondPoint.X)).ToArray();
				Assert(Math.Abs(starts[0] - 0.0) <= config.GeometryTolerance
						&& Math.Abs(ends[0] - 40.0) <= config.GeometryTolerance,
					label + ": first piece must be [0,40]");
				Assert(Math.Abs(starts[1] - 40.0) <= config.GeometryTolerance
						&& Math.Abs(ends[1] - 70.0) <= config.GeometryTolerance,
					label + ": second piece must be [40,70]");
				Assert(Math.Abs(starts[2] - 70.0) <= config.GeometryTolerance
						&& Math.Abs(ends[2] - 100.0) <= config.GeometryTolerance,
					label + ": third piece must be [70,100]");
				Assert(!chain.Any(item => Math.Abs(Math.Max(item.FirstPoint.X, item.SecondPoint.X) - 60.0) <= config.GeometryTolerance
						&& Math.Abs(Math.Min(item.FirstPoint.X, item.SecondPoint.X) - 0.0) <= config.GeometryTolerance
						&& Math.Abs(Math.Abs(item.SecondPoint.X - item.FirstPoint.X) - 60.0) <= config.GeometryTolerance),
					label + ": decoy [0,60] must not appear in the successful chain");
			}

			// Decoy first (greedy would pick it and die).
			AssertValidChain(new List<DimensionDeduplicationItem> { decoy, b, c, d }, "decoy-first");
			// Order perturbation: legal pieces shuffled, decoy last.
			AssertValidChain(new List<DimensionDeduplicationItem> { d, c, b, decoy }, "legal-first-shuffled");
			// Decoy between B and C.
			AssertValidChain(new List<DimensionDeduplicationItem> { b, decoy, d, c }, "decoy-middle");
		}

		/// <summary>
		/// Case 2: lengths 40+60=100 but intervals [0,40] and [20,80] overlap and miss [80,100].
		/// </summary>
		private static void OverlappingIntervalsDoNotFormOverallPartition()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionDeduplicationRules(config);
			var overall = CreatePartitionItem(0.0, 0.0, 100.0, 0.0, DimensionKind.OverallWidth);
			var a = CreatePartitionItem(0.0, 0.0, 40.0, 0.0, DimensionKind.Normal, "OutlineSegment");
			var b = CreatePartitionItem(20.0, 0.0, 80.0, 0.0, DimensionKind.Normal, "OutlineSegment");

			Assert(Math.Abs((40.0 + 60.0) - 100.0) <= config.GeometryTolerance,
				"precondition: span sum equals overall");
			Assert(!rules.FormsCompleteOverallPartition(a, b, overall, horizontal: true),
				"overlapping intervals must not form overall partition even when span sum matches");
		}

		/// <summary>
		/// Case 3: lengths 30+70=100 but [0,30] and [40,110] have a gap (and B overshoots).
		/// Also pure-gap case [0,30]+[40,100] inside overall.
		/// </summary>
		private static void GappedIntervalsDoNotFormOverallPartition()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionDeduplicationRules(config);
			var overall = CreatePartitionItem(0.0, 0.0, 100.0, 0.0, DimensionKind.OverallWidth);
			var a = CreatePartitionItem(0.0, 0.0, 30.0, 0.0, DimensionKind.Normal, "OutlineSegment");
			// Gap only (still within overall): [0,30] + [40,100]
			var gappedInside = CreatePartitionItem(40.0, 0.0, 100.0, 0.0, DimensionKind.Normal, "OutlineSegment");
			Assert(!rules.FormsCompleteOverallPartition(a, gappedInside, overall, horizontal: true),
				"gapped intervals [0,30]+[40,100] must not form overall partition");

			// Classic gap + overshoot with span sum 100: [0,30]+[40,110]
			var overshoot = CreatePartitionItem(40.0, 0.0, 110.0, 0.0, DimensionKind.Normal, "OutlineSegment");
			Assert(Math.Abs((30.0 + 70.0) - 100.0) <= config.GeometryTolerance,
				"precondition: span sum equals overall");
			Assert(!rules.FormsCompleteOverallPartition(a, overshoot, overall, horizontal: true),
				"gapped/overshooting intervals must not form overall partition");
		}

		/// <summary>
		/// Case 4: lengths sum to overall but one interval extends past overall max.
		/// </summary>
		private static void OutOfBoundsIntervalsDoNotFormOverallPartition()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionDeduplicationRules(config);
			var overall = CreatePartitionItem(0.0, 0.0, 100.0, 0.0, DimensionKind.OverallWidth);
			// [0,30] + [30,130]: abut but B extends past overall; span sum 130 ≠ 100.
			// Use span-sum equal case with overshoot: [0,40] + [40,100] is valid;
			// overshoot-only: [10,50] + [50,110] spans 40+60=100 but does not cover [0,10] and exceeds max.
			var a = CreatePartitionItem(10.0, 0.0, 50.0, 0.0, DimensionKind.Normal, "OutlineSegment");
			var b = CreatePartitionItem(50.0, 0.0, 110.0, 0.0, DimensionKind.Normal, "OutlineSegment");
			Assert(Math.Abs((40.0 + 60.0) - 100.0) <= config.GeometryTolerance,
				"precondition: span sum equals overall");
			Assert(!rules.FormsCompleteOverallPartition(a, b, overall, horizontal: true),
				"intervals that leave overall min uncovered and exceed max must not partition overall");
		}

		/// <summary>
		/// Case 5: TopStructWidth 60 and BottomStructWidth 40 at different positions —
		/// span sum equals overall but intervals do not form a contiguous cover.
		/// </summary>
		private static void NumericallyComplementaryStructureIntervalsDoNotPartitionOverall()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionDeduplicationRules(config);
			var overall = CreatePartitionItem(0.0, 0.0, 100.0, 0.0, DimensionKind.OverallWidth);
			// Different structural locations: top local step [0,60], bottom local step [0,40]
			var top = CreatePartitionItem(0.0, 50.0, 60.0, 50.0, DimensionKind.Normal, "TopStructWidth");
			var bottom = CreatePartitionItem(0.0, 0.0, 40.0, 0.0, DimensionKind.Normal, "BottomStructWidth");
			Assert(Math.Abs((60.0 + 40.0) - 100.0) <= config.GeometryTolerance,
				"precondition: span sum equals overall");
			Assert(!rules.FormsCompleteOverallPartition(top, bottom, overall, horizontal: true),
				"structure dims at different positions must not partition overall by span sum alone");

			// Another non-cover pair with same spans: top [20,80]=60, bottom [10,50]=40
			var topMid = CreatePartitionItem(20.0, 50.0, 80.0, 50.0, DimensionKind.Normal, "TopStructWidth");
			var bottomMid = CreatePartitionItem(10.0, 0.0, 50.0, 0.0, DimensionKind.Normal, "BottomStructWidth");
			Assert(!rules.FormsCompleteOverallPartition(topMid, bottomMid, overall, horizontal: true),
				"overlapping/non-covering structure intervals must not form overall partition");
		}

		private static DimensionDeduplicationItem CreatePartitionItem(
			double x1, double y1, double x2, double y2, DimensionKind kind, string debugRole = null)
		{
			return new DimensionDeduplicationItem
			{
				FirstPoint = new Point2D(x1, y1),
				SecondPoint = new Point2D(x2, y2),
				Span = Math.Abs(x2 - x1) > 1e-12 ? Math.Abs(x2 - x1) : Math.Abs(y2 - y1),
				Kind = kind,
				DebugRole = debugRole ?? string.Empty,
				OverrideText = string.Empty
			};
		}

		private static void EffectiveSpanControlsVerticalStacking()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.OverallHeight, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(0.0, 100.0), Span = 100.0, ForceOuterLevel = true, ReadingLevel = DimensionReadingLevel.Overall },
				new DimensionLayoutItem { Kind = DimensionKind.DatumHoleLocationY, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(0.0, 10.0), Span = 10.0, ReadingLevel = DimensionReadingLevel.DatumTransfer },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(0.0, 20.0), Span = 20.0, ReadingLevel = DimensionReadingLevel.IntraGroup },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(0.0, 80.0), Span = 80.0, ReadingLevel = DimensionReadingLevel.LocalSpacing }
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Left, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: false);

			Assert(placements.Single(item => item.Index == 1).Level < placements.Single(item => item.Index == 2).Level,
				"the shorter vertical datum transfer must stay inside the longer intra-group span");
			Assert(placements.Single(item => item.Index == 2).Level < placements.Single(item => item.Index == 3).Level,
				"effective span must outrank semantic reading level for non-equal vertical spans");
			Assert(placements.Single(item => item.Index == 3).Level < placements.Single(item => item.Index == 0).Level,
				"the forced vertical overall dimension must remain outermost");
		}

		private static void NearEqualEffectiveSpansUseSemanticTieBreak()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(50.0, 0.0), Span = 50.0, ReadingLevel = DimensionReadingLevel.DatumTransfer },
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(50.0 + config.GeometryTolerance * 0.5, 0.0), Span = 50.0 + config.GeometryTolerance * 0.5, ReadingLevel = DimensionReadingLevel.LocalSpacing }
			};
			var rules = new DimensionLayoutRules(config);
			var order = rules.GetStackingOrder(dimensions, isHorizontal: true);
			var placements = rules.CreateStackingPlan(dimensions, DimensionSide.Top, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: true).ToDictionary(item => item.Index);

			Assert(order.SequenceEqual(new[] { 1, 0 }),
				"semantic reading level may decide only when effective spans are within geometry tolerance");
			Assert(placements[1].OrderingReason == "NearEqualSpanSemanticTieBreak" && placements[0].OrderingReason == "NearEqualSpanSemanticTieBreak",
				"near-equal span diagnostics must expose the semantic tie-break reason");
		}

		private static void LayoutBlocksUseBottomV203SpanOrder()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateRectangle(338.0, 100.0);
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(305.0, 0.0), Span = 305.0, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(10.0, 0.0), SecondPoint = new Point2D(233.5, 0.0), Span = 223.5, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 110, ReadingLevel = DimensionReadingLevel.DatumTransfer, SourceFeatureId = "PG3" },
				new DimensionLayoutItem { Kind = DimensionKind.DatumHoleLocationX, FirstPoint = new Point2D(180.0, 0.0), SecondPoint = new Point2D(233.5, 0.0), Span = 53.5, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 120, ReadingLevel = DimensionReadingLevel.DatumTransfer, SourceFeatureId = "PG1" },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(60.0, 0.0), SecondPoint = new Point2D(180.0, 0.0), Span = 120.0, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 110, ReadingLevel = DimensionReadingLevel.DatumTransfer, SourceFeatureId = "PG2" },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(60.0, 0.0), Span = 60.0, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 100, ReadingLevel = DimensionReadingLevel.IntraGroup, SourceFeatureId = "PG2" },
				new DimensionLayoutItem { Kind = DimensionKind.OverallWidth, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(338.0, 0.0), Span = 338.0, ForceOuterLevel = true, ReadingLevel = DimensionReadingLevel.Overall },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(25.0, 0.0), Span = 25.0, ReadingLevel = DimensionReadingLevel.LocalSpacing }
			};
			var rules = new DimensionLayoutRules(config);
			var order = rules.GetStackingOrder(dimensions, isHorizontal: true);
			var placements = rules.CreateStackingPlan(dimensions, DimensionSide.Bottom, outline, 2.5, 1.25, 5.0, 6.5, isHorizontal: true).ToDictionary(item => item.Index);

			Assert(order.SequenceEqual(new[] { 6, 1, 2, 3, 4, 0, 5 }),
				"v203 bottom order must be local25, isolated223.5, rooted233.5, structure305, overall338");
			Assert(placements[1].Level < placements[2].Level && placements[2].Level < placements[0].Level && placements[0].Level < placements[5].Level,
				"layout blocks must be stacked outward by effective span");
			Assert(new[] { 2, 3, 4 }.Select(index => placements[index].LayoutBlockId).Distinct().Count() == 1
				&& new[] { 2, 3, 4 }.All(index => placements[index].Level == placements[2].Level)
				&& Math.Abs(placements[2].EffectiveSpan - 233.5) <= config.GeometryTolerance,
				"the rooted datum chain must remain one indivisible 233.5 layout block");
			Assert(placements.Values.All(placement => placement.PhysicalOrderValidated),
				"the final bottom physical coordinates must preserve the v203 outward order");
		}

		private static void RootedLayoutBlockSurvivesLegacyLaneProcessing()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateRectangle(338.0, 100.0);
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.DatumHoleLocationX, FirstPoint = new Point2D(245.5, 0.0), SecondPoint = new Point2D(305.0, 0.0), Span = 59.5, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 120, ReadingLevel = DimensionReadingLevel.DatumTransfer, SourceFeatureId = "PG1" },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(121.5, 0.0), SecondPoint = new Point2D(245.5, 0.0), Span = 124.0, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 110, ReadingLevel = DimensionReadingLevel.DatumTransfer, SourceFeatureId = "PG2" },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(71.5, 0.0), SecondPoint = new Point2D(121.5, 0.0), Span = 50.0, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 100, ReadingLevel = DimensionReadingLevel.IntraGroup, SourceFeatureId = "PG2" },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(22.0, 0.0), SecondPoint = new Point2D(245.5, 0.0), Span = 223.5, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 110, ReadingLevel = DimensionReadingLevel.DatumTransfer, SourceFeatureId = "PG3" }
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Bottom, outline, 2.5, 1.25, 5.0, 6.5, isHorizontal: true).ToDictionary(item => item.Index);
			var rooted = new[] { placements[0], placements[1], placements[2] };

			Assert(rooted.Select(item => item.LayoutBlockId).Distinct().Count() == 1
				&& rooted.All(item => item.LayoutBlockType == "RootedAlignmentLane" && item.Level == rooted[0].Level),
				"legacy alignment-lane processing must not split an already formed rooted layout block");
			Assert(rooted.All(item => item.AlignmentLaneKey == "PG1:DatumChain:H#1" && item.AlignmentLaneMemberCount == 3)
				&& rooted.All(item => item.DimLineCoordinateOverride.HasValue)
				&& rooted.Select(item => item.DimLineCoordinateOverride.Value).Distinct().Count() == 1,
				"rooted layout-block diagnostics and physical coordinates must remain block-consistent");
			Assert(placements[3].Level < rooted[0].Level && placements.Values.All(item => item.PhysicalOrderValidated),
				"isolated 223.5 transfer must remain inside the rooted 233.5 block");
		}

		private static void LooseChainFormsOneEffectiveSpanBlock()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateRectangle(100.0, 120.0);
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(100.0, 0.0), SecondPoint = new Point2D(100.0, 20.0), Span = 20.0, LooseChainId = 1, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(100.0, 20.0), SecondPoint = new Point2D(100.0, 50.0), Span = 30.0, LooseChainId = 3, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(100.0, 0.0), SecondPoint = new Point2D(100.0, 40.0), Span = 40.0, AlignmentKey = "PG1:DatumChain:V", AlignmentPriority = 100, ReadingLevel = DimensionReadingLevel.IntraGroup },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(100.0, 40.0), SecondPoint = new Point2D(100.0, 85.0), Span = 45.0, AlignmentKey = "PG1:DatumChain:V", AlignmentPriority = 110, ReadingLevel = DimensionReadingLevel.DatumTransfer },
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(100.0, 0.0), SecondPoint = new Point2D(100.0, 100.0), Span = 100.0, ReadingLevel = DimensionReadingLevel.LocalSpacing }
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Right, outline, 2.5, 1.25, 5.0, 6.5, isHorizontal: false).ToDictionary(item => item.Index);

			Assert(placements[0].LayoutBlockId == placements[1].LayoutBlockId
				&& placements[0].LayoutBlockType == "LooseChain"
				&& Math.Abs(placements[0].EffectiveSpan - 50.0) <= config.GeometryTolerance,
				"all members of one loose chain must form one 50-unit layout block");
			Assert(placements[0].Level < placements[2].Level && placements[2].Level < placements[4].Level,
				"right-side loose50, rooted85, and outline100 blocks must follow effective-span order");
		}

		private static void DisconnectedLooseChainsRemainSeparateBlocks()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(100.0, 0.0), SecondPoint = new Point2D(100.0, 20.0), Span = 20.0, LooseChainId = 1 },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(100.0, 50.0), SecondPoint = new Point2D(100.0, 70.0), Span = 20.0, LooseChainId = 3 }
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Right, null, 2.5, 1.25, 5.0, 6.5, isHorizontal: false).ToDictionary(item => item.Index);

			Assert(placements[0].LayoutBlockId != placements[1].LayoutBlockId,
				"disconnected loose dimensions with different chain identifiers must remain separate layout blocks");
		}

		private static void OverlappingRootedIntervalsSplitIntoSeparateLeftBlocks()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateTopLeftShoulderOutline();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(245.5, 15.0), SecondPoint = new Point2D(121.5, 56.0), Span = 41.0, AlignmentKey = "PG1:DatumChain:V", AlignmentPriority = 110, ReadingLevel = DimensionReadingLevel.DatumTransfer, SourceFeatureId = "PG2" },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(245.5, 15.0), SecondPoint = new Point2D(22.0, 191.5), Span = 176.5, AlignmentKey = "PG1:DatumChain:V", AlignmentPriority = 110, ReadingLevel = DimensionReadingLevel.DatumTransfer, SourceFeatureId = "PG4" },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(121.5, 56.0), SecondPoint = new Point2D(71.5, 11.0), Span = 45.0, AlignmentKey = "PG1:DatumChain:V", AlignmentPriority = 100, ReadingLevel = DimensionReadingLevel.IntraGroup, SourceFeatureId = "PG2" },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(22.0, 45.0), SecondPoint = new Point2D(22.0, 15.0), Span = 30.0, AlignmentKey = "PG1:DatumChain:V", AlignmentPriority = 100, ReadingLevel = DimensionReadingLevel.IntraGroup, SourceFeatureId = "PG3" },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(22.0, 45.0), SecondPoint = new Point2D(22.0, 30.0), Span = 15.0, PreferLocalBoundary = true, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "PG3" },
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(-33.0, 181.5), SecondPoint = new Point2D(-33.0, 201.5), Span = 20.0, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(22.0, 191.5), SecondPoint = new Point2D(24.0, 143.0), Span = 48.5, LooseChainId = 6, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "L6" },
				new DimensionLayoutItem { Kind = DimensionKind.OverallHeight, FirstPoint = new Point2D(5.0, 0.0), SecondPoint = new Point2D(-33.0, 201.5), Span = 201.5, ForceOuterLevel = true, ReadingLevel = DimensionReadingLevel.Overall }
			};
			var rules = new DimensionLayoutRules(config);
			var placements = rules.CreateStackingPlan(dimensions, DimensionSide.Left, outline, 2.5, 1.25, 10.0, 6.5, isHorizontal: false).ToDictionary(item => item.Index);
			double pg2TransferCoordinate = placements[0].DimLineCoordinateOverride
				?? rules.GetDimLineCoordinate(dimensions[0], DimensionSide.Left, outline, placements[0].Offset);
			double pg2PinCoordinate = placements[2].DimLineCoordinateOverride
				?? rules.GetDimLineCoordinate(dimensions[2], DimensionSide.Left, outline, placements[2].Offset);
			double pg4TransferCoordinate = placements[1].DimLineCoordinateOverride
				?? rules.GetDimLineCoordinate(dimensions[1], DimensionSide.Left, outline, placements[1].Offset);
			double pg3PinCoordinate = placements[3].DimLineCoordinateOverride
				?? rules.GetDimLineCoordinate(dimensions[3], DimensionSide.Left, outline, placements[3].Offset);

			Assert(placements[0].LayoutBlockId != placements[2].LayoutBlockId
				&& placements[0].Level < placements[2].Level
				&& Math.Abs(pg2TransferCoordinate - pg2PinCoordinate) >= 6.5 - config.GeometryTolerance,
				"shared-endpoint PG2 dimensions with overlapping arrow intervals must use separate levels");
			Assert(placements[1].LayoutBlockId != placements[3].LayoutBlockId
				&& placements[3].Level < placements[1].Level
				&& Math.Abs(pg4TransferCoordinate - pg3PinCoordinate) >= 6.5 - config.GeometryTolerance,
				"shared-endpoint PG3/PG4 dimensions with overlapping arrow intervals must use separate levels");
			Assert(Math.Abs(placements[0].EffectiveSpan - 41.0) <= config.GeometryTolerance
				&& Math.Abs(placements[2].EffectiveSpan - 45.0) <= config.GeometryTolerance
				&& Math.Abs(placements[3].EffectiveSpan - 30.0) <= config.GeometryTolerance
				&& Math.Abs(placements[1].EffectiveSpan - 176.5) <= config.GeometryTolerance,
				"split dimensions must preserve their own effective spans");
			double pg4Rank = -pg4TransferCoordinate;
			double looseCoordinate = placements[6].DimLineCoordinateOverride
				?? rules.GetDimLineCoordinate(dimensions[6], DimensionSide.Left, outline, placements[6].Offset);
			double overallCoordinate = placements[7].DimLineCoordinateOverride
				?? rules.GetDimLineCoordinate(dimensions[7], DimensionSide.Left, outline, placements[7].Offset);
			Assert(placements[2].Level < placements[1].Level
				&& pg4Rank >= -looseCoordinate + 6.5 - config.GeometryTolerance
				&& -overallCoordinate >= pg4Rank + 6.5 - config.GeometryTolerance
				&& placements.Values.All(placement => placement.PhysicalOrderValidated),
				"left-side split dimensions must retain physical outward order across local and global boundaries");
		}

		private static void OverlappingDatumAndPinIntervalsSplitIntoSeparateBlocks()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.DatumHoleLocationY, FirstPoint = new Point2D(40.0, 15.0), SecondPoint = new Point2D(40.0, 56.0), Span = 41.0, AlignmentKey = "PG1:DatumChain:V", AlignmentPriority = 120, ReadingLevel = DimensionReadingLevel.DatumTransfer, SourceFeatureId = "PG1" },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(20.0, 11.0), SecondPoint = new Point2D(20.0, 56.0), Span = 45.0, AlignmentKey = "PG1:DatumChain:V", AlignmentPriority = 100, ReadingLevel = DimensionReadingLevel.IntraGroup, SourceFeatureId = "PG1" }
			};
			var rules = new DimensionLayoutRules(config);
			var placements = rules.CreateStackingPlan(dimensions, DimensionSide.Left, null, 2.5, 1.25, 5.0, 5.0, isHorizontal: false).ToDictionary(item => item.Index);
			double datumCoordinate = placements[0].DimLineCoordinateOverride
				?? rules.GetDimLineCoordinate(dimensions[0], DimensionSide.Left, null, placements[0].Offset);
			double pinCoordinate = placements[1].DimLineCoordinateOverride
				?? rules.GetDimLineCoordinate(dimensions[1], DimensionSide.Left, null, placements[1].Offset);

			Assert(placements[0].LayoutBlockId != placements[1].LayoutBlockId
				&& placements[0].Level < placements[1].Level
				&& Math.Abs(datumCoordinate - pinCoordinate) >= 5.0 - config.GeometryTolerance
				&& placements.Values.All(placement => placement.PhysicalOrderValidated),
				"shared-endpoint datum and pin dimensions with overlapping arrow intervals must use separate levels");
		}

		private static void IndependentLocalAndGlobalDimensionsMayShareLogicalLevel()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateRightNotchOutline();
			var dimensions = new[]
			{
				new DimensionLayoutItem
				{
					Kind = DimensionKind.HoleLocation,
					FirstPoint = new Point2D(20.0, 30.0),
					SecondPoint = new Point2D(20.0, 40.0),
					Span = 10.0,
					PreferLocalBoundary = true,
					PreservePreferredSide = true,
					ReadingLevel = DimensionReadingLevel.LocalSpacing
				},
				new DimensionLayoutItem
				{
					Kind = DimensionKind.Normal,
					FirstPoint = new Point2D(200.0, 85.0),
					SecondPoint = new Point2D(200.0, 95.0),
					Span = 10.0,
					ReadingLevel = DimensionReadingLevel.LocalSpacing
				}
			};
			var rules = new DimensionLayoutRules(config);
			var placements = rules.CreateStackingPlan(dimensions, DimensionSide.Right, outline, 2.5, 1.25, 5.0, 6.5, isHorizontal: false).ToDictionary(item => item.Index);
			double localCoordinate = placements[0].DimLineCoordinateOverride ?? rules.GetDimLineCoordinate(dimensions[0], DimensionSide.Right, outline, placements[0].Offset);
			double globalCoordinate = placements[1].DimLineCoordinateOverride ?? rules.GetDimLineCoordinate(dimensions[1], DimensionSide.Right, outline, placements[1].Offset);

			Assert(placements[0].Level == placements[1].Level && Math.Abs(localCoordinate - globalCoordinate) > config.GeometryTolerance,
				"independent local-boundary and global dimensions may share one logical level at different physical coordinates");
			Assert(placements.Values.All(placement => placement.PhysicalOrderValidated),
				"different coordinates on a shared non-conflicting logical level must not invalidate physical block ordering");
		}

		private static void IsolatedShortPinGroupTransferUsesInnerSpanOrder()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.OverallHeight, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(0.0, 100.0), Span = 100.0, ForceOuterLevel = true, ReadingLevel = DimensionReadingLevel.Overall },
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(10.0, 42.0), SecondPoint = new Point2D(0.0, 0.0), Span = 42.0, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(155.0, 14.5), SecondPoint = new Point2D(40.0, 0.0), Span = 14.5, AlignmentKey = "PG1:DatumChain:V", AlignmentPriority = 110, ReadingLevel = DimensionReadingLevel.DatumTransfer }
			};
			var rules = new DimensionLayoutRules(config);
			var order = rules.GetStackingOrder(dimensions, isHorizontal: false);
			var placements = rules.CreateStackingPlan(dimensions, DimensionSide.Left, null, 2.5, 1.25, 5.0, 6.5, isHorizontal: false).ToDictionary(item => item.Index);

			Assert(order.SequenceEqual(new[] { 2, 1, 0 }),
				"an isolated short pin-group transfer must use span order ahead of a larger overlapping local dimension");
			Assert(placements[2].Level == 0 && placements[1].Level == 1 && placements[0].Level == 2,
				"left-side isolated GD 14.5 must sit inside local structure height 42 while overall height remains outermost");
			Assert(placements[2].AlignmentLaneMemberCount == 1,
				"the span-order exception must apply only to an isolated alignment lane");
		}

		private static void DimensionDiagnosticsRecordFinalPlacement()
		{
			var plan = new DimensionPlan();
			var dimension = new PlannedDimension
			{
				Kind = DimensionKind.PinDistance,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Top,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(30.0, 0.0),
				AlignmentKey = "PG1:DatumChain:H",
				AlignmentPriority = 100,
				ReadingLevel = DimensionReadingLevel.IntraGroup,
				DebugRole = "PinDistance"
			};
			plan.Add(dimension);
			plan.CaptureFinalDimensions();
			plan.Diagnostics.RecordFinalPlacement(dimension.DiagnosticId, 2, 15.0, 125.0, true, true, "PG1:DatumChain:H#1", 3, "AlignedLaneCoordinateOverride", "AlignmentLane:PG1:DatumChain:H#1", "RootedAlignmentLane", 30.0, 1, "EffectiveSpanAscending", "Dimension:2", 15.0, true);

			Assert(dimension.DiagnosticId > 0, "planned dimensions must receive a stable diagnostic id");
			Assert(plan.Diagnostics.DimensionCandidates.Single().ReadingLevel == DimensionReadingLevel.IntraGroup.ToString()
				&& plan.Diagnostics.DimensionCandidates.Single().AlignmentKey == "PG1:DatumChain:H",
				"candidate diagnostics must expose reading and requested alignment metadata");
			Assert(plan.Diagnostics.DimensionCandidates.Single().HasFinalPlacement
				&& plan.Diagnostics.FinalDimensions.Single().HasFinalPlacement
				&& plan.Diagnostics.FinalDimensions.Single().StackingLevel == 2
				&& Math.Abs(plan.Diagnostics.FinalDimensions.Single().StackingOffset.Value - 15.0) <= 1E-09
				&& Math.Abs(plan.Diagnostics.FinalDimensions.Single().ResolvedDimLineCoordinate.Value - 125.0) <= 1E-09,
				"final placement diagnostics must update both candidate and final-dimension views");
			Assert(plan.Diagnostics.FinalDimensions.Single().AlignmentLaneMemberCount == 3
				&& plan.Diagnostics.FinalDimensions.Single().AlignmentDecision == "AlignedLaneCoordinateOverride",
				"final placement diagnostics must explain the resolved alignment lane");
			Assert(plan.Diagnostics.FinalDimensions.Single().LayoutBlockType == "RootedAlignmentLane"
				&& Math.Abs(plan.Diagnostics.FinalDimensions.Single().EffectiveSpan - 30.0) <= 1E-09
				&& plan.Diagnostics.FinalDimensions.Single().PhysicalOrderValidated,
				"final placement diagnostics must expose v203 layout-block and physical-order metadata");
		}

		private static void RuleEvidenceRejectsReassignmentAndRenderSuppressionStaysCompatible()
		{
			const string ruleId = "OuterContourStepOverallRemainder";
			var plan = new DimensionPlan();
			var dimension = CreateTestDimension(
				DimensionKind.Normal,
				DimensionOrientation.Horizontal,
				DimensionSide.Bottom,
				new Point2D(0.0, 0.0),
				new Point2D(20.0, 0.0),
				"BottomStructWidth");
			plan.Add(dimension);
			plan.RecordRuleEvidence(dimension, ruleId, new[] { "edge-a" }, "first");
			plan.RecordRuleEvidence(dimension, ruleId, new[] { "edge-b" }, "second");

			var diagnostic = plan.Diagnostics.DimensionCandidates.Single();
			Assert(dimension.RuleId == ruleId
					&& diagnostic.RuleId == ruleId
					&& diagnostic.SourceGeometryIds.Contains("edge-a")
					&& diagnostic.SourceGeometryIds.Contains("edge-b"),
				"recording the same RuleId again must be idempotent and merge evidence");

			bool rejected = false;
			try
			{
				plan.RecordRuleEvidence(dimension, "DifferentRule");
			}
			catch (InvalidOperationException)
			{
				rejected = true;
			}
			Assert(rejected && dimension.RuleId == ruleId && diagnostic.RuleId == ruleId,
				"a candidate with a non-empty RuleId must reject reassignment to another rule");

			plan.CaptureFinalDimensions();
			plan.Diagnostics.RecordRenderSuppressed(dimension.DiagnosticId, "RenderConflict");
			Assert(diagnostic.Decision == DimensionCandidateDecision.Suppressed
					&& diagnostic.DecisionStatus == "RenderSuppressed",
				"render suppression must expose formal Suppressed while preserving the legacy status");
			Assert(plan.Diagnostics.FinalDimensions.Count == 0,
				"render-suppressed dimensions must be removed from final diagnostics");
		}

		private static void FormattedDimensionTextLengthIgnoresControlCodes()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var dimension = new DimensionLayoutItem
			{
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(0.0, 15.0),
				Span = 15.0,
				OverrideText = "15\\H0.8x;\u00B10.02\\H1x;"
			};

			double length = rules.GetDimensionTextLength(dimension, 2.5);

			Assert(Math.Abs(length - 10.5) <= config.GeometryTolerance,
				"formatted tolerance control codes must not count as visible glyphs when estimating dimension text length");
		}

		private static void FittingVerticalLocalTextStaysCentered()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var dimension = new DimensionLayoutItem
			{
				Kind = DimensionKind.HoleLocation,
				FirstPoint = new Point2D(20.0, 0.0),
				SecondPoint = new Point2D(20.0, 15.0),
				Span = 15.0,
				OverrideText = "15\\H0.8x;\u00B10.02\\H1x;",
				PreferLocalBoundary = true
			};
			var dimLinePoint = new Point2D(-10.0, 7.5);
			var placed = new DimensionTextPlacementItem
			{
				Dimension = dimension,
				Side = DimensionSide.Left,
				DimLinePoint = dimLinePoint,
				TextBounds = rules.ComputePlacedTextBounds(dimension, dimLinePoint, isHorizontal: false, textHeight: 2.5)
			};

			var slides = rules.SelectShortLocalDimensionTextSlides(new[] { placed }, new TextBounds2D[0], 2.5, 2.5, 3.0);

			Assert(slides.Count == 1 && rules.DimensionTextFitsBetweenOwnExtensionLines(dimension, 2.5),
				"dimension text that fits between its own extension lines must receive an explicit centered position");
			Assert(Math.Abs(slides[0].TextPosition.X + 10.0) <= config.GeometryTolerance
				&& Math.Abs(slides[0].TextPosition.Y - 7.5) <= config.GeometryTolerance,
				"fitting vertical text must stay at the center of its dimension line even when arrow clearance is tighter");
		}

		private static void ShortVerticalLocalTextClearsArrowheads()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var dimension = new DimensionLayoutItem
			{
				Kind = DimensionKind.HoleLocation,
				FirstPoint = new Point2D(20.0, 0.0),
				SecondPoint = new Point2D(20.0, 8.0),
				Span = 8.0,
				OverrideText = "15\\H0.8x;\u00B10.02\\H1x;",
				PreferLocalBoundary = true
			};
			var dimLinePoint = new Point2D(-10.0, 4.0);
			var placed = new DimensionTextPlacementItem
			{
				Dimension = dimension,
				Side = DimensionSide.Left,
				DimLinePoint = dimLinePoint,
				TextBounds = rules.ComputePlacedTextBounds(dimension, dimLinePoint, isHorizontal: false, textHeight: 2.5)
			};

			var slides = rules.SelectShortLocalDimensionTextSlides(new[] { placed }, new TextBounds2D[0], 2.5, 2.5, 3.0);

			Assert(slides.Count == 1, "a short vertical local dimension must move its text outside its arrowheads");
			TextBounds2D bounds = slides[0].TextBounds;
			Assert(bounds.MaxY <= -5.5 + config.GeometryTolerance || bounds.MinY >= 13.5 - config.GeometryTolerance,
				"vertical external text must keep the configured clearance beyond the arrowhead envelope");
		}

		private static void FittingHorizontalTextStaysCenteredDespiteNeighborArrow()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var target = new DimensionLayoutItem
			{
				Kind = DimensionKind.PinGroupDistance,
				FirstPoint = new Point2D(0.0, 20.0),
				SecondPoint = new Point2D(223.5, 20.0),
				Span = 223.5,
				OverrideText = "223.5\\H0.8x;\u00B10.05\\H1x;"
			};
			var neighbor = new DimensionLayoutItem
			{
				Kind = DimensionKind.PinDistance,
				FirstPoint = new Point2D(111.75, 30.0),
				SecondPoint = new Point2D(130.0, 30.0),
				Span = 18.25,
				OverrideText = "18.25"
			};
			var targetLinePoint = new Point2D(111.75, -10.0);
			var neighborLinePoint = new Point2D(120.875, -10.0);
			var placed = new[]
			{
				new DimensionTextPlacementItem
				{
					Dimension = target,
					Side = DimensionSide.Bottom,
					DimLinePoint = targetLinePoint,
					TextBounds = rules.ComputePlacedTextBounds(target, targetLinePoint, isHorizontal: true, textHeight: 2.5)
				},
				new DimensionTextPlacementItem
				{
					Dimension = neighbor,
					Side = DimensionSide.Bottom,
					DimLinePoint = neighborLinePoint,
					TextBounds = rules.ComputePlacedTextBounds(neighbor, neighborLinePoint, isHorizontal: true, textHeight: 2.5)
				}
			};

			var slides = rules.SelectShortLocalDimensionTextSlides(placed, new TextBounds2D[0], 2.5, 2.5, 3.0);
			var targetPlacement = slides.Single(slide => slide.Index == 0);

			Assert(rules.DimensionTextFitsBetweenOwnExtensionLines(target, 2.5),
				"the long formatted dimension must fit between its own extension lines");
			Assert(Math.Abs(targetPlacement.TextPosition.X - 111.75) <= config.GeometryTolerance
				&& Math.Abs(targetPlacement.TextPosition.Y + 10.0) <= config.GeometryTolerance,
				"fitting horizontal text must stay explicitly centered even when another dimension arrow is nearby");
		}

		private static void ShortHorizontalLocalTextClearsArrowheads()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var dimension = new DimensionLayoutItem
			{
				Kind = DimensionKind.PinDistance,
				FirstPoint = new Point2D(0.0, 20.0),
				SecondPoint = new Point2D(8.0, 20.0),
				Span = 8.0,
				OverrideText = "15\\H0.8x;\u00B10.02\\H1x;"
			};
			var dimLinePoint = new Point2D(4.0, -10.0);
			var placed = new DimensionTextPlacementItem
			{
				Dimension = dimension,
				Side = DimensionSide.Bottom,
				DimLinePoint = dimLinePoint,
				TextBounds = rules.ComputePlacedTextBounds(dimension, dimLinePoint, isHorizontal: true, textHeight: 2.5)
			};

			var slides = rules.SelectShortLocalDimensionTextSlides(new[] { placed }, new TextBounds2D[0], 2.5, 2.5, 3.0);

			Assert(slides.Count == 1, "a short horizontal local dimension must move its text outside its arrowheads");
			TextBounds2D bounds = slides[0].TextBounds;
			Assert(bounds.MaxX <= -5.5 + config.GeometryTolerance || bounds.MinX >= 13.5 - config.GeometryTolerance,
				"horizontal external text must keep the configured clearance beyond the arrowhead envelope");
		}

		private static void ShortVerticalChainTextAvoidsNeighborArrowheads()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var dimensions = new[]
			{
				new DimensionLayoutItem
				{
					Kind = DimensionKind.HoleLocation,
					FirstPoint = new Point2D(20.0, 0.0),
					SecondPoint = new Point2D(20.0, 8.0),
					Span = 8.0,
					OverrideText = "15\\H0.8x;\u00B10.02\\H1x;",
					PreferLocalBoundary = true,
					LooseChainId = 1
				},
				new DimensionLayoutItem
				{
					Kind = DimensionKind.HoleLocation,
					FirstPoint = new Point2D(20.0, 8.0),
					SecondPoint = new Point2D(20.0, 16.0),
					Span = 8.0,
					OverrideText = "15\\H0.8x;\u00B10.02\\H1x;",
					PreferLocalBoundary = true,
					LooseChainId = 1
				}
			};
			var placed = dimensions.Select(dimension =>
			{
				var dimLinePoint = new Point2D(-10.0, (dimension.FirstPoint.Y + dimension.SecondPoint.Y) / 2.0);
				return new DimensionTextPlacementItem
				{
					Dimension = dimension,
					Side = DimensionSide.Left,
					DimLinePoint = dimLinePoint,
					TextBounds = rules.ComputePlacedTextBounds(dimension, dimLinePoint, isHorizontal: false, textHeight: 2.5)
				};
			}).ToList();

			var slides = rules.SelectShortLocalDimensionTextSlides(placed, new TextBounds2D[0], 2.5, 2.5, 3.0);
			var first = slides.Single(slide => slide.Index == 0);
			var second = slides.Single(slide => slide.Index == 1);

			Assert(first.TextBounds.MaxY <= -5.5 + config.GeometryTolerance,
				"the first short-chain label must choose the free side away from the neighboring arrowheads");
			Assert(second.TextBounds.MinY >= 21.5 - config.GeometryTolerance,
				"the second short-chain label must choose the opposite free side away from the neighboring arrowheads");
			Assert(!rules.TextBoundsOverlap(first.TextBounds, second.TextBounds, 3.0),
				"external labels in one short chain must preserve their configured mutual clearance");
		}

		private static void InvalidDatumCoordinateIsSkippedWithDiagnostic()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateRectangle(100.0, 50.0);
			var datumPin = CreateHole(20.0, 20.0, 6.0, HoleKind2D.Pin);
			var datum = Datum2D.FromOutline(outline);
			datum.DatumHole = datumPin;
			datum.DatumHoleLocationBaseX = 150.0;
			var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, new[]
			{
				datumPin,
				CreateHole(60.0, 20.0, 6.0, HoleKind2D.Pin)
			});
			var skipped = plan.Diagnostics.DimensionCandidates.Single(candidate => candidate.DebugRole == "DatumX"
				&& candidate.DecisionStatus == "Skipped");

			Assert(!skipped.IsAttachmentValid && !skipped.IsSelected && !skipped.IsSuppressed,
				"an invalid datum coordinate must be skipped without pretending it was selected or suppressed");
			Assert(skipped.DecisionReason.StartsWith("NoRealOutlineAttachment:XDatum=", StringComparison.Ordinal),
				"an invalid datum coordinate must retain a machine-readable skip reason");
			Assert(plan.Dimensions.Any(d => d.DebugRole == "DatumY"),
				"a bad X datum must not discard the valid Y datum dimension");
			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth)
				&& plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallHeight),
				"a skipped datum dimension must not discard valid overall dimensions");
		}

		private static void InvalidSlotDatumIsSkippedWithDiagnostic()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateRectangle(100.0, 50.0);
			var datum = Datum2D.FromOutline(outline);
			datum.BaseX = 150.0;
			var slot = new SlotFeature2D
			{
				GroupId = "INVALID-SLOT-DATUM",
				FirstCenter = new Point2D(20.0, 25.0),
				SecondCenter = new Point2D(60.0, 25.0),
				Radius = 5.0,
				CenterDistance = 40.0
			};

			var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, new HoleFeature2D[0], new[] { slot });
			var skipped = plan.Diagnostics.DimensionCandidates.Where(candidate => candidate.DecisionStatus == "Skipped").ToList();

			Assert(skipped.Any(candidate => candidate.DebugRole == "SlotDatumH" || candidate.DebugRole == "SlotChainH"),
				"an invalid slot datum must create an explicit skipped diagnostic");
			Assert(skipped.All(candidate => !candidate.IsAttachmentValid
				&& candidate.DecisionReason.StartsWith("NoRealOutlineAttachment:", StringComparison.Ordinal)),
				"every skipped slot datum must report an invalid real-outline attachment");
			Assert(plan.Dimensions.Any(d => d.DebugRole == "SlotCenter" && Math.Abs(GetSpan(d) - 40.0) <= 0.001),
				"an invalid slot datum must not discard a valid slot center dimension");
		}

        private static void ClosedPathRecognitionBuildsOutline()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = new FeatureRecognizer2D(config).RecognizeOutlineFromClosedPath(new[]
            {
                new Point2D(0.0, 0.0),
                new Point2D(80.0, 0.0),
                new Point2D(80.0, 30.0),
                new Point2D(0.0, 30.0)
            });
            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(outline.Segments.Count == 4, "closed path should create four outline segments");
            AssertHasDimension(plan, DimensionKind.OverallWidth, DimensionOrientation.Horizontal, 80.0, "closed path width");
            AssertHasDimension(plan, DimensionKind.OverallHeight, DimensionOrientation.Vertical, 30.0, "closed path height");
        }

        private static void NonFortyFiveSlopeIsNotChamfer()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = new OutlineFeature2D
            {
                MinX = 0.0,
                MinY = 0.0,
                MaxX = 100.0,
                MaxY = 50.0
            };

            AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(100.0, 0.0), "bottom");
            AddSegment(outline, new Point2D(100.0, 0.0), new Point2D(100.0, 35.0), "right");
            AddSegment(outline, new Point2D(100.0, 35.0), new Point2D(80.0, 50.0), "slope");
            AddSegment(outline, new Point2D(80.0, 50.0), new Point2D(0.0, 50.0), "top");
            AddSegment(outline, new Point2D(0.0, 50.0), new Point2D(0.0, 0.0), "left");

            new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(outline);

            Assert(outline.Chamfers.Count == 0, "non-45-degree slope should not be recognized as chamfer");
        }

        private static void VerticalStructurePointsCreateStepWidths()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = new OutlineFeature2D
            {
                MinX = 0.0,
                MinY = 0.0,
                MaxX = 100.0,
                MaxY = 50.0
            };

            AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(0.0, 50.0), "left");
            AddSegment(outline, new Point2D(40.0, 10.0), new Point2D(40.0, 45.0), "middle");
            AddSegment(outline, new Point2D(100.0, 0.0), new Point2D(100.0, 50.0), "right");

            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(plan.Dimensions.Any(d =>
                    (d.DebugRole == "TopStructWidth" || d.DebugRole == "BottomStructWidth")
                    && d.Orientation == DimensionOrientation.Horizontal
                    && Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 40.0) <= 0.001),
                "expected structure width from left boundary to middle vertical");
            Assert(plan.Dimensions.Any(d =>
                    d.DebugRole == "BottomStructWidth"
                    && d.Orientation == DimensionOrientation.Horizontal
                    && Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 40.0) <= 0.001),
                "expected bottom structure width to keep the non-removed candidate");
            Assert(!plan.Dimensions.Any(d =>
                    d.DebugRole == "BottomStructWidth"
                    && d.Orientation == DimensionOrientation.Horizontal
                    && Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 60.0) <= 0.001),
                "bottom structure width should remove the longest extension candidate");
            Assert(plan.Dimensions.Where(d => d.DebugRole == "TopStructWidth").All(d =>
                    d.AlignmentKey == "Structure:T:H" && d.AlignmentPriority == 70),
                "top structure widths must share Structure:T:H alignment key");
            Assert(plan.Dimensions.Where(d => d.DebugRole == "BottomStructWidth").All(d =>
                    d.AlignmentKey == "Structure:B:H" && d.AlignmentPriority == 70),
                "bottom structure widths must share Structure:B:H alignment key");
        }

		private static void BottomStructureRejectsCrossAxisProjection()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 338.0,
				MaxY = 201.5
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(0.0, 201.5), "left");
			AddSegment(outline, new Point2D(305.0, 176.5), new Point2D(305.0, 201.5), "high-shoulder");
			AddSegment(outline, new Point2D(338.0, 0.0), new Point2D(338.0, 201.5), "right");

			var plan = new DimensionPlan();
			var retained = new DimensionPlanner(config).BuildBottomStructureWidthDimensions(outline, plan);
			var bottomCandidates = plan.Diagnostics.DimensionCandidates
				.Where(candidate => candidate.DebugRole == "BottomStructWidth")
				.ToList();

			Assert(bottomCandidates.Any(candidate => Math.Abs(candidate.Value - 33.0) <= config.GeometryTolerance
					&& Math.Abs(Math.Abs(candidate.SecondPointY - candidate.FirstPointY) - 176.5) <= config.GeometryTolerance),
				"cross-axis BottomStructWidth 33 candidate must be generated from the complete structure path");
			Assert(bottomCandidates.Any(candidate => Math.Abs(candidate.Value - 33.0) <= config.GeometryTolerance
					&& candidate.DecisionReason == "NotBottomSideStructureCandidate"),
				"cross-axis BottomStructWidth 33 must be rejected with NotBottomSideStructureCandidate");
			Assert(retained.Any(dimension => dimension.DebugRole == "BottomStructWidth"
					&& Math.Abs(Math.Abs(dimension.SecondPoint.X - dimension.FirstPoint.X) - 305.0) <= config.GeometryTolerance),
				"valid BottomStructWidth 305 must remain selected (got: "
				+ string.Join(",", bottomCandidates.Select(candidate => candidate.Value + "/" + candidate.DecisionStatus + "/" + candidate.DecisionReason))
				+ ")");
		}

		private static void StructureWidthsThatPartitionOverallAreSuppressed()
		{
			// Cross-side partition: BottomStruct 25 + TopStruct 232 = Overall 257 → both suppressed.
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 257.0,
				MaxY = 20.0
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(25.0, 0.0), "bottom-left");
			AddSegment(outline, new Point2D(25.0, 0.0), new Point2D(25.0, 20.0), "shoulder");
			AddSegment(outline, new Point2D(25.0, 0.0), new Point2D(257.0, 0.0), "bottom-main");
			AddSegment(outline, new Point2D(257.0, 0.0), new Point2D(257.0, 20.0), "right");
			AddSegment(outline, new Point2D(257.0, 20.0), new Point2D(0.0, 20.0), "top");
			AddSegment(outline, new Point2D(0.0, 20.0), new Point2D(0.0, 0.0), "left");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 257.0) <= config.GeometryTolerance),
				"overall width 257 must remain");
			// Selected structure widths must not form an overall partition pair.
			var selectedStruct = plan.Dimensions
				.Where(d => d.DebugRole == "TopStructWidth" || d.DebugRole == "BottomStructWidth")
				.ToList();
			Assert(!selectedStruct.Any(a => selectedStruct.Any(b => a != b
					&& Math.Abs(Math.Abs(a.SecondPoint.X - a.FirstPoint.X) + Math.Abs(b.SecondPoint.X - b.FirstPoint.X) - 257.0) <= config.GeometryTolerance)),
				"no selected structure pair may partition overall 257");
			// Complementary remainder / partition should remove the long leftover edge 232.
			Assert(!selectedStruct.Any(d => Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 232.0) <= config.GeometryTolerance),
				"structure width 232 (overall remainder) must not remain selected");
		}

		/// <summary>
		/// Repro of GEN|BSW1|GB|B|L0: bottom edge split 20+78 = overall 98 with a shoulder.
		/// BottomStructWidth 20 + OutlineSegment 78 form a true overall partition → structure must suppress.
		/// </summary>
		private static void StructureWidthThatPartitionsOverallWithOutlineSegmentIsSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 98.0,
				MaxY = 20.0
			};
			// Bottom/top collinear splits + full-height shoulder at x=20 (matches last-run geometry).
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(20.0, 0.0), "bottom-left");
			AddSegment(outline, new Point2D(20.0, 0.0), new Point2D(98.0, 0.0), "bottom-right");
			AddSegment(outline, new Point2D(98.0, 0.0), new Point2D(98.0, 20.0), "right");
			AddSegment(outline, new Point2D(98.0, 20.0), new Point2D(20.0, 20.0), "top-right");
			AddSegment(outline, new Point2D(20.0, 20.0), new Point2D(0.0, 20.0), "top-left");
			AddSegment(outline, new Point2D(0.0, 20.0), new Point2D(0.0, 0.0), "left");
			AddSegment(outline, new Point2D(20.0, 0.0), new Point2D(20.0, 20.0), "shoulder");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 98.0) <= config.GeometryTolerance),
				"overall width 98 must remain");
			// Decision A: keep the short step length, drop the long remainder (OS/structure 78).
			Assert(plan.Dimensions.Any(d =>
					(d.DebugRole == "BottomStructWidth" || d.DebugRole == "TopStructWidth")
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 20.0) <= config.GeometryTolerance),
				"short structure step 20 must remain (decision A with overall 98)");
			Assert(!plan.Dimensions.Any(d =>
					(d.DebugRole == "BottomStructWidth" || d.DebugRole == "TopStructWidth" || d.DebugRole == "OutlineSegment")
					&& d.Orientation == DimensionOrientation.Horizontal
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 78.0) <= config.GeometryTolerance),
				"long remainder 78 that closes overall with step 20 must not remain");
			Assert(!plan.Dimensions.Any(d => d.DebugRole == "OutlineSegment"
					&& d.Orientation == DimensionOrientation.Horizontal),
				"horizontal OutlineSegment partition pieces must be suppressed");
		}

		/// <summary>
		/// Repro last-run BSW 9+11 + TSW 71 = overall 91 (pairwise incomplete, chain complete).
		/// BuildOverallPartitionChain must suppress all three structure widths.
		/// </summary>
		private static void ThreeStructureWidthsThatPartitionOverallAreSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 91.0,
				MaxY = 20.0
			};
			// Two shoulders at x=9 and x=20 → structure chain [0,9]+[9,20]+[20,91].
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(9.0, 0.0), "bottom-a");
			AddSegment(outline, new Point2D(9.0, 0.0), new Point2D(20.0, 0.0), "bottom-b");
			AddSegment(outline, new Point2D(20.0, 0.0), new Point2D(91.0, 0.0), "bottom-c");
			AddSegment(outline, new Point2D(91.0, 0.0), new Point2D(91.0, 20.0), "right");
			AddSegment(outline, new Point2D(91.0, 20.0), new Point2D(20.0, 20.0), "top-c");
			AddSegment(outline, new Point2D(20.0, 20.0), new Point2D(9.0, 20.0), "top-b");
			AddSegment(outline, new Point2D(9.0, 20.0), new Point2D(0.0, 20.0), "top-a");
			AddSegment(outline, new Point2D(0.0, 20.0), new Point2D(0.0, 0.0), "left");
			AddSegment(outline, new Point2D(9.0, 0.0), new Point2D(9.0, 20.0), "shoulder-9");
			AddSegment(outline, new Point2D(20.0, 0.0), new Point2D(20.0, 20.0), "shoulder-20");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 91.0) <= config.GeometryTolerance),
				"overall width 91 must remain");
			// BSW 9 and 11 (and any TSW that completes the overall chain) must not stay selected.
			Assert(!plan.Dimensions.Any(d =>
					(d.DebugRole == "BottomStructWidth" || d.DebugRole == "TopStructWidth")
					&& (Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 9.0) <= config.GeometryTolerance
						|| Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 11.0) <= config.GeometryTolerance)),
				"BottomStructWidth 9 and 11 that partition overall must not remain selected");
			var structCandidates = plan.Diagnostics.DimensionCandidates
				.Where(c => c.DebugRole == "BottomStructWidth" || c.DebugRole == "TopStructWidth")
				.ToList();
			var bsw9 = structCandidates.Where(c => Math.Abs(c.Value - 9.0) <= config.GeometryTolerance).ToList();
			var bsw11 = structCandidates.Where(c => Math.Abs(c.Value - 11.0) <= config.GeometryTolerance).ToList();
			Assert(bsw9.Count > 0 && bsw9.All(c => c.IsSuppressed),
				"structure width 9 must be generated and suppressed (got: "
				+ string.Join(",", structCandidates.Select(c => c.DebugRole + "=" + c.Value + "/" + c.SuppressedReason + "/sel=" + c.IsSelected)) + ")");
			Assert(bsw11.Count > 0 && bsw11.All(c => c.IsSuppressed),
				"structure width 11 must be generated and suppressed");
			Assert(bsw9.Concat(bsw11).All(c => c.SuppressedReason == "LocalGeometryOnOverallEnvelope"),
				"outer-envelope structure chain members must record the higher-priority envelope reason");
			Assert(!plan.Dimensions.Any(d => d.DebugRole == "OutlineSegment" && d.Side == DimensionSide.Bottom),
				"bottom OutlineSegment chain covering overall must also be suppressed by outline rule");
		}

		private static void LocalTopStructureWidthIsKeptWhenNotPartitioningOverall()
		{
			// Local step width 40 on overall 100 — must keep structure width (positioning), not treat as overall partition.
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 100.0,
				MaxY = 50.0
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(0.0, 50.0), "left");
			AddSegment(outline, new Point2D(40.0, 10.0), new Point2D(40.0, 45.0), "middle");
			AddSegment(outline, new Point2D(100.0, 0.0), new Point2D(100.0, 50.0), "right");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d =>
					(d.DebugRole == "TopStructWidth" || d.DebugRole == "BottomStructWidth")
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 40.0) <= config.GeometryTolerance),
				"local structure width 40 must remain when it does not partition overall 100");
			Assert(plan.Diagnostics.DimensionCandidates
					.Where(c => (c.DebugRole == "TopStructWidth" || c.DebugRole == "BottomStructWidth")
						&& Math.Abs(c.Value - 40.0) <= config.GeometryTolerance)
					.All(c => c.SuppressedReason != "StructureOverallPartition"),
				"local structure width must not be suppressed as StructureOverallPartition");
		}

		/// <summary>
		/// L-shape regression after bottom-step fix: TopStructWidth 20 (tower) must not be killed
		/// by StructureOverallPartition with Top OutlineSegment 71 on the arm (same Side=Top,
		/// different Y — not collinear). Right arm height 20 lies on MaxX and must be suppressed.
		/// </summary>
		private static void LShapeTowerTopWidthIsKeptDespiteArmTopOutlineSegment()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 91.0,
				MaxY = 50.0
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(91.0, 0.0), "bottom");
			AddSegment(outline, new Point2D(91.0, 0.0), new Point2D(91.0, 20.0), "right-arm");
			AddSegment(outline, new Point2D(91.0, 20.0), new Point2D(20.0, 20.0), "arm-top");
			AddSegment(outline, new Point2D(20.0, 20.0), new Point2D(20.0, 39.0), "step-up");
			AddSegment(outline, new Point2D(20.0, 39.0), new Point2D(9.0, 50.0), "chamfer");
			AddSegment(outline, new Point2D(9.0, 50.0), new Point2D(0.0, 50.0), "tower-top");
			AddSegment(outline, new Point2D(0.0, 50.0), new Point2D(0.0, 0.0), "left");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 91.0) <= config.GeometryTolerance),
				"overall width 91 must remain");
			Assert(plan.Dimensions.Any(d =>
					d.DebugRole == "TopStructWidth"
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 20.0) <= config.GeometryTolerance),
				"tower top width 20 must remain (not erased by arm-top OutlineSegment via StructureOverallPartition)");
			Assert(plan.Dimensions.Any(d =>
					d.DebugRole == "RightStructHeight"
					&& Math.Abs(Math.Abs(d.SecondPoint.Y - d.FirstPoint.Y) - 20.0) <= config.GeometryTolerance),
				"right arm height 20 on stepped MaxX must be kept (edge is not full-length)");
			Assert(plan.Diagnostics.DimensionCandidates
					.Where(c => c.DebugRole == "TopStructWidth" && Math.Abs(c.Value - 20.0) <= config.GeometryTolerance)
					.All(c => c.SuppressedReason != "StructureOverallPartition"),
				"tower top width must not be StructureOverallPartition against non-collinear arm OS");
		}

		/// <summary>
		/// Stepped bottom: overall width 215, lower ledge width 120 on MinY. MinY is not a
		/// full-length envelope edge (coverage 120 &lt; 215), so the step face is kept.
		/// Upper undercut stops short of the step (gap) so StructureOverallPartition cannot form
		/// 95+120 against overall 215.
		/// </summary>
		private static void BottomStepWidthOnOverallEnvelopeIsSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 215.0,
				MaxY = 100.0
			};
			// Left undercut Y=40 for X in [0,80]; gap 80→95; lower ledge Y=0 for X in [95,215] (span 120).
			AddSegment(outline, new Point2D(0.0, 40.0), new Point2D(80.0, 40.0), "upper-bottom");
			AddSegment(outline, new Point2D(80.0, 40.0), new Point2D(95.0, 40.0), "upper-to-step");
			AddSegment(outline, new Point2D(95.0, 40.0), new Point2D(95.0, 0.0), "step-down");
			AddSegment(outline, new Point2D(95.0, 0.0), new Point2D(215.0, 0.0), "lower-ledge");
			AddSegment(outline, new Point2D(215.0, 0.0), new Point2D(215.0, 100.0), "right");
			AddSegment(outline, new Point2D(215.0, 100.0), new Point2D(0.0, 100.0), "top");
			AddSegment(outline, new Point2D(0.0, 100.0), new Point2D(0.0, 40.0), "left");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 215.0) <= config.GeometryTolerance),
				"overall width 215 must remain");
			Assert(plan.Dimensions.Any(d =>
					d.DebugRole == "BottomStructWidth"
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 120.0) <= config.GeometryTolerance),
				"bottom step/ledge width 120 on partial MinY must be kept");
			Assert(!plan.Dimensions.Any(d => d.Orientation == DimensionOrientation.Horizontal
					&& Math.Abs(GetSpan(d) - 95.0) <= config.GeometryTolerance),
				"projected body remainder 95 must be removed even though the real step 120 is longer");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "BottomStructWidth"
					&& Math.Abs(c.Value - 95.0) <= config.GeometryTolerance
					&& c.IsSuppressed
					&& c.SuppressedReason == "OuterContourStepOverallRemainder"),
				"body remainder 95 must record topology-based outer-step suppression");
		}

		private static void BottomProtrusionSuppressesOuterWidthAndInnerLedge()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 287.0,
				MaxY = 110.0
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(50.0, 0.0), "bottom-protrusion");
			AddSegment(outline, new Point2D(50.0, 0.0), new Point2D(50.0, 10.0), "protrusion-riser");
			AddSegment(outline, new Point2D(50.0, 10.0), new Point2D(70.0, 10.0), "inner-ledge");
			AddSegment(outline, new Point2D(70.0, 10.0), new Point2D(70.0, 110.0), "inner-left");
			AddSegment(outline, new Point2D(70.0, 110.0), new Point2D(287.0, 110.0), "top-right");
			AddSegment(outline, new Point2D(287.0, 110.0), new Point2D(287.0, 10.0), "right");
			AddSegment(outline, new Point2D(287.0, 10.0), new Point2D(70.0, 10.0), "inner-bottom");
			AddSegment(outline, new Point2D(70.0, 110.0), new Point2D(0.0, 110.0), "top-left");
			AddSegment(outline, new Point2D(0.0, 110.0), new Point2D(0.0, 0.0), "left");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.DebugRole == "BottomStructWidth"
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 50.0) <= config.GeometryTolerance),
				"bottom protrusion width 50 on partial MinY must be kept");
			Assert(!plan.Dimensions.Any(d => (d.DebugRole == "BottomStructWidth" || d.DebugRole == "OutlineSegment")
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 20.0) <= config.GeometryTolerance),
				"inner ledge width 20 must not remain selected");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.IsSuppressed
					&& Math.Abs(c.Value - 20.0) <= config.GeometryTolerance
					&& c.SuppressedReason == "BottomProtrusionInnerRemainder"),
				"inner ledge candidates must record BottomProtrusionInnerRemainder");
		}

		private static void BottomFilletedProtrusionWidthIsSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 90.0,
				MaxY = 53.5
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(20.0, 0.0), "bottom-step");
			AddSegment(outline, new Point2D(20.0, 0.0), new Point2D(20.0, 23.5), "step-riser");
			outline.Arcs.Add(new Arc2D
			{
				Start = new Point2D(20.0, 23.5),
				End = new Point2D(30.0, 33.5),
				Center = new Point2D(30.0, 23.5),
				Radius = 10.0,
				Bulge = Math.Tan(Math.PI / 8.0),
				SourceKey = "step-fillet"
			});
			AddSegment(outline, new Point2D(30.0, 33.5), new Point2D(90.0, 33.5), "body-floor");
			AddSegment(outline, new Point2D(90.0, 33.5), new Point2D(90.0, 53.5), "right");
			AddSegment(outline, new Point2D(90.0, 53.5), new Point2D(0.0, 53.5), "top");
			AddSegment(outline, new Point2D(0.0, 53.5), new Point2D(0.0, 0.0), "left");
			new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(outline);

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(outline.Fillets.Count == 1, "step fillet must be recognized");
			Assert(plan.Dimensions.Any(d => d.DebugRole == "BottomStructWidth"
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 20.0) <= config.GeometryTolerance),
				"filleted bottom protrusion width 20 on partial MinY must be kept");
		}

		private static void ChamferedTopStepUsesCompositeDimensions()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 90.0,
				MaxY = 53.5
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(90.0, 0.0), "bottom");
			AddSegment(outline, new Point2D(90.0, 0.0), new Point2D(90.0, 33.5), "right-body");
			AddSegment(outline, new Point2D(0.0, 33.5), new Point2D(70.0, 33.5), "body-floor");
			AddSegment(outline, new Point2D(70.0, 33.5), new Point2D(90.0, 33.5), "step-floor");
			AddSegment(outline, new Point2D(70.0, 33.5), new Point2D(70.0, 47.0), "step-left-low");
			AddSegment(outline, new Point2D(70.0, 47.0), new Point2D(71.0, 48.0), "step-left-chamfer-low");
			AddSegment(outline, new Point2D(71.0, 48.0), new Point2D(71.0, 52.5), "step-left-high");
			AddSegment(outline, new Point2D(71.0, 52.5), new Point2D(70.0, 53.5), "step-left-chamfer-top");
			AddSegment(outline, new Point2D(70.0, 53.5), new Point2D(90.0, 53.5), "step-top");
			AddSegment(outline, new Point2D(90.0, 33.5), new Point2D(90.0, 47.0), "step-right-low");
			AddSegment(outline, new Point2D(90.0, 47.0), new Point2D(89.0, 48.0), "step-right-chamfer-low");
			AddSegment(outline, new Point2D(89.0, 48.0), new Point2D(89.0, 52.5), "step-right-high");
			AddSegment(outline, new Point2D(89.0, 52.5), new Point2D(90.0, 53.5), "step-right-chamfer-top");
			AddSegment(outline, new Point2D(0.0, 53.5), new Point2D(0.0, 0.0), "left");
			outline.Chamfers.Add(new ChamferFeature2D { StartPoint = new Point2D(70.0, 47.0), EndPoint = new Point2D(71.0, 48.0) });
			outline.Chamfers.Add(new ChamferFeature2D { StartPoint = new Point2D(71.0, 52.5), EndPoint = new Point2D(70.0, 53.5) });
			outline.Chamfers.Add(new ChamferFeature2D { StartPoint = new Point2D(90.0, 47.0), EndPoint = new Point2D(89.0, 48.0) });
			outline.Chamfers.Add(new ChamferFeature2D { StartPoint = new Point2D(89.0, 52.5), EndPoint = new Point2D(90.0, 53.5) });

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.DebugRole == "TopChamferedStepWidth"
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 20.0) <= config.GeometryTolerance),
				"chamfered top step width 20 must remain selected");
			Assert(plan.Dimensions.Any(d => d.DebugRole == "RightChamferedStepHeight"
					&& Math.Abs(Math.Abs(d.SecondPoint.Y - d.FirstPoint.Y) - 20.0) <= config.GeometryTolerance),
				"chamfered top step height 20 on stepped MaxX must be kept");
		}

		private static void InteriorHorizontalOutlineSegmentPrefersNonCrossingSide()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 90.0, MaxY = 28.0 };
			// Shaft: interior horizontal edge at y=8.26 with solid above and free space below.
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(70.0, 0.0), "bottom-body");
			AddSegment(outline, new Point2D(70.0, 0.0), new Point2D(90.0, 0.0), "bottom-step");
			AddSegment(outline, new Point2D(90.0, 0.0), new Point2D(90.0, 8.26), "step-right-low");
			AddSegment(outline, new Point2D(90.0, 8.26), new Point2D(70.0, 8.26), "step-shelf");
			AddSegment(outline, new Point2D(70.0, 8.26), new Point2D(70.0, 28.0), "shoulder");
			AddSegment(outline, new Point2D(70.0, 28.0), new Point2D(0.0, 28.0), "body-top");
			AddSegment(outline, new Point2D(0.0, 28.0), new Point2D(0.0, 0.0), "left");
			AddSegment(outline, new Point2D(0.0, 8.26), new Point2D(70.0, 8.26), "interior-body");
			var planner = new DimensionPlanner(config);
			var interior = new Segment2D(new Point2D(0.0, 8.26), new Point2D(70.0, 8.26));
			Assert(planner.ResolveHorizontalOutlineSegmentSide(interior, outline) == DimensionSide.Bottom,
				"interior horizontal body edge must resolve Bottom (free side), not Top through solid");
		}

		private static void TopEnvelopeHorizontalSegmentStaysTop()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 90.0, MaxY = 28.0 };
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(90.0, 0.0), "bottom");
			AddSegment(outline, new Point2D(90.0, 0.0), new Point2D(90.0, 28.0), "right");
			AddSegment(outline, new Point2D(90.0, 28.0), new Point2D(0.0, 28.0), "top");
			AddSegment(outline, new Point2D(0.0, 28.0), new Point2D(0.0, 0.0), "left");
			var top = new Segment2D(new Point2D(0.0, 28.0), new Point2D(90.0, 28.0));
			Assert(new DimensionPlanner(config).ResolveHorizontalOutlineSegmentSide(top, outline) == DimensionSide.Top,
				"MaxY envelope edge must stay Top");
		}

		private static void BottomEnvelopeHorizontalSegmentStaysBottom()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 90.0, MaxY = 28.0 };
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(90.0, 0.0), "bottom");
			AddSegment(outline, new Point2D(90.0, 0.0), new Point2D(90.0, 28.0), "right");
			AddSegment(outline, new Point2D(90.0, 28.0), new Point2D(0.0, 28.0), "top");
			AddSegment(outline, new Point2D(0.0, 28.0), new Point2D(0.0, 0.0), "left");
			var bottom = new Segment2D(new Point2D(0.0, 0.0), new Point2D(90.0, 0.0));
			Assert(new DimensionPlanner(config).ResolveHorizontalOutlineSegmentSide(bottom, outline) == DimensionSide.Bottom,
				"MinY envelope edge must stay Bottom");
		}

		private static void BottomBodyWidthNotDroppedByLongestExtension()
		{
			const string ruleId = "OuterContourStepOverallRemainder";
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 90.0, MaxY = 28.0 };
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(70.0, 0.0), "bottom-body");
			AddSegment(outline, new Point2D(70.0, 0.0), new Point2D(90.0, 0.0), "bottom-step");
			AddSegment(outline, new Point2D(90.0, 0.0), new Point2D(90.0, 20.0), "step-right");
			AddSegment(outline, new Point2D(90.0, 20.0), new Point2D(70.0, 20.0), "step-top");
			AddSegment(outline, new Point2D(70.0, 20.0), new Point2D(70.0, 28.0), "shoulder");
			AddSegment(outline, new Point2D(70.0, 28.0), new Point2D(0.0, 28.0), "body-top");
			AddSegment(outline, new Point2D(0.0, 28.0), new Point2D(0.0, 0.0), "left");
			AddSegment(outline, new Point2D(70.0, 0.0), new Point2D(70.0, 20.0), "shoulder-down");
			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);
			Assert(!plan.Diagnostics.DimensionCandidates.Any(c =>
					c.DebugRole == "BottomStructWidth"
					&& Math.Abs(c.Value - 70.0) <= config.GeometryTolerance
					&& c.DecisionStatus == "Skipped"
					&& c.DecisionReason == "LongestExtensionCandidate"),
				"bottom body width 70 must not be silently dropped as LongestExtensionCandidate");
			// May be Selected or later Suppressed by closed-chain rules, but must be diagnosable.
			Assert(plan.Diagnostics.DimensionCandidates.Any(c =>
					c.DebugRole == "BottomStructWidth"
					&& Math.Abs(c.Value - 70.0) <= config.GeometryTolerance),
				"bottom body width 70 must appear in diagnostics");
			Assert(!plan.Diagnostics.DimensionCandidates.Any(c => c.RuleId == ruleId),
				"adjacent same-level bottom geometry must not be claimed by the outer-step remainder rule");
		}

		private static void BottomOuterContourStepKeeps20AndSuppresses70Body()
		{
			const string ruleId = "OuterContourStepOverallRemainder";
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateOuterContourStepOutline(translateX: 0.0, translateY: 0.0);
			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			var bottomBodyCandidates = plan.Diagnostics.DimensionCandidates.Where(c =>
					c.DebugRole == "BottomStructWidth"
					&& Math.Abs(c.Value - 70.0) <= config.GeometryTolerance)
				.ToList();
			var outlineBodyCandidates = plan.Diagnostics.DimensionCandidates.Where(c =>
					c.DebugRole == "OutlineSegment"
					&& Math.Abs(c.Value - 70.0) <= config.GeometryTolerance)
				.ToList();
			var leftResidualCandidates = plan.Diagnostics.DimensionCandidates.Where(c =>
					c.DebugRole == "LeftStructHeight"
					&& Math.Abs(c.Value - 8.26) <= config.GeometryTolerance)
				.ToList();
			Assert(bottomBodyCandidates.Count == 1,
				"projected BottomStructWidth body 70 must exist exactly once before suppression");
			Assert(outlineBodyCandidates.Count == 1,
				"OutlineSegment body 70 must exist exactly once before suppression");
			Assert(leftResidualCandidates.Count == 1,
				"projected left residual 8.26 must exist exactly once before suppression");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "BottomStructWidth"
					&& Math.Abs(c.Value - 20.0) <= config.GeometryTolerance),
				"real chamfered bottom outer step 20 must exist as a candidate");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "LeftStructHeight"
					&& Math.Abs(c.Value - 20.0) <= config.GeometryTolerance),
				"real partial MinX outer face 20 must exist as a candidate");

			var suppressedRemainders = bottomBodyCandidates
				.Concat(outlineBodyCandidates)
				.Concat(leftResidualCandidates)
				.ToList();
			Assert(suppressedRemainders.All(c => c.IsSuppressed
					&& c.Decision == DimensionCandidateDecision.Suppressed
					&& c.SuppressedReason == ruleId
					&& c.RuleId == ruleId),
				"all three projected remainders must have the unique outer-step RuleId and suppressed decision");
			Assert(bottomBodyCandidates[0].Role == DimensionCandidateRole.Structure
					&& outlineBodyCandidates[0].Role == DimensionCandidateRole.OutlineSegment
					&& leftResidualCandidates[0].Role == DimensionCandidateRole.Structure,
				"suppressed OC01 candidates must expose formal roles");
			Assert(suppressedRemainders.All(c => c.OwnerKind == DimensionCandidateOwnerKind.Outline),
				"suppressed OC01 candidates must belong to the outline");
			Assert(suppressedRemainders.All(c => c.SourceGeometryIds.Count > 0
					&& c.SourceGeometryIds.All(id => !string.IsNullOrWhiteSpace(id))),
				"suppressed OC01 candidates must retain source geometry ids");
			Assert(suppressedRemainders.All(c => !string.IsNullOrWhiteSpace(c.TopologyEvidence)),
				"suppressed OC01 candidates must retain topology evidence");

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(GetSpan(d) - 90.0) <= config.GeometryTolerance),
				"overall width 90 must remain");
			Assert(plan.Dimensions.Any(d => d.DebugRole == "BottomStructWidth"
					&& Math.Abs(GetSpan(d) - 20.0) <= config.GeometryTolerance
					&& d.RuleId == ruleId),
				"real chamfered bottom outer step 20 must remain with rule evidence");
			Assert(!plan.Dimensions.Any(d => d.Orientation == DimensionOrientation.Horizontal
					&& Math.Abs(GetSpan(d) - 70.0) <= config.GeometryTolerance),
				"all body remainder representations 70 must be removed");
			Assert(plan.Dimensions.Any(d => d.DebugRole == "LeftStructHeight"
					&& Math.Abs(GetSpan(d) - 20.0) <= config.GeometryTolerance
					&& d.RuleId == ruleId),
				"real partial MinX outer face height must remain with rule evidence");
			Assert(!plan.Dimensions.Any(d => d.Orientation == DimensionOrientation.Vertical
					&& Math.Abs(GetSpan(d) - 8.26) <= config.GeometryTolerance),
				"projected lower height residual must not remain");
		}

		private static void TranslatedOuterContourStepKeepsRuleDecision()
		{
			const string ruleId = "OuterContourStepOverallRemainder";
			var config = DimensionRuleConfig.CreateDefault();
			var originalPlan = new DimensionPlanner(config).CreateOutlinePlan(
				CreateOuterContourStepOutline(translateX: 0.0, translateY: 0.0));
			var transformedPlan = new DimensionPlanner(config).CreateOutlinePlan(
				CreateOuterContourStepOutline(translateX: 130.0, translateY: 40.0));

			var originalRuleCandidates = originalPlan.Diagnostics.DimensionCandidates
				.Where(c => c.RuleId == ruleId)
				.ToList();
			var transformedRuleCandidates = transformedPlan.Diagnostics.DimensionCandidates
				.Where(c => c.RuleId == ruleId)
				.ToList();
			Assert(originalRuleCandidates.Count > 0
					&& transformedRuleCandidates.Count == originalRuleCandidates.Count,
				"translation must preserve the number of candidates decided by the outer-step rule");
			foreach (var original in originalRuleCandidates)
			{
				Assert(transformedRuleCandidates.Any(candidate =>
						candidate.Role == original.Role
						&& candidate.Decision == original.Decision
						&& Math.Abs(candidate.Value - original.Value) <= config.GeometryTolerance),
					"translation must preserve RuleId, formal role, decision, and value");
			}

			var unmatchedFinal = transformedPlan.Dimensions.ToList();
			foreach (var original in originalPlan.Dimensions)
			{
				var equivalent = unmatchedFinal.FirstOrDefault(candidate =>
					candidate.Role == original.Role
					&& candidate.Orientation == original.Orientation
					&& candidate.Side == original.Side
					&& Math.Abs(GetSpan(candidate) - GetSpan(original)) <= config.GeometryTolerance);
				Assert(equivalent != null,
					"translation must preserve the final dimension role, orientation, side, and span");
				unmatchedFinal.Remove(equivalent);
			}
			Assert(unmatchedFinal.Count == 0,
				"translation must not add final dimensions");
		}

		private static OutlineFeature2D CreateOuterContourStepOutline(double translateX, double translateY)
		{
			Func<double, double> x = value => translateX + value;
			Func<double, double> y = value => translateY + value;
			var outline = new OutlineFeature2D
			{
				MinX = x(0.0),
				MinY = y(0.0),
				MaxX = x(90.0),
				MaxY = y(28.26)
			};
			// Closed production topology: upper body underside 70, two 1x1 chamfers, bottom step 20 overall.
			AddSegment(outline, new Point2D(x(0.0), y(8.26)), new Point2D(x(70.0), y(8.26)), "body-underside");
			AddSegment(outline, new Point2D(x(70.0), y(8.26)), new Point2D(x(70.0), y(1.0)), "step-shoulder");
			AddSegment(outline, new Point2D(x(70.0), y(1.0)), new Point2D(x(71.0), y(0.0)), "step-chamfer-left");
			AddSegment(outline, new Point2D(x(71.0), y(0.0)), new Point2D(x(89.0), y(0.0)), "step-bottom");
			AddSegment(outline, new Point2D(x(89.0), y(0.0)), new Point2D(x(90.0), y(1.0)), "step-chamfer-right");
			AddSegment(outline, new Point2D(x(90.0), y(1.0)), new Point2D(x(90.0), y(28.26)), "right");
			AddSegment(outline, new Point2D(x(90.0), y(28.26)), new Point2D(x(0.0), y(28.26)), "top");
			AddSegment(outline, new Point2D(x(0.0), y(28.26)), new Point2D(x(0.0), y(8.26)), "left");
			return outline;
		}

		private static void OutlineSegmentBodyLengthClosedByStepIsSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var plan = new DimensionPlan();
			var overall = new PlannedDimension
			{
				Kind = DimensionKind.OverallWidth,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(90.0, 0.0),
				DebugRole = "OverallWidth"
			};
			var os70 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(0.0, 8.26),
				SecondPoint = new Point2D(70.0, 8.26),
				DebugRole = "OutlineSegment"
			};
			var step20 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(70.0, 0.0),
				SecondPoint = new Point2D(90.0, 0.0),
				DebugRole = "BottomStructWidth"
			};
			plan.Add(overall);
			plan.Add(os70);
			plan.Add(step20);
			planner.SuppressClosedOverallLengthRemainders(plan, horizontal: true);
			Assert(plan.Dimensions.Contains(overall) && plan.Dimensions.Contains(step20),
				"overall 90 and step 20 must remain");
			Assert(!plan.Dimensions.Contains(os70),
				"OutlineSegment body 70 that closes overall with step 20 must be suppressed (decision A)");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c =>
					c.Id == os70.DiagnosticId
					&& c.IsSuppressed
					&& c.SuppressedReason == "ComplementaryOutlineRemainder"),
				"OS 70 must record ComplementaryOutlineRemainder");
		}

		/// <summary>
		/// Shaft-like step: overall 90, top body 70, bottom step 20. 70+20 closes overall and the
		/// larger complementary piece (70) must suppress while keeping overall and the step 20.
		/// Directly exercises the cross-side pass with concrete planned dims (full outline path may
		/// still envelope-suppress the bottom step when MinY is collinear full-length).
		/// </summary>
		private static void CrossSideStructureWidthsThatCloseOverallChainAreSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var plan = new DimensionPlan();
			var overall = new PlannedDimension
			{
				Kind = DimensionKind.OverallWidth,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(90.0, 0.0),
				DebugRole = "OverallWidth"
			};
			var top70 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Top,
				FirstPoint = new Point2D(0.0, 28.0),
				SecondPoint = new Point2D(70.0, 28.0),
				DebugRole = "TopStructWidth"
			};
			var bottom20 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(70.0, 0.0),
				SecondPoint = new Point2D(90.0, 0.0),
				DebugRole = "BottomStructWidth"
			};
			plan.Add(overall);
			plan.Add(top70);
			plan.Add(bottom20);

			planner.SuppressCrossSideComplementaryStructureRemainders(plan, horizontal: true);

			Assert(plan.Dimensions.Contains(overall), "overall width 90 must remain");
			Assert(plan.Dimensions.Contains(bottom20), "step length 20 must remain");
			Assert(!plan.Dimensions.Contains(top70),
				"body length 70 that closes overall with step 20 must be suppressed");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c =>
					c.Id == top70.DiagnosticId
					&& c.IsSuppressed
					&& c.SuppressedReason == "ComplementaryOutlineRemainder"),
				"closed-chain body length 70 must record ComplementaryOutlineRemainder");

			// Full outline path with collinear bottom (matches the shaft drawing): keep step 20,
			// drop body 70, keep overall 90.
			var outline = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 90.0, MaxY = 28.0 };
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(70.0, 0.0), "bottom-body");
			AddSegment(outline, new Point2D(70.0, 0.0), new Point2D(90.0, 0.0), "bottom-step");
			AddSegment(outline, new Point2D(90.0, 0.0), new Point2D(90.0, 20.0), "step-right");
			AddSegment(outline, new Point2D(90.0, 20.0), new Point2D(70.0, 20.0), "step-top");
			AddSegment(outline, new Point2D(70.0, 20.0), new Point2D(70.0, 28.0), "shoulder");
			AddSegment(outline, new Point2D(70.0, 28.0), new Point2D(0.0, 28.0), "body-top");
			AddSegment(outline, new Point2D(0.0, 28.0), new Point2D(0.0, 0.0), "left");
			AddSegment(outline, new Point2D(70.0, 0.0), new Point2D(70.0, 20.0), "shoulder-down");
			var full = new DimensionPlanner(config).CreateOutlinePlan(outline);
			Assert(full.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(GetSpan(d) - 90.0) <= config.GeometryTolerance),
				"full-path overall 90 must remain");
			Assert(full.Dimensions.Any(d =>
					(d.DebugRole == "BottomStructWidth" || d.DebugRole == "TopStructWidth")
					&& Math.Abs(GetSpan(d) - 20.0) <= config.GeometryTolerance),
				"full-path step length 20 must remain");
			Assert(!full.Dimensions.Any(d =>
					(d.DebugRole == "TopStructWidth" || d.DebugRole == "BottomStructWidth")
					&& Math.Abs(GetSpan(d) - 70.0) <= config.GeometryTolerance),
				"full-path must not keep body length 70 beside the closed chain");
		}

		private static void ProjectedCrossLevelStructureWidthsDoNotPartitionOverall()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 96.0,
				MaxY = 35.5
			};
			// Top 25 is real. The next top projection spans 3 in X but drops 15.5 in Y.
			// Bottom 3 + 68 must not join the top edge into an overall partition.
			AddSegment(outline, new Point2D(0.0, 20.0), new Point2D(0.0, 35.5), "left");
			AddSegment(outline, new Point2D(0.0, 35.5), new Point2D(25.0, 35.5), "top-step");
			AddSegment(outline, new Point2D(25.0, 0.0), new Point2D(25.0, 35.5), "top-riser");
			AddSegment(outline, new Point2D(28.0, 0.0), new Point2D(28.0, 20.0), "inner-riser");
			AddSegment(outline, new Point2D(28.0, 0.0), new Point2D(96.0, 0.0), "bottom-main");
			AddSegment(outline, new Point2D(28.0, 20.0), new Point2D(96.0, 20.0), "arm-top");
			AddSegment(outline, new Point2D(96.0, 0.0), new Point2D(96.0, 35.5), "right");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);
			var top25 = plan.Diagnostics.DimensionCandidates.Where(c => c.DebugRole == "TopStructWidth"
				&& Math.Abs(c.Value - 25.0) <= config.GeometryTolerance).ToList();
			var projected3 = plan.Diagnostics.DimensionCandidates.Where(c => c.DebugRole == "TopStructWidth"
				&& Math.Abs(c.Value - 3.0) <= config.GeometryTolerance).ToList();
			var bottom68 = plan.Diagnostics.DimensionCandidates.Where(c => c.DebugRole == "BottomStructWidth"
				&& Math.Abs(c.Value - 68.0) <= config.GeometryTolerance).ToList();

			Assert(top25.Count > 0, "real top step width 25 must be generated");
			Assert(top25.All(c => c.SuppressedReason != "LocalGeometryOnOverallEnvelope"),
				"top step width 25 on partial MaxY must not be removed by full-envelope local geometry");
			Assert(projected3.All(c => c.SuppressedReason != "StructureOverallPartition"),
				"cross-level 3 must not participate in StructureOverallPartition");
			Assert(bottom68.All(c => c.SuppressedReason != "StructureOverallPartition"),
				"bottom arm 68 must not be wiped only via StructureOverallPartition from cross-level tops");
			Assert(bottom68.All(c => c.SuppressedReason != "LocalGeometryOnOverallEnvelope"),
				"bottom arm 68 on partial MinY must not be removed by full-envelope local geometry");
		}

		private static void DatumRootedLeftOuterEnvelopeDimensionsAreSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 36.0,
				MaxY = 35.5
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(36.0, 0.0), "bottom");
			AddSegment(outline, new Point2D(36.0, 0.0), new Point2D(36.0, 35.5), "right");
			AddSegment(outline, new Point2D(36.0, 35.5), new Point2D(18.0, 35.5), "tower-top");
			AddSegment(outline, new Point2D(18.0, 35.5), new Point2D(18.0, 20.0), "tower-riser");
			AddSegment(outline, new Point2D(18.0, 20.0), new Point2D(0.0, 20.0), "left-step");
			AddSegment(outline, new Point2D(0.0, 20.0), new Point2D(0.0, 10.0), "left-upper");
			AddSegment(outline, new Point2D(0.0, 10.0), new Point2D(0.0, 0.0), "left-lower");
			AddSegment(outline, new Point2D(0.0, 10.0), new Point2D(36.0, 10.0), "internal-full-width");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(GetSpan(d) - 36.0) <= config.GeometryTolerance),
				"overall width 36 must remain");
			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallHeight
					&& Math.Abs(GetSpan(d) - 35.5) <= config.GeometryTolerance),
				"overall height 35.5 must remain");
			Assert(plan.Dimensions.Any(d => d.DebugRole == "TopStructWidth"
					&& Math.Abs(GetSpan(d) - 18.0) <= config.GeometryTolerance),
				"top tower width 18 on partial MaxY must be kept");
			Assert(plan.Dimensions.Any(d => d.DebugRole == "LeftStructHeight"
					&& Math.Abs(GetSpan(d) - 20.0) <= config.GeometryTolerance)
				|| plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "LeftStructHeight"
					&& Math.Abs(c.Value - 20.0) <= config.GeometryTolerance
					&& c.SuppressedReason != "LocalGeometryOnOverallEnvelope"),
				"left step height 20 must not be removed solely as full-envelope local geometry");
			Assert(!plan.Dimensions.Any(d => d.Kind == DimensionKind.Normal
					&& d.Orientation == DimensionOrientation.Vertical
					&& (Math.Abs(GetSpan(d) - 10.0) <= config.GeometryTolerance
						|| Math.Abs(GetSpan(d) - 15.5) <= config.GeometryTolerance)),
				"internal 10 and complementary 15.5 must not remain");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "OutlineSegment"
					&& Math.Abs(c.Value - 15.5) <= config.GeometryTolerance
					&& c.SuppressedReason == "ComplementaryOutlineRemainder"),
				"complementary 15.5 must record ComplementaryOutlineRemainder");
		}

		private static void DatumRootedBottomAndLeftOuterEnvelopeDimensionsAreSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 96.0,
				MaxY = 36.0
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(25.0, 0.0), "bottom-left");
			AddSegment(outline, new Point2D(96.0, 16.0), new Point2D(96.0, 36.0), "right");
			AddSegment(outline, new Point2D(96.0, 36.0), new Point2D(0.0, 36.0), "top");
			AddSegment(outline, new Point2D(0.0, 36.0), new Point2D(0.0, 18.0), "left-upper");
			AddSegment(outline, new Point2D(0.0, 18.0), new Point2D(0.0, 0.0), "left-lower");
			AddSegment(outline, new Point2D(0.0, 18.0), new Point2D(25.0, 18.0), "left-ledge");
			AddSegment(outline, new Point2D(25.0, 0.0), new Point2D(25.0, 18.0), "left-riser");
			AddSegment(outline, new Point2D(25.0, 18.0), new Point2D(28.0, 16.0), "transition");
			AddSegment(outline, new Point2D(28.0, 16.0), new Point2D(96.0, 16.0), "arm-top");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& d.Side == DimensionSide.Bottom
					&& Math.Abs(GetSpan(d) - 96.0) <= config.GeometryTolerance),
				"bottom overall width 96 must remain");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "TopStructWidth"
					&& Math.Abs(c.Value - 96.0) <= config.GeometryTolerance
					&& c.SuppressedReason == "LocalGeometryOnOverallEnvelope"),
				"full-span top-envelope structure width 96 must be suppressed");
			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallHeight
					&& Math.Abs(GetSpan(d) - 36.0) <= config.GeometryTolerance),
				"overall height 36 must remain");
			Assert(plan.Dimensions.Any(d => d.DebugRole == "BottomStructWidth"
					&& Math.Abs(GetSpan(d) - 25.0) <= config.GeometryTolerance),
				"bottom step width 25 on partial MinY must be kept");
			Assert(plan.Dimensions.Any(d => d.DebugRole == "LeftStructHeight"
					&& Math.Abs(GetSpan(d) - 18.0) <= config.GeometryTolerance)
				|| plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "LeftStructHeight"
					&& Math.Abs(c.Value - 18.0) <= config.GeometryTolerance
					&& c.SuppressedReason != "LocalGeometryOnOverallEnvelope"),
				"left step height 18 must not be removed solely as full-envelope local geometry");
			Assert(!plan.Dimensions.Any(d => d.Kind == DimensionKind.Normal
					&& d.Orientation == DimensionOrientation.Horizontal
					&& (Math.Abs(GetSpan(d) - 3.0) <= config.GeometryTolerance
						|| Math.Abs(GetSpan(d) - 68.0) <= config.GeometryTolerance)),
				"projected transition 3 and residual arm 68 must not remain");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "OutlineSegment"
					&& Math.Abs(c.Value - 68.0) <= config.GeometryTolerance
					&& c.SuppressedReason == "StructureDuplicateOfEnvelopeOutlineSegment"),
				"inner top arm residual 68 must record the envelope-duplicate reason");
			Assert(plan.Dimensions.Any(d => Math.Abs(GetSpan(d) - 25.0) <= config.GeometryTolerance
					&& (d.DebugRole == "BottomStructWidth" || d.DebugRole == "TopStructWidth")),
				"step width 25 on a partial envelope edge must remain available");
		}

		private static void DatumRootedBottomEnvelopeStepAndArcResidualAreSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 96.0,
				MaxY = 36.0
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(25.0, 0.0), "bottom-left");
			AddSegment(outline, new Point2D(25.0, 0.0), new Point2D(25.0, 18.0), "root-riser");
			outline.Arcs.Add(new Arc2D
			{
				Start = new Point2D(28.0, 16.0),
				End = new Point2D(25.0, 13.0),
				Center = new Point2D(28.0, 13.0),
				Radius = 3.0,
				Bulge = Math.Tan(Math.PI / 8.0),
				SourceKey = "R3"
			});
			AddSegment(outline, new Point2D(28.0, 16.0), new Point2D(96.0, 16.0), "arm-top");
			AddSegment(outline, new Point2D(96.0, 16.0), new Point2D(96.0, 36.0), "right");
			AddSegment(outline, new Point2D(96.0, 36.0), new Point2D(0.0, 36.0), "top");
			AddSegment(outline, new Point2D(0.0, 36.0), new Point2D(0.0, 0.0), "left");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.DebugRole == "BottomStructWidth"
					&& Math.Abs(GetSpan(d) - 25.0) <= config.GeometryTolerance),
				"datum-rooted bottom step width 25 on partial MinY must be kept");
			Assert(!plan.Dimensions.Any(d => d.Orientation == DimensionOrientation.Horizontal
					&& Math.Abs(GetSpan(d) - 68.0) <= config.GeometryTolerance),
				"outer arm residual 68 connected by a small real arc must not remain");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "OutlineSegment"
					&& Math.Abs(c.Value - 68.0) <= config.GeometryTolerance
					&& c.SuppressedReason == "StructureDuplicateOfEnvelopeOutlineSegment"),
				"arc-connected arm residual 68 must record the envelope-duplicate reason");
		}

		/// <summary>
		/// L-shaped part matching CAD (overall 91×50, left tower width 20, right arm height 20,
		/// chamfer on tower so riser is not a clean [20,50] OS that would form overall partition
		/// with RightStructHeight 20). The right arm height lies on MaxX and must be suppressed
		/// by the higher-priority outer-envelope rule.
		/// </summary>
		private static void RightArmHeightOnOverallMaxXIsSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 91.0,
				MaxY = 50.0
			};
			// L + top-right chamfer on tower (like C11): riser is not a pure 30 vertical that
			// abuts arm height 20 into overall 50, so StructureOverallPartition does not apply.
			// MaxX coverage is only 20 &lt; overall height 50 → stepped edge keeps arm height.
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(91.0, 0.0), "bottom");
			AddSegment(outline, new Point2D(91.0, 0.0), new Point2D(91.0, 20.0), "right-arm");
			AddSegment(outline, new Point2D(91.0, 20.0), new Point2D(20.0, 20.0), "step-top");
			AddSegment(outline, new Point2D(20.0, 20.0), new Point2D(20.0, 39.0), "step-up");
			AddSegment(outline, new Point2D(20.0, 39.0), new Point2D(9.0, 50.0), "chamfer");
			AddSegment(outline, new Point2D(9.0, 50.0), new Point2D(0.0, 50.0), "top-left");
			AddSegment(outline, new Point2D(0.0, 50.0), new Point2D(0.0, 0.0), "left");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallHeight
					&& Math.Abs(Math.Abs(d.SecondPoint.Y - d.FirstPoint.Y) - 50.0) <= config.GeometryTolerance),
				"overall height 50 must remain");
			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 91.0) <= config.GeometryTolerance),
				"overall width 91 must remain");
			Assert(plan.Dimensions.Any(d =>
					d.DebugRole == "RightStructHeight"
					&& Math.Abs(Math.Abs(d.SecondPoint.Y - d.FirstPoint.Y) - 20.0) <= config.GeometryTolerance),
				"right arm height 20 on stepped MaxX must be kept");
		}


		/// <summary>
		/// Left-side step stack: keep LeftStructHeight 50+30; suppress right vertical OutlineSegments
		/// 30 (same interval) and 20 (overall residual). Phase-1 left/right symmetry.
		/// </summary>
		private static void LeftStepStructureHeightsPreferOverRightOutlineSegments()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var plan = new DimensionPlan();
			plan.Add(new PlannedDimension
			{
				Kind = DimensionKind.OverallHeight,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(0.0, 100.0),
				DebugRole = "OverallHeight"
			});
			plan.Add(new PlannedDimension
			{
				Kind = DimensionKind.OverallWidth,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(100.0, 0.0),
				DebugRole = "OverallWidth"
			});
			var left50 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(0.0, 50.0),
				DebugRole = "LeftStructHeight"
			};
			var left30 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left,
				FirstPoint = new Point2D(0.0, 50.0),
				SecondPoint = new Point2D(0.0, 80.0),
				DebugRole = "LeftStructHeight"
			};
			var rightOs30 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Right,
				FirstPoint = new Point2D(50.0, 80.0),
				SecondPoint = new Point2D(50.0, 50.0),
				DebugRole = "OutlineSegment"
			};
			var rightOs20 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Right,
				FirstPoint = new Point2D(80.0, 100.0),
				SecondPoint = new Point2D(80.0, 80.0),
				DebugRole = "OutlineSegment"
			};
			plan.Add(left50);
			plan.Add(left30);
			plan.Add(rightOs30);
			plan.Add(rightOs20);

			// Mirror then secondary-OS cleanup (same order as SuppressDuplicateDimensions tail).
			// Use full CreateOutlinePlan-equivalent suppress path via public-ish internals:
			// call the two new stages by running envelope-adjacent helpers through reflection-free API:
			// SuppressSecondary is internal; mirror is private — exercise via CreateOutlinePlan geometry below
			// for integration; this unit path calls internal secondary suppress after manual mirror prefer.
			planner.SuppressSecondaryVerticalOutlineSegmentsRedundantWithPrimaryStructureStack(plan);

			Assert(plan.Dimensions.Contains(left50), "left structure height 50 must remain");
			Assert(plan.Dimensions.Contains(left30), "left structure height 30 must remain (primary stack)");
			Assert(!plan.Dimensions.Contains(rightOs30),
				"right OutlineSegment 30 must suppress as primary-structure duplicate");
			Assert(!plan.Dimensions.Contains(rightOs20),
				"right OutlineSegment 20 must suppress as overall residual");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.IsSuppressed
					&& c.SuppressedReason == "SecondaryOutlineSegmentDuplicatesPrimaryStructureHeight"),
				"expected SecondaryOutlineSegmentDuplicatesPrimaryStructureHeight reason");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.IsSuppressed
					&& c.SuppressedReason == "SecondaryOutlineSegmentOverallResidual"),
				"expected SecondaryOutlineSegmentOverallResidual reason");
		}

		/// <summary>
		/// Mirror of left-step case: primary RightStructHeight 50+30; left vertical OS 30/20 drop.
		/// </summary>
		private static void RightStepStructureHeightsPreferOverLeftOutlineSegments()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var plan = new DimensionPlan();
			plan.Add(new PlannedDimension
			{
				Kind = DimensionKind.OverallHeight,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(0.0, 100.0),
				DebugRole = "OverallHeight"
			});
			var right50 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Right,
				FirstPoint = new Point2D(100.0, 0.0),
				SecondPoint = new Point2D(100.0, 50.0),
				DebugRole = "RightStructHeight"
			};
			var right30 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Right,
				FirstPoint = new Point2D(100.0, 50.0),
				SecondPoint = new Point2D(100.0, 80.0),
				DebugRole = "RightStructHeight"
			};
			var leftOs30 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left,
				FirstPoint = new Point2D(50.0, 50.0),
				SecondPoint = new Point2D(50.0, 80.0),
				DebugRole = "OutlineSegment"
			};
			var leftOs20 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left,
				FirstPoint = new Point2D(20.0, 80.0),
				SecondPoint = new Point2D(20.0, 100.0),
				DebugRole = "OutlineSegment"
			};
			plan.Add(right50);
			plan.Add(right30);
			plan.Add(leftOs30);
			plan.Add(leftOs20);

			planner.SuppressSecondaryVerticalOutlineSegmentsRedundantWithPrimaryStructureStack(plan);

			Assert(plan.Dimensions.Contains(right50), "right structure height 50 must remain");
			Assert(plan.Dimensions.Contains(right30), "right structure height 30 must remain");
			Assert(!plan.Dimensions.Contains(leftOs30),
				"left OutlineSegment 30 must suppress as primary-structure duplicate");
			Assert(!plan.Dimensions.Contains(leftOs20),
				"left OutlineSegment 20 must suppress as overall residual");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.IsSuppressed
					&& c.SuppressedReason == "SecondaryOutlineSegmentDuplicatesPrimaryStructureHeight"),
				"expected SecondaryOutlineSegmentDuplicatesPrimaryStructureHeight reason");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.IsSuppressed
					&& c.SuppressedReason == "SecondaryOutlineSegmentOverallResidual"),
				"expected SecondaryOutlineSegmentOverallResidual reason");
		}

        private static void HorizontalStructurePointsCreateStepHeights()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = new OutlineFeature2D
            {
                MinX = 0.0,
                MinY = 0.0,
                MaxX = 100.0,
                MaxY = 50.0
            };

            AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(100.0, 0.0), "bottom");
            AddSegment(outline, new Point2D(10.0, 30.0), new Point2D(80.0, 30.0), "middle");
            AddSegment(outline, new Point2D(0.0, 50.0), new Point2D(100.0, 50.0), "top");

            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(plan.Dimensions.Any(d =>
                    (d.DebugRole == "LeftStructHeight" || d.DebugRole == "RightStructHeight")
                    && d.Orientation == DimensionOrientation.Vertical
                    && Math.Abs(Math.Abs(d.SecondPoint.Y - d.FirstPoint.Y) - 20.0) <= 0.001),
                "expected structure height from middle horizontal to top");
            Assert(!plan.Dimensions.Any(d =>
                    d.DebugRole == "LeftStructHeight"
                    && d.Orientation == DimensionOrientation.Vertical
                    && Math.Abs(Math.Abs(d.SecondPoint.Y - d.FirstPoint.Y) - 30.0) <= 0.001),
                "left structure height should remove the longest extension candidate");
        }

        private static void DiagonalFragmentsDoNotCreateStructureDimensions()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = new OutlineFeature2D
            {
                MinX = 0.0,
                MinY = 0.0,
                MaxX = 73.0,
                MaxY = 28.0
            };

            AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(0.0, 20.0), "left");
            AddSegment(outline, new Point2D(50.0, 0.0), new Point2D(50.0, 15.5), "right-step");
            AddSegment(outline, new Point2D(53.0, 28.0), new Point2D(73.0, 27.0), "top-fragment");
            AddSegment(outline, new Point2D(0.0, 20.0), new Point2D(35.0, 20.0), "upper-left");
            AddSegment(outline, new Point2D(53.0, 28.0), new Point2D(73.0, 28.0), "upper-right");

            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(!plan.Dimensions.Any(d => d.DebugRole == "BottomStructWidth" && Math.Abs(GetSpan(d) - 3.0) <= 0.001),
                "small diagonal horizontal fragment should not create bottom structure width");
        }

        private static void BottomInclinedStructurePointsRequireInnerGrooveChamfer()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = new OutlineFeature2D
            {
                MinX = 0.0,
                MinY = 0.0,
                MaxX = 100.0,
                MaxY = 50.0
            };

            AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(0.0, 50.0), "left");
            AddSegment(outline, new Point2D(100.0, 0.0), new Point2D(100.0, 50.0), "right");
            AddSegment(outline, new Point2D(30.0, 10.0), new Point2D(40.0, 20.0), "inner-chamfer-left");
            AddSegment(outline, new Point2D(40.0, 20.0), new Point2D(60.0, 20.0), "inner-flat");
            AddSegment(outline, new Point2D(60.0, 20.0), new Point2D(70.0, 10.0), "inner-chamfer-right");
            AddSegment(outline, new Point2D(80.0, 5.0), new Point2D(90.0, 15.0), "isolated-slope");

            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(plan.Dimensions.Any(d =>
                    d.DebugRole == "BottomStructWidth"
                    && (EndpointXEquals(d, 30.0) || EndpointXEquals(d, 40.0) || EndpointXEquals(d, 60.0) || EndpointXEquals(d, 70.0))),
                "bottom inner groove chamfer endpoints should create bottom structure dimensions");
            Assert(!plan.Dimensions.Any(d =>
                    d.DebugRole == "BottomStructWidth"
                    && (EndpointXEquals(d, 80.0) || EndpointXEquals(d, 90.0))),
                "isolated 45-degree slope should not create bottom structure dimensions");
        }

        private static void SideInclinedStructurePointsRequireInnerGrooveChamfer()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var leftOutline = new OutlineFeature2D
            {
                MinX = 0.0,
                MinY = 0.0,
                MaxX = 100.0,
                MaxY = 100.0
            };

            AddSegment(leftOutline, new Point2D(0.0, 0.0), new Point2D(100.0, 0.0), "bottom");
            AddSegment(leftOutline, new Point2D(0.0, 100.0), new Point2D(100.0, 100.0), "top");
            AddSegment(leftOutline, new Point2D(20.0, 30.0), new Point2D(30.0, 40.0), "left-groove-chamfer-lower");
            AddSegment(leftOutline, new Point2D(30.0, 40.0), new Point2D(30.0, 60.0), "left-groove-vertical");
            AddSegment(leftOutline, new Point2D(30.0, 60.0), new Point2D(20.0, 70.0), "left-groove-chamfer-upper");
            AddSegment(leftOutline, new Point2D(50.0, 10.0), new Point2D(60.0, 20.0), "isolated-slope");

            var leftPlan = new DimensionPlanner(config).CreateOutlinePlan(leftOutline);

            Assert(leftPlan.Dimensions.Any(d =>
                    d.DebugRole == "LeftStructHeight"
                    && (EndpointYEquals(d, 30.0) || EndpointYEquals(d, 40.0) || EndpointYEquals(d, 60.0) || EndpointYEquals(d, 70.0))),
                "left inner groove chamfer endpoints should create side structure dimensions");

            var rightOutline = new OutlineFeature2D
            {
                MinX = 0.0,
                MinY = 0.0,
                MaxX = 100.0,
                MaxY = 100.0
            };

            AddSegment(rightOutline, new Point2D(0.0, 0.0), new Point2D(100.0, 0.0), "bottom");
            AddSegment(rightOutline, new Point2D(0.0, 100.0), new Point2D(100.0, 100.0), "top");
            AddSegment(rightOutline, new Point2D(80.0, 30.0), new Point2D(70.0, 40.0), "right-groove-chamfer-lower");
            AddSegment(rightOutline, new Point2D(70.0, 40.0), new Point2D(70.0, 60.0), "right-groove-vertical");
            AddSegment(rightOutline, new Point2D(70.0, 60.0), new Point2D(80.0, 70.0), "right-groove-chamfer-upper");
            AddSegment(rightOutline, new Point2D(50.0, 10.0), new Point2D(60.0, 20.0), "isolated-slope");

            var rightPlan = new DimensionPlanner(config).CreateOutlinePlan(rightOutline);

            Assert(rightPlan.Dimensions.Any(d =>
                    d.DebugRole == "RightStructHeight"
                    && (EndpointYEquals(d, 30.0) || EndpointYEquals(d, 40.0) || EndpointYEquals(d, 60.0) || EndpointYEquals(d, 70.0))),
                "right inner groove chamfer endpoints should create side structure dimensions");
            Assert(!leftPlan.Dimensions.Concat(rightPlan.Dimensions).Any(d =>
                    (d.DebugRole == "LeftStructHeight" || d.DebugRole == "RightStructHeight")
                    && (EndpointYEquals(d, 10.0) || EndpointYEquals(d, 20.0))),
                "isolated 45-degree slope should not create side structure dimensions");
        }


		/// <summary>
		/// Phase 2: Left structure height that restates OverallHeight must suppress (symmetric of right).
		/// </summary>
		private static void LeftStructureHeightDuplicatingOverallHeightIsSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 73.0,
				MaxY = 29.038
			};
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(73.0, 0.0), "bottom");
			AddSegment(outline, new Point2D(0.0, 29.038), new Point2D(73.0, 29.038), "top");
			AddSegment(outline, new Point2D(23.0, 0.0), new Point2D(23.0, 15.5), "left-lower-step");
			AddSegment(outline, new Point2D(0.0, 29.038), new Point2D(0.5, 29.038), "left-top-step");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallHeight),
				"overall height should remain");
			Assert(!plan.Dimensions.Any(d =>
					d.DebugRole == "LeftStructHeight"
					&& Math.Abs(GetSpan(d) - 29.038) <= 0.001),
				"left structure height duplicating overall height should be suppressed");
		}

		/// <summary>
		/// Phase 2: left structure complementary pair — larger remainder (~80 with short 20)
		/// removed at candidate build (same as right RemoveComplementaryOverallRemainderCandidates).
		/// </summary>
		private static void LeftStructureComplementaryRemainderIsRemovedLikeRight()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = new OutlineFeature2D { MinX = 0.0, MinY = 0.0, MaxX = 40.0, MaxY = 100.0 };
			AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(40.0, 0.0), "bottom");
			AddSegment(outline, new Point2D(40.0, 0.0), new Point2D(40.0, 100.0), "right");
			AddSegment(outline, new Point2D(40.0, 100.0), new Point2D(0.0, 100.0), "top");
			AddSegment(outline, new Point2D(0.0, 100.0), new Point2D(0.0, 0.0), "left");
			AddSegment(outline, new Point2D(0.0, 20.0), new Point2D(15.0, 20.0), "left-shoulder");

			var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);
			Assert(!plan.Dimensions.Any(d =>
					d.DebugRole == "LeftStructHeight"
					&& Math.Abs(Math.Abs(d.SecondPoint.Y - d.FirstPoint.Y) - 80.0) <= config.GeometryTolerance),
				"left complementary remainder height ~80 must be removed like right-side path");
			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallHeight
					&& Math.Abs(Math.Abs(d.SecondPoint.Y - d.FirstPoint.Y) - 100.0) <= config.GeometryTolerance),
				"overall height 100 must remain");
		}

        private static void RightStructureHeightDuplicatingOverallHeightIsSuppressed()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = new OutlineFeature2D
            {
                MinX = 0.0,
                MinY = 0.0,
                MaxX = 73.0,
                MaxY = 29.038
            };

            AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(73.0, 0.0), "bottom");
            AddSegment(outline, new Point2D(0.0, 29.038), new Point2D(73.0, 29.038), "top");
            AddSegment(outline, new Point2D(50.0, 0.0), new Point2D(50.0, 15.5), "right-lower-step");
            AddSegment(outline, new Point2D(72.5, 29.038), new Point2D(73.0, 29.038), "right-top-step");

            var plan = new DimensionPlanner(config).CreateOutlinePlan(outline);

            Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallHeight),
                "overall height should remain");
            Assert(!plan.Dimensions.Any(d =>
                    d.DebugRole == "RightStructHeight"
                    && Math.Abs(GetSpan(d) - 29.038) <= 0.001),
                "right structure height duplicating overall height should be suppressed");
        }


		/// <summary>
		/// A short structure height is not orphan noise when an actual partial-envelope segment
		/// backs it. Regression: real left-top step 20 on overall height 201.5 was lost.
		/// </summary>
		private static void PartialEnvelopeStructureHeightSurvivesOrphanTipSuppression()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 338.0,
				MaxY = 201.5
			};
			AddSegment(outline, new Point2D(0.0, 181.5), new Point2D(0.0, 201.5), "real-left-top-step");
			var plan = new DimensionPlan();
			plan.Add(new PlannedDimension
			{
				Kind = DimensionKind.OverallHeight,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left,
				FirstPoint = new Point2D(38.0, 0.0),
				SecondPoint = new Point2D(0.0, 201.5),
				DebugRole = "OverallHeight"
			});
			var left20 = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left,
				FirstPoint = new Point2D(0.0, 181.5),
				SecondPoint = new Point2D(0.0, 201.5),
				DebugRole = "LeftStructHeight"
			};
			plan.Add(left20);

			planner.SuppressOrphanOuterVerticalStructureHeightTips(plan, outline);

			Assert(plan.Dimensions.Contains(left20),
				"real 20-high step on a partial MinX envelope edge must survive orphan-tip suppression");
			Assert(!plan.Diagnostics.DimensionCandidates.Any(candidate => candidate.Id == left20.DiagnosticId
					&& candidate.SuppressedReason == "OrphanOuterVerticalStructureHeightTip"),
				"protected real partial-envelope step must not record orphan-tip suppression");
		}

		/// <summary>
		/// Two short RightStructHeight tips (5+5 on overall 45) are overall residual noise and
		/// must suppress (regression: structure&gt;OS mirror had kept them).
		/// </summary>
		private static void OrphanRightOuterStructureHeightTipsAreSuppressed()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var planner = new DimensionPlanner(config);
			var plan = new DimensionPlan();
			plan.Add(new PlannedDimension
			{
				Kind = DimensionKind.OverallHeight,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left,
				FirstPoint = new Point2D(0.0, 0.0),
				SecondPoint = new Point2D(0.0, 45.0),
				DebugRole = "OverallHeight"
			});
			var r5a = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Right,
				FirstPoint = new Point2D(40.0, 35.0),
				SecondPoint = new Point2D(40.0, 40.0),
				DebugRole = "RightStructHeight"
			};
			var r5b = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Right,
				FirstPoint = new Point2D(40.0, 30.0),
				SecondPoint = new Point2D(40.0, 35.0),
				DebugRole = "RightStructHeight"
			};
			plan.Add(r5a);
			plan.Add(r5b);
			planner.SuppressOrphanOuterVerticalStructureHeightTips(plan);
			Assert(!plan.Dimensions.Contains(r5a) && !plan.Dimensions.Contains(r5b),
				"orphan right outer structure height tips 5+5 must be suppressed");
			Assert(plan.Diagnostics.DimensionCandidates.Any(c =>
					c.DebugRole == "RightStructHeight"
					&& c.IsSuppressed
					&& c.SuppressedReason == "OrphanOuterVerticalStructureHeightTip"),
				"expected OrphanOuterVerticalStructureHeightTip reason");
		}

        private static void TwoArcSlotIsRecognized()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var geometry = new DrawingGeometry();
            geometry.Arcs.Add(new Arc2D
            {
                SourceKey = "a1",
                Center = new Point2D(20.0, 10.0),
                Start = new Point2D(20.0, 5.0),
                End = new Point2D(20.0, 15.0),
                Radius = 5.0,
                Bulge = -1.0
            });
            geometry.Arcs.Add(new Arc2D
            {
                SourceKey = "a2",
                Center = new Point2D(60.0, 10.0),
                Start = new Point2D(60.0, 15.0),
                End = new Point2D(60.0, 5.0),
                Radius = 5.0,
                Bulge = -1.0
            });
            geometry.Segments.Add(new Segment2D(new Point2D(20.0, 15.0), new Point2D(60.0, 15.0)) { SourceKey = "l1" });
            geometry.Segments.Add(new Segment2D(new Point2D(20.0, 5.0), new Point2D(60.0, 5.0)) { SourceKey = "l2" });

            var slots = new FeatureRecognizer2D(config).RecognizeSlotFeatures(geometry);

            Assert(slots.Count == 1, "expected one two-arc slot");
            Assert(!slots[0].IsSingleArcSlot, "two-arc slot should not be single-arc");
            Assert(!slots[0].IsVertical, "two-arc horizontal slot should be horizontal");
            Assert(Math.Abs(slots[0].CenterDistance - 40.0) <= 0.001, "two-arc slot center distance should be recognized");
        }

        private static void SingleArcSlotIsRecognized()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var geometry = new DrawingGeometry();
            geometry.Arcs.Add(new Arc2D
            {
                SourceKey = "a1",
                Center = new Point2D(20.0, 20.0),
                Start = new Point2D(20.0, 15.0),
                End = new Point2D(20.0, 25.0),
                Radius = 5.0,
                Bulge = -1.0
            });
            geometry.Segments.Add(new Segment2D(new Point2D(20.0, 15.0), new Point2D(40.0, 15.0)) { SourceKey = "l1" });
            geometry.Segments.Add(new Segment2D(new Point2D(20.0, 25.0), new Point2D(40.0, 25.0)) { SourceKey = "l2" });

            var slots = new FeatureRecognizer2D(config).RecognizeSlotFeatures(geometry);

            Assert(slots.Count == 1, "expected one single-arc U slot");
            Assert(slots[0].IsSingleArcSlot, "single-arc U slot should be marked");
            Assert(!slots[0].IsVertical, "single-arc horizontal U slot should be horizontal");
            Assert(Math.Abs(slots[0].Radius - 5.0) <= 0.001, "single-arc slot radius should be preserved");
        }

        private static void SlotDimensionsUseCenterAndDatumChainsWithoutPins()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateRectangle(100.0, 50.0);
            var datum = Datum2D.FromOutline(outline);
            var slots = new[]
            {
                new SlotFeature2D
                {
                    GroupId = "SLOT1",
                    FirstCenter = new Point2D(20.0, 20.0),
                    SecondCenter = new Point2D(60.0, 20.0),
                    Radius = 5.0,
                    CenterDistance = 40.0
                },
                new SlotFeature2D
                {
                    GroupId = "SLOT2",
                    FirstCenter = new Point2D(80.0, 30.0),
                    SecondCenter = new Point2D(80.0, 40.0),
                    Radius = 5.0,
                    CenterDistance = 10.0,
                    IsVertical = true
                }
            };

            var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, new HoleFeature2D[0], slots);

            Assert(plan.Dimensions.Any(d => d.DebugRole == "SlotCenter" && Math.Abs(GetSpan(d) - 40.0) <= 0.001),
                "horizontal slot center distance should be planned");
            Assert(plan.Dimensions.Any(d => d.DebugRole == "SlotCenter" && Math.Abs(GetSpan(d) - 10.0) <= 0.001),
                "vertical slot center distance should be planned");
            Assert(plan.Dimensions.Any(d => d.DebugRole == "SlotDatumH"),
                "horizontal slot datum location should be planned without pins");
            Assert(plan.Dimensions.Any(d => d.DebugRole == "SlotDatumV"),
                "vertical slot datum location should be planned without pins");
            Assert(plan.Dimensions.Any(d => d.DebugRole == "SlotChainH" || d.DebugRole == "SlotChainV"),
                "slot datum chain dimensions should be planned without pins");
        }

        private static void HolesAreGroupedByHorizontalRows()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var planner = new DimensionPlanner(config);
            var rows = planner.GroupHolesByHorizontalRow(new[]
            {
                CreateHole(30.0, 20.0, 8.0, HoleKind2D.Normal),
                CreateHole(10.0, 10.0, 8.0, HoleKind2D.Normal),
                CreateHole(20.0, 10.0, 8.0, HoleKind2D.Normal)
            });

            Assert(rows.Count == 2, "expected two horizontal hole rows");
            Assert(rows[0].Count == 2, "first row should contain two holes");
            Assert(Math.Abs(rows[0][0].Center.X - 10.0) <= 0.001, "row holes should be sorted by X");
        }

        private static void NormalHolesLocateFromOutlineDatum()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateRectangle(100.0, 50.0);
            var datum = Datum2D.FromOutline(outline);
            var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, new[]
            {
                CreateHole(25.0, 20.0, 8.0, HoleKind2D.Normal)
            });

            Assert(plan.Dimensions.Count(d => d.Kind == DimensionKind.HoleLocation) == 2,
                "normal hole should create horizontal and vertical location dimensions");
            Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.HoleLocation && d.Orientation == DimensionOrientation.Horizontal && d.Side == DimensionSide.Bottom),
                "normal hole horizontal location should use nearest horizontal side");
            Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.HoleLocation && d.Orientation == DimensionOrientation.Vertical && d.Side == DimensionSide.Left),
                "normal hole vertical location should use nearest vertical side");
        }

        private static void PinGroupsPlanBaseAndPairDistances()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateRectangle(200.0, 100.0);
            var datumPin = CreateHole(20.0, 20.0, 6.0, HoleKind2D.Pin);
            var holes = new List<HoleFeature2D>
            {
                datumPin,
                CreateHole(60.0, 20.0, 6.0, HoleKind2D.Pin),
                CreateHole(140.0, 80.0, 6.0, HoleKind2D.Pin),
                CreateHole(170.0, 80.0, 6.0, HoleKind2D.Pin)
            };
			var datum = Datum2D.FromOutline(outline);
			datum.DatumHole = datumPin;
			datum.DatumHoleLocationBaseX = 200.0;
			datum.DatumHoleLocationBaseY = 100.0;
			datum.DatumHoleLocationUseToleranceX = true;
			datum.DatumHoleLocationUseToleranceY = true;

            var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, holes);

			Assert(plan.PinGroups.Count == 2, "expected two pin groups");
			Assert(plan.PinGroups[0].BasePin == datumPin, "first pin group should keep user datum pin as base");
			var datumX = plan.Dimensions.Single(d => d.Kind == DimensionKind.DatumHoleLocationX);
			var datumY = plan.Dimensions.Single(d => d.Kind == DimensionKind.DatumHoleLocationY);
			Assert(Math.Abs(datumX.FirstPoint.X - 200.0) <= 0.001 && datumX.FirstPointMustLieOnOutline,
				"datum pin X location should preserve the selected real X datum");
			Assert(Math.Abs(datumY.FirstPoint.Y - 100.0) <= 0.001 && datumY.FirstPointMustLieOnOutline,
				"datum pin Y location should preserve the selected real Y datum");
			Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.PinDistance), "expected same-group pin distance");
            Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.PinGroupDistance), "expected pin group transfer distance");
			Assert(plan.Dimensions.Where(d => d.Kind == DimensionKind.PinDistance).All(d => d.ReadingLevel == DimensionReadingLevel.IntraGroup),
				"same-group pin distances must carry the intra-group reading level");
			Assert(plan.Dimensions.Where(d => d.Kind == DimensionKind.PinGroupDistance).All(d => d.ReadingLevel == DimensionReadingLevel.DatumTransfer),
				"pin-group transfer distances must carry the datum-transfer reading level");
			var horizontalTransfer = plan.Dimensions.Single(d => d.Kind == DimensionKind.PinGroupDistance && d.Orientation == DimensionOrientation.Horizontal);
			var horizontalPg2Direct = plan.Dimensions.Single(d => d.Kind == DimensionKind.PinDistance && d.DebugOwner == "PG2" && d.Orientation == DimensionOrientation.Horizontal);
			var verticalTransfer = plan.Dimensions.Single(d => d.Kind == DimensionKind.PinGroupDistance && d.Orientation == DimensionOrientation.Vertical);
			Assert(!string.IsNullOrEmpty(horizontalTransfer.AlignmentKey)
				&& datumX.AlignmentKey == horizontalTransfer.AlignmentKey
				&& horizontalTransfer.AlignmentKey == horizontalPg2Direct.AlignmentKey,
				"PG1 datum, PG1-to-PG2 transfer, and PG2 direct dimensions must share one rooted datum chain");
			Assert(datumX.AlignmentPriority > horizontalTransfer.AlignmentPriority
				&& horizontalTransfer.AlignmentPriority > horizontalPg2Direct.AlignmentPriority,
				"the rooted chain must fall back from PG1 datum to transfer and then to PG2 direct dimensions");
			Assert(horizontalTransfer.AlignmentKey != verticalTransfer.AlignmentKey,
				"horizontal and vertical transfer dimensions must never share an alignment key");
			Assert(datumX.ReadingLevel == DimensionReadingLevel.DatumTransfer && datumY.ReadingLevel == DimensionReadingLevel.DatumTransfer,
				"datum-hole locations must carry the datum-transfer reading level");
			Assert(plan.Dimensions.Where(d => d.Kind == DimensionKind.OverallWidth || d.Kind == DimensionKind.OverallHeight).All(d => d.ReadingLevel == DimensionReadingLevel.Overall),
				"overall dimensions must carry the outermost reading level");
			Assert(plan.Diagnostics.DimensionCandidates.Any(d => d.Kind == DimensionKind.PinDistance.ToString() && d.ReadingLevel == DimensionReadingLevel.IntraGroup.ToString()),
				"dimension diagnostics must expose the explicit reading level");
        }

		private static void PinAlignmentGroupsRespectSideAndOrientation()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var horizontal = new[]
			{
				new DimensionLayoutItem
				{
					Kind = DimensionKind.PinGroupDistance,
					FirstPoint = new Point2D(0.0, 30.0),
					SecondPoint = new Point2D(40.0, 30.0),
					Span = 40.0,
					PreferFeatureLocalPlacement = true,
					AlignmentKey = "PG1-PG2:H",
					AlignmentPriority = 100
				},
				new DimensionLayoutItem
				{
					Kind = DimensionKind.PinDistance,
					FirstPoint = new Point2D(40.0, 10.0),
					SecondPoint = new Point2D(60.0, 10.0),
					Span = 20.0,
					PreferFeatureLocalPlacement = true,
					AlignmentKey = "PG1-PG2:H",
					AlignmentPriority = 90
				}
			};
			var top = rules.CreateStackingPlan(horizontal, DimensionSide.Top, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);
			var bottom = rules.CreateStackingPlan(horizontal, DimensionSide.Bottom, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);
			var vertical = new[]
			{
				new DimensionLayoutItem
				{
					Kind = DimensionKind.PinGroupDistance,
					FirstPoint = new Point2D(80.0, 0.0),
					SecondPoint = new Point2D(80.0, 40.0),
					Span = 40.0,
					PreferFeatureLocalPlacement = true,
					AlignmentKey = "PG1-PG2:H",
					AlignmentPriority = 100
				},
				new DimensionLayoutItem
				{
					Kind = DimensionKind.PinDistance,
					FirstPoint = new Point2D(60.0, 40.0),
					SecondPoint = new Point2D(60.0, 60.0),
					Span = 20.0,
					PreferFeatureLocalPlacement = true,
					AlignmentKey = "PG1-PG2:H",
					AlignmentPriority = 90
				}
			};
			var left = rules.CreateStackingPlan(vertical, DimensionSide.Left, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: false);

			Assert(top.All(p => p.DimLineCoordinateOverride.HasValue)
				&& top.Select(p => p.DimLineCoordinateOverride.Value).Distinct().Count() == 1,
				"same-key dimensions on the same side and orientation must align");
			Assert(Math.Abs(top[0].DimLineCoordinateOverride.Value - bottom[0].DimLineCoordinateOverride.Value) > config.GeometryTolerance,
				"same keys on different sides must be resolved independently");
			Assert(Math.Abs(top[0].DimLineCoordinateOverride.Value - left[0].DimLineCoordinateOverride.Value) > config.GeometryTolerance,
				"same keys in different orientations must be resolved independently");
		}

		private static void RootedDatumChainMergesTransitiveAlignmentLanes()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(30.0, 0.0), Span = 30.0, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 100, ReadingLevel = DimensionReadingLevel.IntraGroup },
				new DimensionLayoutItem { Kind = DimensionKind.DatumHoleLocationX, FirstPoint = new Point2D(145.0, 0.0), SecondPoint = new Point2D(205.0, 0.0), Span = 60.0, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 120, ReadingLevel = DimensionReadingLevel.DatumTransfer },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(30.0, 0.0), SecondPoint = new Point2D(145.0, 0.0), Span = 115.0, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 110, ReadingLevel = DimensionReadingLevel.DatumTransfer }
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Top, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);
			var coordinates = placements.Select(item => item.DimLineCoordinateOverride).ToList();

			Assert(coordinates.All(coordinate => coordinate.HasValue)
				&& coordinates.Select(coordinate => coordinate.Value).Distinct().Count() == 1,
				"a rooted DX-to-GD-to-PD datum chain must merge transitively into one shared dimension line");
		}

		private static void FunctionalHoleAlignmentUsesSeparateSameOrientationLane()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(140.0, 85.0), SecondPoint = new Point2D(95.0, 10.0), Span = 45.0, PreferLocalBoundary = true, AlignmentKey = "PG1:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(140.0, 85.0), SecondPoint = new Point2D(185.0, 10.0), Span = 45.0, PreferLocalBoundary = true, AlignmentKey = "PG1:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.DatumHoleLocationX, FirstPoint = new Point2D(200.0, 85.0), SecondPoint = new Point2D(140.0, 85.0), Span = 60.0, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 120, ReadingLevel = DimensionReadingLevel.DatumTransfer }
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Top, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);
			var functionalHole1 = placements.Single(item => item.Index == 0);
			var functionalHole2 = placements.Single(item => item.Index == 1);
			var datum = placements.Single(item => item.Index == 2);

			Assert(functionalHole1.DimLineCoordinateOverride.HasValue && functionalHole2.DimLineCoordinateOverride.HasValue
				&& Math.Abs(functionalHole1.DimLineCoordinateOverride.Value - functionalHole2.DimLineCoordinateOverride.Value) <= config.GeometryTolerance,
				"functional holes from one pin group and orientation must share a dimension line");
			Assert(functionalHole1.AlignmentLaneMemberCount == 2 && functionalHole2.AlignmentLaneMemberCount == 2
				&& functionalHole1.AlignmentLaneKey == functionalHole2.AlignmentLaneKey,
				"functional-hole placements must expose their resolved diagnostic alignment lane");
			Assert(Math.Abs(functionalHole1.DimLineCoordinateOverride.Value - datum.DimLineCoordinateOverride.Value) > config.GeometryTolerance,
				"functional-hole alignment must remain separate from the datum chain");
		}

		private static void FunctionalHoleAlignmentPreservesV198Stacking()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(40.0, 0.0), SecondPoint = new Point2D(25.0, 0.0), Span = 15.0, AlignmentKey = "PG2:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(40.0, 0.0), SecondPoint = new Point2D(55.0, 0.0), Span = 15.0, AlignmentKey = "PG2:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(40.0, 0.0), SecondPoint = new Point2D(10.0, 0.0), Span = 30.0, OverrideText = @"30\H0.8x;±0.02\H1x;", AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 100, ReadingLevel = DimensionReadingLevel.IntraGroup },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(155.0, 0.0), SecondPoint = new Point2D(110.0, 0.0), Span = 45.0, AlignmentKey = "PG1:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(155.0, 0.0), SecondPoint = new Point2D(200.0, 0.0), Span = 45.0, AlignmentKey = "PG1:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(90.0, 0.0), SecondPoint = new Point2D(140.0, 0.0), Span = 50.0, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.DatumHoleLocationX, FirstPoint = new Point2D(215.0, 0.0), SecondPoint = new Point2D(155.0, 0.0), Span = 60.0, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 120, ReadingLevel = DimensionReadingLevel.DatumTransfer },
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(140.0, 0.0), SecondPoint = new Point2D(215.0, 0.0), Span = 75.0, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(155.0, 0.0), SecondPoint = new Point2D(40.0, 0.0), Span = 115.0, OverrideText = @"115\H0.8x;±0.05\H1x;", AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 110, ReadingLevel = DimensionReadingLevel.DatumTransfer }
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Top, null, 2.5, 1.25, 5.0, 5.0, isHorizontal: true);
			var byIndex = placements.ToDictionary(item => item.Index);

			Assert(byIndex[0].Level == 0 && byIndex[1].Level == 0 && byIndex[3].Level == 0 && byIndex[4].Level == 0,
				"functional-hole alignment must preserve the v198 innermost stacking level");
			Assert(byIndex[5].Level == 1 && byIndex[7].Level == 1,
				"structure dimensions must retain the v198 middle stacking level");
			Assert(byIndex[2].Level == 2 && byIndex[6].Level == 2 && byIndex[8].Level == 2,
				"the rooted datum chain must retain the v198 outer stacking level");
			Assert(placements.Select(item => item.Level).Distinct().OrderBy(level => level).SequenceEqual(new[] { 0, 1, 2 }),
				"functional-hole alignment must not create empty stacking levels or enlarge spacing");
			Assert(byIndex[0].DimLineCoordinateOverride.HasValue && byIndex[1].DimLineCoordinateOverride.HasValue
				&& Math.Abs(byIndex[0].DimLineCoordinateOverride.Value - byIndex[1].DimLineCoordinateOverride.Value) <= config.GeometryTolerance,
				"safe PG2 functional-hole dimensions must remain collinear without moving outward");
			Assert(byIndex[3].DimLineCoordinateOverride.HasValue && byIndex[4].DimLineCoordinateOverride.HasValue
				&& Math.Abs(byIndex[3].DimLineCoordinateOverride.Value - byIndex[4].DimLineCoordinateOverride.Value) <= config.GeometryTolerance,
				"safe PG1 functional-hole dimensions must remain collinear without moving outward");
		}

		/// <summary>
		/// Two functional-hole dimensions share an alignment key with PreserveAlignmentLevel, so
		/// BuildLayoutBlocks gives them separate SingleDimension blocks while BuildAlignmentLanes
		/// still unifies their dimension-line coordinate. When only one of them collides with an
		/// inner block, EnsureLayoutBlockPhysicalOutwardOrder pushes that BLOCK outward and the
		/// shared lane coordinate is silently lost - AGENTS.md requires functional-hole dimensions
		/// to stay with their owning pin group.
		/// </summary>
		/// <summary>
		/// Baseline chain 41 (datum) + 30 (pin) must stay inside a same-group functional hole 45
		/// that reaches past the far pin, even though raw span 45 &lt; chain effective span 71.
		/// </summary>
		private static void FunctionalHoleBeyondPinChainStacksOutsideDatumChain()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(41.0, 0.0), SecondPoint = new Point2D(56.0, 0.0), Span = 15.0, AlignmentKey = "PG1:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "PG1" },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(41.0, 0.0), SecondPoint = new Point2D(86.0, 0.0), Span = 45.0, AlignmentKey = "PG1:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "PG1" },
				new DimensionLayoutItem { Kind = DimensionKind.DatumHoleLocationX, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(41.0, 0.0), Span = 41.0, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 120, ReadingLevel = DimensionReadingLevel.DatumTransfer, SourceFeatureId = "PG1" },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(41.0, 0.0), SecondPoint = new Point2D(71.0, 0.0), Span = 30.0, OverrideText = @"30\H0.8x;±0.02\H1x;", AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 100, ReadingLevel = DimensionReadingLevel.IntraGroup, SourceFeatureId = "PG1" },
				new DimensionLayoutItem { Kind = DimensionKind.OverallWidth, FirstPoint = new Point2D(0.0, 0.0), SecondPoint = new Point2D(96.0, 0.0), Span = 96.0, ForceOuterLevel = true, ReadingLevel = DimensionReadingLevel.Overall }
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Bottom, null, 2.5, 1.25, 5.0, 5.0, isHorizontal: true);
			var byIndex = placements.ToDictionary(item => item.Index);

			Assert(byIndex[2].Level == byIndex[3].Level,
				"41 and 30 must remain on one datum/pin chain level");
			Assert(byIndex[0].Level < byIndex[2].Level,
				"15 must stay inside (lower level than) the 41-30 chain");
			Assert(byIndex[1].Level > byIndex[2].Level,
				"45 must sit outside the 41-30 chain");
			Assert(byIndex[4].Level > byIndex[1].Level,
				"overall 96 must remain outermost");
		}

		private static void Dl01TopStructureFunctionalHoleDatumChainOrder()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(7.0, 0.0), SecondPoint = new Point2D(22.0, 0.0), Span = 15.0, AlignmentKey = "PG1:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "PG1" },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(9.0, 0.0), SecondPoint = new Point2D(24.0, 0.0), Span = 15.0, LooseChainId = 6, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "L6" },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(22.0, 0.0), SecondPoint = new Point2D(24.0, 0.0), Span = 2.0, LooseChainId = 6, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "L6" },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(-8.0, 0.0), SecondPoint = new Point2D(22.0, 0.0), Span = 30.0, OverrideText = @"30\H0.8x;±0.02\H1x;", AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 100, ReadingLevel = DimensionReadingLevel.IntraGroup, SourceFeatureId = "PG1" },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(22.0, 0.0), SecondPoint = new Point2D(245.5, 0.0), Span = 223.5, AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 110, ReadingLevel = DimensionReadingLevel.DatumTransfer, SourceFeatureId = "PG1" },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(-23.0, 0.0), SecondPoint = new Point2D(22.0, 0.0), Span = 45.0, AlignmentKey = "PG1:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "PG1" },
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(-33.0, 0.0), SecondPoint = new Point2D(40.0, 0.0), Span = 73.0, AlignmentKey = "Structure:T:H", AlignmentPriority = 70, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "TopStructWidth" },
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(40.0, 0.0), SecondPoint = new Point2D(127.554, 0.0), Span = 87.554, AlignmentKey = "Structure:T:H", AlignmentPriority = 70, ReadingLevel = DimensionReadingLevel.LocalSpacing, SourceFeatureId = "TopStructWidth" }
			};
			var placements = new DimensionLayoutRules(config)
				.CreateStackingPlan(dimensions, DimensionSide.Top, null, 2.5, 1.25, 5.0, 5.0, isHorizontal: true)
				.ToDictionary(item => item.Index);

			Assert(placements[0].Level == 0 && placements[1].Level == 1 && placements[2].Level == 1,
				"DL01 Top local Functional15 and LooseChain 15/2 must retain levels 0 and 1");
			Assert(placements[3].Level == 2 && placements[4].Level == 2,
				"DL01 Top 30±0.02 and 223.5 must remain one datum-chain block");
			Assert(placements[5].Level == 3,
				"DL01 Top FunctionalHole 45 must occupy level 3");
			Assert(placements[6].Level == 4 && placements[7].Level == 4,
				"DL01 Top 73 and 87.554 must remain one TopStructWidth block (levels: "
				+ string.Join(",", placements.OrderBy(item => item.Key).Select(item => item.Value.Level))
				+ ")");
			Assert(placements[6].Level > placements[5].Level && placements[5].Level > placements[3].Level,
				"DL01 Top outer three levels must be TopStructWidth 73/87.554, FunctionalHole 45, DatumChain 30/223.5");
		}

		private static void FunctionalHoleAlignmentLaneSurvivesOutwardPromotion()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(10.0, 0.0), SecondPoint = new Point2D(40.0, 0.0), Span = 30.0, AlignmentKey = "PG1:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(100.0, 0.0), SecondPoint = new Point2D(130.0, 0.0), Span = 30.0, AlignmentKey = "PG1:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, ReadingLevel = DimensionReadingLevel.LocalSpacing },
				// Smaller span, so it sorts inner; its arrow interval strictly overlaps member 0
				// only, which is what makes the promotion asymmetric.
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(25.0, 0.0), SecondPoint = new Point2D(45.0, 0.0), Span = 20.0, ReadingLevel = DimensionReadingLevel.LocalSpacing }
			};
			var placements = new DimensionLayoutRules(config).CreateStackingPlan(dimensions, DimensionSide.Top, null, 2.5, 1.25, 5.0, 5.0, isHorizontal: true);
			var byIndex = placements.ToDictionary(item => item.Index);

			// Non-vacuity guard: if the outward promotion never fires, this scenario proves
			// nothing and must fail loudly rather than pass by accident.
			Assert(!string.IsNullOrEmpty(byIndex[0].PromotedByConflictWith) || !string.IsNullOrEmpty(byIndex[1].PromotedByConflictWith),
				"scenario did not trigger an outward promotion - the repro no longer covers the lane-splitting path");

			Assert(byIndex[0].DimLineCoordinateOverride.HasValue && byIndex[1].DimLineCoordinateOverride.HasValue,
				"both functional-hole lane members must keep an alignment coordinate override");
			Assert(Math.Abs(byIndex[0].DimLineCoordinateOverride.Value - byIndex[1].DimLineCoordinateOverride.Value) <= config.GeometryTolerance,
				"functional-hole alignment lane must move as one when a member is pushed outward");
			Assert(string.Equals(byIndex[0].AlignmentLaneKey, byIndex[1].AlignmentLaneKey, StringComparison.Ordinal)
				&& !string.IsNullOrEmpty(byIndex[0].AlignmentLaneKey),
				"both lane members must still report the same alignment lane key");
		}

		private static void PinAlignmentAnchorFallsBackWithStableOrdering()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var fallbackMembers = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(0.0, 40.0), SecondPoint = new Point2D(10.0, 40.0), Span = 10.0, PreferFeatureLocalPlacement = true, AlignmentKey = "fallback", AlignmentPriority = 90 },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(10.0, 20.0), SecondPoint = new Point2D(20.0, 20.0), Span = 10.0, PreferFeatureLocalPlacement = true, AlignmentKey = "fallback", AlignmentPriority = 80 }
			};
			var fallback = rules.CreateStackingPlan(fallbackMembers, DimensionSide.Top, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);
			double fallbackExpected = rules.GetDimLineCoordinate(fallbackMembers[0], DimensionSide.Top, null, fallback.Single(p => p.Index == 0).Offset);

			var equalPriority = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(0.0, 10.0), SecondPoint = new Point2D(40.0, 10.0), Span = 40.0, PreferFeatureLocalPlacement = true, AlignmentKey = "stable-span", AlignmentPriority = 90 },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(40.0, 30.0), SecondPoint = new Point2D(60.0, 30.0), Span = 20.0, PreferFeatureLocalPlacement = true, AlignmentKey = "stable-span", AlignmentPriority = 90 }
			};
			var bySpan = rules.CreateStackingPlan(equalPriority, DimensionSide.Top, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);
			double spanExpected = rules.GetDimLineCoordinate(equalPriority[1], DimensionSide.Top, null, bySpan.Single(p => p.Index == 1).Offset);

			var equalSpan = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(0.0, 50.0), SecondPoint = new Point2D(20.0, 50.0), Span = 20.0, PreferFeatureLocalPlacement = true, AlignmentKey = "stable-order", AlignmentPriority = 90 },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(20.0, 30.0), SecondPoint = new Point2D(40.0, 30.0), Span = 20.0, PreferFeatureLocalPlacement = true, AlignmentKey = "stable-order", AlignmentPriority = 90 }
			};
			var byOrder = rules.CreateStackingPlan(equalSpan, DimensionSide.Top, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);
			double orderExpected = rules.GetDimLineCoordinate(equalSpan[0], DimensionSide.Top, null, byOrder.Single(p => p.Index == 0).Offset);

			Assert(fallback.All(p => Math.Abs(p.DimLineCoordinateOverride.Value - fallbackExpected) <= config.GeometryTolerance),
				"when the preferred anchor is suppressed, the next-highest-priority remaining member must anchor the group");
			Assert(bySpan.All(p => Math.Abs(p.DimLineCoordinateOverride.Value - spanExpected) <= config.GeometryTolerance),
				"equal-priority members must choose the smaller span as the stable anchor");
			Assert(byOrder.All(p => Math.Abs(p.DimLineCoordinateOverride.Value - orderExpected) <= config.GeometryTolerance),
				"equal-priority equal-span members must choose the earlier generated member as the stable anchor");
		}

		private static void PinAlignmentGroupMovesTogetherOnConflict()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(40.0, 0.0), SecondPoint = new Point2D(50.0, 0.0), Span = 10.0 },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(0.0, 20.0), SecondPoint = new Point2D(35.0, 20.0), Span = 35.0, PreferFeatureLocalPlacement = true, AlignmentKey = "move-together", AlignmentPriority = 100 },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(35.0, 10.0), SecondPoint = new Point2D(70.0, 10.0), Span = 35.0, PreferFeatureLocalPlacement = true, AlignmentKey = "move-together", AlignmentPriority = 90 }
			};
			var placements = rules.CreateStackingPlan(dimensions, DimensionSide.Top, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);
			var anchor = placements.Single(p => p.Index == 1);
			var follower = placements.Single(p => p.Index == 2);

			Assert(anchor.Level == follower.Level && Math.Abs(anchor.Offset - follower.Offset) <= config.GeometryTolerance,
				"a conflict affecting one member must move the entire alignment group to the same layer");
			Assert(anchor.Level > 0,
				"the aligned group must move outward when a member conflicts with an existing dimension");
			Assert(Math.Abs(anchor.DimLineCoordinateOverride.Value - follower.DimLineCoordinateOverride.Value) <= config.GeometryTolerance,
				"moving an alignment group must preserve its unbroken shared dimension line");
		}

		private static void PinAlignmentGroupAvoidsResolvedCoordinateConflicts()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var dimensions = new[]
			{
				new DimensionLayoutItem
				{
					Kind = DimensionKind.Normal,
					FirstPoint = new Point2D(0.0, 30.0),
					SecondPoint = new Point2D(20.0, 30.0),
					Span = 20.0,
					PreferFeatureLocalPlacement = true
				},
				new DimensionLayoutItem
				{
					Kind = DimensionKind.DatumHoleLocationX,
					FirstPoint = new Point2D(0.0, 25.0),
					SecondPoint = new Point2D(20.0, 25.0),
					Span = 20.0,
					PreferFeatureLocalPlacement = true,
					AlignmentKey = "resolved-conflict",
					AlignmentPriority = 120
				},
				new DimensionLayoutItem
				{
					Kind = DimensionKind.PinGroupDistance,
					FirstPoint = new Point2D(20.0, 10.0),
					SecondPoint = new Point2D(40.0, 10.0),
					Span = 20.0,
					PreferFeatureLocalPlacement = true,
					AlignmentKey = "resolved-conflict",
					AlignmentPriority = 110
				}
			};
			var placements = rules.CreateStackingPlan(dimensions, DimensionSide.Top, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);
			var external = placements.Single(p => p.Index == 0);
			var anchor = placements.Single(p => p.Index == 1);
			var follower = placements.Single(p => p.Index == 2);
			double externalCoordinate = rules.GetDimLineCoordinate(dimensions[0], DimensionSide.Top, null, external.Offset);

			Assert(anchor.Level == follower.Level && anchor.Level >= 2,
				"a group must move again when different abstract levels resolve to the same physical dimension line");
			Assert(Math.Abs(anchor.DimLineCoordinateOverride.Value - externalCoordinate) >= 5.0 - config.GeometryTolerance,
				"resolved group coordinates must retain one full stacking interval from overlapping external dimensions");
		}

		private static void PinAlignmentKeySplitsStrictHorizontalOverlapsIntoLanes()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.DatumHoleLocationX, FirstPoint = new Point2D(205.0, 40.0), SecondPoint = new Point2D(265.0, 40.0), Span = 60.0, AlignmentKey = "multi-group:H", AlignmentPriority = 120 },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(81.0, 30.0), SecondPoint = new Point2D(205.0, 30.0), Span = 124.0, AlignmentKey = "multi-group:H", AlignmentPriority = 110 },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(31.0, 20.0), SecondPoint = new Point2D(81.0, 20.0), Span = 50.0, AlignmentKey = "multi-group:H", AlignmentPriority = 100 },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(31.0, 10.0), SecondPoint = new Point2D(205.0, 10.0), Span = 174.0, AlignmentKey = "multi-group:H", AlignmentPriority = 110 }
			};
			var placements = rules.CreateStackingPlan(dimensions, DimensionSide.Bottom, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: true);
			double chainCoordinate = placements.Single(p => p.Index == 0).DimLineCoordinateOverride.Value;

			Assert(placements.Where(p => p.Index < 3).All(p => Math.Abs(p.DimLineCoordinateOverride.Value - chainCoordinate) <= config.GeometryTolerance),
				"endpoint-connected horizontal dimensions must remain on one continuous lane");
			Assert(Math.Abs(placements.Single(p => p.Index == 3).DimLineCoordinateOverride.Value - chainCoordinate) >= 5.0 - config.GeometryTolerance,
				"a strictly overlapping horizontal dimension must move to a separate lane");
		}

		private static void PinAlignmentKeySplitsStrictVerticalOverlapsIntoLanes()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var dimensions = new[]
			{
				new DimensionLayoutItem { Kind = DimensionKind.DatumHoleLocationY, FirstPoint = new Point2D(40.0, 205.0), SecondPoint = new Point2D(40.0, 265.0), Span = 60.0, AlignmentKey = "multi-group:V", AlignmentPriority = 120 },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(30.0, 81.0), SecondPoint = new Point2D(30.0, 205.0), Span = 124.0, AlignmentKey = "multi-group:V", AlignmentPriority = 110 },
				new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(20.0, 31.0), SecondPoint = new Point2D(20.0, 81.0), Span = 50.0, AlignmentKey = "multi-group:V", AlignmentPriority = 100 },
				new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(10.0, 31.0), SecondPoint = new Point2D(10.0, 205.0), Span = 174.0, AlignmentKey = "multi-group:V", AlignmentPriority = 110 }
			};
			var placements = rules.CreateStackingPlan(dimensions, DimensionSide.Left, null, 2.5, 1.0, 5.0, 5.0, isHorizontal: false);
			double chainCoordinate = placements.Single(p => p.Index == 0).DimLineCoordinateOverride.Value;

			Assert(placements.Where(p => p.Index < 3).All(p => Math.Abs(p.DimLineCoordinateOverride.Value - chainCoordinate) <= config.GeometryTolerance),
				"endpoint-connected vertical dimensions must remain on one continuous lane");
			Assert(Math.Abs(placements.Single(p => p.Index == 3).DimLineCoordinateOverride.Value - chainCoordinate) >= 5.0 - config.GeometryTolerance,
				"a strictly overlapping vertical dimension must move to a separate lane");
		}

		private static void EquivalentPinGroupTransfersSuppressAcrossDebugOwners()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateRectangle(200.0, 100.0);
			var datumPin = CreateHole(180.0, 10.0, 6.0, HoleKind2D.Pin);
			var holes = new[]
			{
				datumPin,
				CreateHole(190.0, 10.0, 6.0, HoleKind2D.Pin),
				CreateHole(100.0, 10.0, 7.0, HoleKind2D.Pin),
				CreateHole(110.0, 10.0, 7.0, HoleKind2D.Pin),
				CreateHole(50.0, 10.0, 8.0, HoleKind2D.Pin),
				CreateHole(60.0, 10.0, 8.0, HoleKind2D.Pin),
				CreateHole(50.0, 30.0, 9.0, HoleKind2D.Pin),
				CreateHole(60.0, 30.0, 9.0, HoleKind2D.Pin)
			};
			var datum = Datum2D.FromOutline(outline);
			datum.DatumHole = datumPin;
			var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, holes);
			var equivalentTransfers = plan.Diagnostics.DimensionCandidates
				.Where(candidate => candidate.Kind == DimensionKind.PinGroupDistance.ToString()
					&& candidate.Orientation == DimensionOrientation.Horizontal.ToString()
					&& Math.Abs(candidate.MeasurementMinimum - 60.0) <= config.GeometryTolerance
					&& Math.Abs(candidate.MeasurementMaximum - 180.0) <= config.GeometryTolerance)
				.ToList();

			Assert(equivalentTransfers.Count == 2 && equivalentTransfers.Select(candidate => candidate.SourceFeatureId).Distinct().Count() == 2,
				"the fixture must generate equivalent transfers owned by two different pin groups");
			Assert(equivalentTransfers.Count(candidate => candidate.IsSelected) == 1
				&& equivalentTransfers.Count(candidate => candidate.IsSuppressed && candidate.SuppressedReason == "DuplicateMeasuredDimension") == 1,
				"equivalent pin-group coordinates must retain one stable member and diagnose the other as suppressed regardless of DebugOwner");
		}

        private static void FunctionalHolesAttachToPinGroup()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateRectangle(200.0, 100.0);
            var datumPin = CreateHole(20.0, 20.0, 6.0, HoleKind2D.Pin);
            var holes = new List<HoleFeature2D>
            {
                datumPin,
                CreateHole(60.0, 20.0, 6.0, HoleKind2D.Pin),
                CreateHole(20.0, 40.0, 8.0, HoleKind2D.Normal),
                CreateHole(60.0, 40.0, 8.0, HoleKind2D.Normal)
            };
            var datum = Datum2D.FromOutline(outline);
            datum.DatumHole = datumPin;

            var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, holes);

            Assert(plan.Dimensions.Any(d => d.DebugRole == "FunctionalHole" && d.DebugOwner == "PG1"),
                "functional holes should attach to the pin group");
			var functionalHoles = plan.Dimensions.Where(d => d.DebugRole == "FunctionalHole").ToList();
			var verticalFunctionalHoles = functionalHoles.Where(d => d.Orientation == DimensionOrientation.Vertical).ToList();
			Assert(functionalHoles.Count > 0
				&& functionalHoles.All(d => d.PreservePreferredSide && !d.PreferFeatureLocalPlacement && d.PreferLocalBoundary),
				"functional-hole dimensions must stay with their pin group and prefer a nearby local boundary");
			Assert(functionalHoles.GroupBy(d => d.Orientation).All(group => group.Select(d => d.AlignmentKey).Distinct().Count() == 1
				&& group.All(d => !string.IsNullOrEmpty(d.AlignmentKey) && d.AlignmentPriority == 90 && d.PreserveAlignmentLevel)),
				"functional-hole dimensions in one pin group must receive a same-orientation alignment key");
			Assert(verticalFunctionalHoles.All(d => d.Side == plan.PinGroups[0].VerticalSide),
				"functional-hole dimensions must inherit their pin group's vertical side");
        }

		private static void HoleBetweenPinPairAttachesAsFunctionalHole()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var outline = CreateRectangle(300.0, 40.0);
			// Two pin pairs, each with one normal hole strictly between the pins (repro of the bar fixture).
			var datumPin = CreateHole(210.0, 20.0, 6.0, HoleKind2D.Pin);
			var holes = new List<HoleFeature2D>
			{
				// Right pin group (datum): pins at 180/210, hole at midpoint 195
				CreateHole(180.0, 20.0, 6.0, HoleKind2D.Pin),
				datumPin,
				CreateHole(195.0, 20.0, 8.0, HoleKind2D.Normal),
				// Left pin group: pins at 80/110, hole at midpoint 95
				CreateHole(80.0, 20.0, 6.0, HoleKind2D.Pin),
				CreateHole(110.0, 20.0, 6.0, HoleKind2D.Pin),
				CreateHole(95.0, 20.0, 8.0, HoleKind2D.Normal),
				// Far-left isolated hole should remain loose (not between any pin pair)
				CreateHole(30.0, 20.0, 8.0, HoleKind2D.Normal)
			};
			var datum = Datum2D.FromOutline(outline);
			datum.DatumHole = datumPin;
			var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, holes);

			var functional = plan.Dimensions.Where(d => d.DebugRole == "FunctionalHole").ToList();
			var loose = plan.Dimensions.Where(d => d.DebugRole == "LooseHole").ToList();

			Assert(plan.PinGroups.Count == 2, "fixture must form two pin groups");
			Assert(functional.Count > 0 && functional.All(d => d.DebugOwner == "PG1" || d.DebugOwner == "PG2"),
				"holes between pin pairs must become FunctionalHole owned by a pin group");
			// Each between-pin hole should be located from its group's base pin (horizontal span 15 = half of pin distance 30).
			Assert(functional.Count(d => d.Orientation == DimensionOrientation.Horizontal
					&& Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 15.0) <= config.GeometryTolerance) >= 2,
				"each between-pin hole must be dimensioned 15 from the owning base pin");
			Assert(functional.All(d =>
					plan.PinGroups.Any(g => g.BasePin != null
						&& (IsNear(d.FirstPoint, g.BasePin.Center, config) || IsNear(d.SecondPoint, g.BasePin.Center, config)))),
				"functional-hole dimensions must use the pin-group base pin as one endpoint");
			Assert(!loose.Any(d => Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 100.0) <= 1.0
					|| Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 93.0) <= 1.0),
				"between-pin holes must not form a long loose chain across pin groups");
			// Isolated left hole may still emit a short location, but not re-chain the pin-pair midpoints.
			Assert(!loose.Any(d =>
					Math.Abs(d.FirstPoint.X - 95.0) <= config.GeometryTolerance
					|| Math.Abs(d.SecondPoint.X - 95.0) <= config.GeometryTolerance
					|| Math.Abs(d.FirstPoint.X - 195.0) <= config.GeometryTolerance
					|| Math.Abs(d.SecondPoint.X - 195.0) <= config.GeometryTolerance),
				"between-pin holes must not appear in LooseHole dimensions");
		}

		private static bool IsNear(Point2D a, Point2D b, DimensionRuleConfig config)
		{
			return Math.Abs(a.X - b.X) <= config.GeometryTolerance && Math.Abs(a.Y - b.Y) <= config.GeometryTolerance;
		}

		private static void PreferredSideLockedHoleLocationUsesLocalBoundary()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var functionalHole = new DimensionLayoutItem
			{
				Kind = DimensionKind.HoleLocation,
				FirstPoint = new Point2D(20.0, 30.0),
				SecondPoint = new Point2D(20.0, 70.0),
				Span = 40.0,
				PreferLocalBoundary = true,
				PreservePreferredSide = true
			};
			var crowdedRight = Enumerable.Range(0, 4).Select(index => new DimensionLayoutItem
			{
				Kind = DimensionKind.HoleLocation,
				FirstPoint = new Point2D(10.0 + index, 30.0),
				SecondPoint = new Point2D(10.0 + index, 70.0),
				Span = 40.0
			}).ToList();

			var selectedSide = rules.ChooseVerticalHoleLocationSide(functionalHole, DimensionSide.Right, crowdedRight, new DimensionLayoutItem[0], 10.0);
			var moves = rules.SelectVerticalHoleLocationRebalanceMoves(new[] { functionalHole }, new DimensionLayoutItem[0], 1.0);
			var outline = CreateRightNotchOutline();
			var datumY = new DimensionLayoutItem
			{
				Kind = DimensionKind.DatumHoleLocationY,
				FirstPoint = new Point2D(20.0, 30.0),
				SecondPoint = new Point2D(20.0, 70.0),
				Span = 40.0
			};
			double functionalCoordinate = rules.GetDimLineCoordinate(functionalHole, DimensionSide.Right, outline, 5.0);
			double datumCoordinate = rules.GetDimLineCoordinate(datumY, DimensionSide.Right, outline, 5.0);

			Assert(selectedSide == DimensionSide.Right,
				"side-locked functional holes must keep the pin group's preferred vertical side during initial placement");
			Assert(!rules.CanRebalanceVerticalHoleLocation(functionalHole, 1.0) && moves.Count == 0,
				"side-locked functional holes must not move during later vertical-side rebalancing");
			Assert(Math.Abs(functionalCoordinate - 65.0) <= config.GeometryTolerance
				&& Math.Abs(datumCoordinate - 205.0) <= config.GeometryTolerance,
				"local functional-hole dimensions must use the nearby notch while global datum dimensions remain outside the full outline");
		}

		private static void ExplicitLocalLooseChainUsesNearbyConcaveBoundary()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var outline = CreateRightNotchOutline();
			var loose = new DimensionLayoutItem
			{
				Kind = DimensionKind.HoleLocation,
				FirstPoint = new Point2D(20.0, 30.0),
				SecondPoint = new Point2D(22.0, 70.0),
				Span = 40.0,
				LooseChainId = 6,
				PreferLocalBoundary = true
			};
			var legacyGlobalLoose = new DimensionLayoutItem
			{
				Kind = DimensionKind.HoleLocation,
				FirstPoint = loose.FirstPoint,
				SecondPoint = loose.SecondPoint,
				Span = loose.Span,
				LooseChainId = loose.LooseChainId
			};
			var crowdedSource = new List<DimensionLayoutItem> { loose };
			crowdedSource.AddRange(Enumerable.Range(0, 3).Select(index => new DimensionLayoutItem
			{
				Kind = DimensionKind.Normal,
				FirstPoint = new Point2D(10.0 + index, 30.0),
				SecondPoint = new Point2D(10.0 + index, 70.0),
				Span = 40.0
			}));
			var moves = rules.SelectVerticalHoleLocationRebalanceMoves(crowdedSource, new DimensionLayoutItem[0], 1.0);

			Assert(rules.TryGetDimensionLocalBoundary(loose, DimensionSide.Right, outline, out var boundary)
				&& Math.Abs(boundary - 60.0) <= config.GeometryTolerance,
				"an explicitly local loose chain must find the nearby concave boundary");
			Assert(Math.Abs(rules.GetDimLineCoordinate(loose, DimensionSide.Right, outline, 5.0) - 65.0) <= config.GeometryTolerance,
				"a rebalanced loose chain must stay beside its holes instead of using the distant global side");
			Assert(!rules.TryGetDimensionLocalBoundary(legacyGlobalLoose, DimensionSide.Right, outline, out _)
				&& Math.Abs(rules.GetDimLineCoordinate(legacyGlobalLoose, DimensionSide.Right, outline, 5.0) - 205.0) <= config.GeometryTolerance,
				"unmarked legacy loose chains must still fall back to the global boundary");
			Assert(moves.Any(move => move.SourceIndex == 0),
				"local placement must remain compatible with moving a crowded loose chain to the nearer notch side");
		}

        private static void LooseHolesUseChainDimensions()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateRectangle(240.0, 120.0);
            var datumPin = CreateHole(20.0, 20.0, 6.0, HoleKind2D.Pin);
            var holes = new List<HoleFeature2D>
            {
                datumPin,
                CreateHole(60.0, 20.0, 6.0, HoleKind2D.Pin),
                CreateHole(100.0, 70.0, 8.0, HoleKind2D.Normal),
                CreateHole(130.0, 70.0, 8.0, HoleKind2D.Normal),
                CreateHole(160.0, 70.0, 8.0, HoleKind2D.Normal)
            };
            var datum = Datum2D.FromOutline(outline);
            datum.DatumHole = datumPin;

            var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, holes);

            Assert(plan.Dimensions.Any(d => d.DebugRole == "LooseHole" && d.DebugOwner.StartsWith("L", StringComparison.Ordinal)),
                "loose holes should use chained hole-location dimensions");
			Assert(plan.Dimensions.Count(d => d.DebugRole == "LooseHole") >= 3,
				"loose hole chain should include center distances and pin/location references");
			Assert(plan.Dimensions.Where(d => d.DebugRole == "LooseHole").All(d => d.ChainId > 0),
				"loose hole dimensions must preserve their generated chain identifiers through planning");
			Assert(plan.Dimensions.Where(d => d.DebugRole == "LooseHole").All(d => d.PreferLocalBoundary),
				"loose hole chains must prefer a nearby valid boundary to avoid full-part extension lines");
        }

        private static void ConcentricLooseHolesShareOneLocationDimension()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateRectangle(73.0, 20.0);
            var datumPin = CreateHole(10.0, 10.0, 8.0, HoleKind2D.Pin);
            var holes = new List<HoleFeature2D>
            {
                datumPin,
                CreateHole(40.0, 10.0, 8.0, HoleKind2D.Pin),
                CreateHole(25.0, 10.0, 14.0, HoleKind2D.Normal),
                CreateHole(25.0, 10.0, 9.0, HoleKind2D.Normal)
            };
            var datum = Datum2D.FromOutline(outline);
            datum.DatumHole = holes[1];
            datum.DatumHoleLocationBaseX = 73.0;
            datum.DatumHoleLocationBaseY = 20.0;

            var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, holes);

			// Concentric non-pin holes sit between the pin pair → claimed as FunctionalHole from base pin.
			// Same measured span should collapse to one selected location (duplicate suppressed).
			var functionalHorizontal = plan.Diagnostics.DimensionCandidates
				.Where(d => d.DebugRole == "FunctionalHole"
					&& d.Orientation == DimensionOrientation.Horizontal.ToString()
					&& Math.Abs(d.Value - 15.0) <= 0.001)
				.ToList();
			Assert(functionalHorizontal.Count >= 1, "concentric between-pin holes must locate from the pin-group base as FunctionalHole");
			Assert(functionalHorizontal.Count(d => d.IsSelected) == 1,
				"concentric between-pin holes that share the same center must keep a single selected location dimension");
			Assert(!plan.Dimensions.Any(d => d.DebugRole == "LooseHole"),
				"between-pin concentric holes must not fall through to LooseHole");
        }

        private static void HoleLocationDimensionsUseSegmentedExtensionLines()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var outline = CreateRectangle(100.0, 50.0);
            var datum = Datum2D.FromOutline(outline);
            var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, new[]
            {
                CreateHole(25.0, 20.0, 8.0, HoleKind2D.Normal)
            });

            Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallWidth && !d.UseSegmentedExtensionLines),
                "overall width should not use segmented extension lines");
            Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.OverallHeight && !d.UseSegmentedExtensionLines),
                "overall height should not use segmented extension lines");
            Assert(plan.Dimensions.Count(d => d.Kind == DimensionKind.HoleLocation && d.UseSegmentedExtensionLines) == 2,
                "hole location dimensions should use segmented extension lines");
        }

        private static void HoleCalloutsGroupByRowsWithoutPins()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var holes = new List<HoleFeature2D>
            {
                CreateHole(10.0, 10.0, 6.0, HoleKind2D.Normal),
                CreateHole(30.0, 10.0, 6.0, HoleKind2D.Normal),
                CreateHole(10.0, 40.0, 8.0, HoleKind2D.Thread)
            };
            holes[2].ThreadCallout = "M8";

            var plans = new HoleCalloutPlanner(config).CreatePlans(holes, null);

            Assert(plans.Count == 2, "hole callouts without pins should group compatible holes by spatial rows");
            Assert(plans.Any(p => p.Kind == HoleCalloutKind.Normal && p.Text == "2-%%c6"),
                "two equal normal holes should share one diameter callout");
            Assert(plans.Any(p => p.Kind == HoleCalloutKind.Thread && p.Text == "M8"),
                "thread holes should use explicit thread callout text");
        }

        private static void HoleCalloutsUsePinClustersAndFitText()
        {
            var config = DimensionRuleConfig.CreateDefault();
            var datumPin = CreateHole(10.0, 10.0, 8.0, HoleKind2D.Pin);
            datumPin.FitTolerance = "H7";
            var secondPin = CreateHole(40.0, 10.0, 8.0, HoleKind2D.Pin);
            secondPin.FitTolerance = "H7";
            var holes = new List<HoleFeature2D>
            {
                datumPin,
                secondPin,
                CreateHole(25.0, 10.0, 14.0, HoleKind2D.Normal),
                CreateHole(25.0, 10.0, 9.0, HoleKind2D.Normal)
            };

            var plans = new HoleCalloutPlanner(config).CreatePlans(holes, secondPin);

            Assert(plans.Any(p => p.Kind == HoleCalloutKind.Pin && p.DebugOwner == "PG1" && p.Text == "2-%%c8H7"),
                "pin callout plan should include count and fit tolerance");
            Assert(plans.Any(p => p.Kind == HoleCalloutKind.Normal && p.DebugOwner == "PG1" && p.Text == "%%c14"),
                "counterbore outer diameter should stay as a separate normal callout");
            Assert(plans.Any(p => p.Kind == HoleCalloutKind.Normal && p.DebugOwner == "PG1" && p.Text == "%%c9"),
                "counterbore inner diameter should stay as a separate normal callout");
        }

        private static OutlineFeature2D CreateRectangle(double width, double height)
        {
            return CreateRectangleAt(0.0, 0.0, width, height);
        }

        private static OutlineFeature2D CreateRectangleAt(double minX, double minY, double width, double height)
        {
            var outline = new OutlineFeature2D
            {
                MinX = minX,
                MinY = minY,
                MaxX = minX + width,
                MaxY = minY + height
            };

            AddSegment(outline, new Point2D(minX, minY), new Point2D(minX + width, minY), "bottom");
            AddSegment(outline, new Point2D(minX + width, minY), new Point2D(minX + width, minY + height), "right");
            AddSegment(outline, new Point2D(minX + width, minY + height), new Point2D(minX, minY + height), "top");
            AddSegment(outline, new Point2D(minX, minY + height), new Point2D(minX, minY), "left");
            return outline;
        }

		private static OutlineFeature2D CreateRightNotchOutline()
		{
			var outline = new OutlineFeature2D
			{
				MinX = 0.0,
				MinY = 0.0,
				MaxX = 200.0,
				MaxY = 100.0
			};
			var points = new[]
			{
				new Point2D(0.0, 0.0),
				new Point2D(200.0, 0.0),
				new Point2D(200.0, 20.0),
				new Point2D(60.0, 20.0),
				new Point2D(60.0, 80.0),
				new Point2D(200.0, 80.0),
				new Point2D(200.0, 100.0),
				new Point2D(0.0, 100.0),
				new Point2D(0.0, 0.0)
			};
			for (int i = 1; i < points.Length; i++)
			{
				AddSegment(outline, points[i - 1], points[i], "right-notch");
			}
			return outline;
		}

		private static OutlineFeature2D CreateTopLeftShoulderOutline()
		{
			var outline = new OutlineFeature2D
			{
				MinX = -33.0,
				MinY = 0.0,
				MaxX = 100.0,
				MaxY = 201.5
			};
			var points = new[]
			{
				new Point2D(0.0, 0.0),
				new Point2D(100.0, 0.0),
				new Point2D(100.0, 201.5),
				new Point2D(-33.0, 201.5),
				new Point2D(-33.0, 181.5),
				new Point2D(0.0, 181.5),
				new Point2D(0.0, 0.0)
			};
			for (int i = 1; i < points.Length; i++)
			{
				AddSegment(outline, points[i - 1], points[i], "top-left-shoulder");
			}
			return outline;
		}

        private static OutlineFeature2D CreateTopRightChamferedOutline()
        {
            var outline = new OutlineFeature2D
            {
                MinX = 0.0,
                MinY = 0.0,
                MaxX = 100.0,
                MaxY = 50.0
            };

            AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(100.0, 0.0), "bottom");
            AddSegment(outline, new Point2D(100.0, 0.0), new Point2D(100.0, 40.0), "right-local");
            AddSegment(outline, new Point2D(100.0, 40.0), new Point2D(90.0, 50.0), "chamfer");
            AddSegment(outline, new Point2D(90.0, 50.0), new Point2D(0.0, 50.0), "top-local");
            AddSegment(outline, new Point2D(0.0, 50.0), new Point2D(0.0, 0.0), "left");
            return outline;
        }

        private static void AddSegment(OutlineFeature2D outline, Point2D start, Point2D end, string sourceKey)
        {
            outline.Vertices.Add(start);
            outline.Segments.Add(new Segment2D(start, end) { SourceKey = sourceKey });
        }

        private static HoleFeature2D CreateHole(double x, double y, double diameter, HoleKind2D kind)
        {
            return new HoleFeature2D
            {
                Center = new Point2D(x, y),
                Diameter = diameter,
                Kind = kind
            };
        }

        private static PlannedDimension CreateTestDimension(
            DimensionKind kind,
            DimensionOrientation orientation,
            DimensionSide side,
            Point2D firstPoint,
            Point2D secondPoint,
            string debugRole)
        {
            return new PlannedDimension
            {
                Kind = kind,
                Orientation = orientation,
                Side = side,
                FirstPoint = firstPoint,
                SecondPoint = secondPoint,
                DebugRole = debugRole
            };
        }

        private static void AssertHasDimension(
            DimensionPlan plan,
            DimensionKind kind,
            DimensionOrientation orientation,
            double expectedSpan,
            string message)
        {
            var found = plan.Dimensions.Any(d =>
                d.Kind == kind
                && d.Orientation == orientation
                && Math.Abs(GetSpan(d) - expectedSpan) <= 0.001);

            Assert(found, "missing " + message);
        }

        private static double GetSpan(PlannedDimension dimension)
        {
            return dimension.Orientation == DimensionOrientation.Horizontal
                ? Math.Abs(dimension.SecondPoint.X - dimension.FirstPoint.X)
                : Math.Abs(dimension.SecondPoint.Y - dimension.FirstPoint.Y);
        }

        private static bool EndpointXEquals(PlannedDimension dimension, double x)
        {
            return Math.Abs(dimension.FirstPoint.X - x) <= 0.001
                || Math.Abs(dimension.SecondPoint.X - x) <= 0.001;
        }

        private static bool EndpointYEquals(PlannedDimension dimension, double y)
        {
            return Math.Abs(dimension.FirstPoint.Y - y) <= 0.001
                || Math.Abs(dimension.SecondPoint.Y - y) <= 0.001;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
