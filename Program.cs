using System;
using System.Drawing;
using System.Windows.Forms;
using TimePicker.Test;

namespace TimePicker;

internal static class Program
{
    // https://sourceforge.net/projects/time-picker/
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        //TextOverlay.TestAlgorithm();
        //return;

        //System.Globalization.CultureInfo ci = new System.Globalization.CultureInfo("ja-JP");
        //System.Threading.Thread.CurrentThread.CurrentCulture = ci;
        //System.Threading.Thread.CurrentThread.CurrentUICulture = ci;

        var f = new Form();
        f.Text = "TimePickerDemo";
        f.ClientSize = new Size(700, 700);

        var p = new MaskedTextBoxDemoPanel();
        f.Controls.Add(p);
        f.Font = new Font(SystemFonts.MenuFont.FontFamily, 12f, FontStyle.Regular);
        f.StartPosition = FormStartPosition.CenterScreen;
        Application.Run(f);
    }
}