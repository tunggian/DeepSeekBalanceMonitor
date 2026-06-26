using System;
using System.Drawing;
using System.Drawing.Text;

namespace DeepSeekBalanceMonitor
{
    /// <summary>
    /// 在 32x32 圆形蓝色背景上绘制简短文字作为托盘图标
    /// </summary>
    internal static class IconPainter
    {
        public static Icon Draw(string text)
        {
            string label = SanitizeLabel(text);

            using (var bitmap = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bitmap))
                using (var bg = new SolidBrush(Color.FromArgb(0, 120, 212)))
                using (var fg = new SolidBrush(Color.White))
                using (var font = BuildFont(label))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                    g.FillEllipse(bg, 1, 1, 30, 30);

                    SizeF size = g.MeasureString(label, font);
                    float x = (32 - size.Width) / 2;
                    float y = (32 - size.Height) / 2 - 1;
                    g.DrawString(label, font, fg, x, y);
                }

                IntPtr hIcon = bitmap.GetHicon();
                try
                {
                    // FromHandle 返回的 Icon 需要 Clone 以独立管理生命周期
                    return (Icon)Icon.FromHandle(hIcon).Clone();
                }
                finally
                {
                    NativeMethods.DestroyIcon(hIcon);
                }
            }
        }

        private static string SanitizeLabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return "DS";
            // 最长 5 个字符，否则溢出 32x32 圆形容器
            return text.Length > 5 ? text.Substring(0, 5) : text;
        }

        private static Font BuildFont(string label)
        {
            float size = label.Length > 4 ? 6.4f : 8.5f;
            return new Font("Segoe UI Semibold", size, FontStyle.Bold);
        }
    }
}
