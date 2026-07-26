# 重构验证手册（分支 `refactor/v2-roadmap`）

> **对象**：`refactor/v2-roadmap`，基于 `dev-7`（`c61ced6`），共 49 个提交
> **编写日期**：2026-07-27
> **依据**：`CAD-AUTO-L1-架构评估报告-v2.md` 的「立刻做 + 近期做」两档，外加 v2 核实新增的 V1/V2/V3
> **本机限制**：改动全部在 macOS 上完成，**未经任何编译或运行**。本文档列出的每一项都需要在 Windows + AutoCAD 2020 环境执行。
> **测试方式**：一次性在 HEAD 上验证。中间提交 `1d61afc` 是**故意红**的复现用例，下一个提交 `3ae2dae` 修复它——请勿单独在 `1d61afc` 上构建。

---

## 一、五分钟速览

| 阶段 | 内容 | 是否改变出图 |
|---|---|---|
| 第 1 步 | 编译三程序集 + 跑 Core 测试（**117** 个用例） | — |
| 第 2 步 | 零行为验证：DL01 出图必须与 dev-7 基线**逐条一致** | 否（金标准） |
| 第 3 步 | 功能点验证：诊断、清理、部署、螺纹、腰槽、包络 | 是（逐项列出预期变化） |
| 第 4 步 | 四方向回归 + 期望文件是否需要重新基线的判定 | 待定（见第六节） |

**用例总数从 116 变为 117**（新增 P11 复现用例）。任何写着 "116/116" 的旧文档或脚本请按 117 理解。

---

## 二、构建与 Core 测试

```powershell
# 新增了 .sln，可以一键构建四个工程
msbuild AutoFixtureDim.sln /p:Configuration=Debug /v:minimal

# 或按旧方式逐工程构建
dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /p:PostBuildEvent= /v:minimal
```

```powershell
.\scripts\run-core-tests.ps1
```

**预期**：末行 `CadAuto.Core.Tests: 117/117 passed.`，退出码 0。

测试宿主本轮已改造，现在的行为是：

- 支持过滤：`.\scripts\run-core-tests.ps1 -Filter Slot`（用例名子串）或 `-Filter P3`（层级）。
- P1–P3 层的失败**不再中断整轮**，逐条打印 `FAIL P<层> <用例名> :: <消息>`；P0 层保留 fail-fast。
- 末行始终打印 `n/总数`，失败时也打印（改造前失败会导致汇总行完全不输出）。

**如果有用例变红**：把 `FAIL` 那一行原文发回。消息文本本身足以定位，不需要再猜。特别关注这三类：

| 若失败的是 | 说明 | 处理 |
|---|---|---|
| `TwoArcSlotIsRecognized` / `SingleArcSlotIsRecognized` | 我给三个腰槽夹具补的 `Bulge = -1.0` 判断有误 | 停下，发我失败原文 |
| `FunctionalHoleAlignmentLaneSurvivesOutwardPromotion` | 若消息是 "scenario did not trigger an outward promotion"，说明复现场景没触发，该用例不成立（**不是**产品缺陷） | 发我原文，我重构场景 |
| 任何倒角/圆角用例 | 双识别器同步暴露的既有分叉 | 发我清单，逐条裁决期望行为 |

---

## 三、零行为验证（金标准）

这是判断"重构是否干净"的唯一可靠标准，**与四方向期望文件无关**。

### 3.1 采基线（如已在上一轮采过可复用）

用 **dev-7 原始 DLL** 在 `regression/dimension-layout/fixtures/DL01-four-side-dimension-layout.dwg` 上跑一次 `ASD4`，归档：

- 出图截图
- `diagnostics/last-run.json`

### 3.2 用新 DLL 重跑同一张图同一命令

**预期：图上标注逐条一致。**`last-run.json` 允许且仅允许出现以下差异：

| 差异 | 原因 | 提交 |
|---|---|---|
| `runId` / `generatedAt` 变化 | 每次运行不同 | — |
| `finalDimensions` 条目**减少** | 渲染阶段被删除的尺寸不再谎报为 Selected | `5aa19a3` |
| `dimensionCandidates` 新增 `decisionStatus="Skipped"` 条目 | 规划前被丢弃的候选现在有记录 | `f93a593` |
| 新增 `decisionStatus="RenderSuppressed"` 条目 | 同上，渲染阶段一端 | `5aa19a3` |
| `environment` 新增 4 个字段 | 字高/箭头真值源观测 | `34a425b` |
| 新增顶层 `nonFiniteValueCount`、`warnings` | JSON 守卫与非致命告警通道 | `5d0e616` `b6cab4d` |

> **关键对照**：`finalDimensions` 的条数现在应当**等于**图上实际画出的 AUTOFIXDIM 标注实体数。dev-7 上报告数应当**大于等于**实际数。这一个数字就能证明"报告变诚实了"而非"图变了"。

如果**图本身**变了，请把变化的尺寸列出来——那才是真回归。

---

## 四、功能点验证

按下表逐项执行。除非注明，都在一张常规夹具零件图上进行。

### 4.1 诊断与报告（零出图变化）

| # | 操作 | 预期 | 提交 |
|---|---|---|---|
| 1 | 任意图跑 `ASD6`（只出孔类） | `last-run.json` 中能看到 `decisionStatus="RenderSuppressed"`、`decisionReason` 以 `OutOfCommandScope:` 开头的轮廓类条目 | `5aa19a3` |
| 2 | 喂一张会触发 PostValidator 失败的图 | 命令行提示带**异常类型名**；`last-run.json` 被刷新（不是上次的旧内容），且含顶层 `error` 节点（`type`/`message`/`stackTrace`） | `a01b10e` |
| 3 | 任意成功运行后检查 JSON | 可被 `ConvertFrom-Json` 完整解析；含 `nonFiniteValueCount`（正常为 0）与 `warnings` 数组 | `5d0e616` |
| 4 | 检查 `environment` 节点 | 含 `resolvedDimStyleName`、`styleDimscale`、`effectiveTextHeight`、`effectiveArrowSize` | `34a425b` |

> 第 4 项只是**采数据**，不改任何布局判定。请在几张不同图幅/不同 DIMTXT 的图上收集，用于将来决定是否统一字高真值源（报告 P9 第二、三步，本轮未做）。

### 4.2 XData 与清理（**有行为变化**）

| # | 操作 | 预期 | 提交 |
|---|---|---|---|
| 5 | **AG1 → ASD4** | **AG1 的粗糙度块与 CNC 加工文字保留下来**（这是本轮修复的真实缺陷） | `090ea9a` |
| 6 | 接上一步再跑 `ASD3` | 尺寸标注仍能正常清除 | `090ea9a` |
| 7 | 把已生成的标注和零件一起做成块，再跑 `ASD4` | 块定义内的标注**不被擦除**，该块的其它插入不受影响 | `090ea9a` |
| 8 | 在**从未跑过** ASD 的空白新图上直接执行 `ASDCOREDBG` | 有调试标注产出，**不报 `eRegappIdNotFound`** | `c6f8254` |
| 9 | 接上一步执行 `ASD3` | 调试标注被清除（groupId 已改为时间戳） | `c6f8254` |
| 10 | 检查任意新生成标注的 XData | 含 `Kind`（`Dimension`/`Ag1`/`CoreDebug`）与 `SchemaVersion=1` | `855a4c5` |
| 11 | 在含**旧版**插件标注的图上跑 `ASD4` | 旧标注（无 `Kind` 字段）仍能被清除——向后兼容回落 | `090ea9a` |

### 4.3 识别（**有行为变化**）

| # | 操作 | 预期 | 提交 |
|---|---|---|---|
| 12 | 含 M6/M8/M10 螺纹孔的真实图跑 `ASD` | 标注为 `M6`/`M8`/`M10`，**不是** `%%c5` 之类的小径直径 | `d9b02b3` |
| 13 | 含 **260°–270° 非螺纹圆弧**的图 | 这些弧**不被**误判为螺纹（容差 2.0°，接受带 268°–360°） | `d9b02b3` |
| 14 | `HS07-vertical-waist-slot.dwg` / `HS08-transformed-waist-slot.dwg` | 腰槽数量与 R 引线位置**不变** | `2976261` |
| 15 | 含 **R<1 小圆弧**的图 | 半弧接受带收紧为 165°–195°，小半径弧不再被过宽地判为槽端弧 | `2976261` |
| 16 | 含大直径销孔标记块的图 | 销孔仍被识别（V2 的 1.0 常量保持原值未动） | `8985e26` |

### 4.4 包络与失败回退（**有行为变化，风险最高**）

| # | 操作 | 预期 | 提交 |
|---|---|---|---|
| 17 | DL01 跑 `ASD4` | `last-run.json` 里 selection 包围盒与总长/总高数值**完全不变** | `34b3061` |
| 18 | 含**镜像圆弧**的图 | 不再抛 "envelope does not match" 异常 | `64715d4` |
| 19 | 含**带宽度多段线**的图 | 同上；包围盒按中心线（顶点）而非含半宽的 GeometricExtents | `34b3061` |
| 20 | 框选中包含**独立圆**（不构成轮廓线弧）的散线回退场景 | 命令行提示"已排除 N 个不含线/弧几何的对象"，且不再因此抛异常 | `34b3061` |
| 21 | 若出现 `warnings` 中的 `StoredEnvelopeReconciled` | 命令**继续完成**而不是整条归零；把该条 warning 原文发我 | `b6cab4d` |

> 第 18–20 项是本轮最需要真实图纸的地方。报告 P10 判定这是"整条命令归零且无任何回退"的唯一硬失败点，现在应当已经消失。

### 4.5 部署与加载

| # | 操作 | 预期 | 提交 |
|---|---|---|---|
| 22 | NETLOAD 后看命令行 | 打印全部命令名 + **完整程序集路径**，路径与刚构建的 `Debug-vN` 一致 | `df1598f` |
| 23 | 临时移走源目录的 `CadAuto.Core.dll` 再跑部署脚本 | 立即 `throw "缺少 ... 无法部署"`，不产出残缺版本 | `e7e5f8a` |
| 24 | 设置 `$env:AUTOFIXDIM_DEPLOY_DIR` 后跑部署脚本 | 部署到该目录，不再依赖硬编码的个人机路径 | `e7e5f8a` |

---

## 五、回归设施验证

### 5.1 布局回归报告新鲜度

```powershell
# 正常流程：跑一次 ASD4 后校验
.\scripts\validate-dimension-layout-report.ps1 -CaseId DL01-bottom-block-span-order

# 新鲜度守卫：不重跑 ASD4，直接用当前时间做运行起点再校验
.\scripts\validate-dimension-layout-report.ps1 -CaseId DL01-bottom-block-span-order -RunStartedAt (Get-Date)
```

**预期**：第一条按内容通过/失败；第二条必须**失败**并提示 `stale report`——证明新鲜度门控生效。

校验器现在还会拒绝：`runId` 为空、报告含 `error` 节点、`drawing.sha256` 与夹具哈希不符。

LSP 侧新增可选门控：加载 LSP 后、执行 `V203*` 前设置

```lisp
(setq *v203-report-file* "<项目根>\\diagnostics\\last-run.json")
```

则只有报告文件在本次运行中确实被改写才写 `COMPLETE`，否则写 `ERROR stale-report side=<方向>`。不设置则保持旧行为。

### 5.2 hole-slot 回归（首次跑通）

这条链路**从未真正运行过**。现在已修好：`HS05` 的 fixture 路径原先指向磁盘上不存在的文件（已改为实际的 `HS05-unique-pin-datum.dwg`），8 条 case 全部补上了 `fixtureSha256`，校验脚本新增夹具存在性、哈希、`fixtureReady` 与运行身份校验。

```powershell
# 首次跑通必须显式放行 fixtureReady=false
.\scripts\validate-hole-slot-report.ps1 -CaseId HS01-normal-hole -AllowNotReady
```

**重要纪律**：

- 首次运行**大概率暴露若干真实失败**，请预留排查时间。
- `fixtureReady` 只有在该 case **真正跑通**之后才改成 `true`，**一条一条改**。
- 绝不要为了让脚本变绿而批量把 `fixtureReady` 置 true——那会把仅存的防线也废掉。

**待你确认**：`HS05` 的 case id 是 `HS05-multiple-pin-datum`（语义：多个候选需用户确认），但磁盘文件名是 `HS05-unique-pin-datum`（语义：唯一候选自动选中）。请打开该 DWG 数一下合法销孔候选数，然后决定是改 case 语义还是改文件名。我只改了指向,没有动语义。

### 5.3 CI

新增 `.github/workflows/core-ci.yml`，仅构建并运行 `CadAuto.Core.Tests`（零 AutoCAD 依赖），另含两条仓库守卫：不得跟踪 `bin`/`obj`/`obj-testN` 产物、AutoCAD 引用必须 `Private=False`。

需要 push 分支后才能观察首次运行。**绝不要**在 CI 里构建 `AutoFixtureDim` 或 `CadAuto.CadAdapter`——AcMgd 在托管 runner 上不存在。

---

## 六、四方向回归与期望文件（待判定，尚未解决）

上一轮实测结果为 1/4 通过，但**dev-7 原始 DLL 也没通过它自己的基线**。仓库证据支持"期望文件过时"这一解释：

- 期望文件最后更新于 2026-07-20（`39226ee`）
- 此后 `DimensionPlanner` 在 dev-7 上又有 **9 个提交**（7-21 至 7-24），其中四个直接改抑制行为

因此四方向失败**不构成对本分支的判决**。要定性，需要下面任一条数据：

**方案 A（最省事）**：把 Top/Left/Right 三个方向的**校验器报错原文**发我。判别规则：

| 报错形态 | 含义 |
|---|---|
| `Block 'X' expected at least N members, found M` | 计数短缺 → 是 `5aa19a3` 让 `finalDimensions` 变诚实的效应（报告变了，图没变） |
| `split across multiple physical coordinates` / `outward order` / `stacking level` / `layer step` | 顺序或几何不符 → Planner 行为漂移对上过时期望，与本分支无关 |

**方案 B（最彻底）**：用 dev-7 的 DLL 跑 Top/Left/Right，对同一批 v203 目标校验。dev-7 若同样失败 → 期望过时，本分支干净。

**在判定之前，请不要重新基线期望文件。** 重新基线是迟早要做的（dev-7 已证明其过时），但那必须是一次独立、有签字的任务，且要从一次人工确认过出图的运行里采集。

---

## 七、悬而未决的决策

| # | 事项 | 需要什么 |
|---|---|---|
| 1 | `FeatureRecognizer.cs` 的 `PinMarkerFallbackHitRadius = 1.0`（原写法 `Math.Max(radius * 0.0, 1.0)`，半径项被乘零） | 一张含大直径销孔标记的图；确认 1.0 是意图值还是本该随半径缩放 |
| 2 | `HS05` 夹具语义（见 5.2） | 打开 DWG 数销孔候选 |
| 3 | 螺纹弧容差是否从 2.0° 放宽到 10.0° | 先按 2.0° 跑一版真实图纸 |
| 4 | 报告 P9 第二、三步（统一字高/箭头真值源） | 先用 4.1 第 4 项采集的数据判断差异是否真实存在 |
| 5 | 四方向期望文件重新基线 | 先完成第六节的判定 |

---

## 八、本轮未做的事（有意为之）

以下来自 v2 报告「长期考虑」与「暂不建议做」，本轮**明确不做**，理由见报告对应章节：

- 合并双识别器（只同步了已分叉的判据，未做结构合并）
- 切除 Adapter 侧不带句柄的模型
- 把布局编排整体下沉到 Core
- 几何快照 + Core 离线回放
- 交互式 jig 的事务拆分
- 外部配置文件、SDK-style csproj 迁移、`Directory.Build.props`、启用 NRT、全局变量重命名、重写 git 历史

另有两项属于本轮范围但依赖你的决策，故未动：报告 P9 的第二、三步，以及 ASD3 的跨命令语义（当前保持"清除最近一次生成的组"，与 `docs/Environment.md` 的描述一致）。

---

## 九、提交清单

按提交顺序（`git log --reverse dev-7..HEAD`）：

| # | 提交 | 说明 |
|---|---|---|
| 1 | `a3d7398` | 785 个构建产物 + 生成的 `cad-test.scr` 移出 git 索引（磁盘未动） |
| 2 | `f03724e` | 文档修正：5 处 `RuleConfig.cs`、V177Source、识别器归属、字段名 |
| 3 | `eee575c` | 测试宿主：per-test try/catch、名字/层级过滤、`n/总数` 汇总 |
| 4 | `e0bc37c` | 测试构建保留符号；脚本透传 `-Filter` |
| 5 | `5b401a2` | 文档：删除不必要的 `obj-testNNN` 中间目录覆盖 |
| 6 | `acf8205` | 删除死文件 `AutoCadGeometryConverter`（零引用且 `ToCore(Arc)` 不写 Bulge） |
| 7 | `38d7c14` | 删除零引用的 `BoundingBox2D` |
| 8 | `a07099c` | `DimensionDrawer` 删除 17 处零调用转发方法（逐个 grep 核实） |
| 9 | `72c2c5e` | `DimensionPlanner` 删除 20 个死方法（4891→4625 行） |
| 10 | `37530f2` | 清理三处反编译残留（`\|\| 1 == 0` 等） |
| 11 | `c272af9` | 8 个纯函数改 `internal` 以便单测 |
| 12 | `4525738` | Bottom 缺失互补余量路径标注为有意（`ponytail:`） |
| 13 | `4ee65d4` | 两个 internal 直调测试补 `SuppressedReason` 断言 |
| 14 | `eb7eda0` | `Mark` 加 `using` 释放 `ResultBuffer`；`AppName` 常量去重 |
| 15 | `855a4c5` | XData 写入 `Kind` 与 `SchemaVersion` |
| 16 | `c6f8254` | `EnsureRegApp` 收进 `CadEntityWriter` 构造；ASDCOREDBG groupId 改时间戳；ASD 补 `InvariantCulture` |
| 17 | `5d0e616` | JSON 的 NaN/Infinity 与 <0x20 控制字符守卫 |
| 18 | `a01b10e` | 失败路径也写 `last-run.json`，含 `error` 节点 |
| 19 | `34a425b` | 诊断 environment 增加解析后的样式度量 |
| 20 | `5aa19a3` | **P1**：渲染阶段抑制写入诊断 |
| 21 | `f93a593` | **N1**：规划前丢弃写入诊断 |
| 22 | `df1598f` | NETLOAD 打印全部命令与程序集路径 |
| 23 | `e7e5f8a` | 部署脚本三件套校验 + `AUTOFIXDIM_DEPLOY_DIR` |
| 24 | `09e3263` | 标注 `FeatureRecognizer2D` 为仅供测试 |
| 25 | `b164228` | 测试识别器倒角邻边对齐生产的双组合 |
| 26 | `f53accd` | 测试识别器低凸度弧中点落在圆周 |
| 27 | `b2f7a0d` | `DimensionDrawer` 轮廓深拷贝记忆化 |
| 28 | `1b2920a` | 螺纹弧确认一次性物化圆列表 |
| 29 | `093d0bf` | **N3**：四方向 `IgnoredPoint` 统一并携带理由 |
| 30 | `02848f4` | 23 趟抑制流水线文档化；18 个理由字面量收进常量表 |
| 31 | `02a4e64` | `IsHoleDimension` 序数区间展开为显式 switch |
| 32 | `2b5ea02` | `DimensionPlanner` 拆成 5 个 partial 文件 |
| 33 | `4a07086` | 布局回归校验绑定到本次运行 |
| 34 | `dcbd59d` | hole-slot 回归链路修复 |
| 35 | `85a26c7` | 新增 `AutoFixtureDim.sln` |
| 36 | `e89762c` | 新增 Core CI 与两条仓库守卫 |
| 37 | `47597fa` | 文档：报告新鲜度门控说明 |
| 38–39 | `0b8938b` `99f2cdf` | 修正镜像包络断言（原断言为空真，详见下方注） |
| 40 | `d9b02b3` | **P2**：`IsThreadArc` 改用角度容差 |
| 41 | `4034728` | 生产侧倒角邻边补引用不等守卫 |
| 42 | `2976261` | **P8-3**：`IsHalfArc` 三处判据统一到扫掠角 |
| 43 | `090ea9a` | **P3**：清理白名单 + 只走布局空间 |
| 44 | `8985e26` | V1 前置条件、V2 具名常量（零行为变更） |
| 45 | `34b3061` | **P10-1**：包络单一来源 |
| 46 | `64715d4` | **P10-2**：圆弧方向改用带符号 bulge |
| 47 | `b6cab4d` | **P10-3**：包络不符改为回退而非中止 |
| 48 | `1d61afc` | **P11** 复现用例（单独构建时预期为红） |
| 49 | `3ae2dae` | **P11** 修复：对齐通道整体外推 |

> **关于 38–39**：`MirroredEnvelopeStructureWidthsAreBothSuppressed` 的第二条断言长期是**空真**——52 的镜像候选在候选构建阶段就被丢弃（15.54+52+23 恰好完整分割总长 90.54），从未进入计划，因此改动前 `DimensionCandidates` 里根本没有 52 的条目，`.All()` 对空集恒真。`f93a593` 让丢弃可见后它才第一次被触发。现在的断言要求两侧候选都必须在诊断中留下痕迹（带原因的 Skipped 或带包络理由的抑制），严格强于原来的空断言。产品行为自始至终未变。

---

## 十、回滚指引

每个工作项都是独立提交，可单独 `git revert`。风险最高、最可能需要单独回滚的是：

| 提交 | 若出现什么现象就回滚它 |
|---|---|
| `2976261` | 腰槽识别数量变化且经确认是错的（半弧接受带收紧） |
| `d9b02b3` | 非螺纹圆弧被误判为螺纹（可先只把容差从 2.0 调回更小值） |
| `34b3061` `64715d4` | 包围盒或总长/总高数值变化 |
| `b6cab4d` | 需要恢复"包络不符即中止"的严格语义 |
| `3ae2dae` | 四方向布局观感不可接受（对齐通道整体外推） |
| `090ea9a` | 清理范围变化引发问题（可分别回退 kind 白名单与 `IsLayout`，两者在同一提交内） |
