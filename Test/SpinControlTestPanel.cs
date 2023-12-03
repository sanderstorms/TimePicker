using System;
using System.Drawing;
using System.Windows.Forms;
using TimePicker.Opulos.Core.UI;

namespace TimePicker.Test;

public class SpinControlTestPanel : Panel
{
    private const string buttonText = "Example Button Style Look";
    private const string styleNotSupported = " Style Not Supported";

    private readonly Button btnExample = new()
        { Text = buttonText, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };

    private readonly CheckBox cbEnabled = new() { Text = "Enabled", Checked = true, AutoSize = true };
    private readonly Combo2 comboStyle = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker dpPicker = new() { Format = DateTimePickerFormat.Short, ShowUpDown = true };
    private Font font;

    private readonly NumericUpDown nudFontSize = new()
        { Minimum = 6, Maximum = 100, Increment = 0.2m, DecimalPlaces = 1 };

    private readonly SpinControl scCustom = new();
    private readonly TextBox tbCustom = new();

    public SpinControlTestPanel()
    {
        Dock = DockStyle.Fill;

        tbCustom.Controls.Add(scCustom);

        var k = 0;
        scCustom.UpClicked += delegate
        {
            tbCustom.Text = k.ToString();
            k++;
        };
        scCustom.DownClicked += delegate
        {
            k--;
            tbCustom.Text = k.ToString();
        };

        nudFontSize.ValueChanged += delegate
        {
            var oldFont = font;
            font = new Font(Font.FontFamily, Convert.ToSingle(nudFontSize.Value), Font.Style, Font.Unit,
                Font.GdiCharSet, Font.GdiVerticalFont);
            Font = font;
            if (oldFont != null)
                oldFont.Dispose();
        };
        nudFontSize.Value = 8;

        cbEnabled.CheckedChanged += delegate
        {
            var b = cbEnabled.Checked;
            tbCustom.Enabled = b;
            dpPicker.Enabled = b;
            nudFontSize.Enabled = b;
            btnExample.Enabled = b;
        };

        comboStyle.Items.AddRange(new object[]
        {
            SpinButtonStyle.Flat, SpinButtonStyle.Modern, SpinButtonStyle.Popup, SpinButtonStyle.Standard,
            SpinButtonStyle.System, SpinButtonStyle.ControlPaint
        });
        comboStyle.SelectedItem = scCustom.ButtonStyle;
        comboStyle.SelectedValueChanged += delegate
        {
            var style = (SpinButtonStyle)comboStyle.SelectedItem;
            scCustom.ButtonStyle = style;
            if (style == SpinButtonStyle.Modern || style == SpinButtonStyle.ControlPaint)
            {
                btnExample.Text = style + styleNotSupported;
            }
            else
            {
                btnExample.FlatStyle = (FlatStyle)style;
                btnExample.Text = buttonText;
            }
        };

        var x = 0;
        Padding = new Padding(10);
        var p = new TableLayoutPanel2 { Dock = DockStyle.Fill };
        p.Add(new Label3("Default NumericUpDown\r\nand Font Size"), nudFontSize, x++);
        p.Add(new Label3("Default DateTimePicker"), dpPicker, x++);
        p.Add(new Label3("Custom Spin Buttons") { BackColor = Color.LightYellow }, tbCustom, x++);
        p.Add(new Label3("Custom Button Style"), comboStyle, x);
        p.Controls.Add(btnExample, 2, x++);
        p.Add(null, cbEnabled, x++);
        Controls.Add(p);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            if (font != null)
            {
                font.Dispose();
                font = null;
            }
    }

    private class TableLayoutPanel2 : TableLayoutPanel
    {
        public void Add(Control c1, Control c2, int row)
        {
            if (c1 != null)
                Controls.Add(c1, 0, row);
            if (c2 != null)
                Controls.Add(c2, 1, row);
        }
    }

    private class Label3 : Label
    {
        public Label3(string text)
        {
            Text = text;
            AutoSize = true;
            Anchor = AnchorStyles.Left;
        }
    }
}