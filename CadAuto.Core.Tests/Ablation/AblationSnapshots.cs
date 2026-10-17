using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Recognition;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        private sealed class AblationInput
        {
            public OutlineFeature2D Outline;
            public Datum2D Datum;
            public HoleFeature2D[] Holes = new HoleFeature2D[0];
            public SlotFeature2D[] Slots = new SlotFeature2D[0];
            public bool RecognizeCorners;
        }

        private static JavaScriptSerializer AblationJson()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 128 };
        }

        private static string AblationHash(string text)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        // These factories reuse the existing regression geometry, not generated planner outputs.
        // A fresh object graph is created for EVERY profile and trial (the validator can mutate envelopes).
        private static Dictionary<string, Func<AblationInput>> AblationInputs()
        {
            var cases = new Dictionary<string, Func<AblationInput>>();
            cases.Add("rectangle", () => new AblationInput { Outline = CreateRectangle(100, 50) });
            cases.Add("duplicate-segments", () => {
                var o = CreateRectangle(100, 50);
                AddSegment(o, new Point2D(40, 10), new Point2D(40, 30), "duplicate-a");
                AddSegment(o, new Point2D(40, 10), new Point2D(40, 30), "duplicate-b");
                return new AblationInput { Outline = o };
            });
            cases.Add("concave-datum", () => {
                var o = new OutlineFeature2D { MinX = 100, MinY = 100, MaxX = 200, MaxY = 200 };
                var p = new[] { new Point2D(100,100), new Point2D(200,100), new Point2D(200,200),
                    new Point2D(160,200), new Point2D(160,140), new Point2D(100,140) };
                var keys = new[] { "bottom", "right", "top", "inner-right", "shoulder", "left" };
                for (int i = 0; i < p.Length; i++) AddSegment(o, p[i], p[(i+1)%p.Length], keys[i]);
                return new AblationInput { Outline = o, Datum = Datum2D.FromOutline(o),
                    Holes = new[] { CreateHole(180,170,8,HoleKind2D.Normal) } };
            });
            cases.Add("outer-step", () => new AblationInput { Outline = CreateOuterContourStepOutline(0,0) });
            cases.Add("stepped-plate", () => new AblationInput { Outline = CreateSteppedPlate102Outline() });
            cases.Add("c-notch", () => new AblationInput { Outline = CreateCNotch180Outline() });
            foreach (int angle in new[] { 0, 90, 180, 270 })
            {
                int degrees = angle;
                cases.Add("oblique-" + angle, () => new AblationInput {
                    Outline = TransformLProfileFixture(CreateObliqueBoundaryInformationFixture(), degrees, false), RecognizeCorners = true });
            }
            foreach (bool horizontal in new[] { false, true })
            foreach (bool reverse in new[] { false, true })
            {
                bool h = horizontal, r = reverse;
                cases.Add("pin-family-" + (h ? "h" : "v") + (r ? "-reverse" : ""), () => {
                    var o = CreateRectangle(h ? 65 : 20, h ? 20 : 65);
                    var datum = Datum2D.FromOutline(o);
                    if (r) { if (h) datum.BaseY = o.MaxY; else datum.BaseX = o.MaxX; }
                    var holes = new[] { CreateHole(10,10,8,HoleKind2D.Pin),
                        CreateHole(h ? 40 : 10, h ? 10 : 40,8,HoleKind2D.Pin),
                        CreateHole(h ? 25 : 10,h ? 10 : 25,6,HoleKind2D.Thread),
                        CreateHole(h ? 55 : 10,h ? 10 : 55,6,HoleKind2D.Thread) };
                    for (int i = 0; i < holes.Length; i++) AddCenterlineCross(datum, holes[i].Center, "same-side:" + i);
                    return new AblationInput { Outline = o, Datum = datum, Holes = holes };
                });
            }
            cases.Add("separate-pin-groups", () => new AblationInput { Outline = CreateRectangle(100,100),
                Holes = new[] { CreateHole(10,10,6,HoleKind2D.Pin),CreateHole(10,20,6,HoleKind2D.Pin),
                    CreateHole(90,80,6,HoleKind2D.Pin),CreateHole(90,90,6,HoleKind2D.Pin) } });
            cases.Add("normal-two", () => {
                var o = CreateRectangle(100,100); var datum = Datum2D.FromOutline(o);
                AddCenterlineCross(datum,new Point2D(20,20),"N1"); AddCenterlineCross(datum,new Point2D(50,40),"N2");
                return new AblationInput { Outline = o, Datum = datum,
                    Holes = new[] { CreateHole(20,20,8,HoleKind2D.Normal),CreateHole(50,40,8,HoleKind2D.Normal) } };
            });
            cases.Add("normal-grid", () => {
                var o = CreateRectangle(100,100); var datum = Datum2D.FromOutline(o);
                var holes = new[] { CreateHole(20,20,8,HoleKind2D.Normal),CreateHole(20,40,8,HoleKind2D.Normal),
                    CreateHole(40,20,8,HoleKind2D.Normal),CreateHole(40,40,8,HoleKind2D.Normal) };
                foreach (var hole in holes) AddCenterlineCross(datum,hole.Center,"G" + hole.Center.X + ":" + hole.Center.Y);
                return new AblationInput { Outline = o, Datum = datum, Holes = holes };
            });
            cases.Add("selected-pin-grid", () => {
                var o = CreateRectangle(100,100); var datum = Datum2D.FromOutline(o);
                var pin = CreateHole(80,80,6,HoleKind2D.Pin); datum.DatumHole = pin;
                var holes = new[] { pin,CreateHole(50,80,6,HoleKind2D.Pin),CreateHole(80,30,6,HoleKind2D.Pin),CreateHole(50,30,6,HoleKind2D.Pin) };
                foreach (var hole in holes) AddCenterlineCross(datum,hole.Center,"P" + hole.Center.X + ":" + hole.Center.Y);
                return new AblationInput { Outline = o, Datum = datum, Holes = holes };
            });
            cases.Add("slots-without-pins", () => new AblationInput { Outline = CreateRectangle(100,50), Slots = new[] {
                new SlotFeature2D { GroupId="SLOT1",FirstCenter=new Point2D(20,20),SecondCenter=new Point2D(60,20),Radius=5,CenterDistance=40 },
                new SlotFeature2D { GroupId="SLOT2",FirstCenter=new Point2D(80,30),SecondCenter=new Point2D(80,40),Radius=5,CenterDistance=10,IsVertical=true } } });
            return cases;
        }

        private static List<object> CaptureAblation(string disabledRule)
        {
            var results = new List<object>();
            foreach (bool featureFirst in new[] { false, true })
            foreach (var scenario in AblationInputs())
            {
                var config = DimensionRuleConfig.CreateDefault();
                config.UseFeatureFirstStructurePipeline = featureFirst;
                if (disabledRule != "None")
                {
                    var property = typeof(DimensionRuleConfig).GetProperty("DisabledExperimentRule");
                    if (property == null) throw new InvalidOperationException("This build has no experimental switches.");
                    if (!Enum.GetNames(property.PropertyType).Contains(disabledRule))
                        throw new ArgumentException("Expected exactly one named rule; combinations/numbers are not accepted.");
                    property.SetValue(config, Enum.Parse(property.PropertyType, disabledRule, false), null);
                }
                var input = scenario.Value();
                var inputJson = AblationJson().Serialize(new { outline=input.Outline,datum=input.Datum,holes=input.Holes,
                    slots=input.Slots,recognizeCorners=input.RecognizeCorners });
                DimensionPlan plan = null;
                string error = null;
                var layout = new List<object>();
                var watch = Stopwatch.StartNew();
                try
                {
                    if (input.RecognizeCorners) new FeatureRecognizer2D(config).RecognizeOutlineCornerFeatures(input.Outline);
                    var planner = new DimensionPlanner(config);
                    plan = input.Datum == null && input.Holes.Length == 0 && input.Slots.Length == 0
                        ? planner.CreateOutlinePlan(input.Outline)
                        : planner.CreateDimensionPlan(input.Outline,input.Datum ?? Datum2D.FromOutline(input.Outline),input.Holes,input.Slots);
                    foreach (DimensionSide side in Enum.GetValues(typeof(DimensionSide)))
                    {
                        var dimensions = plan.Dimensions.Where(d => d.Side == side).ToList();
                        if (dimensions.Count == 0) continue;
                        bool horizontal = side == DimensionSide.Bottom || side == DimensionSide.Top;
                        var items = CreatePinDatumAlignmentLayout(dimensions);
                        for (int i=0;i<items.Count;i++) items[i].SourceFeatureId = dimensions[i].DebugOwner ?? dimensions[i].DebugRole;
                        var rules = new DimensionLayoutRules(config);
                        // Fixed Core test units, NOT a claim about a drawing's active CAD dimstyle.
                        var placements = rules.CreateStackingPlan(items,side,input.Outline,config.TextHeight,1.25,config.FirstDimOffset,6.5,horizontal);
                        foreach (var p in placements)
                        {
                            var d = dimensions[p.Index]; var item = items[p.Index];
                            double coordinate = p.DimLineCoordinateOverride ?? rules.GetDimLineCoordinate(item,side,input.Outline,p.Offset);
                            var text = rules.ComputeTextInterval(item,horizontal,config.TextHeight);
                            layout.Add(new { candidateId=d.DiagnosticId,side=side.ToString(),coordinate,
                                arrowMinimum=horizontal ? Math.Min(d.FirstPoint.X,d.SecondPoint.X) : Math.Min(d.FirstPoint.Y,d.SecondPoint.Y),
                                arrowMaximum=horizontal ? Math.Max(d.FirstPoint.X,d.SecondPoint.X) : Math.Max(d.FirstPoint.Y,d.SecondPoint.Y),
                                textMinimum=text.Item1,textMaximum=text.Item2,textHeight=config.TextHeight,placement=p });
                            plan.Diagnostics.RecordFinalPlacement(d.DiagnosticId,side.ToString(),p.Level,p.Offset,coordinate,
                                rules.TryGetLocalDimLineCoordinate(item,side,input.Outline,p.Offset,out _),p.DimLineCoordinateOverride.HasValue,p.AlignmentLaneKey,p.AlignmentLaneMemberCount,
                                "CoreExperimentPlacement",p.LayoutBlockId,p.LayoutBlockType,p.EffectiveSpan,p.EffectiveOrder,p.OrderingReason,
                                p.PromotedByConflictWith,p.PhysicalOutwardDistance,p.PhysicalOrderValidated);
                        }
                    }
                    plan.Diagnostics.ValidateDatumFamilyLayout(6.5,config.GeometryTolerance);
                }
                catch (Exception ex) { error = ex.ToString(); }
                watch.Stop();
                results.Add(new { caseId=scenario.Key,profile=featureFirst ? "asd-feature-first" : "legacy-default",disabledRule,
                    inputSha256=AblationHash(inputJson),input=AblationJson().DeserializeObject(inputJson),
                    elapsedMilliseconds=watch.Elapsed.TotalMilliseconds,error,
                    dimensions=plan == null ? null : plan.Dimensions,diagnostics=plan == null ? null : plan.Diagnostics,layout });
            }
            return results;
        }

        private static void SaveAblationJson(string path, object value)
        {
            if (File.Exists(path)) throw new IOException("Refusing to overwrite experiment output: " + path);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path,AblationJson().Serialize(value),new UTF8Encoding(false));
        }

        private static int WriteAblationSnapshot(string path)
        {
            SaveAblationJson(path,new { schemaVersion=1,scope="CoreOnly_NotCadRendered",results=CaptureAblation("None") });
            return 0;
        }

        private static int RunAblation(string path, string disabledRule)
        {
            SaveAblationJson(path,new { schemaVersion=1,scope="CoreOnly_NotCadRendered",results=CaptureAblation(disabledRule) });
            return 0; // Trial exceptions are data; the external runner separately enforces baseline compatibility.
        }

        private static int CheckAblation()
        {
            var one = CaptureAblation("None"); var two = CaptureAblation("None");
            Assert(one.Count == 38 && two.Count == one.Count,"Ablation case/profile registration changed; update its explicit contract.");
            for (int i=0;i<one.Count;i++)
            {
                var a = AblationJson().Deserialize<Dictionary<string,object>>(AblationJson().Serialize(one[i]));
                var b = AblationJson().Deserialize<Dictionary<string,object>>(AblationJson().Serialize(two[i]));
                a.Remove("elapsedMilliseconds"); b.Remove("elapsedMilliseconds");
                Assert(AblationJson().Serialize(a) == AblationJson().Serialize(b),"Fresh-fixture determinism failed at record " + i);
            }
            var first = DimensionRuleConfig.CreateDefault(); var second = DimensionRuleConfig.CreateDefault();
            Assert(first.DisabledExperimentRule == DimensionExperimentRule.None && second.DisabledExperimentRule == DimensionExperimentRule.None,
                "Experiments must be off by default.");
            first.DisabledExperimentRule = DimensionExperimentRule.MeasuredDeduplication;
            Assert(second.DisabledExperimentRule == DimensionExperimentRule.None,"Experimental configuration leaked between instances.");
            bool rejected = false;
            try { first.DisabledExperimentRule = (DimensionExperimentRule)999; }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Assert(rejected,"Unknown experiment values must be rejected.");
            rejected = false;
            try { CaptureAblation("PinPairGrouping,MeasuredDeduplication"); }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected,"Combined rule names must be rejected.");
            var outline = CreateRectangle(100,50);
            AddSegment(outline,new Point2D(40,10),new Point2D(40,30),"duplicate-a");
            AddSegment(outline,new Point2D(40,10),new Point2D(40,30),"duplicate-b");
            var ablated = new DimensionPlanner(first).CreateOutlinePlan(outline);
            var full = new DimensionPlanner(second).CreateOutlinePlan(outline);
            Assert(ablated.Dimensions.Count == full.Dimensions.Count + 1,"Same-side duplicate ablation did not isolate its real pass.");
            Assert(full.Diagnostics.DimensionCandidates.Any(d => d.SuppressedReason == "DuplicateMeasuredDimension")
                && !ablated.Diagnostics.DimensionCandidates.Any(d => d.SuppressedReason == "DuplicateMeasuredDimension"),
                "The target suppression decision must disappear only in the ablated plan.");
            var holes = new[] { CreateHole(10,10,6,HoleKind2D.Pin),CreateHole(10,40,6,HoleKind2D.Pin) };
            first.DisabledExperimentRule = DimensionExperimentRule.PinPairGrouping;
            Assert(new DimensionPlanner(first).BuildPinGroupPlan(holes,holes[0]).Count == 2
                && new DimensionPlanner(second).BuildPinGroupPlan(holes,holes[0]).Count == 1,"Pairing fallback must retain singleton pins.");
            Console.WriteLine("Ablation self-check passed: 38 deterministic snapshots; default, isolation, invalid value, duplicate pass, singleton pin fallback.");
            return 0;
        }
    }
}
