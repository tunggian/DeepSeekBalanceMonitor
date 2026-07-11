using System;
using System.Reflection;
using System.Windows.Forms;

namespace DeepSeekBalanceMonitor
{
    internal static class WindowPositionPersistenceTests
    {
        [STAThread]
        private static int Main()
        {
            using (var form = new FloatingBalanceForm())
            {
                int changes = 0;
                form.PositionChanged += delegate { changes++; };
                typeof(FloatingBalanceForm).GetField("dragging", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, true);
                typeof(FloatingBalanceForm).GetMethod("OnDragEnd", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(
                    form, new object[] { form, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                if (changes != 1)
                    throw new InvalidOperationException("拖动结束应触发一次位置保存请求，实际：" + changes);
            }
            Console.WriteLine("PASS: 拖动结束触发一次位置保存请求");
            return 0;
        }
    }
}
