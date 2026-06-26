using System;
using System.Drawing;
using DeepSeekBalanceMonitor;

namespace DeepSeekBalanceMonitor.Tests
{
    internal static class Tests
    {
        [STAThread]
        private static void Main()
        {
            int failed = 0;

            try { ParsesBalance(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { HandlesMalformedJson(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { NormalizesRefreshMinutes(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { NormalizesApiKey(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { ValidatesApiKey(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { MasksApiKey(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { DefaultsWindowAndThemeSettings(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { NormalizesThemeMode(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { DefaultsWindowPositionUnset(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { ScalesRectanglesForDpi(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { ComputesFloatingLayout(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { CentersControlsInRows(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { BuildsCompactDisplayText(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }
            try { ComputesEdgeDockBounds(); } catch (Exception ex) { Console.WriteLine("  ✗ " + ex.Message); failed++; }

            if (failed == 0)
                Console.WriteLine("\n✓ 全部测试通过");
            else
                Console.WriteLine("\n✗ " + failed + " 个测试失败");

            Environment.Exit(failed > 0 ? 1 : 0);
        }

        private static void ScalesRectanglesForDpi()
        {
            AssertEqual(new Rectangle(15, 6, 30, 45),
                DpiHelper.ScaleRect(new Rectangle(10, 4, 20, 30), 1.5f),
                "scaled rectangle");
        }

        private static void CentersControlsInRows()
        {
            AssertEqual(6, SettingsLayout.CenterOffset(32, 20), "center offset small control");
            AssertEqual(1, SettingsLayout.CenterOffset(32, 30), "center offset tall control");
            AssertEqual(0, SettingsLayout.CenterOffset(32, 34), "center offset clamps oversized control");
        }

        // ============================================================
        //  BalanceSnapshot
        // ============================================================

        private static void ParsesBalance()
        {
            const string json = @"{
              ""is_available"": true,
              ""balance_infos"": [
                { ""currency"": ""CNY"", ""total_balance"": ""110.00"", ""granted_balance"": ""10.00"", ""topped_up_balance"": ""100.00"" }
              ]
            }";

            BalanceSnapshot snapshot = BalanceSnapshot.FromJson(json);

            AssertEqual("CNY", snapshot.Currency, "currency");
            AssertEqual("110.00", snapshot.TotalBalance, "total balance");
            AssertEqual(true, snapshot.IsAvailable, "availability");
            AssertEqual("CNY 110.00", snapshot.ShortText, "short text");
            AssertEqual("¥110.00", snapshot.TaskbarText, "taskbar text");
        }

        private static void HandlesMalformedJson()
        {
            // 空 JSON
            AssertThrows(() => BalanceSnapshot.FromJson(""), "empty string");
            AssertThrows(() => BalanceSnapshot.FromJson("not json"), "not json");
            // 没有 balance_infos
            AssertThrows(() => BalanceSnapshot.FromJson(@"{""is_available"": true}"), "missing infos");
        }

        // ============================================================
        //  AppSettings – RefreshMinutes
        // ============================================================

        private static void NormalizesRefreshMinutes()
        {
            AssertEqual(1, AppSettings.NormalizeRefreshMinutes(0), "minimum refresh");
            AssertEqual(1, AppSettings.NormalizeRefreshMinutes(-5), "negative refresh");
            AssertEqual(30, AppSettings.NormalizeRefreshMinutes(30), "valid refresh");
            AssertEqual(1440, AppSettings.NormalizeRefreshMinutes(2000), "maximum refresh");
        }

        // ============================================================
        //  AppSettings – API Key
        // ============================================================

        private static void NormalizesApiKey()
        {
            AssertEqual("sk-abc", AppSettings.NormalizeApiKey(" Bearer sk-abc "), "bearer key");
            AssertEqual("sk-abc", AppSettings.NormalizeApiKey("\"sk-abc\""), "quoted key");
            AssertEqual("sk-abc", AppSettings.NormalizeApiKey("'Bearer sk-abc'"), "quoted bearer key");
            AssertEqual("", AppSettings.NormalizeApiKey(null), "null key");
            AssertEqual("", AppSettings.NormalizeApiKey(""), "empty key");
        }

        private static void ValidatesApiKey()
        {
            AssertEqual(true, AppSettings.IsValidApiKey("sk-xxxxxxxxxxxxxxxx"), "valid key");
            AssertEqual(true, AppSettings.IsValidApiKey(" sk-xxx "), "valid key with spaces");
            AssertEqual(false, AppSettings.IsValidApiKey(""), "empty key");
            AssertEqual(false, AppSettings.IsValidApiKey(null), "null key");
            AssertEqual(true, AppSettings.IsValidApiKey("Bearer sk-xxx"), "bearer key normalized");
        }

        private static void MasksApiKey()
        {
            AssertEqual("sk-a****bcde", AppSettings.MaskApiKey("sk-abcdefghijklmnopqbcde"), "long key mask");
            AssertEqual("", AppSettings.MaskApiKey(""), "empty key mask");
            AssertEqual("sk-t****", AppSettings.MaskApiKey("sk-test"), "short key mask");
        }

        private static void DefaultsWindowAndThemeSettings()
        {
            var settings = new AppSettings();
            AssertEqual(true, settings.AlwaysOnTop, "default always on top");
            AssertEqual("System", settings.ThemeMode, "default theme mode");
        }

        private static void NormalizesThemeMode()
        {
            AssertEqual("Light", AppSettings.NormalizeThemeMode("light"), "light theme mode");
            AssertEqual("Dark", AppSettings.NormalizeThemeMode("DARK"), "dark theme mode");
            AssertEqual("System", AppSettings.NormalizeThemeMode(""), "empty theme mode");
            AssertEqual("System", AppSettings.NormalizeThemeMode("unknown"), "unknown theme mode");
        }


        private static void DefaultsWindowPositionUnset()
        {
            var settings = new AppSettings();
            AssertEqual(0, settings.WindowWidth, "default window width unset");
            AssertEqual(0, settings.WindowHeight, "default window height unset");
        }
        // ============================================================
        private static void ComputesFloatingLayout()
        {
            Rectangle value = FloatingBalanceLayout.ValueBounds(new Size(178, 40));
            Rectangle dot = FloatingBalanceLayout.StatusDotBounds(new Size(178, 40));
            Rectangle status = FloatingBalanceLayout.StatusTextBounds(new Size(178, 40));
            Rectangle refresh = FloatingBalanceLayout.RefreshIconBounds(new Size(178, 40));
            AssertEqual(new Rectangle(11, 0, 66, 40), value, "value bounds");
            AssertEqual(new Rectangle(88, 16, 7, 7), dot, "status dot bounds");
            AssertEqual(new Rectangle(100, 10, 36, 20), status, "status text bounds");
            AssertEqual(new Rectangle(142, 12, 16, 16), refresh, "refresh icon bounds");
        }

        // ============================================================
        // ============================================================
        //  BalanceSnapshot – display text
        // ============================================================

        private static void BuildsCompactDisplayText()
        {
            BalanceSnapshot snapshot = BalanceSnapshot.FromJson(@"{ ""is_available"": true, ""balance_infos"": [ { ""currency"": ""CNY"", ""total_balance"": ""110.00"" } ] }");
            AssertEqual("¥110.00", snapshot.TaskbarText, "taskbar text");
            AssertEqual("CNY 110.00", snapshot.ShortText, "short text");
        }

        // ============================================================
        //  EdgeDockLayout
        // ============================================================

        private static void ComputesEdgeDockBounds()
        {
            Rectangle work = new Rectangle(0, 0, 1920, 1040);
            Size size = new Size(260, 132);
            Rectangle expanded = EdgeDockLayout.ExpandedBounds(work, size, EdgeSide.Right);
            Rectangle hidden = EdgeDockLayout.HiddenBounds(expanded, EdgeSide.Right, 10);

            AssertEqual(1660, expanded.X, "expanded x");
            AssertEqual(80, expanded.Y, "expanded y");
            AssertEqual(20, hidden.Width, "hidden tab width");
            AssertEqual(1900, hidden.X, "hidden tab x");
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
                throw new Exception(name + ": expected <" + expected + "> but got <" + actual + ">");
            Console.WriteLine("  ✓ " + name);
        }

        private static void AssertThrows(Action action, string name)
        {
            try
            {
                action();
                throw new Exception(name + ": expected exception but none thrown");
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("  ✓ " + name + " (正确抛出异常)");
            }
            catch (Exception ex)
            {
                throw new Exception(name + ": expected InvalidOperationException but got " + ex.GetType().Name);
            }
        }
    }
}








