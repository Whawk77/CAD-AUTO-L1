# Handoff — 尺寸规则改进（2026-07-20）

> 新对话请先读本文件，再读 `docs/DimensionRules.md`，需要时用 graphify。

## 仓库与分支

- 路径：`D:\work\AI\project\L1-grok`（git worktree，主库在 `D:\work\AI\project\L1`）
- 远程：`https://github.com/Whawk77/CAD-AUTO-L1.git`
- 分支：`dev-7`（跟踪 `origin/dev-7`）
- 关键提交：`419ef49` — Improve dimension planning rules for structure, holes, and overall.
- 基线：`39226ee` 基线1

## 工作约定

1. **优先用 graphify**：`graphify-out/graph.json` 已更新；代码结构问题先 `graphify query` / `path` / `explain`，再读源码。
2. 改完代码后可 `graphify update .`（代码 AST，无 LLM）。
3. 中文回复；项目 Claude 约定末句可加 `--喵`（若沿用该规范）。
4. 保留 `docs/`（含本地笔记）；勿批量删目录。

## 本次已落地的规则

### 1. 销对之间的孔 → 功能孔

- 非销孔若投影**严格落在**销对线段内、且靠近销轴 → 归该销组。
- 定位从**基准销 BasePin** 出发，`DebugRole=FunctionalHole`，owner `PGn`。
- 不再进散孔 `LooseHole` 长链（避免与销组 93 等重复）。
- 代码：`IsHoleBetweenPinPair`、`BuildFunctionalHoleGroups` 后处理。

### 2. 同侧结构段对齐（方案 1）

- 结构尺寸共享 rooted alignment key（priority 70）：
  - Top `Structure:T:H` / Bottom `Structure:B:H`
  - Left `Structure:L:V` / Right `Structure:R:V`
- 无 key 时相邻同特征仍可能因“共端点外扩”分层；有 key 则走 `RootedAlignmentLane`。

### 3. OutlineSegment 复述 overall

- `AddLinearSegmentDimensions` 会为非全长边生成 `OutlineSegment`。
- **抑制**：同侧两条 OutlineSegment（或与同侧 Normal）满足 `A+B≈Overall` → 两条都抑制。
- 原因：`OutlineSegmentOverallPartition`。
- 注意：不要把通用 complementary 扩到 Bottom/Left 全体 Normal（会误伤 SlotDatum 等）。

### 4. 结构宽/高 vs overall — 规则过大已修正

**错误做法（已撤销）**：结构点在包络边（Y≈MaxY/MinY 或 X≈MinX/MaxX）一律忽略。  
→ 过严，会杀掉合法定位如 `GEN|TSW1|GB|T|L1`（局部顶边台阶 50）。

**正确做法（当前）**：

| 保留 | 抑制 |
|------|------|
| 局部结构宽/高（如 TopStructWidth=50，凑不成 overall） | 两条结构尺寸 `A+B≈Overall`（可跨侧），原因 `StructureOverallPartition` |
| 例：25+232=257 → 25 与 232 都删，只留 Overall | |

- 代码：`SuppressStructureDimensionsThatPartitionOverall`、`IsStructureWidthOrHeightRole`
- **不要**再加回“包络点一律丢弃”除非产品明确收紧且有回归用例。

### 5. 文档与测试

- 规则说明：`docs/DimensionRules.md`
- 核心逻辑：`CadAuto.Core/V177Source/CadAuto.Core.Planning/DimensionPlanner.cs`
- 测试：`CadAuto.Core.Tests/Program.cs`（约 77 项应全过）
- 构建：VS2022 BuildTools MSBuild；AutoCAD 锁 `bin\Debug` 时可输出到 `bin\Debug-*`，或 `deploy_next_version.ps1` 出 `autofixdim-vNNN.dll`

## 调试标签速查

- `GEN|TSW*|GB|T|L?` → TopStructWidth  
- `GEN|OutlineSegment*|…` → 轮廓段（易与 overall 拆分纠缠）  
- `FH` / `PD` / `GD` / `LH` → 功能孔 / 销距 / 组距 / 散孔  
- 诊断文件：`diagnostics/last-run.json`（运行时，通常勿提交）

## 未提交/本地杂项（推送时有意未带）

- `diagnostics/last-run.json`
- `graphify-out/2026-07-20/` 备份
- 大量新增 `graphify-out/cache/ast/**`

## 新对话推荐开场白（可复制）

```
请先阅读 D:\work\AI\project\L1-grok\HANDOFF-2026-07-20-dimension-rules.md
和 docs/DimensionRules.md。工作在 dev-7 分支，优先用 graphify 查代码再改。
继续尺寸规则/结构宽/功能孔相关工作。
```

## 已知注意点

- 插件 worktree 的 `.git` 指向 `L1/.git/worktrees/L1-grok`。
- AutoCAD 占用 DLL 时勿硬写 `bin\Debug`，用版本化 DLL 加载。
- 结构分区只杀“合起来等于 overall”的**结构**对；局部 TSW 必须保留。
