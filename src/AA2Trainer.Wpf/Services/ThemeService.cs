using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// Swaps the Light/Dark resource dictionary and keeps the window chrome in
    /// sync. This is deliberately small: the theme dictionaries hold semantic
    /// brushes, and every style uses DynamicResource for colour.
    /// </summary>
    public static class ThemeService
    {
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeLegacy = 19;

        public static ThemeMode CurrentMode { get; private set; } = ThemeMode.System;
        public static bool IsDark { get; private set; }

        public static void Apply(ThemeMode mode, Window window)
        {
            CurrentMode = mode;
            IsDark = mode == ThemeMode.Dark ||
                (mode == ThemeMode.System && !IsSystemLight());

            string assembly = typeof(ThemeService).Assembly.GetName().Name;
            ResourceDictionary dictionary = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/" + assembly + ";component/Themes/" +
                    (IsDark ? "Dark.xaml" : "Light.xaml"),
                    UriKind.Absolute)
            };

            ResourceDictionary merged = Application.Current.Resources;
            for (int i = merged.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                Uri source = merged.MergedDictionaries[i].Source;
                if (source == null) continue;
                string text = source.OriginalString;
                if (text.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                    text.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    merged.MergedDictionaries.RemoveAt(i);
                }
            }
            merged.MergedDictionaries.Add(dictionary);

            if (window != null) ApplyWindowChrome(window, IsDark);
        }

        public static void ApplyWindowChrome(Window window, bool dark)
        {
            if (window == null) return;
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;

            int value = dark ? 1 : 0;
            int result = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode,
                ref value, sizeof(int));
            if (result != 0)
            {
                DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeLegacy,
                    ref value, sizeof(int));
            }
        }

        public static bool IsSystemLight()
        {
            try
            {
                object value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 1);
                return value == null || Convert.ToInt32(value) != 0;
            }
            catch
            {
                return true;
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute,
            ref int value, int size);
    }
}
