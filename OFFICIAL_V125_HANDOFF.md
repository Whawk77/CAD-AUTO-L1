# AUTOFIXDIM / ASD 正式版 v125 交接

整理时间：2026-05-22

补充：2026-05-23 之后的 LA 系列实验开发在 `D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340` 继续推进；本文件仍只记录正式 `v125` 基线，不作为 LA 实验线的最新规则说明。

## 结论

`v125` 是当前正式推进基线。`D:\work\AI\project\L1\bin\Debug\autofixdim-v125.dll` 已从正式源码目录复制，并与正式源码输出、工具箱 DLL 哈希一致。

不要把实验版本语义重构内容混入正式版。后续“横向尺寸标注规则”应从正式 `v125` 源码继续推进。

## 文件位置

- 正式源码：`D:\work\AI\project\autocad-net-c-autocad-autocad-net`
- L1 源码备份：`D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340`
- L1 正式 DLL：`D:\work\AI\project\L1\bin\Debug\autofixdim-v125.dll`
- 正式源码 DLL：`D:\work\AI\project\autocad-net-c-autocad-autocad-net\bin\Debug\autofixdim-v125.dll`
- 工具箱 DLL：`D:\app\不加班的小刘_工具箱\dll\autofixdim-v125.dll`

## v125 DLL 校验

SHA256：`1304053EF0F47D61D9BC772FBDB397B2A24F18041022BCA1045946CBA1A16A2A`

三处文件哈希一致：

- `D:\work\AI\project\autocad-net-c-autocad-autocad-net\bin\Debug\autofixdim-v125.dll`
- `D:\work\AI\project\L1\bin\Debug\autofixdim-v125.dll`
- `D:\app\不加班的小刘_工具箱\dll\autofixdim-v125.dll`

## 工程信息

- 项目文件：`AutoFixtureDim.csproj`
- 目标框架：`.NET Framework 4.7.2`
- AutoCAD：AutoCAD 2020
- 主命令：`ASD`
- 兼容命令：`AUTOFIXDIM`、`AUTOFIXDIMREGEN`、`AUTOFIXDIMCLEAR`
- 标注清理依据：生成对象带 `AUTOFIXDIM` XData
- 标准编译命令：`dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /v:minimal`

## 正式 v125 已包含的关键规则

### 总尺寸

- `OverallWidth` 永远取 MainOutline 的 `MinX -> MaxX`。
- `OverallHeight` 永远取 MainOutline 的 `MinY -> MaxY`，并保持在左侧。
- 总尺寸优先级最高，不被局部台阶、倒角、圆角尺寸替代。

### 左侧竖向阶梯链

入口：`DimensionDrawer.DrawRightStepHeight(outline)`。

当前规则：

1. 收集外轮廓水平线段。
2. 按 Y 高度分组。
3. 每组取未被忽略的最左端点。
4. 按 Y 从高到低生成相邻竖向候选链。
5. 检查端点向左到左侧标注线的水平尺寸界线路径。
6. 如果穿过、接触、占用外轮廓，或候选与斜边语义冲突，则记录端点并整链重算。
7. 稳定后执行“删除向左延伸线最长的一条”防闭链兜底。
8. 剩余候选加入 `_leftDims`。

### 右侧竖向阶梯链

入口：`DimensionDrawer.DrawRightSideStepHeight(outline)`。

当前规则与左侧独立，不共享忽略端点状态：

1. 收集外轮廓水平线段。
2. 按 Y 高度分组。
3. 每组取未被忽略的最右端点。
4. 按 Y 从高到低生成相邻竖向候选链。
5. 检查端点向右到右侧标注线的水平尺寸界线路径。
6. 如果穿过、接触、占用外轮廓，或候选与斜边语义冲突，则记录端点并整链重算。
7. 稳定后执行“删除向右延伸线最长的一条”防闭链兜底。
8. 剩余候选加入 `_rightDims`。

### 左右独立性

`v125` 的左右竖向阶梯链是顺序运行、独立计算：

- 左侧使用自己的 `ignoredPoints`。
- 右侧使用自己的 `ignoredPoints`。
- 左侧排除过的端点不限制右侧。
- 右侧排除过的端点不反向影响左侧。
- 最终都交给 `FlushStackedDimensions(outline)` 统一堆叠绘制。

## 下一步：横向尺寸标注规则入口

横向尺寸建议从正式 `v125` 的 `DimensionDrawer.cs` 继续，不要从实验语义版本开始。

重点入口：

- `DrawStepOutlineDimensions(outline)`：当前调用顺序为顶部/底部横向规则、左侧竖向规则、右侧竖向规则。
- `DrawTopStepWidth(outline)`：现有顶部横向局部尺寸逻辑。
- `DrawLowerRightStepWidth(outline)`：现有底部/右下局部横向尺寸逻辑。
- `_bottomDims`、`_topDims`：横向尺寸最终应进入对应侧的延迟队列。
- `DeferredDim`：统一候选尺寸结构。
- `FlushStackedDimensions(outline)`：统一堆叠与冲突处理。

建议新规则仍遵守当前正式版方向：

1. 先识别横向结构候选，不直接按单根线段裸标注。
2. 左/右竖向阶梯链不要参与横向端点忽略状态。
3. 横向规则内部如需“记录端点并重算”，应独立维护自己的状态。
4. 不覆盖 `OverallWidth`。
5. 不重复倒角、圆角、孔位、槽位已经表达的语义尺寸。
6. 输出前交给 `_bottomDims` 或 `_topDims`，让统一堆叠逻辑处理位置。

## 正式版边界

以下内容不属于正式 `v125` 基线：

- 语义识别“推倒重来”的实验版本。
- 右侧局部语义尺寸实验版本。
- `v126` 及之后实验 DLL。

后续如要引入语义识别重构，应先在实验目录验证，再决定是否合入正式 `v125` 后续版本。

## 交接检查

- `v125` 已复制到 `D:\work\AI\project\L1\bin\Debug`。
- DLL 哈希已核对一致。
- L1 已有正式源码备份。
- 本次未修改正式源码目录。
- 本次未删除任何文件。
