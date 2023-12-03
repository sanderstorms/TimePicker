using System;
using System.Globalization;
using System.Windows.Forms;

namespace TimePicker.Opulos.Core.UI;

public class TimePicker : MaskedTextBox<DateTime>
{
    private readonly ToolStripDropDownAttacher attacher;

    public ClockControl ClockMenu;

    public TimePicker() : this(3, true, true, true, true)
    {

    }
    /// <summary>
    ///     <para>
    ///         Creates a time picker with the specified number of millisecond digits. The base format
    ///         will be HH:mm:ss when the use24HourClock parameter is true, otherwise the base format
    ///         will be hh:mm:ss tt. The milliseconds are appended at the end, e.g. HH:mm:ss.ffff
    ///     </para>
    ///     <para>Call the MimicDateTimePicker() to mimic the same style of input.</para>
    ///     <para>Use the Value property to access the current DateTime value.</para>
    ///     <para>Use the ValueChanged event to listen for DateTime changes.</para>
    ///     <para>Use the TimeFormat and Mask in conjuction to change the format.</para>
    ///     <para>
    ///         The Arrays MinValues, MaxValues, PageUpDownDeltas and ByValues must
    ///         contain at least the number of tokens as the TimeFormat contains.
    ///     </para>
    /// </summary>
    /// <param name="addUpDownSpinner">An option to display up-down buttons.</param>
    /// <param name="immediateValueChange">
    ///     An option if changing the clock menu value requires clicking the OK button, or if
    ///     the change is instant.
    /// </param>
    /// <param name="numMilliDigits">The number of milliseconds to show.</param>
    /// <param name="showClockMenu">An option to show a clock menu when this control gets the focus or is clicked.</param>
    /// <param name="use24HourClock">An option to show hours 0 to 23 or 1 to 12.</param>
    public TimePicker(int numMilliDigits = 3, bool use24HourClock = true, bool addUpDownSpinner = true,
        bool showClockMenu = true, bool immediateValueChange = true) : base(addUpDownSpinner)
    {
        //Using 9s produces more natural usability than 0s.
        var mask = "99:99:99";
        var timeFormat = use24HourClock ? "HH:mm:ss" : "hh:mm:ss";
        var maxValue = 1;
        if (numMilliDigits > 0)
        {
            mask += "." + new string('9', numMilliDigits);
            timeFormat += "." + new string('f', numMilliDigits);
            for (var i = 0; i < numMilliDigits; i++)
                maxValue *= 10;
        }

        if (!use24HourClock)
        {
            timeFormat += " tt";
            mask += @"\ LL";
        }

        DateTimeFormat = timeFormat;
        Mask = mask;
        if (use24HourClock)
        {
            Tokens[0].MaxValue = 24;
        }
        else
        {
            Tokens[0].MinValue = 1;
            Tokens[0].MaxValue = 13;
            var dtfi = DateTimeFormatInfo.CurrentInfo;
            Tokens[Tokens.Count - 1].CustomValues = new object[] { dtfi.AMDesignator, dtfi.PMDesignator };
        }

        Tokens[2].MaxValue = 60;
        Tokens[4].MaxValue = 60;
        //--
        Tokens[0].BigIncrement = 4;
        Tokens[2].BigIncrement = 10;
        Tokens[4].BigIncrement = 10;
        //--

        MimicDateTimePicker();
        Value = TextToValue(ValueToText(DateTime.Now));

        ClockMenu = new ClockControl(); //false, false, false);
        if (showClockMenu)
        {
            attacher = new ToolStripDropDownAttacher(ClockMenu, this, false)
                { FontSizeDelta = -1.4f }; //-3 }; // changed 2016-01-25
            attacher.ClickOpensMenu = false;
            //attacher.EscapeCloseReason = null;

            attacher.MenuShowing += delegate
            {
                var t = TokenAt(SelectionStart);
                if (t != null)
                {
                    if (t.SeqNo == 0)
                        ClockMenu.ClockFace = ClockFace.Hours;
                    else if (t.SeqNo == 2)
                        ClockMenu.ClockFace = ClockFace.Minutes;
                }
            };

            var flag = false;
            GotFocus += delegate { flag = true; };
            LostFocus += delegate { flag = false; };

            MouseDown += delegate
            {
                if (flag)
                {
                    // if the clock just opened from a GotFocus then do not close it
                    flag = false;
                    attacher.ShowMenu();
                    return;
                }

                if (!ClockMenu.Visible)
                {
                    attacher.ShowMenu();
                }
                else
                {
                    var t = TokenAt(SelectionStart);
                    if (t == PreviousSelectedToken && (t.SeqNo == 0 || t.SeqNo == 2))
                        attacher.CloseMenu(ToolStripDropDownCloseReason.AppClicked);
                    else
                        attacher.ShowMenu();
                }
            };
        }

        ClockMenu.ButtonClicked += ClockMenu_ButtonClicked;

        ValueChanged += delegate { ClockMenu.Value = Value; };

        var origValue = DateTime.MinValue;
        ClockMenu.VisibleChanged += delegate
        {
            if (ClockMenu.Visible)
            {
                origValue = Value;
                ClockMenu.Value = Value;
            }
        };

        ClockMenu.Closed += (o, e) =>
        {
            // escape key is used
            if (e.CloseReason == ToolStripDropDownCloseReason.Keyboard)
                Value = origValue;
            else
                Value = ClockMenu.Value;
        };

        ClockMenu.ValueChanged += delegate
        {
            if (immediateValueChange)
                Value = ClockMenu.Value;
        };
    }

    public string DateTimeFormat { get; set; }

    public bool AutoCloseMenuFocusLost
    {
        get => attacher.AutoCloseReasonControlFocusLost.HasValue;
        set => attacher.AutoCloseReasonControlFocusLost = value ? ToolStripDropDownCloseReason.AppFocusChange : null;
    }

    public bool AutoCloseMenuWindowChanged
    {
        get => attacher.AutoCloseReasonWindowLostFocus.HasValue;
        set => attacher.AutoCloseReasonWindowLostFocus = value ? ToolStripDropDownCloseReason.AppFocusChange : null;
    }

    /*protected override bool ProcessCmdKey(ref Message msg, Keys keyData) { // added 2016-01-25
        if (keyData == Keys.Escape) {
            if (ClockMenu.Visible) {
                attacher.CloseMenu(ToolStripDropDownCloseReason.Keyboard);
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }*/

    private void ClockMenu_ButtonClicked(object sender, EventArgs e)
    {
        // if Cancel button, then send Keyboard, which is used to indicate keyboard escape
        var r = sender == ClockMenu.ClockButtonOK
            ? ToolStripDropDownCloseReason.ItemClicked
            : ToolStripDropDownCloseReason.Keyboard;
        attacher.CloseMenu(r);
    }

    public override DateTime TextToValue(string text)
    {
        var d = DateTime.MinValue;
        var success = DateTime.TryParseExact(text, DateTimeFormat, DateTimeFormatInfo.CurrentInfo, DateTimeStyles.None,
            out d);
        if (!success)
            success = DateTime.TryParseExact(text, DateTimeFormat, DateTimeFormatInfo.InvariantInfo,
                DateTimeStyles.None, out d);

        if (DateTimeFormat.IndexOf("tt") < 0) // fixed 2016-01-25
            // If the format does not display the meridiem, then the time will be parsed to AM.
            // Maintain the PM value by adding 12 hours.
            if (d.Hour < 12 && Value.Hour >= 12)
                d = d.AddHours(12);

        return d;
    }

    public override string ValueToText(DateTime value)
    {
        return value.ToString(DateTimeFormat);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            if (ClockMenu != null)
                ClockMenu.Dispose();

            ClockMenu = null;
        }
    }
}