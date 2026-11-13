using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AA2Trainer.WpfPrototype.Controls
{
    public partial class ColorPicker : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(int), typeof(ColorPicker),
                new FrameworkPropertyMetadata(unchecked((int)0xFFFFFFFF),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnValueChanged));

        private const int SpectrumWidth = 220;
        private const int SpectrumHeight = 140;
        private const int HueWidth = 220;

        private bool updating;
        private double hue;
        private double saturation = 100;
        private double lightness = 50;
        private int spectrumHue = -999;

        public ColorPicker()
        {
            InitializeComponent();
            Loaded += delegate
            {
                // Build the spectrum once. Re-adding the picker to the tree
                // (tab switches) must not rebuild the bitmap.
                if (SpectrumImage != null && SpectrumImage.Source == null)
                    ReadValue();
            };
            IsVisibleChanged += delegate
            {
                if (!IsVisible) return;
                UpdateSpectrum();
                UpdateMarkers();
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public double Hue
        {
            get { return hue; }
            set
            {
                double next = Clamp(value, 0, 360);
                if (Math.Abs(hue - next) < 0.01) return;
                hue = next;
                OnPropertyChanged();
                if (updating) return;
                UpdateValueFromHsl();
                UpdateSpectrum();
                UpdateMarkers();
            }
        }

        public double Saturation
        {
            get { return saturation; }
            set
            {
                double next = Clamp(value, 0, 100);
                if (Math.Abs(saturation - next) < 0.01) return;
                saturation = next;
                OnPropertyChanged();
                if (updating) return;
                UpdateValueFromHsl();
                UpdateMarkers();
            }
        }

        public double Lightness
        {
            get { return lightness; }
            set
            {
                double next = Clamp(value, 0, 100);
                if (Math.Abs(lightness - next) < 0.01) return;
                lightness = next;
                OnPropertyChanged();
                if (updating) return;
                UpdateValueFromHsl();
                UpdateMarkers();
            }
        }

        public int Red
        {
            get { return (Value >> 16) & 0xFF; }
            set { SetChannel(16, value); }
        }

        public int Green
        {
            get { return (Value >> 8) & 0xFF; }
            set { SetChannel(8, value); }
        }

        public int Blue
        {
            get { return Value & 0xFF; }
            set { SetChannel(0, value); }
        }

        private void SetChannel(int shift, int channel)
        {
            int clamped = channel < 0 ? 0 : (channel > 255 ? 255 : channel);
            int rgb = Value & 0xFFFFFF;
            if (((rgb >> shift) & 0xFF) == clamped) return;
            rgb = (rgb & ~(0xFF << shift)) | (clamped << shift);
            Value = unchecked((int)(0xFF000000u | (uint)rgb));
        }

        private static void OnValueChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            ColorPicker picker = d as ColorPicker;
            if (picker != null) picker.ReadValue();
        }

        private void ReadValue()
        {
            if (updating) return;
            int value = Value;
            Color color = Color.FromRgb(
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF));

            double h;
            double s;
            double l;
            RgbToHsl(color.R, color.G, color.B, out h, out s, out l);

            updating = true;
            hue = h;
            saturation = s * 100;
            lightness = l * 100;
            updating = false;

            OnPropertyChanged(nameof(Hue));
            OnPropertyChanged(nameof(Saturation));
            OnPropertyChanged(nameof(Lightness));
            OnPropertyChanged(nameof(Red));
            OnPropertyChanged(nameof(Green));
            OnPropertyChanged(nameof(Blue));
            UpdateSpectrum();
            UpdateMarkers();
        }

        private void UpdateValueFromHsl()
        {
            Color color = HslToRgb(hue, saturation / 100.0, lightness / 100.0);
            int value = unchecked((int)(0xFF000000u |
                ((uint)color.R << 16) | ((uint)color.G << 8) | color.B));

            updating = true;
            Value = value;
            updating = false;
        }

        private void UpdateSpectrum()
        {
            if (SpectrumImage == null) return;
            if (!IsVisible) return;

            int key = (int)Math.Round(hue) % 360;
            if (SpectrumImage.Source != null && key == spectrumHue) return;
            spectrumHue = key;

            byte[] pixels = new byte[SpectrumWidth * SpectrumHeight * 4];
            for (int y = 0; y < SpectrumHeight; y++)
            {
                double l = 1.0 - (double)y / (SpectrumHeight - 1);
                for (int x = 0; x < SpectrumWidth; x++)
                {
                    double s = (double)x / (SpectrumWidth - 1);
                    Color color = HslToRgb(hue, s, l);
                    int index = (y * SpectrumWidth + x) * 4;
                    pixels[index] = color.B;
                    pixels[index + 1] = color.G;
                    pixels[index + 2] = color.R;
                    pixels[index + 3] = 0xFF;
                }
            }

            BitmapSource bitmap = BitmapSource.Create(
                SpectrumWidth, SpectrumHeight, 96, 96,
                PixelFormats.Bgra32, null, pixels, SpectrumWidth * 4);
            bitmap.Freeze();
            SpectrumImage.Source = bitmap;
        }

        private void UpdateMarkers()
        {
            if (SpectrumMarker != null)
            {
                Canvas.SetLeft(SpectrumMarker,
                    saturation / 100.0 * SpectrumWidth - 6);
                Canvas.SetTop(SpectrumMarker,
                    (1.0 - lightness / 100.0) * SpectrumHeight - 6);
            }
            if (HueMarker != null)
            {
                Canvas.SetLeft(HueMarker, hue / 360.0 * HueWidth - 5);
                Canvas.SetTop(HueMarker, 0);
            }
        }

        private void Spectrum_MouseLeftButtonDown(object sender,
            MouseButtonEventArgs e)
        {
            SpectrumCanvas.CaptureMouse();
            UpdateSpectrumPoint(e.GetPosition(SpectrumCanvas));
            e.Handled = true;
        }

        private void Spectrum_MouseMove(object sender, MouseEventArgs e)
        {
            if (!SpectrumCanvas.IsMouseCaptured) return;
            UpdateSpectrumPoint(e.GetPosition(SpectrumCanvas));
        }

        private void Spectrum_MouseLeftButtonUp(object sender,
            MouseButtonEventArgs e)
        {
            SpectrumCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void UpdateSpectrumPoint(Point point)
        {
            double s = Clamp(point.X / SpectrumWidth * 100.0, 0, 100);
            double l = Clamp((1.0 - point.Y / SpectrumHeight) * 100.0, 0, 100);
            Saturation = s;
            Lightness = l;
        }

        private void Hue_MouseLeftButtonDown(object sender,
            MouseButtonEventArgs e)
        {
            HueCanvas.CaptureMouse();
            UpdateHuePoint(e.GetPosition(HueCanvas));
            e.Handled = true;
        }

        private void Hue_MouseMove(object sender, MouseEventArgs e)
        {
            if (!HueCanvas.IsMouseCaptured) return;
            UpdateHuePoint(e.GetPosition(HueCanvas));
        }

        private void Hue_MouseLeftButtonUp(object sender,
            MouseButtonEventArgs e)
        {
            HueCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void UpdateHuePoint(Point point)
        {
            Hue = Clamp(point.X / HueWidth * 360.0, 0, 360);
        }

        private void OnPropertyChanged(
            [CallerMemberName] string name = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static void RgbToHsl(byte r, byte g, byte b,
            out double h, out double s, out double l)
        {
            double red = r / 255.0;
            double green = g / 255.0;
            double blue = b / 255.0;
            double max = Math.Max(red, Math.Max(green, blue));
            double min = Math.Min(red, Math.Min(green, blue));
            l = (max + min) / 2.0;

            if (Math.Abs(max - min) < 0.00001)
            {
                h = 0;
                s = 0;
                return;
            }

            double delta = max - min;
            s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

            if (max == red)
                h = (green - blue) / delta + (green < blue ? 6.0 : 0.0);
            else if (max == green)
                h = (blue - red) / delta + 2.0;
            else
                h = (red - green) / delta + 4.0;

            h *= 60.0;
        }

        private static Color HslToRgb(double h, double s, double l)
        {
            if (s <= 0.00001)
            {
                byte gray = ToByte(l * 255.0);
                return Color.FromRgb(gray, gray, gray);
            }

            double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            double p = 2.0 * l - q;
            double hk = h / 360.0;

            return Color.FromRgb(
                ToByte(HueToRgb(p, q, hk + 1.0 / 3.0) * 255.0),
                ToByte(HueToRgb(p, q, hk) * 255.0),
                ToByte(HueToRgb(p, q, hk - 1.0 / 3.0) * 255.0));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1.0;
            if (t > 1) t -= 1.0;
            if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
            return p;
        }

        private static byte ToByte(double value)
        {
            if (value <= 0) return 0;
            if (value >= 255) return 255;
            return (byte)Math.Round(value);
        }
    }
}
