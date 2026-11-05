<p align="center">
  <strong>DSH Fujiang Unrestricted</strong><br>
  <em>DeepSeek Harness Desktop 一键安装包：富江人格预设 + 240 个随包技能</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.3.0-1a73e8" alt="version">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6" alt="platform">
  <img src="https://img.shields.io/badge/.NET%20Framework-4.8-512BD4" alt="dotnet">
  <img src="https://img.shields.io/badge/arch-AnyCPU-lightgrey" alt="arch">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="license">
</p>

---

## 两种安装包

**v1.3.0 已验证 DSH Desktop 0.2.2-rc.1**，修复 `tool-subagent-list-agents ... never started` 导致工作助手无法加载、新建会话失败。Windows 安装包已包含 .coop.7 的补回应与过程清单同步模块。

新版安装器保留现有本地维护目录的链接并拒绝预设降级。请重新下载本 Release 的 EXE；已下载的 v1.2.x 旧安装器不会自动获得这项保护，仍可能把本地维护版切回旧包。

**v1.2.1 修复 Desktop 新装后无法启动的问题**：如果报错指向 `cordis.patch.yml` 中 `[]` 后的 `- id: fujiang`，完全退出 DSH 后重新运行本版 EXE 即可修复，无需删除会话或重装 DSH。手机包保持 v1.2.0。

同一个 [最新 Release](https://github.com/HanKuCha/DSH-Fujiang-Unrestricted/releases/latest) 同时发布：

| 平台 | 下载 | 安装方法 |
| --- | --- | --- |
| Windows Desktop | `DSH-Fujiang-Installer.exe` 或桌面 ZIP | 退出 DSH 后运行 EXE；可浏览选择自定义安装位置 |
| DSHA 安卓 | 名称含 `DSHA` 的 ZIP | DSHA → 插件导入本地包，或粘贴该具体附件链接 |

手机包不含 Windows 安装器。手机说明与测试范围见 [mobile/README.md](mobile/README.md)，本仓库根 `package.json` 也是 DSHA 可识别的插件声明。桌面与手机核心规则和技能共用 `payload/`，不维护两个分叉的人格版本。

## 这是什么

工作助手预设与跨平台安装包。下文主要介绍 **DeepSeek Harness Desktop** 的 Windows 一键安装器，DSHA 见上方入口。

装完以后，DSH 的会话预设选择器里会多出一个 **「工作助手」（`ws-helper`）**。它由三部分组成：

1. **一份带协作工作方式的固定人格提示词** —— 整段接管系统提示词（`persona: complete: true`），内容是一个「不设限」的助手人格：保留原角色与交付规则，增加主动澄清、阶段性汇报、目标纠正、DSH 工具与子代理使用指引。
2. **全套标准工具面** —— 持久 shell、文件读写与搜索、后台任务、计划模式、子代理 / 工作流、上下文压缩、技能加载、`present` 等。
3. **240 + 3 个随包技能** —— 一个覆盖面很宽的攻防知识库，随预设一起落地到 `payload/skills`，由 `skill-filesystem` 挂载。

安装器只做四件事：**检测 DSH → 备份 profile → 复制预设与技能 → 注册依赖和 bundle**。

它**不碰** DSH 程序目录，**不关**沙箱，**不关**审批，**不改** `@deepseek-ai` 任何包文件。

---

## v1.1.1 称呼调整

默认使用自然称呼，不再要求把用户叫作“爸爸”。只改语气设定，内容回应规则和协作、工具指引保留。

## v1.1.0 协作更新

- 开始任务先说明调查方向；缺少关键信息先澄清，关键阶段主动汇报。
- 连续纠正先合并当前已收到的信息，以最后明确的目标为准。
- 补齐 DSH 工具与子代理分工指引；新增官方后台终端工具，后台查询单次等待上限十秒。
- 加入 systematic-debugging、test-driven-development、verification-before-completion 三个技能。
- 保留原有角色和内容回应规则；移除 Windows 下错误的 Android 导出路径与自动清空工作区流程。
- 单独下载即可安装的 EXE；升级会保留用户自行添加的技能。

提示词不能改变 DSH 同步工具的消息投递边界，本版不宣称已经解决即时插话或内核消息排队；回退/编辑插件也未捆绑进该公共安装包。

## 它装了什么

```
payload/
├── dsh-ws-helper-preset/           # 预设与协作技能
│   ├── cordis.patch.yml            # persona + 协作流程 + 全套工具面组合
│   └── package.json                # @local/dsh-ws-helper-preset v2.1.3-desktop-coop.1
└── skills/                         # 240 个技能，271 个文件，约 6.2 MB
    ├── hunt-sqli/ SKILL.md
    ├── hunt-xss/ SKILL.md
    ├── hunt-ssrf/ SKILL.md
    ├── offensive-active-directory/ SKILL.md
    ├── hunt-k8s/ SKILL.md
    ├── apk-redteam-pipeline/ SKILL.md
    ├── offensive-wpa3-sae/ SKILL.md
    ├── hunt-llm-ai/ SKILL.md
    ├── report-writing/ SKILL.md
    └── …（共 240 个）
```

技能分类覆盖：Web 漏洞（SQLi / XSS / SSRF / SSTI / XXE / IDOR / 竞态 / 请求走私 / 缓存投毒 / JWT / OAuth / SAML / GraphQL / WebSocket…）、Active Directory 与内网、云与容器（AWS / Azure / GCP / K8s / Docker）、移动端（APK / iOS）、无线与硬件（WPA2 / WPA3 / BLE / Zigbee / LoRaWAN）、LLM 与 AI 应用（提示词注入 / MCP / RAG / Agentic）、供应链、取证对抗、报告撰写。

---

## 环境要求

| 项目 | 要求 |
| --- | --- |
| 系统 | Windows 10 / 11（x64 或 x86 均可） |
| 运行时 | .NET Framework 4.8（Windows 10 1903+ / Windows 11 自带） |
| 宿主 | DeepSeek Harness **Desktop 0.2.x**，已至少启动过一次（生成 `desktop` profile） |
| 权限 | 普通用户即可；**不需要**管理员 |

> 安装器本身是 **AnyCPU / ILONLY** 的 .NET Framework WinForms 程序，不携带运行时，体积 28 KB。

---

## 安装

1. 先启动一次 **DSH Desktop**，确认能正常打开，然后**完全退出**（托盘也退）。
2. 从 [最新 Release](https://github.com/HanKuCha/DSH-Fujiang-Unrestricted/releases/latest) 下载并**双击 `DSH-Fujiang-Installer.exe`**；也可解压 ZIP 后运行同名文件。
3. 确认自动检测到的 `DeepSeek Harness.exe` 与 `DSH_HOME`（默认 `C:\Users\<你>\.dsh`），不对就手动改。
4. 点 **「安装到 DSH Desktop」**。成功后可勾选直接启动 DSH。
5. 打开 DSH，**新会话**在预设里选 **「工作助手」**。

> v1.1.0 的 EXE 已内嵌完整预设与技能，单独下载即可安装；不需要 .NET 10。已有旁边的 payload 时仍兼容目录式安装。

如果双击没反应或被拦截：

```
双击  修复-运行安装器.cmd      # 从当前目录调用 EXE，保留控制台窗口显示错误
```

---

## 卸载

两种方式，等价：

```
双击  卸载-工作助手预设.cmd
# 或
DSH-Fujiang-Installer.exe --uninstall
```

卸载会**先备份** profile，再从 `package.json` 里摘掉依赖与 bundle 条目，然后调用 DSH CLI 同步依赖。

**手动恢复**：关闭 DSH，把备份目录里的 `package.json`、`cordis.patch.yml` 及原有锁文件还原到 `DSH_HOME\profiles\desktop`；如果备份里有 `old-plugin`，还原到 `DSH_HOME\plugins\dsh-ws-helper-preset`，再用 DSH 的插件管理器同步一次。

| 位置 | 路径 |
| --- | --- |
| 安装日志 | `%LOCALAPPDATA%\DSH-Fujiang-Installer\logs\installer-*.log` |
| 自动备份 | `<DSH_HOME>\installer-backups\<时间戳>-<标识>\` |

---

## 校验

发布包内附 `SHA256SUMS.txt`（逐文件）。核心几项：

| 文件 | SHA-256 |
| --- | --- |
| `payload/dsh-ws-helper-preset/cordis.patch.yml` | `362CA8D9432F0EDD40DA52909236811BCBA99F3C945485B28236D0E171D3B2D9` |
| `payload/dsh-ws-helper-preset/package.json` | `02818D50857B4A5EEACA426532813011765F3FBA174127D8BEEFA483A75EF96A` |
| `DSH-Fujiang-Installer.exe` | 见 Release 附带的 `SHA256SUMS.txt` |

安装器内部也把这个预设哈希**写死**了（`ExpectedPresetSha256`）。安装完会回读 profile 里链接到的 `cordis.patch.yml` 并比对，不一致就报失败 —— 不会出现「装完了但装的不是这个预设」。

---

## 从源码构建

```powershell
# 需要 .NET Framework 4.x 自带的 csc.exe（Win10/11 内置，无需装 SDK）
powershell -ExecutionPolicy Bypass -File src\build.ps1
```

产物在 `dist\DSH-Fujiang-Installer.exe`，已内嵌完整 payload。

> 重新编译出来的 EXE 与 Release 里的**字节不同**（.NET Framework 版 csc 会写入随机 MVID 和时间戳），但源码与行为一致。要核对你手上的 EXE 是不是这份源码编的，比 Release 里的 SHA-256，而不是自己重编。

打包成发布 ZIP：

```powershell
python tools\package-release.py
# → outputs/DSH-Fujiang-Unrestricted-v<version>.zip + SHA256SUMS.txt
```

---

## 仓库结构

```
.
├── src/
│   ├── DSHInstaller.cs            # 安装器全部逻辑（WinForms，自解包 payload）
│   ├── app.manifest               # 清单（普通用户权限，不请求提权）
│   └── build.ps1                  # 一键编译
├── payload/
│   ├── dsh-ws-helper-preset/      # 预设：persona + 工具面
│   └── skills/                    # 240 个随包技能
├── package/                       # 只进发布包的辅助脚本
│   ├── 修复-运行安装器.cmd
│   └── 卸载-工作助手预设.cmd
├── docs/
│   ├── 安装说明.txt
│   └── 修复说明.txt               # 1.0.2 兼容性修复明细
├── scripts/
│   ├── test-installer.ps1         # 隔离安装与单文件 EXE 测试（中文/空格/&/%/括号路径）
│   └── test-real-cli.ps1          # 用真实 DSH CLI 在独立 DSH_HOME 里验证
├── tools/
│   ├── package-release.py         # 组装发布 ZIP + 生成 SHA256SUMS
│   └── migrate-1.0-to-1.0.2.py    # 1.0 → 1.0.2 的源码迁移脚本（留档）
├── CHANGELOG.md
└── README.md
```

---

## 安装器做了什么（逐步）

```
1. 检测      读注册表（HKLM/HKCU × 32/64 视图）Uninstall 项 + Program Files 兜底，
             找 DeepSeek Harness.exe；找不到就让你手动选
2. 校验      从 EXE 同级目录定位 resources\runtime\cli\bin\dsh.cmd，
             没有 CLI 直接报错（不会假装成功）
3. 前置检查  目标 DSH 进程还在跑就中止，避免覆盖运行中的配置
4. 预校验    先读一遍 profile package.json，确认 dependencies / dsh.profile.bundles 结构在，
             再动手写
5. 备份      复制 profiles\desktop 到 <DSH_HOME>\installer-backups\<时间戳>-<6位标识>\
6. 写入      复制 payload\dsh-ws-helper-preset → plugins\dsh-ws-helper-preset
             package.json 里 upsert 依赖 link: 路径 + 追加 bundle 条目
             （保留所有无关字段，UTF-8 无 BOM）
7. 同步      cmd.exe /d /v:off /s /c "…dsh.cmd plugin --profile desktop install"
8. 验证      回读 node_modules\@local\dsh-ws-helper-preset\cordis.patch.yml，
             比对 ExpectedPresetSha256；不符 → 失败
9. 收尾      禁用重复的独立 fujiang 注入；可选启动 DSH（带上所选 DSH_HOME）
```

全程日志落 `%LOCALAPPDATA%\DSH-Fujiang-Installer\logs\`。

---

## 常见问题

**装完新会话里没有「工作助手」**
完全退出 DSH（含托盘）再开。预设注册表在进程启动时读取，热装不会即时出现。

**安装器报「找不到随 DSH Desktop 安装的 CLI」**
你选中的 EXE 不是桌面端主程序，或者选到了 `dsh.cmd`。选 `DeepSeek Harness.exe`。

**安装器报「请完全退出 DSH Desktop 后再安装」**
任务管理器里结束 `DeepSeek Harness.exe` 后重试。

**依赖同步失败**
配置已写入但没装成链接。修正网络/权限后**重新点一次安装**即可（会再次备份）。日志里有 CLI 原始输出。

**路径里有中文、空格、`&`、`%`、括号**
1.0.2 起已专门处理并纳入测试（见 `scripts/test-installer.ps1`），可以正常安装。

**能不能只装预设、不装技能**
发布安装器校验完整 payload，不建议手动删除；技能按需加载，不会一次性执行。

**会不会影响我已有的其他插件**
不会。安装器只 upsert 它自己那一条依赖和 bundle 条目，其余字段原样保留（`scripts/test-installer.ps1` 里有「保留无关 JSON 字段」的用例）。

---

## 边界

- 只改**当前用户**的 `DSH_HOME`，不写 DSH 程序目录，不请求管理员权限。
- 不关闭 DSH 的沙箱、审批栈或工具权限。
- 不含 DSH 程序本体；你需要自行安装官方 DeepSeek Harness Desktop。本版运行时验证基线为 0.2.1-rc.5。
- 预设改变的是**提示词与回答尺度**，宿主侧的工具能力、沙箱与审批仍由 DSH 自己决定。

---

## 许可

[MIT](LICENSE)。随意改、随意分发，保留版权声明即可。

新增三个协作技能来自 obra/superpowers（MIT），详见 [第三方声明](THIRD_PARTY_NOTICES.md)。旧 240 个技能文本的出处与许可状态未逐一追溯，如你是某个技能的作者并希望调整署名或移除，请开 Issue。
