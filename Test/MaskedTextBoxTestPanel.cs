using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Opulos.Core.UI;

namespace TimePicker.Test;

public class MaskedTextBoxDemoPanel : Panel
{
    private readonly ClockControlEmbeddedDemoPanel panelEmbedded = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly MaskedTextBoxGeneralPanel panelGeneral = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly SpinControlTestPanel panelSpinButtons = new();
    private readonly TimePickerMillisPanel panelTimePicker = new() { Dock = DockStyle.Fill, AutoScroll = true };

    private readonly TabControl tc = new() { Dock = DockStyle.Fill };
    private readonly TabPage tpEmbedded = new("Time Picker Embedded");
    private readonly TabPage tpGeneral = new("General");
    private readonly TabPage tpSpinButtons = new("Spin Buttons");
    private readonly TabPage tpTimePicker = new("Time Picker");

    public MaskedTextBoxDemoPanel()
    {
        Dock = DockStyle.Fill;
        tpGeneral.Controls.Add(panelGeneral);
        tpTimePicker.Controls.Add(panelTimePicker);
        tpEmbedded.Controls.Add(panelEmbedded);
        tpSpinButtons.Controls.Add(panelSpinButtons);
        tc.TabPages.Add(tpGeneral);
        tc.TabPages.Add(tpTimePicker);
        tc.TabPages.Add(tpEmbedded);
        tc.TabPages.Add(tpSpinButtons);
        tc.SelectedTab = tpTimePicker;

        Controls.Add(tc);
    }
}

public class TimePickerMillisPanel : Panel
{
    private static bool AssignOwnerWindow = true;
    private static bool AutoCloseMenuFocusLost;

    private static bool AutoCloseMenuWindowChanged;

    //HFLP panel = null;
    private static int FormCount;

    private readonly Button btnNewWindow = new()
        { Text = "New Window", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty };

    private readonly Button btnNow = new()
        { Text = "Now()", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty };

    private readonly CheckBox cbAssignOwnerWindow = new()
        { Text = "Assign Owner Window", AutoSize = true, Checked = AssignOwnerWindow };

    private readonly CheckBox cbAutoCloseMenuFocusLost = new()
        { Text = "Auto Close Menu (Focus Lost)", AutoSize = true, Checked = AutoCloseMenuFocusLost };

    private readonly CheckBox cbAutoCloseMenuWindowChanged = new()
        { Text = "Auto Close Menu (Window Changed)", AutoSize = true, Checked = AutoCloseMenuWindowChanged };

    private int numEvents;
    private readonly Opulos.Core.UI.TimePicker timePicker = new(0, false); //new TimePicker(3, true, true);
    private ToolTip tips = new();

    public TimePickerMillisPanel()
    {
        //var tokens = timePicker.Tokens;
        //tokens.RemoveAt(3); // removes ':'
        //tokens.RemoveAt(3); // removes 'ss'
        //timePicker.Mask = "09:99\\ LL";
        //timePicker.DateTimeFormat = "hh:mm tt";
        //timePicker.Value = timePicker.TextToValue(timePicker.ValueToText(DateTime.Now));

        timePicker.Margin = new Padding(3, 5, 3, 3);
        btnNow.Margin = new Padding(3);
        btnNewWindow.Margin = new Padding(3);

        var panel2 = new TableLayoutPanel();
        panel2.Padding = new Padding(10);
        panel2.SuspendLayout();
        panel2.Controls.Add(btnNewWindow, 0, 0);
        panel2.Controls.Add(btnNow, 1, 0);
        panel2.Controls.Add(timePicker, 2, 0);
        panel2.Controls.Add(cbAssignOwnerWindow, 0, 1);
        panel2.Controls.Add(cbAutoCloseMenuFocusLost, 0, 2);
        panel2.Controls.Add(cbAutoCloseMenuWindowChanged, 0, 3);
        panel2.SetColumnSpan(cbAssignOwnerWindow, 3);
        panel2.SetColumnSpan(cbAutoCloseMenuFocusLost, 3);
        panel2.SetColumnSpan(cbAutoCloseMenuWindowChanged, 3);
        panel2.Dock = DockStyle.Fill;
        panel2.ResumeLayout(false);

        //panel = new HFLP(timePicker, btnNow, cbKeepMenuOpen, btnNewWindow, cbAssignOwnerWindow) { Padding = new Padding(20) };
        //panel.Dock = DockStyle.Fill;
        Controls.Add(panel2);

        btnNow.Click += btnNow_Click;
        btnNewWindow.Click += btnNewWindow_Click;
        timePicker.ValueChanged += timePicker_ValueChanged;
        Dock = DockStyle.Fill;
        //panel.Dock = DockStyle.Fill;

        tips.SetToolTip(cbAssignOwnerWindow, "Assigns the current form as the Owner of the new window.");
        tips.SetToolTip(cbAutoCloseMenuFocusLost,
            "Keeps the menu open when a different window top level window is activated.");

        cbAssignOwnerWindow.CheckedChanged += delegate { AssignOwnerWindow = cbAssignOwnerWindow.Checked; };
        cbAutoCloseMenuFocusLost.CheckedChanged += delegate
        {
            timePicker.AutoCloseMenuFocusLost = cbAutoCloseMenuFocusLost.Checked;
            AutoCloseMenuFocusLost = cbAutoCloseMenuFocusLost.Checked;
        };
        cbAutoCloseMenuWindowChanged.CheckedChanged += delegate
        {
            timePicker.AutoCloseMenuWindowChanged = cbAutoCloseMenuWindowChanged.Checked;
            AutoCloseMenuWindowChanged = cbAutoCloseMenuWindowChanged.Checked;
        };

        timePicker.AutoCloseMenuFocusLost = AutoCloseMenuFocusLost;
        timePicker.AutoCloseMenuWindowChanged = AutoCloseMenuWindowChanged;
    }

    private void btnNewWindow_Click(object sender, EventArgs e)
    {
        var f = new Form();
        f.Text = "TimePicker" + ++FormCount;
        f.Size = new Size(600, 400);
        f.Controls.Add(new TimePickerMillisPanel());
        if (cbAssignOwnerWindow.Checked)
            f.Owner = FindForm();
        f.Show();
    }

    private void timePicker_ValueChanged(object sender, ValueChangedEventArgs<DateTime> e)
    {
        numEvents++;
        btnNow.Text = "Now(" + numEvents + ")";
    }

    private void btnNow_Click(object sender, EventArgs e)
    {
        timePicker.Value = DateTime.Now;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            if (tips != null)
                tips.Dispose();

            tips = null;
        }
    }
}

internal class Combo2 : ComboBox
{
    public Combo2()
    {
        AutoSize = true;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var s = base.GetPreferredSize(proposedSize);
        var dw = SystemInformation.HorizontalScrollBarArrowWidth;
        using (var g = CreateGraphics())
        {
            foreach (var item in Items)
            {
                if (item == null)
                    continue;
                var s2 = TextRenderer.MeasureText(g, item.ToString(), Font);
                if (s2.Width + dw > s.Width)
                    s.Width = s2.Width + dw;
            }
        }

        return s;
    }
}

public class MaskedTextBoxGeneralPanel : FlowLayoutPanel
{
    public MaskedTextBoxGeneralPanel()
    {
        var lbDateTimePicker = new Label { Text = "Date Time Picker", AutoSize = true };
        var datePicker = new DateTimePicker();
        datePicker.AutoSize = true; // doesn't work
        datePicker.CustomFormat = "HH:mm:ss tt";
        datePicker.Format = DateTimePickerFormat.Custom;
        datePicker.ShowUpDown = true;
        datePicker.AutoSize = true;

        var lbDefaultMaskedTextBox = new Label { Text = "Default Masked Text Box", AutoSize = true };
        var mtbDefault = new MaskedTextBox();
        mtbDefault.PromptChar = '0';
        mtbDefault.Mask = @"00:00:00.000 \A\M";
        mtbDefault.InsertKeyMode = InsertKeyMode.Overwrite;
        mtbDefault.AllowPromptAsInput = true;
        mtbDefault.ResetOnPrompt = true;
        mtbDefault.ResetOnSpace = true; // space turns into PromptChar
        mtbDefault.AutoSize = true; // height adjusts, but not width

        var lbEnhancedMaskedTextBox = new Label
            { Text = "Enhanced Masked Text Box", AutoSize = true, BackColor = Color.LightYellow };
        MaskedTextBox<object> mtbEnhanced = new MaskedTextBoxNoImpl<object>();
        mtbEnhanced.CaretWrapsAround = true;
        mtbEnhanced.ChopRunningText = false;
        mtbEnhanced.ValuesCarryOver = true;
        mtbEnhanced.CaretVisible = true;
        mtbEnhanced.ByDigit = true;
        mtbEnhanced.PromptChar = '0';
        mtbEnhanced.Mask = @"00:00:00.000 \XX";
        var dtfi = DateTimeFormatInfo.CurrentInfo;
        mtbEnhanced.Tokens[5].CustomValues = new[] { dtfi.AMDesignator, dtfi.PMDesignator };
        mtbEnhanced.Tokens[5].PadRule = TokenValuePadRule.Left;
        mtbEnhanced.Tokens[5].PadChar = ' '; // required to keep the space on the left
        mtbEnhanced.Size = mtbEnhanced.PreferredSize;

        var bg = Color.LightYellow;
        var cbCaretWrapsAround = new CheckBox
            { Text = "Caret Wraps Around", Checked = mtbEnhanced.CaretWrapsAround, AutoSize = true, BackColor = bg };
        var cbKeepTokenSelected = new CheckBox
            { Text = "Keep Token Selected", Checked = mtbEnhanced.KeepTokenSelected, AutoSize = true, BackColor = bg };
        var cbKeepSelectedIncludesWhitespace = new CheckBox
        {
            Text = "Keep Selected Includes Whitespace", Checked = mtbEnhanced.KeepSelectedIncludesWhitespace,
            AutoSize = true, BackColor = bg
        };
        var cbValuesWrapAround = new CheckBox
            { Text = "Values Wrap Around", Checked = mtbEnhanced.ValuesWrapAround, AutoSize = true, BackColor = bg };
        var cbCaretVisible = new CheckBox
            { Text = "Caret Visible", Checked = mtbEnhanced.CaretVisible, AutoSize = true, BackColor = bg };
        var cbValuesCarryOver = new CheckBox
            { Text = "Values Carry Over", Checked = mtbEnhanced.ValuesCarryOver, AutoSize = true, BackColor = bg };
        var cbByDigit = new CheckBox
            { Text = "By Digit", Checked = mtbEnhanced.ByDigit, AutoSize = true, BackColor = bg };
        var cbDeleteKeyShiftsTextLeft = new CheckBox
        {
            Text = "Delete Key Shifts Text Left", Checked = mtbEnhanced.DeleteKeyShiftsTextLeft, AutoSize = true,
            BackColor = bg
        };
        var cbChopRunningText = new CheckBox
            { Text = "Chop Running Text", Checked = mtbEnhanced.ChopRunningText, AutoSize = true, BackColor = bg };
        var cbUseMaxValueIfTooLarge = new CheckBox
        {
            Text = "Use Max Value If Too Large", Checked = mtbEnhanced.UseMaxValueIfTooLarge, AutoSize = true,
            BackColor = bg
        };

        var lbValueFixMode = new Label { Text = "Value Fix Mode", AutoSize = true };
        ComboBox comboValueFixMode = new Combo2 { DropDownStyle = ComboBoxStyle.DropDownList };
        comboValueFixMode.Items.AddRange(Enum.GetValues(typeof(ValueFixMode)).Cast<object>().ToArray());
        comboValueFixMode.SelectedItem = mtbEnhanced.ValueTooLargeFixMode;

        var lbPromptChar = new Label { Text = "Prompt Char", AutoSize = true };
        var comboPromptChar = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        comboPromptChar.Items.Add('_');
        comboPromptChar.Items.Add(' ');
        comboPromptChar.Items.Add('0');
        comboPromptChar.SelectedItem = mtbEnhanced.PromptChar;

        var tips = new ToolTip { InitialDelay = 200, AutoPopDelay = 30000 };
        tips.SetToolTip2(cbByDigit,
            "Pressing the up or down keys only change the character at the caret. Individual tokens can override this default value. Does not make sense to use in combination with KeepTokenSelected.");
        tips.SetToolTip2(cbCaretVisible,
            "An option to hide the flashing caret. Typically true when KeepTokenSelected is true.");
        tips.SetToolTip2(cbCaretWrapsAround,
            "If the caret is at the very start and the left arrow key is pressed or very end and the right arrow key is pressed then the caret moves to the opposite side. If KeepTokenSelected is true, then the first editable token on the opposite side is selected.");
        tips.SetToolTip2(cbChopRunningText,
            "When typing text into a selected area, the text resets back to empty if the maximum length is reached. Otherwise the previously typed characters are kept.");
        tips.SetToolTip2(cbDeleteKeyShiftsTextLeft,
            "Applies only when selection length is 0. Pressing the delete key can pull the right side text towards the caret, or the character at the caret is deleted and the caret moves to the right.");
        tips.SetToolTip2(cbKeepSelectedIncludesWhitespace,
            "Only applies when KeepTokenSelected is true. If the displayed text contains leading or trailing whitespace, the blue highlighted text can include or exclude the whitespace. Individual tokens can override this value.");
        tips.SetToolTip2(cbKeepTokenSelected,
            "An option to always keep the text that belongs to the token at the caret selected. Non-editable tokens cannot be selected.");
        tips.SetToolTip2(cbValuesCarryOver,
            "When the value of the current token wraps around, this option can increase or decrease the values of the tokens to the left. Individual tokens can specify the carry over range.");
        tips.SetToolTip2(cbValuesWrapAround,
            "Using the up-down keys or buttons to increase or decrease the value of a token when it's at its maximum or minimum value cause it to wrap around. Otherwise the value is not changed. Individual tokens can override this value. Only applies when text is selected.");
        tips.SetToolTip2(cbUseMaxValueIfTooLarge,
            "Typing text takes to the token's maximum value if the typed text exceeds the token's max value. Only applies when KeepTokenSelected is true and ChopRunningText is false.");
        tips.SetToolTip2(comboValueFixMode, "Specifies how values that are too large or too small are handled.");

        var lbFontSize = new Label { Text = "Font Size", AutoSize = true };
        var nudFontSize = new NumericUpDown
            { Value = 8, Increment = 0.25m, DecimalPlaces = 2, Minimum = 6, Maximum = 100, AutoSize = true };
        nudFontSize.ValueChanged += delegate
        {
            var f = Font;
            FindForm().Font = new Font(f.FontFamily, Convert.ToSingle(nudFontSize.Value), f.Style, f.Unit, f.GdiCharSet,
                f.GdiVerticalFont);
        };

        lbFontSize.Font = Font; // fixed size so they don't change
        nudFontSize.Font = Font;

        cbCaretWrapsAround.CheckedChanged += delegate
        {
            mtbEnhanced.CaretWrapsAround = cbCaretWrapsAround.Checked;
            mtbEnhanced.Focus();
        };
        cbKeepTokenSelected.CheckedChanged += delegate
        {
            mtbEnhanced.KeepTokenSelected = cbKeepTokenSelected.Checked;
            mtbEnhanced.Focus();
        };
        cbKeepSelectedIncludesWhitespace.CheckedChanged += delegate
        {
            mtbEnhanced.KeepSelectedIncludesWhitespace = cbKeepSelectedIncludesWhitespace.Checked;
            mtbEnhanced.Focus();
        };
        cbValuesWrapAround.CheckedChanged += delegate
        {
            mtbEnhanced.ValuesWrapAround = cbValuesWrapAround.Checked;
            mtbEnhanced.Focus();
        };
        cbCaretVisible.CheckedChanged += delegate
        {
            mtbEnhanced.CaretVisible = cbCaretVisible.Checked;
            mtbEnhanced.Focus();
        };
        cbValuesCarryOver.CheckedChanged += delegate
        {
            mtbEnhanced.ValuesCarryOver = cbValuesCarryOver.Checked;
            mtbEnhanced.Focus();
        };
        cbByDigit.CheckedChanged += delegate
        {
            mtbEnhanced.ByDigit = cbByDigit.Checked;
            mtbEnhanced.Focus();
        };
        cbDeleteKeyShiftsTextLeft.CheckedChanged += delegate
        {
            mtbEnhanced.DeleteKeyShiftsTextLeft = cbDeleteKeyShiftsTextLeft.Checked;
            mtbEnhanced.Focus();
        };
        comboPromptChar.SelectedValueChanged += delegate
        {
            var promptChar = (char)comboPromptChar.SelectedItem;
            mtbEnhanced.PromptChar = promptChar;
            mtbDefault.PromptChar = promptChar;
            mtbEnhanced.Focus();
        };
        cbChopRunningText.CheckedChanged += delegate
        {
            mtbEnhanced.ChopRunningText = cbChopRunningText.Checked;
            mtbEnhanced.Focus();
        };
        cbUseMaxValueIfTooLarge.CheckedChanged += delegate
        {
            mtbEnhanced.UseMaxValueIfTooLarge = cbUseMaxValueIfTooLarge.Checked;
            mtbEnhanced.Focus();
        };
        comboValueFixMode.SelectedIndexChanged += delegate
        {
            var vfm = (ValueFixMode)comboValueFixMode.SelectedItem;
            mtbEnhanced.ValueTooLargeFixMode = vfm;
            mtbEnhanced.ValueTooSmallFixMode = vfm;
            mtbEnhanced.Focus();
        };

        Disposed += delegate { tips.Dispose(); };

        Dock = DockStyle.Fill;
        BackColor = Color.LightBlue;
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        FlowLayoutPanel p0 = this;
        p0.Controls.Add(CreateHPanel(lbFontSize, nudFontSize));
        p0.Controls.Add(CreateHPanel(lbDateTimePicker, datePicker));
        p0.Controls.Add(CreateHPanel(lbDefaultMaskedTextBox, mtbDefault));
        p0.Controls.Add(CreateHPanel(lbEnhancedMaskedTextBox, mtbEnhanced));
        p0.Controls.Add(cbCaretWrapsAround);
        p0.Controls.Add(cbKeepTokenSelected);
        p0.Controls.Add(cbKeepSelectedIncludesWhitespace);
        p0.Controls.Add(cbValuesWrapAround);
        p0.Controls.Add(cbCaretVisible);
        p0.Controls.Add(cbValuesCarryOver);
        p0.Controls.Add(cbByDigit);
        p0.Controls.Add(cbDeleteKeyShiftsTextLeft);
        p0.Controls.Add(cbChopRunningText);
        p0.Controls.Add(cbUseMaxValueIfTooLarge);
        var p1 = CreateHPanel(lbValueFixMode, comboValueFixMode);
        p1.BackColor = bg;
        p0.Controls.Add(p1);
        p0.Controls.Add(CreateHPanel(lbPromptChar, comboPromptChar));

        FontChanged += delegate
        {
            nudFontSize.Value = Convert.ToDecimal(Font.Size);
            datePicker.Size = datePicker.PreferredSize; // width stays the same regardless of font size
            mtbDefault.Size = mtbDefault.PreferredSize;
        };

        mtbDefault.VisibleChanged += delegate
        {
            if (mtbDefault.Visible) mtbDefault.Size = mtbDefault.PreferredSize;
        };
    }

    private static FlowLayoutPanel CreateHPanel(params Control[] controls)
    {
        FlowLayoutPanel p = new HFLP();
        //FlowLayoutPanel p = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        foreach (var c in controls)
        {
            p.Controls.Add(c);
            c.AutoSize = true;
            c.Anchor = AnchorStyles.None;
        }

        return p;
    }
}

public class ClockControlEmbeddedDemoPanel : Panel
{
    private readonly ClockControl cc = new(showOKCancelButtons: false, childControl: true);

    public ClockControlEmbeddedDemoPanel()
    {
        var lb = new Label
        {
            Text = "Example using ClockControl as a child control", AutoSize = true, Dock = DockStyle.Top,
            Margin = new Padding(20), Padding = new Padding(20)
        };

        var panel = new FlowLayoutPanel
        {
            WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10),
            Dock = DockStyle.Fill
        };
        var btn = new Button
        {
            Text = "Now", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(10, 0, 0, 0)
        };
        panel.Controls.Add(cc);
        panel.Controls.Add(btn);
        btn.Click += delegate { cc.Value = DateTime.Now; };

        BackColor = Color.LightYellow;
        panel.BackColor = SystemColors.Control;

        Controls.Add(panel);
        Controls.Add(lb);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        var f = Font;
        cc.Font = f;
    }
}

internal class MaskedTextBoxNoImpl<T> : MaskedTextBox<T>
{
    public override T TextToValue(string text)
    {
        return default;
    }

    public override string ValueToText(T value)
    {
        return null;
    }
}

internal class HFLP : FlowLayoutPanel
{
    public HFLP(params Control[] controls)
    {
        FlowDirection = FlowDirection.LeftToRight;
        WrapContents = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        foreach (var c in controls)
            Controls.Add(c);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var s = Size.Empty;
        foreach (Control c in Controls)
        {
            var ps = c.PreferredSize;
            var m = c.Margin;
            s.Width += ps.Width + m.Horizontal;
            var h = ps.Height + m.Vertical;
            if (h > s.Height)
                s.Height = h;
        }

        return s;
    }
}

internal static class ToolTipEx
{
    //private static Hashtable htWidths = new Hashtable();
    //private static float baseWidth = -1;
    private static readonly int MaxWidthPixels;

    static ToolTipEx()
    {
        using (var g = Graphics.FromHwnd(IntPtr.Zero))
        {
            //	baseWidth = g.MeasureString("AA", SystemFonts.DefaultFont).Width; //TextRenderer.MeasureText(g, "AA", SystemFonts.DefaultFont).Width;
            //htWidths[' '] = g.MeasureString("A A", SystemFonts.DefaultFont).Width - baseWidth; //TextRenderer.MeasureText(g, "A A", SystemFonts.DefaultFont).Width - baseWidth;
            MaxWidthPixels = (int)g.MeasureString("012345678901234567890123456789012345678901234567890123456789",
                SystemFonts.DefaultFont).Width;
            foreach (var s in Screen.AllScreens)
            {
                var r = s.Bounds;
                var third = r.Width / 3;
                if (MaxWidthPixels > third)
                    MaxWidthPixels = third;
            }
        }
    }

    private static string Format(string caption, int maxPixelsWide)
    {
        float width = 0;
        var g = Graphics.FromHwnd(IntPtr.Zero);
        char? lastChar = null;
        var sb = new StringBuilder();
        int? startIndex = null;
        for (var k = 0; k <= caption.Length; k++)
        {
            var c = k < caption.Length ? caption[k] : ' ';
            if (char.IsWhiteSpace(c))
            {
                var currentWord = "";
                if (startIndex.HasValue)
                    currentWord = caption.Substring(startIndex.Value, k - startIndex.Value);

                if (c == '\n')
                {
                    if (lastChar.HasValue)
                        sb.Append(lastChar.Value);
                    sb.Append(currentWord).Append('\n');
                    width = 0;
                    lastChar = null;
                }
                else
                {
                    float lastCharPlusCurrentWordWidth = 0;
                    float wordWidth = 0;

                    wordWidth = g.MeasureString(currentWord, SystemFonts.DefaultFont).Width;
                    if (lastChar.HasValue)
                        lastCharPlusCurrentWordWidth =
                            g.MeasureString(lastChar.Value + currentWord, SystemFonts.DefaultFont).Width;
                    else
                        lastCharPlusCurrentWordWidth = wordWidth;

                    if (width + lastCharPlusCurrentWordWidth > maxPixelsWide)
                    {
                        var diff = width + wordWidth - maxPixelsWide;
                        if (diff < wordWidth / 2)
                        {
                            if (lastChar.HasValue)
                                sb.Append(lastChar.Value);
                            sb.Append(currentWord).Append('\n');
                            width = 0;
                            lastChar = null;
                        }
                        else
                        {
                            sb.Append('\n').Append(currentWord);
                            //width = (lastChar.HasValue ? wordWidth - lastCharWidth : wordWidth);
                            width = lastChar.HasValue ? wordWidth : lastCharPlusCurrentWordWidth;
                            lastChar = c;
                        }
                    }
                    else
                    {
                        if (lastChar.HasValue)
                            sb.Append(lastChar.Value);
                        sb.Append(currentWord);
                        width += wordWidth;
                        lastChar = c;
                    }
                }

                startIndex = null;
            }
            else
            {
                if (startIndex == null)
                    startIndex = k;
            }
        }

        g.Dispose();

        return sb.ToString();
    }

    public static void SetToolTip2(this ToolTip tip, Control control, string caption, int? maxWidthPixels = null)
    {
        caption = Format(caption, maxWidthPixels.HasValue ? maxWidthPixels.Value : MaxWidthPixels);
        tip.SetToolTip(control, caption);
    }
}