param([string]$StagePath)
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot ('tests 中文 & percent% (space)-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$releaseVersion = [regex]::Match([IO.File]::ReadAllText((Join-Path (Split-Path $PSScriptRoot) 'src\DSHInstaller.cs')), 'AssemblyVersion\("(\d+\.\d+\.\d+)').Groups[1].Value
$stage = if ($StagePath) { [IO.Path]::GetFullPath($StagePath) } else { Join-Path (Split-Path $PSScriptRoot) ('outputs\DSH-Fujiang-Unrestricted-v' + $releaseVersion) }
New-Item -ItemType Directory -Path $root -Force | Out-Null
$runtime = Join-Path $root 'DSH 程序 & %APPDATA% (test)'
$cliDir = Join-Path $runtime 'resources\runtime\cli\bin'
New-Item -ItemType Directory -Path $cliDir -Force | Out-Null
$exe = Join-Path $runtime 'DeepSeek Harness.exe'
[IO.File]::WriteAllText($exe, 'test fixture, never executed')
$cli = Join-Path $cliDir 'dsh.cmd'
$utf8 = New-Object Text.UTF8Encoding($false)
$script:passed = 0
function Check($condition, $description) {
    if (!$condition) { throw "FAILED: $description" }
    $script:passed++
    Write-Output "PASS $description"
}
function New-Profile($name) {
    $homePath = Join-Path $root $name
    $profile = Join-Path $homePath 'profiles\desktop'
    New-Item -ItemType Directory -Path $profile -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $profile 'package.json'), '{"name":"fixture","private":true,"dependencies":{"keep":"1.0.0"},"dsh":{"profile":{"bundles":["keep"]}},"custom":{"中文":"保留","count":123}}', $utf8)
    [IO.File]::WriteAllText((Join-Path $profile 'cordis.patch.yml'), "[]`r`n", $utf8)
    return $homePath
}
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $stage 'DSH-Fujiang-Installer.exe'))
$service = $assembly.GetType('DSHFujiangInstaller.InstallerService')
$flags = [Reflection.BindingFlags]'Static,NonPublic,Public'
$run = $service.GetMethod('RunInstall', $flags)
function Install($homePath) {
    return $run.Invoke($null, @([string]$stage, [string]$exe, [string]$homePath))
}
# Simulate the CLI's package link creation, without installing the application.
$success = @'
@echo off
setlocal DisableDelayedExpansion
if not "%1 %2 %3 %4"=="plugin --profile desktop install" exit /b 19
if not exist "%DSH_HOME%\profiles\desktop\node_modules\@local" mkdir "%DSH_HOME%\profiles\desktop\node_modules\@local"
if not exist "%DSH_HOME%\profiles\desktop\node_modules\@local\dsh-ws-helper-preset" mklink /J "%DSH_HOME%\profiles\desktop\node_modules\@local\dsh-ws-helper-preset" "%DSH_HOME%\plugins\dsh-ws-helper-preset"
exit /b 0
'@
[IO.File]::WriteAllText($cli, $success, [Text.Encoding]::ASCII)
$homePath = New-Profile '用户 中文 & %TEMP% (1)'
$originalJson = [IO.File]::ReadAllBytes((Join-Path $homePath 'profiles\desktop\package.json'))
$result = Install $homePath
Check ([IO.File]::Exists((Join-Path $result.BackupDirectory 'package.json'))) 'backup created'
Check ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $result.BackupDirectory 'package.json'))) -eq [Convert]::ToBase64String($originalJson)) 'backup preserves original bytes'
$profilePath = Join-Path $homePath 'profiles\desktop\package.json'
$profile = Get-Content -LiteralPath $profilePath -Raw -Encoding UTF8 | ConvertFrom-Json
Check ($profile.custom.'中文' -eq '保留' -and $profile.dependencies.keep -eq '1.0.0') 'unrelated JSON fields preserved'
Check ($profile.dsh.profile.bundles -contains '@local/dsh-ws-helper-preset') 'bundle registered'
Check ([IO.File]::ReadAllBytes($profilePath)[0] -ne 239) 'JSON written without UTF8 BOM'
Check (![IO.File]::ReadAllText((Join-Path $homePath 'profiles\desktop\cordis.patch.yml')).Contains('[]')) 'empty YAML sequence converted before append'
$overlayCases = @(
    @('fresh-comments', "# dsh profile patch`r`n# overrides, disables, and inserts`r`n[]`r`n", "# dsh profile patch`r`n# overrides, disables, and inserts"),
    @('inline-comment', "# preserved`n[ ] # empty root`n", '# empty root'),
    @('broken-prefix', "# preserved`r`n[]`r`n`r`n- id: fujiang`r`n  disabled: true`r`n", '# preserved'),
    @('existing-entries', "# preserve unrelated overrides`r`n- id: keep`r`n  config:`r`n    list: []`r`n", '    list: []')
)
foreach ($overlayCase in $overlayCases) {
    $overlayHome = New-Profile $overlayCase[0]
    $overlayPath = Join-Path $overlayHome 'profiles\desktop\cordis.patch.yml'
    [IO.File]::WriteAllText($overlayPath, $overlayCase[1], $utf8)
    $overlayResult = Install $overlayHome
    $repaired = [IO.File]::ReadAllText($overlayPath)
    Check ($repaired.Contains($overlayCase[2])) ("preserve comments or nested lists: " + $overlayCase[0])
    Check ([IO.File]::ReadAllText((Join-Path $overlayResult.BackupDirectory 'cordis.patch.yml')) -eq $overlayCase[1]) ("backup original overlay: " + $overlayCase[0])
    Check (@([regex]::Matches($repaired, '(?m)^- id: fujiang\r?$')).Count -eq 1) ("single fujiang override: " + $overlayCase[0])
    Check ($repaired -notmatch '(?m)^\s*\[\s*\]\s*(#.*)?\r?$') ("no empty root before block entries: " + $overlayCase[0])
}
$customSkill = Join-Path $homePath 'plugins\dsh-ws-helper-preset\skills\user-skill\SKILL.md'
New-Item -ItemType Directory -Force -Path (Split-Path $customSkill) | Out-Null
[IO.File]::WriteAllText($customSkill, 'user-owned skill', $utf8)
$result2 = Install $homePath
Check ([IO.File]::ReadAllText($customSkill) -eq 'user-owned skill') 'upgrade preserves user-added skills'
$profile = Get-Content -LiteralPath $profilePath -Raw -Encoding UTF8 | ConvertFrom-Json
Check (@($profile.dsh.profile.bundles | Where-Object { $_ -eq '@local/dsh-ws-helper-preset' }).Count -eq 1) 'repeat install does not duplicate bundle'
Check ($result.BackupDirectory -ne $result2.BackupDirectory) 'repeat install has independent backups'
$payload = Join-Path $stage 'payload'
$installed = Join-Path $homePath 'plugins\dsh-ws-helper-preset'
$mismatches = @()
foreach ($file in Get-ChildItem -LiteralPath $payload -File -Recurse) {
    $rel = $file.FullName.Substring($payload.Length + 1)
    if ($rel.StartsWith('dsh-ws-helper-preset\')) { $rel = $rel.Substring('dsh-ws-helper-preset\'.Length) }
    $dest = Join-Path $installed $rel
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $dest).Hash) { $mismatches += $rel }
}
Check ($mismatches.Count -eq 0) 'all copied payload files match hashes'
# Failures must propagate rather than report successful installation.
foreach ($case in @(@('failure', "@echo off`r`necho fixture-sync-failed`r`nexit /b 37`r`n", '37'), @('missing-link', "@echo off`r`nexit /b 0`r`n", '预设链接'))) {
    [IO.File]::WriteAllText($cli, $case[1], [Text.Encoding]::ASCII)
    $failed = $false
    try { $null = Install (New-Profile $case[0]) } catch { $failed = $_.Exception.ToString().Contains($case[2]) }
    Check $failed ("reject " + $case[0])
}
$badHome = New-Profile 'malformed'
[IO.File]::WriteAllText((Join-Path $badHome 'profiles\desktop\package.json'), '{}')
try { $null = Install $badHome; throw 'expected rejection' } catch { Check ($_.Exception.ToString().Contains('dependencies')) 'invalid profile rejected' }
Check (!(Test-Path -LiteralPath (Join-Path $badHome 'plugins'))) 'invalid profile causes no payload writes'
[IO.File]::WriteAllText($cli, $success, [Text.Encoding]::ASCII)
$remove = $service.GetMethod('RemovePreset', $flags)
$null = $remove.Invoke($null, @([string]$homePath))
$profile = Get-Content -LiteralPath $profilePath -Raw -Encoding UTF8 | ConvertFrom-Json
Check ($profile.dsh.profile.bundles -notcontains '@local/dsh-ws-helper-preset' -and $profile.dependencies.keep -eq '1.0.0') 'uninstall preserves unrelated dependency'
# Exercise the actual executable entry point, including --silent as first argument.
$silentHome = New-Profile 'silent 中文'
$proc = Start-Process -FilePath (Join-Path $stage 'DSH-Fujiang-Installer.exe') -ArgumentList @('--silent', '--dsh-home', ('"' + $silentHome + '"'), '--dsh-exe', ('"' + $exe + '"')) -PassThru -WindowStyle Hidden
if (!$proc.WaitForExit(30000)) { $proc.Kill(); throw 'Silent entry point timed out' }
Check ($proc.ExitCode -eq 0) 'EXE --silent first argument works'
[IO.File]::WriteAllText($cli, "@echo off`r`nexit /b 37`r`n", [Text.Encoding]::ASCII)
$proc = Start-Process -FilePath (Join-Path $stage 'DSH-Fujiang-Installer.exe') -ArgumentList @('--silent', '--dsh-home', ('"' + $silentHome + '"'), '--dsh-exe', ('"' + $exe + '"')) -PassThru -WindowStyle Hidden
if (!$proc.WaitForExit(30000)) { $proc.Kill(); throw 'Silent failure timed out' }
Check ($proc.ExitCode -eq 1) 'EXE propagates install failure exit code'
[IO.File]::WriteAllText($cli, $success, [Text.Encoding]::ASCII)
$standalone = Join-Path $root 'standalone EXE'
New-Item -ItemType Directory -Path $standalone | Out-Null
Copy-Item -LiteralPath (Join-Path $stage 'DSH-Fujiang-Installer.exe') -Destination $standalone
$standaloneHome = New-Profile 'standalone-home'
$proc = Start-Process -FilePath (Join-Path $standalone 'DSH-Fujiang-Installer.exe') -ArgumentList @('--silent', '--dsh-home', ('"' + $standaloneHome + '"'), '--dsh-exe', ('"' + $exe + '"')) -PassThru -WindowStyle Hidden
if (!$proc.WaitForExit(30000)) { $proc.Kill(); throw 'Standalone installer timed out' }
Check ($proc.ExitCode -eq 0) 'standalone EXE installs without adjacent payload'
Check ([IO.File]::Exists((Join-Path $standaloneHome 'plugins\dsh-ws-helper-preset\collaboration-skills\systematic-debugging\SKILL.md'))) 'standalone EXE includes collaboration skills'
Write-Output "TOTAL_PASS=$script:passed"
