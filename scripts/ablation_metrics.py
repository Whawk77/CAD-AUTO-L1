"""Partial regression-contract metrics. Never treats a baseline output as annotation truth."""
from __future__ import annotations

import copy
import hashlib
import itertools
import json


def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(",", ":"), allow_nan=False)


def digest(value):
    return hashlib.sha256(canonical(value).encode("utf-8")).hexdigest()


def behavior(record):
    """Compatibility fingerprint ONLY, not a correctness oracle; omit timing and stack locations."""
    return {key: (record[key].splitlines()[0] if key == "error" and record[key] else record[key])
            for key in ("caseId", "profile", "inputSha256", "dimensions", "diagnostics", "layout", "error")}


def matches(candidate, selector, epsilon):
    for key, expected in selector.items():
        if key == "sourceContains":
            if expected not in candidate.get("SourceGeometryIds", []):
                return False
        elif isinstance(expected, (int, float)) and not isinstance(expected, bool):
            actual = candidate.get(key)
            if not isinstance(actual, (int, float)) or isinstance(actual, bool) or abs(actual - expected) > epsilon:
                return False
        elif candidate.get(key) != expected:
            return False
    return True


def requirements(case, profile):
    result = [
        {"id": "overall-width", "identity": {"Kind": "OverallWidth", "Orientation": "Horizontal", "Value": case["overall"][0]}, "min": 1, "max": 1},
        {"id": "overall-height", "identity": {"Kind": "OverallHeight", "Orientation": "Vertical", "Value": case["overall"][1]}, "min": 1, "max": 1},
    ]
    if case.get("requirementsProfile", profile) == profile:
        result.extend(copy.deepcopy(case.get("required", [])))
    if "pinFamily" in case:
        family = case["pinFamily"]
        horizontal = family["orientation"] == "Horizontal"
        common = {"Orientation": family["orientation"]}
        check = {"PlacementSide": family["side"], "AttachmentKind": "SelectedCenterlineEndpoint",
                 "SecondPointY" if horizontal else "SecondPointX": family["secondCrossCoordinate"]}
        for name, identity in [
            ("family-datum", {"Kind": "DatumHoleLocationX" if horizontal else "DatumHoleLocationY", "Value": 10}),
            ("family-pin", {"Kind": "PinDistance", "Value": 30}),
            ("functional-15", {"DebugRole": "FunctionalHole", "Value": 15}),
            ("functional-45", {"DebugRole": "FunctionalHole", "Value": 45}),
        ]:
            result.append({"id": name, "identity": dict(common, **identity), "check": check, "min": 1, "max": 1})
    return result


def layout_metrics(layout, epsilon):
    line_pairs, text_pairs = [], []
    for a, b in itertools.combinations(layout, 2):
        if a["side"] != b["side"]:
            continue
        coordinate_gap = abs(a["coordinate"] - b["coordinate"])
        if coordinate_gap <= epsilon and min(a["arrowMaximum"], b["arrowMaximum"]) - max(a["arrowMinimum"], b["arrowMinimum"]) > epsilon:
            line_pairs.append([a["candidateId"], b["candidateId"]])
        # Core's centered text-length estimate; excludes CAD text sliding, dimstyle and renderer geometry.
        if coordinate_gap < (a["textHeight"] + b["textHeight"]) / 2 - epsilon and min(a["textMaximum"], b["textMaximum"]) - max(a["textMinimum"], b["textMinimum"]) > epsilon:
            text_pairs.append([a["candidateId"], b["candidateId"]])
    return {"coreDimensionLineOverlapPairs": len(line_pairs), "centeredTextConflictProxyPairs": len(text_pairs)}, {
        "dimensionLinePairs": line_pairs, "centeredTextPairs": text_pairs}


COUNT_METRICS = ["contractRequired", "contractMissingRequired", "contractWrongDimensions", "contractDuplicateDimensions",
                 "contractIncorrectlySuppressedRequired", "contractSuppressedRequiredAttachmentUnknown", "contractCorrectRetained", "finalDimensionCount",
                 "coreDimensionLineOverlapPairs", "centeredTextConflictProxyPairs", "physicalOrderViolations", "datumFamilySpacingViolations"]


def evaluate(record, case, epsilon):
    required = requirements(case, record["profile"])
    # Even a planner that produced no plan must not earn perfect recall or zero error.
    if record["error"]:
        return {"status": "Exception_NotEvaluated", "contractPassed": False,
                "metrics": dict.fromkeys(COUNT_METRICS + ["contractRecall"]),
                "evidence": [{"error": record["error"]}], "elapsedMilliseconds": record["elapsedMilliseconds"]}
    diagnostic = record["diagnostics"]
    finals = diagnostic["FinalDimensions"]
    candidates = diagnostic["DimensionCandidates"]
    missing = duplicate = removed = pending_removals = correct = total = 0
    wrong_ids, failures = set(), []
    for req in required:
        found = [d for d in finals if matches(d, req["identity"], epsilon)]
        valid = [d for d in found if matches(d, req.get("check", {}), epsilon)]
        minimum, maximum = req.get("min", 1), req.get("max", 1)
        absent = max(0, minimum - len(found))
        total += minimum
        missing += absent
        correct += min(minimum, len(valid))
        duplicate += max(0, len(found) - maximum)
        wrong_ids.update(d["Id"] for d in found if d not in valid)
        # At most one incorrect removal per missing required slot, not per speculative candidate.
        suppressed = [d for d in candidates if d["IsSuppressed"] and matches(d, req["identity"], epsilon)
                      and matches(d, req.get("check", {}), epsilon)]
        validated_suppressed = [d for d in suppressed if d.get("IsAttachmentValid") is True]
        unknown_suppressed = [d for d in suppressed if d.get("IsAttachmentValid") is None]
        removed_here = min(absent, len(validated_suppressed))
        removed += removed_here
        pending_removals += min(absent - removed_here, len(unknown_suppressed))
        if absent or len(found) > maximum or len(valid) < min(minimum, len(found)):
            failures.append({"requirement": req["id"], "identity": req["identity"], "check": req.get("check", {}),
                             "expectedMin": minimum, "expectedMax": maximum, "actual": found,
                             "matchingSuppressedCandidates": suppressed})
    forbidden = case.get("forbidden", []) if case.get("forbiddenProfile", record["profile"]) == record["profile"] else []
    for req in forbidden:
        found = [d for d in finals if matches(d, req["identity"], epsilon)]
        wrong_ids.update(d["Id"] for d in found)
        if found:
            failures.append({"forbidden": req["id"], "actual": found})
    layout, layout_evidence = layout_metrics(record["layout"], epsilon)
    metrics = dict(contractRequired=total, contractMissingRequired=missing, contractWrongDimensions=len(wrong_ids),
                   contractDuplicateDimensions=duplicate, contractIncorrectlySuppressedRequired=removed,
                   contractSuppressedRequiredAttachmentUnknown=pending_removals,
                   contractCorrectRetained=correct, contractRecall=correct / total if total else None,
                   finalDimensionCount=len(finals),
                   physicalOrderViolations=sum(not p["placement"]["PhysicalOrderValidated"] for p in record["layout"]),
                   datumFamilySpacingViolations=sum(d["FamilySpacingValid"] is False for d in finals), **layout)
    return {"status": "Evaluated_PartialContractOnly", "contractPassed": not failures, "metrics": metrics,
            "evidence": failures, "layoutEvidence": layout_evidence,
            "warnings": diagnostic["Warnings"], "elapsedMilliseconds": record["elapsedMilliseconds"]}


def aggregate(evaluations):
    usable = [e for e in evaluations if e["status"] == "Evaluated_PartialContractOnly"]
    metrics = {key: sum(e["metrics"][key] for e in usable) if usable else None for key in COUNT_METRICS}
    metrics["contractRecall"] = (metrics["contractCorrectRetained"] / metrics["contractRequired"]
                                 if metrics["contractRequired"] else None)
    return {"caseProfileCount": len(evaluations), "evaluableCount": len(usable),
            "exceptionCount": len(evaluations) - len(usable),
            "contractPassCount": sum(e["contractPassed"] for e in usable),
            "contractFailureCount": sum(not e["contractPassed"] for e in usable), "metrics": metrics}


def compare(base, trial):
    # Fixed paired denominator: a trial that simply avoids a baseline exception cannot be declared superior.
    ids = [key for key in base if base[key]["status"] == trial[key]["status"] == "Evaluated_PartialContractOnly"]
    b, t = aggregate([base[key] for key in ids]), aggregate([trial[key] for key in ids])
    delta = {key: t["metrics"][key] - b["metrics"][key] if b["metrics"][key] is not None and t["metrics"][key] is not None else None
             for key in COUNT_METRICS + ["contractRecall"]}
    return {"pairedEvaluableCount": len(ids), "baselineOnPairedInputs": b, "trialOnPairedInputs": t, "delta": delta,
            "contractRegressions": [key for key in ids if base[key]["contractPassed"] and not trial[key]["contractPassed"]],
            "newExceptions": [key for key in base if base[key]["status"] != "Exception_NotEvaluated" and trial[key]["status"] == "Exception_NotEvaluated"],
            "recoveredExceptions_NotQualityProof": [key for key in base if base[key]["status"] == "Exception_NotEvaluated" and trial[key]["status"] != "Exception_NotEvaluated"]}
