using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeepSeekBalanceMonitor
{
    /// <summary>
    /// 托盘图标主上下文 — 管理刷新周期、托盘菜单、浮窗联动。
    /// 修复：async void 安全模式、Interlocked 线程安全、首次运行引导、通知提示。
    /// </summary>
    internal sealed class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon notifyIcon;
        private readonly Icon appIcon;
        private readonly System.Windows.Forms.Timer refreshTimer;
        private readonly DeepSeekBalanceClient client = new DeepSeekBalanceClient();
        private readonly FloatingBalanceForm balanceForm;

        private AppSettings settings;
        private int refreshing; // 0 = false, 1 = true (Interlocked)

        // ============================================================
        //  Construction
        // ============================================================

        public TrayAppContext()
        {
            settings = AppSettings.Load();

            // ---- 加载应用图标 ----
            appIcon = LoadAppIcon();

            // ---- 浮窗 ----
            balanceForm = new FloatingBalanceForm();
            // ponytail: 无标题栏窗体，不需要设 Icon
            balanceForm.SetCachedSettings(settings);
            balanceForm.AutoHideEnabled = settings.AutoHide;
            balanceForm.SettingsRequested += ShowSettings;
            balanceForm.RefreshRequested += OnRefreshRequested;
            balanceForm.ExitRequested += ExitThread;

            // ---- 托盘图标 ----
            notifyIcon = new NotifyIcon
            {
                Icon = appIcon,
                Text = "DeepSeek 余额：未刷新",
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };
            notifyIcon.DoubleClick += delegate { ShowBalanceWindow(); };

            // ---- 刷新定时器 ----
            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Tick += OnTimerTick;
            ApplyTimer();

            // ---- 同步注册表自启状态（处理 exe 路径变更等） ----
            AppSettings.ApplyAutoStart(settings.AutoStart);

            // ---- 首次运行引导 ----
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                // 先显示浮窗
                ShowBalanceWindow();
                // 延迟一点弹出设置窗口，等 UI 稳定
                refreshTimer.Tick += OnFirstRunGuide;
            }
            else
            {
                // 正常启动：显示窗口并立即刷新
                ShowBalanceWindow();
                OnRefreshRequested();
            }
        }

        // ============================================================
        //  First-Run Guide
        // ============================================================

        private void OnFirstRunGuide(object sender, EventArgs e)
        {
            refreshTimer.Tick -= OnFirstRunGuide;
            ShowSettings();
        }

        // ============================================================
        //  Menu
        // ============================================================

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();

            var titleItem = menu.Items.Add("DeepSeek 余额监控");
            titleItem.Enabled = false;

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add("显示余额窗", null, delegate { ShowBalanceWindow(); });
            menu.Items.Add("立即刷新", null, delegate { OnRefreshRequested(); });
            menu.Items.Add("设置", null, delegate { ShowSettings(); });

            menu.Items.Add(new ToolStripSeparator());

            // 显示当前状态
            var statusItem = menu.Items.Add("就绪");
            statusItem.Enabled = false;
            statusItem.Name = "statusMenuItem";

            menu.Items.Add("退出", null, delegate { ExitThread(); });

            return menu;
        }

        private void UpdateMenuStatus(string text)
        {
            try
            {
                var menu = notifyIcon.ContextMenuStrip;
                if (menu == null) return;
                var item = menu.Items["statusMenuItem"];
                if (item != null)
                    item.Text = text ?? "";
            }
            catch
            {
                // 菜单更新失败不阻塞主流程
            }
        }

        // ============================================================
        //  Window Management
        // ============================================================

        private void ShowBalanceWindow()
        {
            if (!balanceForm.Visible)
                balanceForm.Show();
            balanceForm.ShowExpanded();
            balanceForm.Activate();
        }

        // ============================================================
        //  Timer
        // ============================================================

        private void ApplyTimer()
        {
            refreshTimer.Interval = AppSettings.NormalizeRefreshMinutes(settings.RefreshMinutes) * 60 * 1000;
            refreshTimer.Start();
        }

        // ============================================================
        //  Timer Tick – 安全的 async void
        // ============================================================

        private async void OnTimerTick(object sender, EventArgs e)
        {
            try
            {
                await RefreshBalanceAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // 终极安全网 —— 不应到达这里，但防止崩溃进程
                SetStatus("ERR", "刷新异常：" + TruncateText(ex.Message, 55), "自动恢复中");
            }
        }

        // ============================================================
        //  Refresh – 核心方法
        // ============================================================

        private void OnRefreshRequested()
        {
            // 触发 async 但不等待（由内部 catch 保证安全）
#pragma warning disable 4014
            RefreshBalanceAsync();
#pragma warning restore 4014
        }

        /// <summary>
        /// 刷新余额。
        /// 线程安全：Interlocked 防止并发刷新，CancellationToken 支持超时。
        /// 异常安全：所有异常在此方法内部捕获。
        /// </summary>
        private async Task RefreshBalanceAsync()
        {
            // 防止并发刷新
            if (Interlocked.Exchange(ref refreshing, 1) != 0)
                return;

            try
            {
                SetStatus("...", "正在同步", "刷新中");

                var snapshot = await client.GetBalanceAsync(settings.ApiKey)
                    .ConfigureAwait(true); // 回到 UI 线程更新控件

                string tooltip = "DeepSeek 余额：" + snapshot.ShortText
                    + (snapshot.IsAvailable ? "" : "（不可用）");

                SetStatus(snapshot.TaskbarText, tooltip, snapshot.IsAvailable ? "余额可用" : "余额不可用");

                UpdateMenuStatus("上次更新：" + DateTime.Now.ToShortTimeString());
            }
            catch (OperationCanceledException)
            {
                SetStatus("…", "DeepSeek 余额：请求已取消", "已取消");
            }
            catch (InvalidOperationException ex)
            {
                // 用户可理解的业务错误（Key 无效、格式错误等）
                SetStatus("ERR", "DeepSeek 余额：" + ex.Message,
                    ex.Message.Split(new[] { '\n' }, 2)[0]);
            }
            catch (Exception ex)
            {
                // 未知错误
                SetStatus("ERR", "DeepSeek 余额：" + TruncateText(ex.Message, 55),
                    "网络错误");
            }
            finally
            {
                Interlocked.Exchange(ref refreshing, 0);
            }
        }

        // ============================================================
        //  UI Update
        // ============================================================

        private void SetStatus(string iconText, string tooltip, string detail)
        {
            try
            {
                // 托盘提示 —— 超过 63 字符时从尾部截断，但保留消息前缀
                notifyIcon.Text = tooltip != null && tooltip.Length > 63
                    ? tooltip.Substring(0, 60) + "..."
                    : (tooltip ?? "DeepSeek 余额");

                // 浮窗详情
                balanceForm.SetStatus(iconText, detail);
            }
            catch
            {
                // UI 更新失败不应影响下次刷新
            }
        }

        private static Icon LoadAppIcon()
        {
            string icoPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(typeof(Program).Assembly.Location),
                "AppIcon.ico");
            if (System.IO.File.Exists(icoPath))
                return new Icon(icoPath);
            return IconPainter.Draw("DS");
        }

        // ============================================================
        //  Settings
        // ============================================================

        private void ShowSettings()
        {
            using (var form = new SettingsForm(settings))
            {
                form.PreviewChanged += balanceForm.ApplySettings;
                if (form.ShowDialog() != DialogResult.OK)
                {
                    balanceForm.ApplySettings(settings);
                    return;
                }

                settings = form.Settings;
                if (!settings.Save())
                    MessageBox.Show("设置保存失败，请检查磁盘空间或文件权限。", "保存错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                AppSettings.ApplyAutoStart(settings.AutoStart);
                balanceForm.ApplySettings(settings);
                ApplyTimer();
                OnRefreshRequested();
            }
        }

        // ============================================================
        //  Exit
        // ============================================================

        protected override void ExitThreadCore()
        {
            // 保存窗口位置
            try
            {
                balanceForm.PersistCurrentBounds(settings);
                AppSettings.ApplyAutoStart(settings.AutoStart);
                settings.Save();
            }
            catch
            {
                // 退出时保存失败不阻塞退出
            }

            refreshTimer.Stop();
            notifyIcon.Visible = false;

            // 清理资源
            notifyIcon.Dispose();
            balanceForm.Dispose();
            refreshTimer.Dispose();
            if (appIcon != null)
                appIcon.Dispose();

            base.ExitThreadCore();
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private static string TruncateText(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "…";
        }
    }
}


