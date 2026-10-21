# Core 回归执行恢复与凹轮廓基准夹点修复

执行主机日期：2026-10-17；Git HEAD：`37c16787008014ad907b6b4ff358334bce80e5be`，分支 `codex/non-occluding-centerline-rule`，基于原有 dirty 工作树精确编辑，未提交 Git。

证据：[报告](REPORT.md)、[机器结果](results.json)、[实际命令](commands.jsonl)。

## 结论与边界

目标 `ConcaveHoleDatumUsesRealOutlineIntersections` 已修复；全部 245 个原有测试方法可在显式诊断模式下完整执行。**不是全量回归全绿，也不是 CAD/DWG 验收通过。**

| 执行方式 | 注册 | 执行 | 通过 | 失败 | 跳过 |
|---|---:|---:|---:|---:|---:|
| 修复前，默认门禁 | 245 | 7 | 6 | 1 | 238 |
| 修复前，诊断全跑 | 245 | 245 | 217 | 28 | 0 |
| 修复后，默认门禁 | 245 | 115 | 93 | 22 | 130 |
| 修复后，诊断全跑 | 245 | 245 | 218 | 27 | 0 |

245 项逐项比较，唯一状态变化为目标测试 `Failed -> Passed`；其他 27 项失败的首条错误信息相同，没有 `Passed -> Failed`。245 个原测试方法及其辅助几何工厂的源码保持不变，没有改期望值，也没有把失败改成跳过。

上一轮的 6 组 FeatureFirst **快照异常**、值为 **53 的一项尺寸缺失**以及未知夹点候选处理均未修复。它们和本轮发现的其他既有失败留待独立任务。这里的 245/27 是测试方法数量，不代表 DWG 数量或独立根因数量；失败方法内后续断言/参数循环仍按原语义停止，未到达的组合不算通过。

## 为什么原来有 238 项未执行

所有测试先完成注册，再按 P0、P1、P2、P3 执行：

- P0 11 项中的第 7 项失败，`RunRegisteredTests` 立即 `return`，同层剩余 **4 项**也未执行。
- P1 **104 项**、P2 **70 项**、P3 **60 项**均受 P0 门禁阻止。
- 合计 `4 + 104 + 70 + 60 = 238`，不是测试漏注册、过滤器漏匹配或超时。
- 旧 `_selectedTests` 按进入的层级累加，打印 `6/11 passed`；11 是进入 P0 时选入的数量，不是实际执行数量 7。
- 原有 P1–P3 会执行完本层再判断；本层有失败则不进入后续层。这项默认门禁仍保留。

## 新的显式诊断执行方式

现有 `CadAuto.Core.Tests.exe` 新增：

```text
[原有测试名子串或 P0/P1/P2/P3]
--filter <测试名子串或层级>
--continue-on-failure
--report-json <不存在的 JSON 文件>
--datum-reference-self-check
```

`--continue-on-failure` **仅继续执行下一个 Core 测试方法**，仍保持层级顺序和串行执行，仍执行每条原断言；任何失败都返回退出码 1，不授权编译插件、部署或 CAD 验证。默认执行仍受原门禁约束。

新增逐项报告包括：`Name`、`Priority`、`Selected`、`Status`、`Reason`、完整异常 `Error`、可空耗时 `ElapsedMilliseconds`。状态含义：

- `Passed`：方法实际执行并正常完成。
- `Failed`：方法实际执行并抛出异常；完整异常保留。
- `Skipped/BlockedByP<n>:<名称>`：因默认门禁未执行。
- `Skipped/FilteredOut`：未被本次过滤器选中。

汇总分别给出注册、选中、执行、通过、失败、阻塞、过滤和跳过数量。未执行记录的耗时为 null，不计为通过。诊断模式和过滤运行的 `productGatePassed` 均为 false。JSON 使用 `FileMode.CreateNew`，拒绝覆盖已有报告；未知选项、无匹配过滤器和重复报告路径不会静默成功。

`--runner-self-check` 保留原有门禁自测，并增加默认停止、继续执行、过滤、完整异常、计数一致性、诊断不可绕过产品门禁、历史报告不可覆盖等检查。新增 26 组几何自测放在单独模式中，没有把原注册数 245 改大来混淆前后比较。

## 目标失败的根因和最小输入

输入为 Core 中构造的闭合 L 形 6 条真实线段，不是 DWG：

```text
(100,100) -> (200,100) -> (200,200)
          -> (160,200) -> (160,140) -> (100,140) -> (100,100)
普通孔中心 (180,170)，直径 8
BaseX=100，BaseY=100；没有已选中心线端点
```

| 项目 | 原断言/真实边界查询 | 修复前最终夹点 | 修复后 |
|---|---|---|---|
| 水平孔定位首点 | `(100,140)` | `(100,140)` | `(100,140)` |
| 竖直孔定位首点 | `(180,100)` | **`(200,100)`** | `(180,100)` |
| 竖直测量值 | `170 - 100 = 70` | 70 | 70 |
| 竖直布局侧 | Right | Right | Right |

调用链：

```text
CreateDimensionPlan
 -> AddNonPinHolesFromOutlineEdge
 -> AddNonPinHoleDatumLocation
 -> AddVerticalOutlineReferenceDimension
 -> TryFindHorizontalBoundaryPoint(Y=100, preferredX=180)  // 正确得到 (180,100)
 -> GetSideFacingOutlineSegmentEndpoint(Right)            // 原先无条件改为 (200,100)
 -> ApplyCenterlineEndpointAttachments                    // 无端点证据，不修正
 -> post-validator                                       // 两点都在边界且 Y 正确，不能识别此语义错误
```

轮廓闭合，包络与基准正确；`(180,100)` 是孔的竖直轴线与真实底边的交点。问题属于**算法在无匹配证据时误用侧向端点策略**，不是输入几何错误，也不需要修改原断言。仅有“落在真实边界上”不足以证明夹点语义正确。

### 最小修复

- 无销孔时回退到外轮廓定位的非销孔（普通孔/螺纹孔共用入口），先复用原有 `FindSideFacingCenterlineEndpoint(...).IsMatched`，判断当前方向是否确有匹配证据。
- 只有匹配时才沿用既有侧向轮廓段端点策略；无匹配时保留真实边界查询得到的最近交点。
- 两个私有轮廓引用 helper 新增默认 true 的可选参数，由上述非销孔基准定位调用点显式控制。
- 不改基准坐标、布局侧、测量值、销组算法、槽路径、中心线匹配器或未知有效性处理。其他调用点保持原行为。

26 组附加几何检查覆盖 legacy/FeatureFirst、原 L 形、矩形、平移、四侧孔位置、反向基准、分段真实底边、无端点/无关端点/匹配端点；匹配端点时保留原有侧向附着行为。新增检查在修复前已跑红，输出明确显示 `(180,100) -> (200,100)` 的错误，修复后全部通过。

## 消融重跑与默认行为差异

没有修改 `regression/ablation/dataset.json`、`reference/default-behavior.json` 或历史实验结果。使用原工具在两个全新目录重跑，并保留两次原始输出：

1. 首次重跑遇到外部活动更新 `diagnostics/last-run.json` 和 `.outline.svg`，保护检查为 false；原失败结果未删除或改写。
2. 新目录确认重跑的输入/源码/二进制保护检查为 true。
3. 两次工具仍返回 **1**：旧默认参考正确地检出本次授权修复的 2 个差异。没有刷新旧参考以获取全绿。

对 38 组默认快照逐项审查，差异仅为 `legacy-default/concave-datum`、`asd-feature-first/concave-datum` 的 `HoleDatumY.FirstPoint.X: 200 -> 180`。其他 36 组语义指纹相同（比较忽略耗时和异常栈位置）；6 组既有异常的类型/首条消息、输入及输出状态相同；值 53 的缺失和未知候选仍保留。新的完整系统重复快照 38/38 一致，全部变体仍使用相同输入。

原消融测量测试 17/17、实验自测和 runner 自测均通过；但这不代表原回归 245 项全绿。工具保留的 `InfrastructureOrCompatibilityFailure` 状态与审计结论“仅目标修复的预期兼容性差异”需要同时阅读。

### 并发现场诊断

任务执行期间检测到一份新的 `ASD6` 报告，记录的插件为已有 `bin/Debug-v75/AutoFixtureDim.dll`，AutoCAD 进程启动时间早于本任务。Core 工程没有写 `diagnostics/` 的路径，本轮命令也没有启动/控制 AutoCAD。这些现场文件没有被回退、覆盖或当作本轮 DWG 验收。旧消融档案、固定 DWG 与人工期望保持原哈希；两份 live 诊断的外部变化单独记录在证据中。

## 重复执行

在项目根目录，用一个新的输出目录直接构建原测试项目并运行 exe，避开已知但本轮未修改的 PowerShell 包装器空 `Process.ExitCode` 问题：

```powershell
$run = Join-Path (Get-Location) ('regression/ablation/runs/core-recovery-' + [guid]::NewGuid().ToString('N'))
$build = Join-Path $run 'build'
dotnet msbuild CadAuto.Core.Tests/CadAuto.Core.Tests.csproj /t:Build /p:Configuration=Debug "/p:OutDir=$build/" /v:minimal
$exe = Join-Path $build 'CadAuto.Core.Tests.exe'
& $exe --runner-self-check
& $exe --datum-reference-self-check
& $exe ConcaveHoleDatumUsesRealOutlineIntersections --report-json (Join-Path $run 'target.json')
& $exe --continue-on-failure --report-json (Join-Path $run 'full-diagnostic.json')
# 仍有既有失败时，上一条预期返回 1；不要忽略其报告。
& $exe --report-json (Join-Path $run 'default-gated.json')
python -m unittest discover -s scripts -p test_ablation_metrics.py
python scripts/run-ablation.py
# 旧默认参考仍会检出本轮授权差异；不能擅自刷新参考。
git diff --check
```

精确的本轮实际路径、参数、退出码、耗时和原始日志见归档 `commands.jsonl` / `process-logs.json`。新的 JSON 路径必须不存在，不能复用旧文件。

## 尚未解决

- 完整诊断仍有 27 个既有失败；默认门禁在 P1 阻塞，P2/P3 的 130 项在默认模式下仍不执行，但诊断模式已全部执行。
- FeatureFirst 异常、值 53 缺失、未知候选、其余孔组/槽/布局/旋转用例需按独立证据分别裁决，未同时修改。
- 旧 PowerShell 包装器进程退出码问题保留；本轮使用直接 MSBuild/exe 命令，没有宣称该包装器已修复。
- 未构建/部署 CAD 插件，未执行 DWG 标注或图像验收。外部 v75 现场报告不包含本轮新构建验证，不能用来替代它。

## 附录 A：本轮实际修改文件
- `CadAuto.Core.Tests/Program.cs`：参数、诊断继续执行、完整异常和准确结果汇总；原 245 项测试/工厂后缀字节不变
- `CadAuto.Core.Tests/CadAuto.Core.Tests.csproj`：仅增加 Runner/*.cs 编译项
- `CadAuto.Core/Planning/DimensionPlanner.cs`：私有轮廓引用 helper 增加默认 true 的端点调整参数
- `CadAuto.Core/Planning/DimensionPlanner.Features.cs`：无销孔的非销孔基准定位复用 IsMatched 决定是否调整端点
- 新增 `CadAuto.Core.Tests/Runner/RegressionReporting.cs`
- 新增 `CadAuto.Core.Tests/Runner/DatumReferenceRecoveryChecks.cs`
- 新增 `docs/CoreRegressionRecovery.md`

`session-changes.patch` 是相对任务开始 dirty 源码的增量，不是从 HEAD 误打包全部用户改动。`source-before.zip` 保留修复前 Core/测试/脚本源码，可在独立目录重建目标失败，不要覆盖当前工作树。

## 附录 B：仍失败的 27 个原测试方法
- P1 `RotationSignature_F215GoldenMultisetFourWayEqual`：System.InvalidOperationException: F215 @0° Signature must equal golden; got=[50,95,100,215] expected=[42,50,75,100,120,215]
- P1 `RotationSignature_F215CadGoldenMultisetFourWayEqual`：System.InvalidOperationException: F215Cad @0° Signature must equal golden; got=[42,100,215] expected=[42,50,75,100,120,215]
- P1 `F215CadRotatedKeeps42Drops32Stub`：System.InvalidOperationException: rotated F215 must keep arm 42; sig=[38,75,100,215]
- P1 `RotationSignature_F338CadGoldenMultisetFourWayEqual`：System.InvalidOperationException: F338Cad @0° Signature must equal golden; got=[87.55,201.5,338] expected=[20,73,87.55,100,201.5,305,338]
- P1 `CadLastRunGrooveSnapsToWallClearWidthFourWay`：System.InvalidOperationException: CAD-last-run @0° must keep foot tip 20; kept=[92.55]
- P1 `RotationSignature_Stair3GoldenMultisetFourWayEqual`：System.InvalidOperationException: Stair3 @0° Signature must equal golden; got=[134.79,134.79,163.37,269.59,429.91,438.8] expected=[126.11,134.79,134.79,163.37,429.91,438.8]
- P1 `RotationSignature_LBoss257GoldenMultisetFourWayEqual`：System.InvalidOperationException: LBoss257 @0° Signature must equal golden; got=[3,3,3,3,10,10,15,18,25,25,25,30,30,30,40,58,66,134,232,232,257] expected=[10,25,25,25,30,40,58,134,257]
- P1 `SteppedPlateReplacesThinStepRisersWithComplement53`：System.InvalidOperationException: 53 (overall min to inner top ledge) must be kept; sig=[50,50,55,102.5]
- P1 `CNotchPlateKeepsTopEnvelopeBodyWidth120`：System.InvalidOperationException: Dimension candidate 13 already has RuleId 'BoundaryInformation.ObliqueProjection' and cannot be reassigned to 'BoundaryInformation.Covered'.
- P1 `StepGroovePlacesOnSameSideAsOuterStepFourWay`：System.InvalidOperationException: F338Cad @0° missing 73; sides=[87.55@Bottom]
- P1 `CrossSideStructureWidthsThatCloseOverallChainAreSuppressed`：System.InvalidOperationException: full-path step length 20 must remain
- P1 `DatumRootedBottomAndLeftOuterEnvelopeDimensionsAreSuppressed`：System.InvalidOperationException: bottom overall width 96 must remain
- P1 `StructureWidthThatPartitionsOverallWithOutlineSegmentIsSuppressed`：System.InvalidOperationException: short structure step 20 must remain (decision A with overall 98)
- P1 `ObliqueBoundaryInformationMatchesUserScheme`：System.InvalidOperationException: Dimension candidate 19 already has RuleId 'BoundaryInformation.ObliqueProjection' and cannot be reassigned to 'BoundaryInformation.Covered'.
- P1 `ObliqueBoundaryInformationProtectsEqualProjections`：System.InvalidOperationException: Dimension candidate 20 already has RuleId 'BoundaryInformation.ObliqueProjection' and cannot be reassigned to 'BoundaryInformation.Covered'.
- P1 `ObliqueBoundaryInformationFallsBackAndAuditsMissingProviders`：System.InvalidOperationException: Dimension candidate 19 already has RuleId 'BoundaryInformation.ObliqueProjection' and cannot be reassigned to 'BoundaryInformation.Covered'.
- P1 `LProfileChamferedArmResidualUsesBoundarySequence`：System.InvalidOperationException: the real short edge must be suppressed by boundary topology, not a fixed numeric value width=32 chamfer=7 rotation=0 mirror=False
- P1 `LProfileAnnotationRulesUseTopology`：System.InvalidOperationException: top 25 replacement must retain an attachment-valid rule diagnostic
- P1 `LProfileFarEndCornerUsesAdjacentVerticalChain`：System.InvalidOperationException: Dimension candidate 19 already has RuleId 'BoundaryInformation.ObliqueProjection' and cannot be reassigned to 'BoundaryInformation.Covered'.
- P1 `LProfileOuterContourAnnotationRulesUseTopology`：System.InvalidOperationException: outer-contour L must diagnose derived inner 52 suppression, case=1
- P1 `BottomProtrusionSuppressesOuterWidthAndInnerLedge`：System.InvalidOperationException: bottom protrusion width 50 on partial MinY must be kept
- P1 `HoleAndPinChainsUseSideFacingCenterlineEndpoints`：System.InvalidOperationException: pin horizontal distances must inherit the datum-pin top side and axis-endpoint chain
- P2 `SlotDimensionsUseCenterAndDatumChainsWithoutPins`：System.InvalidOperationException: horizontal slot datum location should be planned without pins
- P2 `CenterlineEndpointIsAllowedForHolePositioning`：System.InvalidOperationException: hole positioning may use a selected centerline endpoint
- P2 `PinGroupsPlanBaseAndPairDistances`：System.InvalidOperationException: PG1 datum, PG1-to-PG2 transfer, and PG2 direct dimensions must share one rooted datum chain
- P3 `SlotChainAlignmentKeyAlignsEdgeLocationAndCenterDistance`：System.InvalidOperationException: expected vertical slot chain segments (edge location + center distance)
- P3 `RootedLaneChoosesNearestSafeFiniteSegmentCoordinate`：System.InvalidOperationException: the block must choose the nearest candidate that is safe for every finite dimension segment

## 附录 C：每条实际执行命令、退出码与时间

以下均实际执行；完整输出按原字节保存于 process-logs.json，未把返回 1 的测试写成通过。

### initial-build.log（exit=0，3.961s）

```text
dotnet msbuild CadAuto.Core.Tests/CadAuto.Core.Tests.csproj /t:Build /p:Configuration=Debug "/p:OutDir=D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\initial-build/" /v:minimal
```

### target-before.log（exit=1，0.248s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\initial-build\CadAuto.Core.Tests.exe" ConcaveHoleDatumUsesRealOutlineIntersections
```

### gated-before.log（exit=1，0.246s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\initial-build\CadAuto.Core.Tests.exe"
```

### snapshot-before.log（exit=0，0.726s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\initial-build\CadAuto.Core.Tests.exe" --ablation-snapshot "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\snapshots-before.json"
```

### runner-build-before-algorithm.log（exit=0，2.726s）

```text
dotnet msbuild CadAuto.Core.Tests/CadAuto.Core.Tests.csproj /t:Build /p:Configuration=Debug "/p:OutDir=D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\runner-build-before-algorithm/" /v:minimal
```

### detailed-runner-self-check.log（exit=0，0.127s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\runner-build-before-algorithm\CadAuto.Core.Tests.exe" --runner-self-check
```

### gated-before-fix.log（exit=1，0.308s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\runner-build-before-algorithm\CadAuto.Core.Tests.exe" --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\gated-before-fix.json"
```

### full-before-fix.log（exit=1，1.087s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\runner-build-before-algorithm\CadAuto.Core.Tests.exe" --continue-on-failure --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\full-before-fix.json"
```

### runner-only-snapshot.log（exit=0，0.694s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\runner-build-before-algorithm\CadAuto.Core.Tests.exe" --ablation-snapshot "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\runner-only-snapshots.json"
```

### recovery-check-red-build.log（exit=0，1.658s）

```text
dotnet msbuild CadAuto.Core.Tests/CadAuto.Core.Tests.csproj /t:Build /p:Configuration=Debug "/p:OutDir=D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\recovery-check-red-build/" /v:minimal
```

### recovery-check-before.log（exit=1，0.244s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\recovery-check-red-build\CadAuto.Core.Tests.exe" --datum-reference-self-check
```

### fixed-build.log（exit=0，2.567s）

```text
dotnet msbuild CadAuto.Core.Tests/CadAuto.Core.Tests.csproj /t:Build /p:Configuration=Debug "/p:OutDir=D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build/" /v:minimal
```

### target-after.log（exit=0，0.279s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" ConcaveHoleDatumUsesRealOutlineIntersections --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\target-after.json"
```

### datum-reference-after.log（exit=0，0.316s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" --datum-reference-self-check
```

### runner-self-check-after.log（exit=0，0.111s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" --runner-self-check
```

### related-0-NormalHolesLocateFromOutlineDatum.log（exit=0，0.239s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" NormalHolesLocateFromOutlineDatum --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-0.json"
```

### related-1-InvalidDatumCoordinateIsSkippedWithDiagnostic.log（exit=0，0.253s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" InvalidDatumCoordinateIsSkippedWithDiagnostic --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-1.json"
```

### related-2-InvalidSlotDatumIsSkippedWithDiagnostic.log（exit=0，0.243s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" InvalidSlotDatumIsSkippedWithDiagnostic --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-2.json"
```

### related-3-HoleAndPinChainsUseSideFacingCenterlineEndpoints.log（exit=1，0.314s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" HoleAndPinChainsUseSideFacingCenterlineEndpoints --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-3.json"
```

### related-4-PinDatumSideIsInheritedOnAllFourSides.log（exit=0，0.302s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" PinDatumSideIsInheritedOnAllFourSides --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-4.json"
```

### related-5-SeparatePinGroupsKeepIndependentSides.log（exit=0，0.243s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" SeparatePinGroupsKeepIndependentSides --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-5.json"
```

### related-6-HoleGripUsesNearCenterlineStubNotFarAxisEnd.log（exit=0，0.237s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" HoleGripUsesNearCenterlineStubNotFarAxisEnd --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-6.json"
```

### related-7-TopLeftRightHoleGripsUsePlacementSideHoleCenterline.log（exit=0，0.241s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" TopLeftRightHoleGripsUsePlacementSideHoleCenterline --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-7.json"
```

### related-8-ExplicitLocalLooseChainUsesNearbyConcaveBoundary.log（exit=0，0.101s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" ExplicitLocalLooseChainUsesNearbyConcaveBoundary --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-8.json"
```

### related-9-ConcentricLooseHolesShareOneLocationDimension.log（exit=0，0.249s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" ConcentricLooseHolesShareOneLocationDimension --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-9.json"
```

### related-10-OffsetColumnDoesNotStealLowerHoleCenterline.log（exit=0，0.236s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" OffsetColumnDoesNotStealLowerHoleCenterline --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-10.json"
```

### related-11-BottomHoleGripUsesLowerHoleCenterlineInColumn.log（exit=0，0.243s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" BottomHoleGripUsesLowerHoleCenterlineInColumn --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-11.json"
```

### related-12-CenterlineEndpointIsAllowedForHolePositioning.log（exit=1，0.251s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" CenterlineEndpointIsAllowedForHolePositioning --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\related-12.json"
```

### full-after-fix.log（exit=1，1.003s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" --continue-on-failure --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\full-after-fix.json"
```

### gated-after-fix.log（exit=1，0.828s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\gated-after-fix.json"
```

### ablation-rerun.log（exit=1，14.413s）

```text
C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe scripts/run-ablation.py --output "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\ablation-rerun"
```

### ablation-confirmation.log（exit=1，14.358s）

```text
C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe scripts/run-ablation.py --output "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\ablation-confirmation"
```

### graphify-isolated.log（exit=0，13.307s）

```text
cmd /c C:\Users\Administrator\.local\bin\graphify.EXE update . --no-cluster
```

### final-metric-tests.log（exit=0，0.109s）

```text
C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe -m unittest discover -s scripts -p test_ablation_metrics.py
```

### final-runner-self-check.log（exit=0，0.120s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" --runner-self-check
```

### final-datum-reference-self-check.log（exit=0，0.348s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" --datum-reference-self-check
```

### final-ablation-self-check.log（exit=0，1.043s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" --ablation-self-check
```

### final-p0.log（exit=0，0.312s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" P0 --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\p0-after.json"
```

### final-full-diagnostic.log（exit=1，1.046s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" --continue-on-failure --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\full-after-repeat.json"
```

### final-default-gate.log（exit=1，0.868s）

```text
"D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\fixed-build\CadAuto.Core.Tests.exe" --report-json "D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-154719-core-recovery-590b7b\gated-after-repeat.json"
```

### final-git-diff-check.log（exit=0，0.365s）

```text
git diff --check
```
