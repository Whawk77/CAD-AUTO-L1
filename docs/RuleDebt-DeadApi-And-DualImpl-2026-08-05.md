# 规则债务：死 API / 双实现对照表

> 日期：2026-08-05（S1 实施同步更新）  
> 分支：`8.5`  
> 仓库：`D:\work\AI\grok\cad\CAD-AUTO-L1`  
> 状态：债务登记 + **S1 零行为清理已实施**（见 §7）；仍不授权删抑制 pass、不授权改 expectation、不含 §4.1  
> 范围：插件规则层（`CadAuto.Core/Rules`、`DimensionPlanner` 抑制/结构、相关文档符号、镜像去重双通道）  
> 方法：全仓库 `*.cs` 符号引用扫描 + 人工对照定义/调用点；知识图谱辅助导航（`graphify-out`）

---

## 1. 目的

把先前规则评审中的两类可核债务落成可追溯清单：

1. **死 API**：有定义、生产路径与测试均无外部调用（或仅自引用、入口不可达）。
2. **双实现 / 同义多路径**：同名或同意图的逻辑在两处及以上实现，或分层各判一次。

本文件是后续清理的**准入清单**，不是实施计划。任何删除/合并必须另开任务，并满足 `AGENTS.md` / 真值矩阵约束。

---

## 2. 状态标签

| 标签 | 含义 |
| --- | --- |
| `DEAD` | 全仓库除定义文件外无引用；或定义簇内自引用但无外部入口 |
| `DEAD-DOC` | 仅出现在文档，代码中无符号 |
| `DUAL` | 两处及以上实现，语义重叠 |
| `DUAL-LAYER` | 计划层与渲染层各有一套同意图逻辑 |
| `QUAD` | 四向复制（Top/Bottom/Left/Right） |
| `LIVE` | 有生产调用，登记仅为对照基线 |
| `REMOVED-S1` | 已在 S1 零行为清理中删除或文档正名（2026-08-05） |

风险等级：

| 等级 | 含义 |
| --- | --- |
| R0 | 文档/注释漂移，删改不影响运行 |
| R1 | 死代码删除风险低，但仍需编译与 Core 全量 |
| R2 | 双实现合并可能改变行为，需定向回归 |
| R3 | 与抑制顺序/镜像/布局交织，动则需 case 真值 |

---

## 3. 死 API 对照表

扫描规则：对符号名做 `\bName\b` 计数；`total=1` 且仅定义文件 → `DEAD`。  
簇内方法可被同文件其他死方法调用，整体仍记 `DEAD`（入口不可达）。

### 3.1 `StructureSuppressionRules` — 台阶有用性 / 冗余簇

| 符号 | 原定义（历史） | 引用结论 | 状态 | 风险 | 备注 |
| --- | --- | --- | --- | --- | --- |
| `IsUsefulHorizontalStep` | 原 `StructureSuppressionRules.cs` | 仅定义 | `REMOVED-S1` | R1 | S1 直接删除 |
| `IsUsefulVerticalStep` | 同文件 | 仅定义 | `REMOVED-S1` | R1 | |
| `IsFeatureRedundantStepDimension` | 同文件 | 簇入口 | `REMOVED-S1` | R1 | |
| `IsRedundantChamferStepDimension` | 同文件 | 仅被簇入口调用 | `REMOVED-S1` | R1 | |
| `IsRedundantFilletStepDimension` | 同文件 | 仅被簇入口调用 | `REMOVED-S1` | R1 | |
| `IsSingleTangentFilletStepDimension` | 同文件 | 仅被 fillet 冗余调用 | `REMOVED-S1` | R1 | |
| `IsSmallFeatureAdjacentDimension` | 同文件 | 仅被 chamfer/fillet 冗余调用 | `REMOVED-S1` | R1 | |
| `StepHeightExtensionCrossesOutlineInterior` | 同文件 | 仅定义 | `REMOVED-S1` | R1 | P2 级联删 `HorizontalProbeCrossesInterior` / `GetProbeSampleXs` |
| `ShouldIgnoreDirectionalInclinedEndpoint` | 同文件 | 仅定义 | `REMOVED-S1` | R1 | **保留** `GetDirectionalInclinedIgnoreReason` |
| `IsBetweenTwoChamfersAndDerivedFromOverall` | private 级联 | 仅服务死簇 | `REMOVED-S1` | R1 | 连带 `FindChamferTouchingPoint` / `GetChamferProjectionX|Y` |

**存活对照（同文件，勿当死代码）：**

| 符号 | 生产调用方（示例） |
| --- | --- |
| `GetDirectionalInclinedIgnoreReason` | `DimensionPlanner.StructureGeometry` 包装转发 |
| `ShouldIgnoreSideDirectionalInclinedEndpoint` | StructureGeometry |
| `IsEnvelopeSidePoint` / `IsEnvelopeHorizontalSidePoint` | StructureEndpointRules / StructureGeometry |
| `IsFortyFiveDegreeSegment` / 内槽相关 | StructureGeometry / StructureEndpointRules |
| `IsPointInsideOutlineByRayCast` / `IsPointOnSegment` | DimensionLayoutRules / DimensionPlanner.Structure |

### 3.2 `StructureSuppressionRules` — 四向延伸线 public 版（死）

| 符号 | 定义 | 引用结论 | 状态 | 风险 | 备注 |
| --- | --- | --- | --- | --- | --- |
| `LeftExtensionCrossesOutline` | `StructureSuppressionRules.cs:82` | 无外部调用 | `DEAD` + 见 §4.1 | R2 | 签名含 `firstDimensionOffset` |
| `RightExtensionCrossesOutline` | `:105` | 无外部调用 | `DEAD` + 见 §4.1 | R2 | |
| `TopExtensionCrossesOutline` | `:128` | 无外部调用 | `DEAD` + 见 §4.1 | R2 | |
| `BottomExtensionCrossesOutline` | `:151` | 无外部调用 | `DEAD` + 见 §4.1 | R2 | |

> 说明：生产实际使用的是 `DimensionPlanner.StructureGeometry` 的 **private 同名方法**（见 §4.1）。Rules 侧四方法当前是「公开但无调用」的双实现半边。

### 3.3 `DimensionDeduplicationRules` — 对称半边

| 符号 | 定义 | 引用结论 | 状态 | 风险 | 备注 |
| --- | --- | --- | --- | --- | --- |
| `IsRightStructureHeightCoveredByLeft` | 原 `DimensionDeduplicationRules.cs` | 仅定义 | `REMOVED-S1` | R1 | S1 已删；产品仍仅单向 |
| `IsLeftStructureHeightCoveredByRight` | 现文件 | `DimensionPlanner.Suppression` 调用 | `LIVE` | — | 产品仅单向策略 |

### 3.4 文档幽灵符号

| 符号 | 文档位置 | 代码 | 状态 | 风险 |
| --- | --- | --- | --- | --- |
| `DrawTopStepWidth` | 原 `docs/DimensionRules.md` | 无 | `REMOVED-S1` | R0 | 已正名为 `TopStructWidth` + Structure 入口锚点 |
| `DrawLowerRightStepWidth` | 同上 | 无 | `REMOVED-S1` | R0 | → `BottomStructWidth` |
| `DrawRightStepHeight` | 同上 | 无 | `REMOVED-S1` | R0 | → `LeftStructHeight` |
| `DrawRightSideStepHeight` | 同上 | 无 | `REMOVED-S1` | R0 | → `RightStructHeight` |

### 3.5 明确不是死 API（对照，防误删）

| 符号 | 生产用途摘要 |
| --- | --- |
| `FormsCompleteOverallPartition*` | Suppression / Structure 高频 |
| `FindCompleteOverallPartitionChain` | Suppression + StructureEndpointRules |
| `CanSuppressMirroredNormalDimension` | DimensionDrawer |
| `CanSuppressMirroredHoleRelatedDimension` | DimensionDrawer |
| `CreateStackingPlan` / Layout 大面 API | DimensionDrawer + Core.Tests |
| `SlotArcRules` / `ThreadArcRules` | 识别/采集路径 |

---

## 4. 双实现对照表

### 4.1 延伸线穿轮廓（同名双实现）— `DUAL` / R2

| 侧 | 实现 A（**生产在用**） | 实现 B（**当前无调用**） | 差异要点 |
| --- | --- | --- | --- |
| Left | `DimensionPlanner.StructureGeometry.cs:196` private | `StructureSuppressionRules.cs:82` public | A 固定 `_config.FirstDimOffset`；B 参数 `firstDimensionOffset` |
| Right | StructureGeometry `:219` | Rules `:105` | 同上 |
| Top | StructureGeometry `:438` | Rules `:128` | 同上 |
| Bottom | StructureGeometry `:173` | Rules `:151` | 同上 |

**调用方（A）：** StructureGeometry 在收集 ignored structure points 时调用（约 `:54`–`:83`、`:287`），Reason 字符串与方法同名。

**合并建议（仅债务，不实施）：**

1. 确认 A/B 算法是否字节级等价（当前阅读：主体循环与相交判断同构，offset 来源不同）。
2. 若等价：让 A 委托 B（传入 `_config.FirstDimOffset`），再删 private 副本。
3. 回归：OC01 / 结构台阶相关 Core 用例 + 任一 bottom/left outer-step CAD case（若 fixtureReady）。

### 4.2 镜像抑制双通道 — `DUAL-LAYER` / R3

| 层 | 入口 | 谓词 | 适配对象 |
| --- | --- | --- | --- |
| 计划抑制 | `DimensionPlanner.Suppression.SuppressMirroredDuplicates` `:2349` | 私有 `CanSuppressMirroredDimension` `:2402` + `IsSameMeasuredDimension` | `PlannedDimension` |
| 渲染延迟 | `DimensionDrawer`（约 `:252`–`:302`） | `DimensionDeduplicationRules.CanSuppressMirroredNormalDimension` / `CanSuppressMirroredHoleRelatedDimension` | `DeferredDim` → `DimensionDeduplicationItem` |

| 对照项 | 说明 |
| --- | --- |
| 意图 | 去掉镜像侧重复线性尺寸 |
| 风险 | 同一测量可能被两层各判一次；条件不完全共享同一函数 |
| 清理方向 | 统一谓词在 DeduplicationRules；Planner/Drawer 只做 DTO 映射；**禁止**在未对照诊断报告前删除任一层 |

### 4.3 斜向忽略：包装 vs 死入口 — `DUAL`（半） / R1–R2

| 角色 | 位置 | 状态 |
| --- | --- | --- |
| 真源 | `StructureSuppressionRules.GetDirectionalInclinedIgnoreReason` `:377` | `LIVE`（经 Planner 转发） |
| 包装 | `DimensionPlanner.StructureGeometry.GetDirectionalInclinedIgnoreReason` `:539` | `LIVE` 转发 |
| 死旁路 | `StructureSuppressionRules.ShouldIgnoreDirectionalInclinedEndpoint` `:372` | `DEAD`（bool 包装，无人调） |
| 存活旁路 | `ShouldIgnoreSideDirectionalInclinedEndpoint` | `LIVE` |

### 4.4 结构点四向复制 — `QUAD` / R2

`CadAuto.Core/Rules/StructureEndpointRules.cs`：

| 族 | Top | Bottom | Left | Right |
| --- | --- | --- | --- | --- |
| Current structure point | `:448` `IsCurrentTopSideStructurePoint` | `:428` Bottom | `:488` Left | `:468` Right |
| Side candidate | `:413` `IsTopSideHorizontalStructureCandidate` | `:406` Bottom | `:423` Left | `:418` Right |

| 对照项 | 说明 |
| --- | --- |
| 意图 | 分侧结构端点/候选筛选 |
| 重复形态 | 轴与包络边界不同，主体模式同构 |
| 清理方向 | 参数化 `DimensionSide` 单实现；**高回归成本**，需四向 DL01 + 结构 case |

### 4.5 模型双栈（Adapter vs Core）— `DUAL-LAYER` / R3（边界，非规则死码）

| 概念 | Adapter / 宿主 | Core |
| --- | --- | --- |
| 轮廓 | `OutlineFeature` | `OutlineFeature2D` |
| 孔 | `HoleFeature` | `HoleFeature2D` |
| 边 | `DimSide` | `DimensionSide` |
| 识别 | `FeatureRecognizer` | `FeatureRecognizer2D`（偏测试/Core） |

| 对照项 | 说明 |
| --- | --- |
| 是否规则重复 | 多数为边界映射，非两套产品规则 |
| 读代码成本 | 高：易误判「两套规则」 |
| 清理方向 | 映射层文档化；不在本债务表做合并 |

### 4.6 布局区间工具碎片 — `DUAL`（微） / R1

均在 `DimensionLayoutRules.cs` 内部：

| 工具 | 用途倾向 |
| --- | --- |
| `IntervalOverlap` | 标量重叠长度 |
| `IntervalsOverlap` | 带容差布尔重叠 |
| `AreCompatible` | 文本区间是否可同层 |
| `TextBoundsOverlap` / `TextBoundsAreTooClose` / `TextBoundsHasHardOverlap` | 二维文本框 |

属同一文件内命名分裂，**不是跨层双实现**；合并收益低、误改布局风险中。

### 4.7 抑制 pass 同意图族（结构性重复，非同函数）— R3

权威流水线：`DimensionPlanner.Suppression.SuppressDuplicateDimensions`（源码注释：约 27 pass，完整计划跑两轮）。

同意图族示例（登记，不展开删改）：

| 意图族 | 代表方法前缀 |
| --- | --- |
| overall partition | `SuppressStructureDimensionsThatPartitionOverall` / `SuppressProjected*` / `SuppressOutlineSegmentsThatPartitionOverall` |
| complementary remainder | `SuppressComplementaryOutlineRemainders` / closed chain 相关 |
| measured duplicate | `SuppressDuplicateMeasuredDimensions`（四边） |
| mirror | `SuppressMirroredDuplicates` |

详见 `plan/REMEDIATION-PLAN-2026-07-29.md` §3.1。本表**不**把单个 pass 标 `DEAD`。

---

## 5. 建议处理顺序（仍不改代码时的排队）

仅优先级建议，需用户另授权才动手：

| 顺序 | 项 | 标签 | 建议动作类型 |
| --- | --- | --- | --- |
| 1 | §3.4 文档幽灵 `Draw*` | `DEAD-DOC` | 文档正名 |
| 2 | §3.1 台阶/冗余死簇 | `DEAD` R1 | 删除或 `[Obsolete]` + 单测锁定「无引用」 |
| 3 | §3.3 `IsRightStructureHeightCoveredByLeft` | `DEAD` R1 | 删除或补齐对称策略（产品决策） |
| 4 | §4.1 延伸线：Rules public 死半边 vs StructureGeometry | `DUAL` R2 | 委托合并 + 结构回归 |
| 5 | §4.2 镜像双通道 | `DUAL-LAYER` R3 | 谓词统一，保留两层调用点直至诊断对齐 |
| 6 | §4.4 四向结构点 | `QUAD` R2 | 参数化重构，强依赖四向回归 |
| 7 | §4.7 抑制 pass 族 | 结构债 | 走整改计划统一裁决，禁止零散删 pass |

---

## 6. 复扫命令（债务复验）

在仓库根目录（PowerShell）：

```powershell
# 示例：检查某符号是否仍仅出现在定义文件
$names = @(
  'IsUsefulHorizontalStep',
  'IsFeatureRedundantStepDimension',
  'StepHeightExtensionCrossesOutlineInterior',
  'ShouldIgnoreDirectionalInclinedEndpoint',
  'IsRightStructureHeightCoveredByLeft',
  'DrawTopStepWidth'
)
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch 'graphify' } |
  Select-String -Pattern ($names -join '|') |
  Group-Object Pattern |
  Select-Object Name, Count
```

更新本表时：改「日期 / 提交 / 状态列」，并在下方修订记录追加一行。

---

## 7. 修订记录

| 日期 | 提交 | 说明 |
| --- | --- | --- |
| 2026-08-05 | `ba25e04` / 分支 `8.5` | 首版：死 API 表 + 双实现对照；不改产品逻辑 |
| 2026-08-05 | 分支 `8.5` S1 | **A/S1/D1/P2/N2**：删除 §3.1 死簇+级联 private、§3.3 对称死 API、文档 N2 正名；**不含 §4.1**；门禁 2 |

### 7.1 S1 实施清单（已完成）

| 文件 | 变更 |
| --- | --- |
| `CadAuto.Core/Rules/StructureSuppressionRules.cs` | 删死 public 簇 + `ShouldIgnoreDirectionalInclinedEndpoint` + 级联 private |
| `CadAuto.Core/Rules/DimensionDeduplicationRules.cs` | 删 `IsRightStructureHeightCoveredByLeft` |
| `docs/DimensionRules.md` | 四条 `Draw*` → DebugRole + Structure 入口锚点（N2） |
| `docs/RuleDebt-DeadApi-And-DualImpl-2026-08-05.md` | 本状态同步 |

**明确未做：** Rules 四向 `*ExtensionCrossesOutline` 删除/委托（§4.1）、镜像双通道、四向结构点参数化、任何抑制 pass。

---

## 8. 非目标（本文件明确不做）

- 不修改任何 `.cs` 产品/测试行为。
- 不调整 `regression/remediation/case-truth-matrix.json`。
- 不删除 27 pass 中的任何抑制步骤。
- 不把 `DebugRole` → typed Role 的迁移写入本债务的实施范围（可另开语义债文档）。
