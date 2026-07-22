# Fix Proposal — Issue 1: 包络 OutlineSegment 结构共删缺共线/同侧

| 项 | 值 |
| --- | --- |
| 状态 | 方案（未实施） |
| 来源 | Review `cffd427f` Issue 1 — Severity: bug |
| 相关提交 | `cab2979`…`f23071a`（envelope OS suppress + structure co-delete） |
| 主文件 | `CadAuto.Core/Planning/DimensionPlanner.cs` → `SuppressOutlineSegmentsOnOverallEnvelope` |
| 抑制标签 | `StructureDuplicateOfEnvelopeOutlineSegment` |

---

## 1. 问题陈述

当 Overall 已存在时，`SuppressOutlineSegmentsOnOverallEnvelope` 会：

1. 抑制落在外框包络上的 **OutlineSegment 碎段**（`OutlineSegmentOnOverallEnvelope`）；
2. 再把 **同测量区间** 的短水平结构宽（`BottomStructWidth` / `TopStructWidth` 等）一并共删，理由是「与包络 tip OS 是同一几何的双候选」。

当前共删条件过宽：

```text
structureSpan < 0.5 * overallWidth
AND ∃ envelope OS (horizontal) 与 structure 满足 IsSameMeasurementInterval（仅 1D 区间）
```

**缺少：**

- 同侧（`Side`）；
- 共线（水平同 Y / 竖直同 X）；
- 结构自身是否也在该包络边上。

因此：顶边短台阶与底边 tip OS **仅 X 区间碰巧相同** 时，可能被标成 `StructureDuplicateOfEnvelopeOutlineSegment` 误杀。  
半宽阈值只限制 span，**不保证边身份一致**。

---

## 2. 现状代码（缺陷点）

位置：`DimensionPlanner.SuppressOutlineSegmentsOnOverallEnvelope`（约 516–537 行）。

```csharp
// 现状：只比 1D 区间 + 半宽
foreach (PlannedDimension structure in plan.Dimensions
    .Where(d => d.Kind == DimensionKind.Normal
        && IsHorizontalStructureWidthRole(d.DebugRole)))
{
    double structureSpan = GetDimensionSpan(structure, horizontal: true);
    if (structureSpan >= overallWidthSpan * 0.5 - _config.GeometryTolerance)
        continue;

    if (envelopeOutlineSegments.Any(os =>
        os.Orientation == DimensionOrientation.Horizontal
        && IsSameMeasurementInterval(structure, os, horizontal: true)))
    {
        toSuppress.Add(structure);
    }
}
```

同文件里，**结构 overall-partition** 路径已经有正确门控（约 3317–3321 行）：

```csharp
if (os.Side == focus.Side && AreCollinearStructurePartners(focus, os, horizontal, tol))
    partnerPool.Add(os);
```

`AreCollinearStructurePartners`：水平要求两端 Y 共线且同 Y；竖直要求两端 X 共线且同 X。  
Issue 1 的修复应 **复用同一语义**，而不是再发明一套边判断。

---

## 3. 目标行为

| 场景 | 期望 |
| --- | --- |
| 底边 tip OS 10 + 同 Y、同区间 `BottomStructWidth` 10 | **共删**结构（真重复） |
| 底边 tip OS 10 + **顶边**短 `TopStructWidth` 仅 X 区间相同 | **保留**顶边结构 |
| 底边 ledge `BottomStructWidth` 120（span ≥ 半 overall） | **保留**（现有半宽守卫继续生效） |
| `Left/RightStructHeight` | **不参与**本路径共删（保持现状：仅水平结构宽） |
| 包络 OS 自身、同区间非包络 OS | 行为不变（本 Issue 只改 **结构** 共删支路） |

**原则：** 共删 = 「同一外框边上的重复候选」，不是「任意同 1D 投影的短结构」。

---

## 4. 推荐修复（方案 A — 最小正确改动）

### 4.1 共删谓词（新）

在结构共删循环中，对每个候选 `structure` 与每个 `envelopeOs`，**全部**满足才共删：

1. **角色**：`IsHorizontalStructureWidthRole(structure.DebugRole)`（保持现状，不扩到高度）。
2. **方向**：`envelopeOs.Orientation == Horizontal`（与结构一致）。
3. **短 tip**：`structureSpan < overallWidthSpan * 0.5 - GeometryTolerance`（保持现状，二次守卫）。
4. **同测量区间**：`IsSameMeasurementInterval(structure, envelopeOs, horizontal: true)`。
5. **同侧**：`structure.Side == envelopeOs.Side`  
   - 底 tip 只对 `Bottom` 结构；顶 tip 只对 `Top` 结构。
6. **共线**：`AreCollinearStructurePartners(structure, envelopeOs, horizontal: true, tol)`  
   - 与 StructureOverallPartition 一致。

可选加固（推荐一并做，成本低）：

7. **结构也在包络边上**（或至少贴在该 OS 所在包络 Y）：  
   `IsDimensionOnOverallEnvelope(structure, minX, maxX, minY, maxY)`  
   - 真重复的 tip 结构一定在 outer envelope；  
   - 内部水平台阶即使同区间也不会误进。

### 4.2 伪代码

```csharp
double tol = _config.GeometryTolerance;
double overallWidthSpan = maxX - minX;

foreach (PlannedDimension structure in plan.Dimensions
    .Where(d => d.Kind == DimensionKind.Normal
        && IsHorizontalStructureWidthRole(d.DebugRole)))
{
    double structureSpan = GetDimensionSpan(structure, horizontal: true);
    if (structureSpan >= overallWidthSpan * 0.5 - tol)
        continue;

    // 可选：结构自身须在 overall 包络水平边上
    if (!IsDimensionOnOverallEnvelope(structure, minX, maxX, minY, maxY))
        continue;

    bool isEnvelopeDuplicate = envelopeOutlineSegments.Any(os =>
        os.Orientation == DimensionOrientation.Horizontal
        && structure.Side == os.Side
        && AreCollinearStructurePartners(structure, os, horizontal: true, tol)
        && IsSameMeasurementInterval(structure, os, horizontal: true));

    if (isEnvelopeDuplicate)
        toSuppress.Add(structure);
}
```

### 4.3 不改动的部分

| 部分 | 原因 |
| --- | --- |
| 包络 OS 收集 / `OutlineSegmentOnOverallEnvelope` | Issue 1 不涉及 |
| 同区间非包络 OS 共删（`OutlineSegmentSameIntervalAsEnvelopeFragment`） | 针对 OS–OS；产品意图是压重复 tip 投影，与结构共删正交 |
| `StructureOverallPartition` 共线门控 | 已正确，仅复用 helper |
| 半宽阈值 | 作为「大台阶保留」的二次保险暂时保留；半宽启发式本身是 Review Issue 4，可另案 |

### 4.4 注释同步

把现有注释：

> Same-interval *horizontal* structure co-suppress only for short overall-edge tips

改为明确写出：

> Same side + collinear + same measurement interval + short span  
> （可选：structure on envelope）

避免后人再加回「仅区间匹配」。

---

## 5. 备选方案（不推荐作首选）

| 方案 | 做法 | 缺点 |
| --- | --- | --- |
| **B. 只加 Side** | `structure.Side == os.Side` | 同侧不同边（L 形同侧多层）仍可能共线失败以外的误配；弱于 partition 路径 |
| **C. 只加共线、不加 Side** | 仅 `AreCollinearStructurePartners` | 理论上同 Y 的底边 OS 与异常 Side 标记可能仍配对；Side 便宜且与 placement 一致 |
| **D. 去掉结构共删，只靠 partition** | 删除共删支路 | 破坏 `StructureDuplicateOfEnvelopeOutlineSegmentIsSuppressed`：tip 10 在 mid 被 chamfer 打断时 **形不成** complete partition，却仍是 OS 双候选 |
| **E. 仅用半宽改阈值** | 调 0.5 或配置化 | 不解决跨边同区间误杀，只是边界搬家 |

**结论：方案 A（同侧 + 共线 + 同区间 + 短 span [+ 结构在包络]）** 与现有 partition 语义对齐，且保留「无完整 partition 链时仍去双候选」能力。

---

## 6. 测试计划

### 6.1 必须保留（回归）

| 测试 | 期望不变 |
| --- | --- |
| `OutlineSegmentOnOverallEnvelopeIsSuppressed` | tip OS 仍 suppress |
| `StructureDuplicateOfEnvelopeOutlineSegmentIsSuppressed` | 底边 tip 结构 10 仍共删 |
| `BottomStepWidthOnOverallEnvelopeIsKept` | 120 仍保留 |
| `RightArmHeightOnOverallMaxXIsKept` | 右臂高仍不走本路径 |
| `LShapeTowerTopWidthIsKept…` / tower 相关 | 不因本改动变坏 |

### 6.2 必须新增（钉死 Issue 1）

**名称建议：** `TopStructureSameIntervalAsBottomEnvelopeTipIsKept`

**几何要点（示意）：**

```text
Overall: 0..75 × 0..40
底边：右 tip [65,75] @ Y=0  →  envelope OS 10 @ Bottom
顶边：短台阶 [65,75] @ Y=40 →  TopStructWidth 10（或等价结构）
中间可有竖肩，保证顶边结构能生成
```

**断言：**

1. `OverallWidth` 75 保留；
2. 存在（或诊断中曾存在）底边 tip 相关 suppress（OS 或 BSW）——按生成情况；
3. **`TopStructWidth`（value≈10）必须 retained**，且  
   `SuppressedReason != StructureDuplicateOfEnvelopeOutlineSegment`；
4. 若底边存在 `BottomStructWidth` 10 与 tip 共线，则其仍可被共删（对照正例）。

更稳的构造：手写/最小 outline 保证同时产出：

- Bottom envelope OS 或 BottomStructWidth @ Y=minY，区间 [65,75]；
- TopStructWidth @ Y=maxY，区间 [65,75]。

### 6.3 可选加固

- 同侧但 **不同 Y**（内台阶与底边 tip 同 X 区间）：结构不在 envelope → 在加了 `IsDimensionOnOverallEnvelope(structure)` 后应保留。
- 近 0.5×overall 边界：归 Issue 4，本 fix 不强制。

---

## 7. 验收标准

1. 新测试 `TopStructureSameIntervalAsBottomEnvelopeTipIsKept` 通过。  
2. 6.1 既有回归全部通过。  
3. `StructureDuplicateOfEnvelopeOutlineSegment` 触发路径在代码审查中可见：**Side + collinear + interval**（+ 可选 envelope）。  
4. 诊断标签字符串不改名（兼容既有 CAD/诊断解读与测试）。  
5. 不主动扩到竖直结构高度共删（超出本 Issue 与 HANDOFF 产品意图）。

---

## 8. 实施清单（落地时）

1. 改 `SuppressOutlineSegmentsOnOverallEnvelope` 结构共删条件（方案 A）。  
2. 更新方法 XML 注释。  
3. 在 `CadAuto.Core.Tests/Program.cs` 增加 6.2 测试并 `RunTest(...)` 注册。  
4. 用户明确要求时再跑：`powershell -NoProfile -File scripts\run-core-tests.ps1`。  
5. 用户明确要求编译可加载 DLL 时：`bin\Debug-v14\`（当前最高 v13 → **v14**）。  
6. 代码改完后：`graphify update .`。  
7. **本方案文档**在实施后把状态改为「已实施」，并回填提交 hash（若有）。

---

## 9. 风险与边界

| 风险 | 缓解 |
| --- | --- |
| Side 标记与真实 Y 不一致 | 共线 + 可选 `IsDimensionOnOverallEnvelope` 双保险 |
| 结构 grip 略偏出 envelope（数值噪声） | 一律用 `GeometryTolerance`；与 OS 包络判定同一 tol |
| 合法「同边双候选」因共线失败而漏删 | 真重复必共线；漏删时仍可由 `StructureOverallPartition` / `MirroredDuplicate` 兜底（既有测试已接受多种 reason） |
| 半宽启发式仍脆 | 单列 Issue 4；本 fix 不扩大半宽语义 |

---

## 10. 一句话总结

**把 envelope 结构共删从「短 + 同 1D 区间」收紧为「短 + 同侧 + 共线 + 同区间（+ 结构在包络）」**，与 `SuppressStructureDimensionsThatPartitionOverall` 的 OS partner 门控对齐，专杀「同一外框边上的 OS/结构双开」，避免顶/底跨边同投影误杀。
