#!/usr/bin/env python3
"""Run isolated, single-rule Core experiments inside the existing test executable.
Exit 0: harness and regression green; 2: existing regression gate red; 1: infrastructure/compatibility failure.
Never builds a CAD plugin, launches AutoCAD, edits truth files, or writes a DWG.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import statistics
import subprocess
import sys
import time
import uuid

from ablation_metrics import aggregate, behavior, canonical, compare, digest, evaluate

ROOT = Path(__file__).resolve().parents[1]
DATASET = ROOT / "regression/ablation/dataset.json"


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def save(path, value):
    with Path(path).open("x", encoding="utf-8", newline="\n") as f:
        json.dump(value, f, ensure_ascii=False, indent=2, allow_nan=False)
        f.write("\n")


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT).decode("utf-8", errors="replace").strip()


def source_fingerprints():
    files = []
    for pattern in ("CadAuto.Core/**/*.cs", "CadAuto.Core/**/*.csproj", "CadAuto.Core.Tests/**/*.cs", "CadAuto.Core.Tests/**/*.csproj",
                    "scripts/*ablation*.py", "regression/ablation/dataset.json", "regression/ablation/reference/*.json", "Commands.cs"):
        files.extend(p for p in ROOT.glob(pattern) if "obj" not in p.parts and "bin" not in p.parts)
    return {str(p.relative_to(ROOT)).replace("\\", "/"): sha(p) for p in sorted(set(files))}


def protected_inputs():
    files = []
    for folder in ("regression/core-cad", "regression/hole-slot", "regression/dimension-layout", "regression/visual-inspection", "regression/asd4-correctness", "regression/remediation"):
        files.extend(p for p in (ROOT / folder).rglob("*") if p.is_file() and "runs" not in p.parts
                     and p.suffix.lower() in (".dwg", ".dxf", ".json", ".md"))
    files.extend([ROOT / "diagnostics/last-run.json"] if (ROOT / "diagnostics/last-run.json").exists() else [])
    return {str(p.relative_to(ROOT)).replace("\\", "/"): sha(p) for p in sorted(files)}


def inventory():
    matrix = load(ROOT / "regression/remediation/case-truth-matrix.json")
    program = (ROOT / "CadAuto.Core.Tests/Program.cs").read_text(encoding="utf-8-sig").replace("\r\n", "\n")
    current_hash = hashlib.sha256(program.encode("utf-8")).hexdigest()
    truth = []
    for case in matrix["cases"]:
        source = ROOT / case["input"]["source"]
        actual = None
        if source.is_file():
            actual = current_hash if case["input"].get("hashMode") == "text-lf" else sha(source)
        truth.append(dict(id=case["id"], historicalStatus=case["status"], source=case["input"]["source"],
                          expectedSha256=case["input"]["sha256"].lower(), actualSha256=actual,
                          hashMatches=actual == case["input"]["sha256"].lower(),
                          interpretation="Historical evidence only; a matching fixture is not a fresh CAD pass."))
    round1 = load(ROOT / "regression/asd4-correctness/L01-13/round1-expectations.json")
    return dict(historicalTruthMatrix=truth, l01=dict(status=round1["status"],
        confirmedForbidden=[r["id"] for r in round1["dimensionExpectations"] if r.get("confirmationStatus") == "ConfirmedByUser"],
        pendingRequired=[r["id"] for r in round1["dimensionExpectations"] if r["policy"] == "required" and r.get("needsHumanConfirmation")],
        integrated=False, reason="Production selection/boundary ownership and diagnostic feature mapping are not yet verified. Do not guess a new outline from the extraction."))


def execute(args, out, label, timeout=180):
    start = time.perf_counter()
    try:
        p = subprocess.run([str(a) for a in args], cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=timeout)
        raw, code = p.stdout, p.returncode
    except subprocess.TimeoutExpired as exc:
        raw, code = (exc.stdout or b"") + b"\nOWNED CORE PROCESS TIMED OUT\n", -1
    log = out / (label + ".log")
    log.write_bytes(raw)
    record = dict(command=[str(a) for a in args], cwd=str(ROOT), exitCode=code,
                  elapsedSeconds=time.perf_counter() - start, log=log.name)
    with (out / "commands.jsonl").open("a", encoding="utf-8") as f:
        f.write(canonical(record) + "\n")
    print(f"{label}: exit={code}, {record['elapsedSeconds']:.3f}s", flush=True)
    return record, raw.decode("utf-8", errors="replace")


def validate_snapshot(data, dataset, disabled):
    if data.get("scope") != "CoreOnly_NotCadRendered" or data.get("schemaVersion") != 1:
        raise ValueError("Wrong snapshot schema/scope")
    expected = {(p, c["id"]) for p in dataset["profiles"] for c in dataset["cases"]}
    actual = [(r["profile"], r["caseId"]) for r in data["results"]]
    if len(actual) != len(set(actual)) or set(actual) != expected:
        raise ValueError("Missing, duplicate or extra case/profile; refusing unequal datasets")
    if any(r["disabledRule"] != disabled for r in data["results"]):
        raise ValueError("Wrong rule switch in trial")


def regression_summary(text):
    registered = re.findall(r"RunTest\(nameof\((\w+)\)", (ROOT / "CadAuto.Core.Tests/Program.cs").read_text(encoding="utf-8-sig"))
    passed = re.findall(r"^PASS P\d (\w+)", text, re.M)
    failed = re.findall(r"^FAIL P\d (\w+) :: (.*)", text, re.M)
    return dict(registeredCount=len(registered), executedCount=len(passed) + len(failed), passedCount=len(passed), failedCount=len(failed),
                notExecutedCount=len(registered) - len(passed) - len(failed), failures=[dict(test=n, message=m.strip()) for n, m in failed],
                gateMessages=re.findall(r"^TIER (?:FAIL|NOT RUN).*", text, re.M))


def report(result):
    baseline = result["variants"][0]
    lines = ["# CAD 标注规则单规则消融报告", "", f"- 运行：`{result['runId']}`；时间：`{result['generatedAt']}`（主机时钟）。",
             f"- 代码：`{result['environment']['head']}`，dirty 工作树；精确源码/数据 SHA256、命令、异常见 results.json。",
             f"- 环境：{result['environment']['os']} / {result['environment']['architecture']}；.NET SDK {result['environment']['dotnetSdkVersion']}；目标 .NET Framework 4.7.2；Python {platform.python_version()}。",
             "- 范围：现有 Core 测试宿主和真实规划/布局代码；19 个原有输入 × 2 配置 ×（完整系统 + 11 单规则消融）。未运行生产 CAD。",
             "- legacy-default 保持 CreateDefault 的 false；asd-feature-first 对齐 ASD 的 true，仅表示配置相同，不代表 CAD 识别/渲染已验证。",
             f"- 默认兼容性：**{result['defaultCompatibility']['status']}**；比较修改前指纹及本轮重复结果（不是正确性真值）。", "", "## 现有回归门禁", "",
             f"注册 {result['regression']['registeredCount']}；执行 {result['regression']['executedCount']}；通过 {result['regression']['passedCount']}；失败 {result['regression']['failedCount']}；未执行 {result['regression']['notExecutedCount']}。"]
    lines.extend(f"- `{x['test']}`：{x['message']}" for x in result["regression"]["failures"])
    lines += ["", "## 完整系统基线（部分契约，不是全图真值）", "", "| 配置 | 案例 | 可评估 | 异常 | 契约通过 | 契约失败 | 必需项 | 漏标 | 已知错误 | 重复 | 错误抑制 | 正确保留/召回 | 线重叠/文字代理 |", "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
    for profile in result["dataset"]["profiles"]:
        a = baseline["byProfile"][profile]; m = a["metrics"]
        lines.append(f"| {profile} | {a['caseProfileCount']} | {a['evaluableCount']} | {a['exceptionCount']} | {a['contractPassCount']} | {a['contractFailureCount']} | {m['contractRequired']} | {m['contractMissingRequired']} | {m['contractWrongDimensions']} | {m['contractDuplicateDimensions']} | {m['contractIncorrectlySuppressedRequired']} | {m['contractCorrectRetained']}/{m['contractRecall']:.3f} | {m['coreDimensionLineOverlapPairs']}/{m['centeredTextConflictProxyPairs']} |")
    lines += ["", f"完整系统进程用时 {baseline['timing']['processSeconds']:.3f}s；38 个输入配置的规划/布局累计 {baseline['timing']['totalCaseMilliseconds']:.3f}ms、中位数 {baseline['timing']['medianCaseMilliseconds']:.3f}ms（含异常案例）。",
              "例外案例不记为零缺陷。漏标仅统计显式必需身份缺失；错误包含已知禁止项及已知夹点/侧向违例；召回要求身份及约束都正确。重复是已知身份的超额条数，不按数值去重。",  "", f"另有 {baseline['aggregate']['metrics']['contractSuppressedRequiredAttachmentUnknown']} 个必需身份对应被抑制候选的夹点有效性未知，单列为待确认，不冒充已证明错误删除。", "", "## 单规则比较（固定成对可评估子集）", "", "Δ 正数表示关闭后增加；保留数减少并不等于改善。全部规则各自从完整系统启动，无组合实验。数值合计两个配置的契约槽，不代表独立图纸总量；各配置拆分见 JSON 的 byProfileComparison。",  "", "| 关闭规则 | 可配对 | Δ漏标 | Δ错误 | Δ重复 | Δ错误抑制 | Δ召回 | Δ线重叠 | Δ文字代理 | 新异常/恢复异常 |", "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
    for variant in result["variants"][1:]:
        c = variant["comparison"]; d = c["delta"]
        lines.append(f"| {variant['disabledRule']} | {c['pairedEvaluableCount']} | {d['contractMissingRequired']:+} | {d['contractWrongDimensions']:+} | {d['contractDuplicateDimensions']:+} | {d['contractIncorrectlySuppressedRequired']:+} | {d['contractRecall']:+.3f} | {d['coreDimensionLineOverlapPairs']:+} | {d['centeredTextConflictProxyPairs']:+} | {len(c['newExceptions'])}/{len(c['recoveredExceptions_NotQualityProof'])} |")
    lines += ["", "## 逐条结论与证据", ""]
    for variant in result["variants"][1:]:
        c = variant["comparison"]; d = c["delta"]
        conclusion = "暂保留；关闭导致已知契约回归，但仍须人工真值和 CAD 验证。" if variant["recommendation"] == "RetainPendingHumanCadValidation" else "进一步验证；当前可配对输入没有证明其独立必要性，不支持移除。"
        if c["recoveredExceptions_NotQualityProof"]:
            conclusion += " 关闭后避开既有异常，提示该阶段需修改/修复；不是尺寸质量改善证据。"
        lines += [f"### {variant['disabledRule']}", "", conclusion,
                  f"- 隔离入口：`{variant['entry']}`；{variant['fallback']}",
                  f"- 默认契约从通过变失败：{', '.join(c['contractRegressions']) or '无'}。",
                  f"- 候选、尺寸或布局证据有变化：{', '.join(variant['changedCases']) or '无'}。",
                  f"- 本版本用时：进程 {variant['timing']['processSeconds']:.3f}s；规划/布局累计 {variant['timing']['totalCaseMilliseconds']:.3f}ms、中位数 {variant['timing']['medianCaseMilliseconds']:.3f}ms（单次冷启动，含异常）。",
                  f"- 本变体绝对结果（非配对差值）：可评估 {variant['aggregate']['evaluableCount']}/38、异常 {variant['aggregate']['exceptionCount']}；漏标 {variant['aggregate']['metrics']['contractMissingRequired']}、已知错误 {variant['aggregate']['metrics']['contractWrongDimensions']}、重复 {variant['aggregate']['metrics']['contractDuplicateDimensions']}、错误抑制 {variant['aggregate']['metrics']['contractIncorrectlySuppressedRequired']}。恢复异常案例没有可靠的原质量分数，不得据此计算改善。",
                  f"- 新异常：{', '.join(c['newExceptions']) or '无'}；避开原异常：{', '.join(c['recoveredExceptions_NotQualityProof']) or '无'}。",
                  f"- 配对 Δ尺寸总数 {d['finalDimensionCount']:+}，Δ正确保留 {d['contractCorrectRetained']:+}；不可单独据尺寸数或通过率判断布局。",
                  f"- 原始证据：`{variant['snapshot']}`；逐例 expected/actual、候选 ID、真实夹点、抑制理由、布局冲突对见 results.json 的 variants/cases。", ""]
    lines += ["## 默认失败及证据索引", ""]
    for key, case in baseline["cases"].items():
        if not case["contractPassed"]:
            detail = case["evidence"][0]
            reason = detail.get("error", "契约违例：" + str(detail.get("requirement", detail.get("forbidden"))))
            lines.append(f"- `{key}`：{reason.splitlines()[0]}；完整证据见 results.json。")
    lines += ["", "## 真值、依赖和未评估边界", "", "- 历史矩阵将 P0/P1 标记为确认，但当前源码哈希不匹配；本轮仅称“现有测试契约”，不提升为当前图纸人工真值。OC01/DL01/HS 等 DWG/期望哈希及历史标签已盘点；未据旧报告宣称本轮通过。",
              "- L01-13：旧 expectations.json 待确认；round1-expectations.json 已确认禁止顶部 20、右下 15，四条必需尺寸的基准/公差仍待确认。未猜测其 MainOutline 或诊断身份，未纳入本轮 Core 消融。",
              "- 全图必须尺寸漏标、错误尺寸、错误删除、正确保留率、实际文字/箭头/延伸线冲突均为 **null/NotReliablyComputable**。目前只有部分契约计数和 Core 布局代理。未知输出不算错；异常不算零。",
              "- 线重叠来自 Core 同侧同坐标严格区间相交；文字代理复用 Core 文字长度模型，未执行 CAD 文字滑移/实际字框测量，不是最终渲染碰撞率。不同侧碰撞、延伸线/轮廓冲突尚未测量。",
              "- 本轮运行时间为冷启动单次观测，包含 JIT；JSON 记录每例毫秒及进程时间，不作统计显著性或性能收益结论。",
              "- 镜像、同侧去重、包络和整体分割可能互相掩盖；组识别会改变后续公差/传递链/功能孔所有权；完成阶段的尺寸提供者与覆盖消费者必须一起隔离。无变化只说明本数据未识别独立影响。",
              "- 建议修改/排查：既有 P0 凹轮廓基准失败、FeatureFirst RuleId 重赋异常、阶梯板 53 必需结构尺寸、PowerShell 包装器空 ExitCode；本任务没有擅改这些产品行为或人工预期。",
              "- 建议暂保留（关闭有契约回归）：" + ", ".join(v["disabledRule"] for v in result["variants"][1:] if v["recommendation"] == "RetainPendingHumanCadValidation") + "。",
              "- 建议修复/排查后再验证：" + ", ".join(v["disabledRule"] for v in result["variants"][1:] if v["recommendation"] == "InvestigateExistingExceptionThenValidate") + "。",
              "- 建议进一步验证：" + ", ".join(v["disabledRule"] for v in result["variants"][1:] if v["recommendation"] == "FurtherValidationInsufficientIndependentEvidence") + "。没有规则得到足够证据支持移除。", ""]
    lines.extend(f"- 未评估 `{r['rule']}`：{r['reason']}" for r in result["dataset"]["notEvaluated"])
    lines += ["", "## 重复运行", "", "```powershell", f"cd '{ROOT}'", "python -m unittest discover -s scripts -p test_ablation_metrics.py", "python scripts/run-ablation.py", "```", "", "每次创建全新运行目录，拒绝覆盖已有目录/快照；原始 DWG、期望和 diagnostics/last-run.json 运行前后均校验哈希。", "Core 回归仍红时脚本完成实验并返回 2；基础设施/默认兼容性失败返回 1。返回 0 才表示所有门禁通过，绝不以“实验完成”替代回归通过。", "", f"- 运行产物：`{result['outputDirectory']}`", "- 机器结果：`results.json`；逐条命令/退出码：`commands.jsonl`；完整诊断：`snapshots/*.json`。", ""]
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, help="Must not exist; defaults to a unique regression/ablation/runs directory")
    parser.add_argument("--reference", type=Path, default=ROOT / "regression/ablation/reference/default-behavior.json")
    args = parser.parse_args()
    run_id = dt.datetime.now().strftime("%Y%m%d-%H%M%S") + "-" + uuid.uuid4().hex[:8]
    out = (args.output or ROOT / "regression/ablation/runs" / run_id).resolve()
    out.mkdir(parents=True, exist_ok=False)
    dataset = load(DATASET)
    reference = load(args.reference)
    protected = protected_inputs()
    sources = source_fingerprints()
    env = dict(head=git("rev-parse", "HEAD"), branch=git("branch", "--show-current"),
               status=git("-c", "core.quotePath=false", "status", "--porcelain=v1", "-uall").splitlines(),
               python=sys.version, os=platform.platform(), architecture=platform.machine(),
               sourceSha256=sources, protectedInputSha256=protected, datasetSha256=sha(DATASET),
               compatibilityReferenceSha256=sha(args.reference))
    commands = []
    cmd, sdk = execute(["dotnet", "--version"], out, "dotnet-version")
    commands.append(cmd)
    env["dotnetSdkVersion"] = sdk.strip()
    save(out / "environment.json", env)
    cmd, _ = execute(["dotnet", "--info"], out, "dotnet-info")
    commands.append(cmd)
    cmd, _ = execute([sys.executable, "-m", "unittest", "discover", "-s", "scripts", "-p", "test_ablation_metrics.py"], out, "metric-tests")
    commands.append(cmd)
    build = out / "core-build"
    cmd, _ = execute(["dotnet", "msbuild", "CadAuto.Core.Tests/CadAuto.Core.Tests.csproj", "/t:Build", "/p:Configuration=Debug", f"/p:OutDir={build}{os.sep}", "/v:minimal"], out, "core-build")
    commands.append(cmd)
    if cmd["exitCode"] != 0:
        raise RuntimeError("Core build failed; see " + str(out))
    exe = build / "CadAuto.Core.Tests.exe"
    binaries = {p.name: sha(p) for p in (exe, build / "CadAuto.Core.dll")}
    env["binariesSha256"] = binaries
    save(out / "build-artifacts.json", binaries)
    for name, flag in (("runner-self-check", "--runner-self-check"), ("ablation-self-check", "--ablation-self-check")):
        cmd, _ = execute([exe, flag], out, name)
        commands.append(cmd)
    snapshot_dir = out / "snapshots"
    snapshot_dir.mkdir()
    variants, raw_baseline = [], None
    cases = {c["id"]: c for c in dataset["cases"]}
    compatibility = dict(status="MatchedPreSwitchReferenceAndRepeat", mismatches=[], reference=str(args.reference),
                         meaning="Behavior compatibility only, never annotation truth")
    for rule in [{"id": "None", "entry": "All current rules", "fallback": "None"}] + dataset["rules"] + [{"id": "None-repeat"}]:
        name = rule["id"]
        disabled = "None" if name == "None-repeat" else name
        path = snapshot_dir / (name + ".json")
        cmd, _ = execute([exe, "--ablation", path, disabled], out, "trial-" + name)
        commands.append(cmd)
        if cmd["exitCode"] != 0:
            raise RuntimeError("Trial process failed: " + name)
        data = load(path)
        validate_snapshot(data, dataset, disabled)
        rows = {r["profile"] + "/" + r["caseId"]: r for r in data["results"]}
        if raw_baseline is None:
            raw_baseline = rows
            if set(reference["records"]) != set(rows):
                raise ValueError("Compatibility reference dataset changed; do not silently regenerate it")
            for key, r in rows.items():
                if reference["records"][key]["behaviorSha256"] != digest(behavior(r)):
                    compatibility["mismatches"].append(dict(case=key, reason="PreSwitchReferenceMismatch"))
        else:
            for key, r in rows.items():
                if r["inputSha256"] != raw_baseline[key]["inputSha256"]:
                    raise ValueError("Trial input drift: " + key)
                if name == "None-repeat" and behavior(r) != behavior(raw_baseline[key]):
                    compatibility["mismatches"].append(dict(case=key, reason="RepeatMismatch"))
        if name == "None-repeat":
            continue
        evaluations = {key: evaluate(r, cases[r["caseId"]], dataset["epsilon"]) for key, r in rows.items()}
        variant = dict(disabledRule=name, switches={r["id"]: r["id"] != name for r in dataset["rules"]},
                       entry=rule["entry"], fallback=rule["fallback"], snapshot=str(path.relative_to(out)).replace("\\", "/"),
                       snapshotSha256=sha(path), cases=evaluations, aggregate=aggregate(list(evaluations.values())),
                       byProfile={p: aggregate([e for k, e in evaluations.items() if k.startswith(p + "/")]) for p in dataset["profiles"]},
                       timing=dict(totalCaseMilliseconds=sum(r["elapsedMilliseconds"] for r in rows.values()),
                                   medianCaseMilliseconds=statistics.median(r["elapsedMilliseconds"] for r in rows.values()),
                                   processSeconds=cmd["elapsedSeconds"]))
        if name != "None":
            variant["comparison"] = compare(variants[0]["cases"], evaluations)
            comparison = variant["comparison"]
            d = comparison["delta"]
            worsened = any(d[k] is not None and d[k] > 0 for k in ("contractMissingRequired", "contractWrongDimensions", "contractDuplicateDimensions", "contractIncorrectlySuppressedRequired")) or (d["contractRecall"] is not None and d["contractRecall"] < 0) or bool(comparison["newExceptions"])
            variant["recommendation"] = ("RetainPendingHumanCadValidation" if worsened else "InvestigateExistingExceptionThenValidate"
                if comparison["recoveredExceptions_NotQualityProof"] else "FurtherValidationInsufficientIndependentEvidence")
            variant["byProfileComparison"] = {p: compare({k: e for k, e in variants[0]["cases"].items() if k.startswith(p + "/")},
                                                        {k: e for k, e in evaluations.items() if k.startswith(p + "/")}) for p in dataset["profiles"]}
            variant["metricChangedCases"] = [k for k in rows if evaluations[k]["metrics"] != variants[0]["cases"][k]["metrics"]]
            variant["changedCases"] = [k for k in rows if behavior(rows[k]) != behavior(raw_baseline[k])]
        variants.append(variant)
    cmd, text = execute([exe], out, "final-existing-regression")
    commands.append(cmd)
    if compatibility["mismatches"]:
        compatibility["status"] = "FAILED_DefaultBehaviorDrift"
    protected_after, sources_after = protected_inputs(), source_fingerprints()
    unchanged = protected_after == protected and sources_after == sources and binaries == {p.name: sha(p) for p in (exe, build / "CadAuto.Core.dll")}
    initial_regression = load(ROOT / "regression/ablation/reference/initial-regression.json")
    current_regression = regression_summary(text)
    current_regression["matchesInitialGateOutcome"] = all(current_regression[key] == initial_regression[key]
        for key in ("registeredCount", "executedCount", "passedCount", "failedCount", "notExecutedCount")) and all(
            failure["test"] in initial_regression["stdout"] and failure["message"] in initial_regression["stdout"]
            for failure in current_regression["failures"])
    result = dict(schemaVersion=1, runId=run_id, generatedAt=dt.datetime.now(dt.timezone.utc).isoformat(),
                  outputDirectory=str(out), environment=env, dataset=dataset, truthInventory=inventory(),
                  defaultCompatibility=compatibility, protectedInputsAndSourcesUnchanged=unchanged,
                  globalAnnotationTruthMetrics=dict(status="NotReliablyComputable", missingRequired=None, wrongDimensions=None,
                      duplicates=None, incorrectlyDeletedCorrect=None, correctRetentionRecall=None,
                      renderedTextConflicts=None, renderedArrowConflicts=None, extensionLineConflicts=None),
                  initialRegression=initial_regression,
                  regression=dict(**current_regression, process=cmd), commands=commands, variants=variants)
    result["status"] = ("InfrastructureOrCompatibilityFailure" if not unchanged or compatibility["mismatches"] or any(c["exitCode"] != 0 for c in commands[:-1])
                        else "BlockedExistingRegression" if cmd["exitCode"] != 0 else "CompletedAllGatesPassed")
    save(out / "results.json", result)
    (out / "REPORT.md").write_text(report(result), encoding="utf-8")
    print("OUTPUT=" + str(out))
    print("STATUS=" + result["status"])
    return 1 if result["status"] == "InfrastructureOrCompatibilityFailure" else 2 if cmd["exitCode"] != 0 else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exc:
        print(f"ABLATION INFRASTRUCTURE ERROR: {exc}", file=sys.stderr)
        sys.exit(1)
