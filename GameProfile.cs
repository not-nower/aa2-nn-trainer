using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace AA2Trainer
{
    public sealed class GameProfile
    {
        // Do not update this fingerprint without re-verifying the full pointer
        // map in Definitions.cs against the new executable. The fingerprint and
        // the offsets are one profile; changing only the hash would let a
        // mismatched build open the write gate.
        public const string ExpectedVersion = "2.0.1";
        public const uint ExpectedModuleSize = 0xD2C000;
        public const string ExpectedSha256 =
            "DEE8451A1C7DAB60703C0BE9B0E01A82053F5E98DBCAF09F704CD440923D4517";

        public string FileVersion { get; private set; }
        public uint ModuleSize { get; private set; }
        public string Sha256 { get; private set; }
        public bool VersionAndSizeMatch { get; private set; }
        public bool HashMatch { get; private set; }
        public bool Known { get { return VersionAndSizeMatch && HashMatch; } }

        public string Display
        {
            get
            {
                string version = string.IsNullOrEmpty(FileVersion) ? "unknown" : FileVersion;
                string hash = string.IsNullOrEmpty(Sha256) ? "no hash" : Sha256.Substring(0, 8);
                return "AA2Play " + version + " / 0x" + ModuleSize.ToString("X") + " / " + hash +
                       (Known ? "" : "  (unverified)");
            }
        }

        private static string cachedHashPath;
        private static long cachedHashLength;
        private static long cachedHashWriteTicks;
        private static string cachedHash;

        public static GameProfile Detect(GameMemory mem)
        {
            GameProfile p = new GameProfile();
            p.ModuleSize = mem.ModuleSize;
            p.FileVersion = null;
            p.Sha256 = null;

            if (!string.IsNullOrEmpty(mem.GamePath) && File.Exists(mem.GamePath))
            {
                try
                {
                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(mem.GamePath);
                    p.FileVersion = NormalizeVersion(info.FileVersion);
                }
                catch { }

                try
                {
                    p.Sha256 = GetCachedSha256(mem.GamePath);
                }
                catch { }
            }

            p.VersionAndSizeMatch =
                string.Equals(p.FileVersion, ExpectedVersion, StringComparison.OrdinalIgnoreCase) &&
                p.ModuleSize == ExpectedModuleSize;
            p.HashMatch =
                !string.IsNullOrEmpty(p.Sha256) &&
                string.Equals(p.Sha256, ExpectedSha256, StringComparison.OrdinalIgnoreCase);
            return p;
        }

        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return null;
            string v = version.Trim();
            // FileVersionInfo normally gives 2.0.1; some tools append .0
            if (v.EndsWith(".0", StringComparison.Ordinal) && v.Split('.').Length == 4)
                v = v.Substring(0, v.Length - 2);
            return v;
        }

        private static string GetCachedSha256(string path)
        {
            // Hash the exe once per launch. Hashing a 13 MB file on every
            // periodic health check would stall the UI for no benefit.
            FileInfo info = new FileInfo(path);
            if (!info.Exists) return null;

            long length = info.Length;
            long writeTicks = info.LastWriteTimeUtc.Ticks;
            if (cachedHash != null &&
                string.Equals(cachedHashPath, info.FullName, StringComparison.OrdinalIgnoreCase) &&
                cachedHashLength == length &&
                cachedHashWriteTicks == writeTicks)
                return cachedHash;

            string hash = ComputeSha256(path);
            cachedHashPath = info.FullName;
            cachedHashLength = length;
            cachedHashWriteTicks = writeTicks;
            cachedHash = hash;
            return hash;
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }
    }
}
