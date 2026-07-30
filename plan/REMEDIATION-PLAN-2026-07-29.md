# CAD 自动标注规则与回归体系整改计划

> 日期：2026-07-29
> 状态：实施中；M3 已完成；进入 M4/阶段 6，视觉检查先报告、暂不阻断
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

M4 首批只以 warning/report 形式检查碰撞、穿越和布局，不改变产品标注、不修改 expectation，也不阻断现有 Core 或 CAD 门禁；待误报率和正确图片完成验收后再单独申请升级为阻断。

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
- M4：阶段 6，人工验收收敛为首次批准与发布抽检；已进入非阻断视觉检查阶段。

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
