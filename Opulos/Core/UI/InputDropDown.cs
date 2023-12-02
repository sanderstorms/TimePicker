using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Layout;

//using Opulos.Core.Localization;

namespace Opulos.Core.UI;

public class MultiInputComboBox : ComboBox
{
    //private String dateTimeFormat = null;

    public MultiInputDropDown InputControl;

    public MultiInputComboBox(IList inputs, IList<string> toolTips)
    {
        InputControl = new MultiInputDropDown(inputs, toolTips);

        Attacher = new ToolStripDropDownAttacher(InputControl, this,
            false); // { FontSizeDelta = -1.4f }; //-3 }; // changed 2016-01-25
        Attacher.AutoCloseReasonWindowLostFocus = null;
        Attacher.ClickOpensMenu = false;
        //attacher.EscapeCloseReason = null;

        /*attacher.MenuShowing += delegate {
            Token t = this.TokenAt(this.SelectionStart);
            if (t.SeqNo == 0)
                ClockMenu.ClockFace = ClockFace.Hours;
            else if (t.SeqNo == 2)
                ClockMenu.ClockFace = ClockFace.Minutes;
        };*/

        var flag = false;
        GotFocus += delegate { flag = true; };
        LostFocus += delegate { flag = false; };

        MouseDown += delegate
        {
            if (flag)
            {
                // if the clock just opened from a GotFocus then do not close it
                flag = false;
                Attacher.ShowMenu();
                return;
            }

            if (!InputControl.Visible)
                Attacher.ShowMenu();
            else
                //Token t = this.TokenAt(this.SelectionStart);
                //if (t == PreviousSelectedToken && (t.SeqNo == 0 || t.SeqNo == 2))
                Attacher.CloseMenu(ToolStripDropDownCloseReason.AppClicked);
            //else
            //	attacher.ShowMenu();
        };
    }

    public ToolStripDropDownAttacher Attacher { get; }
}

///<summary>A drop down user interface that maps constants to text values.</summary>
public class MultiInputDropDown : ToolStripDropDown
{
    private readonly List<ToolStripLabel2> labels = new();
    private ContentAlignment labelTextAlignment = ContentAlignment.MiddleLeft;
    private readonly InputLayoutEngine layoutEngine = new();
    private readonly List<ToolStripTextBox2> textBoxes = new();

    public MultiInputDropDown(IList inputs, IList<string> toolTips)
    {
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var toolTip = toolTips != null && i < toolTips.Count ? toolTips[i] : "";
            var lb = new ToolStripLabel2(input.ToString()) { TextAlign = ContentAlignment.MiddleLeft };
            var tb = new ToolStripTextBox2 { ToolTipText = toolTip, AutoToolTip = false };
            labels.Add(lb);
            textBoxes.Add(tb);
            Items.Add(lb);
            Items.Add(tb);
        }
    }

    public ContentAlignment LabelTextAlignment
    {
        get => labelTextAlignment;
        set
        {
            labelTextAlignment = value;
            foreach (var lb in labels) lb.TextAlign = value;
        }
    }

    public override LayoutEngine LayoutEngine => layoutEngine;

    public virtual string[] GetValues()
    {
        var arr = new string[textBoxes.Count];
        for (var i = 0; i < arr.Length; i++)
        {
            var tb = textBoxes[i];
            arr[i] = tb.Text;
        }

        return arr;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var ps = layoutEngine.LayoutInternal(false, this);
        return ps;
    }

    private class ToolStripTextBox2 : ToolStripTextBox
    {
        public void SetBounds(Point p, Size s)
        {
            SetBounds(new Rectangle(p, s));
        }
    }

    private class ToolStripLabel2 : ToolStripLabel
    {
        public ToolStripLabel2(string text) : base(text)
        {
        }

        public void SetBounds(Point p, Size s)
        {
            SetBounds(new Rectangle(p, s));
        }
    }

    private class InputLayoutEngine : LayoutEngine
    {
        public override void InitLayout(object child, BoundsSpecified specified)
        {
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            //var c = layoutEventArgs.AffectedControl;
            //sb.AppendLine((c == null ? "null" : c.GetType().FullName + "  " + layoutEventArgs.AffectedProperty));
            LayoutInternal(true, (MultiInputDropDown)container);
            return false;
        }

        //public int GetPreferredClockDiameter(ClockControl clock) {
        //	Size s = Size.Empty;
        //	foreach (var cni in clock.minuteItems) {
        //		Size ps = cni.PreferredSize;
        //		if (ps.Width > s.Width)
        //			s.Width = ps.Width;
        //		if (ps.Height > s.Height)
        //			s.Height = ps.Height;
        //	}
        //	foreach (var cni in clock.hourItems) {
        //		Size ps = cni.PreferredSize;
        //		if (ps.Width > s.Width)
        //			s.Width = ps.Width;
        //		if (ps.Height > s.Height)
        //			s.Height = ps.Height;
        //	}

        //	int r = Math.Max(s.Width, s.Height);
        //	double circumference = 12 * clock.ClockFaceSizeFactor * r;
        //	double diameter = circumference / Math.PI;
        //	int w = (int) Math.Ceiling(diameter);
        //	return w;
        //}

        public Size LayoutInternal(bool doLayout, MultiInputDropDown control)
        {
            var ps = Size.Empty;
            var p = control.Padding;
            var w1 = 0;
            var w2 = 0;
            var h = 0;
            var sz1 = new List<Size>();
            var sz2 = new List<Size>();
            for (var i = 0; i < control.labels.Count; i++)
            {
                var lb = control.labels[i];
                var tb = control.textBoxes[i];
                var szLabel = lb.GetPreferredSize(Size.Empty);
                var szTextBox = tb.GetPreferredSize(Size.Empty);
                if (szLabel.Width > w1)
                    w1 = szLabel.Width;
                if (szTextBox.Width > w2)
                    w2 = szTextBox.Width;
                h += Math.Max(szLabel.Height, szTextBox.Height);
                if (doLayout)
                {
                    sz1.Add(szLabel);
                    sz2.Add(szTextBox);
                }
            }

            if (doLayout)
            {
                var y = 0;
                for (var i = 0; i < control.labels.Count; i++)
                {
                    var lb = control.labels[i];
                    var tb = control.textBoxes[i];
                    var szLabel = sz1[i];
                    var szTextBox = sz2[i];
                    var maxH = Math.Max(szLabel.Height, szTextBox.Height);
                    var dh = 0; //Math.Max((szTextBox.Height - szLabel.Height) / 2, 0);
                    lb.SetBounds(new Point(0, y), new Size(w1 + dh, maxH));
                    tb.SetBounds(new Point(w1, y), new Size(w2, maxH));
                    y += maxH;
                }
            }

            return new Size(w1 + w2 + 10, h + 10);

            /*int width = 0;
            Size clockFaceSize = Size.Empty;
            if (doLayout) {
                clockFaceSize = clock.ClockFaceSize;
                width = clock.Width;
            }
            else {
                width = GetPreferredClockDiameter(clock);
                clockFaceSize = new Size(width, width);
                clock.ClockFaceSize = clockFaceSize;
                width += p.Horizontal;
            }

            Size szAM = clock.ClockItemAM.PreferredSize;
            Size szPM = clock.ClockItemPM.PreferredSize;

            Point ptAM = Point.Empty;
            if (clock.ClockItemAM.Owner != null)
                ptAM = GetLocation(Point.Empty, clockFaceSize, clock.ClockItemAMAngleDegrees, szAM, (int) (-clock.ClockItemAMDistanceFactor * szAM.Width));

            Point ptPM = Point.Empty;
            if (clock.ClockItemPM.Owner != null)
                ptPM = GetLocation(Point.Empty, clockFaceSize, clock.ClockItemPMAngleDegrees, szPM, (int) (-clock.ClockItemPMDistanceFactor * szPM.Width));
            int offsetX = 0;

            if (ptAM.X < -clockFaceSize.Width / 2) {
                offsetX = -(clockFaceSize.Width / 2) - ptAM.X;
                width = p.Horizontal + (ptPM.X + szPM.Width - ptAM.X);
            }

            ps.Width = width;
            int h2 = clock.GetHeaderHeight();
            int gap = clock.GetHeaderClockFaceGap();
            int ww = clockFaceSize.Width;
            clock.ClockFaceCenter = new Point(offsetX + p.Left + ww / 2, h2 + gap + ww / 2);

            if (doLayout) {
                var headers = new [] { clock.ClockItemHour, clock.ClockItemColon, clock.ClockItemMinute, clock.ClockItemAMPM };
                double[] alignY = new [] { 0.5, 1.0, 0.5, 1.0 };
                int gap0 = clock.hourItems[0].PreferredSize.Width / 3;
                int[] gaps = new [] { gap0, gap0, (int) (1.4 * gap0), 0 };

                int totalWidth = 0;
                for (int i = 0; i < headers.Length; i++) {
                    var h = headers[i];
                    var s = h.PreferredSize;
                    totalWidth += s.Width + gaps[i];
                }

                int m1 = clock.ClockItemHour.TextBounds.X;
                int m2 = clock.ClockItemAMPM.PreferredSize.Width - clock.ClockItemAMPM.TextBounds.Right;
                totalWidth -= (m1 + m2);
                int x = Math.Max(0, width - totalWidth) / 2 - m1;

                for (int i = 0; i < alignY.Length; i++) {
                    var h = headers[i];
                    int idealy = (int) Math.Ceiling(0.5 * (h2 - h.TextBounds.Height));
                    int dy = idealy - h.TextBounds.Y;
                    int dy2 = (int) Math.Ceiling((alignY[i] - 0.5) * (headers[0].TextBounds.Height - h.TextBounds.Height));
                    int y = p.Top + dy + dy2;
                    var s = h.PreferredSize;
                    h.SetBounds(new Point(x, y + 1), s);
                    x += s.Width + gaps[i];
                }

                var items = (clock.clockFace == ClockFace.Hours ? clock.hourItems : clock.minuteItems);
                foreach (var item in items) {
                    Size s = item.PreferredSize;
                    Point pt = GetLocation(clock, item);
                    item.SetBounds(pt, s);
                }
            }

            ptAM = GetLocation(clock.ClockFaceCenter, clockFaceSize, clock.ClockItemAMAngleDegrees, szAM, (int) (-clock.ClockItemAMDistanceFactor * szAM.Width));
            ptPM = GetLocation(clock.ClockFaceCenter, clockFaceSize, clock.ClockItemPMAngleDegrees, szPM, (int) (-clock.ClockItemPMDistanceFactor * szPM.Width));
            ptPM.X++; // pixel adjustment

            Padding mOK = clock.ClockButtonOK.Margin;
            Padding mCancel = clock.ClockButtonCancel.Margin;

            //var r = clock.miAM.Bounds;
            int y2a = 0;
            if (clock.ClockItemAM.Owner != null)
                y2a = ptAM.Y + (int) (clock.ButtonGapFactor * szAM.Height) + 1; // +1 otherwise the starting y-loc
            int y2b = 0;
            if (clock.ClockItemPM.Owner != null)
                y2b = ptPM.Y + (int) (clock.ButtonGapFactor * szPM.Height) + 1; // would be on the separator line

            int y2 = Math.Max(y2a, y2b);
            if (y2 == 0) {
                // use the same gap above the clock as below the clock when no AM button and no PM button are visible
                y2 = clock.ClockFaceCenter.Y + clockFaceSize.Height / 2 + gap;
            }

            if (doLayout) {
                if (clock.ClockItemAM.Owner != null)
                    clock.ClockItemAM.SetBounds(ptAM, szAM);
                if (clock.ClockItemPM.Owner != null)
                    clock.ClockItemPM.SetBounds(ptPM, szPM);
            }

            if (clock.ClockButtonOK.Owner != null || clock.ClockButtonCancel.Owner != null) {
                //---
                int wOK = (int) Math.Ceiling(clock.ClockButtonOKWidthFactor * width);
                int xOK = (int) ((width - wOK) * clock.ClockButtonOKAlignX) + mOK.Left;
                wOK = wOK - mOK.Horizontal;
                Point ptOK = new Point(xOK, y2 + mOK.Top);
                Size szOK = new Size(wOK, clock.ClockButtonOK.PreferredSize.Height);
                //---
                int wCancel = (int) Math.Ceiling(clock.ClockButtonCancelWidthFactor * width);
                int xCancel = (int) ((width - wCancel) * clock.ClockButtonCancelAlignX) + mCancel.Left;
                wCancel = wCancel - mCancel.Horizontal;
                Point ptCancel = new Point(xCancel, y2 + mCancel.Top);
                Size szCancel = new Size(wCancel, clock.ClockButtonCancel.PreferredSize.Height);

                if (doLayout) {
                    if (clock.ClockButtonOK.Owner != null)
                        clock.ClockButtonOK.SetBounds(ptOK, szOK);
                    if (clock.ClockButtonCancel.Owner != null)
                        clock.ClockButtonCancel.SetBounds(ptCancel, szCancel);
                }

                int yz1 = ptOK.Y + szOK.Height + mOK.Bottom + p.Bottom;
                int yz2 = ptCancel.Y + szCancel.Height + mCancel.Bottom + p.Bottom;
                ps.Height = Math.Max(yz1, yz2); // expected to be identical
            }
            else {
                ps.Height = y2 + p.Bottom;
            }*/
            return ps;
        }
    }
}