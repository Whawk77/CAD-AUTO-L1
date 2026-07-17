# CATIA AI Panel

CATIA AI Panel 是面向 CATIA V5 的本地 WPF 伴随面板。它读取当前 Part/Product 文档及选择，将有界上下文交给本机 Codex CLI 生成 CATScript，并在每个版本由用户确认后通过 CATIA Automation COM 执行。面板运行时读取 CATIA 的 Release/Service Pack，当前已在 V5-6R2018（B28）与 V5-6R2022（B32）接口路径上构建，并在本机 B32 SP6 验证连接和上下文读取。

## 环境要求

- Windows x64
- CATIA V5-6R2018（B28）或 V5-6R2022（B32），运行时需已打开一个 Part 或 Product 文档
- .NET 9 Desktop Runtime/SDK
- 已安装并登录的 Codex CLI；当前开发机验证版本为 `codex-cli 0.142.4`

## 构建与运行

```powershell
dotnet build .\CatiaAiPanel.slnx
dotnet run --project .\CatiaAiPanel.App\CatiaAiPanel.App.csproj
```

先启动 CATIA 并打开文档，再启动面板。面板会把当前显示器工作区自动划分为左右两栏：CATIA 使用左侧，AI 面板使用右侧，二者不会互相覆盖。调整面板宽度会同步调整 CATIA；CATIA 最小化时面板一起隐藏，正常关闭面板时恢复 CATIA 原来的窗口位置和最大化状态。该行为是 WPF 伴随窗口编排，不是 CAA 原生停靠。

使用流程：

1. 在 CATIA 中选择 Body、特征、面/边或 Product 实例。
2. 在面板底部描述任务并发送。
3. 检查“宏预览”和“风险与影响”。
4. 点击“确认执行”，在最终确认框中再次核对影响。
5. 如结果不符合预期，填写反馈并点击“继续修正”；每个修正版仍需重新确认。

### 3DXML/CGR 测量

3DXML 中的 `.cgr` 是可视化表示，不包含 CATScript 可稳定读取的 `Part.Bodies`、孔特征或通用包围盒 API。选择这类对象并要求测量时，面板不会再生成一个看似成功但没有尺寸的宏，而会提示使用 **测量项目** 或 **测量距离**，由受信任的面板代码启动 CATIA 原生命令。

- 外形、半径等单项尺寸使用 **测量项目**；两个对象之间的距离使用 **测量距离**。
- 孔径、孔深、参数化特征等精确数据：需要打开对应的源 `.CATPart`，再选择 Hole、Face、Edge 或 Body 生成查询宏。
- 面板启动原生命令不保存、关闭或修改文档；生成的 CATScript 仍然禁止调用 `StartCommand`。

### 测量账本与 Part 复现

原生 Measure 对话框中的临时值不是 CATIA Automation/Selection 的公开数据。B32 版本中，面板通过 Windows UI Automation 读取 Measure Item/Measure Between 的结构化只读字段，不使用 OCR；同时继续支持 SPA 持久化 Distance 和普通 Measurable。可复现流程是：

1. 在 CATIA 中选择一个可测边/面/实体时，750 ms 轮询会通过 CATIA SPAWorkbench `GetMeasurable` 自动采集半径、长度、周长、面积或体积；同时选择两个对象时会自动采集最小距离和两个最近点。
2. 对 CGR/3DXML 中 SPA 无法解析的对象，点击 **测量项目** 或 **测量距离**，选择具体几何并完成原生交互测量。
3. 保持原生测量结果窗口打开。面板每 750 ms 检查一次；对象、数值或坐标变化时自动写入账本，即使 CATIA 已清空 `Document.Selection` 也不受影响。
4. 连续测量会按“两个来源对象 + 尺寸类型”增量保存：新对象/尺寸追加，相同对象的同一尺寸更新。“同步测量记录”用于立即核对；对跨对象距离仍建议使用 **Keep Measure**，作为 SPA Distance 的第二条采集路径。
5. 记录保存到 `%LOCALAPPDATA%\CatiaAiPanel\measurements`，并自动作为后续 Codex 提示中的 `measurements` 上下文。
6. 建议把 `Distance.1` 等保留测量重命名为 `overall_length`、`hole_pitch_x` 等语义名称。生成重建宏前，AI 会要求确认仍无语义的尺寸。

每条记录包含数值、单位、几何类型、来源、测点坐标、方向、精度和来源方式。重建新 `.CATPart` 时，生成规则要求先创建有语义名称的 CATIA Parameters，再让草图约束和特征引用这些参数；不会只在宏中硬编码裸数值。

### 分析与自动建模

1. 打开 **测量账本**。在“引导式原生测量”下拉框中选择总长、总宽、总高、厚度、孔径、孔距、圆角半径、中心距、槽宽、槽深等建模尺寸；面板也会自动选中下一项缺失尺寸。
2. 点击 **开始并自动记录**。面板按尺寸类型启动 CATIA Measure Item 或 Measure Between，并保存开始前的账本快照。
   - 总长、总宽、总高、厚度、孔径、圆角半径、槽宽/槽深、台阶高度默认使用单对象 Measure Item，选择一条代表尺寸的边、圆或曲面即可。
   - 孔距、中心距、偏移距离使用 Measure Between，只有这些关系尺寸才会提示依次选择两个对象。
   - 选择下拉框中的尺寸类型时会立即进入等待状态；完成一项后，面板自动选择并等待下一项缺失尺寸。因此可以在同一个 CATIA 原生测量窗口中连续完成总长、总宽、总高，不必每项重复点击“开始”。匹配时优先采用当前原生窗口的实时结果，避免被先前未分类数值干扰。
3. 如需放弃本次选择，点击 **清空当前**；它会取消当前引导并清空当前尺寸类型，但不会删除已经确认写入的测量账本。
4. 在账本列表中选择一条记录可点击 **删除选中**；点击 **清空全部** 可在确认后清空当前文档的整个本地测量账本。已删除记录会被抑制，避免仍打开的 CATIA 原生测量窗口在下一次同步时把它自动加回来；开始新的原生测量后可重新记录相同尺寸。
3. 在 CATIA 原生窗口完成这一个尺寸的测量。面板检测到唯一、类型兼容的新结果后，会自动绑定语义名称并持久化；文档切换或一次出现多个候选值时不会猜测绑定。
4. 对已经存在的旧记录，仍可在账本中手工修正名称；确认过的名称不会被后续轮询覆盖。准备度面板持续列出下一项待测尺寸。
5. 点击 **分析/自动建模**。可在底部输入框补充“主体是等截面拉伸，先 Pad 再开槽”等设计意图。
6. 尺寸不足时，Codex 只返回逐项测量清单，不生成宏；尺寸足够时才生成 `targetDocumentMode=newPart` 的 CATScript。
7. 确认后，宏通过 `CATIA.Documents.Add("Part")` 新建目标 CATPart，创建语义 Parameters、草图约束和 Part 特征。当前 3DXML/CGR 参考文档可以未保存或只读，且不会被修改。

自动建模仍遵守逐版人工确认。新 CATPart 默认不自动保存，需在 CATIA 中检查几何、参数和关键尺寸后由用户保存。

修改当前文档的写操作要求活动文档非只读且没有未保存修改；新建 CATPart 不受参考文档只读/未保存状态限制。执行前切换文档、选择或上下文会使旧宏失效，必须重新生成。

## 安全边界

- Codex 运行在独立会话目录和 `read-only` 沙箱中，不能直接执行 CATIA 写操作。
- 宏静态检查会阻止 Shell/外部进程、网络、注册表、任意文件系统、嵌套脚本执行，以及 CATIA Quit、Close、SaveAs。
- CATIA 写入只发生在面板的确认执行路径中。
- CATIA 宏调用超过 120 秒时，面板停止等待但不会强制结束 CATIA。
- 会话、提案、宏和执行结果保存在 `%LOCALAPPDATA%\CatiaAiPanel\sessions`，不包含遥测。

## 测试

无需第三方测试框架：

```powershell
dotnet run --project .\CatiaAiPanel.Tests\CatiaAiPanel.Tests.csproj
```

可选的真实 Codex CLI 启动与 resume 冒烟测试会调用当前登录的模型服务：

```powershell
dotnet run --project .\CatiaAiPanel.Tests\CatiaAiPanel.Tests.csproj -- --codex-smoke
```

CATIA 已运行并打开 Part/Product 时，可验证真实 COM 上下文读取：

```powershell
dotnet run --project .\CatiaAiPanel.Tests\CatiaAiPanel.Tests.csproj -- --catia-smoke
```

真实 CATIA 验证需手动覆盖 Part/Product 选择、只读/未保存文档、执行前切换上下文、成功宏和故意失败宏。工程不会自动启动、保存、关闭或退出 CATIA。

使用完整的合成语义账本调用真实 Codex、生成新 CATPart 宏并执行静态安全检查（不会执行 CATIA 宏）：

```powershell
dotnet run --project .\CatiaAiPanel.Tests\CatiaAiPanel.Tests.csproj -- --synthetic-model-codex-smoke
```

## 项目结构

- `CatiaAiPanel.Core`：CATIA COM、上下文、Codex JSONL、宏安全和工作流。
- `CatiaAiPanel.App`：WPF 面板、逐版确认和窗口吸附。
- `CatiaAiPanel.Tests`：纯 .NET 自动化测试及可选 Codex 在线冒烟测试。
