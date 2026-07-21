# HANDOFF 2026-07-21 — Overall partition / envelope / slot chain / Point2D snap / complementary snap

## 工作区与分支

| 项 | 值 |
| --- | --- |
| Worktree | `D:\work\AI\project\L1-grok` |
| 分支 | `dev-7` |
| 远程 | `origin/dev-7` → `https://github.com/Whawk77/CAD-AUTO-L1.git` |
| 已推送提交（本会话相关） | `cab2979`（前期 partition/slot 对齐）、`5d65f09`（DFS + 结构 suppress 回归修复） |
| 本会话后半未推送 | Phase 2（Point2D sentinel）、Phase 3（Complementary Snap 收紧）及 v12/v13 相关本地改动 — **若 working tree 有未提交 diff，下会话先 `git status` 确认** |
| 本地未跟踪 | `.grok/`、`tmp_dbg_partition/`（调试临时目录，勿提交） |

## 最新可测 DLL

| 版本 | 路径 | 内容概要 |
| --- | --- | --- |
| **Debug-v13** | `bin\Debug-v13\AutoFixtureDim.dll` | 含 Phase 2 + Phase 3（推荐手测） |
| Debug-v12 | `bin\Debug-v12\` | L 形共线修复后 |
| Debug-v11 | `bin\Debug-v11\` | 底台阶 120 修复后 |

NETLOAD 用绝对路径：`D:\work\AI\project\L1-grok\bin\Debug-v13\AutoFixtureDim.dll`。

## 本会话解决的问题（按时间）

### A. Overall partition 误删 / 漏删

**旧 bug：** 仅用 `spanA + spanB ≈ overallSpan` 判断可 suppress。

**现规则：** 必须真实一维区间完整分割 Overall：

- 方向一致；无 gap / interior overlap；首尾贴 Overall 端点；用 `GeometryTolerance`。
- 实现：`FormsCompleteOverallPartition`、`FindCompleteOverallPartitionChain`（**Phase 1 已改为 DFS/回溯**，禁止最长路径死胡同不回头）。

**涉及：**

- `SuppressOutlineSegmentsThatPartitionOverall` — 多段 OS 链（如 9+11+71=91）
- `SuppressStructureDimensionsThatPartitionOverall` — 结构 + 共线同侧 OS / 多结构
- `SuppressComplementaryOutlineRemainders` — 已用几何 partition（Top/Right）

### B. 外框碎段 OutlineSegment + 结构双开

- `OutlineSegmentOnOverallEnvelope`：Overall 已存在时，压包络上的 OS 碎段。
- 与包络 OS **同区间** 的 **短** 水平结构宽（span &lt; Overall 一半）可共删（`StructureDuplicateOfEnvelopeOutlineSegment`）。
- **不**共删：较长底台阶（如 120/215）、Left/RightStructHeight（L 形右臂高 20）。

### C. StructureOverallPartition 过宽（修“其它图崩了”）

- OS partner 须 **同侧 + 共线**（水平同 Y，竖直同 X），不能只同 Side。
- 按 **focus 结构** 单独建池搜索，只 suppress 该 focus。
- 避免：塔顶宽 20 被臂顶 OS 71 误杀；底台阶 120 被顶边 OS 链误杀。

### D. U 槽链对齐

- `SlotChainV` / `SlotChainH`：边定位 + 孔间距共用 `AlignmentKey = SlotChain:{side}:{H|V}:{coord}`，priority 80。
- 修复 `GEN|SCV1|L1` 与 `GEN|SCV2|L0` 尺寸线不共层问题。

### E. Phase 2 — `default(Point2D)==(0,0)` 哨兵

- `SnapVerticalPointToLowerConnectedHorizontal` / `SnapHorizontalPointToLeftConnectedVertical`：用 `Point2D?` + `found ?? original`。
- `FindOutlinePointAtX`：空列表不再 `FirstOrDefault()` 落到 (0,0)。
- **保留** Try 模式里 `out point = default`（`OutlineGeometryQuery`、`FeatureRecognizer2D`）— 靠 bool 判断成败。
- `InternalsVisibleTo("CadAuto.Core.Tests")` 用于测 internal Snap。

### F. Phase 3 — Complementary Snap 收紧

- `SnapComplementaryHorizontal/VerticalRemainderEndpoints` **禁止**仅 span 和触发。
- 新 helper：`CanAttemptComplementaryPartitionSnap` — Structure 语义 + 方向 + 较短 partner + **`FormsCompleteOverallPartition`**。
- Snap 只改 attachment；suppress 仍靠 FormsComplete / multi-piece chain。
- `RemoveComplementaryOverallRemainderCandidates` 本身已用 FormsComplete，未再改。

## 关键代码位置

| 主题 | 文件 / 符号 |
| --- | --- |
| Partition 几何 | `CadAuto.Core/Rules/DimensionDeduplicationRules.cs` — `FormsCompleteOverallPartition*`, `FindCompleteOverallPartitionChain` (DFS) |
| Suppress / Snap / 包络 | `CadAuto.Core/Planning/DimensionPlanner.cs` — `SuppressDuplicateDimensions` 顺序、`SuppressOutlineSegmentsOnOverallEnvelope`、`SuppressStructureDimensionsThatPartitionOverall`、`CanAttemptComplementaryPartitionSnap`、`SnapComplementary*` |
| 测试 | `CadAuto.Core.Tests/Program.cs`（当前约 **99** 项全过） |
| 规则文档 | `docs/RecognitionRules.md`、`docs/DimensionRules.md`、`docs/Environment.md` |
| 诊断输出 | `diagnostics/last-run.json`（每次 CAD 跑会覆盖） |

## Suppress 流水线顺序（摘要）

1. Right/Left structure height vs Overall  
2. **StructureOverallPartition**（per-focus + 共线同侧 OS）  
3. OutlineSegment multi-piece overall partition  
4. Mirror  
5. OutlineSegment on overall envelope（+ 短水平结构共删）  
6. ComplementaryOutlineRemainders（Top/Right）  
7. Duplicate measured  

结构候选构建里：`SnapComplementary*` → `RemoveComplementaryOverallRemainderCandidates`（几何 partition）。

## 销孔 / 公差（本会话结论，未改代码）

- 销孔 = 圆 + **CadAider pin marker 块** 匹配；块名对不上 → `pinHoleCount=0`。  
- 中心距公差 `PinCenterDistanceToleranceText` 只挂在 `PinDistance`；`HoleChainV` 是普通孔链，**故意无公差**。  
- 用户已确认：之前是块名未对应。

## 诊断标签速查

| 标签片段 | 含义 |
| --- | --- |
| `GEN\|BSW*\|…\|B\|L*` | BottomStructWidth |
| `GEN\|SCV*\|…\|L\|L*` | SlotChainV（槽竖向链） |
| `GEN\|HoleChainV*\|…` | 非销孔竖向中心距（无 ± 公差） |
| `GEN\|PD*\|…` | PinDistance（有 ±0.02） |
| `GB` / `LB` | Global / Local boundary |
| 末段 `L0/L1` | 堆叠 level |

ASD4 + 只选某一侧时，**仅该侧线性尺寸 flush**；计划里 Selected 多、图上只见一条是正常现象。全图用 `ASD` 或 ASD4→**All**。

## 测试要求（下会话）

```powershell
powershell -NoProfile -File scripts\run-core-tests.ps1
```

重点回归名（节选）：

- Partition：`ThreeOutlineSegments…`、`StructureWidthThatPartitions…`、`ThreeStructureWidths…`、`GreedyDeadEndStillFindsValidOverallPartitionChain`
- 结构保留：`LocalTopStructureWidth…`、`RightArmHeightOnOverallMaxXIsKept`、`LShapeTowerTopWidthIsKept…`、`BottomStepWidthOnOverallEnvelopeIsKept`
- Phase 2：`SnapToOriginPointIsNotTreatedAsNotFound`、`SnapKeepsOriginalPointWhenNoCandidateExists`
- Phase 3：`LegalComplementaryPartitionAllowsSnapAndSuppress`、`OverlappingSpanSumDoesNotAllowComplementarySnap`、`GappedSpanSumDoesNotAllowComplementarySnap`、`NumericComplementLocalStructuresDoNotSnapOrSuppress`

手测建议图：

- 98 宽：20+78 抑制、98 保留  
- 91 宽 L 形：9/11 等 partition；塔顶 20 + 右臂高 20 保留  
- 215 底台阶：~120 保留  
- U 槽：SCV 边定位与中心距共线  

## 明确不要做的（除非用户要求）

- 不要批量删文件  
- 不要主动编译/开 CAD（Agents.md）；用户说「编译一版」再用 `bin\Debug-v{N+1}\`  
- 不要改 Pin/Hole/Slot 识别、layout renderer，除非新任务  
- Phase 1–3 已完成，无 Phase 4 在本会话定义  

## 下会话优先检查

1. `git status`：Phase 2/3 是否已提交推送。  
2. 若未推送：提交说明可参考「Point2D snap sentinel + complementary partition snap gate」。  
3. 用户若反馈某图尺寸错：先看 `diagnostics/last-run.json` 的 `suppressedReason` / `debugRole` / `placementSide`，再对照本 HANDOFF 规则表。  

## graphify

代码改后应：`graphify update .`  
查询：`graphify query "…"`（`graphify-out/graph.json` 存在时优先）。
