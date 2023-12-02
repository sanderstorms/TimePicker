using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Opulos.Core.UI;
using Opulos.Core.Utils;
// MultiKey

namespace Opulos.Core.Drawing;

public static class MeasureString
{
    private const int
        A = 255 << 24; // Alpha Mask (bits [0 to 7] in the Color Value. Using (color.ToArgb() | A) != -1 should be faster than comparing each R/G/B to 255.

    private static readonly object dummy = new();
    private static readonly RectangleF MAX_RECTF = new(0, 0, float.MaxValue, float.MaxValue);
    private static readonly Hashtable ht = new();

    public static SizeF MeasureApprox(string text, Graphics g, Font font, StringFormat format)
    {
        lock (dummy)
        {
            return g.MeasureString(text, font, int.MaxValue, format);
        }
    }

    /// <summary>
    ///     Much faster way to calculate the width of text than compared with using a raster approach. Should only be used with
    ///     regular text, not icon glyphs.
    ///     Only the width is accurate, and only for charaters that do not over-extend or under-extend their baseline.
    ///     For example, Segoe UI Symbol private use area character '\uE216 is a big ring with a triangle point at
    ///     the top. Using MeasureFast returns dimensions X=0,Y=0,Width=40.83659,Height=44.33594. However, using
    ///     MeasureRaster returns the correct size with the underhang: X=-12,Y=-14,Width=64,Height=70.
    /// </summary>
    public static RectangleF MeasureFast(string text, Graphics g, Font font, StringFormat format)
    {
        // lock actually speeds up the performance. Espcially when the same font is accessed, there is a 4x performance hit.
        // Accessing the same Graphics object can lead to a crash. Accessing the same StringFormat object
        // can lead to conflicts with the text.Length, e.g: throws an ArgumentException "Parameter is not valid."
        lock (dummy)
        {
            // Note: When StringFormat.GenericDefault is used, then a lot of extra space is included in the width.
            // Even when StringFormat.GenericTypographic is used, it's closer, but the differences add up and make
            // the ToolTip text noticeably unjustified.
            //SizeF sz = g.MeasureString(text, font, short.MaxValue, format);
            //return new RectangleF(0, 0, sz.Width, sz.Height);

            CharacterRange[] ranges = { new(0, text.Length) };
            format.SetMeasurableCharacterRanges(ranges);
            var regions = g.MeasureCharacterRanges(text, font, MAX_RECTF, format);

            if (regions.Length != 1)
                throw new Exception("Graphics.MeasureCharacterRanges returned an unexpected number of regions: " +
                                    regions.Length);

            using (var r = regions[0])
            {
                var rect = r.GetBounds(g);
                return rect;
            }
        }
    }

    // returns a rectangle because it's possible that the top,left are outside of the 0,0 requested position.
    public static Rectangle Measure(string text, Graphics graphics, Font font,
        DrawMethod drawMethod = DrawMethod.Graphics, TextFormatFlags textFormatFlags = TextFormatFlags.Default,
        Rectangle? rect = null, StringFormat stringFormat = null, bool cache = false, bool increaseSize = true)
    {
        if (string.IsNullOrEmpty(text))
            return Rectangle.Empty;

        lock (dummy)
        {
            return MeasureInternal(text, graphics, font, drawMethod, textFormatFlags, rect, stringFormat, cache,
                increaseSize);
        }
    }

    private static Rectangle MeasureInternal(string text, Graphics graphics, Font font, DrawMethod drawMethod,
        TextFormatFlags textFormatFlags, Rectangle? rect, StringFormat stringFormat, bool cache, bool increaseSize)
    {
        MultiKey mk = null;
        if (cache)
        {
            var numKeys = 3 + (stringFormat == null ? 0 : 7) + (drawMethod == DrawMethod.TextRenderer ? 1 : 0);
            var keys = new object[numKeys];
            var k = 0;
            keys[k++] = text;
            keys[k++] = graphics.TextRenderingHint;
            keys[k++] = font;
            if (drawMethod == DrawMethod.TextRenderer)
                keys[k++] = textFormatFlags;

            if (stringFormat != null)
            {
                // StringFormat.GenericXXX returns new objects, so the object itself can't be used as a key. E.g. StringFormat.GenericDefault == StringFormat.GenericDefault returns false,
                // and the HashCode returned is different for each time it is called.
                // However, Font behaves well. E.g: Font f1 = SystemFonts.MenuFont; Font f2 = new Font(f1.Name, f1.Size, f1.Style); f1.Equals(f2); // returns true and both have the same hash code
                keys[k++] = stringFormat.Alignment;
                keys[k++] = stringFormat.DigitSubstitutionLanguage;
                keys[k++] = stringFormat.DigitSubstitutionMethod;
                keys[k++] = stringFormat.FormatFlags;
                keys[k++] = stringFormat.HotkeyPrefix;
                keys[k++] = stringFormat.LineAlignment;
                keys[k++] = stringFormat.Trimming;
            }

            //keys.Add(rect); // don't add rectangle
            mk = new MultiKey(keys);

            var o = ht[mk];
            if (o != null)
                return (Rectangle)o;
        }

        var size = Size.Empty;
        if (rect.HasValue)
        {
            var r2 = rect.Value;
            size = r2.Size;
            r2.Location = Point.Empty;
            rect = r2;
        }
        else
        {
            if (drawMethod == DrawMethod.Graphics)
                size = Size.Ceiling(graphics.MeasureString(text, font, int.MaxValue, stringFormat));
            else
                size = TextRenderer.MeasureText(graphics, text, font, Size.Empty, textFormatFlags);
        }

        if (size.Width == 0 || size.Height == 0)
            return Rectangle.Empty;

        var increaseBy = Math.Max(10, (int)Math.Ceiling(font.Size));
        size = new Size(size.Width + increaseBy, size.Height + increaseBy);

        Bitmap bitmap = null;
        Graphics g2 = null;
        var r3 = rect.HasValue ? rect.Value : new Rectangle(new Point(increaseBy / 2, increaseBy / 2), size);

        var stride = 0;
        byte[] pixels = null;
        var bitsPerPixel = 0;
        var i = 0;

        while (i++ < 10)
        {
            // protect against infinite loop
            if (bitmap != null)
            {
                bitmap.Dispose();
                g2.Dispose();
            }

            // Note: tested using PixelFormat.Format24bppRgb to see if it had performance was better
            // but there was no significant different.
            bitmap = new Bitmap(r3.Width, r3.Height, graphics);
            bitsPerPixel = Image.GetPixelFormatSize(bitmap.PixelFormat);
            g2 = Graphics.FromImage(bitmap);
            g2.TextRenderingHint = graphics.TextRenderingHint;
            g2.SmoothingMode = graphics.SmoothingMode;
            g2.Clear(Color.White);
            if (drawMethod == DrawMethod.Graphics)
                g2.DrawString(text, font, Brushes.Black, r3.X, r3.Y, stringFormat);
            else
                // always specify a bounding rectangle. Otherwise if flags contains VerticalCenter it will be half cutoff above point (0,0)
                // it's possible that the textFormatFlags force the text to always be drawn on the boundary, which would lead to an infinite loop
                TextRenderer.DrawText(g2, text, font, r3, Color.Black, Color.White, textFormatFlags);

            pixels = GetPixelArray(bitmap, out stride);
            if (!increaseSize)
                break;

            bool top, left, bottom, right;
            if (!ScanEdgesInternal(pixels, r3.Width, r3.Height, stride, bitsPerPixel, out top, out left, out bottom,
                    out right))
                break; // no non-white pixels on border, hurrah!

            if (top)
            {
                r3.Height += increaseBy;
                r3.Y += increaseBy;
            }

            if (bottom)
                r3.Height += increaseBy;

            if (left)
            {
                r3.Width += increaseBy;
                r3.X += increaseBy;
            }

            if (right)
                r3.Width += increaseBy;
        }

        var r = ScanInternal(pixels, bitmap.Width, bitmap.Height, stride, bitsPerPixel);
        r.X -= r3.X;
        r.Y -= r3.Y;

        g2.Dispose();
        bitmap.Dispose();

        if (cache)
            ht[mk] = r;

        return r;
    }

    /// <summary>
    ///     Note: The color order of PixelFormat is:
    ///     Format24bppRgb:   [B, G, R, B, G, R, ... ]
    ///     Format32bppPArgb: [B, G, R, A, B, G, R, A, ... ]
    ///     Format32bppRgb:   [B, G, R, 255, B, G, R, 255, ... ]
    ///     Format32bppArgb:  [B, G, R, A, B, G, R, A, ... ] (Bitmaps 'A' is always 255).
    ///     For Format24bppRgb, the stride is not always a whole multiple of the width. An image might be 150 pixels wide, but
    ///     have a stride length of 452.
    /// </summary>
    public static byte[] GetPixelArray(Bitmap bmp, out int stride)
    {
        var w = bmp.Width;
        var h = bmp.Height;
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, bmp.PixelFormat);
        var pointer = data.Scan0;
        stride = data.Stride; // stride = (bytes per pixel) x (w)
        var size = stride * h;
        var pixels = new byte[size];
        Marshal.Copy(pointer, pixels, 0, size);
        bmp.UnlockBits(data);
        return pixels;
    }

    public static Rectangle Scan(Bitmap bmp)
    {
        var pf = bmp.PixelFormat;
        var bitsPerPixel = Image.GetPixelFormatSize(pf);
        if (bitsPerPixel != 24 && bitsPerPixel != 32)
            return Scan_old(bmp);

        var w = bmp.Width; //int w = maxWidth.HasValue ? Math.Min(maxWidth.Value, bitmap.Width) : bitmap.Width;
        var h = bmp.Height; //int h = maxHeight.HasValue ? Math.Min(maxHeight.Value, bitmap.Height) : bitmap.Height;
        var stride = 0;
        var pixels = GetPixelArray(bmp, out stride);

        return ScanInternal(pixels, w, h, stride, bitsPerPixel);
    }

    // returns true if any of the edges has a non-white pixel, and indicates which edges have non-white pixels
    private static bool ScanEdgesInternal(byte[] pixels, int w, int h, int stride, int bitsPerPixel, out bool top,
        out bool left, out bool bottom, out bool right)
    {
        top = left = bottom = right = false;
        var w1 = w - 1;
        var h1 = h - 1;
        var bk = h1 * stride;
        var bytesPerPixel = bitsPerPixel / 8; //(stride / w) / 8;

        for (var i = w1; i >= 0; i--)
        {
            var ix = i * bytesPerPixel;
            if (!top)
            {
                var k = ix;
                if (pixels[k++] != 255 || pixels[k++] != 255 || pixels[k++] != 255)
                {
                    top = true;
                    if (bottom)
                        break;
                }
            }

            if (!bottom)
            {
                var k = ix + bk;
                if (pixels[k++] != 255 || pixels[k++] != 255 || pixels[k++] != 255)
                {
                    bottom = true;
                    if (top)
                        break;
                }
            }
        }

        var wx = w1 * bytesPerPixel;
        for (var i = h1; i >= 0; i--)
        {
            var ix = i * stride;
            if (!left)
            {
                var k = ix;
                if (pixels[k++] != 255 || pixels[k++] != 255 || pixels[k++] != 255)
                {
                    left = true;
                    if (right)
                        break;
                }
            }

            if (!right)
            {
                var k = ix + wx;
                if (pixels[k++] != 255 || pixels[k++] != 255 || pixels[k++] != 255)
                {
                    right = true;
                    if (left)
                        break;
                }
            }
        }

        return top || bottom || left || right;
    }

    private static Rectangle ScanInternal(byte[] pixels, int w, int h, int stride, int bitsPerPixel)
    {
        int left, right, top, bottom;
        left = right = top = bottom = -1;
        var bytesPerPixel = bitsPerPixel / 8;
        var a = bitsPerPixel == 24 ? 0 : 1;

        // scanning saves about 50-60% of having to scan the entire image
        for (var i = 0; i < h; i++)
        {
            var k = i * stride;
            for (var j = 0; j < w; j++)
            {
                if (pixels[k++] != 255 || pixels[k++] != 255 || pixels[k++] != 255)
                {
                    top = i;
                    goto FOUND_TOP;
                }

                k += a; // skip the alpha channel if there is one
            }
        }

        return Rectangle.Empty;
        FOUND_TOP:
        for (var i = h - 1; i >= 0; i--)
        {
            var k = i * stride;
            for (var j = 0; j < w; j++)
            {
                if (pixels[k++] != 255 || pixels[k++] != 255 || pixels[k++] != 255)
                {
                    bottom = i;
                    goto FOUND_BOTTOM;
                }

                k += a; // skip the alpha channel if there is one
            }
        }

        FOUND_BOTTOM:
        var ts = top * stride;
        for (var j = 0; j < w; j++)
        {
            var z = j * bytesPerPixel + ts;
            for (var i = top; i <= bottom; i++, z += stride)
            {
                var k = z;
                if (pixels[k++] != 255 || pixels[k++] != 255 || pixels[k++] != 255)
                {
                    left = j;
                    goto FOUND_LEFT;
                }
            }
        }

        FOUND_LEFT:
        for (var j = w - 1; j >= 0; j--)
        {
            var z = j * bytesPerPixel + ts;
            for (var i = top; i <= bottom; i++, z += stride)
            {
                var k = z;
                if (pixels[k++] != 255 || pixels[k++] != 255 || pixels[k++] != 255)
                {
                    right = j;
                    goto FOUND_ALL;
                }
            }
        }

        FOUND_ALL:
        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    public static Rectangle Scan_old(Bitmap bitmap, int? maxWidth = null, int? maxHeight = null)
    {
        int left, right, top, bottom;
        left = right = top = bottom = -1;
        var w = maxWidth.HasValue ? Math.Min(maxWidth.Value, bitmap.Width) : bitmap.Width;
        var h = maxHeight.HasValue ? Math.Min(maxHeight.Value, bitmap.Height) : bitmap.Height;

        // scanning saves about 50-60% of having to scan the entire image
        for (var i = 0; i < w; i++)
        {
            for (var j = 0; j < h; j++)
            {
                var c = bitmap.GetPixel(i, j);
                if ((c.ToArgb() | A) != -1)
                {
                    //(c.R != 255 || c.G != 255 || c.B != 255) {
                    left = i;
                    break;
                }
            }

            if (left >= 0) break;
        }

        if (left == -1)
            return Rectangle.Empty;

        for (var i = w - 1; i >= 0; i--)
        {
            for (var j = 0; j < h; j++)
            {
                var c = bitmap.GetPixel(i, j);
                if ((c.ToArgb() | A) != -1)
                {
                    //(c.R != 255 || c.G != 255 || c.B != 255) {
                    right = i;
                    break;
                }
            }

            if (right >= 0) break;
        }

        for (var j = 0; j < h; j++)
        {
            for (var i = left; i <= right; i++)
            {
                var c = bitmap.GetPixel(i, j);
                if ((c.ToArgb() | A) != -1)
                {
                    //(c.R != 255 || c.G != 255 || c.B != 255) {
                    top = j;
                    break;
                }
            }

            if (top >= 0) break;
        }

        for (var j = h - 1; j >= 0; j--)
        {
            for (var i = left; i <= right; i++)
            {
                var c = bitmap.GetPixel(i, j);
                if ((c.ToArgb() | A) != -1)
                {
                    //(c.R != 255 || c.G != 255 || c.B != 255) {
                    bottom = j;
                    break;
                }
            }

            if (bottom >= 0) break;
        }

        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    //public static void SpeedTest24vsDefault() {
    //	DateTime utcNow = DateTime.UtcNow;
    //	using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) {
    //		for (int i = 0; i < 30000; i++) {
    //			Measure("a" + i, g, SystemFonts.MenuFont, DrawMethod.Graphics, TextFormatFlags.Default, null, StringFormat2.GenericDefault, false);
    //		}
    //	}
    //	double seconds = (DateTime.UtcNow - utcNow).TotalSeconds;
    //	int bp = 1;
    //}

    // tests that the fast way produces the same bounding rectangle as the slow way for each color channel
    public static void UnitTest()
    {
        var h_arr = new[]
        {
            TextRenderingHint.AntiAlias, TextRenderingHint.AntiAliasGridFit, TextRenderingHint.ClearTypeGridFit,
            TextRenderingHint.SingleBitPerPixel, TextRenderingHint.SingleBitPerPixelGridFit
        };
        var pf_arr = new[]
        {
            PixelFormat.Format24bppRgb, PixelFormat.Format32bppArgb, PixelFormat.Format32bppPArgb,
            PixelFormat.Format32bppRgb
        };
        StringFormat[] sf_arr = { StringFormat.GenericDefault, StringFormat.GenericTypographic };

        var sb = new StringBuilder();
        sb.AppendLine(string.Join("\t", "StringFormat", "Hint", "PixelFormat", "BitsPerPixel", "Alpha", "Color", "X",
            "Y", "W", "H", "FileName"));

        for (var j = 0; j <= 1; j++)
        {
            var sf = sf_arr[j];
            var sf_name = j == 0 ? "default" : "typo";
            foreach (var hint in h_arr)
            foreach (var pf in pf_arr)
            foreach (var alpha in new[] { 128, 255 })
            {
                var brushes = new Brush[]
                {
                    new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)), new SolidBrush(Color.FromArgb(alpha, 255, 0, 0)),
                    new SolidBrush(Color.FromArgb(alpha, 0, 255, 0)), new SolidBrush(Color.FromArgb(alpha, 0, 0, 255))
                };
                for (var i = 0; i < brushes.Length; i++)
                {
                    var b = brushes[i];
                    using (var bmp = new Bitmap(150, 150, pf))
                    {
                        using (var f = new Font("Segoe UI Symbol", 20f))
                        {
                            using (var g = Graphics.FromImage(bmp))
                            {
                                var text = "\uE216"; // "\uE202";
                                g.TextRenderingHint = hint;
                                g.Clear(Color.White);
                                g.DrawString(text, f, b, 50, 50, sf);
                                g.DrawString("A", f, b, 50, 50, sf);

                                //r = Opulos.Core.UI.MeasureString.Measure(text, g, f, Opulos.Core.UI.DrawMethod.Graphics, stringFormat:sf);
                                //r2 = Opulos.Core.UI.MeasureString.MeasureFast(text, g, f, sf);
                                //sb.AppendLine(r.ToString());
                                //var r3 = Opulos.Core.UI.MeasureString.Measure(text, g, f, Opulos.Core.UI.DrawMethod.Graphics, stringFormat:sf);
                                var color = ((SolidBrush)b).Color;
                                string cn = null;
                                if (color.R == 255)
                                    cn = "red";
                                else if (color.G == 255)
                                    cn = "green";
                                else if (color.B == 255)
                                    cn = "blue";
                                else
                                    cn = "black";

                                var name = sf_name + "_" + alpha + "_" + hint + "_" + bmp.PixelFormat + "_" + cn;

                                //bmp.Save("c:\\temp\\aa11\\{0}.png".Format2(name));
                                var r = Scan(bmp); // MeasureString.Scan(bmp);
                                var rb = Scan_old(bmp);
                                if (r.X != rb.X || r.Y != rb.Y || r.Width != rb.Width || r.Height != rb.Height)
                                    throw new Exception();

                                var info = string.Join("\t", sf_name, hint, pf, Image.GetPixelFormatSize(pf), alpha, cn,
                                    r.X, r.Y, r.Width, r.Height, name);
                                sb.AppendLine(info);
                            }
                        }
                    }

                    b.Dispose();
                }
            }
        }
    }

    public static Color GetPixel(byte[] pixels, int stride, PixelFormat pf, int x, int y)
    {
        var bytesPerPixel = Image.GetPixelFormatSize(pf) / 8;

        var index = y * stride + x * bytesPerPixel;
        byte A = 255;
        byte B = 0;
        byte G = 0;
        byte R = 0;
        if (pf == PixelFormat.Format32bppArgb)
        {
            B = pixels[index + 0];
            G = pixels[index + 1];
            R = pixels[index + 2];
            A = pixels[index + 3];
        }
        else if (pf == PixelFormat.Format32bppPArgb)
        {
            A = pixels[index + 3];
            if (A != 0)
            {
                var a = 255.0 / A;
                B = (byte)(a * pixels[index + 0]);
                G = (byte)(a * pixels[index + 1]);
                R = (byte)(a * pixels[index + 2]);
            }
        }
        else if (pf == PixelFormat.Format24bppRgb || pf == PixelFormat.Format32bppRgb)
        {
            // for Format32bppRgb, Alpha stays as 255 (but the actual value in the array may be different, not sure what it is)
            B = pixels[index + 0];
            G = pixels[index + 1];
            R = pixels[index + 2];
        }
        else
        {
            return Color.Empty;
        }

        return Color.FromArgb(A, R, G, B);
    }

/*
        // this code is used to test the logic of the GetPixel(...) method:
        // The result of calling Bitmap.GetPixel(x, y) should be the same as calling GetPixel(pixels, stride, pf, x, y);

        StringBuilder sb = new StringBuilder();
        foreach (PixelFormat pf in new [] { PixelFormat.Format24bppRgb, PixelFormat.Format32bppArgb, PixelFormat.Format32bppPArgb, PixelFormat.Format32bppRgb }) {
            using (Bitmap bmp = new Bitmap(100, 100, pf)) {
                using (Graphics g = Graphics.FromImage(bmp)) {
                    //g.Clear(Color.White); // this makes Alpha always equal 255
                    using (Pen penYellow = new Pen(Color.FromArgb(128, Color.Yellow), 20)) {
                    using (Pen penGreen = new Pen(Color.FromArgb(68, Color.Green), 20)) {
                        g.DrawLine(penYellow, 40, 0, 40, 100);
                        g.DrawLine(penGreen, 0, 40, 100, 40);

                        int stride = 0;
                        byte[] pixels = MeasureString.GetPixelArray(bmp, out stride);

                        Color c = bmp.GetPixel(40, 40);
                        Color c2 = MeasureString.GetPixel(pixels, stride, pf, 40, 40);
                        sb.AppendLine("PixelFormat: " + pf);
                        sb.AppendLine(c.ToString() + " c");
                        sb.AppendLine(c2.ToString() + " c2");
                        sb.AppendLine(ColorUtil.Blend(c2, Color.White, c2.A / 255.0) + " color util");
                        sb.AppendLine();
                        //bmp.Save("c:\\temp\\pcross_" + pf + ".png");
                    }}
                }
            }
        }

*/

    public static void UnitTest2()
    {
        var flags = TextFormatFlags.Top | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding;
        using (var g = Graphics.FromHwnd(IntPtr.Zero))
        {
            for (var fs = 82; fs < 230; fs += 2)
            {
                var fontSize = 1f * fs / 10;
                Debug.WriteLine("" + fontSize);
                using (var f = new Font("Segoe UI Symbol", fontSize, FontStyle.Regular, GraphicsUnit.Point))
                {
                    for (var i = 0; i < 60; i++)
                    {
                        var t0 = "" + i;
                        var t1 = "0" + i;
                        foreach (var text in new[] { t0, t1 })
                        {
                            var ps = TextRenderer.MeasureText(g, text, f, Size.Empty, flags);
                            var r1 = MeasureString_old.Measure(text, g, f, DrawMethod.TextRenderer, flags,
                                new Rectangle(0, 0, ps.Width, ps.Height));
                            var r2 = Measure(text, g, f, DrawMethod.TextRenderer, flags,
                                new Rectangle(0, 0, ps.Width, ps.Height), increaseSize: false, cache: true);
                            if (!r1.Equals(r2))
                                throw new Exception("Text: " + text + " font size: " + f.Size + " did not match.");
                        }
                    }
                }
            }
        }
    }
}

internal static class MeasureString_old
{
    private static readonly Hashtable ht = new();

    // returns a rectangle because it's possible that the top,left are outside of the 0,0 requested position.
    public static Rectangle Measure(string text, Graphics graphics, Font font,
        DrawMethod drawMethod = DrawMethod.Graphics, TextFormatFlags textFormatFlags = TextFormatFlags.Default,
        Rectangle? rect = null)
    {
        if (string.IsNullOrEmpty(text))
            return Rectangle.Empty;


        var mk = new MultiKey(text, graphics.TextRenderingHint, font, drawMethod, textFormatFlags, rect);
        //if (drawMethod == DrawMethod.Graphics)
        //	mk = new MultiKey(text, graphics.TextRenderingHint, font, drawMethod);
        //else {
        //	mk = new 

        var o = ht[mk];
        if (o != null)
            return (Rectangle)o;

        var size = Size.Empty;
        if (rect.HasValue)
        {
            var r2 = rect.Value;
            size = r2.Size;
            r2.Location = Point.Empty;
            rect = r2;
        }
        else
        {
            size = drawMethod == DrawMethod.Graphics
                ? graphics.MeasureString(text, font).ToSize()
                : TextRenderer.MeasureText(graphics, text, font, Size.Empty, textFormatFlags);
        }

        var w = size.Width;
        var h = size.Height;
        if (w == 0 || h == 0) return Rectangle.Empty;
        var bitmap = new Bitmap(w, h, graphics);

        var g2 = Graphics.FromImage(bitmap);
        g2.TextRenderingHint = graphics.TextRenderingHint;
        g2.SmoothingMode = graphics.SmoothingMode;
        g2.Clear(Color.White);
        if (drawMethod == DrawMethod.Graphics)
        {
            g2.DrawString(text, font, Brushes.Black, 0, 0);
        }
        else
        {
            // always specify a bounding rectangle. Otherwise if flags contains VerticalCenter it will be half cutoff above point (0,0)
            var r2 = rect.HasValue ? rect.Value : new Rectangle(Point.Empty, size);
            TextRenderer.DrawText(g2, text, font, r2, Color.Black, Color.White, textFormatFlags);
        }

        int left, right, top, bottom;
        left = right = top = bottom = -1;

        // scanning saves about 50-60% of having to scan the entire image

        for (var i = 0; i < w; i++)
        {
            for (var j = 0; j < h; j++)
            {
                var c = bitmap.GetPixel(i, j);
                if (c.R != 255 || c.G != 255 || c.B != 255)
                {
                    left = i;
                    break;
                }
            }

            if (left >= 0) break;
        }

        if (left == -1)
            return Rectangle.Empty;

        for (var i = w - 1; i >= 0; i--)
        {
            for (var j = 0; j < h; j++)
            {
                var c = bitmap.GetPixel(i, j);
                if (c.R != 255 || c.G != 255 || c.B != 255)
                {
                    right = i;
                    break;
                }
            }

            if (right >= 0) break;
        }

        for (var j = 0; j < h; j++)
        {
            for (var i = left; i <= right; i++)
            {
                var c = bitmap.GetPixel(i, j);
                if (c.R != 255 || c.G != 255 || c.B != 255)
                {
                    top = j;
                    break;
                }
            }

            if (top >= 0) break;
        }

        for (var j = h - 1; j >= 0; j--)
        {
            for (var i = left; i <= right; i++)
            {
                var c = bitmap.GetPixel(i, j);
                if (c.R != 255 || c.G != 255 || c.B != 255)
                {
                    bottom = j;
                    break;
                }
            }

            if (bottom >= 0) break;
        }

        g2.Dispose();
        bitmap.Dispose();

        var r = new Rectangle(left, top, right - left + 1, bottom - top + 1);
        ht[mk] = r;
        return r;
    }
}