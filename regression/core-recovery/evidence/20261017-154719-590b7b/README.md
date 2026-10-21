# Core 回归恢复证据：2026-10-17

状态：目标夹点失败已修复，完整诊断执行已恢复；原回归仍有 27 项既有失败，不是全绿，也不是 DWG/CAD 验收。

- `REPORT.md`：完整分析、238 项未运行原因、根因/修复、全部 41 条实际命令及退出码、27 项未解决失败。
- `results.json`：机器摘要、逐层统计、唯一状态变化、默认差异、代码/二进制指纹和保护审计。
- `commands.jsonl`：本轮实际命令、退出码、时间、日志名；消融内部命令另在对应子目录。
- `process-logs.json`：原始进程输出按原字节 base64 保存，附 SHA256。避免 Windows 控制台编码被转换后失真。
- `reports/*.json.gz`：实际 Core 测试报告、修复前快照、保护/默认差异审计和最终检查，解压后字节与本机原文件一致。
  - `full-before-fix.json.gz`：245 执行，217 通过，28 失败，0 跳过。
  - `full-after-fix.json.gz`、`full-after-repeat.json.gz`：245 执行，218 通过，27 失败，0 跳过。
  - `gated-before-fix.json.gz`：7 执行，6 通过，1 失败，238 跳过。
  - `gated-after-fix.json.gz`、`gated-after-repeat.json.gz`：115 执行，93 通过，22 失败，130 跳过。
  - `target-after.json.gz`：目标实际通过，其余 244 是 FilteredOut，不是执行失败或门禁阻塞。
- `minimal-repro.json`：目标的 6 LINE 闭合 Core 输入、孔/基准及前后夹点；没有假称用 DWG 测试。
- `session-changes.patch`：相对任务开始时 dirty 源码的本轮增量，仅供审阅，不要盲目应用到 HEAD 或覆盖当前工作树。
- `source-before.zip`：任务开始时 Core、Core.Tests 和相关脚本源码，可解压到独立目录构建修复前单测；不是整个仓库的备份，不含生产 DWG。
- `preservation.json`：声明的 4 个既有源码编辑、原测试后缀字节一致性、其他源码保护及外部 live 诊断变化。
- `ablation-rerun/`：第一次原工具重跑；外部 live JSON 更新导致保护失败，原结果保留。
- `ablation-confirmation/`：新目录确认重跑；输入/源码/二进制保护通过，但旧默认参考仍检出两个授权修复差异，因此原工具保持退出 1。
- 两次消融目录中的 `snapshots/*.json.gz`：真实原始快照；解压后的 SHA256 与对应 results.json 的 snapshotSha256 可核对。

原始未压缩目录：

```text
regression/ablation/runs/20261017-154719-core-recovery-590b7b/
```

各报告中的 `.log`、`snapshots/*.json` 相对于本轮原始目录或对应消融目录，不是要求当前目录存在同名未压缩文件。解压/查看请用新的独立目录，不覆盖任何历史证据。

`source-before.zip` 中原测试可用原 Core.Tests 工程直接构建，运行 `ConcaveHoleDatumUsesRealOutlineIntersections` 复现修复前失败。修复后的复现及可直接执行的完整 PowerShell 命令见 `docs/CoreRegressionRecovery.md`。

本轮未修改旧消融结果、数据集、人工期望或默认参考，未提交 Git、未构建/部署 AutoCAD 插件、未启动/控制 AutoCAD。期间观察到的已有 v75 的 ASD6 live 报告属于现场并发变化，未回退、未当作本轮 CAD 验收。
