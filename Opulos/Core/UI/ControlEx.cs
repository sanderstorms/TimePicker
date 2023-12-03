using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TimePicker.Opulos.Core.Utils;

namespace TimePicker.Opulos.Core.UI;

public static class ControlEx
{
    private const int WM_SETREDRAW = 0x0b;

    /// <summary>
    ///     The active control is the control that would receive the keyboard input if its
    ///     top level window was active. The control input parameter should extend ContainerControl,
    ///     typically Form or SplitContainer.
    /// </summary>
    public static Control FindActiveControl(this Control control)
    {
        if (control == null)
            return null;

        var fc = FindFocusedControl(control);
        if (fc != null)
            return fc;

        var container = control as ContainerControl;
        while (container != null)
        {
            control = container.ActiveControl;
            container = control as ContainerControl;
        }

        if (control is TabControl)
            control = ((TabControl)control).SelectedTab;

        return control;
    }

    private static Control FindFocusedControl(Control control)
    {
        var s = new Stack();
        s.Push(control);
        while (s.Count > 0)
        {
            var c = (Control)s.Pop();

            foreach (Control c2 in c.Controls)
            {
                if (c2.Focused)
                    return c2;

                if (c2.Controls.Count > 0)
                    s.Push(c2);
            }
        }

        return null;
    }

    /*
    Two different ways to invoke:

    1.	control.BeginInvokeSafe((Action<Object>) delegate(Object o) {
            //...
        }, new Object[] { ... });


    2.	control.BeginInvoke(new Action<Object>(o => {
            //...
        }), (Object) (new Object[] { ... }));
    */
    public static void BeginInvokeSafe(this Control control, Delegate method, object[] args = null,
        bool throwException = false)
    {
        var c = control;
        while (c != null)
        {
            if (c.IsHandleCreated && !c.IsDisposed)
                break;
            c = c.Parent;
        }

        if (c == null)
            foreach (Form f in Application.OpenForms)
                if (f.IsHandleCreated && !f.IsDisposed)
                {
                    c = f;
                    break;
                }

        if (c == null)
            try
            {
                using (var p = Process.GetCurrentProcess())
                {
                    var mainForm = Control.FromHandle(p.MainWindowHandle);
                    if (mainForm != null && mainForm.IsHandleCreated && !mainForm.IsDisposed)
                        c = mainForm;
                }
            }
            catch
            {
            }

        if (c == null)
            throw new Exception("Cannot find valid control to perform BeginInvoke.");

        try
        {
            c.BeginInvoke(method, args);
        }
        catch (Exception ex)
        {
            if (throwException)
                ex.Rethrow();
        }
    }

    /// <summary>
    ///     Returns the Parent control that doesn't have a parent itself. This is typically a Form, however some
    ///     controls hosted in a ToolStripControlHost, or WindowsFormsHost (when mixing and matching WPF) will not have a
    ///     Form as the top-most parent. If the control has no immediate parent then null is returned.
    /// </summary>
    /*public static Control TopMostParent(this Control c) {
        if (c == null)
            return c;
        Control p = c.Parent;
        while (p != null) {
            Control p2 = p.Parent;
            if (p2 == null || p == p2)
                break;
            p = p2;
        }
        return p;
    }*/
    private static List<T> FindControls<T>(this Control c)
    {
        var ty = typeof(T);
        var list = new List<T>();
        var s = new Stack();
        s.Push(c);
        while (s.Count > 0)
        {
            c = (Control)s.Pop();
            foreach (Control c2 in c.Controls)
            {
                var ty2 = c2.GetType();
                if (ty.IsAssignableFrom(ty2))
                {
                    var t = (T)(object)c2;
                    list.Add(t);
                }

                if (c2.Controls.Count > 0)
                    s.Push(c2);
            }
        }

        return list;
    }

    /// <summary>
    ///     The problem with CreateGraphics() is that it has the unexpected side-effect of creating a Control's handle.
    ///     This is problematic when a LayoutEngine is calling the GetPreferredSize(Size proposedSize) method, as the handles
    ///     could be created in different orders, which causes the order of the control itself within their parent's Control
    ///     collection changes. Many layout engines, like FlowLayoutPanel, use the order of the controls in the collection
    ///     to determine each control's relative placement. A layout engine like TableLayoutPanel may not suffer from this
    ///     as the user explicitly has to specify the row and column location of each control.
    /// </summary>
    public static Graphics CreateGraphicsSafe(this Control c)
    {
        while (c != null)
        {
            if (c.IsHandleCreated && !c.IsDisposed)
                return c.CreateGraphics();
            c = c.Parent;
        }

        var forms = Application.OpenForms;
        if (forms != null)
            foreach (Form f in forms)
                if (f.IsHandleCreated && !f.IsDisposed)
                    return f.CreateGraphics();
        return Graphics.FromHwnd(IntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

    public static void SuspendDrawing(this Control control)
    {
        SendMessage(control.Handle, WM_SETREDRAW, (IntPtr)0, IntPtr.Zero);
    }

    public static void ResumeDrawing(this Control control)
    {
        SendMessage(control.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
        control.Invalidate();
    }

    public static bool IsRightToLeft(this Control c)
    {
        while (c != null)
        {
            var rtl = c.RightToLeft;
            if (rtl == RightToLeft.Yes)
                return true;
            if (rtl == RightToLeft.No)
                return false;
            c = c.Parent;
        }

        var fc = Application.OpenForms;
        if (fc != null && fc.Count > 0)
            return fc[0].RightToLeft == RightToLeft.Yes;
        return false;
    }

    // before allowing a click, make sure the target control can receive the focus, and that other controls don't require validation
    public static bool CanPerformClick(this Control target)
    {
        if (!target.CanSelect)
            return false;

        var c = target.Parent;
        while (c != null)
        {
            if (c is ContainerControl)
                break;
            c = c.Parent;
        }

        var valid = true;
        if (c is ContainerControl)
        {
            var cc = (ContainerControl)c;
            valid = cc.Validate(true);
        }

        return valid;
    }

    private delegate void WorkerThreadStartDelegate2(object argument);
}