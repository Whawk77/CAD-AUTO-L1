# 旋转不变量（四向标注一致）

## 产品共识（grill-me 2026-08-06）

| 决策 | 选择 |
|------|------|
| Phase 1 硬门禁 | **数值多重集**（忽略放置侧） |
| Phase 2 增强 | 语义对（Overall / 真实台阶 / 内部特征） |
| Signature 成员 | **仅 Overall + 结构族**（不含 OS / 孔销） |
| 金标 | **最干净合法集**（不盲从 0°） |
| 干净集规则 | Overall + 真实台阶；Overall−台阶可推余量删除 |
| 台阶对 | 留真实短台阶，删互补长 body |
| Phase 1 范围 | **只改抑制**；生成不对称 → Phase 2 |
| 实现 | OuterContour + Complementary **合并为四向**结构规则 |
| P1 冲突 | 列表 → 人工批准后再改期望 |
| 开链 Bottom 专用 | Phase 1 **不动** |

## Signature 定义

```text
Signature(plan) = sorted multiset of round(span, 2)
  for selected dims where
    Kind ∈ { OverallWidth, OverallHeight }
    OR Role == Structure
    OR DebugRole ∈ structure family (Top/Bottom/Left/Right Struct*, ChamferedStep*)
```

验收：`Signature(0°) == Signature(90°) == Signature(180°) == Signature(270°)`。

## 核心测试

| 测试 | Fixture |
|------|---------|
| `RotationSignature_OuterStepOverallStructureIsStable` | OC01 外轮廓台阶 90×28 |
| `RotationSignature_ClosedChainStructureIsStable` | 215 顶凹口封闭链轮廓 |

## Phase 状态

- [x] Phase 0：旋转 Signature 门禁（Phase 1 硬不变量 + 完整多重集日志）
- [x] Phase 1：抑制四向（OuterContour Top/Right + 结构互补四向）
- [ ] Phase 2：结构生成统一抽取；**完整多重集四向相等**
- [ ] Phase 3：默认基准端点（可选）

### Phase 1 硬不变量（已测）

| Fixture | 四向必须 |
|---------|----------|
| OC01 | 含 90、20、~28.26 |
| 215 封闭链 | 含 215、100；**禁止**同时选中 90+50+75 |
