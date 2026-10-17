# 标注规则消融实验

文档日期：2026-10-16。实验时间戳取执行主机时钟（本轮主机记录为 2026-10-17），不是审批时间。

## 目的与范围

在现有 `CadAuto.Core.Tests` console runner 内复用真实规划器、布局规则和既有输入工厂，测量单条规则的独立影响。不建立替代标注系统，不更改生产命令、原始 DWG、既有人工预期或 `diagnostics/last-run.json`。

参阅：[当前标注规则](当前标注规则.md)、[DimensionRules](DimensionRules.md)、[CoreCadRegression](CoreCadRegression.md)、[布局回归](DimensionLayoutRegression.md)、[中心线规则](不遮挡中心线标注规则.md)。

本轮执行证据索引见 `regression/ablation/evidence/`；每次运行的完整 JSON、Markdown、输入快照和命令日志位于独立的 `regression/ablation/runs/<唯一运行号>/`。`runs/` 被忽略，保留在本机，不自动删除；可携带的兼容性指纹和归档摘要不在该忽略目录内。

**这是 Core 层消融，不是生产 CAD 端到端验收。** 原有 P0 失败门禁保持不变。实验模式独立采集失败、异常和有限契约数据，不能作为绕过门禁编译/部署插件的依据。

## 2026-10-17 执行证据

- [Markdown 报告](../regression/ablation/evidence/20261017-141216-fa77bf2a/REPORT.md)
- [机器结果 JSON](../regression/ablation/evidence/20261017-141216-fa77bf2a/results.json)
- [归档与原始诊断说明](../regression/ablation/evidence/20261017-141216-fa77bf2a/README.md)

执行了 456 个案例/配置/变体观测（另有完整系统重复及自测）；17 个 Python 测量测试、Core runner/实验自测通过；38 组默认行为与改动前及重复运行一致。原有回归仍为 245 注册、7 执行、6 通过、1 失败、238 未执行，和改动前相同。因此总体状态是 `BlockedExistingRegression`，不是项目回归通过。

在 32 个可配对配置输入上，关闭销孔配对增加 28 个契约身份缺失、关闭初始功能孔归属增加 16 个、关闭中心线端点附着增加 48 个已知约束违例、关闭同侧去重增加 1 个重复、关闭原始轮廓整体分割增加 2 个已知禁止项。以上合计两个配置的契约槽，不是全图人工真值指标；没有规则得到足够证据支持移除。

## 代码与数据地图

| 路径 | 职责 |
|---|---|
| `CadAuto.Core/Rules/DimensionExperimentRule.cs` | 非 Flags 的单规则枚举；`None=0` |
| `CadAuto.Core/Rules/DimensionRuleConfig.cs` | 实例级 `DisabledExperimentRule`，默认 None，拒绝未知值，无环境变量/全局开关 |
| `CadAuto.Core.Tests/Ablation/AblationSnapshots.cs` | 现有 Program 的 partial；19 个现有输入工厂、规划/布局诊断、隔离自测 |
| `regression/ablation/dataset.json` | 每例原测试名、显式尺寸身份/夹点/侧向预期、规则入口和适用范围；不是程序输出反推的答案 |
| `scripts/ablation_metrics.py` | 部分契约计数、布局代理、固定配对分母、默认兼容性指纹 |
| `scripts/test_ablation_metrics.py` | 空输出、异常、同值不同特征、错误夹点、重复、错误抑制、数据集/开关一致性等防误判测试 |
| `scripts/run-ablation.py` | Python 标准库驱动现有 Core 工程，独立输出、SHA256、命令/异常归档、JSON/Markdown 报告 |
| `regression/ablation/reference/default-behavior.json` | **改动前的行为兼容性指纹，不是尺寸正确性真值**；测试默认行为未变 |
| `regression/ablation/reference/initial-regression.json` | 修改前实际回归输出及退出码，包含原有 P0 失败 |

### 两个配置必须分开

- `legacy-default`：`CreateDefault()` 的 `UseFeatureFirstStructurePipeline=false`。
- `asd-feature-first`：仅将该字段设为 true，与正常 `ASD/ASD4` 一致；并未模拟 CAD 选择、生产识别器或活动 dimstyle。

两种配置不能合并解释为一个“生产基线”。轮廓案例复用原测试的 `CreateOutlinePlan` 入口；有孔/槽的案例使用 `CreateDimensionPlan`，不是将全部轮廓测试伪称为生产命令全链路。每个配置、案例、实验都重新创建输入对象，避免 post-validator 对包络的修正污染下一次运行。输入图元/孔/槽/基准在规划前序列化并计算 SHA256；所有变体必须与完整系统的同例输入逐字节匹配。

### 已隔离的实际规则

1. 普通孔最近基准孔排序 `ChooseNonPinBaseHole`：仅移除距离排序，保留普通孔优先和 X/Y 稳定排序；不覆盖人工选定的销孔基准。
2. 销孔配对 `CreatePinPairGroup`：关闭后退为单孔组，每个销孔仍存在，后续基准、传递尺寸和校验仍运行。
3. 初始非销孔归属 `AssignNonPinHolesToNearestPinPair`：只关闭此归属入口，不声称关闭了全部功能孔网格识别。
4. 已选中心线附着 `ApplyCenterlineEndpointAttachments`：保留替换前特征夹点；**没有中心线生成消融**，生成中心线本来就被禁止。
5. 镜像去重 `SuppressMirroredDuplicates`。
6. 同侧测量去重 `SuppressDuplicateMeasuredDimensions`。
7. 原始轮廓总体分割 `SuppressOutlineSegmentsThatPartitionOverall`。
8. 局部包络抑制 `SuppressLocalGeometryOnOverallEnvelope`。
9. 边界信息完成 `ApplyBoundaryInformationPlanning`：投影/倒角面补充、恢复、依赖证明和覆盖消费者是一组耦合阶段，整体隔离；独立的最终审计保持启用。不能仅删除提供者而留下依赖其存在的抑制。
10. 共享延伸线对齐 `AlignDimensionsBySharedExtensionLines`。
11. 并行基准栈压缩 `CompactParallelDatumStacks`。

抑制规则在完整规划中可能调用两次；入口开关覆盖两次及全部侧向，不只是第一次。规则依赖和其他抑制阶段保持不动。全部默认路径都是原有路径，仅在显式指定某个枚举时跳过对应入口；不支持组合开关、数字名称或逗号组合。

未隔离：人工基准输入、零长度/真实附着安全校验、其余强顺序依赖的 legacy/LProfile 抑制、CAD 识别和渲染冲突处理。不能把已覆盖的 11 个入口称为全系统全部规则。

## 真值分层

1. `regression/remediation/case-truth-matrix.json` 对 OC01、四向 DL01、HS01–HS08 有确认记录及固定图纸哈希；运行器核对并归档哈希，但没有据历史通过报告宣称本轮 CAD 通过。
2. P0/P1 在项目约定中是产品不变量/用户确认案例，但当前 `Program.cs` 已有大量未提交修改，哈希与历史确认矩阵不符。本轮只称它们为**现有测试契约**，不把每条当前断言自动升级为新的人工确认真值。P2/P3 还应保持未确认标签。
3. `L01-13/expectations.json` 是早期待确认稿；`round1-expectations.json` 后续部分确认禁止顶部 20、右下 15 残差，R5 排除。四条必需尺寸的基准/公差解释仍未全部确认。尚未验证该 DWG 的生产 MainOutline 和特征身份映射，不猜测集成，不改任一份期望。
4. `reference/default-behavior.json` 只验证默认行为与本次改动前一致。**它不能用于计算正确率，也不能替代 dataset 的独立断言。** 普通运行不会更新它；未来输入/默认行为的授权变更需另建、审查新的版本化证据，不能为让实验通过而刷新原指纹。

## 指标含义与局限

每例同时保存完整的 `DimensionPlan.Dimensions`、`DimensionDiagnosticReport` 候选/最终尺寸、真实夹点、来源、RuleId、抑制理由及 Core 布局坐标。

| 指标 | 含义 |
|---|---|
| `contractMissingRequired` | 显式必需尺寸身份缺失的条数，不是所有未知加工要求的漏标数 |
| `contractWrongDimensions` | 已知禁止身份，或现有尺寸违反明确夹点/侧向/附着要求；同一候选只计一次 |
| `contractDuplicateDimensions` | 同一已声明身份超出允许条数，不按尺寸数值去重 |
| `contractIncorrectlySuppressedRequired` | 必需身份缺失，且匹配契约、夹点明确有效（`IsAttachmentValid=true`）的候选被抑制；仅为可证明的检出下界，不把未生成/Skipped 或夹点未知候选猜成“错误删除” |
| `contractSuppressedRequiredAttachmentUnknown` | 必需身份被抑制，但候选夹点有效性未知；单独待确认，不计入已证明的错误删除 |
| `contractCorrectRetained` / `contractRecall` | 身份及全部声明约束均正确的必需项数/必需项总数；不因重复额外加分 |
| `coreDimensionLineOverlapPairs` | 同侧、同尺寸线坐标上的严格跨度重叠对数，共享端点不算严格重叠 |
| `centeredTextConflictProxyPairs` | 使用既有 Core 文字长度估计的居中文字冲突对；不是 CAD 最终文字框检测 |
| `physicalOrderViolations` / `datumFamilySpacingViolations` | 复用已有 Core 物理顺序和基准族间距诊断 |

- 异常例的质量指标为 null，而不是零；全局人工真值指标统一标记 `NotReliablyComputable`。
- 未列入契约的输出是未知，不自动判成错误。暂未可靠测量全图必需尺寸召回、真实 CAD 文字/箭头/延伸线冲突和不同侧碰撞。
- 关闭阶段后避开基线异常，只列入 `recoveredExceptions_NotQualityProof`；质量差值仅在两边均可评估的固定输入子集上计算。不能用变化后的分母宣称质量提升。
- 任何候选/诊断变化均可追溯到变体快照；诊断理由变更不一定改变尺寸实体或布局。报告不以数量下降、通过率或单图视觉效果建议移除规则。
- 时间是每例、每进程的单次冷启动观测，含 JIT 和异常；不是性能优化结论。

## 重复运行

要求 Windows、.NET SDK/MSBuild 能构建现有 .NET Framework 4.7.2 Core 测试工程，以及 Python 3（仅标准库，无 pip 安装）。从项目根执行：

```powershell
python -m unittest discover -s scripts -p test_ablation_metrics.py
python scripts/run-ablation.py
```

指定另一个**不存在**的独立目录：

```powershell
python scripts/run-ablation.py --output 'regression/ablation/runs/manual-unique-run'
```

运行器实际执行并记录：

```text
dotnet --version
dotnet --info
python -m unittest discover -s scripts -p test_ablation_metrics.py
dotnet msbuild CadAuto.Core.Tests/CadAuto.Core.Tests.csproj /t:Build /p:Configuration=Debug /p:OutDir=<本轮绝对路径>/core-build/ /v:minimal
<core-build>/CadAuto.Core.Tests.exe --runner-self-check
<core-build>/CadAuto.Core.Tests.exe --ablation-self-check
<core-build>/CadAuto.Core.Tests.exe --ablation <本轮新快照.json> None
<core-build>/CadAuto.Core.Tests.exe --ablation <本轮新快照.json> <某一规则名称>
# 所有单规则分别运行，最后再运行一次 None 验证无状态泄漏
<core-build>/CadAuto.Core.Tests.exe
```

退出码：

- **0**：基础设施、默认兼容性和原有回归均通过。
- **2**：实验已完成，但原有回归门禁失败；不允许继续生产插件/CAD 验收。
- **1**：基础设施、自测、数据集一致性、保护文件哈希或默认行为兼容性失败。检查日志，不更新期望掩盖失败。

`commands.jsonl` 记录完整参数、退出码、耗时和日志路径；`environment.json` 记录 Git HEAD、分支、dirty 状态、Python/SDK/OS、精确源码哈希；`results.json` 记录每条规则状态、逐例 expected/actual、相对基线差值及真值盘点；`REPORT.md` 给出 Markdown 总结；`snapshots/*.json` 为完整诊断。

输出目录和快照拒绝覆盖；Core 构建只写本轮新目录，不编译/部署插件。运行前后核对原始 DWG、既有期望、诊断报告和源码哈希。既有 PowerShell `run-core-tests.ps1` 在本环境出现空 `Process.ExitCode` 误判，本运行器保留其原文件，直接调用等价的现有 MSBuild 工程和 exe。

## 后续验收门槛

1. 先独立裁决已有 P0 基准夹点失败、FeatureFirst RuleId 重赋异常和阶梯板 53 需求缺口，不在消融任务中擅改预期或产品规则。
2. 补齐有可核验来源的人工必需/禁止尺寸身份、基准和公差；确认当前 P1 工作树与批准版本的差异。
3. 完成 Core 门禁后，按现有 Core-CAD/四向流程在新工作 DWG、副本报告目录和新 `bin/Debug-vN/` 插件构建上复验。不能覆盖用户正在使用的 CAD 进程或诊断。
4. 取得真实 CAD 文字、箭头、延伸线/轮廓碰撞证据，再评价布局收益。几条规则在现有输入中没有独立可观测效果，只说明数据敏感性不足或存在掩盖，不支持移除。
5. 只有单规则证据完整、门禁和真值补齐后，才设计有明确交互假设的组合实验；本轮未运行组合实验。
