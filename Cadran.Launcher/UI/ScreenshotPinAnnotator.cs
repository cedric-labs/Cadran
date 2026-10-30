using System.Drawing.Drawing2D;

namespace Cadran.Launcher.UI;

/// <summary>
/// Dessine sur une capture d'écran de documentation les numéros et cadres repérant les zones de l'interface.
/// </summary>
internal static class ScreenshotPinAnnotator
{
    private const int PinDiameter = 24;

    /// <summary>
    /// Entoure chaque zone d'un cadre coloré et pose une pastille numérotée sur son angle supérieur gauche.
    /// </summary>
    public static void Draw(Graphics graphics, IEnumerable<MainLauncherForm.ScreenshotPin> pins)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        foreach (var pin in pins)
        {
            using var ringPen = new Pen(pin.Color, 2f);
            graphics.DrawRectangle(ringPen, pin.Rect.X - 3, pin.Rect.Y - 3, pin.Rect.Width + 6, pin.Rect.Height + 6);

            var pinX = pin.Rect.X - 10;
            var pinY = pin.Rect.Y - 10;
            using var brush = new SolidBrush(pin.Color);
            graphics.FillEllipse(brush, pinX, pinY, PinDiameter, PinDiameter);
            using var whitePen = new Pen(Color.White, 2f);
            graphics.DrawEllipse(whitePen, pinX, pinY, PinDiameter, PinDiameter);

            using var textBrush = new SolidBrush(Color.White);
            using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString(pin.Number, font, textBrush, new RectangleF(pinX, pinY, PinDiameter, PinDiameter), format);
        }
    }
}
