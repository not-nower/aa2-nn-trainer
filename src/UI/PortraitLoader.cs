using System;
using System.Drawing;
using System.IO;

namespace AA2Trainer
{
    public sealed class PortraitImage : IDisposable
    {
        public Bitmap Bitmap { get; private set; }
        public uint Address { get; private set; }
        public uint Size { get; private set; }

        public PortraitImage(Bitmap bitmap, uint address, uint size)
        {
            Bitmap = bitmap;
            Address = address;
            Size = size;
        }

        public void Dispose()
        {
            if (Bitmap != null)
            {
                Bitmap.Dispose();
                Bitmap = null;
            }
        }
    }

    public static class PortraitLoader
    {
        public static void ReadInfo(GameMemory mem, GameContext view,
            out uint address, out uint size)
        {
            uint thumbAddr = ReadUInt(mem, view, "ThumbAddr");
            uint thumbSize = ReadUInt(mem, view, "ThumbSize");

            if (thumbAddr != 0 && thumbSize > 0 && mem.IsPlausiblePointer(thumbAddr))
            {
                address = thumbAddr;
                size = thumbSize;
                return;
            }

            address = ReadUInt(mem, view, "PotraitAddr");
            size = ReadUInt(mem, view, "PotraitSize");
        }

        public static void ReadFullInfo(GameMemory mem, GameContext view,
            out uint address, out uint size)
        {
            address = ReadUInt(mem, view, "PotraitAddr");
            size = ReadUInt(mem, view, "PotraitSize");

            if (address != 0 && size > 0 && mem.IsPlausiblePointer(address))
                return;

            // Fall back to the small face thumbnail if the card portrait
            // is unavailable or invalid.
            ReadInfo(mem, view, out address, out size);
        }

        public static PortraitImage Load(GameMemory mem, GameContext view)
        {
            uint address;
            uint size;
            ReadInfo(mem, view, out address, out size);
            return Load(mem, address, size);
        }

        public static PortraitImage Load(GameMemory mem, uint address, uint size)
        {
            return new PortraitImage(LoadBitmap(mem, address, size), address, size);
        }

        public static Bitmap LoadBitmap(GameMemory mem, uint address, uint size)
        {
            if (size == 0 || size > 8 * 1024 * 1024) return null;
            if (!mem.IsPlausiblePointer(address)) return null;

            byte[] data = mem.ReadBytes(address, (int)size);
            if (data == null || data.Length < 8) return null;

            try
            {
                using (MemoryStream stream = new MemoryStream(data))
                using (Image image = Image.FromStream(stream))
                {
                    return new Bitmap(image);
                }
            }
            catch
            {
                return null;
            }
        }

        private static uint ReadUInt(GameMemory mem, GameContext view, string name)
        {
            EvalResult r = DashboardData.Eval(mem, view, name, 0);
            if (r == null || r.Bytes == null || r.Bytes.Length < 4) return 0;
            return BitConverter.ToUInt32(r.Bytes, 0);
        }
    }
}
