using System;
using System.Diagnostics;
using System.Reflection;

namespace AA2Trainer
{
    /// <summary>
    /// Version strings for the title bar. The trainer version comes from the
    /// assembly (set by &lt;Version&gt; in the project file) so it is defined once.
    /// </summary>
    public static class AppInfo
    {
        public static string Version
        {
            get
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version == null) return "0.0.0";
                int build = version.Build < 0 ? 0 : version.Build;
                return version.Major + "." + version.Minor + "." + build;
            }
        }

        public static string FileVersion(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrEmpty(info.FileVersion))
                    return info.FileVersion.Trim();
                if (!string.IsNullOrEmpty(info.ProductVersion))
                    return info.ProductVersion.Trim();
            }
            catch { }
            return null;
        }
    }
}
