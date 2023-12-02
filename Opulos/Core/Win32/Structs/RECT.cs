using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Opulos.Core.Win32 {

public enum X : int {}
public enum Y : int {}
public enum Width : int {}
public enum Height : int {}

[StructLayout(LayoutKind.Sequential)]
public struct RECT {
	public int Left, Top, Right, Bottom;

	public RECT(System.Drawing.Rectangle r) : this(r.Left, r.Top, r.Right, r.Bottom) {}

	public RECT(X x, Y y, Width width, Height height) {
		this.Left = (int) x;
		this.Top = (int) y;
		this.Right = (int) x + (int) width;
		this.Bottom = (int) y + (int) height;
	}

	public RECT(int left, int top, int right, int bottom) {
		Left = left;
		Top = top;
		Right = right;
		Bottom = bottom;
	}

	public int X {
		get { return Left; }
		set { Right -= (Left - value); Left = value; }
	}

	public int Y {
		get { return Top; }
		set { Bottom -= (Top - value); Top = value; }
	}

	public int Height {
		get { return Bottom - Top; }
		set { Bottom = value + Top; }
	}

	public int Width {
		get { return Right - Left; }
		set { Right = value + Left; }
	}

	public System.Drawing.Point Location {
		get { return new System.Drawing.Point(Left, Top); }
		set { X = value.X; Y = value.Y; }
	}

	public System.Drawing.Size Size {
		get { return new System.Drawing.Size(Width, Height); }
		set { Width = value.Width; Height = value.Height; }
	}

	public static implicit operator System.Drawing.Rectangle(RECT r) {
		return new System.Drawing.Rectangle(r.Left, r.Top, r.Width, r.Height);
	}

	public static implicit operator RECT(System.Drawing.Rectangle r) {
		return new RECT(r);
	}

	public static bool operator ==(RECT r1, RECT r2) {
		return r1.Equals(r2);
	}

	public static bool operator !=(RECT r1, RECT r2) {
		return !r1.Equals(r2);
	}

	public bool Equals(RECT r) {
		return r.Left == Left && r.Top == Top && r.Right == Right && r.Bottom == Bottom;
	}

	public override bool Equals(object obj) {
		if (obj is RECT)
			return Equals((RECT) obj);
		if (obj is System.Drawing.Rectangle)
			return Equals(new RECT((System.Drawing.Rectangle) obj));
		return false;
	}

	public override int GetHashCode() {
		unchecked { // Overflow is fine, just wrap
			int hash = 17;
			hash = hash * 29 + Left;
			hash = hash * 29 + Top;
			hash = hash * 29 + Right;
			hash = hash * 29 + Bottom;
			return hash;
		}
	}

	public override String ToString() {
		return string.Format(System.Globalization.CultureInfo.CurrentCulture, "{{X={0}, Y={1}, W={2}, H={3}}}", X, Y, Width, Height);
	}
}

public static partial class User32 {

	public static RECT GetWindowRect(IntPtr hwnd) {
		RECT r = new RECT();
		GetWindowRect(hwnd, ref r);
		return r;
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool IsIconic(IntPtr hWnd); // IsIconic is a simple approach than GetPlacement(IntPtr);

	[DllImport("user32.dll", SetLastError=true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetWindowRect(IntPtr hwnd, ref RECT lpRect);

	[DllImport("user32.dll", SetLastError=true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetClientRect(IntPtr hwnd, ref RECT lpRect);
}

}
