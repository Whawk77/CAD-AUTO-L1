# CAD-AUTO-L1 重构计划

## 基线与目标

- 基线：`dev-7` / `c61ced6bec403468cf9dd202d2e7d7f7be0d0844`
- 依据：`CAD-AUTO-L1 架构评估报告 v2`
- 目标：保持现有命令、尺寸语义、图层、样式和 XData 兼容性，逐步收敛为：

```text
Commands
  -> CadAuto.CadAdapter（AutoCAD 读取、映射、渲染）
    -> CadAuto.Core（纯几何、规划、布局规则）
```

必须持续保持：

1. `CadAuto.Core` 不引用 Autodesk 程序集。
2. 标注附着点只落在真实几何上。
3. Overall、销孔组、功能孔、槽与四方向布局的现有产品规则不被“顺手统一”。
4. 每个行为改动都有 Core 回归或真实 DWG 回归证据。
5. 不做 SDK-style 迁移、NRT 全仓启用、外部配置化、Git 历史重写或四方向谓词大合并。

## 实施顺序

当前工作区状态：

| 阶段 | 状态 | 说明 |
| --- | --- | --- |
| 阶段 0 | 已实现，待运行 | 测试过滤、分层失败收集、统一汇总、PDB 保留已落地 |
| 阶段 1 | 已实现，待 CAD 验证 | 渲染诊断、XData Kind、ASDCOREDBG、失败 JSON、加载/部署提示已落地 |
| 阶段 2 | 部分完成 | 螺纹弧规则与开放 Polyline 守卫已落地；双识别器其余分叉未动 |
| 阶段 3 | 部分完成 | 已删 Adapter/Core 两个零引用类型和 Drawer 死转发；Planner 清理与 partial 拆分未动 |
| 阶段 4 | 部分完成 | 报告新鲜度、错误节点和 LSP `COMPLETE` 门禁已落地；布局度量与 CI 未动 |

### 阶段 0：建立可信反馈环

范围：

- 改造 Core 测试宿主：支持名称或 `P0`～`P3` 过滤。
- P0 失败立即停止；P1～P3 在当前层收集全部失败，再阻断后续层。
- 统一输出通过数、选中用例总数和失败清单。
- 测试脚本保留 PDB，使失败栈包含源码行号。

完成门槛：

- 全量与过滤运行均有确定退出码。
- Core 回归全部通过后，才进入任何尺寸语义改动。

### 阶段 1：修复诊断与元数据契约

范围：

- 记录命令 scope 过滤及渲染层四类删除原因。
- `finalDimensions` 只保留实际进入渲染的尺寸。
- XData 增加 `Kind` 与 `SchemaVersion`，并兼容没有 `Kind` 的旧标注。
- ASD/ASD3 只清理 Dimension/CoreDebug，不再误删 AG1 工艺标注。
- XData 写入释放 `ResultBuffer`，并保留其他应用的 XData 段。
- ASDCOREDBG 自动注册 RegApp，使用时间戳 groupId。
- 异常路径也刷新诊断 JSON；非有限数写成 `null`；完整转义控制字符。
- 加载提示打印全部命令与实际 DLL 路径；部署脚本校验三件套。

完成门槛：

- ASD5/ASD6/ASD7 的 scope 差集均有明确 `RenderSuppressed` 原因。
- AG1 → ASD4 后 AG1 标注仍保留。
- 干净图纸可直接运行 ASDCOREDBG，随后可由 ASD3 清除。
- 失败运行不会复用上一次 `last-run.json`。

### 阶段 2：识别规则收敛

范围：

- 把螺纹弧扫掠角判定下沉为 Core 纯规则，两个 Adapter 调用点共用。
- 先使用 `2°` 保守容差，并为阈值边界补 Core 测试。
- 确认开放 Polyline 是否可进入主轮廓路径，再处理幻影闭合边。
- 仅同步双识别器的三个已证实分叉点，不整体合并识别器。

完成门槛：

- M6/M8/M10 真实图仍输出螺纹标注。
- 260°～267° 非螺纹圆弧不被误识别。
- 槽、倒角、圆角真实图回归通过。

### 阶段 3：Planner 内部重构

范围：

- 先删除逐个核实的 private 死代码和三处反编译恒等逻辑。
- 补候选进入 plan 前的丢弃诊断。
- 统一四方向 `IgnoredPoint` 诊断元数据，但不统一四方向几何谓词。
- 给现有抑制测试补 `SuppressedReason` 断言。
- 将 8 个纯算法接缝改为 `internal`，补直接单元测试。
- 最后把 `DimensionPlanner` 拆成 5 个 `partial` 文件；不提取新类、不改访问级别和调用顺序。

完成门槛：

- 每次删除与每次文件迁移后单独编译。
- Core 全量回归保持通过。
- `SuppressDuplicateDimensions` 的顺序和理由字面量不变。

### 阶段 4：布局与回归设施

范围：

- 先记录有效 DimStyle 字高、箭头与 DIMSCALE，再统一布局度量来源。
- 先构造 P11 的功能孔跨块对齐复现测试，再决定是否整体外推 lane。
- 修复报告新鲜度校验、LSP `COMPLETE` 假绿和 hole-slot fixture 清单。
- 清理 Git 已跟踪构建中间产物后，再增加只构建 Core 的 Windows CI。

完成门槛：

- 默认 DIMTXT=2.5 图纸视觉结果不变。
- 四方向结构化校验与 PNG 目视检查同时通过。
- hole-slot 八张 fixture 均有 SHA256、真实运行记录与明确结论。

## 提交边界

建议按以下边界独立提交，方便回滚：

1. `test: 改造 Core 测试宿主与过滤`
2. `fix: 补齐尺寸渲染抑制诊断`
3. `fix: 区分 Dimension 与 AG1 的 XData 元数据`
4. `fix: 修复调试命令与失败诊断`
5. `fix: 统一螺纹弧角度规则`
6. `refactor: 清理 Planner 死代码`
7. `refactor: 将 DimensionPlanner 拆分为 partial 文件`
8. `test: 修复 CAD 回归新鲜度与 hole-slot 基线`

## 本轮实施范围

本轮先完成阶段 0、阶段 1，以及阶段 2 中可由 Core 纯测试保护的螺纹弧规则。Planner 文件拆分、DimStyle 布局度量与轮廓包络规则留在后续批次，避免在未经编译和真实 DWG 回归时混入高风险几何变化。
