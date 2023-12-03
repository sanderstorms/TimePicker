namespace TimePicker
{
    partial class TimePickerControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        private Opulos.Core.UI.TimePicker timePicker;
        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.timePicker = new TimePicker.Opulos.Core.UI.TimePicker();
            this.SuspendLayout();
            // 
            // timePicker
            // 
            this.timePicker.AutoCloseMenuFocusLost = true;
            this.timePicker.AutoCloseMenuWindowChanged = true;
            this.timePicker.ByDigit = false;
            this.timePicker.CaretVisible = false;
            this.timePicker.CaretWrapsAround = true;
            this.timePicker.ChopRunningText = true;
            this.timePicker.Cursor = System.Windows.Forms.Cursors.Default;
            this.timePicker.CutCopyMaskFormat = System.Windows.Forms.MaskFormat.IncludePromptAndLiterals;
            this.timePicker.DateTimeFormat = "HH:mm:ss.fff";
            this.timePicker.DeleteKeyShiftsTextLeft = true;
            this.timePicker.EscapeKeyRevertsValue = false;
            this.timePicker.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.timePicker.InsertKeyMode = System.Windows.Forms.InsertKeyMode.Overwrite;
            this.timePicker.KeepSelectedIncludesWhitespace = false;
            this.timePicker.KeepTokenSelected = true;
            this.timePicker.Location = new System.Drawing.Point(0, 0);
            this.timePicker.Margin = new System.Windows.Forms.Padding(0);
            this.timePicker.Mask = "99:99:99.999";
            this.timePicker.Name = "timePicker";
            this.timePicker.PromptChar = '0';
            this.timePicker.Size = new System.Drawing.Size(114, 24);
            this.timePicker.SplitChars = null;
            this.timePicker.TabIndex = 0;
            this.timePicker.Text = "1 1541163";
            this.timePicker.TextMaskFormat = System.Windows.Forms.MaskFormat.IncludePromptAndLiterals;
            this.timePicker.UseMaxValueIfTooLarge = false;
            this.timePicker.Value = new System.DateTime(2023, 12, 3, 10, 15, 41, 163);
            this.timePicker.ValuesCarryOver = false;
            this.timePicker.ValuesWrapAround = true;
            this.timePicker.ValuesWrapIfNoCarryRoom = true;
            this.timePicker.ValueTooLargeFixMode = TimePicker.Opulos.Core.UI.ValueFixMode.KeepExistingValue;
            this.timePicker.ValueTooSmallFixMode = TimePicker.Opulos.Core.UI.ValueFixMode.KeepExistingValue;
            // 
            // TimePickerControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.timePicker);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "TimePickerControl";
            this.Size = new System.Drawing.Size(114, 24);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
    }
}
