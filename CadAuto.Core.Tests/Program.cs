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
				RunTest(nameof(PinAlignmentAnchorFallsBackWithStableOrdering), PinAlignmentAnchorFallsBackWithStableOrdering);
				RunTest(nameof(PinAlignmentGroupMovesTogetherOnConflict), PinAlignmentGroupMovesTogetherOnConflict);
				RunTest(nameof(PinAlignmentGroupAvoidsResolvedCoordinateConflicts), PinAlignmentGroupAvoidsResolvedCoordinateConflicts);
                RunTest(nameof(FunctionalHolesAttachToPinGroup), FunctionalHolesAttachToPinGroup);
                RunTest(nameof(PreferredSideLockedHoleLocationKeepsGlobalAlignment), PreferredSideLockedHoleLocationKeepsGlobalAlignment);
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
			var verticalFunctionalHoles = plan.Dimensions.Where(d => d.DebugRole == "FunctionalHole"
				&& d.Orientation == DimensionOrientation.Vertical).ToList();
			Assert(verticalFunctionalHoles.Count > 0
				&& verticalFunctionalHoles.All(d => d.PreservePreferredSide && !d.PreferFeatureLocalPlacement),
				"functional-hole dimensions must lock their side without enabling feature-local coordinates");
			Assert(verticalFunctionalHoles.All(d => d.Side == plan.PinGroups[0].VerticalSide),
				"functional-hole dimensions must inherit their pin group's vertical side");
        }

		private static void PreferredSideLockedHoleLocationKeepsGlobalAlignment()
		{
			var config = DimensionRuleConfig.CreateDefault();
			var rules = new DimensionLayoutRules(config);
			var functionalHole = new DimensionLayoutItem
			{
				Kind = DimensionKind.HoleLocation,
				FirstPoint = new Point2D(20.0, 20.0),
				SecondPoint = new Point2D(20.0, 40.0),
				Span = 20.0,
				PreservePreferredSide = true
			};
			var crowdedLeft = Enumerable.Range(0, 4).Select(index => new DimensionLayoutItem
			{
				Kind = DimensionKind.HoleLocation,
				FirstPoint = new Point2D(10.0 + index, 20.0),
				SecondPoint = new Point2D(10.0 + index, 40.0),
				Span = 20.0
			}).ToList();

			var selectedSide = rules.ChooseVerticalHoleLocationSide(functionalHole, DimensionSide.Left, crowdedLeft, new DimensionLayoutItem[0], 1.0);
			var moves = rules.SelectVerticalHoleLocationRebalanceMoves(new[] { functionalHole }, new DimensionLayoutItem[0], 1.0);
			var outline = CreateRectangle(200.0, 100.0);
			var datumY = new DimensionLayoutItem
			{
				Kind = DimensionKind.DatumHoleLocationY,
				FirstPoint = new Point2D(20.0, 0.0),
				SecondPoint = new Point2D(20.0, 20.0),
				Span = 20.0
			};
			double functionalCoordinate = rules.GetDimLineCoordinate(functionalHole, DimensionSide.Left, outline, 5.0);
			double datumCoordinate = rules.GetDimLineCoordinate(datumY, DimensionSide.Left, outline, 5.0);
			var placements = rules.CreateStackingPlan(new[] { datumY, functionalHole }, DimensionSide.Left, outline, 2.5, 1.0, 5.0, 5.0, isHorizontal: false);

			Assert(selectedSide == DimensionSide.Left,
				"side-locked functional holes must keep the pin group's preferred vertical side during initial placement");
			Assert(!rules.CanRebalanceVerticalHoleLocation(functionalHole, 1.0) && moves.Count == 0,
				"side-locked functional holes must not move during later vertical-side rebalancing");
			Assert(Math.Abs(functionalCoordinate - datumCoordinate) <= config.GeometryTolerance,
				"side locking must not opt functional holes into feature-local coordinates or break DatumY alignment");
			Assert(placements.Count == 2 && Math.Abs(placements[0].Offset - placements[1].Offset) <= config.GeometryTolerance,
				"touching DatumY and functional-hole dimensions must form one aligned left-side chain");
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
