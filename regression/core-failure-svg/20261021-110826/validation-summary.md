# SVG 导出验证摘要

验证目录：`D:\work\AI\9.1 托压块版本\regression\core-failure-svg\20261021-110826`

最终 v3 捕获 **29 个原失败案例、51 个实际 scene**；29/29 原失败消息复现，missing placement 为 0，86 个原源码文件哈希无变化。此任务只导出失败样例，不代表回归通过。

## 文件与产物

原项目源码/测试未变（`source-manifest.json`：86 hashes，`originalSourceChanges=[]`）。探针只改动临时隔离副本：`CadAuto.Core/Planning/DimensionPlanner.cs`、`CadAuto.Core/Rules/DimensionLayoutRules.cs`、`CadAuto.Core.Tests/Program.cs`；新增 `CadAuto.Core/Planning/FailureSvgProbe.cs`、`CadAuto.Core.Tests/Runner/FailureSvgCapture.cs`，用于失败状态采集。副本根目录：`C:\Users\Administrator\AppData\Local\Temp\asd-failure-svg-20261021-110826-50dbaf\project`。

产物为 29 个聚合 SVG + 51 个 scene SVG（80 个）、`index.html`、`failure-cases-svg.zip`，以及当前总览和样张共 6 张 PNG。SVG 文字做阅读避让，Core 尺寸线坐标未改。主代理目视了案例总览及 08、22、24 样张。

## 命令与结果

四份 v3 meta 均记为 completed、exit 0：

- `dotnet msbuild CadAuto.Core.Tests\CadAuto.Core.Tests.csproj /t:Build /p:Configuration=Debug /p:OutputPath=C:/Users/Administrator/AppData/Local/Temp/asd-failure-svg-20261021-110826-50dbaf/project/bin/FailureSvgCapture-v3/ /p:PostBuildEvent= /v:minimal` — 0。
- `C:\Users\Administrator\AppData\Local\Temp\asd-failure-svg-20261021-110826-50dbaf\project\bin\FailureSvgCapture-v3\CadAuto.Core.Tests.exe --export-failure-svg C:\Users\Administrator\AppData\Local\Temp\asd-composite-v98-validation-139a2e3b6315466d83cf94ed44e0f18f\continuation.json D:\work\AI\9.1 托压块版本\regression\core-failure-svg\20261021-110826` — 0；`capture-v3.check.json` passed，29/29 reproduction、51 scene、0 missing placement。
- `C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe C:\Users\Administrator\AppData\Local\Temp\asd-failure-svg-20261021-110826-50dbaf\render.py D:\work\AI\9.1 托压块版本\regression\core-failure-svg\20261021-110826` — 0。
- `C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe C:\Users\Administrator\AppData\Local\Temp\asd-failure-svg-20261021-110826-50dbaf\preview.cjs D:\work\AI\9.1 托压块版本\regression\core-failure-svg\20261021-110826` — 0。

辅助 `render-v3.check.json` 标记 failed：其递归计数断言将 SVG 总数预期为 29，实际根目录合计 80。主代理按目录核对 `svg\`=29、`scenes\`=51，并确认 XML 与 `validation.json` 校验成功；因此该辅助计数失败不表示 SVG 缺失或渲染失败。

## 失败、未执行与风险

原回归用例 29/29 仍失败、未修复；名称见 `validation.json` 和 `capture-manifest.json`，均记录 `sameFailureMessage=true`。第 8 例 `CadLastRunGrooveSnapsToWallClearWidthFourWay` 原错误首行为 `System.InvalidOperationException: CAD-last-run @0° must keep foot tip 20; kept=[92.55]`。原测试在 selector 断言处停止；捕获真实 kept 结构后，用相同轮廓/配置补算 Core plan，标记 `DiagnosticPlanAfterSelectionFailure`，未补画期望 20。纯布局 scene 只用实际轮廓和 dimension 定义点，不推测孔槽；未执行的后续旋转场景未生成。

初轮采集失败，v2 第 8 例为 0 scene；主代理修正后 v3 完整采集。未执行 AutoCAD 实机验证和部署，未验收真实 CAD 文字样式/尺寸界线。主代理负责审核并最终验收；SVG 预览不能证明实机显示效果。

日志入口：`D:\work\AI\9.1 托压块版本\regression\core-failure-svg\20261021-110826\logs\`（`build-v3.*`、`capture-v3.*`、`render-v3.*`、`preview-v3.*`）。报告和产物：`D:\work\AI\9.1 托压块版本\regression\core-failure-svg\20261021-110826\validation-summary.md`、`validation.json`、`capture-manifest.json`、`source-manifest.json`、`index.html`、`failure-cases-svg.zip`、`svg\`、`scenes\`、`previews\`。

未检查项目：AutoCAD 实机显示、部署及未执行的旋转场景。实际风险：回归错误仍在，且 SVG 预览无法验收 CAD 实际文字和界线；主代理负责最终验收。
