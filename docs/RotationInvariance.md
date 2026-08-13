# 旋转不变量（四向标注一致）v3

## 产品共识（grill-me 2026-08-07 锁定）

| 决策 | 选择 |
|------|------|
| 验收 | **四向 Signature 多重集严格全等**（0°/90°/180°/270°） |
| Signature 成员 | Overall + **Structure only**（OS 不得顶替金标 span） |
| 集合 | **final = 金标**（不多不少） |
| 选边 | **以几何为准**；放置侧可变 |
| span 真值 | **合成外轮廓边**（圆角/倒角只作 micro-gap 邻接） |
| 主路径 | **FeatureFirst** |
| 默认开关 | `CreateDefault.UseFeatureFirstStructurePipeline = false` |
| ASD 开 FF | 仅当 **Golden338Cad 四向绿** 之后 |
| 禁止 | Left/Right 专用结构补丁当终态 |

## Signature

```text
Signature(plan) = sorted multiset of round(span, 2)
  for selected dims where
    Kind ∈ { OverallWidth, OverallHeight }
    OR Role == Structure
    OR DebugRole ∈ { Top/Bottom/Left/Right Struct*, ChamferedStep* }
```

## 金标分轨

### 直角单测 F215

```text
Golden215 = { 42, 50, 75, 100, 120, 215 }
```

### 直角单测 F338

```text
Golden338 = { 73, 87.55, 100, 201.5, 305, 338 }   // 无 20
```

### CAD 真图轨 F338（test2 拓扑 / 合成边）

**槽宽不是固定数**：中间台阶「槽/台面净宽」由相邻立面间距几何量出（直角件常为 87.55，真图圆角后常见 ~92.55）。  
四向必须都有这一 **Structure 槽宽**，且同一朝向序列下 span 全等。

```text
# 直角/合成 fixture 门禁示例（槽宽=87.55）
Golden338Cad = { 20, 73, 87.55, 100, 201.5, 305, 338 }

# 真图手测：槽宽取实测（如 92.55），集合形如
# { 20, 73, <槽宽>, 100, 201.5, 305, 338 }
```

| 必须（特征） | 禁止 |
|--------------|------|
| 脚尖 20、外台阶 73、**槽宽**、臂高 100、长臂 305、overall | 体残差 265/172.45/171.5；肩高 91.5 顶替槽宽 |

实现：`StepGroove` = 相邻台阶立面间距；禁止 Corner/InteriorRiser 抹掉槽宽。

## 架构

```text
Outline → StructureFeatureExtractor   // 合成边 + 特征
       → StructureMeasurementSelector  // 无 Side 键
       → StructurePlacementAdapter     // 仅放置
       → Layout
```

## Phase 状态

- [x] Phase A–C：直角 F215/F338 四向金标 + FeatureFirst 管线
- [x] Phase D1：Golden338Cad fixture + 四向门禁（166/166 含 F338Cad 四向绿）
- [x] Phase D2：合成外轮廓边（collinear micro-gap）+ tip 再发射
- [x] Phase D3：ASD 打开 FeatureFirst（门禁已绿；CreateDefault 仍 false）
- [ ] Phase D4：CAD 真图 test2 手测四向 + 若需再抽 last-run 折线替换 fixture

## 核心测试

| 测试 | 期望 |
|------|------|
| `RotationSignature_F215GoldenMultisetFourWayEqual` | 四向 == Golden215（FF=true） |
| `RotationSignature_F338GoldenMultisetFourWayEqual` | 四向 == Golden338（FF=true，无 20） |
| `RotationSignature_F338CadGoldenMultisetFourWayEqual` | 四向 == **Golden338Cad**（FF=true，含 20） |
