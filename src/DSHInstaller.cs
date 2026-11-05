using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Collections;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyVersion("1.3.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.3.0.0")]
[assembly: System.Reflection.AssemblyProduct("DSH Fujiang Unrestricted")]

namespace DSHFujiangInstaller
{
    internal static class Program
    {
        private const string ProductName = "DSH Fujiang Unrestricted";
        private const string PackageName = "@local/dsh-ws-helper-preset";
        private const string ExpectedPresetSha256 = "6B3EF552112AE3511E94905C6C295F8ACF0DE1177579D6A5FDC09B9984C8D87E";

        [STAThread]
        private static void Main(string[] args)
        {
            var logRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DSH-Fujiang-Installer", "logs");
            try { Directory.CreateDirectory(logRoot); }
            catch { logRoot = Path.GetTempPath(); }
            LogPath = Path.Combine(logRoot, "installer-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log");
            var writer = TextWriter.Synchronized(new StreamWriter(LogPath, false, new UTF8Encoding(false)) { AutoFlush = true });
            Console.SetOut(writer);
            Console.SetError(writer);
            Console.WriteLine("Installer 1.2.0; OS=" + Environment.OSVersion + "; 64bitOS=" + Environment.Is64BitOperatingSystem);
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => CleanupExtractedPayload();
            Application.ThreadException += (sender, e) => ReportError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => Console.Error.WriteLine(e.ExceptionObject);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var mode = args.Length > 0 ? args[0].TrimStart('-').ToLowerInvariant() : "install";
            if (mode != "uninstall" && args.Any(arg => string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase)))
            {
                var exitCode = RunSilent(args);
                Environment.ExitCode = exitCode;
                return;
            }
            if (mode == "uninstall")
            {
                Application.Run(new UninstallForm());
                return;
            }

            Application.Run(new InstallForm());
        }

        internal static string LogPath;
        private static string extractedPackageRoot;

        internal static string ResolvePackageRoot()
        {
            if (extractedPackageRoot != null) return extractedPackageRoot;
            var adjacent = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            if (Directory.Exists(Path.Combine(adjacent, "payload"))) return adjacent;
            using (var resource = typeof(Program).Assembly.GetManifestResourceStream("DSHFujiang.Payload.zip"))
            {
                if (resource == null) throw new InvalidDataException("安装器没有内嵌 payload，请重新下载完整 EXE 或 ZIP。");
                var tempBase = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DSH-Fujiang-Installer"));
                var target = Path.GetFullPath(Path.Combine(tempBase, "payload-" + Guid.NewGuid().ToString("N")));
                var prefix = target.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Directory.CreateDirectory(target);
                extractedPackageRoot = target;
                using (var archive = new ZipArchive(resource, ZipArchiveMode.Read))
                foreach (var entry in archive.Entries)
                {
                    var destination = Path.GetFullPath(Path.Combine(target, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("内嵌 ZIP 的路径越界。");
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (var input = entry.Open())
                    using (var output = File.Create(destination)) input.CopyTo(output);
                }
                Console.WriteLine("EXTRACT=" + target);
                return target;
            }
        }

        private static void CleanupExtractedPayload()
        {
            if (extractedPackageRoot == null) return;
            var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DSH-Fujiang-Installer")) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(extractedPackageRoot);
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;
            try { Directory.Delete(target, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal static void ReportError(Exception error)
        {
            Environment.ExitCode = 1;
            Console.Error.WriteLine(error);
            MessageBox.Show(error.Message + "\r\n\r\n日志：" + LogPath, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static int RunSilent(string[] args)
        {
            try
            {
                var dshHome = ReadOption(args, "--dsh-home") ?? InstallerService.DefaultDshHome();
                var dshRoot = ReadOption(args, "--dsh-root");
                var dshExecutable = string.IsNullOrWhiteSpace(dshRoot)
                    ? (ReadOption(args, "--dsh-exe") ?? InstallerService.DefaultDshPath())
                    : Path.Combine(dshRoot, "DeepSeek Harness.exe");
                var packageRoot = ResolvePackageRoot();
                Console.WriteLine("START packageRoot=" + packageRoot);
                Console.WriteLine("START dshHome=" + dshHome);
                var result = Task.Run(() => InstallerService.RunInstall(packageRoot, dshExecutable, dshHome)).GetAwaiter().GetResult();
                Console.WriteLine("OK");
                Console.WriteLine("BACKUP=" + result.BackupDirectory);
                foreach (var line in result.Output ?? Array.Empty<string>())
                {
                    Console.WriteLine(line);
                }
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.GetBaseException().Message);
                return 1;
            }
        }

        private static string ReadOption(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        internal static string ProductDisplayName
        {
            get { return ProductName; }
        }

        internal static string PackageId
        {
            get { return PackageName; }
        }

        internal static string ExpectedHash
        {
            get { return ExpectedPresetSha256; }
        }
    }

    internal sealed class InstallForm : Form
    {
        private readonly TextBox dshPath;
        private readonly TextBox dshHome;
        private readonly Button browseDsh;
        private readonly Button browseHome;
        private readonly Button install;
        private readonly Button cancel;
        private readonly RichTextBox log;
        private readonly ProgressBar progress;
        private readonly Label summary;
        private readonly string packageRoot;
        private bool busy;

        public InstallForm()
        {
            packageRoot = Program.ResolvePackageRoot();

            Text = Program.ProductDisplayName + " - 一键安装";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(780, 610);
            Size = new Size(840, 650);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(245, 247, 250);
            ForeColor = Color.FromArgb(31, 41, 55);

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 104,
                BackColor = Color.FromArgb(17, 24, 39),
                Padding = new Padding(22, 16, 22, 10)
            };
            var title = new Label
            {
                Text = "DSH Fujiang Unrestricted",
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 19F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.White,
                Location = new Point(22, 17)
            };
            var subtitle = new Label
            {
                Text = "DeepSeek Harness Desktop · 工作助手预设安装器",
                AutoSize = true,
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(24, 61)
            };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            Controls.Add(header);

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(22, 18, 22, 18)
            };
            Controls.Add(body);

            var pathLabel = new Label
            {
                Text = "DeepSeek Harness.exe",
                AutoSize = true,
                Location = new Point(22, 20)
            };
            dshPath = new TextBox
            {
                Location = new Point(22, 44),
                Width = 620,
                Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            browseDsh = new Button
            {
                Text = "浏览",
                Location = new Point(650, 43),
                Size = new Size(92, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            browseDsh.Click += (sender, e) => BrowseDsh();

            var homeLabel = new Label
            {
                Text = "DSH_HOME（通常不用改）",
                AutoSize = true,
                Location = new Point(22, 86)
            };
            dshHome = new TextBox
            {
                Location = new Point(22, 110),
                Width = 620,
                Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            browseHome = new Button
            {
                Text = "浏览",
                Location = new Point(650, 109),
                Size = new Size(92, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            browseHome.Click += (sender, e) => BrowseHome();

            summary = new Label
            {
                AutoSize = false,
                Location = new Point(22, 154),
                Width = 720,
                Height = 44,
                BackColor = Color.FromArgb(231, 238, 247),
                ForeColor = Color.FromArgb(30, 64, 110),
                Padding = new Padding(10, 8, 10, 6),
                Text = "安装协作版工作助手：主动澄清与汇报、DSH 工具与子代理指引、240 + 3 个技能；自动备份 Desktop profile。",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            log = new RichTextBox
            {
                Location = new Point(22, 214),
                Width = 720,
                Height = 270,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                DetectUrls = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            progress = new ProgressBar
            {
                Location = new Point(22, 498),
                Width = 720,
                Height = 16,
                Style = ProgressBarStyle.Continuous,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            cancel = new Button
            {
                Text = "关闭",
                Location = new Point(650, 530),
                Size = new Size(92, 34),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            cancel.Click += (sender, e) => Close();

            install = new Button
            {
                Text = "安装到 DSH Desktop",
                Location = new Point(486, 530),
                Size = new Size(152, 34),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            install.FlatAppearance.BorderSize = 0;
            install.Click += (sender, e) => BeginInstall();

            body.Controls.Add(pathLabel);
            body.Controls.Add(dshPath);
            body.Controls.Add(browseDsh);
            body.Controls.Add(homeLabel);
            body.Controls.Add(dshHome);
            body.Controls.Add(browseHome);
            body.Controls.Add(summary);
            body.Controls.Add(log);
            body.Controls.Add(progress);
            body.Controls.Add(cancel);
            body.Controls.Add(install);

            dshPath.Text = InstallerService.DefaultDshPath();
            dshHome.Text = InstallerService.DefaultDshHome();
            body.BringToFront();
            FormClosing += (sender, e) => { if (busy) e.Cancel = true; };
        }

        private void BrowseDsh()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 DeepSeek Harness.exe";
                dialog.Filter = "DeepSeek Harness|DeepSeek Harness.exe|EXE 文件|*.exe";
                dialog.CheckFileExists = true;
                if (File.Exists(dshPath.Text))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(dshPath.Text);
                }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    dshPath.Text = dialog.FileName;
                }
            }
        }

        private void BrowseHome()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择 DSH_HOME";
                dialog.SelectedPath = dshHome.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    dshHome.Text = dialog.SelectedPath;
                }
            }
        }

        private void BeginInstall()
        {
            if (busy)
            {
                return;
            }

            var exe = dshPath.Text.Trim();
            var home = dshHome.Text.Trim();
            if (!File.Exists(exe))
            {
                MessageBox.Show(this, "没有找到 DSH Desktop 程序，请重新选择 DeepSeek Harness.exe。", Program.ProductDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            busy = true;
            install.Enabled = false;
            cancel.Enabled = false;
            log.Clear();
            progress.Value = 0;

            InstallerService.RunInstallAsync(packageRoot, exe, home)
                .ContinueWith(task =>
                {
                    if (InvokeRequired)
                    {
                        BeginInvoke(new Action(() => FinishInstall(task)));
                    }
                    else
                    {
                        FinishInstall(task);
                    }
                });
        }

        private void FinishInstall(System.Threading.Tasks.Task<InstallResult> task)
        {
            busy = false;
            cancel.Enabled = true;
            install.Enabled = true;
            progress.Value = 100;

            if (task.IsFaulted)
            {
                Environment.ExitCode = 1;
                Console.Error.WriteLine(task.Exception);
                var error = task.Exception == null ? "未知错误" : task.Exception.GetBaseException().Message;
                Append("安装失败：" + error);
                Append("日志：" + Program.LogPath);
                summary.BackColor = Color.FromArgb(254, 226, 226);
                summary.ForeColor = Color.FromArgb(127, 29, 29);
                summary.Text = "安装未完成。若已进入写入阶段，备份位置见日志；修正错误后可重试。";
                MessageBox.Show(this, error, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Environment.ExitCode = 0;
            var result = task.Result;
            Append("");
            Append("安装完成。");
            Append("备份目录：" + result.BackupDirectory);
            appendLines(result.Output);
            summary.BackColor = Color.FromArgb(220, 252, 231);
            summary.ForeColor = Color.FromArgb(22, 101, 52);
            summary.Text = "安装完成。Desktop 将重新启动；首次发送消息前请先选择“工作助手”预设（默认已设置为它）。";

            if (MessageBox.Show(this, "安装已完成。现在启动 DSH Desktop 吗？", Program.ProductDisplayName, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try
                {
                    var start = new ProcessStartInfo
                    {
                        FileName = result.DshExecutable,
                        WorkingDirectory = Path.GetDirectoryName(result.DshExecutable),
                        UseShellExecute = false
                    };
                    start.EnvironmentVariables["DSH_HOME"] = result.DshHome;
                    Process.Start(start);
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, "启动 DSH 失败：" + error.Message, Program.ProductDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void Append(string text)
        {
            log.AppendText(text + Environment.NewLine);
            log.SelectionStart = log.TextLength;
            log.ScrollToCaret();
        }

        private void appendLines(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                Append(line);
            }
        }
    }

    internal sealed class UninstallForm : Form
    {
        public UninstallForm()
        {
            Text = Program.ProductDisplayName + " - 卸载/修复";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 410);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(245, 247, 250);

            var title = new Label
            {
                Text = "修复或卸载预设",
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(17, 24, 39),
                Location = new Point(24, 22)
            };
            var text = new Label
            {
                Text = "此功能会从当前 DSH_HOME 的 desktop profile 移除本预设，并恢复 fujiang 注入开关。\r\n如果只是升级，请优先运行安装器重新安装。",
                AutoSize = true,
                Location = new Point(26, 78),
                ForeColor = Color.FromArgb(55, 65, 81)
            };
            var repair = new Button
            {
                Text = "重新运行安装器",
                Location = new Point(24, 160),
                Size = new Size(180, 36)
            };
            repair.Click += (sender, e) => Close();

            var remove = new Button
            {
                Text = "移除本预设",
                Location = new Point(220, 160),
                Size = new Size(180, 36),
                BackColor = Color.FromArgb(220, 38, 38),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            remove.FlatAppearance.BorderSize = 0;
            remove.Click += (sender, e) =>
            {
                var result = MessageBox.Show(this, "会备份 profile，然后移除本预设与对应的 fujiang 重复注入。继续吗？", Program.ProductDisplayName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                {
                    return;
                }
                try
                {
                    var backup = InstallerService.RemovePreset(InstallerService.DefaultDshHome());
                    MessageBox.Show(this, "已移除。备份目录：" + backup, Program.ProductDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
                catch (Exception error)
                {
                    Program.ReportError(error);
                }
            };
            var close = new Button
            {
                Text = "关闭",
                Location = new Point(416, 160),
                Size = new Size(92, 36)
            };
            close.Click += (sender, e) => Close();

            Controls.Add(title);
            Controls.Add(text);
            Controls.Add(repair);
            Controls.Add(remove);
            Controls.Add(close);
        }
    }

    internal static class InstallerService
    {
        public static string DefaultDshHome()
        {
            var home = Environment.GetEnvironmentVariable("DSH_HOME");
            if (!string.IsNullOrWhiteSpace(home))
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(home.Trim().Trim('"')));
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");
        }

        public static string DefaultDshPath()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidates = new List<string>
            {
                Path.Combine(local, "Programs", "DeepSeek Harness", "DeepSeek Harness.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DeepSeek Harness", "DeepSeek Harness.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "DeepSeek Harness", "DeepSeek Harness.exe")
            };
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (var root = RegistryKey.OpenBaseKey(hive, view))
                    using (var uninstall = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (uninstall == null) continue;
                        foreach (var name in uninstall.GetSubKeyNames())
                        using (var key = uninstall.OpenSubKey(name))
                        {
                            if (key == null || !string.Equals(key.GetValue("DisplayName") as string, "DeepSeek Harness", StringComparison.OrdinalIgnoreCase)) continue;
                            var location = key.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrWhiteSpace(location)) candidates.Add(Path.Combine(location.Trim('"'), "DeepSeek Harness.exe"));
                        }
                    }
                }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }
            }
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return candidates[0];
        }

        internal static Task<InstallResult> RunInstallAsync(string packageRoot, string dshExecutable, string dshHome)
        {
            return Task.Run(() => RunInstall(packageRoot, dshExecutable, dshHome));
        }

        internal static InstallResult RunInstall(string packageRoot, string dshExecutable, string dshHome)
        {
            Console.WriteLine("STEP validate inputs");
            if (string.IsNullOrWhiteSpace(dshHome))
            {
                throw new InvalidOperationException("DSH_HOME 不能为空。");
            }
            if (!File.Exists(dshExecutable))
            {
                throw new InvalidOperationException("找不到 DeepSeek Harness.exe：" + dshExecutable);
            }

            dshHome = Path.GetFullPath(dshHome);
            dshExecutable = Path.GetFullPath(dshExecutable);
            Console.WriteLine("DSH_HOME=" + dshHome);
            Console.WriteLine("DSH_EXE=" + dshExecutable);
            foreach (var running in Process.GetProcessesByName("DeepSeek Harness"))
            using (running)
            {
                string runningPath;
                try { runningPath = running.MainModule.FileName; }
                catch (System.ComponentModel.Win32Exception) { continue; }
                catch (InvalidOperationException) { continue; }
                if (string.Equals(runningPath, dshExecutable, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("请完全退出所选 DSH Desktop 后再安装，避免运行中的程序覆盖配置。");
            }
            var cli = Path.Combine(Path.GetDirectoryName(dshExecutable), "resources", "runtime", "cli", "bin", "dsh.cmd");
            if (!File.Exists(cli))
                throw new FileNotFoundException("找不到随 DSH Desktop 安装的 CLI，请选择正确的 DeepSeek Harness.exe。", cli);

            var payload = Path.Combine(packageRoot, "payload");
            var presetSource = Path.Combine(payload, "dsh-ws-helper-preset", "cordis.patch.yml");
            var packageSource = Path.Combine(payload, "dsh-ws-helper-preset", "package.json");
            var skillsSource = Path.Combine(payload, "skills");
            if (!File.Exists(presetSource) || !File.Exists(packageSource) || !Directory.Exists(skillsSource))
            {
                throw new InvalidOperationException("发布包内容不完整，请重新解压 ZIP 后再运行。");
            }
            var actualHash = HashFile(presetSource);
            if (!string.Equals(actualHash, Program.ExpectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("预设 SHA-256 校验失败，文件可能损坏。");
            }

            var profileRoot = Path.Combine(dshHome, "profiles", "desktop");
            var profilePackage = Path.Combine(profileRoot, "package.json");
            var profilePatch = Path.Combine(profileRoot, "cordis.patch.yml");
            if (!File.Exists(profilePackage) || !File.Exists(profilePatch))
            {
                throw new InvalidOperationException("找不到 DSH Desktop profile。请先启动一次 DSH Desktop，确认 DSH_HOME 选择正确。");
            }

            // Validate the profile before making any changes.
            var checkedProfile = ReadJson(profilePackage);
            GetDependencies(checkedProfile);
            GetBundles(checkedProfile);
            var targetPlugin = ResolvePluginTarget(checkedProfile, profileRoot, dshHome);
            RejectPresetDowngrade(packageSource, Path.Combine(targetPlugin, "package.json"));
            Console.WriteLine("STEP backup profile");
            var backup = CreateBackup(dshHome);
            Console.WriteLine("BACKUP=" + backup);
            Directory.CreateDirectory(Path.Combine(dshHome, "plugins"));
            if (Directory.Exists(targetPlugin))
            {
            var old = Path.Combine(backup, "old-plugin");
                CopyDirectory(targetPlugin, old);
            }
            Console.WriteLine("STEP copy preset");
            CopyDirectory(Path.Combine(payload, "dsh-ws-helper-preset"), targetPlugin);
            Console.WriteLine("STEP copy skills");
            CopySkills(skillsSource, Path.Combine(targetPlugin, "skills"));

            Console.WriteLine("STEP update profile package");
            var mutable = ReadJson(profilePackage);
            UpsertDependency(mutable, Program.PackageId, "link:" + targetPlugin.Replace("\\", "/"));
            UpsertBundle(mutable, Program.PackageId);
            WriteJson(profilePackage, mutable);

            Console.WriteLine("STEP disable duplicate fujiang");
            var originalPatch = File.ReadAllText(profilePatch);
            var patchText = NormalizeEmptyOverlayRoot(originalPatch);
            if (!patchText.Contains("- id: fujiang"))
            {
                patchText = patchText.TrimEnd() + Environment.NewLine + Environment.NewLine + "- id: fujiang" + Environment.NewLine + "  disabled: true" + Environment.NewLine;
            }
            if (patchText != originalPatch)
                File.WriteAllText(profilePatch, patchText, new UTF8Encoding(false));

            Console.WriteLine("STEP dependency sync");
            var output = RunCli(cli, dshHome, 300000, "plugin", "--profile", "desktop", "install");
            var cliOutput = new List<string>(Lines(output));
            var installedPreset = Path.Combine(profileRoot, "node_modules", "@local", "dsh-ws-helper-preset", "cordis.patch.yml");
            if (!File.Exists(installedPreset) || !string.Equals(HashFile(installedPreset), Program.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("依赖同步后未找到正确的预设链接，安装未完成。备份：" + backup);
            var installedRoot = Path.GetDirectoryName(installedPreset);
            var bundledVersion = Convert.ToString(ReadJson(packageSource)["version"]);
            if (Convert.ToString(ReadJson(Path.Combine(installedRoot, "package.json"))["version"]) != bundledVersion)
                throw new InvalidOperationException("实际链接的预设版本与安装包不一致。备份：" + backup);
            foreach (var module in Directory.GetFiles(Path.GetDirectoryName(packageSource), "*.mjs"))
            {
                var installedModule = Path.Combine(installedRoot, Path.GetFileName(module));
                if (!File.Exists(installedModule) || HashFile(installedModule) != HashFile(module))
                    throw new InvalidOperationException("实际链接的运行时模块不一致：" + Path.GetFileName(module) + "。备份：" + backup);
            }
            Console.WriteLine("STEP verified installed preset");

            return new InstallResult
            {
                DshExecutable = dshExecutable,
                DshHome = dshHome,
                BackupDirectory = backup,
                Output = cliOutput
            };
        }

        public static string RemovePreset(string dshHome)
        {
            var profileRoot = Path.Combine(dshHome, "profiles", "desktop");
            var profilePackage = Path.Combine(profileRoot, "package.json");
            var profilePatch = Path.Combine(profileRoot, "cordis.patch.yml");
            if (!File.Exists(profilePackage) || !File.Exists(profilePatch))
            {
                throw new InvalidOperationException("找不到 Desktop profile。");
            }

            var backup = CreateBackup(dshHome);
            Console.WriteLine("BACKUP=" + backup);
            var mutable = ReadJson(profilePackage);
            RemoveDependency(mutable, Program.PackageId);
            RemoveBundle(mutable, Program.PackageId);
            WriteJson(profilePackage, mutable);

            var lines = File.ReadAllLines(profilePatch).ToList();
            for (int i = lines.Count - 2; i >= 0; i--)
            {
                if (lines[i].Trim() == "- id: fujiang" && lines[i + 1].Trim() == "disabled: true")
                {
                    lines.RemoveAt(i);
                    lines.RemoveAt(i);
                }
            }
            File.WriteAllText(profilePatch, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false));
            return backup;
        }

        // DSH's initial overlay contains comments followed by an empty root list.
        // Remove only that root node before adding block-list entries; also repair
        // the same prefix left by older installers, including an existing fujiang row.
        private static string NormalizeEmptyOverlayRoot(string text)
        {
            var root = Regex.Match(text, @"\A(?<leading>(?:(?:[ \t]*\#[^\r\n]*)?[ \t]*(?:\r?\n|\z))*)(?<empty>[ \t]*\[[ \t]*\][ \t]*)(?<comment>\#[^\r\n]*)?(?=\r?\n|\z)");
            if (!root.Success) return text;
            return text.Remove(root.Groups["empty"].Index, root.Groups["empty"].Length);
        }

        private static string ResolvePluginTarget(Dictionary<string, object> profile, string profileRoot, string dshHome)
        {
            object dependency;
            if (GetDependencies(profile).TryGetValue(Program.PackageId, out dependency))
            {
                var link = dependency as string;
                if (link != null && link.StartsWith("link:", StringComparison.OrdinalIgnoreCase))
                {
                    var target = Path.GetFullPath(Path.Combine(profileRoot, link.Substring(5)));
                    var homePrefix = Path.GetFullPath(dshHome).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    var manifest = Path.Combine(target, "package.json");
                    if (!target.StartsWith(homePrefix, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("现有预设链接位于所选 DSH_HOME 之外，请先核实该自定义位置，安装器不会替换它。");
                    if (File.Exists(manifest) && Convert.ToString(ReadJson(manifest)["name"]) == Program.PackageId) return target;
                    throw new InvalidOperationException("现有预设链接缺失或不是工作助手，请先检查：" + target);
                }
            }
            return Path.Combine(dshHome, "plugins", "dsh-ws-helper-preset");
        }

        private static void RejectPresetDowngrade(string bundledManifest, string installedManifest)
        {
            if (!File.Exists(installedManifest)) return;
            var bundled = Convert.ToString(ReadJson(bundledManifest)["version"]);
            var installed = Convert.ToString(ReadJson(installedManifest)["version"]);
            var pattern = @"^(\d+)\.(\d+)\.(\d+)(?:-desktop-coop\.(\d+))?";
            var source = Regex.Match(bundled, pattern);
            var current = Regex.Match(installed, pattern);
            if (!source.Success || !current.Success) throw new InvalidDataException("无法比较预设版本，安装未执行。");
            for (int i = 1; i <= 4; i++)
            {
                var a = source.Groups[i].Success ? int.Parse(source.Groups[i].Value) : 0;
                var b = current.Groups[i].Success ? int.Parse(current.Groups[i].Value) : 0;
                if (a > b) return;
                if (a < b) throw new InvalidOperationException("拒绝降级工作助手：本机 " + installed + " 比安装包 " + bundled + " 更新。");
            }
        }

        private static string CreateBackup(string dshHome)
        {
            var profileRoot = Path.Combine(dshHome, "profiles", "desktop");
            var root = Path.Combine(dshHome, "installer-backups", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(root);
            foreach (var name in new[] { "package.json", "cordis.patch.yml", "pnpm-lock.yaml", "pnpm-workspace.yaml" })
            {
                var source = Path.Combine(profileRoot, name);
                if (File.Exists(source))
                {
                    File.Copy(source, Path.Combine(root, name), true);
                }
            }
            return root;
        }

        private static void CopySkills(string source, string target)
        {
            CopyDirectory(source, target);
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(directory.Replace(source, target));
            }
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var destination = file.Replace(source, target);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
        }

        private static string HashFile(string file)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(file))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
        }

        private static Dictionary<string, object> ReadJson(string file)
        {
            var root = Serializer().DeserializeObject(File.ReadAllText(file)) as Dictionary<string, object>;
            if (root == null) throw new InvalidDataException("profile package.json 必须是 JSON 对象。");
            return root;
        }

        private static Dictionary<string, object> GetObject(Dictionary<string, object> root, string key)
        {
            object value;
            if (!root.TryGetValue(key, out value) || !(value is Dictionary<string, object>))
                throw new InvalidDataException("profile package.json 缺少对象：" + key);
            return (Dictionary<string, object>)value;
        }

        private static Dictionary<string, object> GetDependencies(Dictionary<string, object> root)
        {
            return GetObject(root, "dependencies");
        }

        private static IList GetBundles(Dictionary<string, object> root)
        {
            var profile = GetObject(GetObject(root, "dsh"), "profile");
            object value;
            if (!profile.TryGetValue("bundles", out value) || !(value is IList))
                throw new InvalidDataException("profile package.json 缺少 dsh.profile.bundles 数组。");
            return (IList)value;
        }

        private static void UpsertDependency(Dictionary<string, object> root, string name, string value)
        {
            GetDependencies(root)[name] = value;
        }

        private static void RemoveDependency(Dictionary<string, object> root, string name)
        {
            GetDependencies(root).Remove(name);
        }

        private static void UpsertBundle(Dictionary<string, object> root, string name)
        {
            var bundles = new ArrayList(GetBundles(root));
            if (!bundles.Contains(name)) bundles.Add(name);
            GetObject(GetObject(root, "dsh"), "profile")["bundles"] = bundles;
        }

        private static void RemoveBundle(Dictionary<string, object> root, string name)
        {
            var bundles = new ArrayList(GetBundles(root));
            while (bundles.Contains(name)) bundles.Remove(name);
            GetObject(GetObject(root, "dsh"), "profile")["bundles"] = bundles;
        }

        private static void WriteJson(string file, Dictionary<string, object> root)
        {
            File.WriteAllText(file, Serializer().Serialize(root) + Environment.NewLine, new UTF8Encoding(false));
        }

        private static string RunCli(string cli, string dshHome, int timeoutMs, params string[] args)
        {
            var info = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                Arguments = "/d /v:off /s /c \"chcp 65001 >nul & \"%DSH_INSTALLER_CLI%\" plugin --profile desktop install\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(cli) ?? Environment.CurrentDirectory
            };
            info.EnvironmentVariables["DSH_HOME"] = dshHome;
            info.EnvironmentVariables["DSH_INSTALLER_CLI"] = cli;
            using (var process = Process.Start(info))
            {
                if (process == null) throw new InvalidOperationException("无法启动 DSH CLI。");
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutMs))
                {
                    try
                    {
                        using (var killer = Process.Start(new ProcessStartInfo
                        {
                            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe"),
                            Arguments = "/PID " + process.Id + " /T /F",
                            UseShellExecute = false, CreateNoWindow = true
                        })) { killer.WaitForExit(5000); }
                    }
                    catch { try { process.Kill(); } catch { } }
                    throw new TimeoutException("DSH CLI 依赖同步超过 " + (timeoutMs / 1000) + " 秒，安装未完成。");
                }
                var output = outputTask.GetAwaiter().GetResult();
                var error = errorTask.GetAwaiter().GetResult();
                Console.WriteLine(output);
                Console.Error.WriteLine(error);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("DSH CLI 退出码 " + process.ExitCode + "：" + output + Environment.NewLine + error);
                }
                return output + Environment.NewLine + error;
            }
        }

        private static IEnumerable<string> Lines(string value)
        {
            return value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }

    internal sealed class InstallResult
    {
        public string DshExecutable { get; set; }
        public string DshHome { get; set; }
        public string BackupDirectory { get; set; }
        public IEnumerable<string> Output { get; set; }
    }
}
