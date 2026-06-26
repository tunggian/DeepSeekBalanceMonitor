using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeepSeekBalanceMonitor
{
    internal sealed class FloatingBalanceForm : Form
    {
        private Color thBg;
        private Color thFg;
        private Color thSecondary;
        private Color thAccent;
        private Color thBorder;
        private bool darkMode;
        private string themeMode = "System";

        private string balanceText = "--";
        private readonly StatusDotControl statusDot = new StatusDotControl();
        private readonly Label statusLabel = new Label();
        private readonly RefreshIconControl refreshButton = new RefreshIconControl();
        private readonly ContextMenuStrip contextMenu = new ContextMenuStrip();
        private readonly Timer refreshAnimationTimer = new Timer();
        private int refreshAnimationStep;

        private bool dragging;
        private Point dragStart;

        private readonly Timer edgeTimer = new Timer();
        private EdgeSide dockSide = EdgeSide.Right;
        private Rectangle expandedBounds;
        private bool hidden;
        private DateTime lastShownTime = DateTime.MinValue;

        private bool autoHideEnabled = true;
        public bool AutoHideEnabled
        {
            get { return autoHideEnabled; }
            set { autoHideEnabled = value; }
        }

        public event Action SettingsRequested;
        public event Action RefreshRequested;
        public event Action ExitRequested;

        public FloatingBalanceForm()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = DpiHelper.ScaleSize(new Size(178, 40));
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer, true);

            ApplyTheme();
            BuildControls();
            TryDwmAttributes();

            WireDrag(statusLabel);
            WireDrag(statusDot);
            WireDrag(this);

            MouseEnter += (s, e) => { if (hidden) ShowExpanded(); };

            ContextMenuStrip = BuildMenu();

            refreshAnimationTimer.Interval = 90;
            refreshAnimationTimer.Tick += OnRefreshAnimationTick;

            edgeTimer.Interval = 150;
            edgeTimer.Tick += CheckAutoHide;
            edgeTimer.Start();
        }

        public void ApplySettings(AppSettings settings)
        {
            AutoHideEnabled = settings.AutoHide;
            TopMost = settings.AlwaysOnTop;
            themeMode = AppSettings.NormalizeThemeMode(settings.ThemeMode);
            ApplyTheme();
            TryDwmAttributes();
            ApplyControlColors();
            Invalidate(true);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RestoreBounds();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(thBg);
            using (var border = new Pen(thBorder))
                e.Graphics.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            using (var bar = new SolidBrush(thAccent))
                e.Graphics.FillRectangle(bar, 0, 0, DpiHelper.Px(5), ClientSize.Height);

            // 直接绘制余额文字，避免 Label 在 UserPaint 窗体上不渲染
            var valueRect = FloatingBalanceLayout.ValueBounds(ClientSize);
            using (var font = new Font("Segoe UI", 12F, FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, balanceText, font, valueRect, thFg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                edgeTimer.Stop();
                edgeTimer.Dispose();
                refreshAnimationTimer.Stop();
                refreshAnimationTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ApplyTheme()
        {
            darkMode = themeMode == "Dark" || (themeMode == "System" && IsWindowsDarkMode());

            if (darkMode)
            {
                thBg = Color.FromArgb(32, 32, 32);
                thFg = Color.FromArgb(205, 205, 210);
                thSecondary = Color.FromArgb(190, 190, 190);
                thAccent = Color.FromArgb(96, 205, 255);
                thBorder = Color.FromArgb(62, 62, 62);
            }
            else
            {
                thBg = Color.FromArgb(247, 247, 247);
                thFg = Color.FromArgb(32, 32, 32);
                thSecondary = Color.FromArgb(95, 95, 95);
                thAccent = Color.FromArgb(0, 103, 192);
                thBorder = Color.FromArgb(225, 225, 225);
            }

            BackColor = thBg;
        }

        private static bool IsWindowsDarkMode()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key == null) return false;
                    return (int)key.GetValue("AppsUseLightTheme", 1) == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private void TryDwmAttributes()
        {
            try
            {
                IntPtr h = Handle;
                int rounded = 2;
                NativeMethods.DwmSetWindowAttribute(h, 33, ref rounded, sizeof(int));
                int dark = darkMode ? 1 : 0;
                NativeMethods.DwmSetWindowAttribute(h, 20, ref dark, sizeof(int));
            }
            catch { }
        }

        private void BuildControls()
        {
            statusDot.Text = "●";
            statusDot.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            statusDot.AutoSize = true;

            statusLabel.Text = "可用";
            statusLabel.Font = new Font("Microsoft YaHei", 8F, FontStyle.Regular);
            statusLabel.AutoSize = false;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;

            refreshButton.Size = DpiHelper.ScaleSize(new Size(16, 16));
            refreshButton.Cursor = Cursors.Hand;
            refreshButton.Click += (s, e) =>
            {
                StartRefreshAnimation();
                if (RefreshRequested != null) RefreshRequested();
            };

            Controls.Add(statusDot);
            Controls.Add(statusLabel);
            Controls.Add(refreshButton);
            ApplyControlColors();
        }

        private void ApplyControlColors()
        {
            statusLabel.BackColor = thBg;
            statusLabel.ForeColor = thSecondary;
            refreshButton.IconColor = thAccent;
            UpdateStatusIndicator(statusLabel.Tag as string);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            statusDot.Bounds = FloatingBalanceLayout.StatusDotBounds(ClientSize);
            statusLabel.Bounds = FloatingBalanceLayout.StatusTextBounds(ClientSize);
            refreshButton.Bounds = FloatingBalanceLayout.RefreshIconBounds(ClientSize);
        }

        public void SetStatus(string value, string detail)
        {
            balanceText = value ?? "--";
            UpdateStatusIndicator(detail);
            Invalidate(); // 直接绘制，需要整体重绘
        }

        private void UpdateStatusIndicator(string detail)
        {
            statusLabel.Tag = detail;
            Color color;
            if (detail == "余额可用")
            {
                statusLabel.Text = "可用";
                color = darkMode ? Color.FromArgb(108, 203, 95) : Color.FromArgb(15, 123, 15);
            }
            else if (detail == "余额不可用")
            {
                statusLabel.Text = "不可用";
                color = darkMode ? Color.FromArgb(255, 95, 95) : Color.FromArgb(210, 20, 30);
            }
            else if (detail == "刷新中" || detail == "正在同步")
            {
                statusLabel.Text = "同步";
                color = thSecondary;
            }
            else
            {
                statusLabel.Text = string.IsNullOrEmpty(detail) ? "--" : detail;
                color = darkMode ? Color.FromArgb(232, 186, 0) : Color.FromArgb(180, 140, 0);
            }
            statusLabel.ForeColor = color;
            statusDot.DotColor = color;
            statusDot.Invalidate();
        }

        private void StartRefreshAnimation()
        {
            refreshAnimationStep = 0;
            refreshAnimationTimer.Stop();
            refreshAnimationTimer.Start();
        }

        private void OnRefreshAnimationTick(object sender, EventArgs e)
        {
            refreshButton.RotationStep = refreshAnimationStep;
            refreshButton.Invalidate();
            refreshAnimationStep++;
            if (refreshAnimationStep > 8)
            {
                refreshAnimationTimer.Stop();
                }
        }

        public void ShowExpanded()
        {
            hidden = false;
            if (expandedBounds.Width > 0)
                Bounds = expandedBounds;
            else
                RestoreBounds();
            Opacity = 0.98;
            lastShownTime = DateTime.Now;
            SetChildControlsVisible(true);
        }

        public void PersistCurrentBounds(AppSettings settings)
        {
            Rectangle b = hidden ? expandedBounds : Bounds;
            settings.WindowX = b.X;
            settings.WindowY = b.Y;
            settings.WindowWidth = b.Width;
            settings.WindowHeight = b.Height;
            settings.DockSide = dockSide.ToString();
        }

        private ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            m.Items.Add("立即刷新", null, (s, e) => { StartRefreshAnimation(); if (RefreshRequested != null) RefreshRequested(); });
            m.Items.Add("设置", null, (s, e) => { if (SettingsRequested != null) SettingsRequested(); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("退出", null, (s, e) => { if (ExitRequested != null) ExitRequested(); });
            return m;
        }

        private void WireDrag(Control c)
        {
            c.MouseDown += OnDragStart;
            c.MouseMove += OnDragMove;
            c.MouseUp += OnDragEnd;
        }

        private void OnDragStart(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            dragStart = e.Location;
            Capture = true; // 确保鼠标移出窗体外也能收到 MouseUp
            ShowExpanded();
        }

        private void OnDragMove(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            var screen = PointToScreen(e.Location);
            int x = screen.X - dragStart.X;
            int y = screen.Y - dragStart.Y;
            var area = Screen.FromControl(this).WorkingArea;
            // 不超出屏幕工作区
            if (x < area.Left) x = area.Left;
            if (y < area.Top) y = area.Top;
            if (x + Width > area.Right) x = area.Right - Width;
            if (y + Height > area.Bottom) y = area.Bottom - Height;
            Location = new Point(x, y);
        }

        private void OnDragEnd(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            dragging = false;
            Capture = false;
            SnapToNearestEdge();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && dragging)
            {
                // 鼠标捕获被外部释放（如系统对话框），结束拖拽
                dragging = false;
                SnapToNearestEdge();
            }
        }

        private void SnapToNearestEdge()
        {
            var area = Screen.FromControl(this).WorkingArea;
            int left = Math.Abs(Left - area.Left);
            int right = Math.Abs(area.Right - Right);
            int top = Math.Abs(Top - area.Top);
            int bottom = Math.Abs(area.Bottom - Bottom);
            int min = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
            if (min > DpiHelper.Px(50)) return;

            dockSide = min == left ? EdgeSide.Left
                     : min == right ? EdgeSide.Right
                     : min == top ? EdgeSide.Top
                     : EdgeSide.Bottom;

            int x = Left, y = Top;
            switch (dockSide)
            {
                case EdgeSide.Left:
                    x = area.Left;
                    y = Clamp(Top, area.Top, area.Bottom - Height);
                    break;
                case EdgeSide.Right:
                    x = area.Right - Width;
                    y = Clamp(Top, area.Top, area.Bottom - Height);
                    break;
                case EdgeSide.Top:
                    y = area.Top;
                    x = Clamp(Left, area.Left, area.Right - Width);
                    break;
                case EdgeSide.Bottom:
                    y = area.Bottom - Height;
                    x = Clamp(Left, area.Left, area.Right - Width);
                    break;
            }

            expandedBounds = new Rectangle(x, y, Width, Height);
            Bounds = expandedBounds;
        }

        private static int Clamp(int value, int lo, int hi)
        {
            return value < lo ? lo : value > hi ? hi : value;
        }

        private void CheckAutoHide(object sender, EventArgs e)
        {
            if (!Visible || dragging) return;
            if ((DateTime.Now - lastShownTime).TotalSeconds < 1) return;
            bool hovering = Bounds.Contains(Cursor.Position);
            if (hovering)
            {
                if (hidden) ShowExpanded();
                return;
            }
            if (!hidden && AutoHideEnabled && IsTouchingEdge())
                HideToEdge();
        }

        private bool IsTouchingEdge()
        {
            var area = Screen.FromControl(this).WorkingArea;
            return Math.Abs(Left - area.Left) <= 2
                || Math.Abs(area.Right - Right) <= 2
                || Math.Abs(Top - area.Top) <= 2
                || Math.Abs(area.Bottom - Bottom) <= 2;
        }

        private void HideToEdge()
        {
            expandedBounds = Bounds;
            Bounds = EdgeDockLayout.HiddenBounds(expandedBounds, dockSide, DpiHelper.Px(4));
            hidden = true;
            Opacity = 0.72;
            SetChildControlsVisible(false);
        }

        private void SetChildControlsVisible(bool visible)
        {
            statusDot.Visible = visible;
            statusLabel.Visible = visible;
            refreshButton.Visible = visible;
        }

        private new void RestoreBounds()
        {
            var screen = Screen.FromControl(this).WorkingArea;
            var settings = AppSettings.Load();
            ApplySettings(settings);

            EdgeSide saved;
            if (Enum.TryParse(settings.DockSide, out saved))
                dockSide = saved;

            if ((settings.WindowX != 0 || settings.WindowY != 0) && settings.WindowWidth > 0 && settings.WindowHeight > 0)
            {
                var savedSize = settings.WindowWidth > 0 && settings.WindowHeight > 0
                    ? new Size(Math.Max(settings.WindowWidth, DpiHelper.Px(178)), Math.Max(settings.WindowHeight, DpiHelper.Px(40)))
                    : DpiHelper.ScaleSize(new Size(178, 40));
                var r = new Rectangle(settings.WindowX, settings.WindowY, savedSize.Width, savedSize.Height);
                if (screen.IntersectsWith(r))
                {
                    expandedBounds = r;
                    Bounds = r;
                    return;
                }
            }

            expandedBounds = EdgeDockLayout.ExpandedBounds(screen, Size, dockSide);
            Bounds = expandedBounds;
        }



        private sealed class StatusDotControl : Control
        {
            public Color DotColor { get; set; }

            public StatusDotControl()
            {
                DotColor = Color.FromArgb(15, 123, 15);
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(DotColor))
                    e.Graphics.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
            }
        }
        private sealed class RefreshIconControl : Control
        {
            public Color IconColor { get; set; }
            public int RotationStep { get; set; }
            private Image iconImage;

            public RefreshIconControl()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                IconColor = Color.FromArgb(0, 103, 192);
                iconImage = LoadIconImage();
            }

            private static Image LoadIconImage()
            {
                string path = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(Program).Assembly.Location),
                    "refresh-icon.png");
                return System.IO.File.Exists(path) ? Image.FromFile(path) : null;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int w = Width, h = Height;
                e.Graphics.TranslateTransform(w / 2F, h / 2F);
                e.Graphics.RotateTransform(RotationStep * 45F);
                e.Graphics.TranslateTransform(-w / 2F, -h / 2F);

                if (iconImage != null)
                {
                    int size = Math.Min(w, h);
                    int pad = DpiHelper.Px(1);
                    e.Graphics.DrawImage(iconImage,
                        new Rectangle(pad, pad, size - pad * 2, size - pad * 2),
                        new Rectangle(0, 0, iconImage.Width, iconImage.Height),
                        GraphicsUnit.Pixel);
                }
                else
                {
                    // ponytail: fallback — 基本的圆弧箭头
                    float pw = DpiHelper.Px(2);
                    int m = DpiHelper.Px(3);
                    int s = w - m * 2;
                    using (var pen = new Pen(IconColor, pw))
                    {
                        pen.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
                        e.Graphics.DrawArc(pen, m, m, s, s, 135, 270);
                    }
                }
            }
        }
        protected override void WndProc(ref Message m)
        {
            const int WM_SETTINGCHANGE = 0x001A;
            const int WM_THEMECHANGED = 0x031A;
            if (m.Msg == WM_SETTINGCHANGE || m.Msg == WM_THEMECHANGED)
            {
                ApplyTheme();
                TryDwmAttributes();
                ApplyControlColors();
                Invalidate(true);
            }
            base.WndProc(ref m);
        }
    }
}





