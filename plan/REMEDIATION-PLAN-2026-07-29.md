# CAD 自动标注规则与回归体系整改计划

> 日期：2026-07-29
> 状态：实施中；M3 已完成；M4/阶段 6 门禁验证通过并冻结；仅两条稳定规则阻断
> 原则：渐进替换、行为可对照、每阶段可回退；禁止大爆炸重写

## 1. 目标与边界

本计划解决两个核心问题：

1. 标注规则依赖顺序敏感的补丁式抑制，修复一个图样容易破坏相邻图样。
2. Core、真实 DWG、布局和视觉验收之间存在断层，回归仍需要大量手动编排和判断。

目标不是继续增加规则数量，而是建立“完整候选 → 统一裁决 → 可解释结果 → 自动验证”的闭环。

本计划不授权立即修改产品行为。实施期间必须遵守：

- 禁止大爆炸重写；每次只迁移一个明确规则族。
- 禁止为了测试变绿直接重置 expectation、fixture 状态或基线。
- 禁止新增没有正式 `RuleId`、证据和反例保护的裸 suppression pass。
- 禁止同时重构候选裁决与布局算法。
- 禁止把未知或未经用户确认的结果写成正确基线。
- P0/P1 期望只有在用户明确批准产品规则变化后才能修改（`docs/DimensionRules.md:111-115`）。

## 2. 现状证据

| 现状 | 仓库证据 | 影响 |
| --- | --- | --- |
| 抑制流水线有 27 个 pass，且源码明确声明顺序属于产品行为 | `CadAuto.Core/Planning/DimensionPlanner.Suppression.cs:13-29` | 新规则必须理解所有前后顺序，补丁容易相互遮蔽 |
| 完整计划会执行两次抑制流水线 | `CadAuto.Core/Planning/DimensionPlanner.Suppression.cs:30-32`；调用位于 `CadAuto.Core/Planning/DimensionPlanner.cs:144-177` | 外形候选与孔槽候选经历的裁决次数不同，行为难推理 |
| 抑制后会把候选从活动集合移除 | `CadAuto.Core/Planning/DimensionPlanner.cs:304-310`；`CadAuto.Core/Planning/DimensionPlanner.Structure.cs:211-223` | 后续规则看不到完整候选集，只能增加快照、保护条件或调整顺序 |
| 诊断对象已经能记录 Candidate、Suppressed、Skipped、Selected | `CadAuto.Core/Planning/DimensionPlan.cs:28-50`、`CadAuto.Core/Planning/DimensionPlan.cs:61-112` | 可在不丢失候选的前提下演进为统一裁决，不必另建框架 |
| `PlannedDimension` 只有 `DebugOwner`、`DebugRole` 等字符串，没有正式业务角色和拓扑证据 | `CadAuto.Core/Planning/PlannedDimension.cs:5-49` | 规则、诊断和测试容易分别解释同一字符串，语义会漂移 |
| Core 测试识别器不是生产识别器，源码要求人工镜像规则 | `CadAuto.Core/Recognition/FeatureRecognizer2D.cs:11-16` | Core 通过不能充分证明真实 CAD 识别路径正确 |
| Core 测试集中在单个手工注册程序中 | 注册容器见 `CadAuto.Core.Tests/Program.cs:14-18`，126 项手工调用见 `CadAuto.Core.Tests/Program.cs:103-233` | 用例可运行，但维护、分组、发现漏注册和精确定位成本较高 |
| 托管 CI 只构建并运行 Core 测试 | `.github/workflows/core-ci.yml:3-6`、`.github/workflows/core-ci.yml:44-48` | 真实 AutoCAD 识别、绘制和报告链未进入自动门禁 |
| Core-CAD 已有确定性 runner 和结构化候选断言，但当前 manifest 只有 OC01 | `docs/CoreCadRegression.md:5-27`；`regression/core-cad/cases.json:3-15` | 基础设施可复用，真实图样覆盖仍不足 |
| DL01 已定义四方向及固定 SHA256，但仍包含人工视觉检查 | `regression/dimension-layout/cases.json:3-80`；`docs/DimensionLayoutRegression.md:217-269` | 结构化报告通过后仍不能自动证明文字、箭头和延伸线无碰撞 |
| Hole/Slot 有 8 个 case，但全部 `fixtureReady=false` | `regression/hole-slot/cases.json:3-100` | 当前不能把 8 个 case 当作可信阻断基线 |
| HS05 的 case 语义是 multiple-pin，fixture 文件名却是 unique-pin | `regression/hole-slot/cases.json:52-62` | 首先要人工确认图样真实语义，不能根据文件名或测试期望猜测 |
| 真实附着点规则已明确禁止理论交点和浮空点 | `docs/DimensionRules.md:11-17` | 自动回归必须验证真实几何引用，不能只比数值 |

补充说明：当前 `CadAuto.Core.Tests/Program.cs` 实际注册 126 个测试；旧验证文档仍写 117 个（`docs/RefactorVerification-2026-07-27.md:15-20`、`docs/RefactorVerification-2026-07-27.md:34-44`），说明测试资产和文档统计已经出现漂移。

## 3. 根因

### 3.1 裁决模型以“删除顺序”为核心

当前流程边生成、边抑制、边从活动集合删除。规则是否能看到某个候选取决于它位于第几个 pass，导致：

- 新问题通常通过增加 pass、快照或保护条件修复。
- 一个 pass 提前删除候选后，后续规则无法基于完整证据比较。
- 同一候选可能先受生成规则影响，再受两轮抑制影响，最终原因不够唯一。

### 3.2 业务语义依附调试字符串

`DebugRole` 同时承担诊断标签、规则分流和测试匹配职责，但缺少正式角色、来源实体、拓扑关系和决策证据。字符串兼容要求逐渐绑住产品实现。

### 3.3 测试真值链断层

目前存在四层真值，但没有完全闭环：

1. Core 人工构造几何。
2. CadAdapter 生产识别。
3. AutoCAD 实体绘制和诊断报告。
4. 最终视觉效果。

Core 使用独立识别器；CI 只跑 Core；DWG runner 覆盖少；碰撞和可读性仍靠人工图片复核。因此单层通过不能证明整体正确。

### 3.4 基线状态没有统一表达

`fixtureReady` 只能表示可否作为正式 fixture，无法完整表达“确认正确、已知错误、尚未裁决”。未知结果容易被误当成失败，或为变绿而被错误固化。

### 3.5 回归编排按 case 重复执行

现有 Core-CAD runner 的 `CaseId` 为必填项（`scripts/run-core-cad-regression.ps1:1-17`），每个 case 会分别运行映射 Core 测试并选择或生成 DLL（`scripts/run-core-cad-regression.ps1:125-160`）。缺少编译一次、串行运行所有 ready case、统一归档结果的薄编排层。

## 4. 整改阶段

## 阶段 0：冻结行为与建立三态真值矩阵

### 工作

- 盘点 Core、Core-CAD、DL01、Hole/Slot 的现有 case。
- 每个 case 标记为：
  - `ConfirmedCorrect`：有用户确认或可信历史证据，可作为阻断基线。
  - `KnownWrong`：已确认产品行为错误，保留失败证据但不作为正确期望。
  - `Unresolved`：没有足够证据，不推断对错。
- 固定并记录 DWG SHA256、输入实体数、报告 schema、报告哈希、PNG、DLL 目录和 DLL SHA256。
- 明确裁决：
  - DL01 各方向现有 baseline/target 哪个代表当前产品契约。
  - HS05 到底是唯一候选还是多个候选。
  - 8 个 Hole/Slot case 哪些具备真实可复现 fixture。

### 交付物

- 一份 case 真值矩阵。
- 每个 `ConfirmedCorrect` case 的证据索引。
- `KnownWrong` 和 `Unresolved` 清单及责任人/待确认问题。

### 退出条件

- 现有全部 case 100% 进入三态之一。
- 每个 `ConfirmedCorrect` case 都有固定输入哈希和一次可追溯结果。
- 不存在仅因当前代码输出而自动认定正确的 expectation。

### 风险与回滚

- 风险：旧报告缺少哈希、图片或版本信息，无法升级为可信基线。
- 处理：保持 `Unresolved`，不补猜测数据。
- 回滚：本阶段只整理资产和状态；删除新增索引即可，不改变产品行为。

## 阶段 1：加固候选级测试门禁

### 工作

每个现有或新增规则至少具备：

1. 候选确实生成的断言。
2. 最终保留/抑制决策及唯一原因断言。
3. 一个相邻反例，证明规则没有扩大作用域。
4. 一个镜像、旋转或平移等价例。
5. 对真实几何附着点和来源实体的断言。

修正规则测试的常见假阳性：

- 禁止仅断言“最终没有某尺寸”，因为候选可能根本没有生成。
- 对需要四个候选后再裁决的场景，必须先断言四个候选真实存在，再断言最终结果。
- 除 required final 外，还要断言 forbidden final 和意外额外尺寸数量为 0。

### 交付物

- 规则—测试—fixture 追踪表。
- 候选生成、最终决策、反例和变换等价测试模板。
- PR 快速门禁清单。

### 退出条件

- 已迁移规则的候选决策覆盖率为 100%。
- 每条已迁移抑制规则至少 1 个正例、1 个反例、1 个变换等价例。
- 所有测试都能证明候选非空，空集合假阳性为 0。
- PR 快速层在标准机器上 3 分钟内完成。

### 风险与回滚

- 风险：测试揭示旧 expectation 相互冲突。
- 处理：转入 `KnownWrong` 或 `Unresolved`，等待产品裁决；不得直接改期望。
- 回滚：测试可按规则族逐批引入；有争议批次单独撤回，不影响其他门禁。

## 阶段 2：类型化候选语义

### 工作

在保持现有输出兼容的前提下，为候选补充正式语义：

- `Role`：正式枚举或封闭类型，表达 Overall、Structure、OutlineSegment、Hole、Slot、Datum 等角色。
- `Owner`：明确所属外形、孔组、销孔组、槽或结构链。
- `SourceGeometryIds`：记录真实实体或 Core 几何来源。
- `TopologyEvidence`：记录包络边、区间邻接、分割闭合、镜像等可验证关系。
- `Decision`：`Selected`、`Suppressed`、`Skipped`、`NotSelected`。
- `RuleId`：最终裁决的唯一权威规则标识。

`DebugRole`、`DebugOwner` 暂时保留为兼容诊断输出，不再作为新规则的唯一业务依据。

### 交付物

- 最小候选语义模型。
- 旧字符串到正式角色的单向映射。
- 新旧诊断报告的规范化对照器。

### 退出条件

- 冻结案例的新旧最终尺寸规范化结果 100% 一致。
- 每个候选都能追溯到正式角色和来源证据。
- 新规则不直接解析 `DebugRole` 字符串决定产品行为。

### 风险与回滚

- 风险：新增字段影响诊断 schema 或旧工具。
- 处理：只追加可选字段，保留现有字段和 schema 兼容。
- 回滚：保留旧路径为读取源；关闭新字段消费即可恢复，不更改旧结果。

## 阶段 3：单次统一裁决

### 工作

- 生成阶段只收集候选和证据，不从主候选集合删除。
- 收集完成后按方向、测量轴、所有者和拓扑关系分组。
- 在一次统一裁决中完成保护、等价比较、分割关系和优先级决策。
- 每个候选只产生一个最终状态和一个权威 `RuleId`；其他命中仅作为辅助证据。
- 首先迁移“外轮廓台阶与 Overall 补余”规则族，以 OC01 为真实锚点。
- 每迁移一个规则族，就用阶段 1 的正例、反例、变换例和真实 DWG 做新旧双跑。

### 交付物

- 统一裁决入口。
- 第一批外轮廓台阶规则迁移。
- 新旧裁决差异报告。

### 退出条件

- 已迁移范围不再调用对应旧 suppression pass。
- 已迁移候选 100% 具有唯一最终状态和 `RuleId`。
- 冻结案例除已批准变化外零差异。
- 后续需求不得以新增裸 suppression pass 方式进入主干。

### 风险与回滚

- 风险：完整候选集改变旧顺序副作用，暴露潜在冲突。
- 处理：新旧裁决并行运行，只记录差异，先不切换产品输出。
- 回滚：按规则族使用单一切换点退回旧裁决；禁止一次删除全部旧 pass。

## 阶段 4：统一方向模型与生产识别

### 工作

- 用“测量轴 + 外法向 + 主侧/次侧”表达 Top、Bottom、Left、Right，减少四套分支规则。
- 把镜像、旋转、平移和轮廓点序反转定义为变换不变量。
- 将可复用的纯几何识别下沉到 Core。
- CadAdapter 生产识别器只负责 AutoCAD 实体读取和到 Core 几何的转换。
- 在生产识别完全切换前，保留适配器与旧实现对照。

### 交付物

- 方向无关的规则输入模型。
- 共用纯几何识别入口。
- 生产识别与 Core 识别对照测试。

### 退出条件

- 同一拓扑在 Top/Bottom/Left/Right、镜像、90° 旋转、平移和轮廓反向后得到等价语义结果。
- Core 测试不再要求人工把生产识别规则镜像到第二份实现。
- CadAdapter 只保留 AutoCAD API 相关转换。

### 风险与回滚

- 风险：AutoCAD 实体容差、圆弧方向和 WCS 转换与纯 Core 模型不完全一致。
- 处理：保留容差配置和原始实体证据，先双跑比较。
- 回滚：按识别特征类型切换，孔、槽、倒角、圆角可独立回退。

## 阶段 5：自动化真实 CAD 回归

### 工作

在现有脚本之上增加薄编排层，不另造测试框架：

1. 枚举三类 manifest 中的 ready case。
2. 编译一次全新的 `bin/Debug-vN` 完整 DLL 集并记录 SHA256。
3. 串行运行 Core-CAD、DL01 四方向、Hole/Slot case，避免 AutoCAD 与报告文件互相争用。
4. 为每个 case 创建独立工作 DWG 和运行目录。
5. 自动归档 trace、report、result、DLL 哈希和 PNG。
6. 输出一份汇总 JSON/Markdown，失败时保留全部证据。

### 交付物

- 一条全量回归入口命令。
- 每次运行的机器可读汇总。
- nightly 自托管 Windows + AutoCAD 作业。

### 退出条件

- Core-CAD 所有 ready case 100% 通过。
- DL01 Top/Bottom/Left/Right 达到 4/4。
- Hole/Slot 在逐一完成真实验收后达到 8/8，未 ready case 不得伪装通过。
- 同一 DLL 对同一全量集合连续运行 3 次，规范化结果零差异。
- 编译只执行 1 次，所有 case 使用同一 DLL 哈希。

### 风险与回滚

- 风险：AutoCAD/Core Console 全局状态、文件锁或模态窗口导致不稳定。
- 处理：CAD case 保持串行、独立目录、超时不强杀、使用新版本 DLL 目录。
- 回滚：编排层可退回逐 case 运行，底层现有脚本保持不变。

## 阶段 6：收敛人工视觉验收

### 工作

从最终 AutoCAD 实体提取并自动检查：

- 尺寸真实附着点及来源实体。
- 尺寸线、文字 extents、箭头和延伸线。
- 尺寸线是否穿越真实轮廓内部。
- 文字、箭头、延伸线和相邻标注是否碰撞。
- 短跨度在内、长跨度向外及 Overall 最外层。
- 同一布局块的连续性和方向一致性。

M4 首批先以 warning/report 形式检查碰撞、穿越和布局，不改变产品标注、不修改 expectation；证据通过并获明确授权后，仅将稳定规则逐项升级为阻断，其余规则继续保持 WarningOnly。

保留人工职责：

- 新 fixture 的首次产品真值批准。
- 自动检查无法表达的可读性判断。
- 发布前抽检。

### 交付物

- CAD 实体几何快照。
- 自动碰撞与穿越检查器。
- 图片与结构化结果的统一验收报告。

### 退出条件

- 已知可计算的附着、穿越和碰撞规则自动覆盖率 100%。
- 常规回归不再要求逐 case 手动画框、复制报告或人工比数值。
- 人工只处理新 fixture 首次批准和发布抽检。
- 自动结构化结果与人工图片复核连续 3 轮无矛盾后，才下调人工频率。

### 风险与回滚

- 风险：文字 extents、字体替换和 dimstyle 造成平台差异。
- 处理：记录字体、dimstyle、DIMSCALE、DIMTXT、DIMASZ；不确定结果标为需人工复核。
- 回滚：自动检查先作为非阻断报告；稳定后逐项升级为门禁。

## 5. 测试门禁

### 5.1 每次规则修改的最低证据

1. 一个修改前失败的最小 Core 用例。
2. 一个证明真实候选已经生成的断言。
3. 一个最终决策和唯一 `RuleId` 断言。
4. 一个防止扩大抑制范围的相邻反例。
5. 一个镜像、旋转或平移等价例。
6. 一个真实问题对应的 DWG/诊断 fixture。
7. 修改 expectation 时，单独说明产品契约变化并取得用户批准。

### 5.2 分级门禁

| 层级 | 触发 | 内容 | 时限/要求 |
| --- | --- | --- | --- |
| PR 快速层 | 所有 PR | TOML/JSON/schema/manifest 校验、Core 全量、诊断快照回放 | 目标不超过 3 分钟 |
| 规则 PR 层 | 修改生成或裁决规则 | 快速层 + 受影响的真实 DWG case | 必须使用固定 fixture 和新鲜报告 |
| Nightly CAD 层 | 每晚自托管 | 全部 ready CAD case；同一 DLL 串行执行 | 连续 3 次确定性检查 |
| 发布层 | 发布候选 | 全量 CAD、实体几何门禁、PNG 归档、少量人工视觉抽检 | 任一阻断失败不得发布 |

### 5.3 基线变更规则

- expectation 变化必须与产品代码变化分开审查。
- 失败回归是证据，不自动等于产品缺陷，也不授权修改产品。
- `KnownWrong` 可以保存失败输出，但不能把错误输出升级为正确基线。
- `Unresolved` 不参与“全绿”统计，必须单独报告数量。
- fixture 哈希变化视为新输入，必须重新进行首次批准。

## 6. 量化验收指标

| 指标 | 当前证据 | 目标 |
| --- | --- | --- |
| suppression pass | 27 个且顺序敏感 | 不再新增裸 pass；按规则族逐步迁移到统一裁决 |
| 候选可解释性 | 有诊断状态，但业务语义主要依赖字符串 | 100% 候选有正式 Role、来源证据、最终状态和唯一 RuleId |
| Core 测试 | 126 个手工注册 | 保持全量通过；规则变更的正例/反例/变换例覆盖 100% |
| 空集合假阳性 | 未形成统一门禁 | 0 |
| CI 范围 | PR 快速层与自托管 CAD nightly 已启用 | PR 有快速层；nightly 覆盖所有 ready CAD case |
| Core-CAD | 当前 1 个 OC01 | 所有 ready case 100% 通过，并持续增加真实问题最小 fixture |
| DL01 | 4/4 target 结构化通过，full/detail 图片已确认 | 4/4 结构化通过，自动化可计算视觉规则 |
| Hole/Slot | 8 个 case，全部未 ready | 逐一真实验收后 8/8；未 ready 不计通过 |
| 确定性 | 5 个 ready case 已用同一 DLL 连续 3 次零差异 | 同一 DLL 全量连续 3 次零差异 |
| 编译与编排 | M3 批次只编译 1 次，15 次 CAD 串行并统一汇总 | 全量运行只编译 1 次，统一汇总 |
| 人工操作 | 仍需逐方向编排和图片判断 | 常规回归零手动输入；人工仅首次批准和发布抽检 |

## 7. 推荐实施顺序

1. **先做阶段 0**：没有可信真值，不应继续增加规则或重构。
2. **紧接阶段 1**：先堵住空集合假阳性和扩大抑制范围，阻止继续堆补丁。
3. **阶段 2 与阶段 3 串行推进**：先补正式语义，再按规则族迁移统一裁决；不要反过来。
4. **阶段 3 首批只选 OC01 外轮廓台阶规则族**：范围小、已有真实 DWG 和候选决策断言。
5. **阶段 4 在统一裁决稳定后实施**：避免同时改变方向表达、识别和裁决。
6. **阶段 5 可在阶段 1 后先做薄编排 MVP**：只复用现有 runner；完整门禁待阶段 3/4 稳定后升级。
7. **阶段 6 最后门禁化**：先报告、后阻断，避免不稳定视觉算法阻塞开发。

建议里程碑：

- M1：阶段 0 + 阶段 1，停止继续增加无证据补丁。
- M2：阶段 2 + OC01 规则族的阶段 3 迁移，证明统一裁决可行。
- M3：阶段 4 + 阶段 5，全量真实回归一键执行；2026-07-30 已完成当前 5 个 ready case 的三轮闭环。
- M4：阶段 6，第三轮独立证据与门禁验证通过并冻结；`LEFT_LAYOUT_TEXT_PLACEMENT` 与 `TEXT_TEXT_COLLISION` 为阻断，其余视觉规则仍为 WarningOnly。

M4 第三轮独立证据（2026-07-31）：

- 真实正样本串行两轮：`20260731-150014849`、`20260731-150020787`；两轮持续检出 `LEFT_LAYOUT_TEXT_PLACEMENT`（`ED7311`/`ED7347`）与 `TEXT_TEXT_COLLISION`（`ED7303`、`ED7304`、`ED7331`、`ED733A`），normalized visual SHA 均为 `45F2AB97E2C978D647C72D6D030FF8F798A2D1C4FA96D01CE2CE9A072D373C18`，且 `WarningOnly`/`blocking=false`。
- Clean batch：`regression/nightly/runs/20260731-150039096/summary.json`；同一 `Debug-v3`、pwsh 外层串行运行 5 个 clean case × 2 轮，`10/10 Passed`、`0 warning`、FP/FN `0/0`、5/5 规范化报告 deterministic，Core `130/130`。
- 正样本 fixture SHA：`79D944E913627459872C6D94F120198E0FBD92105E77A0D409CEF8C9A3ECDD88`；三 DLL SHA 与 M2 `Debug-v3` 一致。PowerShell resolver 仅调整测试入口路径解析；本轮未改变 Top/Right、产品代码、expectation、fixture 或 manifest。

M4 阻断升级证据（2026-07-31）：

- 门禁配置：`regression/visual-inspection/visual-gate.json`；仅 `LEFT_LAYOUT_TEXT_PLACEMENT`、`TEXT_TEXT_COLLISION` 进入 `blockingWarningCodes`，豁免列表为空，失败策略为 `FailOnBlockingWarnings`，回滚模式为 `WarningOnly`。
- 完整回归：`regression/nightly/runs/20260731-151841996/summary.json`；同一 `Debug-v3` 串行 5 个 ready case × 3，`15/15 Passed`，Core `130/130`，视觉 `warningCount=0`、blocking `0`、FP/FN `0/0`，5 个 case 的规范化视觉报告均 `deterministic=true`。
- 三 DLL SHA：`AutoFixtureDim.dll` `B145FDCCC2C5DFA05FCBEA5E8886BC3AD27B4A577D0366EFFD2762A53990CBED`；`CadAuto.Core.dll` `4D20C161DF40BFB0C5B67C6D498438D42F9EBC7EA5B2FCA1BB1CFD0BA34E9498`；`CadAuto.CadAdapter.dll` `D3961973C6F6152789B969BCFF9EC607803B6602D29D774B4CB959DC03CC6857`。
- 原 M4 WarningOnly 正样本矩阵与历史结果保持冻结；本次未修改产品代码、expectation、fixture、manifest 或正式 runner。

M4 门禁冻结验证（2026-07-31）：

- 真实正样本 Blocking：`regression/visual-inspection/runs/M4-positive-collisions-real/20260731-152902558/`；进程 exit `1`，`result.status=Failed`、`mode=Blocking`、`blocking=true`，命中 `LEFT_LAYOUT_TEXT_PLACEMENT`（`ED7311`/`ED7347`）与 `TEXT_TEXT_COLLISION`（`ED7303`、`ED7304`、`ED7331`、`ED733A`），result/report/trace/full/detail/hashes 与三 DLL SHA 均归档。
- WarningOnly 回滚：`regression/visual-inspection/runs/M4-positive-collisions-real/20260731-152930618/`；使用临时回滚配置，exit `0`、`Completed`、`blocking=false`，两类 warning 仍被报告；临时配置已删除，正式 `visual-gate.json` 已恢复并保持 `Blocking`。
- clean 门禁回归：`regression/nightly/runs/20260731-152944270/summary.json`；同一 `Debug-v3`、5 个 ready case × 3，`15/15 Passed`，Core `130/130`，15 个视觉报告均 `Blocking` 模式但 `blocking=false`、warning `0`，FP/FN `0/0`，5/5 deterministic；summary、summary.md、dll-hashes.json 记录 gate decision、blockingCodes 和 DLL SHA。
- M4 已冻结；后续仅按 M5 计划推进，不再修改两条阻断规则、既有 M4 证据或回滚基线。

## M5 正式定义（2026-07-31）

### M5-1：PR 快速层与 nightly 门禁运营化

- 将 PR 快速层与 nightly 门禁的配置、manifest、规则版本和执行摘要纳入版本控制；每次运行归档 artifact，并记录整套 DLL 的 SHA256。
- PR 失败必须保留 case、trace、report、result、PNG（如适用）和哈希；先按固定输入、同一 DLL、串行复跑定位，禁止以改 expectation、产品行为或 fixture 绕过失败。
- 回滚只允许通过已版本化的门禁配置切回上一已验证版本或 `WarningOnly`；不得删除历史 artifact、失败证据或既有 runner。
- 自托管 Runner 仅负责已安装 AutoCAD 环境、标签、在线状态、磁盘空间、凭据隔离和串行 CAD 作业；产品规则、真值裁决和 fixture 批准不由 Runner 运营替代。

### M5-2：Hole/Slot 真实 fixture 补齐

- 每个 Hole/Slot case 单独补齐产品真值、固定 fixture 与 SHA256、结构化 result/report/trace，以及经批准的视觉证据；逐 case 审批，不以汇总绿灯替代。
- `fixtureReady=false` 或缺任一真值、哈希、结构化或视觉证据的 case 均为未 ready：可记录 Skipped，但不得计为 Passed、不得进入通过率分子。
- 2026-07-31：用户确认 HS01 最新图片正确；首次证据 batch `20260731-164709701` 后，HS01 已为 `ConfirmedCorrect` 且 `fixtureReady=true`。ready nightly batch `20260731-165606678` 完成 6 case × 2 = 12/12 Passed、Core 130/130、视觉 warning 0、FP/FN 0/0；HS01 report/visual/full 哈希各唯一 1，Debug-v5 三 DLL SHA 不变。
- 2026-07-31：HS01、HS02 均为 `ConfirmedCorrect` 且 `fixtureReady=true`。ready nightly batch `20260731-170548196` 完成 7 ready case × 2 = 14/14 Passed、Core 130/130、视觉 warning 0、FP/FN 0/0，使用同一 Debug-v5 三 DLL；HS01、HS02 各自 report/visual/full 哈希均唯一 1。
- 2026-08-01：用户确认 HS03 pin-group 探针图片正确；manifest `fixtureReady=true`。ready batch `regression/hole-slot/runs/hs03-pin-group-ready-20260801-132227891/summary.json` 为 `Passed 2/2`，CAD/validator exit 0、trace complete、`pinHoleCount=2`、PinGroup-owned finals=3（DatumX/DatumY/PinDistance），剩余 LooseHole 为非销孔链，warning=0、`blocking=false`；normalized report `A67346E7F4FD4BB3373BD6577FA7F3D2D8F958BA52C79DA346DEE621C35B3E67`、visual `4376DFFF66628C87BFD72A521AE331280A69350FC12481092A246E0B5F15076B`、full `0236CB9490F5E92487C9758A68B61FD1D0A7C919F3ECBF2EE6755554EB15F2B5` 稳定。布局偏好：尺寸优先靠近孔所在一侧轮廓；尺寸较少时可将 50 放左侧与 20 对齐。Debug-v7 DLL SHA 已登记。
- 2026-08-01：用户确认 HS04 最新全图正确；manifest `regression/hole-slot/cases.json` 的 `fixtureReady=true`，fixture SHA `FE114C91410C4AD307A64863A77B40CC2A5D7B66D75DB6A69FE70F27F538F059`。批准图片来自 `regression/hole-slot/runs/hs04-acceptance-20260801-104617480/`（summary SHA `67D58C7E6C38D9594CD54B81D56C8896D2360427F748C2C3B40F21CA4F32122B`，full image SHA `214D0B4B455B62F25805111B4C51868889EF6B2E74040EAAECC46632405FA7D6`）。fixtureReady 后正式两轮 batch `regression/hole-slot/runs/hs04-ready-20260801-105520062/summary.json` 为 `Passed 2/2`（summary SHA `4F431588EFC3555B8C03A98228D5404F66BD0B97FB3BF80E763AEF895678400F`，dll-hashes SHA `2D45372DB1D7E474C52DDDB74A59B2461784D48EEF20FC0A0296C8671E97C92F`；repeat-1 result/report/trace/full/visual-report SHA 依次为 `5FC65BCF4352E146DA56A09537857651D313A179C64962B068332C0A0BE31E1A`、`DDB29FF28ADBADA9AB270766400EA8C774F8D69F8913EADCDAC825EDE7F3772D`、`0625C2ACCF9954F060F2180A2B2128D54EFCC4FDE55B3D3F7930BCA02058BC9F`、`214D0B4B455B62F25805111B4C51868889EF6B2E74040EAAECC46632405FA7D6`、`3CE40D7C33A2716D2FFE2046E63F5938690A119FF8573041DF53F034A6332D94`）。两轮 CAD/validator exit 0、trace complete、DatumX/DatumY 各 1、唯一销孔自动成为基准且无手工选择；normalized report/visual/full SHA `036F241DDDEA99F128FF4937CA47EE41F63D548A8413F9AC657CDC51E5216E6C`、`4DC0C7EE96D5E5CEC1B628652F5D5B2E24C7200A006587295A60BF5EFE38C26B`、`214D0B4B455B62F25805111B4C51868889EF6B2E74040EAAECC46632405FA7D6` 均稳定。`visualWarningCount=1`、`blocking=false`；唯一 warning 为 `SNAPSHOT_ERROR` / source / unsupported DXF LWPOLYLINE，属于检查器源实体抓取限制，不是产品碰撞。Debug-v7 DLL SHA：AutoFixtureDim `B145FDCCC2C5DFA05FCBEA5E8886BC3AD27B4A577D0366EFFD2762A53990CBED`、Core `9CA8B46C15FEEE6172429FA6332946004BBD12B9ECB5C6F7ECE60049B4031A63`、Adapter `D3961973C6F6152789B969BCFF9EC607803B6602D29D774B4CB959DC03CC6857`。
- 2026-08-01：用户确认 HS05 最新全图正确；manifest fixtureReady=true，fixture SHA D6A80DB5D21363669E084F9242F6341D7551AA3514CD41F88CF2FA9FA7865281。尽管 fixture 名为 HS05-unique-pin-datum.dwg，真实两轮诊断稳定证明 marker=2、candidate=2、features.pinHoleCount=2，故 HS05-multiple-pin-datum 语义由证据确认而非名称推断。批准图片 batch regression/hole-slot/runs/hs05-acceptance-20260801-114522211/（summary SHA 537E357E05CC23FC00FCCFEFF46033A5438F7528FFA11A2CFA3EADCF81934D69，full SHA 4F5F26FD11FFE35D15EF904703216404909E92E53EEB1C27B5154ABD905A31A1）；ready batch regression/hole-slot/runs/hs05-ready-20260801-123208715/summary.json 为 Passed 2/2，CAD/validator exit 0、trace complete、手工选择真实 ENAME ED6D32 后匹配已识别销孔并设置基准，DatumX/DatumY/PinDistance 均满足。normalized report/visual/full SHA D24D296803B61539E7C9A4059F014F6A5EACE4FFC880BB92089EF36DA25FCC7A、1F26E564A15CBAECCAF6D26BE8CE0F83E36917A5F29B45F2255CE335D0A12E0C、4F5F26FD11FFE35D15EF904703216404909E92E53EEB1C27B5154ABD905A31A1 两轮稳定。visualWarningCount=1、blocking=false；非阻断 warning 为 Right / OUTWARD_LAYER_DIRECTION_REVERSED / ED6F61。Debug-v7 DLL SHA 及 repeat-1 全部 artifact SHA 见 truth matrix 的 HS05 证据索引。
- 2026-08-01：用户确认 HS06 acceptance batch `regression/hole-slot/runs/hs06-acceptance-20260801-125449489/` 的全图；fixture SHA `34171D0289AD63BDD2E6F28FEEA60E420F567A271E462E1C0BEE22ABC27F3F01`。双轮 status `EvidenceCapturedNotReady` 是在 `fixtureReady=false` 时的历史证据，normalized report/visual/full 哈希稳定；`FunctionalHole` count=1、`ownerKind=PinGroup`、`usesLocalBoundary=true`、`visualWarningCount=0`、`blocking=false`。manifest `fixtureReady=true`；ready batch `regression/hole-slot/runs/hs06-ready-20260801-130015278/summary.json` 为 `Passed 2/2`，CAD/validator exit 0、trace complete、`FunctionalHole=1`、`ownerKind=PinGroup`、`usesLocalBoundary=true`、`visualWarningCount=0`、`blocking=false`；normalized report `8F31700C31AC4A8ADBCC05FD5614CD7E13AB032960B09C18DFDD2DC4DFC154F1`、normalized visual `F3C4A7C1D61D5AB006AF3A965843C5D11C5145708718822705BD17433FA6CBD3`、full `FA37ECABD838ED0958A2BBCA47FC1459EB73BD50B8B00F137E018EB4942F2CAA` 两轮稳定；Debug-v7 DLL SHA 已登记。
- 2026-08-01：用户确认 HS07 图片正确，`fixtureReady=true`。真实两轮 evidence batch `regression/hole-slot/runs/hs07-bringup-20260801-141500000/summary.json` 中 CAD/validator exit 均为 0、trace complete、`features.slotCount=1`、有效附着的 `SlotCenter`/`SlotChainH`/`SlotDatumV` 各 1：`SlotCenter (50,70)->(50,30)`、`SlotChainH (0,30)->(50,30)`、`SlotDatumV (50,0)->(50,30)`；`visualWarningCount=0`、`blocking=false`。normalized report `E8C740EE933052F83A1441C28AEB4F35D4A16FE6FD55D5973008542F4F48C1A6`、normalized visual `B5CA97F3480EA6A8FFE654CFEEFB05BD4D4DF095BEE76650D86DE2418416F40D`、full `94AC8C61EE42940916A23D1BB8BDCF54DA20049008396DDECC2D433B7BBABED3`、trace `1FBBB75F5D64627D6D306AD1C4F24A4142C7F8B9BB898719FAF4AB821B990358` 两轮稳定；DLL SHA：AutoFixtureDim `30FAA04C0EBD79CE541677226A6249AE9C1EC7AD03C65D7E2E09C5ABAFFD6C40`、Core `52E0A8AB26B9B9F075004DF63BF7CA99A957146431FFE23E7EF44C9F737598C1`、Adapter `8CC138BE6BFDFE71CE80F019CCA5452AFEDE701A9192653908642B685739EB9F`。仅调整 runner 的 HS07 入口支持；本次未修改产品行为、expectation 或 DWG fixture；本次人工确认后仅登记 manifest 的 fixtureReady 与证据状态。
- 2026-08-01 M5 恢复核查：同一 `Debug-v8` nightly batch `regression/nightly/runs/20260801-150529152/summary.json` 共 36 条 CAD 记录，`27 Passed / 9 Failed`；Core 独立测试 `133/133`。失败集中为 DL01 Left `3/3`、Right `3/3` 与 HS04 `3/3`，三 DLL SHA 在批次内稳定。DL01 只读归因确认：当前 `L6=48.5` 按语义侧保持 Left，旧 Right expectation 仍要求该尺寸，且 Left 真实物理顺序冲突；未修改产品行为或 expectation，等待单独裁决“保留语义侧一致并更新 expectation+修复 Left 排序”或“恢复旧跨侧 rebalance”。
- 2026-08-01 B 方案语义回写：按用户选择恢复旧的竖向 LooseChain 跨侧 rebalance；渲染器记录实际 `placed.Side`，`DimensionPlan.SynchronizeFinalPlacementSides()` 在 Flush 后将最终侧回写 `PlannedDimension.Side`，同时保留 `RequestedPlacementSide`，因此计划、诊断和最终渲染统一表达 Right。仅涉及 `DimensionPlan`/诊断记录/调用点与 Core 回归断言，未修改 expectation、fixture 或 manifest。Debug-v9 三 DLL：`AutoFixtureDim.dll` `3E91E9D530AB1D67608CE8D511D7774F3D34A3513224D2E521E9F18FCD4153B6`、`CadAuto.Core.dll` `8D7CE13D33589967EF288B7E5E3EADE997B92FF21F06A456D741B2705A19EFB2`、`CadAuto.CadAdapter.dll` `3D01CC62ECE927F296F0FB72E3A0476E2D6D1D43EF5BE9976F5E7BF95317AC45`；Core `133/133` 通过。随后用同一 Debug-v9 串行直跑 DL01 四方向：`regression/dimension-layout/runs/DL01-b-sync-20260801-155807381/`，CAD exit `0`、trace `COMPLETE`、target validator `4/4`；Right 报告中的 L6=`48.5` 为 `placementSide=Right`、`requestedPlacementSide=Left`、`hasFinalPlacement=true`，证明最终渲染侧已回写计划语义；四张 full PNG SHA 分别为 Bottom `6BB06B62D117C97356C2D1FC841DD201F2D806C889DE74574F5906FA0B3DDB7F`、Top `0E7BE92A4EEC102BBE31DF3D55C62B8900449F2E51D9B452DAD124F91B194E4B`、Left `42DFEB4EAB154750A9A3B2DADDA788ED8062FA83B84291E6ACC2F16B28981384`、Right `C67BEBC4C542E463EB9B8E97AA5A2923AF016D3DB6A36923808A50B9795FEF49`。完整 nightly 仍受 CORE-P0/P1 输入 SHA 基线漂移阻断，不能据此宣称全量 nightly 通过。
- 2026-08-01 CORE-P0/P1 基线获批准并更新为 `8FC0CB041DF1F44D3153BB586FB2082519C9F8B0C234A25EAFA5106282517050`；真值矩阵 `17 fixed cases`、manifest `13 cases`、Core `133/133` 均通过。Debug-v9 官方 nightly `20260801-160804320` 已完成 Core-CAD `3/3`、DL01 `12/12`、HS01–HS03 `9/9`；Windows PowerShell 5 runner 在 HS04 repeat-1 产物完成后发生管道等待，已停止明确测试进程。随后使用同一 DLL、PowerShell 7 独立重跑 HS04–HS07，各 `3/3 Passed`、CAD/validator exit `0`、trace complete、hash 稳定；产品/expectation/fixture/manifest 未再修改。正式 full nightly 汇总仍待 runner 兼容性修复后重新生成。
- 2026-08-01 HS04 runner 契约修复：仅改 `scripts/run-hole-slot-cad-regression.ps1`，移除不稳定 stdout 自动选择文案门禁，改为唯一 `HS04_DATUM_POINT` trace 与唯一有效 Selected `DatumX`/`DatumY` report 契约；AST=0，最小真实证据 `regression/hole-slot/runs/hs04-contract-20260801-143000000/summary.json` Passed。独立二轮复核在 repeat-1 产物完成后 runner 未生成 summary 并超时，故不计为二轮通过，PowerShell 兼容性另行处理。
- 2026-08-01 PowerShell 7 runner 闭环：两个测试入口优先解析 `pwsh`、保留 Windows PowerShell 5 fallback；同时稳定排序 visual report 的 `environment` 属性，消除规范化哈希的属性顺序假差异。AST=0、PlanOnly 通过；同一 Debug-v9 正式 nightly `regression/nightly/runs/20260801-163902886/summary.json` `Passed`，36/36 records（Core-CAD 3/3、DL01 12/12、HS01–HS07 21/21），CAD/validator exit 0、trace complete、12/12 case determinism 通过、错误 0。DLL SHA：AutoFixtureDim `3E91E9D530AB1D67608CE8D511D7774F3D34A3513224D2E521E9F18FCD4153B6`、Core `8D7CE13D33589967EF288B7E5E3EADE997B92FF21F06A456D741B2705A19EFB2`、Adapter `3D01CC62ECE927F296F0FB72E3A0476E2D6D1D43EF5BE9976F5E7BF95317AC45`。未修改产品代码、expectation、fixture、manifest；HS04/HS05 保留各轮 1 条非阻断视觉 warning（falsePositiveTotal=6、falseNegativeTotal=0），未扩大门禁范围。
- 2026-08-01 M5-1 运营化配置：仅更新 `.github/workflows/cad-nightly.yml`，nightly preflight/run/publish 统一使用 PowerShell 7，保留 self-hosted `Windows/X64/autocad-2020`、串行 concurrency、3 轮、180 秒 case timeout、30 天 artifact 和 `always()` 证据上传；新增 summary contract 校验（`status=Passed`、`errors=0`、case 非空、visual determinism 全部通过、同批 DLL hash 至少 3 项）。现有 `core-ci.yml` 已满足 push/PR、3 分钟、Core-only 快速层，本轮不改。YAML/关键字段、现有 nightly summary contract、matrix/manifests、Core `133/133` 均通过；本地验证改动已提交到 `agent/m5-remediation`，仍需 GitHub self-hosted runner 实跑确认线上链路。
- expectation、产品行为和现有 fixture 的任何变更继续独立审批；M5 不将三者绑为同一项授权。

### M5 当前状态（2026-08-03）

| 分项 | 状态 | 证据边界 / 下一动作 |
|---|---|---|
| M5-1 本地配置与验证 | `Completed` | `agent/m5-remediation`；Core `133/133`，本地 Debug-v9 nightly `36/36`。 |
| M5-1 GitHub Runner 闭环 | `Completed` | workflow `30779057295`（提交 `1e848dd`）已在带 `Windows/X64/autocad-2020` 标签的 Runner 上完成；`36/36`、`status=Passed`、`errors=0`、12/12 visual deterministic、同批 3 DLL SHA 稳定，artifact `cad-nightly-30779057295-1` 已归档。 |
| M5-2 HS01～HS08 | `Completed (ready scope)` | 各 case 已有人工真值、fixtureReady、结构化证据和哈希；HS08 已完成真实三轮证据，不再重复补证。 |

- M5-1 GitHub Runner 闭环已通过；M5-2 HS01～HS08 ready scope 已完成。该登记只记录运营证据，不改变产品代码、expectation、fixture 内容或已确认基线。
- Runner 失败时只调查 workflow、环境、凭据、超时和 artifact 链路；不得用修改 expectation、fixture 或基线的方式消除失败。
- 2026-08-03：自托管 Runner 恢复后触发 `30777968411`（提交 `9c6e0a0`）。CAD suite `36/36`、`errors=0`、CAD/validator 全部成功，三 DLL SHA 稳定；artifact `cad-nightly-30777968411-1` 已归档。但视觉契约仅 `10/12` deterministic：OC01 与 DL01 Bottom 的 repeat-1 全图分别为 `0DE188D821A0C2E414AFBCC3567F14D7362BB1B32A09EFFD88A02BD92574EF16`、`597F8A100F4F1D7692D2248BE2A9CC0E4C0B693B29B7BA5138CA4F68AEC2BB3A`，repeat-2/3 与已批准哈希一致；两项结构化 normalized report SHA 在三轮均稳定。summary contract 因此失败，本轮不计 M5 通过。初步归因是 `REGEN`/`ZOOM`/`PNGOUT` 连续调用造成首轮截图未稳定；需单独授权测试入口校准，禁止修改产品行为、expectation、fixture 或基线。
- 2026-08-03：按授权仅在两个测试入口的 PNGOUT 前加入 AutoCAD 显示稳定等待；未修改产品行为、expectation、fixture、manifest 或基线。workflow `30779057295`（提交 `1e848dd`）重跑成功：`36/36`、`errors=0`、12/12 visual deterministic、visual FP/FN `6/0`（仅 HS04/HS05 各轮既有非阻断 warning，未新增阻断）、同批 Debug-v1 三 DLL SHA 稳定；artifact `cad-nightly-30779057295-1` 已归档。本轮 M5-1 Runner 闭环通过。
- 2026-08-03：用户确认 HS08 transformed waist slot 全图正确；仅修正 Hole/Slot 测试入口纳入 HS08，并将其圆实体前置条件豁免为腰槽适用路径。HS08 fixture `791F706C84A5421F265B68039D6354D51179422595D1DA44CF6DDDF85C543F3F` 登记为 `fixtureReady=true`、`ConfirmedCorrect`。ready batch `regression/hole-slot/runs/hs08-ready-20260803-112239371/summary.json` 三轮 `Passed`，CAD/validator exit 0、trace complete、`SlotCenter=40`、`SlotDatumH=30`、`SlotChainV=50`、visual warning 0、`blocking=false`；normalized report `9C11C0886079775C72D4A7EED2755091AE31C1105A0581BB985F1147FEBF8523`、normalized visual `A4BAAA7B2619AA96CEAE73CD2C8EF18E72046E2E03BF2B035D6731B700F0AF69`、full `BF824A8B58E309B7979CFC0690FC5A001B31BE4203989E5BA5B31033F4C9C67B`、trace `677133C66547DBB16605B1092CD88692DAC4D7D1D9C63DF64A0031A7F011BA71` 稳定；Debug-v3 三 DLL SHA 已登记于 truth matrix。未修改产品代码、expectation 或 DWG 内容。

### M5 进入条件

- M4 当前阻断配置、历史 WarningOnly 矩阵和证据包保持冻结，现有 5 个 ready case 的三轮结果可复核。
- 已确定 PR 快速层与 nightly 的配置文件、artifact 保留位置、DLL 哈希采集和失败升级责任边界。
- 对每个拟纳入的 Hole/Slot case 已取得对应正确图片与诊断；HS05 未满足时仅可停留在证据收集。

### M5 退出条件

- PR 快速层和 nightly 均可从版本化配置复现，摘要可追溯至 artifact 与同批 DLL 哈希；失败能保留完整证据并按既定路径处理。
- 所有纳入统计的 Hole/Slot case 均逐 case 满足真值、fixture/hash、结构化和视觉证据；其余 case 显式为未 ready，不计通过。
- 自托管 Runner 的运营检查不改变产品输出，连续运行仍保持同一 DLL 下的确定性要求。

### M5 回滚边界与不在范围

- 回滚限于门禁配置、编排和 Runner 运营设置，回退到最近已验证配置；不回滚或覆盖产品行为、expectation、fixture、M4 历史证据和 artifact。
- 不在范围：依据缺失图片或诊断修复 Hole/Slot/HS05 产品逻辑；重写测试框架；新增未获批准的 fixture 真值；将未 ready case 计入通过。

单人实施粗估 4～6 周；每个里程碑必须独立可验证、可回退，不以工期压力跳过真值确认。

## 8. 开工前决策清单

在任何产品代码修改前，必须先由负责人确认：

- [x] DL01 四方向的正确目标图或正确报告；2026-07-30 已完成 target 4/4 与 full/detail 图片确认。
- [ ] HS05 的真实候选数量和产品交互语义；图片诊断完成前禁止修改。
- [x] `ConfirmedCorrect` 首批范围为 P0、P1 和 OC01。
- [x] `ConfirmedCorrect` 第二批范围为 DL01 Top、Bottom、Left、Right。
- [x] 首批迁移仅限 OC01 外轮廓台阶规则族。
- [x] expectation 变更仅由用户本人审批。
- [x] nightly AutoCAD 门禁延至 M3 决策执行。
- [ ] PR 快速层 3 分钟预算的标准机器配置仍未决。

DL01 第二批证据记录：

- Bottom：`20260730-130500001`；CAD exit 0、COMPLETE、selection 98、schema 2、finals 31、warnings 0；target PASS；图片确认无 33。
- Top：`20260730-130500002`；CAD exit 0、COMPLETE、selection 98、schema 2、finals 31、warnings 0；target PASS；图片确认三层顺序正确。
- Left：`20260730-130500003`；CAD exit 0、COMPLETE、selection 98、schema 2、finals 31、warnings 0；target PASS；图片确认无 48.5。
- Right：`20260730-130500004`；CAD exit 0、COMPLETE、selection 98、schema 2、finals 31、warnings 0；target PASS；图片确认存在 48.5。
- `bin/Debug-v3/AutoFixtureDim.dll` SHA256：`B145FDCCC2C5DFA05FCBEA5E8886BC3AD27B4A577D0366EFFD2762A53990CBED`。
- `bin/Debug-v3/CadAuto.Core.dll` SHA256：`4D20C161DF40BFB0C5B67C6D498438D42F9EBC7EA5B2FCA1BB1CFD0BA34E9498`。
- `bin/Debug-v3/CadAuto.CadAdapter.dll` SHA256：`D3961973C6F6152789B969BCFF9EC607803B6602D29D774B4CB959DC03CC6857`。

以上 DL01 单次证据已由后续 M3 三轮 nightly 再次覆盖；Hole/Slot 仍为 0 个 ready case，不计通过也不阻断当前 ready 集合的 M3 闭环。

M3 nightly 配置记录：

- 新增 `.github/workflows/cad-nightly.yml`：仅使用带 `autocad-2020` 标签的 Windows 自托管 Runner，定时或手动串行执行。
- 新增 `scripts/run-cad-regression-suite.ps1`：复用现有校验器与 case runner，固定同一套 DLL，按 Core-CAD、DL01、Hole/Slot 顺序执行，默认连续三轮并归档摘要。
- `scripts/run-core-cad-regression.ps1` 增加可选 `CoreConsolePath`、`Language`、`Profile` 参数，并规范进程级 `Path`，供 nightly Runner 显式指定 AutoCAD 环境。
- `-PlanOnly` 已确认当前清单为 Core-CAD 1、DL01 4、Hole/Slot 0、重复 3 轮；脚本 AST、workflow YAML 与 diff whitespace 检查通过。
- Repository Runner `CAD-AUTO-L1-PC-20260323IMXG` 已注册并在线，标签为 `self-hosted / Windows / X64 / autocad-2020`；默认分支已切换为 `refactor/v2-roadmap`。
- 首次完整通过：[GitHub Actions run 30519843692](https://github.com/Whawk77/CAD-AUTO-L1/actions/runs/30519843692)，提交 `421b2bda2926c8e7d4ea2513e9f2a465eeb5d64c`，批次 `20260730-143203268`。
- 同一套 DLL 串行完成 5 个 ready case × 3 轮：15/15 Passed、CAD exit 0、DL01 validator exit 0、五组规范化报告各只有 1 个哈希、errors 0。
- 三个 DLL SHA256 与 M2 `Debug-v3` 完全一致；DL01 四方向 full/detail PNG 的三轮文件哈希均唯一且与已人工批准图片一致。
- 远端证据包 `cad-nightly-30519843692-1`（artifact `8750361702`）已上传，183 个文件，本机复核 15 个 result、15 个 report、15 个 trace、15 张 full PNG、12 张 detail PNG，必需证据 72/72。
- Hole/Slot manifest 当前 0 个 `fixtureReady=true` case，摘要明确记录 Skipped，没有伪装为通过；M3 按当前 5 个 ready case 的授权范围完成。

| 规则 | 正例 | 相邻反例 | 镜像/平移等价例 | fixture |
| --- | --- | --- | --- | --- |
| `OuterContourStepOverallRemainder` | `BottomOuterContourStepKeeps20AndSuppresses70Body` | `BottomBodyWidthNotDroppedByLongestExtension` | `TranslatedOuterContourStepKeepsRuleDecision` | `regression/core-cad/fixtures/OC01-bottom-outer-step-partition.dwg` |

未确认事项只阻断其对应范围，已确认的 OC01 首批迁移可继续；对未确认范围只允许收集证据和完善测试，不允许推断产品缺陷或修改产品行为。
