using System;
using System.Net;
using System.Windows.Forms;

namespace DeepSeekBalanceMonitor
{
    /// <summary>
    /// 应用程序入口。
    /// 全局 HTTP 配置（TLS 1.2、连接数）在此一次性完成。
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try { NativeMethods.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ---- 全局 HTTP 配置 ----
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 10;

            // ---- 启动主上下文 ----
            Application.Run(new TrayAppContext());
        }
    }
}

