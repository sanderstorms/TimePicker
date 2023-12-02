using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Opulos.Core.UI {

public class SpinControlTestPanel : Panel {

	const String buttonText = "Example Button Style Look";
	const String styleNotSupported = " Style Not Supported";
	Button btnExample = new Button { Text = buttonText, AutoSize = true, AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink };
	Combo2 comboStyle = new Combo2 { DropDownStyle = ComboBoxStyle.DropDownList };
	NumericUpDown nudFontSize = new NumericUpDown { Minimum = 6, Maximum = 100, Increment = 0.2m, DecimalPlaces = 1 };
	DateTimePicker dpPicker = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowUpDown = true };
	CheckBox cbEnabled = new CheckBox { Text = "Enabled", Checked = true, AutoSize = true };
	SpinControl scCustom = new SpinControl();
	TextBox tbCustom = new TextBox();
	Font font = null;

	public SpinControlTestPanel() {
		Dock = DockStyle.Fill;

		tbCustom.Controls.Add(scCustom);

		int k = 0;
		scCustom.UpClicked += delegate {
			tbCustom.Text = k.ToString();
			k++;
		};
		scCustom.DownClicked += delegate {
			k--;
			tbCustom.Text = k.ToString();
		};

		nudFontSize.ValueChanged += delegate {
			Font oldFont = font;
			font = new Font(Font.FontFamily, Convert.ToSingle(nudFontSize.Value), Font.Style, Font.Unit, Font.GdiCharSet, Font.GdiVerticalFont);
			this.Font = font;
			if (oldFont != null)
				oldFont.Dispose();
		};
		nudFontSize.Value = 8;

		cbEnabled.CheckedChanged += delegate {
			bool b = cbEnabled.Checked;
			tbCustom.Enabled = b;
			dpPicker.Enabled = b;
			nudFontSize.Enabled = b;
			btnExample.Enabled = b;
		};

		comboStyle.Items.AddRange(new Object[] { SpinButtonStyle.Flat, SpinButtonStyle.Modern, SpinButtonStyle.Popup, SpinButtonStyle.Standard, SpinButtonStyle.System, SpinButtonStyle.ControlPaint });
		comboStyle.SelectedItem = scCustom.ButtonStyle;
		comboStyle.SelectedValueChanged += delegate {
			SpinButtonStyle style = (SpinButtonStyle) comboStyle.SelectedItem;
			scCustom.ButtonStyle = style;
			if (style == SpinButtonStyle.Modern || style == SpinButtonStyle.ControlPaint) {
				btnExample.Text = style + styleNotSupported;
			} else {
				btnExample.FlatStyle = (FlatStyle) style;
				btnExample.Text = buttonText;
			}

		};

		int x = 0;
		this.Padding = new Padding(10);
		TableLayoutPanel2 p = new TableLayoutPanel2 { Dock = DockStyle.Fill };
		p.Add(new Label3("Default NumericUpDown\r\nand Font Size"), nudFontSize, x++);
		p.Add(new Label3("Default DateTimePicker"), dpPicker, x++);
		p.Add(new Label3("Custom Spin Buttons") { BackColor = Color.LightYellow }, tbCustom, x++);
		p.Add(new Label3("Custom Button Style"), comboStyle, x);
		p.Controls.Add(btnExample, 2, x++);
		p.Add(null, cbEnabled, x++);
		Controls.Add(p);
	}

	private class TableLayoutPanel2 : TableLayoutPanel {
		public void Add(Control c1, Control c2, int row) {
			if (c1 != null)
				Controls.Add(c1, 0, row);
			if (c2 != null)
				Controls.Add(c2, 1, row);
		}		
	}

	private class Label3 : Label {
		public Label3(String text) : base() {
			Text = text;
			AutoSize = true;
			Anchor = AnchorStyles.Left;
		}
	}

	protected override void Dispose(bool disposing) {
		base.Dispose(disposing);
		if (disposing) {
			if (font != null) {
				font.Dispose();
				font = null;
			}
		}
	}
}
}