using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DeepSeekBalanceMonitor
{
    internal static class DpiHelper
    {
        public static readonly float Scale;
        static DpiHelper()
        {
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    Scale = g.DpiX / 96f;
            }
            catch { Scale = 1f; }
        }

        public static int Px(int px) { return (int)(px * Scale + 0.5f); }
        public static int ScaleValue(int px, float scale) { return (int)(px * scale + 0.5f); }
        public static Size ScaleSize(Size size) { return new Size(Px(size.Width), Px(size.Height)); }
        public static Rectangle ScaleRect(Rectangle rect, float scale)
        {
            return new Rectangle(
                ScaleValue(rect.X, scale),
                ScaleValue(rect.Y, scale),
                ScaleValue(rect.Width, scale),
                ScaleValue(rect.Height, scale));
        }
        public static Rectangle ScaleRect(Rectangle rect) { return ScaleRect(rect, Scale); }
    }

    internal static class SettingsLayout
    {
        public static int CenterOffset(int rowHeight, int controlHeight)
        {
            int offset = (rowHeight - controlHeight) / 2;
            return offset < 0 ? 0 : offset;
        }
    }
    internal static class FloatingBalanceLayout
    {
        public static Rectangle ValueBounds(Size clientSize)
        {
            return new Rectangle(DpiHelper.Px(11), 0, DpiHelper.Px(66), clientSize.Height);
        }

        public static Rectangle StatusDotBounds(Size clientSize)
        {
            int dot = DpiHelper.Px(7);
            return new Rectangle(DpiHelper.Px(88), (clientSize.Height - dot) / 2, dot, dot);
        }

        public static Rectangle StatusTextBounds(Size clientSize)
        {
            int h = DpiHelper.Px(20);
            return new Rectangle(DpiHelper.Px(100), (clientSize.Height - h) / 2, DpiHelper.Px(36), h);
        }

        public static Rectangle RefreshIconBounds(Size clientSize)
        {
            int size = DpiHelper.Px(16);
            return new Rectangle(DpiHelper.Px(142), (clientSize.Height - size) / 2, size, size);
        }
    }
    // ============================================================
    //  Models
    // ============================================================

    internal sealed class BalanceSnapshot
    {
        public bool IsAvailable { get; private set; }
        public string Currency { get; private set; }
        public string TotalBalance { get; private set; }

        public string ShortText
        {
            get { return string.IsNullOrEmpty(Currency) ? TotalBalance : Currency + " " + TotalBalance; }
        }

        public string TaskbarText
        {
            get { return Currency == "CNY" ? "¥" + TotalBalance : ShortText; }
        }

        public static BalanceSnapshot FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("DeepSeek 返回了空响应。");

            object parsed;
            try
            {
                parsed = new JavaScriptSerializer().DeserializeObject(json);
            }
            catch (Exception ex)
            {
                string preview = json.Length > 100 ? json.Substring(0, 100) + "..." : json;
                throw new InvalidOperationException(
                    "DeepSeek 返回的数据不是有效 JSON：\n" + preview + "\n\n解析错误：" + ex.Message);
            }

            var root = parsed as Dictionary<string, object>;
            if (root == null)
            {
                string preview = json.Length > 150 ? json.Substring(0, 150) + "..." : json;
                throw new InvalidOperationException("DeepSeek 返回的余额数据不是 JSON 对象：\n" + preview);
            }

            object av;
            bool available = root.TryGetValue("is_available", out av)
                && Convert.ToBoolean(av, CultureInfo.InvariantCulture);

            object infosVal;
            var infos = root.TryGetValue("balance_infos", out infosVal)
                ? infosVal as object[]
                : null;

            if (infos == null || infos.Length == 0)
                throw new InvalidOperationException(
                    "DeepSeek 返回中没有 balance_infos。请确认 API Key 属于 DeepSeek 平台。\n" +
                    "常见原因：粘贴了 OpenAI / 硅基流动等其它平台的 Key。");

            var info = infos
                .OfType<Dictionary<string, object>>()
                .OrderBy(x => string.Equals(StringValue(x, "currency"), "CNY", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .FirstOrDefault();

            if (info == null)
                throw new InvalidOperationException("DeepSeek 返回的 balance_infos 格式异常。");

            string currency = StringValue(info, "currency");
            string total = StringValue(info, "total_balance");

            if (string.IsNullOrWhiteSpace(total))
                throw new InvalidOperationException("DeepSeek 返回中没有 total_balance 字段。响应字段：" +
                    string.Join(", ", info.Keys));

            return new BalanceSnapshot
            {
                IsAvailable = available,
                Currency = currency,
                TotalBalance = total
            };
        }

        private static string StringValue(Dictionary<string, object> values, string key)
        {
            object value;
            return values.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : "";
        }
    }

    // ============================================================
    //  Edge Dock Enums & Layout Helpers
    // ============================================================

    internal enum EdgeSide
    {
        Left,
        Right,
        Top,
        Bottom
    }

    internal static class TaskbarPlacement
    {
        public static Rectangle Place(Rectangle screenBounds, Rectangle workingArea, Size size)
        {
            int reserveRight = 140;
            if (workingArea.Bottom < screenBounds.Bottom)
            {
                int taskbarHeight = screenBounds.Bottom - workingArea.Bottom;
                int x = Math.Max(screenBounds.Left, workingArea.Right - size.Width - reserveRight);
                int y = workingArea.Bottom + Math.Max(0, (taskbarHeight - size.Height) / 2);
                return new Rectangle(x, y, size.Width, size.Height);
            }
            return new Rectangle(workingArea.Right - size.Width - 14,
                workingArea.Bottom - size.Height - 14, size.Width, size.Height);
        }
    }

    internal static class EdgeDockLayout
    {
        public static Rectangle ExpandedBounds(Rectangle workArea, Size size, EdgeSide side)
        {
            switch (side)
            {
                case EdgeSide.Left:
                    return new Rectangle(workArea.Left, workArea.Top + DpiHelper.Px(80), size.Width, size.Height);
                case EdgeSide.Right:
                    return new Rectangle(workArea.Right - size.Width, workArea.Top + DpiHelper.Px(80), size.Width, size.Height);
                case EdgeSide.Top:
                    return new Rectangle(workArea.Left + DpiHelper.Px(80), workArea.Top, size.Width, size.Height);
                default:
                    return new Rectangle(workArea.Left + DpiHelper.Px(80), workArea.Bottom - size.Height, size.Width, size.Height);
            }
        }

        public static Rectangle HiddenBounds(Rectangle expandedBounds, EdgeSide side, int visibleGrip)
        {
            int grip = Math.Max(4, visibleGrip);
            switch (side)
            {
                case EdgeSide.Left:
                    return new Rectangle(expandedBounds.Left - expandedBounds.Width + grip,
                        expandedBounds.Top, expandedBounds.Width, expandedBounds.Height);
                case EdgeSide.Right:
                    return new Rectangle(expandedBounds.Right - grip * 2,
                        expandedBounds.Top, grip * 2, expandedBounds.Height);
                case EdgeSide.Top:
                    return new Rectangle(expandedBounds.Left,
                        expandedBounds.Top - expandedBounds.Height + grip,
                        expandedBounds.Width, expandedBounds.Height);
                default:
                    return new Rectangle(expandedBounds.Left,
                        expandedBounds.Bottom - grip * 2,
                        expandedBounds.Width, grip * 2);
            }
        }
    }

    // ============================================================
    //  Settings – 保存到 %AppData%/DeepSeekBalanceMonitor/settings.ini
    //  API Key 通过 DPAPI (DataProtectionScope.CurrentUser) 加密
    //  新增：窗口位置、首次运行标记、首次隐藏提示标记
    // ============================================================

    internal sealed class AppSettings
    {
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DeepSeekBalanceMonitor");

        private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.ini");

        public AppSettings()
        {
            RefreshMinutes = 1;
            WindowWidth = 0;
            WindowHeight = 0;
            DockSide = "Right";
            AutoHide = true;
            AlwaysOnTop = true;
            ThemeMode = "System";
        }

        // 基础设置
        public string ApiKey { get; set; }
        public int RefreshMinutes { get; set; }

        // 窗口状态持久化
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public string DockSide { get; set; }

        // 首次运行/使用标记
        /// <summary>0 = 从未运行过，>0 = 已运行</summary>
        public int FirstRunVersion { get; set; }
        public bool AutoHideHintShown { get; set; }

        // 行为设置
        public bool AutoHide { get; set; }
        public bool AlwaysOnTop { get; set; }
        public string ThemeMode { get; set; }

        // --------------------------------------------------------
        //  校验与清洗
        // --------------------------------------------------------

        public static int NormalizeRefreshMinutes(int minutes)
        {
            if (minutes < 1) return 1;
            if (minutes > 1440) return 1440;
            return minutes;
        }

        public static string NormalizeThemeMode(string mode)
        {
            if (string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase)) return "Light";
            if (string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase)) return "Dark";
            return "System";
        }

        public static string NormalizeApiKey(string apiKey)
        {
            if (apiKey == null) return "";
            string key = apiKey.Trim().Trim('\'', '"');
            if (key.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                key = key.Substring(7).Trim();
            return key.Trim().Trim('\'', '"');
        }

        /// <summary>快速校验 API Key 格式是否合法（sk- 开头且非空）</summary>
        public static bool IsValidApiKey(string apiKey)
        {
            string key = NormalizeApiKey(apiKey);
            return !string.IsNullOrWhiteSpace(key) && key.StartsWith("sk-", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>给用户友好的 API Key 去敏显示</summary>
        public static string MaskApiKey(string apiKey)
        {
            string key = NormalizeApiKey(apiKey);
            if (string.IsNullOrEmpty(key)) return "";
            if (key.Length <= 8) return key.Substring(0, Math.Min(4, key.Length)) + "****";
            return key.Substring(0, 4) + "****" + key.Substring(key.Length - 4);
        }

        // --------------------------------------------------------
        //  加载与保存
        // --------------------------------------------------------

        public static AppSettings Load()
        {
            var settings = new AppSettings();
            if (!File.Exists(FilePath)) return settings;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(FilePath);
            }
            catch (IOException)
            {
                return settings;
            }

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                int split = line.IndexOf('=');
                if (split <= 0) continue;

                string key = line.Substring(0, split).Trim();
                string value = line.Substring(split + 1).Trim();

                switch (key)
                {
                    case "refresh_minutes":
                        {
                            int m;
                            if (int.TryParse(value, out m))
                                settings.RefreshMinutes = NormalizeRefreshMinutes(m);
                        }
                        break;

                    case "api_key":
                        settings.ApiKey = NormalizeApiKey(Unprotect(value));
                        break;

                    case "window_x":
                        { int v; if (int.TryParse(value, out v)) settings.WindowX = v; }
                        break;
                    case "window_y":
                        { int v; if (int.TryParse(value, out v)) settings.WindowY = v; }
                        break;
                    case "window_width":
                        { int v; if (int.TryParse(value, out v)) settings.WindowWidth = v; }
                        break;
                    case "window_height":
                        { int v; if (int.TryParse(value, out v)) settings.WindowHeight = v; }
                        break;
                    case "dock_side":
                        settings.DockSide = value;
                        break;

                    case "first_run_version":
                        { int v; if (int.TryParse(value, out v)) settings.FirstRunVersion = v; }
                        break;
                    case "auto_hide_hint_shown":
                        { bool v; if (bool.TryParse(value, out v)) settings.AutoHideHintShown = v; }
                        break;
                    case "auto_hide":
                        { bool v; if (bool.TryParse(value, out v)) settings.AutoHide = v; }
                        break;
                    case "always_on_top":
                        { bool v; if (bool.TryParse(value, out v)) settings.AlwaysOnTop = v; }
                        break;
                    case "theme_mode":
                        settings.ThemeMode = NormalizeThemeMode(value);
                        break;
                }
            }

            return settings;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllLines(FilePath, new[]
                {
                    "refresh_minutes=" + NormalizeRefreshMinutes(RefreshMinutes),
                    "api_key=" + Protect(NormalizeApiKey(ApiKey)),
                    "window_x=" + WindowX,
                    "window_y=" + WindowY,
                    "window_width=" + WindowWidth,
                    "window_height=" + WindowHeight,
                    "dock_side=" + DockSide,
                    "first_run_version=" + FirstRunVersion,
                    "auto_hide_hint_shown=" + AutoHideHintShown,
                    "auto_hide=" + AutoHide,
                    "always_on_top=" + AlwaysOnTop,
                    "theme_mode=" + NormalizeThemeMode(ThemeMode),
                });
            }
            catch (Exception ex)
            {
                // 保存失败不应影响主流程，安静跳过
                System.Diagnostics.Debug.WriteLine("Failed to save settings: " + ex.Message);
            }
        }

        // --------------------------------------------------------
        //  DPAPI 加密 / 解密
        // --------------------------------------------------------

        private static string Protect(string value)
        {
            byte[] clear = Encoding.UTF8.GetBytes(value ?? "");
            byte[] encrypted = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        private static string Unprotect(string value)
        {
            try
            {
                byte[] encrypted = Convert.FromBase64String(value);
                return Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser));
            }
            catch
            {
                return "";
            }
        }
    }

    // ============================================================
    //  DeepSeek API 客户端
    //  - 使用 HttpClient（线程安全、连接复用）
    //  - 网络错误自动重试一次（2 秒后）
    //  - 超时 30 秒
    //  - 支持 CancellationToken
    // ============================================================

    internal sealed class DeepSeekBalanceClient
    {
        private const string BalanceUrl = "https://api.deepseek.com/user/balance";

        // 单例 HttpClient —— 连接复用、套接字不耗尽
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        public async Task<BalanceSnapshot> GetBalanceAsync(string apiKey, CancellationToken ct = default(CancellationToken))
        {
            string key = AppSettings.NormalizeApiKey(apiKey);
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("请先设置 DeepSeek API Key。\n右键托盘图标 →「设置」可配置。");

            bool shouldRetry = false;

            // 第一次请求
            try
            {
                return await FetchBalanceAsync(key, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                shouldRetry = true; // 超时 —— 准备重试
            }
            catch (HttpRequestException ex)
            {
                if (IsTransient(ex))
                    shouldRetry = true; // 网络抖动 —— 准备重试
                else
                    throw;
            }

            if (!shouldRetry)
                throw new InvalidOperationException("请求失败。");

            // —— 延迟后重试一次（await 不在 catch 块内，兼容 C# 5）——
            await Task.Delay(2000, ct).ConfigureAwait(false);
            return await FetchBalanceAsync(key, ct).ConfigureAwait(false);
        }

        private static async Task<BalanceSnapshot> FetchBalanceAsync(string normalizedKey, CancellationToken ct)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, BalanceUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", normalizedKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using (var response = await Http.SendAsync(request,
                    HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false))
                {
                    // 401 → 清晰的错误提示
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        string body = await TryReadBodyAsync(response).ConfigureAwait(false);
                        throw new InvalidOperationException(
                            "401 未授权：请确认粘贴的是 DeepSeek API Key。\n" +
                            "不要使用 OpenAI / 硅基流动等其它平台的 Key。\n" +
                            "API 响应：" + (body ?? "（无响应体）"));
                    }

                    // 429 限流
                    if (response.StatusCode == (HttpStatusCode)429)
                    {
                        throw new InvalidOperationException(
                            "429 请求过于频繁，请稍后再试，或调大刷新间隔。");
                    }

                    // 其它 HTTP 错误
                    if (!response.IsSuccessStatusCode)
                    {
                        string body = await TryReadBodyAsync(response).ConfigureAwait(false);
                        throw new InvalidOperationException(
                            "HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase +
                            (body != null ? "\n" + body : ""));
                    }

                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return BalanceSnapshot.FromJson(json);
                }
            }
        }

        private static async Task<string> TryReadBodyAsync(HttpResponseMessage response)
        {
            try
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(body) ? null : body;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsTransient(HttpRequestException ex)
        {
            var inner = ex.InnerException as WebException;
            if (inner == null) return false;
            switch (inner.Status)
            {
                case WebExceptionStatus.ConnectFailure:
                case WebExceptionStatus.NameResolutionFailure:
                case WebExceptionStatus.Timeout:
                case WebExceptionStatus.ReceiveFailure:
                case WebExceptionStatus.SendFailure:
                case WebExceptionStatus.ConnectionClosed:
                    return true;
                default:
                    return false;
            }
        }
    }
}













