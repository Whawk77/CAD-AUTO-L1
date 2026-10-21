# RuleId 覆盖冲突最小修复

范围：Core 代码修复与验证；没有编译新 AutoCAD 插件、部署、启动/关闭 CAD 或执行 DWG。v75/v76 保持原文件，新修复尚未进入它们。

## 根因与修改

- `TrySuppressCoveredStructure` 已通过 replaceableRuleId 审核，却把覆盖规则作为新的候选 RuleId 传给 MarkSuppressed；与生成时的 ObliqueProjection/ChamferSpan 归属冲突。
- 有原 RuleId 时保留；无归属时保留首次赋值的旧行为。覆盖规则继续进入 SuppressedReason/DecisionReason，覆盖证据继续保留 CoveredByIds。
- 合并原生成拓扑依据与覆盖依据，不移动夹点或改尺寸值。
- 不改 EnsureCompatibleRuleId、覆盖资格、拓扑证明、有效提供者、缺失依赖审计、未知夹点处理或已有期望。
- 改动只有 DimensionPlan.cs、Program.cs 的 4 个注册项，以及新 Runner/CoverageRuleRecoveryChecks.cs。原 245 个测试及工厂正文逐字节不变。

## 结果

- 新增 4 项：修复前 1 通过/3 失败；修复后 4/4 通过。
- 原 245 项：218 通过/27 失败 -> 221 通过/24 失败；3 项原斜边信息测试转绿，没有 Passed -> Failed。
- 总计 249 项：修复前 219 通过/30 失败；修复后及重复 225 通过/24 失败，均 0 跳过。
- 默认门禁仍失败：115 执行、96 通过、19 失败、134 未执行；诊断继续不代表发布通过。
- 覆盖 8/8、原防改写 1/1、斜边集成 3/3、P0 11/11、26 组孔基准检查、17/17 测量测试及两个 runner/ablation 自测通过。

## 被原异常遮住、现已到达的失败断言

- `CNotchPlateKeepsTopEnvelopeBodyWidth120`：System.InvalidOperationException: bottom 120 must stay dropped
- `LProfileFarEndCornerUsesAdjacentVerticalChain`：System.InvalidOperationException: far-end corner L profile must suppress the derived 69 inner edge, case=0

这两项之前也是 Failed，之前先遇到 RuleId 异常。本轮没有更改其断言或扩大修复，不能说已通过或属于新增 Passed -> Failed。

## 原消融与保护

- 原工具重跑完成 456 个案例/配置/变体观测；默认 38 个中原先 6 个异常现可规划，其余 32 个语义指纹与修复前一致，全部输入哈希一致，38 个默认重复一致。异常恢复不是完整标注正确性的证明。
- 原参考仍报 8 项差异：6 项是本修复从异常恢复，2 项 concave-datum 是上一轮孔基准修复；不刷新旧参考。因此原工具仍返回 1 / InfrastructureOrCompatibilityFailure。
- 有限契约当前 38 可评估、36 通过/2 失败；仍有值 53 缺失和 CNotch bottom-120，不是全图人工真值。
- 初始 620 个受保护文件哈希全部不变，包括当前失败诊断、原期望/消融档案、v75/v76 和原 dirty 图谱。
- 在独立临时源码/图谱副本执行 graphify update，原图谱未覆盖。git diff --check 通过。

## 每条执行命令

Root commands.jsonl 与 ablation-after/commands.jsonl 保留精确参数、退出码与时间。原进程输出按字节 base64 存于 process-logs.json。

### before-build.log (exit=0)

```text
dotnet msbuild CadAuto.Core.Tests/CadAuto.Core.Tests.csproj /t:Build /p:Configuration=Debug "/p:OutputPath=D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\before-build/" "/p:OutDir=D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\before-build/" /p:BaseIntermediateOutputPath=obj-coverage-4c9062-before/ /v:minimal
```

### coverage-before.log (exit=1)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\before-build\CadAuto.Core.Tests.exe" --filter StructureCoverage --continue-on-failure --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\coverage-before.json"
```

### rule-guard-before.log (exit=0)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\before-build\CadAuto.Core.Tests.exe" --filter RuleEvidenceRejectsReassignment --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\rule-guard-before.json"
```

### full-before.log (exit=1)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\before-build\CadAuto.Core.Tests.exe" --continue-on-failure --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\full-before.json"
```

### after-build.log (exit=0)

```text
dotnet msbuild CadAuto.Core.Tests/CadAuto.Core.Tests.csproj /t:Build /p:Configuration=Debug "/p:OutputPath=D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build/" "/p:OutDir=D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build/" /p:BaseIntermediateOutputPath=obj-coverage-4c9062-after/ /v:minimal
```

### coverage-after.log (exit=0)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build\CadAuto.Core.Tests.exe" --filter StructureCoverage --continue-on-failure --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\coverage-after.json"
```

### rule-guard-after.log (exit=0)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build\CadAuto.Core.Tests.exe" --filter RuleEvidenceRejectsReassignment --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\rule-guard-after.json"
```

### oblique-after.log (exit=0)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build\CadAuto.Core.Tests.exe" --filter ObliqueBoundaryInformation --continue-on-failure --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\oblique-after.json"
```

### p0-after.log (exit=0)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build\CadAuto.Core.Tests.exe" --filter P0 --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\p0-after.json"
```

### full-after.log (exit=1)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build\CadAuto.Core.Tests.exe" --continue-on-failure --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\full-after.json"
```

### gated-after.log (exit=1)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build\CadAuto.Core.Tests.exe" --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\gated-after.json"
```

### runner-self-check.log (exit=0)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build\CadAuto.Core.Tests.exe" --runner-self-check
```

### ablation-self-check.log (exit=0)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build\CadAuto.Core.Tests.exe" --ablation-self-check
```

### datum-reference-self-check.log (exit=0)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build\CadAuto.Core.Tests.exe" --datum-reference-self-check
```

### measurement-tests.log (exit=0)

```text
python -m unittest discover -s scripts -p test_ablation_metrics.py
```

### ablation-after.log (exit=1)

```text
python scripts/run-ablation.py --output "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\ablation-after"
```

### baseline-before.log (exit=0)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\before-build\CadAuto.Core.Tests.exe" --ablation "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\baseline-before.json" None
```

### baseline-after-repeat.log (exit=0)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build\CadAuto.Core.Tests.exe" --ablation "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\baseline-after-repeat.json" None
```

### full-after-repeat.log (exit=1)

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\after-build\CadAuto.Core.Tests.exe" --continue-on-failure --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\full-after-repeat.json"
```

### graphify-isolated.log (exit=0)

```text
cmd /c C:\Users\Administrator\.local\bin\graphify.EXE update . --no-cluster
```

### git-diff-check.log (exit=0)

```text
git diff --check
```

## 剩余 24 项失败

- P1 `RotationSignature_F215GoldenMultisetFourWayEqual`：System.InvalidOperationException: F215 @0° Signature must equal golden; got=[50,95,100,215] expected=[42,50,75,100,120,215]
- P1 `RotationSignature_F215CadGoldenMultisetFourWayEqual`：System.InvalidOperationException: F215Cad @0° Signature must equal golden; got=[42,100,215] expected=[42,50,75,100,120,215]
- P1 `F215CadRotatedKeeps42Drops32Stub`：System.InvalidOperationException: rotated F215 must keep arm 42; sig=[38,75,100,215]
- P1 `RotationSignature_F338CadGoldenMultisetFourWayEqual`：System.InvalidOperationException: F338Cad @0° Signature must equal golden; got=[87.55,201.5,338] expected=[20,73,87.55,100,201.5,305,338]
- P1 `CadLastRunGrooveSnapsToWallClearWidthFourWay`：System.InvalidOperationException: CAD-last-run @0° must keep foot tip 20; kept=[92.55]
- P1 `RotationSignature_Stair3GoldenMultisetFourWayEqual`：System.InvalidOperationException: Stair3 @0° Signature must equal golden; got=[134.79,134.79,163.37,269.59,429.91,438.8] expected=[126.11,134.79,134.79,163.37,429.91,438.8]
- P1 `RotationSignature_LBoss257GoldenMultisetFourWayEqual`：System.InvalidOperationException: LBoss257 @0° Signature must equal golden; got=[3,3,3,3,10,10,15,18,25,25,25,30,30,30,40,58,66,134,232,232,257] expected=[10,25,25,25,30,40,58,134,257]
- P1 `SteppedPlateReplacesThinStepRisersWithComplement53`：System.InvalidOperationException: 53 (overall min to inner top ledge) must be kept; sig=[50,50,55,102.5]
- P1 `CNotchPlateKeepsTopEnvelopeBodyWidth120`：System.InvalidOperationException: bottom 120 must stay dropped
- P1 `StepGroovePlacesOnSameSideAsOuterStepFourWay`：System.InvalidOperationException: F338Cad @0° missing 73; sides=[87.55@Bottom]
- P1 `CrossSideStructureWidthsThatCloseOverallChainAreSuppressed`：System.InvalidOperationException: full-path step length 20 must remain
- P1 `DatumRootedBottomAndLeftOuterEnvelopeDimensionsAreSuppressed`：System.InvalidOperationException: bottom overall width 96 must remain
- P1 `StructureWidthThatPartitionsOverallWithOutlineSegmentIsSuppressed`：System.InvalidOperationException: short structure step 20 must remain (decision A with overall 98)
- P1 `LProfileChamferedArmResidualUsesBoundarySequence`：System.InvalidOperationException: the real short edge must be suppressed by boundary topology, not a fixed numeric value width=32 chamfer=7 rotation=0 mirror=False
- P1 `LProfileAnnotationRulesUseTopology`：System.InvalidOperationException: top 25 replacement must retain an attachment-valid rule diagnostic
- P1 `LProfileFarEndCornerUsesAdjacentVerticalChain`：System.InvalidOperationException: far-end corner L profile must suppress the derived 69 inner edge, case=0
- P1 `LProfileOuterContourAnnotationRulesUseTopology`：System.InvalidOperationException: outer-contour L must diagnose derived inner 52 suppression, case=1
- P1 `BottomProtrusionSuppressesOuterWidthAndInnerLedge`：System.InvalidOperationException: bottom protrusion width 50 on partial MinY must be kept
- P1 `HoleAndPinChainsUseSideFacingCenterlineEndpoints`：System.InvalidOperationException: pin horizontal distances must inherit the datum-pin top side and axis-endpoint chain
- P2 `SlotDimensionsUseCenterAndDatumChainsWithoutPins`：System.InvalidOperationException: horizontal slot datum location should be planned without pins
- P2 `CenterlineEndpointIsAllowedForHolePositioning`：System.InvalidOperationException: hole positioning may use a selected centerline endpoint
- P2 `PinGroupsPlanBaseAndPairDistances`：System.InvalidOperationException: PG1 datum, PG1-to-PG2 transfer, and PG2 direct dimensions must share one rooted datum chain
- P3 `SlotChainAlignmentKeyAlignsEdgeLocationAndCenterDistance`：System.InvalidOperationException: expected vertical slot chain segments (edge location + center distance)
- P3 `RootedLaneChoosesNearestSafeFiniteSegmentCoordinate`：System.InvalidOperationException: the block must choose the nearest candidate that is safe for every finite dimension segment

## 文件说明

- results.json：机器摘要；reports/*.json.gz：完整修复前后结果、重复结果、默认快照与保护清单，解压后为原字节。
- session-changes.patch：相对本轮开始 dirty 源码的增量，不是对 HEAD 整体改写。
- ablation-after/snapshots/*.json.gz：原始快照；旧历史档案未改。
- 本轮不能证明桌面 13.dwg 已修复验收；尚需另行授权后用新插件和图纸副本进行 CAD 验证。
