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

        private static int Main()
        {
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
				RunTest(nameof(OverallRemainsOutermostAfterLayoutAlignment), OverallRemainsOutermostAfterLayoutAlignment);
				RunTest(nameof(OverallCompactsToOnePhysicalSpacing), OverallCompactsToOnePhysicalSpacing);
				RunTest(nameof(SemanticReadingLevelsControlHorizontalStacking), SemanticReadingLevelsControlHorizontalStacking);
				RunTest(nameof(SemanticReadingLevelsControlVerticalStacking), SemanticReadingLevelsControlVerticalStacking);
				RunTest(nameof(IsolatedShortPinGroupTransferUsesInnerSpanOrder), IsolatedShortPinGroupTransferUsesInnerSpanOrder);
				RunTest(nameof(DimensionDiagnosticsRecordFinalPlacement), DimensionDiagnosticsRecordFinalPlacement);
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
                RunTest(nameof(HorizontalStructurePointsCreateStepHeights), HorizontalStructurePointsCreateStepHeights);
                RunTest(nameof(DiagonalFragmentsDoNotCreateStructureDimensions), DiagonalFragmentsDoNotCreateStructureDimensions);
                RunTest(nameof(BottomInclinedStructurePointsRequireInnerGrooveChamfer), BottomInclinedStructurePointsRequireInnerGrooveChamfer);
                RunTest(nameof(SideInclinedStructurePointsRequireInnerGrooveChamfer), SideInclinedStructurePointsRequireInnerGrooveChamfer);
                RunTest(nameof(RightStructureHeightDuplicatingOverallHeightIsSuppressed), RightStructureHeightDuplicatingOverallHeightIsSuppressed);
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
				RunTest(nameof(FunctionalHoleAlignmentUsesSeparateSameOrientationLane), FunctionalHoleAlignmentUsesSeparateSameOrientationLane);
				RunTest(nameof(FunctionalHoleAlignmentPreservesV198Stacking), FunctionalHoleAlignmentPreservesV198Stacking);
                RunTest(nameof(PreferredSideLockedHoleLocationUsesLocalBoundary), PreferredSideLockedHoleLocationUsesLocalBoundary);
                RunTest(nameof(ExplicitLocalLooseChainUsesNearbyConcaveBoundary), ExplicitLocalLooseChainUsesNearbyConcaveBoundary);
                RunTest(nameof(LooseHolesUseChainDimensions), LooseHolesUseChainDimensions);
                RunTest(nameof(ConcentricLooseHolesShareOneLocationDimension), ConcentricLooseHolesShareOneLocationDimension);
                RunTest(nameof(HoleLocationDimensionsUseSegmentedExtensionLines), HoleLocationDimensionsUseSegmentedExtensionLines);
                RunTest(nameof(HoleCalloutsGroupByRowsWithoutPins), HoleCalloutsGroupByRowsWithoutPins);
                RunTest(nameof(HoleCalloutsUsePinClustersAndFitText), HoleCalloutsUsePinClustersAndFitText);
                Console.WriteLine("CadAuto.Core.Tests passed: " + _passedTests + ".");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void RunTest(string name, Action test)
        {
            test();
            _passedTests++;
            Console.WriteLine("PASS " + name);
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

		private static void SemanticReadingLevelsControlHorizontalStacking()
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

			Assert(placements.Single(item => item.Index == 3).Level < placements.Single(item => item.Index == 2).Level,
				"local horizontal spacing must be placed inside intra-group spacing even when generated last");
			Assert(placements.Single(item => item.Index == 2).Level < placements.Single(item => item.Index == 1).Level,
				"intra-group horizontal spacing must be placed inside datum transfer spacing");
			Assert(placements.Single(item => item.Index == 1).Level < placements.Single(item => item.Index == 0).Level,
				"datum transfer spacing must be placed inside the horizontal overall dimension");
		}

		private static void SemanticReadingLevelsControlVerticalStacking()
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

			Assert(placements.Single(item => item.Index == 3).Level < placements.Single(item => item.Index == 2).Level,
				"local vertical spacing must be placed inside intra-group spacing even when generated last");
			Assert(placements.Single(item => item.Index == 2).Level < placements.Single(item => item.Index == 1).Level,
				"intra-group vertical spacing must be placed inside datum transfer spacing");
			Assert(placements.Single(item => item.Index == 1).Level < placements.Single(item => item.Index == 0).Level,
				"datum transfer spacing must be placed inside the vertical overall dimension");
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
			plan.Diagnostics.RecordFinalPlacement(dimension.DiagnosticId, 2, 15.0, 125.0, true, true, "PG1:DatumChain:H#1", 3, "AlignedLaneCoordinateOverride");

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
                Radius = 5.0
            });
            geometry.Arcs.Add(new Arc2D
            {
                SourceKey = "a2",
                Center = new Point2D(60.0, 10.0),
                Start = new Point2D(60.0, 15.0),
                End = new Point2D(60.0, 5.0),
                Radius = 5.0
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
                Radius = 5.0
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
			var moves = rules.SelectVerticalHoleLocationRebalanceMoves(crowdedSource, new DimensionLayoutItem[0], 10.0);

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

            Assert(plan.Dimensions.Count(d => d.DebugRole == "LooseHole") == 1,
                "concentric loose holes should share a single hole-location dimension");
            Assert(plan.Dimensions.Any(d => d.DebugRole == "LooseHole" && Math.Abs(Math.Abs(d.SecondPoint.X - d.FirstPoint.X) - 15.0) <= 0.001),
                "concentric loose hole location should be measured from the pin group base");
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
