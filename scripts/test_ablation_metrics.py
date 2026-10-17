import copy
import unittest

from ablation_metrics import aggregate, behavior, compare, digest, evaluate, layout_metrics, matches


def dimension(identifier=1, **changes):
    d = dict(Id=identifier, Kind="Normal", DebugRole="Feature", Orientation="Horizontal", Value=25,
             MeasurementMinimum=0, MeasurementMaximum=25, FirstPointX=0, FirstPointY=0, SecondPointX=25, SecondPointY=0,
             IsSuppressed=False, IsAttachmentValid=True, FamilySpacingValid=None, SourceGeometryIds=["edge-A"])
    d.update(changes)
    return d


def snapshot(finals=None, candidates=None, error=None):
    return dict(caseId="fixture", profile="legacy-default", inputSha256="same-input", dimensions=[], layout=[],
                diagnostics=dict(FinalDimensions=finals or [], DimensionCandidates=candidates or [], Warnings=[]),
                elapsedMilliseconds=1.0, error=error)


def case(required=None, forbidden=None):
    return dict(id="fixture", overall=[100, 50], required=required or [], forbidden=forbidden or [])


def overalls():
    return [dimension(10, Kind="OverallWidth", DebugRole="OverallWidth", Orientation="Horizontal", Value=100, SourceGeometryIds=["outer-x"]),
            dimension(11, Kind="OverallHeight", DebugRole="OverallHeight", Orientation="Vertical", Value=50, SourceGeometryIds=["outer-y"])]


class AblationMetricsTests(unittest.TestCase):
    def test_absent_is_not_zero_recall(self):
        result = evaluate(snapshot(), case(), .001)
        self.assertEqual(result["metrics"]["contractMissingRequired"], 2)
        self.assertEqual(result["metrics"]["contractRecall"], 0)

    def test_exception_metrics_are_unknown(self):
        result = evaluate(snapshot(error="planner failed"), case(), .001)
        self.assertFalse(result["contractPassed"])
        self.assertTrue(all(v is None for v in result["metrics"].values()))
        self.assertIsNone(aggregate([result])["metrics"]["contractRecall"])

    def test_unknown_extra_dimension_is_not_wrong(self):
        result = evaluate(snapshot(overalls() + [dimension()]), case(), .001)
        self.assertEqual(result["metrics"]["contractWrongDimensions"], 0)
        self.assertTrue(result["contractPassed"])

    def test_same_value_different_feature_and_orientation_is_not_duplicate(self):
        required = [dict(id="arm-x", identity=dict(Orientation="Horizontal", sourceContains="edge-A")),
                    dict(id="arm-y", identity=dict(Orientation="Vertical", sourceContains="edge-B"))]
        result = evaluate(snapshot(overalls() + [dimension(), dimension(2, Orientation="Vertical", SourceGeometryIds=["edge-B"])]), case(required), .001)
        self.assertEqual(result["metrics"]["contractDuplicateDimensions"], 0)
        self.assertEqual(result["metrics"]["contractCorrectRetained"], 4)

    def test_duplicate_is_excess_count_not_pair_count(self):
        required = [dict(id="edge", identity=dict(DebugRole="Feature"), min=1, max=1)]
        result = evaluate(snapshot(overalls() + [dimension(i) for i in range(1, 4)]), case(required), .001)
        self.assertEqual(result["metrics"]["contractDuplicateDimensions"], 2)
        self.assertFalse(result["contractPassed"])

    def test_wrong_anchor_is_not_misreported_as_missing_dimension(self):
        required = [dict(id="edge", identity=dict(DebugRole="Feature"), check=dict(FirstPointY=5))]
        result = evaluate(snapshot(overalls() + [dimension()]), case(required), .001)
        self.assertEqual(result["metrics"]["contractMissingRequired"], 0)
        self.assertEqual(result["metrics"]["contractWrongDimensions"], 1)
        self.assertAlmostEqual(result["metrics"]["contractRecall"], 2/3)

    def test_incorrect_suppression_requires_correct_candidate_and_absence(self):
        required = [dict(id="edge", identity=dict(DebugRole="Feature"), check=dict(FirstPointY=5))]
        for candidates, expected in [([dimension(IsSuppressed=True, FirstPointY=5)], 1),
                                     ([dimension(IsSuppressed=True, FirstPointY=6)], 0), ([], 0)]:
            result = evaluate(snapshot(overalls(), candidates), case(required), .001)
            self.assertEqual(result["metrics"]["contractIncorrectlySuppressedRequired"], expected)
        result = evaluate(snapshot(overalls() + [dimension(3, FirstPointY=5)], [dimension(IsSuppressed=True, FirstPointY=5)]), case(required), .001)
        self.assertEqual(result["metrics"]["contractIncorrectlySuppressedRequired"], 0)

    def test_unknown_or_invalid_attachment_is_not_a_proven_correct_removal(self):
        required = [dict(id="edge", identity=dict(DebugRole="Feature"))]
        for validity, pending in ((None, 1), (False, 0)):
            result = evaluate(snapshot(overalls(), [dimension(IsSuppressed=True, IsAttachmentValid=validity)]), case(required), .001)
            self.assertEqual(result["metrics"]["contractIncorrectlySuppressedRequired"], 0)
            self.assertEqual(result["metrics"]["contractSuppressedRequiredAttachmentUnknown"], pending)

    def test_forbidden_match_uses_identity_not_value_only(self):
        forbidden = [dict(id="bad", identity=dict(Value=25, sourceContains="edge-B"))]
        result = evaluate(snapshot(overalls() + [dimension()]), case(forbidden=forbidden), .001)
        self.assertEqual(result["metrics"]["contractWrongDimensions"], 0)
        result = evaluate(snapshot(overalls() + [dimension(SourceGeometryIds=["edge-B"])]), case(forbidden=forbidden), .001)
        self.assertEqual(result["metrics"]["contractWrongDimensions"], 1)

    def test_recovered_exception_never_improves_paired_quality(self):
        bad = evaluate(snapshot(error="failed"), case(), .001)
        good = evaluate(snapshot(overalls()), case(), .001)
        pair = compare({"a": bad, "b": good}, {"a": good, "b": good})
        self.assertEqual(pair["pairedEvaluableCount"], 1)
        self.assertEqual(pair["delta"]["finalDimensionCount"], 0)
        self.assertEqual(pair["recoveredExceptions_NotQualityProof"], ["a"])

    def test_layout_counts_strict_overlap_not_touching_endpoints(self):
        a = dict(side="Bottom", coordinate=-10, arrowMinimum=0, arrowMaximum=20, textMinimum=5, textMaximum=15, textHeight=2.5, candidateId=1)
        b = dict(a, arrowMinimum=20, arrowMaximum=40, textMinimum=25, textMaximum=35, candidateId=2)
        self.assertEqual(layout_metrics([a, b], .001)[0]["coreDimensionLineOverlapPairs"], 0)
        b["arrowMinimum"] = 10
        self.assertEqual(layout_metrics([a, b], .001)[0]["coreDimensionLineOverlapPairs"], 1)
        b["side"] = "Top"
        self.assertEqual(layout_metrics([a, b], .001)[0]["coreDimensionLineOverlapPairs"], 0)

    def test_behavior_fingerprint_excludes_only_timing_rule_and_stack(self):
        a = snapshot(error="same failure\nold stack")
        b = copy.deepcopy(a)
        b.update(elapsedMilliseconds=99, disabledRule="MeasuredDeduplication", error="same failure\nnew stack")
        self.assertEqual(digest(behavior(a)), digest(behavior(b)))
        b["inputSha256"] = "different"
        self.assertNotEqual(digest(behavior(a)), digest(behavior(b)))

    def test_profile_scoped_expectations_do_not_spill_into_legacy(self):
        scoped = case(required=[dict(id="feature", identity=dict(DebugRole="Feature"))],
                      forbidden=[dict(id="forbidden-feature", identity=dict(DebugRole="Other"))])
        scoped.update(requirementsProfile="asd-feature-first", forbiddenProfile="asd-feature-first")
        result = evaluate(snapshot(overalls() + [dimension(DebugRole="Other")]), scoped, .001)
        self.assertTrue(result["contractPassed"])
        self.assertEqual(result["metrics"]["contractRequired"], 2)

    def test_wrong_and_forbidden_candidate_is_counted_once(self):
        c = case(required=[dict(id="feature", identity=dict(DebugRole="Feature"), check=dict(FirstPointY=5))],
                 forbidden=[dict(id="forbidden-feature", identity=dict(DebugRole="Feature"))])
        result = evaluate(snapshot(overalls() + [dimension()]), c, .001)
        self.assertEqual(result["metrics"]["contractWrongDimensions"], 1)

    def test_empty_aggregate_has_no_fabricated_zero_quality(self):
        self.assertIsNone(aggregate([])["metrics"]["contractMissingRequired"])
        self.assertIsNone(aggregate([])["metrics"]["contractRecall"])

    def test_exact_dataset_and_switch_validation(self):
        import importlib.util
        from pathlib import Path
        spec = importlib.util.spec_from_file_location("cad_ablation_runner", Path(__file__).with_name("run-ablation.py"))
        runner = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(runner)
        dataset = dict(profiles=["legacy-default", "asd-feature-first"], cases=[dict(id="one")])
        data = dict(schemaVersion=1, scope="CoreOnly_NotCadRendered", results=[
            dict(caseId="one", profile=p, disabledRule="None") for p in dataset["profiles"]])
        runner.validate_snapshot(data, dataset, "None")
        for corrupt in (dict(data, results=data["results"][:1]),
                        dict(data, results=data["results"] + data["results"][:1])):
            with self.assertRaises(ValueError):
                runner.validate_snapshot(corrupt, dataset, "None")
        with self.assertRaises(ValueError):
            runner.validate_snapshot(data, dataset, "MeasuredDeduplication")

    def test_epsilon_does_not_allow_feature_identity_substitution(self):
        self.assertTrue(matches(dimension(Value=25.0005), {"Value": 25}, .001))
        self.assertFalse(matches(dimension(Value=25.002), {"Value": 25}, .001))
        self.assertFalse(matches(dimension(), {"sourceContains": "edge-B"}, .001))


if __name__ == "__main__":
    unittest.main()
