# DSHA 手机插件包

适用于 [DSH-APP/DSHA](https://github.com/DSH-APP/DSHA)。手机包只包含插件声明、适配后的预设和 240 + 3 个技能，不包含 Windows EXE、CMD、安装器源码或原生二进制依赖。

## 在手机安装

1. 完成 DSHA 环境准备，确认原来的 dsh 可以正常运行。
2. 下载同一 Release 中名称含 **DSHA** 的 ZIP。可在 DSHA → 插件中导入本地包，也可直接粘贴该 ZIP 的下载链接。Release 同时有桌面 ZIP 和手机 ZIP，请选具体的 DSHA 附件链接。
3. 按 DSHA 显示的名称和版本安装并启用，再按 App 提示重启 Web。
4. 新建会话，在智能体预设里选择「工作助手」。模型和密钥继续使用你已有的配置。

也可在 DSHA 插件页使用本项目仓库 URL；主分支根 `package.json` 是 DSHA 可识别的 bundle 声明。推荐使用版本固定的 Release ZIP，避免安装到正在变动的源码。

## 与桌面版的关系

两端共享同一份核心提示词和技能来源。`mobile/build.cjs` 从 `payload/dsh-ws-helper-preset/cordis.patch.yml` 生成手机适配层：调整技能目录的相对解析，保留运行时上下文，补充 Ubuntu/Android 环境说明。手机安装不改桌面 profile，不下载 Windows 安装器。

本包不捆绑回退、编辑或即时插话的第三方插件。这些是独立的消息交互功能，不能仅靠切换人格保证。

1.3.0 增加了旧/新列表工具入口的兼容加载，并同步共享的过程清单指引。Windows 专用的自动补回应和运行时清单核对 host 模块不加载到手机包；手机不能据此保证与桌面有完全相同的自动行为。

## 兼容性和验证范围

DSHA 先前核查的文档基线为 dsh `0.2.1-rc.2`；本次桌面可用验证运行时为 `0.2.2-rc.1`。包不增加 Node 原生扩展或运行时 npm 依赖。已检查包格式、补丁与技能路径、共享提示词一致性；旧入口与移除入口的兼容分支有隔离测试。安卓真机、旧运行时完整执行与手机上的终端/设备工具尚需实际设备验收，不能把电脑检查写成手机实测。

构建：`npm ci --ignore-scripts`，然后 `npm run build:mobile`、`npm run test:mobile`，最后运行 `python tools/package-mobile.py`。生成的适配层应与共同源一起提交，避免源码安装缺失补丁。
