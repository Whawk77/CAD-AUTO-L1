# 2026-10-17 消融证据归档

运行 ID：`20261017-141216-fa77bf2a`（执行主机时钟）。状态：`BlockedExistingRegression`。这是实际执行结果，不是审批后的标注真值，也不是生产 CAD 验收。

- `REPORT.md`：基线、11 条独立消融、逐例回归、数据缺口和复现命令。
- `results.json`：完整机器结果，包含 source/input/binary SHA256、开关状态、有限契约 expected/actual、已知失败候选及逐配置差值。
- `environment.json`、`build-artifacts.json`：构建前环境/源码及实际 Core exe/DLL 指纹。
- `commands.jsonl`：实际命令、退出码、时间及原始日志名。
- `preservation.json`：起始已哈希文件的保护核对。仅 7 个既有脏源码进行预期的精确编辑，另外 56 个已哈希文件保持原样；原图、期望、原诊断和图谱未被覆写。
- `snapshots/*.json.gz`：13 个真实原始快照（完整系统、11 消融、完整系统重复），含每个案例的输入、规划候选/最终尺寸、真实夹点、抑制依据和 Core 布局坐标。gzip 解压后字节与运行原文件相同，可用 results.json 的 `snapshotSha256` 校验。
- `process-logs.json`：原始进程日志以 base64 保存，并附 SHA256，避免 Windows 编码转换造成证据损失。
- `archive.json`：归档文件的 SHA256 和原始本机运行目录。
- `graphify-isolated.json` / `graphify-isolated.log`：在独立临时源码/图谱副本更新 AST 索引的记录，不覆盖用户原先的脏图谱。

原始未压缩产物目录：

```text
regression/ablation/runs/20261017-delivery/
```

REPORT 中的 `snapshots/<规则>.json`、命令中的 `.log` 相对于上述原始目录（或 results.json 的 `outputDirectory`）。移至其他机器时，解压本归档快照到独立目录即可查看完整证据；重新执行不需要复制旧二进制：

```powershell
python -m unittest discover -s scripts -p test_ablation_metrics.py
python scripts/run-ablation.py
```

新运行必须使用全新目录，不覆盖此归档，不刷新兼容性指纹，不更改人工期望。详见 `docs/AblationExperiments.md`。本轮没有编译/部署 AutoCAD 插件、启动 CAD、修改 DWG 或执行 Git 提交。
