# 四方向尺寸布局自动回归

## 用途与授权

本文记录固定图样 `DL01` 的 Top、Bottom、Left、Right 四方向自动回归流程，供后续代理直接复用。

- 只有用户明确授权编译、测试、启动 AutoCAD 或执行 CAD 脚本时，才可运行本文命令。
- 自动流程采用 AutoLISP 坐标输入，不依赖鼠标框选，也不依赖 Windows Computer Use 截图接口。
- 每个方向必须独立运行，并立即归档该方向的诊断报告；`diagnostics\last-run.json` 会被下一次运行覆盖。
- 回归产物写入 `regression\dimension-layout\runs\`。该目录被 Git 忽略，不是提交基线。

## 固定输入

测试图样：

```text
regression\dimension-layout\fixtures\DL01-four-side-dimension-layout.dwg
```

图样 SHA256：

```text
CAB97F3FFA4F7A9AF613C1905BFD0CF2F7003044472CEC8E2020710B40BE6D34
```

坐标驱动脚本：

```text
scripts\cad-v203-layout-test.lsp
```

脚本内的固定输入：

- 框选左上角：`X=-148.4651, Y=276.5888, Z=0`
- 框选右下角：`X=408.9861, Y=-131.0001, Z=0`
- 基准销孔圆心：`X=245.5, Y=15, Z=0`
- X、Y 基准轮廓交点：`X=305, Y=0, Z=0`
- 销孔圆心匹配容差：`0.001`

这组坐标只适用于 `DL01`。如果更换图样，必须由用户提供新的框选范围、基准销孔圆心和轮廓基准交点，并同步调整脚本、fixture 哈希、用例与期望文件。

## 四方向映射

| 用例 ID | LSP 命令 | ASD4 方向 |
| --- | --- | --- |
| `DL01-top-block-span-order` | `V203TOP` | Top |
| `DL01-bottom-block-span-order` | `V203BOTTOM` | Bottom |
| `DL01-left-block-span-order` | `V203LEFT` | Left |
| `DL01-right-block-span-order` | `V203RIGHT` | Right |

## 版本目录规则

每次编译都必须使用一个从未使用过的 `-vNNN` 输出目录。核心测试和插件完整编译各消耗一个版本号，不能共用目录。

先找出已存在的最大版本号：

```powershell
$versionNumbers = Get-ChildItem .\bin -Directory | ForEach-Object {
    if ($_.Name -match '-v(\d+)$') { [int]$Matches[1] }
}
$nextVersion = (($versionNumbers | Measure-Object -Maximum).Maximum + 1)
$nextVersion
```

例如核心测试使用 `v225` 后，插件完整编译至少应使用 `v226`。不得覆盖或复用任何旧版本目录。

## 1. 核心回归

把下面的 `vNNN` 替换成一个新编号：

```powershell
dotnet msbuild CadAuto.Core.Tests\CadAuto.Core.Tests.csproj `
  /t:Build `
  /p:Configuration=Debug `
  "/p:OutputPath=..\bin\CoreRegression-vNNN\" `
  /v:minimal

.\bin\CoreRegression-vNNN\CadAuto.Core.Tests.exe
```

验收：进程退出码为 `0`，输出中的通过数等于总数。核心回归失败时，不进入 AutoCAD 四方向测试。

## 2. 编译插件

插件编译使用下一个全新版本号：

```powershell
dotnet msbuild AutoFixtureDim.csproj `
  /t:Build `
  /p:Configuration=Debug `
  "/p:OutDir=D:\work\AI\project\autocad-dim\bin\BlockSpanLayout-vNNN\" `
  "/p:BaseIntermediateOutputPath=obj-vNNN\" `
  /p:PostBuildEvent= `
  /p:DebugType=None `
  /p:DebugSymbols=false `
  /v:minimal
```

目标 DLL：

```text
D:\work\AI\project\autocad-dim\bin\BlockSpanLayout-vNNN\AutoFixtureDim.dll
```

记录 DLL 的 SHA256，便于把报告与实际二进制对应起来：

```powershell
Get-FileHash .\bin\BlockSpanLayout-vNNN\AutoFixtureDim.dll -Algorithm SHA256
```

## 3. 为单个方向创建可写副本

四个方向分别执行本节及后续步骤。不要让两个方向共用同一个运行目录或同一份工作 DWG。

```powershell
powershell -ExecutionPolicy Bypass `
  -File .\scripts\prepare-dimension-layout-fixture.ps1 `
  -CaseId DL01-top-block-span-order
```

脚本会：

1. 校验 fixture SHA256。
2. 在 `regression\dimension-layout\runs\<case-id>\<timestamp>\` 创建运行目录。
3. 复制一份可写 DWG。
4. 写入 `run.json`。

保留脚本输出的运行目录和工作 DWG 绝对路径。

## 4. 创建方向驱动脚本

在该方向的运行目录内创建 `run-vNNN-lsp.scr`。所有路径使用正斜杠，替换 DLL、LSP、trace 路径和方向命令：

```lisp
(setvar "FILEDIA" 0)
(setvar "CMDDIA" 0)
(command "_.NETLOAD" "D:/work/AI/project/autocad-dim/bin/BlockSpanLayout-vNNN/AutoFixtureDim.dll")
(load "D:/work/AI/project/autocad-dim/scripts/cad-v203-layout-test.lsp")
(setq *v203-trace-file* "D:/work/AI/project/autocad-dim/regression/dimension-layout/runs/<case-id>/<timestamp>/lsp-trace.txt")
(setq *v203-report-file* "D:/work/AI/project/autocad-dim/regression/dimension-layout/runs/<case-id>/<timestamp>/report-vNNN.json")
(setenv "AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH" *v203-report-file*)
V203TOP
_.QSAVE
_.QUIT
```

方向命令按上表替换为 `V203TOP`、`V203BOTTOM`、`V203LEFT` 或 `V203RIGHT`。

LSP 会临时把 `OSMODE` 设为 `0`，在固定窗口内选择对象，按圆心找到基准销孔，再调用 `ASD4` 并依次提交方向、框选集、销孔、X 基准交点和 Y 基准交点。脚本结束时会恢复原 `OSMODE`。

它对应的人工输入顺序是：框选图形 → 选择右下区域的基准销孔 → 提交右下轮廓交点作为 X 基准并以空输入确认 → 再次提交同一交点作为 Y 基准并以空输入确认。

## 5. 启动 AutoCAD 并监控 trace

```powershell
$acadExe = 'D:\Program Files\Autodesk\AutoCAD 2020\acad.exe'
$workingDwg = '<工作 DWG 的绝对路径>'
$driverScr = '<run-vNNN-lsp.scr 的绝对路径>'

$cadProcess = Start-Process `
  -FilePath $acadExe `
  -ArgumentList @('/nologo', $workingDwg, '/b', $driverScr) `
  -WindowStyle Hidden `
  -PassThru
```

每隔约 2 秒检查一次 `lsp-trace.txt`，单次等待不要超过 60 秒。成功 trace 至少包含：

```text
START side=Top
INPUT selection=98 datumHandle=<handle>
COMPLETE side=Top
```

判定规则：

- 出现 `COMPLETE side=<方向>`：进入报告归档。
- 出现 `ERROR`：本方向失败，保留 DWG、SCR、trace 和 `last-run.json` 排查。
- 未出现 `COMPLETE` 且 AutoCAD 已退出：本方向失败。
- 固定 fixture 正常选择数为 `98`；数量变化时先检查图样、选择窗口和 fixture 哈希，不要直接修改期望文件。

不要用强制结束 AutoCAD 代替错误排查。若 AutoCAD 因保存提示或模态窗口未退出，先检查窗口状态与 trace。

## 6. 核对本次诊断报告

驱动脚本通过 `AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH` 让插件直接写入该方向的独立 `report-vNNN.json`。LSP 只有确认该文件由本次命令新建或刷新后才写 `COMPLETE`。

检查报告的修改时间、`runId`、`generatedAt`、`diagnosticSide`、`drawing.fileName`、`drawing.sha256` 和 v203 布局诊断字段，确保它属于该方向和该次运行。

## 7. 校验该方向

```powershell
powershell -ExecutionPolicy Bypass `
  -File .\scripts\validate-dimension-layout-report.ps1 `
  -CaseId DL01-top-block-span-order `
  -Expectation target `
  -ReportPath '<运行目录>\report-vNNN.json'
```

四方向分别替换 `CaseId`。v203 及后续布局实现使用 `target`；`baseline` 仅用于明确复现旧基线时。

校验器会检查：

- fixture 哈希与诊断方向。
- 必需的 v203 布局诊断字段。
- 物理跨度顺序、块归属、有效跨度与同块关系。
- 维度线坐标、向外排列与约束。
- 层级连续性和层间步距。
- Left 用例允许配置中的同层多坐标例外。

只有脚本明确输出 `PASS` 才算该方向的结构化校验通过。

## 8. 图像复核

结构化校验不能替代图像复核。对每个方向保存后的工作 DWG 生成两张 PNG：

- `preview-vNNN.png`：全图，检查尺寸是否在真实外轮廓之外、是否跑到无关远端。
- `detail-vNNN.png`：仅导出该方向附近的 `DIMENSION`，检查文字、箭头、延伸线、重叠和阅读顺序。

全图脚本模板：

```lisp
(setvar "FILEDIA" 0)
(setvar "CMDDIA" 0)
(command "_.ZOOM" "_E")
(command "_.PNGOUT" "D:/<运行目录>/preview-vNNN.png" "_ALL" "")
_.QUIT
_N
```

固定图样的细节窗口可使用：

| 方向 | 细节窗口左下角 | 细节窗口右上角 |
| --- | --- | --- |
| Top | `(-40,195)` | `(345,260)` |
| Bottom | `(-40,-80)` | `(345,10)` |
| Left | `(-90,-10)` | `(5,215)` |
| Right | `(300,-10)` | `(345,110)` |

细节脚本的核心选择方式：

```lisp
(setq detailSet (ssget "_C" '(-40.0 195.0 0.0) '(345.0 260.0 0.0) '((0 . "DIMENSION"))))
(if detailSet
  (command "_.PNGOUT" "D:/<运行目录>/detail-vNNN.png" detailSet ""))
```

图像验收至少检查：

- 尺寸附着点落在真实边、圆弧、端点或真实交点。
- 尺寸线没有进入真实外轮廓内部。
- 短跨度在内、长跨度向外，块内顺序连续。
- 四方向没有明显文字、箭头或延伸线碰撞。
- 调试标签与产品尺寸分开判断；调试标签重叠不等同于产品尺寸失败。

## 9. 完成标准

一次完整自动回归必须同时满足：

1. 核心回归全部通过。
2. 插件在全新 `-vNNN` 目录编译成功。
3. Top、Bottom、Left、Right 各自在独立运行目录出现 `COMPLETE`。
4. 四份 `last-run.json` 已分别归档，未相互覆盖。
5. 四个 `target` 校验全部 `PASS`。
6. 四方向全图和细节图像复核通过。
7. 最终回复列出核心测试结果、DLL 版本与 SHA256、四方向报告路径和校验结论。

## 故障处理

### `SetIsBorderRequired failed: 不支持此接口 (0x80004002)`

这是 Windows Computer Use 捕获层错误，不是 AutoCAD 插件接口错误。不要反复重试鼠标控制；使用本文的 AutoLISP 固定坐标流程绕过截图与鼠标框选。

### 找不到基准销孔

检查：

- fixture SHA256 是否一致。
- 圆心 `(245.5,15,0)` 是否仍存在圆实体。
- 框选范围是否包含该圆。
- 模型空间与坐标系是否被修改。

### DLL 被 AutoCAD 锁定

不要覆盖已加载目录。为下一次编译使用新的 `-vNNN` 目录，并让新的 AutoCAD 进程加载新路径。

### 报告方向不匹配

`diagnostics\last-run.json` 只保存最后一次运行。重新独立执行该方向，并在 `COMPLETE` 后立即复制报告，再启动下一方向。

### 结构化校验通过但图像不合格

以图像复核为准记录失败。校验器验证布局关系，不保证文字框、箭头和延伸线在 AutoCAD 最终渲染中完全无碰撞。
