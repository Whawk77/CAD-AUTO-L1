using System;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        private static void CheckDatumReferenceRecovery()
        {
            int checks = 0;
            foreach (bool featureFirst in new[] { false, true })
            {
                var config = DimensionRuleConfig.CreateDefault();
                config.UseFeatureFirstStructurePipeline = featureFirst;
                var concave = AblationInputs()["concave-datum"]();
                CheckRealDatumReferences(config, concave.Outline, concave.Datum, concave.Holes.Single(),
                    new Point2D(100, 140), new Point2D(180, 100), "original-concave"); checks++;

                var rectangle = CreateRectangle(100, 50);
                CheckRealDatumReferences(config, rectangle, Datum2D.FromOutline(rectangle), CreateHole(25,20,8,HoleKind2D.Normal),
                    new Point2D(0,20), new Point2D(25,0), "rectangle-no-evidence"); checks++;
                foreach (double x in new[] { 125.0, 175.0 })
                foreach (double y in new[] { 120.0, 160.0 })
                foreach (bool reverseDatum in new[] { false, true })
                {
                    var outline = CreateRectangleAt(100,100,100,80);
                    var datum = Datum2D.FromOutline(outline);
                    if (reverseDatum) { datum.BaseX = 200; datum.BaseY = 180; }
                    CheckRealDatumReferences(config, outline, datum, CreateHole(x,y,8,HoleKind2D.Normal),
                        new Point2D(datum.BaseX,y), new Point2D(x,datum.BaseY),
                        "translated-four-sides-" + x + "-" + y + "-reverse=" + reverseDatum); checks++;
                }
                var unrelatedOutline = CreateRectangle(100,50);
                var unrelatedDatum = Datum2D.FromOutline(unrelatedOutline);
                unrelatedDatum.HoleCenterlineEndpoints.Add(new CenterlineEndpoint2D { Point=new Point2D(80,5), SourceGeometryId="unrelated-evidence" });
                CheckRealDatumReferences(config, unrelatedOutline, unrelatedDatum, CreateHole(25,20,8,HoleKind2D.Normal),
                    new Point2D(0,20), new Point2D(25,0), "unrelated-centerline-is-not-proof"); checks++;

                var split = new OutlineFeature2D { MinX=0,MinY=0,MaxX=100,MaxY=50 };
                var corners = new[] { new Point2D(0,0),new Point2D(40,0),new Point2D(100,0),new Point2D(100,50),new Point2D(0,50) };
                for (int i=0;i<corners.Length;i++) AddSegment(split,corners[i],corners[(i+1)%corners.Length],"split:"+i);
                CheckRealDatumReferences(config, split, Datum2D.FromOutline(split), CreateHole(25,20,8,HoleKind2D.Normal),
                    new Point2D(0,20), new Point2D(25,0), "split-real-bottom-edge"); checks++;

                var selectedOutline = CreateRectangle(100,50);
                var selectedDatum = Datum2D.FromOutline(selectedOutline);
                AddCenterlineCross(selectedDatum,new Point2D(25,20),"selected-proof");
                var plan = new DimensionPlanner(config).CreateDimensionPlan(selectedOutline,selectedDatum,
                    new[] { CreateHole(25,20,8,HoleKind2D.Normal) });
                var horizontal = plan.Dimensions.Single(d => d.DebugRole == "HoleDatumX");
                var vertical = plan.Dimensions.Single(d => d.DebugRole == "HoleDatumY");
                Assert(horizontal.FirstPoint.Equals(new Point2D(0,0)) && vertical.FirstPoint.Equals(new Point2D(0,0)),
                    "matched centerline evidence must retain the existing side-facing outline endpoints");
                Assert(horizontal.SecondPoint.Equals(new Point2D(25,15)) && vertical.SecondPoint.Equals(new Point2D(20,20))
                    && horizontal.AttachmentKind == "SelectedCenterlineEndpoint" && vertical.AttachmentKind == "SelectedCenterlineEndpoint",
                    "the recovery must not weaken existing selected-centerline endpoint attachment");
                Assert(selectedDatum.HoleCenterlineEndpoints.Count == 4,"planner must not generate or erase centerline evidence"); checks++;
            }
            Console.WriteLine("Datum-reference recovery self-check passed: " + checks + " cases across legacy/FeatureFirst; translated, reversed datum, split edges, absent/unrelated/matched evidence.");
        }

        private static void CheckRealDatumReferences(DimensionRuleConfig config, OutlineFeature2D outline, Datum2D datum,
            HoleFeature2D hole, Point2D expectedHorizontal, Point2D expectedVertical, string label)
        {
            double tolerance = config.GeometryTolerance;
            Point2D queryHorizontal, queryVertical;
            Assert(OutlineGeometryQuery.TryFindVerticalBoundaryPoint(outline,datum.BaseX,hole.Center.Y,tolerance,out queryHorizontal)
                && queryHorizontal.DistanceTo(expectedHorizontal) <= tolerance,"independent X datum geometry precondition: " + label);
            Assert(OutlineGeometryQuery.TryFindHorizontalBoundaryPoint(outline,datum.BaseY,hole.Center.X,tolerance,out queryVertical)
                && queryVertical.DistanceTo(expectedVertical) <= tolerance,"independent Y datum geometry precondition: " + label);
            int count = datum.HoleCenterlineEndpoints.Count;
            var plan = new DimensionPlanner(config).CreateDimensionPlan(outline,datum,new[] { hole });
            var horizontal = plan.Dimensions.Single(d => d.DebugRole == "HoleDatumX");
            var vertical = plan.Dimensions.Single(d => d.DebugRole == "HoleDatumY");
            Console.WriteLine("DATUM " + label + " featureFirst=" + config.UseFeatureFirstStructurePipeline
                + " expectedX=" + expectedHorizontal.X + "," + expectedHorizontal.Y
                + " actualX=" + horizontal.FirstPoint.X + "," + horizontal.FirstPoint.Y
                + " expectedY=" + expectedVertical.X + "," + expectedVertical.Y
                + " actualY=" + vertical.FirstPoint.X + "," + vertical.FirstPoint.Y);
            Assert(horizontal.FirstPoint.DistanceTo(expectedHorizontal) <= tolerance
                && vertical.FirstPoint.DistanceTo(expectedVertical) <= tolerance,"unmatched evidence must retain the nearest real datum intersection: " + label);
            Assert(horizontal.SecondPoint.Equals(hole.Center) && vertical.SecondPoint.Equals(hole.Center),"unmatched feature grips must remain real centers: " + label);
            Assert(horizontal.FirstPointMustLieOnOutline && vertical.FirstPointMustLieOnOutline
                && OutlineGeometryQuery.IsPointOnBoundary(horizontal.FirstPoint,outline,tolerance)
                && OutlineGeometryQuery.IsPointOnBoundary(vertical.FirstPoint,outline,tolerance),"datum grips must remain on real geometry: " + label);
            Assert(horizontal.RequiredOutlineReferenceCoordinate == datum.BaseX && vertical.RequiredOutlineReferenceCoordinate == datum.BaseY
                && vertical.PreservePreferredSide,"recovery must preserve datum coordinates and preferred side: " + label);
            Assert(Math.Abs(GetSpan(horizontal)-Math.Abs(hole.Center.X-datum.BaseX)) <= tolerance
                && Math.Abs(GetSpan(vertical)-Math.Abs(hole.Center.Y-datum.BaseY)) <= tolerance,"datum measurements must not change: " + label);
            Assert(plan.Dimensions.All(d => GetSpan(d)>tolerance) && datum.HoleCenterlineEndpoints.Count == count,
                "no zero dimensions or generated centerlines: " + label);
        }
    }
}
