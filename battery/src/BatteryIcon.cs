using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;

namespace FifineTools.Battery
{
    internal static class BatteryIcon
    {
        internal static byte[] Render(BatteryDevice device, BatteryDisplay display)
        {
            Color accent = Accent(display);
            using (var bitmap = new Bitmap(128, 128))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var border = new Pen(Color.FromArgb(115, accent), 2))
            using (var brush = new SolidBrush(accent))
            using (var percentFont = new Font("Segoe UI", 34, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var labelFont = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var frame = new GraphicsPath())
            using (var bytes = new MemoryStream())
            {
                graphics.Clear(ColorTranslator.FromHtml("#161D28"));
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                frame.AddArc(4, 4, 28, 28, 180, 90);
                frame.AddArc(96, 4, 28, 28, 270, 90);
                frame.AddArc(96, 96, 28, 28, 0, 90);
                frame.AddArc(4, 96, 28, 28, 90, 90);
                frame.CloseFigure();
                graphics.DrawPath(border, frame);

                DrawDevice(graphics, device, accent);
                string level = display.Percent.HasValue
                    ? display.Percent.Value.ToString(CultureInfo.InvariantCulture) + "%"
                    : "—";
                graphics.DrawString(level, percentFont, brush, new RectangleF(5, 51, 118, 43), centered);

                string label = display.LastKnown ? "ПОСЛЕДНИЙ" :
                    display.Charging ? "ЗАРЯДКА" :
                    display.Percent.HasValue ? string.Empty : "НЕТ ДАННЫХ";
                graphics.DrawString(label, labelFont, brush, new RectangleF(5, 96, 118, 21), centered);
                bitmap.Save(bytes, ImageFormat.Png);
                return bytes.ToArray();
            }
        }

        internal static void DrawDevice(Graphics graphics, BatteryDevice device, Color color)
        {
            using (var line = new Pen(color, 3.5f))
            using (var fill = new SolidBrush(color))
            {
                line.StartCap = LineCap.Round;
                line.EndCap = LineCap.Round;
                if (device == BatteryDevice.Mouse)
                {
                    graphics.DrawEllipse(line, 51, 13, 26, 34);
                    graphics.DrawLine(line, 64, 19, 64, 28);
                    return;
                }

                graphics.DrawRectangle(line, 42, 17, 44, 27);
                for (int column = 0; column < 4; column++)
                {
                    graphics.FillRectangle(fill, 49 + column * 8, 24, 4, 4);
                }
                graphics.FillRectangle(fill, 51, 34, 26, 3);
            }
        }

        private static Color Accent(BatteryDisplay display)
        {
            if (!display.Percent.HasValue || display.LastKnown)
            {
                return ColorTranslator.FromHtml("#C4CEDD");
            }
            if (display.Percent.Value <= 15)
            {
                return ColorTranslator.FromHtml("#FF9CA6");
            }
            return ColorTranslator.FromHtml(display.Percent.Value <= 30 ? "#FFDC9E" : "#A3F7C5");
        }
    }
}
