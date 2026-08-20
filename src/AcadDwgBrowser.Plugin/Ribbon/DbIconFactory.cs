using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AcadDwgBrowser.Plugin.Ribbon
{
    internal static class DbIconFactory
    {
        private static ImageSource? _large;
        private static ImageSource? _small;

        public static ImageSource Large => _large ??= Create(32);
        public static ImageSource Small => _small ??= Create(16);

        private static ImageSource Create(int size)
        {
            using (var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(System.Drawing.Color.Transparent);

                var pad = Math.Max(1, size / 16);
                var rect = new RectangleF(pad, pad, size - pad * 2, size - pad * 2);
                var radius = size * 0.22f;

                using (var path = RoundedRect(rect, radius))
                using (var fill = new SolidBrush(System.Drawing.Color.FromArgb(0x1E, 0x2A, 0x32)))
                {
                    g.FillPath(fill, path);
                }

                var bar = Math.Max(2, size / 8);
                using (var accent = new SolidBrush(System.Drawing.Color.FromArgb(0x6C, 0xAB, 0xC8)))
                {
                    g.FillRectangle(accent, pad, size - pad - bar, size - pad * 2, bar);
                }

                var fontSize = size >= 28 ? size * 0.42f : size * 0.48f;
                using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                using (var textBrush = new SolidBrush(System.Drawing.Color.White))
                {
                    var textRect = new RectangleF(0, size * 0.02f, size, size);
                    g.DrawString("dB", font, textBrush, textRect, sf);
                }

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    ms.Position = 0;

                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
        }

        private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            var diameter = radius * 2;
            var path = new GraphicsPath();
            if (diameter <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            var arc = new RectangleF(bounds.Location, new SizeF(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
