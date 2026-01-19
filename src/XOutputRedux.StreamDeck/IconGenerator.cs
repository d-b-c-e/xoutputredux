using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace XOutputRedux.StreamDeck;

/// <summary>
/// Generates button images with custom text rendering
/// </summary>
public static class IconGenerator
{
    private const int IconSize = 144; // @2x size for quality

    /// <summary>
    /// Generate a button image with two lines of text
    /// </summary>
    /// <param name="baseIconPath">Path to the base icon (with gamepad)</param>
    /// <param name="topText">First line of text (smaller)</param>
    /// <param name="bottomText">Second line of text (larger)</param>
    /// <returns>Base64 encoded PNG image</returns>
    public static string GenerateButtonImage(string baseIconPath, string topText, string bottomText)
    {
        using var bitmap = new Bitmap(IconSize, IconSize);
        using var graphics = Graphics.FromImage(bitmap);

        // Set quality
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // Fill background first
        graphics.Clear(Color.FromArgb(0x4A, 0x4A, 0x4A));

        // Load and draw base icon (contains gamepad)
        if (File.Exists(baseIconPath))
        {
            try
            {
                using var baseIcon = Image.FromFile(baseIconPath);
                graphics.DrawImage(baseIcon, 0, 0, IconSize, IconSize);
            }
            catch
            {
                // Ignore - will use solid background
            }
        }

        // Text settings
        using var whiteBrush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        // Top text (smaller) - positioned in middle area
        if (!string.IsNullOrEmpty(topText))
        {
            using var topFont = new Font("Segoe UI", 14, FontStyle.Bold);
            var topRect = new RectangleF(0, 58, IconSize, 40);
            graphics.DrawString(topText, topFont, whiteBrush, topRect, format);
        }

        // Bottom text (larger) - positioned lower
        if (!string.IsNullOrEmpty(bottomText))
        {
            using var bottomFont = new Font("Segoe UI", 18, FontStyle.Bold);
            var bottomRect = new RectangleF(0, 98, IconSize, 40);
            graphics.DrawString(bottomText, bottomFont, whiteBrush, bottomRect, format);
        }

        // Convert to base64 with data URI prefix
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        var base64 = Convert.ToBase64String(ms.ToArray());
        return $"data:image/png;base64,{base64}";
    }
}
