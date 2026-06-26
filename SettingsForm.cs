using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeepSeekBalanceMonitor
{
    internal sealed class SettingsForm : Form
    {
        private readonly TextBox apiKeyBox = new TextBox();
        private readonly TextBox refreshBox = new TextBox();
        private readonly ToggleSwitch autoHideSwitch = new ToggleSwitch();
        private readonly ToggleSwitch topMostSwitch = new ToggleSwitch();
        private readonly ToggleSwitch autoStartSwitch = new ToggleSwitch();
        private readonly Label validationHint = new Label();
        private readonly RoundedButton saveButton = new RoundedButton();
        private readonly IconToggleButton toggleKeyButton = new IconToggleButton();
        private readonly SegmentedThemeControl themeControl = new SegmentedThemeControl();

        private bool keyVisible;
        private bool dragging;
        private Point dragStart;

        // 主题配色（构造时设定）
        private static Color C_Bg, C_Surface, C_Text, C_Sub, C_Border, C_Accent, C_AccentHover;
        private static bool darkMode;

        public AppSettings Settings { get; private set; }
        public event Action<AppSettings> PreviewChanged;

        public SettingsForm(AppSettings settings)
        {
            Settings = new AppSettings
            {
                ApiKey = settings.ApiKey,
                RefreshMinutes = settings.RefreshMinutes,
                AutoHide = settings.AutoHide,
                AlwaysOnTop = settings.AlwaysOnTop,
                AutoStart = settings.AutoStart,
                ThemeMode = AppSettings.NormalizeThemeMode(settings.ThemeMode),
                WindowX = settings.WindowX,
                WindowY = settings.WindowY,
                WindowWidth = settings.WindowWidth,
                WindowHeight = settings.WindowHeight,
                DockSide = settings.DockSide,
                FirstRunVersion = settings.FirstRunVersion,
                AutoHideHintShown = settings.AutoHideHintShown,
            };

            ApplyThemeColors(Settings.ThemeMode);

            Text = "";
            Font = new Font("Microsoft YaHei UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = DpiHelper.ScaleSize(new Size(464, 490));
            ShowInTaskbar = true;
            Icon = LoadAppIcon();
            BackColor = C_Bg;
            DoubleBuffered = true;

            BuildLayout();

            OnApiKeyChanged(null, EventArgs.Empty);
        }

        private static void ApplyThemeColors(string themeMode)
        {
            darkMode = themeMode == "Dark" || (themeMode == "System" && IsWindowsDarkMode());

            if (darkMode)
            {
                C_Bg = Color.FromArgb(32, 32, 32);
                C_Surface = Color.FromArgb(44, 44, 46);
                C_Text = Color.FromArgb(245, 245, 245);
                C_Sub = Color.FromArgb(158, 158, 162);
                C_Border = Color.FromArgb(62, 62, 62);
                C_Accent = Color.FromArgb(96, 205, 255);
                C_AccentHover = Color.FromArgb(120, 215, 255);
            }
            else
            {
                C_Bg = Color.FromArgb(243, 242, 248);
                C_Surface = Color.FromArgb(255, 255, 255);
                C_Text = Color.FromArgb(28, 28, 30);
                C_Sub = Color.FromArgb(108, 108, 112);
                C_Border = Color.FromArgb(218, 218, 222);
                C_Accent = Color.FromArgb(0, 103, 192);
                C_AccentHover = Color.FromArgb(0, 85, 162);
            }
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
            catch { return false; }
        }

        private static Icon LoadAppIcon()
        {
            string icoPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(typeof(Program).Assembly.Location),
                "AppIcon.ico");
            if (System.IO.File.Exists(icoPath))
                return new Icon(icoPath);
            return null;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TryRoundedCorners();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), DpiHelper.Px(12)))
            using (var pen = new Pen(C_Border))
                e.Graphics.DrawPath(pen, path);
        }

        // ---- DWM 圆角 ----

        private void TryRoundedCorners()
        {
            try
            {
                int rounded = 2; // DWMWCP_ROUND
                NativeMethods.DwmSetWindowAttribute(Handle, 33, ref rounded, sizeof(int));
            }
            catch { }
        }

        // ---- Layout ----

        private void BuildLayout()
        {
            // 标题栏
            var closeBtn = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 14F),
                ForeColor = C_Sub,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = Pt(432, 8),
                Size = Sz(24, 24),
                Cursor = Cursors.Hand,
                Tag = "sub"
            };
            closeBtn.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            closeBtn.MouseEnter += (s, e) => closeBtn.ForeColor = C_Text;
            closeBtn.MouseLeave += (s, e) => closeBtn.ForeColor = C_Sub;
            Controls.Add(closeBtn);

            WireDrag(closeBtn);
            WireDrag(this);

            // 标题
            var title = new TextLabel
            {
                Text = "DeepSeek 余额设置",
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                ForeColor = C_Text,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = Pt(28, 32),
                Size = Sz(408, 30),
                Tag = "text"
            };
            Controls.Add(title);

            var subtitle = new TextLabel
            {
                Text = "配置 API Key 与浮窗行为",
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = C_Sub,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = Pt(28, 64),
                Size = Sz(408, 20),
                Tag = "sub"
            };
            Controls.Add(subtitle);

            // 内容卡片
            int cardY = DpiHelper.Px(98);
            int cardH = DpiHelper.Px(32);
            int gap = DpiHelper.Px(12);
            int labelW = DpiHelper.Px(72);

            // API Key
            AddCardRow("API Key", cardY, labelW, () =>
            {
                var panel = new RoundedPanel { Location = Point.Empty, Size = new Size(DpiHelper.Px(306), cardH) };
                apiKeyBox.BorderStyle = BorderStyle.None;
                apiKeyBox.UseSystemPasswordChar = true;
                apiKeyBox.Text = Settings.ApiKey != null ? Settings.ApiKey : "";
                apiKeyBox.Font = new Font("Segoe UI", 10F);
                apiKeyBox.Location = new Point(DpiHelper.Px(12), CenterY(cardH, apiKeyBox.Height));
                apiKeyBox.Width = DpiHelper.Px(262);
                apiKeyBox.BackColor = C_Surface;
                apiKeyBox.ForeColor = C_Text;
                apiKeyBox.TextChanged += OnApiKeyChanged;
                toggleKeyButton.Size = Sz(18, 18);
                toggleKeyButton.Location = new Point(DpiHelper.Px(280), CenterY(cardH, toggleKeyButton.Height));
                toggleKeyButton.Click += delegate
                {
                    keyVisible = !keyVisible;
                    apiKeyBox.UseSystemPasswordChar = !keyVisible;
                    toggleKeyButton.IsOn = keyVisible;
                    toggleKeyButton.Invalidate();
                };
                panel.Controls.Add(apiKeyBox);
                panel.Controls.Add(toggleKeyButton);
                return panel;
            });

            cardY += cardH + gap;

            // 刷新间隔
            AddCardRow("刷新间隔", cardY, labelW, () =>
            {
                var panel = new RoundedPanel { Location = Point.Empty, Size = new Size(DpiHelper.Px(80), cardH) };
                refreshBox.BorderStyle = BorderStyle.None;
                refreshBox.Text = AppSettings.NormalizeRefreshMinutes(Settings.RefreshMinutes).ToString();
                refreshBox.Font = new Font("Segoe UI", 10F);
                refreshBox.Location = new Point(DpiHelper.Px(12), CenterY(cardH, refreshBox.Height));
                refreshBox.Width = DpiHelper.Px(40);
                refreshBox.BackColor = C_Surface;
                refreshBox.ForeColor = C_Text;
                panel.Controls.Add(refreshBox);
                return panel;
            });
            Controls.Add(new TextLabel
            {
                Text = "分钟",
                ForeColor = C_Sub,
                Location = new Point(DpiHelper.Px(200), cardY),
                Size = new Size(DpiHelper.Px(40), cardH),
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = "sub"
            });

            cardY += cardH + gap;

            // 贴边隐藏
            AddCardRow("贴边隐藏", cardY, labelW, () =>
            {
                autoHideSwitch.Checked = Settings.AutoHide;
                return autoHideSwitch;
            });
            Controls.Add(new TextLabel
            {
                Text = "靠近屏幕边缘时自动隐藏",
                ForeColor = C_Sub,
                Location = new Point(DpiHelper.Px(170), cardY),
                Size = new Size(DpiHelper.Px(230), cardH),
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = "sub"
            });

            cardY += cardH + gap;

            // 窗口置顶
            AddCardRow("窗口置顶", cardY, labelW, () =>
            {
                topMostSwitch.Checked = Settings.AlwaysOnTop;
                return topMostSwitch;
            });
            Controls.Add(new TextLabel
            {
                Text = "浮窗始终显示在其他窗口上方",
                ForeColor = C_Sub,
                Location = new Point(DpiHelper.Px(170), cardY),
                Size = new Size(DpiHelper.Px(230), cardH),
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = "sub"
            });

            cardY += cardH + gap;

            // 开机自启
            AddCardRow("开机自启", cardY, labelW, () =>
            {
                autoStartSwitch.Checked = Settings.AutoStart;
                return autoStartSwitch;
            });
            Controls.Add(new TextLabel
            {
                Text = "登录 Windows 时自动启动",
                ForeColor = C_Sub,
                Location = new Point(DpiHelper.Px(170), cardY),
                Size = new Size(DpiHelper.Px(230), cardH),
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = "sub"
            });

            cardY += cardH + gap;

            // 主题
            AddCardRow("主题", cardY, labelW, () =>
            {
                themeControl.Value = Settings.ThemeMode;
                themeControl.Size = Sz(216, 30);
                themeControl.ValueChanged += OnThemeChanged;
                return themeControl;
            });

            // 底部
            cardY += cardH + DpiHelper.Px(20);

            validationHint.Location = new Point(DpiHelper.Px(124), cardY);
            validationHint.Size = Sz(280, 20);
            Controls.Add(validationHint);

            cardY += DpiHelper.Px(36);

            saveButton.Text = "保存";
            saveButton.Location = new Point(DpiHelper.Px(260), cardY);
            saveButton.Size = Sz(80, 34);
            saveButton.Primary = true;
            saveButton.Click += OnSave;

            var cancelBtn = new RoundedButton
            {
                Text = "取消",
                Location = new Point(DpiHelper.Px(352), cardY),
                Size = Sz(80, 34),
                Primary = false,
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(saveButton);
            Controls.Add(cancelBtn);
            AcceptButton = saveButton;
            CancelButton = cancelBtn;
        }

        private void AddCardRow(string label, int y, int labelW, Func<Control> factory)
        {
            Controls.Add(new TextLabel
            {
                Text = label,
                ForeColor = C_Text,
                Location = new Point(DpiHelper.Px(28), y),
                Size = new Size(labelW, DpiHelper.Px(32)),
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = "text"
            });
            int x = DpiHelper.Px(28) + labelW + DpiHelper.Px(16);
            var ctrl = factory();
            ctrl.Location = new Point(x, y + CenterY(DpiHelper.Px(32), ctrl.Height));
            Controls.Add(ctrl);
        }

        private void WireDrag(Control c)
        {
            c.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                dragging = true;
                dragStart = e.Location;
            };
            c.MouseMove += (s, e) =>
            {
                if (!dragging) return;
                Location = new Point(
                    MousePosition.X - dragStart.X,
                    MousePosition.Y - dragStart.Y);
            };
            c.MouseUp += (s, e) => dragging = false;
        }

        // ---- Events ----

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyThemeColors(themeControl.Value);
            BackColor = C_Bg;
            ApplyThemeToControls(this);
            OnApiKeyChanged(null, EventArgs.Empty);
            Invalidate(true);
            if (PreviewChanged != null) PreviewChanged(BuildCurrentSettings());
        }

        private AppSettings BuildCurrentSettings()
        {
            int m;
            if (!int.TryParse(refreshBox.Text, out m)) m = Settings.RefreshMinutes;
            return new AppSettings
            {
                ApiKey = AppSettings.NormalizeApiKey(apiKeyBox.Text),
                RefreshMinutes = AppSettings.NormalizeRefreshMinutes(m),
                AutoHide = autoHideSwitch.Checked,
                AlwaysOnTop = topMostSwitch.Checked,
                AutoStart = autoStartSwitch.Checked,
                ThemeMode = themeControl.Value,
                WindowX = Settings.WindowX,
                WindowY = Settings.WindowY,
                WindowWidth = Settings.WindowWidth,
                WindowHeight = Settings.WindowHeight,
                DockSide = Settings.DockSide,
                FirstRunVersion = Settings.FirstRunVersion,
                AutoHideHintShown = Settings.AutoHideHintShown,
            };
        }

        private void ApplyThemeToControls(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                var tag = c.Tag as string;
                if (tag == "text") c.ForeColor = C_Text;
                else if (tag == "sub") c.ForeColor = C_Sub;

                if (c is TextBox)
                {
                    c.BackColor = C_Surface;
                    c.ForeColor = C_Text;
                }
                else if (c is RoundedPanel)
                {
                    c.BackColor = C_Bg;
                }

                c.Invalidate();
                ApplyThemeToControls(c);
            }
        }

        private void OnApiKeyChanged(object sender, EventArgs e)
        {
            string n = AppSettings.NormalizeApiKey(apiKeyBox.Text);
            if (string.IsNullOrWhiteSpace(n))
            {
                validationHint.Text = "请粘贴 DeepSeek API Key";
                validationHint.ForeColor = Color.FromArgb(180, 140, 0);
            }
            else if (!n.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
            {
                validationHint.Text = "Key 格式异常，应以 sk- 开头";
                validationHint.ForeColor = Color.FromArgb(210, 20, 30);
            }
            else if (n.Length < 20)
            {
                validationHint.Text = "Key 长度偏短，请检查是否完整复制";
                validationHint.ForeColor = Color.FromArgb(180, 140, 0);
            }
            else
            {
                validationHint.Text = "Key 格式有效";
                validationHint.ForeColor = Color.FromArgb(15, 123, 15);
            }
        }

        private void OnSave(object sender, EventArgs e)
        {
            string n = AppSettings.NormalizeApiKey(apiKeyBox.Text);
            if (!string.IsNullOrWhiteSpace(apiKeyBox.Text) && !AppSettings.IsValidApiKey(apiKeyBox.Text))
            {
                var r = MessageBox.Show(
                    "当前 API Key 不是以 sk- 开头，可能不是正确的 DeepSeek Key。\n\n是否仍然保存？",
                    "Key 格式警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }

            Settings = BuildCurrentSettings();
            Settings.ApiKey = n;

            saveButton.Text = "已保存";
            saveButton.Enabled = false;
            DialogResult = DialogResult.OK;
            Close();
        }

        // ---- Utility ----

        private static Point Pt(int x, int y) { return new Point(DpiHelper.Px(x), DpiHelper.Px(y)); }
        private static Size Sz(int w, int h) { return new Size(DpiHelper.Px(w), DpiHelper.Px(h)); }
        private static int CenterY(int rowHeight, int controlHeight) { return SettingsLayout.CenterOffset(rowHeight, controlHeight); }

        private static GraphicsPath RoundRect(Rectangle b, int r)
        {
            int d = r * 2;
            var p = new GraphicsPath();
            p.AddArc(b.Left, b.Top, d, d, 180, 90);
            p.AddArc(b.Right - d, b.Top, d, d, 270, 90);
            p.AddArc(b.Right - d, b.Bottom - d, d, d, 0, 90);
            p.AddArc(b.Left, b.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ============================================================
        //  Controls
        // ============================================================

        private sealed class TextLabel : Label
        {
            public TextLabel()
            {
                AutoSize = false;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;
                if (TextAlign == ContentAlignment.MiddleCenter)
                    flags |= TextFormatFlags.HorizontalCenter;
                else
                    flags |= TextFormatFlags.Left;
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, flags);
            }
        }
        private sealed class RoundedPanel : Panel
        {
            public RoundedPanel()
            {
                BackColor = C_Bg;
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            }
            protected override void OnPaintBackground(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent != null ? Parent.BackColor : C_Bg);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), DpiHelper.Px(8)))
                using (var brush = new SolidBrush(C_Surface))
                using (var pen = new Pen(C_Border))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        private sealed class ToggleSwitch : CheckBox
        {
            public ToggleSwitch()
            {
                Size = DpiHelper.ScaleSize(new Size(44, 24));
                Cursor = Cursors.Hand;
                Appearance = Appearance.Button;
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                Text = "";
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(Parent != null ? Parent.BackColor : C_Bg);
                int r = Height / 2;
                using (var path = RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), r))
                using (var brush = new SolidBrush(Checked ? C_Accent : darkMode ? Color.FromArgb(72, 72, 76) : Color.FromArgb(205, 205, 205)))
                    e.Graphics.FillPath(brush, path);
                int pad = Math.Max(2, Height / 7);
                int ksz = Height - pad * 2;
                int knobX = Checked ? Width - ksz - pad : pad;
                using (var knob = new SolidBrush(Color.White))
                    e.Graphics.FillEllipse(knob, knobX, pad, ksz, ksz);
            }
        }

        private sealed class IconToggleButton : Control
        {
            public bool IsOn { get; set; }
            public IconToggleButton()
            {
                Cursor = Cursors.Hand;
                DoubleBuffered = true;
                BackColor = C_Surface;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(C_Surface);
                var c = IsOn ? C_Accent : C_Sub;
                float pw = DpiHelper.Px(2);
                int x2 = DpiHelper.Px(2), y5 = DpiHelper.Px(5);
                int w14 = DpiHelper.Px(14), h8 = DpiHelper.Px(8);
                int x8 = DpiHelper.Px(8), y8 = DpiHelper.Px(8);
                int sz3 = DpiHelper.Px(3);
                int x4 = DpiHelper.Px(4), y15 = DpiHelper.Px(15);
                int x17 = DpiHelper.Px(16), y3 = DpiHelper.Px(3);
                using (var pen = new Pen(c, pw))
                {
                    e.Graphics.DrawEllipse(pen, x2, y5, w14, h8);
                    e.Graphics.FillEllipse(new SolidBrush(c), x8, y8, sz3, sz3);
                    if (IsOn) e.Graphics.DrawLine(pen, x4, y15, x17, y3);
                }
            }
        }

        private sealed class SegmentedThemeControl : Control
        {
            public event EventHandler ValueChanged;
            public string Value { get; set; }
            public SegmentedThemeControl()
            {
                Value = "System";
                Cursor = Cursors.Hand;
                DoubleBuffered = true;
            }
            protected override void OnMouseDown(MouseEventArgs e)
            {
                int seg1 = Width * 64 / 216;
                int seg2 = Width * 128 / 216;
                string old = Value;
                if (e.X < seg1) Value = "Light";
                else if (e.X < seg2) Value = "Dark";
                else Value = "System";
                Invalidate();
                if (old != Value && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int rad = DpiHelper.Px(10);
                using (var bg = RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), rad))
                using (var brush = new SolidBrush(C_Surface))
                using (var pen = new Pen(C_Border))
                {
                    e.Graphics.FillPath(brush, bg);
                    e.Graphics.DrawPath(pen, bg);
                }
                int p2 = DpiHelper.Px(2), p3 = DpiHelper.Px(3);
                int seg1w = Width * 64 / 216;
                int seg2w = Width * 64 / 216;
                int seg3w = Width - seg1w - seg2w;
                int h = Height - p3 - p3;
                var rc1 = new Rectangle(p2, p3, seg1w - p2, h);
                var rc2 = new Rectangle(seg1w, p3, seg2w, h);
                var rc3 = new Rectangle(seg1w + seg2w, p3, seg3w - p2, h);
                var sel = Value == "Light" ? rc1 : Value == "Dark" ? rc2 : rc3;
                using (var path = RoundRect(sel, DpiHelper.Px(8)))
                using (var brush = new SolidBrush(C_Accent))
                    e.Graphics.FillPath(brush, path);
                DrawText(e.Graphics, "明亮", rc1, Value == "Light");
                DrawText(e.Graphics, "黑暗", rc2, Value == "Dark");
                DrawText(e.Graphics, "跟随系统", rc3, Value == "System");
            }
            private static void DrawText(Graphics g, string text, Rectangle rect, bool selected)
            {
                TextRenderer.DrawText(g, text, new Font("Microsoft YaHei UI", 9F), rect,
                    selected ? Color.White : C_Sub,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private sealed class RoundedButton : Control, IButtonControl
        {
            public bool Primary { get; set; }
            private bool hovered;
            private bool isDefault;

            public DialogResult DialogResult { get; set; }

            public void NotifyDefault(bool value)
            {
                isDefault = value;
                Invalidate();
            }

            public void PerformClick()
            {
                OnClick(EventArgs.Empty);
                if (DialogResult != DialogResult.None)
                {
                    var f = FindForm();
                    if (f != null)
                    {
                        f.DialogResult = DialogResult;
                        f.Close();
                    }
                }
            }
            private bool pressed;

            public RoundedButton()
            {
                Font = new Font("Microsoft YaHei UI", 10F);
                Cursor = Cursors.Hand;
                TabStop = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                hovered = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                hovered = false;
                pressed = false;
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); }
                base.OnMouseDown(e);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                if (pressed)
                {
                    pressed = false;
                    Invalidate();
                    if (ClientRectangle.Contains(e.Location))
                        PerformClick();
                }
                base.OnMouseUp(e);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    pressed = true;
                    Invalidate();
                }
                base.OnKeyDown(e);
            }

            protected override void OnKeyUp(KeyEventArgs e)
            {
                if (pressed && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
                {
                    pressed = false;
                    Invalidate();
                    PerformClick();
                }
                base.OnKeyUp(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                Color bg, fg, border;
                if (Primary)
                {
                    bg = pressed ? Color.FromArgb(0, 68, 135) : hovered ? C_AccentHover : C_Accent;
                    fg = Color.White;
                    border = bg;
                }
                else
                {
                    bg = pressed ? (darkMode ? Color.FromArgb(55, 55, 58) : Color.FromArgb(225, 225, 230)) : hovered ? (darkMode ? Color.FromArgb(62, 62, 66) : Color.FromArgb(240, 240, 242)) : C_Surface;
                    fg = C_Text;
                    border = C_Border;
                }
                using (var path = RoundRect(r, DpiHelper.Px(8)))
                using (var brush = new SolidBrush(bg))
                using (var pen = new Pen(border))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
                TextRenderer.DrawText(e.Graphics, Text, Font, r,
                    fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}












