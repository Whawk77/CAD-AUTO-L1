# CAD 标注规则单规则消融报告

- 运行：`20261017-171637-06466cfb`；时间：`2026-10-17T09:16:53.107977+00:00`（主机时钟）。
- 代码：`37c16787008014ad907b6b4ff358334bce80e5be`，dirty 工作树；精确源码/数据 SHA256、命令、异常见 results.json。
- 环境：Windows-10-10.0.19045-SP0 / AMD64；.NET SDK 9.0.316；目标 .NET Framework 4.7.2；Python 3.12.10。
- 范围：现有 Core 测试宿主和真实规划/布局代码；19 个原有输入 × 2 配置 ×（完整系统 + 11 单规则消融）。未运行生产 CAD。
- legacy-default 保持 CreateDefault 的 false；asd-feature-first 对齐 ASD 的 true，仅表示配置相同，不代表 CAD 识别/渲染已验证。
- 默认兼容性：**FAILED_DefaultBehaviorDrift**；比较修改前指纹及本轮重复结果（不是正确性真值）。

## 现有回归门禁

注册 249；执行 115；通过 96；失败 19；未执行 134。
- `RotationSignature_F215GoldenMultisetFourWayEqual`：F215 @0�� Signature must equal golden; got=[50,95,100,215] expected=[42,50,75,100,120,215]
- `RotationSignature_F215CadGoldenMultisetFourWayEqual`：F215Cad @0�� Signature must equal golden; got=[42,100,215] expected=[42,50,75,100,120,215]
- `F215CadRotatedKeeps42Drops32Stub`：rotated F215 must keep arm 42; sig=[38,75,100,215]
- `RotationSignature_F338CadGoldenMultisetFourWayEqual`：F338Cad @0�� Signature must equal golden; got=[87.55,201.5,338] expected=[20,73,87.55,100,201.5,305,338]
- `CadLastRunGrooveSnapsToWallClearWidthFourWay`：CAD-last-run @0�� must keep foot tip 20; kept=[92.55]
- `RotationSignature_Stair3GoldenMultisetFourWayEqual`：Stair3 @0�� Signature must equal golden; got=[134.79,134.79,163.37,269.59,429.91,438.8] expected=[126.11,134.79,134.79,163.37,429.91,438.8]
- `RotationSignature_LBoss257GoldenMultisetFourWayEqual`：LBoss257 @0�� Signature must equal golden; got=[3,3,3,3,10,10,15,18,25,25,25,30,30,30,40,58,66,134,232,232,257] expected=[10,25,25,25,30,40,58,134,257]
- `SteppedPlateReplacesThinStepRisersWithComplement53`：53 (overall min to inner top ledge) must be kept; sig=[50,50,55,102.5]
- `CNotchPlateKeepsTopEnvelopeBodyWidth120`：bottom 120 must stay dropped
- `StepGroovePlacesOnSameSideAsOuterStepFourWay`：F338Cad @0�� missing 73; sides=[87.55@Bottom]
- `CrossSideStructureWidthsThatCloseOverallChainAreSuppressed`：full-path step length 20 must remain
- `DatumRootedBottomAndLeftOuterEnvelopeDimensionsAreSuppressed`：bottom overall width 96 must remain
- `StructureWidthThatPartitionsOverallWithOutlineSegmentIsSuppressed`：short structure step 20 must remain (decision A with overall 98)
- `LProfileChamferedArmResidualUsesBoundarySequence`：the real short edge must be suppressed by boundary topology, not a fixed numeric value width=32 chamfer=7 rotation=0 mirror=False
- `LProfileAnnotationRulesUseTopology`：top 25 replacement must retain an attachment-valid rule diagnostic
- `LProfileFarEndCornerUsesAdjacentVerticalChain`：far-end corner L profile must suppress the derived 69 inner edge, case=0
- `LProfileOuterContourAnnotationRulesUseTopology`：outer-contour L must diagnose derived inner 52 suppression, case=1
- `BottomProtrusionSuppressesOuterWidthAndInnerLedge`：bottom protrusion width 50 on partial MinY must be kept
- `HoleAndPinChainsUseSideFacingCenterlineEndpoints`：pin horizontal distances must inherit the datum-pin top side and axis-endpoint chain

## 完整系统基线（部分契约，不是全图真值）

| 配置 | 案例 | 可评估 | 异常 | 契约通过 | 契约失败 | 必需项 | 漏标 | 已知错误 | 重复 | 错误抑制 | 正确保留/召回 | 线重叠/文字代理 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| legacy-default | 19 | 19 | 0 | 19 | 0 | 71 | 0 | 0 | 0 | 0 | 71/1.000 | 0/1 |
| asd-feature-first | 19 | 19 | 0 | 17 | 2 | 81 | 1 | 1 | 0 | 0 | 80/0.988 | 0/1 |

完整系统进程用时 0.717s；38 个输入配置的规划/布局累计 437.773ms、中位数 2.575ms（含异常案例）。
例外案例不记为零缺陷。漏标仅统计显式必需身份缺失；错误包含已知禁止项及已知夹点/侧向违例；召回要求身份及约束都正确。重复是已知身份的超额条数，不按数值去重。

另有 1 个必需身份对应被抑制候选的夹点有效性未知，单列为待确认，不冒充已证明错误删除。

## 单规则比较（固定成对可评估子集）

Δ 正数表示关闭后增加；保留数减少并不等于改善。全部规则各自从完整系统启动，无组合实验。数值合计两个配置的契约槽，不代表独立图纸总量；各配置拆分见 JSON 的 byProfileComparison。

| 关闭规则 | 可配对 | Δ漏标 | Δ错误 | Δ重复 | Δ错误抑制 | Δ召回 | Δ线重叠 | Δ文字代理 | 新异常/恢复异常 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| NonPinNearestDatum | 38 | +0 | +0 | +0 | +0 | +0.000 | +0 | +0 | 0/0 |
| PinPairGrouping | 38 | +28 | +0 | +0 | +0 | -0.184 | +0 | +0 | 0/0 |
| FunctionalHoleAssignment | 38 | +16 | +0 | +0 | +0 | -0.105 | +0 | +0 | 0/0 |
| SelectedCenterlineAttachment | 38 | +0 | +48 | +0 | +0 | -0.316 | +0 | +0 | 0/0 |
| MirroredDeduplication | 38 | +0 | +0 | +0 | +0 | +0.000 | +0 | +0 | 0/0 |
| MeasuredDeduplication | 38 | +0 | +0 | +1 | +0 | +0.000 | +0 | +0 | 0/0 |
| OutlineOverallPartition | 38 | +0 | +2 | +0 | +0 | +0.000 | +0 | +0 | 0/0 |
| LocalEnvelopeSuppression | 38 | +0 | +0 | +0 | +0 | +0.000 | +0 | +0 | 0/0 |
| BoundaryInformationCompletion | 38 | +4 | +0 | +0 | +0 | -0.026 | +0 | +0 | 0/0 |
| SharedExtensionAlignment | 38 | +0 | +0 | +0 | +0 | +0.000 | +0 | +0 | 0/0 |
| ParallelDatumCompaction | 38 | +0 | +0 | +0 | +0 | +0.000 | +0 | +0 | 0/0 |

## 逐条结论与证据

### NonPinNearestDatum

进一步验证；当前可配对输入没有证明其独立必要性，不支持移除。
- 隔离入口：`DimensionPlanner.Features.cs:ChooseNonPinBaseHole`；Disable nearest-distance ranking only; retain normal-before-thread preference and deterministic X/Y ordering. Never override a selected datum pin.
- 默认契约从通过变失败：无。
- 候选、尺寸或布局证据有变化：无。
- 本版本用时：进程 0.699s；规划/布局累计 423.414ms、中位数 2.671ms（单次冷启动，含异常）。
- 本变体绝对结果（非配对差值）：可评估 38/38、异常 0；漏标 1、已知错误 1、重复 0、错误抑制 0。恢复异常案例没有可靠的原质量分数，不得据此计算改善。
- 新异常：无；避开原异常：无。
- 配对 Δ尺寸总数 +0，Δ正确保留 +0；不可单独据尺寸数或通过率判断布局。
- 原始证据：`snapshots/NonPinNearestDatum.json`；逐例 expected/actual、候选 ID、真实夹点、抑制理由、布局冲突对见 results.json 的 variants/cases。

### PinPairGrouping

暂保留；关闭导致已知契约回归，但仍须人工真值和 CAD 验证。
- 隔离入口：`DimensionPlanner.Features.cs:CreatePinPairGroup`；Singleton pin groups; retain every pin, chosen datum, group transfers, functional processing and validators.
- 默认契约从通过变失败：legacy-default/pin-family-v, legacy-default/pin-family-v-reverse, legacy-default/pin-family-h, legacy-default/pin-family-h-reverse, legacy-default/separate-pin-groups, asd-feature-first/pin-family-v, asd-feature-first/pin-family-v-reverse, asd-feature-first/pin-family-h, asd-feature-first/pin-family-h-reverse, asd-feature-first/separate-pin-groups。
- 候选、尺寸或布局证据有变化：legacy-default/pin-family-v, legacy-default/pin-family-v-reverse, legacy-default/pin-family-h, legacy-default/pin-family-h-reverse, legacy-default/separate-pin-groups, legacy-default/selected-pin-grid, asd-feature-first/pin-family-v, asd-feature-first/pin-family-v-reverse, asd-feature-first/pin-family-h, asd-feature-first/pin-family-h-reverse, asd-feature-first/separate-pin-groups, asd-feature-first/selected-pin-grid。
- 本版本用时：进程 0.724s；规划/布局累计 443.289ms、中位数 2.968ms（单次冷启动，含异常）。
- 本变体绝对结果（非配对差值）：可评估 38/38、异常 0；漏标 29、已知错误 1、重复 0、错误抑制 0。恢复异常案例没有可靠的原质量分数，不得据此计算改善。
- 新异常：无；避开原异常：无。
- 配对 Δ尺寸总数 +4，Δ正确保留 -28；不可单独据尺寸数或通过率判断布局。
- 原始证据：`snapshots/PinPairGrouping.json`；逐例 expected/actual、候选 ID、真实夹点、抑制理由、布局冲突对见 results.json 的 variants/cases。

### FunctionalHoleAssignment

暂保留；关闭导致已知契约回归，但仍须人工真值和 CAD 验证。
- 隔离入口：`DimensionPlanner.Features.cs:AssignNonPinHolesToNearestPinPair`；Skip initial non-pin assignment; downstream functional-grid and loose-hole rules remain active. Not an ablation of ALL functional-hole recognition.
- 默认契约从通过变失败：legacy-default/pin-family-v, legacy-default/pin-family-v-reverse, legacy-default/pin-family-h, legacy-default/pin-family-h-reverse, asd-feature-first/pin-family-v, asd-feature-first/pin-family-v-reverse, asd-feature-first/pin-family-h, asd-feature-first/pin-family-h-reverse。
- 候选、尺寸或布局证据有变化：legacy-default/pin-family-v, legacy-default/pin-family-v-reverse, legacy-default/pin-family-h, legacy-default/pin-family-h-reverse, asd-feature-first/pin-family-v, asd-feature-first/pin-family-v-reverse, asd-feature-first/pin-family-h, asd-feature-first/pin-family-h-reverse。
- 本版本用时：进程 0.721s；规划/布局累计 439.692ms、中位数 2.899ms（单次冷启动，含异常）。
- 本变体绝对结果（非配对差值）：可评估 38/38、异常 0；漏标 17、已知错误 1、重复 0、错误抑制 0。恢复异常案例没有可靠的原质量分数，不得据此计算改善。
- 新异常：无；避开原异常：无。
- 配对 Δ尺寸总数 +4，Δ正确保留 -16；不可单独据尺寸数或通过率判断布局。
- 原始证据：`snapshots/FunctionalHoleAssignment.json`；逐例 expected/actual、候选 ID、真实夹点、抑制理由、布局冲突对见 results.json 的 variants/cases。

### SelectedCenterlineAttachment

暂保留；关闭导致已知契约回归，但仍须人工真值和 CAD 验证。
- 隔离入口：`DimensionPlanner.cs:ApplyCenterlineEndpointAttachments`；Keep pre-attachment real feature grips; never generate centerlines.
- 默认契约从通过变失败：legacy-default/pin-family-v, legacy-default/pin-family-v-reverse, legacy-default/pin-family-h, legacy-default/pin-family-h-reverse, legacy-default/normal-two, legacy-default/normal-grid, legacy-default/selected-pin-grid, asd-feature-first/pin-family-v, asd-feature-first/pin-family-v-reverse, asd-feature-first/pin-family-h, asd-feature-first/pin-family-h-reverse, asd-feature-first/normal-two, asd-feature-first/normal-grid, asd-feature-first/selected-pin-grid。
- 候选、尺寸或布局证据有变化：legacy-default/concave-datum, legacy-default/pin-family-v, legacy-default/pin-family-v-reverse, legacy-default/pin-family-h, legacy-default/pin-family-h-reverse, legacy-default/separate-pin-groups, legacy-default/normal-two, legacy-default/normal-grid, legacy-default/selected-pin-grid, legacy-default/slots-without-pins, asd-feature-first/concave-datum, asd-feature-first/pin-family-v, asd-feature-first/pin-family-v-reverse, asd-feature-first/pin-family-h, asd-feature-first/pin-family-h-reverse, asd-feature-first/separate-pin-groups, asd-feature-first/normal-two, asd-feature-first/normal-grid, asd-feature-first/selected-pin-grid, asd-feature-first/slots-without-pins。
- 本版本用时：进程 0.704s；规划/布局累计 426.714ms、中位数 3.270ms（单次冷启动，含异常）。
- 本变体绝对结果（非配对差值）：可评估 38/38、异常 0；漏标 1、已知错误 49、重复 0、错误抑制 0。恢复异常案例没有可靠的原质量分数，不得据此计算改善。
- 新异常：无；避开原异常：无。
- 配对 Δ尺寸总数 +0，Δ正确保留 -48；不可单独据尺寸数或通过率判断布局。
- 原始证据：`snapshots/SelectedCenterlineAttachment.json`；逐例 expected/actual、候选 ID、真实夹点、抑制理由、布局冲突对见 results.json 的 variants/cases。

### MirroredDeduplication

进一步验证；当前可配对输入没有证明其独立必要性，不支持移除。
- 隔离入口：`DimensionPlanner.Suppression.cs:SuppressMirroredDuplicates`；Skip only this pass on both axes and both planner invocations; other duplicate/coverage rules remain.
- 默认契约从通过变失败：无。
- 候选、尺寸或布局证据有变化：legacy-default/outer-step, legacy-default/stepped-plate, legacy-default/c-notch, legacy-default/oblique-270, asd-feature-first/c-notch。
- 本版本用时：进程 0.719s；规划/布局累计 436.431ms、中位数 2.597ms（单次冷启动，含异常）。
- 本变体绝对结果（非配对差值）：可评估 38/38、异常 0；漏标 1、已知错误 1、重复 0、错误抑制 0。恢复异常案例没有可靠的原质量分数，不得据此计算改善。
- 新异常：无；避开原异常：无。
- 配对 Δ尺寸总数 +3，Δ正确保留 +0；不可单独据尺寸数或通过率判断布局。
- 原始证据：`snapshots/MirroredDeduplication.json`；逐例 expected/actual、候选 ID、真实夹点、抑制理由、布局冲突对见 results.json 的 variants/cases。

### MeasuredDeduplication

暂保留；关闭导致已知契约回归，但仍须人工真值和 CAD 验证。
- 隔离入口：`DimensionPlanner.Suppression.cs:SuppressDuplicateMeasuredDimensions`；Skip only same-side measured duplicate pass on all four sides and both planner invocations.
- 默认契约从通过变失败：legacy-default/duplicate-segments。
- 候选、尺寸或布局证据有变化：legacy-default/duplicate-segments, legacy-default/concave-datum, legacy-default/oblique-90, asd-feature-first/concave-datum, asd-feature-first/outer-step, asd-feature-first/c-notch, asd-feature-first/oblique-0, asd-feature-first/oblique-90, asd-feature-first/oblique-180, asd-feature-first/oblique-270。
- 本版本用时：进程 0.721s；规划/布局累计 440.492ms、中位数 2.741ms（单次冷启动，含异常）。
- 本变体绝对结果（非配对差值）：可评估 38/38、异常 0；漏标 1、已知错误 1、重复 1、错误抑制 0。恢复异常案例没有可靠的原质量分数，不得据此计算改善。
- 新异常：无；避开原异常：无。
- 配对 Δ尺寸总数 +4，Δ正确保留 +0；不可单独据尺寸数或通过率判断布局。
- 原始证据：`snapshots/MeasuredDeduplication.json`；逐例 expected/actual、候选 ID、真实夹点、抑制理由、布局冲突对见 results.json 的 variants/cases。

### OutlineOverallPartition

暂保留；关闭导致已知契约回归，但仍须人工真值和 CAD 验证。
- 隔离入口：`DimensionPlanner.Suppression.cs:SuppressOutlineSegmentsThatPartitionOverall`；Skip raw OutlineSegment overall partition pass; structure partition, envelope and validation rules remain.
- 默认契约从通过变失败：无。
- 候选、尺寸或布局证据有变化：legacy-default/concave-datum, legacy-default/stepped-plate, asd-feature-first/stepped-plate。
- 本版本用时：进程 0.703s；规划/布局累计 423.601ms、中位数 2.542ms（单次冷启动，含异常）。
- 本变体绝对结果（非配对差值）：可评估 38/38、异常 0；漏标 1、已知错误 3、重复 0、错误抑制 0。恢复异常案例没有可靠的原质量分数，不得据此计算改善。
- 新异常：无；避开原异常：无。
- 配对 Δ尺寸总数 +5，Δ正确保留 +0；不可单独据尺寸数或通过率判断布局。
- 原始证据：`snapshots/OutlineOverallPartition.json`；逐例 expected/actual、候选 ID、真实夹点、抑制理由、布局冲突对见 results.json 的 variants/cases。

### LocalEnvelopeSuppression

进一步验证；当前可配对输入没有证明其独立必要性，不支持移除。
- 隔离入口：`DimensionPlanner.Suppression.cs:SuppressLocalGeometryOnOverallEnvelope`；Skip deferred envelope snapshot suppression; other envelope cleanup remains.
- 默认契约从通过变失败：无。
- 候选、尺寸或布局证据有变化：legacy-default/rectangle, legacy-default/duplicate-segments, legacy-default/stepped-plate, legacy-default/c-notch, legacy-default/pin-family-v, legacy-default/pin-family-v-reverse, legacy-default/pin-family-h, legacy-default/pin-family-h-reverse, legacy-default/separate-pin-groups, legacy-default/normal-two, legacy-default/normal-grid, legacy-default/selected-pin-grid, legacy-default/slots-without-pins。
- 本版本用时：进程 0.699s；规划/布局累计 427.551ms、中位数 2.652ms（单次冷启动，含异常）。
- 本变体绝对结果（非配对差值）：可评估 38/38、异常 0；漏标 1、已知错误 1、重复 0、错误抑制 0。恢复异常案例没有可靠的原质量分数，不得据此计算改善。
- 新异常：无；避开原异常：无。
- 配对 Δ尺寸总数 +0，Δ正确保留 +0；不可单独据尺寸数或通过率判断布局。
- 原始证据：`snapshots/LocalEnvelopeSuppression.json`；逐例 expected/actual、候选 ID、真实夹点、抑制理由、布局冲突对见 results.json 的 variants/cases。

### BoundaryInformationCompletion

暂保留；关闭导致已知契约回归，但仍须人工真值和 CAD 验证。
- 隔离入口：`DimensionPlanner.BoundaryInformation.cs:ApplyBoundaryInformationPlanning`；Skip this coupled projection/chamfer completion, restoration and proof-based coverage stage. Do not split provider generation from its coverage consumers. Separate final audit remains enabled.
- 默认契约从通过变失败：asd-feature-first/oblique-0。
- 候选、尺寸或布局证据有变化：asd-feature-first/outer-step, asd-feature-first/c-notch, asd-feature-first/oblique-0, asd-feature-first/oblique-90, asd-feature-first/oblique-180, asd-feature-first/oblique-270。
- 本版本用时：进程 0.685s；规划/布局累计 416.014ms、中位数 2.760ms（单次冷启动，含异常）。
- 本变体绝对结果（非配对差值）：可评估 38/38、异常 0；漏标 5、已知错误 1、重复 0、错误抑制 0。恢复异常案例没有可靠的原质量分数，不得据此计算改善。
- 新异常：无；避开原异常：无。
- 配对 Δ尺寸总数 -3，Δ正确保留 -4；不可单独据尺寸数或通过率判断布局。
- 原始证据：`snapshots/BoundaryInformationCompletion.json`；逐例 expected/actual、候选 ID、真实夹点、抑制理由、布局冲突对见 results.json 的 variants/cases。

### SharedExtensionAlignment

进一步验证；当前可配对输入没有证明其独立必要性，不支持移除。
- 隔离入口：`DimensionLayoutRules.cs:AlignDimensionsBySharedExtensionLines`；Skip shared-extension alignment only; rooted lanes, stacking, conflict checks and final ordering remain.
- 默认契约从通过变失败：无。
- 候选、尺寸或布局证据有变化：无。
- 本版本用时：进程 0.692s；规划/布局累计 419.234ms、中位数 2.706ms（单次冷启动，含异常）。
- 本变体绝对结果（非配对差值）：可评估 38/38、异常 0；漏标 1、已知错误 1、重复 0、错误抑制 0。恢复异常案例没有可靠的原质量分数，不得据此计算改善。
- 新异常：无；避开原异常：无。
- 配对 Δ尺寸总数 +0，Δ正确保留 +0；不可单独据尺寸数或通过率判断布局。
- 原始证据：`snapshots/SharedExtensionAlignment.json`；逐例 expected/actual、候选 ID、真实夹点、抑制理由、布局冲突对见 results.json 的 variants/cases。

### ParallelDatumCompaction

进一步验证；当前可配对输入没有证明其独立必要性，不支持移除。
- 隔离入口：`DimensionLayoutRules.DatumStacks.cs:CompactParallelDatumStacks`；Skip final safe compaction only; grouping, spacing, contour and physical-order checks remain.
- 默认契约从通过变失败：无。
- 候选、尺寸或布局证据有变化：legacy-default/pin-family-v, legacy-default/pin-family-v-reverse, legacy-default/pin-family-h, legacy-default/pin-family-h-reverse, asd-feature-first/pin-family-v, asd-feature-first/pin-family-v-reverse, asd-feature-first/pin-family-h, asd-feature-first/pin-family-h-reverse。
- 本版本用时：进程 0.696s；规划/布局累计 420.518ms、中位数 2.844ms（单次冷启动，含异常）。
- 本变体绝对结果（非配对差值）：可评估 38/38、异常 0；漏标 1、已知错误 1、重复 0、错误抑制 0。恢复异常案例没有可靠的原质量分数，不得据此计算改善。
- 新异常：无；避开原异常：无。
- 配对 Δ尺寸总数 +0，Δ正确保留 +0；不可单独据尺寸数或通过率判断布局。
- 原始证据：`snapshots/ParallelDatumCompaction.json`；逐例 expected/actual、候选 ID、真实夹点、抑制理由、布局冲突对见 results.json 的 variants/cases。

## 默认失败及证据索引

- `asd-feature-first/outer-step`：契约违例：horizontal-body-70；完整证据见 results.json。
- `asd-feature-first/stepped-plate`：契约违例：left-complement-53；完整证据见 results.json。

## 真值、依赖和未评估边界

- 历史矩阵将 P0/P1 标记为确认，但当前源码哈希不匹配；本轮仅称“现有测试契约”，不提升为当前图纸人工真值。OC01/DL01/HS 等 DWG/期望哈希及历史标签已盘点；未据旧报告宣称本轮通过。
- L01-13：旧 expectations.json 待确认；round1-expectations.json 已确认禁止顶部 20、右下 15，四条必需尺寸的基准/公差仍待确认。未猜测其 MainOutline 或诊断身份，未纳入本轮 Core 消融。
- 全图必须尺寸漏标、错误尺寸、错误删除、正确保留率、实际文字/箭头/延伸线冲突均为 **null/NotReliablyComputable**。目前只有部分契约计数和 Core 布局代理。未知输出不算错；异常不算零。
- 线重叠来自 Core 同侧同坐标严格区间相交；文字代理复用 Core 文字长度模型，未执行 CAD 文字滑移/实际字框测量，不是最终渲染碰撞率。不同侧碰撞、延伸线/轮廓冲突尚未测量。
- 本轮运行时间为冷启动单次观测，包含 JIT；JSON 记录每例毫秒及进程时间，不作统计显著性或性能收益结论。
- 镜像、同侧去重、包络和整体分割可能互相掩盖；组识别会改变后续公差/传递链/功能孔所有权；完成阶段的尺寸提供者与覆盖消费者必须一起隔离。无变化只说明本数据未识别独立影响。
- 建议修改/排查：既有 P0 凹轮廓基准失败、FeatureFirst RuleId 重赋异常、阶梯板 53 必需结构尺寸、PowerShell 包装器空 ExitCode；本任务没有擅改这些产品行为或人工预期。
- 建议暂保留（关闭有契约回归）：PinPairGrouping, FunctionalHoleAssignment, SelectedCenterlineAttachment, MeasuredDeduplication, OutlineOverallPartition, BoundaryInformationCompletion。
- 建议修复/排查后再验证：。
- 建议进一步验证：NonPinNearestDatum, MirroredDeduplication, LocalEnvelopeSuppression, SharedExtensionAlignment, ParallelDatumCompaction。没有规则得到足够证据支持移除。

- 未评估 `CenterlineGeneration`：Explicitly prohibited by AGENTS.md; no such production generation rule may be introduced.
- 未评估 `SelectedPinDatumAndManualBase`：User-selected datum is an input contract, not an optional heuristic. No ablation may silently override it.
- 未评估 `ZeroLengthRealAttachmentAndPostValidation`：Safety/product invariants remain active; disabling them could manufacture invalid annotation results.
- 未评估 `RemainingLegacyAndLProfileSuppressionPasses`：Multiple ordered candidate/provider dependencies; not independently isolated in this initial suite. No wholesale pipeline deletion.
- 未评估 `CadRecognitionRenderingAndCollisionResolution`：Core P0 gate fails before changes. Existing GUI AutoCAD is open; no plugin build, CAD launch, last-run overwrite or real rendered text/arrow/extension-line measurement performed.

## 重复运行

```powershell
cd 'D:\work\AI\9.1 托压块版本'
python -m unittest discover -s scripts -p test_ablation_metrics.py
python scripts/run-ablation.py
```

每次创建全新运行目录，拒绝覆盖已有目录/快照；原始 DWG、期望和 diagnostics/last-run.json 运行前后均校验哈希。
Core 回归仍红时脚本完成实验并返回 2；基础设施/默认兼容性失败返回 1。返回 0 才表示所有门禁通过，绝不以“实验完成”替代回归通过。

- 运行产物：`D:\work\AI\9.1 托压块版本\regression\ablation\runs\20261017-171412-coverage-rule-4c9062\ablation-after`
- 机器结果：`results.json`；逐条命令/退出码：`commands.jsonl`；完整诊断：`snapshots/*.json`。
