# DSH Fujiang Unrestricted

A one-click Windows installer for a **DeepSeek Harness Desktop 0.2.x** agent preset.

After installing, a new preset called **「工作助手」(`ws-helper`)** shows up in DSH's
session preset picker. It bundles three things:

1. **A fixed persona prompt** that takes over the system prompt entirely
   (`persona: complete: true`) — refusal blacklist, intent mapping, a four-rung
   fallback ladder, no-midway-stop, and a deliverable contract.
2. **The full standard tool plane** — persistent shell, file read/write/search,
   background jobs, plan mode, subagents & workflows, context compaction, skill
   loading, `present`.
3. **240 existing + 3 collaboration skills** — a broad offensive-security knowledge library mounted
   through `skill-filesystem`.

The installer only does four things: **detect DSH → back up the profile → copy the
preset and skills → register the dependency and bundle entry.**

It does **not** touch the DSH program directory, does **not** disable the sandbox,
does **not** disable approvals, and does **not** modify any `@deepseek-ai` package file.

## Version 1.1.0

Adds clarification, progress updates, latest-goal handling, DSH tool and subagent guidance, background terminal tools, and three debugging/verification skills. Preserves the original persona and response rules. Upgrades preserve user-added skills. Runtime checks target Desktop 0.2.1-rc.5; this release does not claim to fix kernel message scheduling or add message editing.

## DSHA for Android

The same release includes `DSH-Fujiang-DSHA-v1.3.0.zip`. Import that ZIP in DSHA Plugins, or paste its exact asset URL. No Windows installer files are included. Both adapters share the preset and skills. Desktop-only reply and todo host hooks are excluded from the mobile composition; see [mobile instructions](mobile/README.md) for verification limits.

## Requirements

| | |
| --- | --- |
| OS | Windows 10 / 11 (x64 or x86) |
| Runtime | .NET Framework 4.8 (built into Win10 1903+ / Win11) |
| Host | DeepSeek Harness **Desktop 0.2.x**, started at least once |
| Privileges | Standard user — **no admin required** |

The installer is an AnyCPU / ILONLY .NET Framework WinForms binary with an embedded compressed payload. It ships no runtime.

## Install

1. Launch DSH Desktop once, confirm it opens, then **fully exit** it (tray included).
2. Download **`DSH-Fujiang-Installer.exe`** from the latest release and run it, or extract the ZIP and run the same EXE.
3. Confirm the detected `DeepSeek Harness.exe` and `DSH_HOME` (default `C:\Users\<you>\.dsh`).
4. Click **「安装到 DSH Desktop」**.
5. Open DSH and pick **「工作助手」** in a new session.

Version 1.1.0 runs standalone: the EXE embeds the complete preset and skills. Adjacent payload folders remain supported.

If double-clicking does nothing, run `修复-运行安装器.cmd`, which keeps a console
window open so you can read the error.

## Uninstall

```
卸载-工作助手预设.cmd
# or
DSH-Fujiang-Installer.exe --uninstall
```

The uninstaller backs up the profile first, removes its dependency and bundle entry,
then re-syncs dependencies through the DSH CLI.

| | Path |
| --- | --- |
| Logs | `%LOCALAPPDATA%\DSH-Fujiang-Installer\logs\installer-*.log` |
| Backups | `<DSH_HOME>\installer-backups\<timestamp>-<id>\` |

## Build from source

```powershell
powershell -ExecutionPolicy Bypass -File src\build.ps1
```

Output: `dist\DSH-Fujiang-Installer.exe` (embedded payload included), built with the `csc.exe` that ships
with .NET Framework — no SDK needed.

Rebuilt binaries are **byte-different** from the released one (the Framework compiler
writes a random MVID and timestamp). To verify a binary you already have, compare its
SHA-256 against the one in the release's `SHA256SUMS.txt`.

## Layout

```
src/       installer source (DSHInstaller.cs, app.manifest, build.ps1)
payload/   the preset (dsh-ws-helper-preset) and 240 skills
package/   helper .cmd files that ship inside the release ZIP
docs/      install notes and the 1.0.2 compatibility fix list (Chinese)
scripts/   isolated install tests and a real-CLI verification script
tools/     release packaging script
```

## What the installer does

Detect the Desktop EXE (registry + Program Files) → locate the bundled `dsh.cmd` →
refuse to run while DSH is live → pre-validate the profile JSON structure → back up
`profiles\desktop` → copy the preset into `plugins\` → upsert the dependency and bundle
entry (all unrelated fields preserved, UTF-8 without BOM) → run `dsh plugin --profile
desktop install` → re-read the linked `cordis.patch.yml` and compare it against the
hard-coded `ExpectedPresetSha256` → disable the duplicate standalone `fujiang` injection
→ optionally launch DSH with the chosen `DSH_HOME`.

Every step is logged. A failed dependency sync is reported as a failure, never as success.

## Scope

- Writes only to the current user's `DSH_HOME`. No program-directory writes, no elevation.
- Leaves DSH's sandbox, approval stack and tool permissions alone.
- Does not include DSH itself; install the official DeepSeek Harness Desktop first.
- The preset changes the prompt and the answering posture. Tool capability, sandboxing
  and approvals stay under DSH's control.

## License

[MIT](LICENSE).

Third-party skill texts are bundled without per-file provenance tracing. If you are the
author of a skill and want attribution changed or the text removed, please open an issue.

New collaboration skills come from obra/superpowers under MIT; see [third-party notices](THIRD_PARTY_NOTICES.md).
