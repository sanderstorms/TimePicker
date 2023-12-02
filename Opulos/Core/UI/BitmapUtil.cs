using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace Opulos.Core.UI {

public static class PerformanceOptions {

	// this should be a User Setting with the default value true. Tri-state checkbox, true/false/null.
	public static bool? GlobalAntiAlias = true;

	private static bool smoothEdgesOfScreenFonts = false;
	private static bool dragFullWindows = false;
	private static bool eventAdded = false;

	static PerformanceOptions() {
		try {
			smoothEdgesOfScreenFonts = GetFontSmoothing();
			dragFullWindows = GetDragFullWindows();
			Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
			eventAdded = true;
		} catch {}
	}

	private static void SystemEvents_UserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e) {
		if (e.Category == Microsoft.Win32.UserPreferenceCategory.Desktop) {	
			smoothEdgesOfScreenFonts = GetFontSmoothing(); // aka SystemInformation.IsFontSmoothingEnabled
		}
		else if (e.Category == Microsoft.Win32.UserPreferenceCategory.Window) {	
			dragFullWindows = GetDragFullWindows(); // aka SystemInformation.DragFullWindows
		}
	}

	public static bool DragFullWindows {
		get {
			if (eventAdded)
				return dragFullWindows;

			return SystemInformation.DragFullWindows;
		}
	}

	public static bool SmoothEdgesOfScreenFonts {
		get {
			if (GlobalAntiAlias.HasValue)
				return GlobalAntiAlias.Value;

			if (eventAdded)
				return smoothEdgesOfScreenFonts;

			return SystemInformation.IsFontSmoothingEnabled;
		}
	}

	private static bool GetFontSmoothing() {
		bool result = false;
		SystemParametersInfo(SPI_GETFONTSMOOTHING, 0, ref result, 0);
		return result;
	}

	private static bool GetDragFullWindows() {
		bool result = false;
		SystemParametersInfo(SPI_GETDRAGFULLWINDOWS, 0, ref result, 0);
		return result;
	}

	private const uint SPI_GETDRAGFULLWINDOWS = 0x0026; //38;
	private const uint SPI_GETFONTSMOOTHING = 0x004A;
	[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
	private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

	// Another possible way to listen for the "Smooth edges of screen fonts" option changing would be to listen for the Windows Message:
	//	private const int WM_SETTINGCHANGE = 0x1A;
	//	private const int SPI_SETFONTSMOOTHING = 0x004B;
	//	protected override void WndProc(ref Message m) {
	//		if (m.Msg == WM_SETTINGCHANGE) {
	//			IntPtr uiAction = m.WParam;
	//			if (m.WParam.ToInt32() == SPI_SETFONTSMOOTHING) {
	//				enabled = GetEnabledInternal();
	//			}
	//		}
	//		base.WndProc(ref m);
	//	}
}

public static class BitmapUtil {

	public static TextRenderingHint AntiAliasHintOn = TextRenderingHint.AntiAlias;
	public static TextRenderingHint AntiAliasHintOff = TextRenderingHint.SingleBitPerPixelGridFit;

	// todo: the issue with this method is it doesn't account for glyphs that draw outside of their default bounds.
	// See Opulos.Core.Drawing.MeasureString
	public static Bitmap CreateIcon(String text, Font font, Color? foreColor = null, Color? backColor = null, DrawMethod drawMethod = DrawMethod.Graphics, bool? antialias = null, RotateFlipType rotateFlipType = RotateFlipType.RotateNoneFlipNone, float rotateDegrees = 0, int minWidth = 0, int minHeight = 0) {
		if (!antialias.HasValue)
			antialias = PerformanceOptions.GlobalAntiAlias;

		bool b = (antialias.HasValue ? antialias.Value : PerformanceOptions.SmoothEdgesOfScreenFonts);
		TextRenderingHint hint = (b ? AntiAliasHintOn : AntiAliasHintOff);
		return CreateIcon2(text, font, foreColor, backColor, drawMethod, hint, rotateFlipType, rotateDegrees, minWidth, minHeight);
	}

	///<summary>Allows the TextRenderingHint to be set explicitly.</summary>
	public static Bitmap CreateIcon2(String text, Font font, Color? foreColor = null, Color? backColor = null, DrawMethod drawMethod = DrawMethod.Graphics, TextRenderingHint hint = TextRenderingHint.AntiAlias, RotateFlipType rotateFlipType = RotateFlipType.RotateNoneFlipNone, float rotateDegrees = 0, int minWidth = 0, int minHeight = 0) {
		// returns a rectangle because it's possible that the top,left are outside of the 0,0 requested position.
		if (String.IsNullOrWhiteSpace(text)) {
			return new Bitmap(Math.Max(1, minWidth), Math.Max(1, minHeight));
		}

		if (foreColor == null)
			foreColor = Color.Black;

		// Smothing only applies to drawing lines, polygons, etc, so it shouldn't make any difference.
		//SmoothingMode mode = SmoothingMode.Default;

		Size size = Size.Empty;
		if (drawMethod == DrawMethod.Graphics) {
			using (Bitmap bm = new Bitmap(1,1)) {
				Graphics g0 = Graphics.FromImage(bm);
				g0.TextRenderingHint = hint;
				//g0.SmoothingMode = mode;
				size = Size.Ceiling(g0.MeasureString(text, font));
			}
		}
		else {
			size = Size.Ceiling(TextRenderer.MeasureText(text, font));
		}

		int w = size.Width;
		int h = size.Height;
		if (w == 0 || h == 0)
			return new Bitmap(Math.Max(1, minWidth), Math.Max(1, minHeight));

		Bitmap bitmap = new Bitmap(w, h);
		Graphics g2 = Graphics.FromImage(bitmap);
		g2.TextRenderingHint = hint;
		//g2.SmoothingMode = mode;

		if (rotateDegrees != 0) {
			// Set the rotation point to the center in the matrix
			g2.TranslateTransform(w / 2, h / 2);
			g2.RotateTransform(rotateDegrees);
			g2.TranslateTransform(-w / 2, -h / 2);
		}

		Color bg = Color.Empty;
		if (drawMethod == DrawMethod.Graphics) {
			if (backColor.HasValue) {
				bg = backColor.Value;
				g2.Clear(bg); // for SnapButton inside ComboBox
			}

			using (var b = new SolidBrush(foreColor.Value))
				g2.DrawString(text, font, b, 0, 0);
		}
		else {
			bg = (backColor.HasValue ? backColor.Value : Color.White);
			TextRenderer.DrawText(g2, text, font, new Point(0, 0), foreColor.Value, bg);
		}
		int bgA = bg.A;
		int bgR = bg.R;
		int bgG = bg.G;
		int bgB = bg.B;

		int left, right, top, bottom;
		left = right = top = bottom = -1;

		// scanning saves about 50-60% of having to scan the entire image

		for (int i = 0; i < w; i++) {
			for (int j = 0; j < h; j++) {
				Color c = bitmap.GetPixel(i, j);
				if (c.A != bgA || c.R != bgR || c.G != bgG || c.B != bgB) {
					left = i;
					break;
				}
			}
			if (left >= 0) break;
		}

		if (left == -1)
			return new Bitmap(1, 1);

		for (int i = w - 1; i >= 0; i--) {
			for (int j = 0; j < h; j++) {
				Color c = bitmap.GetPixel(i, j);
				if (c.A != bgA || c.R != bgR || c.G != bgG || c.B != bgB) {
					right = i;
					break;
				}
			}
			if (right >= 0) break;
		}

		for (int j = 0; j < h; j++) {
			for (int i = left; i <= right; i++) {
				Color c = bitmap.GetPixel(i, j);
				if (c.A != bgA || c.R != bgR || c.G != bgG || c.B != bgB) {
					top = j;
					break;
				}
			}
			if (top >= 0) break;
		}

		for (int j = h - 1; j >= 0; j--) {
			for (int i = left; i <= right; i++) {
				Color c = bitmap.GetPixel(i, j);
				if (c.A != bgA || c.R != bgR || c.G != bgG || c.B != bgB) {
					bottom = j;
					break;
				}
			}
			if (bottom >= 0) break;
		}

		int x2 = left;
		int y2 = top;
		int w2 = (right - left) + 1;
		int h2 = (bottom - top) + 1;

		// Note: The Rectangle must completely intersect with the image bounds [0, 0, w, h]. Otherwise
		// the bitmap.Clone(...) method throws an OutOfMemoryException. So it's not possible to specify
		// padding in the rectangle.
		var r = new Rectangle(x2, y2, w2, h2);
		Bitmap b2 = bitmap.Clone(r, bitmap.PixelFormat);
		bitmap.Dispose();
		g2.Dispose();
		b2.RotateFlip(rotateFlipType);

		if (w2 < minWidth || h2 < minHeight) {
			// a new bitmap must be created if the image does not meet the desired minimum dimensions.
			int dw = minWidth - w2;
			int dh = minHeight - h2;
			if (dw < 0) dw = 0;
			if (dh < 0) dh = 0;
			Bitmap b3 = new Bitmap(w2 + dw, h2 + dh);
			using (var g3 = Graphics.FromImage(b3)) {
				if (backColor.HasValue)
					g3.Clear(backColor.Value);

				g3.DrawImageUnscaled(b2, dw / 2, dh / 2);
			}
			b2.Dispose();
			b2 = b3;
		}
		return b2;
	}
}
}