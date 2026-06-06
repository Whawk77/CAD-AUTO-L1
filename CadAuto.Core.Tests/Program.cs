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
        private static int Main()
        {
            try
            {
                RectangularOutlineKeepsOverallDimensions();
                ClosedPathRecognitionBuildsOutline();
                ChamferedOutlineKeepsOverallDimensions();
                OverallDimensionsUseBoundaryGripPoints();
                ChamferSuppressesAdjacentLocalLinearDimensions();
                NonFortyFiveSlopeIsNotChamfer();
                VerticalStructurePointsCreateStepWidths();
                HorizontalStructurePointsCreateStepHeights();
                DiagonalFragmentsDoNotCreateStructureDimensions();
                BottomInclinedStructurePointsRequireInnerGrooveChamfer();
                SideInclinedStructurePointsRequireInnerGrooveChamfer();
                RightStructureHeightDuplicatingOverallHeightIsSuppressed();
                TwoArcSlotIsRecognized();
                SingleArcSlotIsRecognized();
                SlotDimensionsUseCenterAndDatumChainsWithoutPins();
                HolesAreGroupedByHorizontalRows();
                NormalHolesLocateFromOutlineDatum();
                PinGroupsPlanBaseAndPairDistances();
                FunctionalHolesAttachToPinGroup();
                LooseHolesUseChainDimensions();
                ConcentricLooseHolesShareOneLocationDimension();
                HoleCalloutsGroupByRowsWithoutPins();
                HoleCalloutsUsePinClustersAndFitText();
                Console.WriteLine("CadAuto.Core.Tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
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
                MaxY = 29.038
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
            datum.DatumHoleLocationUseToleranceX = true;
            datum.DatumHoleLocationUseToleranceY = true;

            var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, holes);

            Assert(plan.PinGroups.Count == 2, "expected two pin groups");
            Assert(plan.PinGroups[0].BasePin == datumPin, "first pin group should keep user datum pin as base");
            Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.DatumHoleLocationX), "expected datum pin X location");
            Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.DatumHoleLocationY), "expected datum pin Y location");
            Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.PinDistance), "expected same-group pin distance");
            Assert(plan.Dimensions.Any(d => d.Kind == DimensionKind.PinGroupDistance), "expected pin group transfer distance");
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
            var outline = new OutlineFeature2D
            {
                MinX = 0.0,
                MinY = 0.0,
                MaxX = width,
                MaxY = height
            };

            AddSegment(outline, new Point2D(0.0, 0.0), new Point2D(width, 0.0), "bottom");
            AddSegment(outline, new Point2D(width, 0.0), new Point2D(width, height), "right");
            AddSegment(outline, new Point2D(width, height), new Point2D(0.0, height), "top");
            AddSegment(outline, new Point2D(0.0, height), new Point2D(0.0, 0.0), "left");
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
                && Math.Abs(d.FirstPoint.DistanceTo(d.SecondPoint) - expectedSpan) <= 0.001);

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
