# 项目目标

本项目是 AutoCAD 2020 的 .NET Framework 标注插件，用于夹具类零件的半自动尺寸标注。主命令是 `ASD`，兼容命令包括 `AUTOFIXDIM`、`AUTOFIXDIMREGEN`、`AUTOFIXDIMCLEAR`。

当前阶段目标是继续改进孔位尺寸布局，重点是散孔的分组、链式标注、重复尺寸抑制、侧边选择，以及销孔组尺寸与外轮廓/局部边界的关系。

# 当前架构

- `Commands.cs`：命令入口、主流程、线性尺寸生成后的交互式倒角/圆角/U 槽/孔径标注流程。
- `GeometryCollector.cs`：外轮廓选择、孔源收集、基准边/基准点/基准销孔交互。
- `FeatureRecognizer.cs`：孔、销孔、螺纹孔、U 槽、外轮廓倒角和圆角识别。
- `FeatureModels.cs`：孔、轮廓、倒角、圆角、尺寸类型等数据模型。
- `RuleConfig.cs`：加工规则和公差文本配置。
- `DimensionDrawer.cs`：线性尺寸生成、孔位尺寸、销孔组逻辑、散孔链式布局、尺寸堆叠、重复尺寸抑制。
- `NativeDiameterDimensioner.cs`：孔径/螺纹交互式标注。
- `AnnotationMetadata.cs`：生成对象 XData 标记与清理逻辑。
- `LayerManager.cs` / `DimStyleManager.cs`：标注图层与标注样式选择。

# 已完成功能

- 外轮廓整体宽高尺寸生成。
- 局部台阶、凸台、内槽相关尺寸生成。
- MainOutline 倒角和圆角识别与引线标注。
- 销孔识别、销孔 H7 孔径标注、销孔组中心距标注。
- 销孔组定位尺寸，包含 `±0.05` 规则。
- 同组销孔中心距尺寸，包含 `±0.02` 规则。
- 普通孔、螺纹孔、U 槽识别与交互式孔径/螺纹/U 槽半径标注。
- 散孔按“同规格 + 同 X / 同 Y”生成自然小组，断档过大时拆链。
- 散孔小组按空间邻近聚成大组。
- 散孔大组选择参考销孔基准和锚点孔，优先使用 mutual-nearest 关系。
- 散孔中心距优先生成链式尺寸。
- 散孔定位尺寸带 `LooseChainId`，后处理避免把同一链中的单个尺寸拆到其他侧。
- 同侧和跨侧重复尺寸抑制已增强，优先保留带公差、销孔、基准、整体等更重要的尺寸。
- 按 2026-05-28 决定，散孔 `HoleLocation` 不再使用局部边界布局。
- `PinDistance` 和 `PinGroupDistance` 仍允许使用局部边界，但尺寸线不能进入真实外轮廓内部。
- 最新测试 DLL：`bin\Debug\autofixdim-LB51.dll`。

# 修改过的文件

- `DimensionDrawer.cs`：本阶段核心修改文件，包含散孔分组/大组/链式定位、链 ID、重复尺寸抑制、局部边界规则调整。
- `PROJECT_HANDOFF.md`：已更新 2026-05-28 交接总结和 LB51 当前测试 DLL 信息。
- `AGENTS.md`：已同步最新测试 DLL 为 LB51，并加入散孔不走局部边界、销孔尺寸可走局部边界但不能进外轮廓内部的持久规则。
- `HANDOFF.md`：本文件，按当前要求生成的结构化交接文档。

# 当前 Bug

- `autofixdim-LB51.dll` 仍需要 AutoCAD 实图验证。
- 散孔链已强制尽量对齐，但取消局部边界后会回到全局外轮廓侧作为基准，视觉距离可能还需要继续调。
- 散孔大组聚类阈值仍是启发式规则，复杂图形中可能过度合组或拆得不够。
- 短散孔定位尺寸的参考孔切换和文字避让仍是启发式处理，极端短尺寸可能需要继续优化。
- `PinDistance` / `PinGroupDistance` 的局部边界选择还需要在凸台、凹槽、异形轮廓附近做视觉验证。
- 跨侧重复尺寸抑制需要检查是否误删了设计上需要保留的镜像尺寸。
- 没有自动化 AutoCAD 视觉测试，当前依赖 `NETLOAD` 后手动运行 `ASD` 验证。

# 下一步计划

- 在 AutoCAD 中 `NETLOAD` 加载 `bin\Debug\autofixdim-LB51.dll`。
- 用 2026-05-28 标注截图中的散孔案例运行 `ASD`。
- 检查散孔链尺寸是否共线、是否仍错误贴近局部边界、是否出现重复尺寸。
- 检查销孔中心距 `±0.02` 和销组定位 `±0.05` 是否仍正确保留。
- 检查销孔组局部边界布局是否贴近合理边界且不进入外轮廓内部。
- 如 LB51 不满足视觉要求，继续从当前源码修改并编译 LB52。
- 如 LB51 满足要求，将 LB51 作为当前交接基线，并同步 `PROJECT_HANDOFF.md`。

# CAD 标注规则

- 生成对象必须写入 XData，应用名为 `AUTOFIXDIM`。
- `AUTOFIXDIMCLEAR` 只能清理带 `AUTOFIXDIM` XData 的插件生成对象。
- 不创建 `CENTER` 图层。
- 不生成孔中心线或十字中心线。
- 标注图层优先使用 `JEE-DIM标注`，否则使用当前图层。
- 线性尺寸优先使用当前图纸的 `SCALE-1-0x` 系列标注样式。
- 孔径/螺纹标注优先使用线性样式对应的 `$3` 子样式。
- 销孔孔径标注必须带 `H7`。
- 同组销孔中心距使用 `±0.02`。
- 销孔组定位尺寸使用 `±0.05`。
- 普通孔和螺纹散孔定位尺寸不能继承销孔公差。
- 零长度尺寸不能生成。

# 外轮廓规则

- 外轮廓优先从 `DRAWING` 图层对象识别。
- 轮廓对象按图层线型过滤中心线、隐藏线、虚线等构造线。
- 优先使用闭合多段线；没有闭合多段线时，按图层语义、线宽、连续性、包围范围等选择最佳连续组件。
- 整体宽度必须使用真实外包络 `MinX -> MaxX`。
- 整体高度必须使用真实外包络 `MinY -> MaxY`。
- 外包络需要考虑直线、圆弧、圆角、倒角、多段线 bulge 和过渡边。
- 整体宽高尺寸优先级最高，不能被局部台阶、倒角切点、圆角切点尺寸替代。
- 局部边界用于销孔中心距/销组定位时，尺寸线必须保持在真实外轮廓外侧。

# 倒角规则

- 倒角只从 MainOutline 上的 45 度或 135 度非轴向段识别。
- 倒角段需要连接一条水平主边和一条竖向主边。
- 相邻共线且足够接近的 45 度段可合并后计算倒角值。
- 内槽补充倒角只用于“倒角 + 内部水平/竖直槽线 + 倒角或圆角”的真实槽结构。
- 内部竖向槽线的另一端必须连接另一个倒角/45 度段或圆角，不能把长斜结构边误判为倒角。
- 倒角文字整数不带小数，例如 `C5`；非整数遵循当前孔径/倒角标注样式精度。
- 倒角引线箭头必须落在真实倒角边上。
- 不要恢复已废弃的 `autofixdim-v89.dll` 行为，即用倒角投影尺寸替代长结构尺寸。

# Git 分支状态

- 当前分支：`master`。
- 本地分支状态：`master...origin/master [ahead 1]`。
- 当前未提交改动包括：
  - `AGENTS.md`
  - `DimensionDrawer.cs`
  - `PROJECT_HANDOFF.md`
  - `HANDOFF.md`
- 当前还有未跟踪文件：`中文注释.md`。
- 不要在未确认前还原或删除用户已有改动。

# 常用命令

- 编译：
  `dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /p:PostBuildEvent= /p:DebugType=None /p:DebugSymbols=false /v:minimal`
- 当前测试 DLL：
  `D:\work\AI\project\autocad-dim\bin\Debug\autofixdim-LB51.dll`
- AutoCAD 加载：`NETLOAD`
- 主命令：`ASD`
- 重新生成：`AUTOFIXDIMREGEN`
- 清理生成标注：`AUTOFIXDIMCLEAR`
- 部署脚本：
  `powershell -ExecutionPolicy Bypass -File .\deploy_next_version.ps1`

# 注意事项

- 本项目当前工作目录是 `D:\work\AI\project\autocad-dim`。
- 当前源码不是干净工作区，接手前先看 `git status --short --branch`。
- AutoCAD 可能锁定已加载 DLL 或 PDB；如果覆盖失败，使用新的 `autofixdim-LB<N>.dll` 后缀。
- 文档中使用绝对日期，不使用相对时间词。
- `AGENTS.md` 是规则文件，不写单次开发流水账。
- 禁止批量删除文件或目录，不使用 `del /s`、`rd /s`、`rmdir /s`、`Remove-Item -Recurse`、`rm -rf`。
- 如果必须删除文件，只能一次删除一个明确路径的文件。
