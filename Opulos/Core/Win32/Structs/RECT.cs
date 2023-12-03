using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;

namespace TimePicker.Opulos.Core.Win32.Structs;

public enum X
{
}

public enum Y
{
}

public enum Width
{
}

public enum Height
{
}

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left, Top, Right, Bottom;

    public RECT(Rectangle r) : this(r.Left, r.Top, r.Right, r.Bottom)
    {
    }

    public RECT(X x, Y y, Width width, Height height)
    {
        Left = (int)x;
        Top = (int)y;
        Right = (int)x + (int)width;
        Bottom = (int)y + (int)height;
    }

    public RECT(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public int X
    {
        get => Left;
        set
        {
            Right -= Left - value;
            Left = value;
        }
    }

    public int Y
    {
        get => Top;
        set
        {
            Bottom -= Top - value;
            Top = value;
        }
    }

    public int Height
    {
        get => Bottom - Top;
        set => Bottom = value + Top;
    }

    public int Width
    {
        get => Right - Left;
        set => Right = value + Left;
    }

    public Point Location
    {
        get => new(Left, Top);
        set
        {
            X = value.X;
            Y = value.Y;
        }
    }

    public Size Size
    {
        get => new(Width, Height);
        set
        {
            Width = value.Width;
            Height = value.Height;
        }
    }

    public static implicit operator Rectangle(RECT r)
    {
        return new Rectangle(r.Left, r.Top, r.Width, r.Height);
    }

    public static implicit operator RECT(Rectangle r)
    {
        return new RECT(r);
    }

    public static bool operator ==(RECT r1, RECT r2)
    {
        return r1.Equals(r2);
    }

    public static bool operator !=(RECT r1, RECT r2)
    {
        return !r1.Equals(r2);
    }

    public bool Equals(RECT r)
    {
        return r.Left == Left && r.Top == Top && r.Right == Right && r.Bottom == Bottom;
    }

    public override bool Equals(object obj)
    {
        if (obj is RECT)
            return Equals((RECT)obj);
        if (obj is Rectangle)
            return Equals(new RECT((Rectangle)obj));
        return false;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            // Overflow is fine, just wrap
            var hash = 17;
            hash = hash * 29 + Left;
            hash = hash * 29 + Top;
            hash = hash * 29 + Right;
            hash = hash * 29 + Bottom;
            return hash;
        }
    }

    public override string ToString()
    {
        return string.Format(CultureInfo.CurrentCulture, "{{X={0}, Y={1}, W={2}, H={3}}}", X, Y, Width, Height);
    }
}

public static class User32
{
    public static RECT GetWindowRect(IntPtr hwnd)
    {
        var r = new RECT();
        GetWindowRect(hwnd, ref r);
        return r;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd); // IsIconic is a simple approach than GetPlacement(IntPtr);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hwnd, ref RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr hwnd, ref RECT lpRect);
}