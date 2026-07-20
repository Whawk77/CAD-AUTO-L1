# Codex CLI 源码速通：上下文 / Skill / 压缩 / 记忆 / 工具 / 提示词 / 挂载时机

> 基于 **openai/codex** 源码（`codex-rs`）整理。  
> 本地参考树：`D:\work\AI\project\_codex_src\codex\codex-rs`（若已 clone）。  
> 目标：搞清楚「怎么写、为什么这样写」，而不是复述官方产品文档。

---

## 0. 总图：上下文不是一锅炖

Codex 的上下文不是「把一堆东西塞进 system prompt」，而是分层、可识别、可 diff 的 **fragment 系统**。

```
┌─────────────────────────────────────────────────────────┐
│ base_instructions（模型模板 + personality）               │  几乎每轮固定，利于 cache
├─────────────────────────────────────────────────────────┤
│ initial / developer 聚合块                                │
│  · skills catalog（目录，不是正文）                         │
│  · memory 读路径说明 + memory_summary                      │
│  · token budget / multi-agent / extensions…               │
├─────────────────────────────────────────────────────────┤
│ WorldState 片段（可 full 注入 / 可 diff）                   │
│  · AGENTS.md、environment、permissions、plugins…          │
│  role 可能是 developer 或 user（带 marker）                │
├─────────────────────────────────────────────────────────┤
│ 对话 history                                              │
│  · 真实 user / assistant / tool calls                     │
│  · 本 turn 显式注入：<skill>全文、plugin、extension…       │
└─────────────────────────────────────────────────────────┘
│ tools[] schema（另一条通道，不进消息正文）                  │
└─────────────────────────────────────────────────────────┘
```

### 核心抽象：`ContextualUserFragment`

关键文件：

- `codex-rs/context-fragments/src/fragment.rs`
- `codex-rs/core/src/context/contextual_user_message.rs`

每个可注入片段实现：

| 方法 | 作用 |
|------|------|
| `role()` | `"developer"` 或 `"user"` |
| `markers()` | 起止标签，用于识别「这是注入的，不是用户打的字」 |
| `body()` / `render()` | 真正进模型的文本 |

**为什么这样写：**  
注入内容必须能被 compaction / history filter / UI 识别；否则压缩会把 AGENTS、skill 当用户话误删或误留，diff 也无法判断「状态变了」。

### 关键源码目录速查

| 主题 | 路径 |
|------|------|
| 会话 / 初始上下文 / 挂载时机 | `core/src/session/mod.rs`, `session/inject.rs`, `session/turn.rs` |
| 上下文片段 | `core/src/context/**` |
| WorldState | `core/src/context/world_state/**` |
| AGENTS.md | `core/src/agents_md.rs` |
| 压缩 | `core/src/compact.rs`, `prompts/templates/compact/**` |
| Skills | `core-skills/src/**` |
| 工具 spec | `core/src/tools/handlers/*_spec.rs` |
| 模型 system 模板 | `core/templates/model_instructions/**` |
| Memory 读 | `ext/memories/templates/memories/read_path.md` |
| Memory 写 | `memories/write/templates/**`, `memories/README.md` |
| 权限提示 | `prompts/templates/permissions/**` |

---

## 1. 上下文注入：写什么、挂在哪、何时挂

### 1.1 挂载时机（最重要）

源码明确：**session 创建时不立刻塞满 initial context**，而是 **defer 到第一个真实 turn**  
（`session/mod.rs` 注释：*Defer initial context insertion until the first real turn starts*）。

| 时机 | 注入内容 | 策略 |
|------|----------|------|
| 首个真实 user turn | **full** initial context + WorldState full render | 建立 `reference_context_item` baseline |
| 后续稳态 turn | **只发 diff**（settings / WorldState 变化） | 省 token + 保持 prefix cache |
| `reference_context` 丢失 | 再 full 注入 | 例如某些 compaction / new window |
| mid-turn compaction | 把 full initial context 插到 **最后一条真实 user 消息前**，summary 必须在最后 | 模型训练期望的边界 |
| pre-turn / 手动 compact | **DoNotInject** initial；下一轮再 full reinject | 新窗口从干净 baseline 开始 |
| 显式 `$skill` / 结构化 skill | **本 turn 才注入 SKILL.md 全文** | 不跨 turn 自动携带 |

**最佳实践：**

1. **稳定、每轮都该遵守的规则** → AGENTS.md / base instructions / WorldState  
2. **目录级可发现能力** → Skills catalog（只 name + description）  
3. **本任务才需要的重正文** → Skill body / 工具结果 / 你自己 `read` 出来的文件  
4. **跨 session 的可检索经验** → Memory（summary 常驻，细节按需 grep）

**不要**把「整个 monorepo 架构」 thrash 进每轮 developer 消息。Codex 自己都在做 **full-once + diff-later**。

### 1.2 AGENTS.md 怎么被组装

关键文件：`core/src/agents_md.rs`、`core/src/context/world_state/agents_md.rs`、`core/src/context/user_instructions.rs`

真实算法：

1. 从 cwd **向上**找 project root（默认 marker：`.git`）  
2. 从 **root → cwd** 收集每一层 `AGENTS.md`（也支持 `AGENTS.override.md`）  
3. 拼接；user 级与 project 级用分隔符：

   ```text
   \n\n--- project-doc ---\n\n
   ```

4. 有 **总字节预算** `project_doc_max_bytes`，超了截断  

渲染形态（`UserInstructions`）：

```text
# AGENTS.md instructions for <dir>

<INSTRUCTIONS>
...正文...
</INSTRUCTIONS>
```

变化时 WorldState 会发：

- 有旧指令：先加  
  `These AGENTS.md instructions replace all previously provided AGENTS.md instructions.`  
- 清空：  
  `The previously provided AGENTS.md instructions no longer apply.`

**为什么：** 模型上下文里可能还残留旧 AGENTS；必须显式说「替换 / 失效」，否则会出现规则冲突。

#### 你该怎么写 AGENTS.md

| 写 | 不写 |
|----|------|
| 本 repo 的硬约束、目录真相、禁止事项 | 长篇教程、一次性任务说明 |
| 可执行的默认行为（提交规范、测试命令） | 会过期的临时状态 |
| 分层：全局 `~/.codex/AGENTS.md` + 项目 root + 子目录 | 把 5 个无关 monorepo 的规矩全塞进 root |
| 短、可扫描、可被截断后仍有用 | 靠「读到最后一行才生效」的关键规则 |

**层级心智：**

- `~/.codex/AGENTS.md`：你这个人的偏好  
- 项目 root：团队共享  
- 子目录 AGENTS：该包专属（Codex 会沿路径拼下来）

---

## 2. Skill 注入：两段式 progressive disclosure

这是 Codex 设计里最值得抄的部分。

### 2.1 阶段 A：Catalog（常驻 / 在 initial context 里）

关键文件：

- `core-skills/src/render.rs`
- `core/src/context/available_skills_instructions.rs`
- `core/src/session/mod.rs`（`build_initial_context_*`）

行为要点：

- **role = `developer`**
- 内容：`## Skills` + intro + 列表 +（可选）How to use  
- 每条 skill **只有** name / description / path  
- 预算：默认 **context window 的 2%**（或 8000 字符兜底）  
- description 单条硬顶 **1024 字符**  
- 预算不够：先截 description，再省略部分 skill；会告警

列表行格式大致是：

```text
- skill-name: description text (file: /abs/or/alias/path/SKILL.md)
```

预算极紧时 minimum 形态：

```text
- skill-name: (file: ...)
```

**为什么：**  
模型需要「知道有什么工具包」，但不该为 50 个 skill 各塞 5KB 正文。Catalog 是 **触发器索引**，不是说明书。

#### description 怎么写（这是技能被选中的唯一广告位）

好的 description 要同时满足：

1. **Trigger 条件**（什么任务必须用）  
2. **边界**（什么时候不要用）  
3. **可区分**（和别的 skill 不撞名）

反面例子：

```yaml
description: 帮助处理文档   # 太宽，乱触发；预算紧时截断后更废
```

正面例子：

```yaml
name: handoff
description: >
  当用户要交接/续聊/给下一个 agent 上下文时使用。
  输出交接文档到临时文件；不要用来做日常编码或 code review。
```

源码 trigger 规则（写在 `SKILLS_HOW_TO_USE_*` 常量里）：

- 用户 `$SkillName` **或** 任务明显 match description → **must use**  
- **不跨 turn 自动延续**，除非再被 mention  
- 多 skill 可叠加，但应选最小集合并说明顺序  

### 2.2 阶段 B：Body 注入（仅本 turn）

关键文件：

- `core-skills/src/injection.rs`
- `core-skills/src/skill_instructions.rs`
- `core/src/session/turn.rs`

流程：

1. `collect_explicit_skill_mentions`：结构化 `UserInput::Skill` + 文本里的 `$name`  
2. `build_skill_injections`：**读完整 `SKILL.md`**  
3. 变成 user fragment：

```xml
<skill>
<name>...</name>
<path>...</path>
...完整 SKILL.md 内容...
</skill>
```

**为什么是 user role + `<skill>`：**  
这是「本 turn 额外装载的操作手册」，语义上接近用户指定的附件，而不是永远有效的系统人格。

### 2.3 How to use 里写死的最佳实践（直接抄）

Codex 在 catalog 后挂的说明，本质是 agent 协议：

1. 决定用 skill 后，**主 agent 自己把 SKILL.md 读完**再行动  
2. 相对路径相对 skill 目录解析  
3. `references/` 按需读；**禁止**丢给 subagent 总结说明书  
4. 有 `scripts/` 就跑/改脚本，别重打大段代码  
5. progressive disclosure = 选相关文件，不是半截读说明书  
6. 多个 skill：最小集合 + 顺序 + 一句 why  

#### 你写 SKILL.md 的结构建议

```markdown
---
name: my-skill
description: 一句话触发条件 + 一句话非触发边界（总长尽量 < 300 字，绝对 < 1024）
---

# 目标
一句话成功标准。

# 何时使用 / 何时不用

# 步骤（可执行、可停）
1. …
2. …

# 需要时再读
- references/foo.md — 什么情况下打开
- scripts/bar.py — 何时跑

# 失败回退
缺文件 / 读失败时怎么办
```

**不要**把 20 页 references 塞进 SKILL.md 正文——源码明确让 agent 按需打开。

### 2.4 Skill 元数据字段（解析侧）

关键文件：`core-skills/src/model.rs`

常见字段：

- `name`, `description`, `short_description`
- `policy.allow_implicit_invocation`（是否允许被隐式匹配进 catalog）
- `policy.products`（产品限制，解析侧已有）
- `dependencies.tools`（依赖的 MCP/工具，可能触发安装流程）
- `interface.*`（UI 展示用）

---

## 3. 压缩（Compaction）：handoff，不是摘要作文

### 3.1 提示词在说什么

文件：

- `prompts/templates/compact/prompt.md`
- `prompts/templates/compact/summary_prefix.md`

`prompt.md` 极短，但方向明确：

> 你在做 CONTEXT CHECKPOINT COMPACTION。为 **另一个 LLM** 写 handoff summary。

必须覆盖：

- 当前进度与关键决策  
- 约束 / 用户偏好  
- 剩余工作与清晰 next steps  
- 继续所需的关键数据 / 例子 / 引用  

`summary_prefix.md`：

> 另一个模型已经开干并留下了 thinking 摘要；工具状态仍在；**接着做，别重复劳动**。

### 3.2 压缩后 history 长什么样

关键文件：`core/src/compact.rs`

`build_compacted_history` 逻辑：

1. （可选）initial context  
2. **从后往前**保留 user messages，总预算约 **20k tokens**（`COMPACT_USER_MESSAGE_MAX_TOKENS`）  
3. 最后是 `SUMMARY_PREFIX + 模型产出的 summary`  
4. mid-turn 时把 full initial context 插到 **最后真实 user 前**，保证 summary 在最后  

相关枚举：`InitialContextInjection`

| 变体 | 用途 |
|------|------|
| `DoNotInject` | 手动 / pre-turn compact；下一常规 turn 再 full reinject |
| `BeforeLastUserMessage` | mid-turn compact；summary 必须仍是 history 最后一项 |

超窗时 compact 循环会 **删最旧 history** 重试——**保护 prefix cache + 保留最近消息**。

压缩完还会警告：长线程多次 compact 会伤精度，**能新开 thread 就新开**。

### 3.3 对你写内容的启示

| 你控制的内容 | 压缩友好写法 |
|--------------|--------------|
| 用户消息 | 关键约束写清楚；别只丢「继续」 |
| AGENTS | 短规则，压缩后会 **reinject**，不靠 history 活 |
| Skill body | 本 turn 才有；跨 compact 别指望它还在 |
| 工具输出 | 自己也别在对话里粘贴巨量；memory 提示同样要求「摘要 + 指针」 |
| Handoff skill | 对齐 compact 哲学：进度 / 决策 / 下一步 / 引用路径 |

如果你在做自己的 agent compact prompt，Codex 的原则是：

- **handoff-oriented**，不是叙事性 recap  
- **保留真实用户消息**（意图锚点）  
- **系统态靠 reinject**，不靠塞进 summary  
- **summary 放最后**（mid-turn 训练分布）  
- **从头部修剪** 以保 cache  

---

## 4. 记忆（Memory）：渐进披露 + 后台双阶段写入

### 4.1 读路径（对主 agent）

关键文件：`ext/memories/templates/memories/read_path.md`

**永远注入：**

- 如何用 memory 的决策边界  
- `memory_summary.md` 全文（夹在 `MEMORY_SUMMARY BEGINS/ENDS`）  

**按需打开（agent 自己工具读）：**

```text
memory_summary.md     ← 已在 prompt，禁止再 open
MEMORY.md             ← 可搜索 registry，主查询面
skills/<name>/        ← 从 memory 固化出的流程包
rollout_summaries/    ← 单次 rollout 详证
```

决策规则：

- **跳过 memory**：时间、翻译、一句 shell、琐碎格式  
- **默认用 memory**：query 命中 summary 里的路径/模块、要一致性、模糊任务、非平凡且相关  

Quick pass 预算：**4–6 次搜索**就该停。

引用：最终回复末尾 `<oai-mem-citation>`（程序可解析）。  

**禁止** agent 随便改 MEMORY.md；用户明确要求更新时，只写：

```text
extensions/ad_hoc/notes/<timestamp>-slug.md
```

### 4.2 写路径（后台，不占你主对话）

关键文件：`memories/README.md`、`memories/write/templates/memories/**`

| 条件 | 行为 |
|------|------|
| 非 ephemeral、开 memory、非 sub-agent、有 state DB | session 启动后台跑 |
| Phase 1 | 多 rollout 并行抽 `raw_memory` + `rollout_summary` |
| Phase 2 | 全局锁，合并到磁盘，必要时再开 consolidation 子 agent |

Phase 1 system prompt 的精华：

- **No-op 优先**：没什么可复用就返回空字段  
- 高信号：稳定偏好 / 高杠杆流程 / 决策触发器 / 失败护盾  
- 读 rollout 顺序：**用户消息 > 工具证据 > 助手话**  
- 偏好要 **evidence → implication**，别写成空泛真理  
- 保密：密钥 redact  
- 不把 brainstorm 当 durable memory  

Phase 2 产物：

- `memory_summary.md`：极密导航（**进 system/developer 的那层**）  
- `MEMORY.md`：可 grep 的手册  
- `skills/`：固化流程  
- `rollout_summaries/`：证据库  

**为什么两阶段：**  
P1 横向扩展、P2 串行保证共享工件一致性；和 skill 的「目录 vs 正文」同一哲学。

#### 你怎么用记忆

- 想让未来 agent 少被纠正 → 多在对话里明确偏好（P1 最信用户话）  
- 想固化流程 → 做成 **repo skill** 或等 memory Phase2 生成 `skills/`  
- 别指望「模型自己记得」；**写进 AGENTS / skill / MEMORY 分层**  

---

## 5. 工具调用与描述：描述 = 行为合同

### 5.1 描述怎么写（源码风格）

关键示例：

- `core/src/tools/handlers/shell_spec.rs`
- `core/src/tools/handlers/tool_search_spec.rs`
- `core/src/tools/handlers/apply_patch_spec.rs`
- `core/templates/search_tool/tool_description.md`

规律：

1. **一句话能力**  
2. **默认值与有效范围**（如 `yield_time_ms` 默认 10000，Windows 2000–30000）  
3. **何时用参数 / 平台差异**  
4. **与其他工具的分工**（`tool_search`：MCP 发现用它，别用 `list_mcp_resources`）  
5. 复杂协议进 **prompt 模板**（permissions 的 `on_request.md`），不塞进一个超长 description  

`apply_patch` 甚至用 freeform + lark grammar，description 只强调：

> FREEFORM，不要包 JSON。

#### 工具描述检查表

| 字段 | 写什么 |
|------|--------|
| name | 动词_名词，稳定不改 |
| description | 能力 + 边界 + 与兄弟工具分工 |
| param description | 默认 / 范围 / 省略时行为 / 危险含义 |
| 重流程 | 单独 instructions fragment，不要全塞 schema |

### 5.2 Deferred tools / tool_search

大量 MCP 工具不会一次性进 tools 列表，而是：

- 源（app）描述先进 `tool_search` 的 description  
- 模型 BM25 搜索后再 expose  

**对你的启示：**  
MCP/tool 描述要 **可检索**（关键词齐全），短但含触发语；否则 search 捞不起来。

### 5.3 权限类提示

`prompts/templates/permissions/approval_policy/on_request.md` 教模型：

- 命令如何被切段评估  
- 何时 `require_escalated`  
- `prefix_rule` 要范畴合理，禁止 `["python3"]` 这种过宽  
- 破坏性命令不要 persist rule  

工具与安全策略：**schema 约束形状，prompt 约束判断。**

---

## 6. 提示词 / 模型 instructions：写的是协作界面

示例文件：

- `core/templates/model_instructions/gpt-5.2-codex_instructions_template.md`
- `core/templates/personalities/**`
- `prompts/templates/**`

主 system 通常包含：

- 身份 + personality 插槽  
- **最终答案格式**（GFM、禁止 nested bullets、文件引用规则）  
- 编辑约束（不乱 revert、不 interactive git）  
- plan tool 使用频率  
- frontend 反 slop 细则（可按模型模板切换）  

**原则：**

1. **格式规则要可机械执行**（模型输出给 TUI 渲染）  
2. **危险操作用 NEVER / ALWAYS 短句**  
3. **领域偏好单独成节**，可关可换模型模板  
4. personality 能 bake 进 base_instructions 就不重复注入（源码有 dedupe）  

自己写 agent system prompt 时，对照 Codex：  
**先协作协议，再工具策略，再领域风格**；别一上来塞项目细节（项目细节走 AGENTS/WorldState）。

---

## 7. 挂载时机速查表（背这个就够）

```text
Session start
  ├─ 后台 Memory P1/P2（条件满足时）
  ├─ 发现 skills / plugins / MCP
  └─ 不立刻塞 full context ──────────────┐
                                         ▼
First real user turn
  ├─ full initial context
  │    developer: skills catalog, memory read path+summary, …
  │    user/dev fragments: AGENTS, env, permissions (WorldState full)
  ├─ tools[] 本 turn 可用集合（含 search 可延迟加载的）
  └─ 若 user 写了 $skill → 再注入 <skill> 全文
                                         ▼
Later turns
  ├─ 只注入 WorldState/settings DIFF
  ├─ 新 $skill 再读正文；旧 skill 不自动续挂
  └─ 工具结果进 history（可被后续 compact）
                                         ▼
Auto/manual compact
  ├─ handoff summary + 保留近端 user messages
  ├─ mid-turn: reinject full context 于 last real user 前
  └─ 警告：长线程降智 → 建议新 thread
```

---

## 8. 写作检查清单

### AGENTS.md

- [ ] 每条规则能否在 1 行内执行？  
- [ ] 删掉后半截是否仍安全？（有字节截断）  
- [ ] 是否把「一次性任务」误写成永久规则？  
- [ ] 子目录专属规则是否下沉到子目录 AGENTS？  

### Skill description

- [ ] 是否写了 **trigger** 与 **anti-trigger**？  
- [ ] 是否短于 ~300 字、绝对 <1024？  
- [ ] 多个 skill 是否会撞同一个模糊词（「文档」「优化」）？  

### SKILL.md 正文

- [ ] 步骤可执行？有停损？  
- [ ] 重材料是否在 `references/` 并写清何时打开？  
- [ ] 有 scripts 是否优先 scripts？  
- [ ] 是否要求主 agent 读完再动手（对齐 Codex 协议）？  

### 工具 description

- [ ] 默认值 / 范围 / 省略行为是否写清？  
- [ ] 是否说明与相邻工具的分工？  
- [ ] 危险参数是否在 prompt/permissions 而不是靠模型猜？  

### Memory / Compact 友好

- [ ] 偏好是否由用户明确说出（便于 P1 抽取）？  
- [ ] 关键路径/命令是否值得进 AGENTS 或 skill，而不是只活在某次 tool 输出？  
- [ ] 长线程是否该 compact 或新开 thread？  

---

## 9. 一句话总纲（设计哲学）

Codex 整条链路都在贯彻同一条工程原则：

> **常驻索引要极瘦，正文按需装载；状态可 diff；压缩做 handoff；记忆做渐进披露；工具描述写合同。**

对应到你的写法：

| 层级 | 形态 | 寿命 |
|------|------|------|
| 人格 / 格式 / 安全 | base instructions | 几乎永久 |
| 项目法律 | AGENTS.md | 项目级，可 diff 更新 |
| 能力索引 | skill/tool description | 每 session catalog |
| 操作手册 | SKILL.md / references | 触发 turn |
| 会话状态 | history + tool outputs | 直到 compact |
| 跨会话经验 | memory_summary → MEMORY → rollouts | 渐进 |

---

## 10. 自己写 Agent 时的对照映射

| 你要做的事 | 抄 Codex 的哪一层 |
|------------|-------------------|
| 全局人格与输出格式 | base_instructions / model template |
| 项目约定 | AGENTS.md + WorldState full/diff |
| 可选能力包 | Skill catalog（瘦）+ body（胖、按触发） |
| 上下文满了 | handoff compact + reinject 系统态 |
| 跨会话学习 | summary 常驻 + 可检索 handbook + 证据库 |
| 工具爆炸 | 核心工具常驻 + tool_search 延迟加载 |
| 注入识别 | 固定 marker / role，方便过滤与压缩 |

---

## 附录：本地阅读路径

若已按之前会话 clone：

```text
D:\work\AI\project\_codex_src\codex\codex-rs\
  core\src\session\
  core\src\context\
  core\src\compact.rs
  core-skills\src\
  prompts\templates\
  memories\
  ext\memories\templates\memories\read_path.md
```

官方仓库：<https://github.com/openai/codex>
