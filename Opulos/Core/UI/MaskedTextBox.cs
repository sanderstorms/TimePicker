using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TimePicker.Opulos.Core.UI;

/// <summary>
///     Adds many useful features to the MaskTextBox, such as:
///     -Ability to add spinner buttons.
///     -Ability to scroll numbers up or down, as a whole or by digit.
///     -Ability for values to carry over into the next token.
///     -Ability to specify minimum and maximum values of each token.
///     -Ability to customize the input behavior to suit the needs of the control.
///     -Ability to provide a predefined list of custom values that can be selected.
///     -Ability to specify the list of characters when using ByDigit.
///     When the Mask property is set, this class parses the mask into a sequence of
///     Tokens (see Tokens property).
/// </summary>
public abstract class MaskedTextBox<T> : MaskedTextBox
{
    //-----------
    private const int DELETE = 0x2E;

    private const int BACKSPACE = 0x08;

    //private const int SPACE = 0x20;
    //private const int ESCAPE = 0x1B;
    //---
    private const int PAGEUP = 0x21;
    private const int PAGEDOWN = 0x22;
    private const int END = 0x23;
    private const int HOME = 0x24;
    private const int LEFT = 0x25;
    private const int UP = 0x26;
    private const int RIGHT = 0x27;

    private const int DOWN = 0x28;

    //---
    private const int WM_KEYDOWN = 0x100;

    private const string NON_LITERALS = "09#L?&CAa.,:/$<>|";


    private const int WM_PASTE = 0x302;
    private const int EM_SETSEL = 0x00B1;
    private const int WM_LBUTTONDBLCLK = 0x203;
    private const int WM_MOUSEMOVE = 0x200;
    private const int WM_CHAR = 0x102;

    private static readonly Dictionary<char, IList<char>> htAllowedCharsBoth = new();
    private static readonly Dictionary<char, IList<char>> htAllowedCharsUpper = new();
    private static readonly Dictionary<char, IList<char>> htAllowedCharsLower = new();
    private bool allowEMSETSEL;
    private T currentValue;

    private bool isBackspacing;

    //-----------
    private bool isCanceling;
    private bool isDeleting;
    private bool isKeyUpDown;
    private bool isValidatedText;
    private bool isValueSetting;
    private int lastSelectedCaret;
    private int lastSelectionLength;
    private string maskChangingDesiredText;
    private string oldOldText;
    private string oldText;

    private string runningText = "";
    private IUpDown spinner;
    private bool wasPaste; // was the OnTextChanged event caused by a Paste action

    static MaskedTextBox()
    {
        var digits = new List<char>();
        var uppers = new List<char>();
        var lowers = new List<char>();
        for (var c = '0'; c <= '9'; c++)
            digits.Add(c);

        for (var c = 'A'; c <= 'Z'; c++)
            uppers.Add(c);

        for (var c = 'a'; c <= 'z'; c++)
            lowers.Add(c);

        var dHash = Combine(digits, new[] { '+', '-' }); // space is not allowed
        var d0 = digits; // Combine(digits, new [] { ' ' });
        var both = Combine(uppers, lowers);

        // Note: For digits, the space character is automatically subsituted with the
        // prompt character (typically 0), so do not include space 0the space character 
        htAllowedCharsBoth['9'] = digits;
        htAllowedCharsBoth['0'] = d0;
        htAllowedCharsBoth['#'] = dHash;
        htAllowedCharsBoth['L'] = both;
        htAllowedCharsBoth['?'] = Combine(uppers, lowers, new[] { ' ' });
        htAllowedCharsBoth['C'] = both;
        htAllowedCharsBoth['A'] = both;
        htAllowedCharsBoth['a'] = both;
        htAllowedCharsBoth['&'] = both;
        //---
        htAllowedCharsUpper['9'] = digits;
        htAllowedCharsUpper['0'] = d0;
        htAllowedCharsUpper['#'] = dHash;
        htAllowedCharsUpper['L'] = uppers;
        htAllowedCharsUpper['?'] = Combine(uppers, new[] { ' ' });
        htAllowedCharsUpper['C'] = uppers;
        htAllowedCharsUpper['A'] = uppers;
        htAllowedCharsUpper['a'] = uppers;
        htAllowedCharsUpper['&'] = uppers;
        //---
        htAllowedCharsLower['9'] = digits;
        htAllowedCharsLower['0'] = d0;
        htAllowedCharsLower['#'] = dHash;
        htAllowedCharsLower['L'] = lowers;
        htAllowedCharsLower['?'] = Combine(lowers, new[] { ' ' });
        htAllowedCharsLower['C'] = lowers;
        htAllowedCharsLower['A'] = lowers;
        htAllowedCharsLower['a'] = lowers;
        htAllowedCharsLower['&'] = lowers;
    }
    //------------

    public MaskedTextBox() : this(true)
    {
        
    }

    public MaskedTextBox(bool addUpDownSpinner)
    {
        Init(addUpDownSpinner);
    }

    public MaskedTextBox(string mask, bool addUpDownSpinner = true) : base(mask)
    {
        Init(addUpDownSpinner);
    }

    /// <summary>
    ///     A token is a continuous sequence of characters that either all editable or all literals. Tokens that
    ///     are literals can supply a IValueTextMap to provide custom text values. The Tokens collection is automatically
    ///     updated when the Mask is changed.
    /// </summary>
    public IList<Token> Tokens { get; private set; }

    /// <summary>
    ///     A token is always selected. This mimics the DateTimePicker input. Call MimicDateTimePicker() to
    ///     apply all the input idiosyncrasies.
    /// </summary>
    public bool KeepTokenSelected { get; set; }

    /// <summary>
    ///     An option to have the highlight select the entire token, including the leading or trailing whitespaces,
    ///     or to only select the non-whitespace characters. The default value is to exclude the whitespace from the
    ///     selection.
    /// </summary>
    public bool KeepSelectedIncludesWhitespace { get; set; }

    /// <summary>
    ///     An option to hide the caret. By default, the caret is visible. The MimicDateTimePicker() method
    ///     sets CaretVisible to false.
    /// </summary>
    public bool CaretVisible { get; set; }

    /// <summary>
    ///     If the caret is at the leftmost or rightmost edge, then pressing the left arrow or right arrow
    ///     will move the caret (or selected token) to the other side.
    /// </summary>
    public bool CaretWrapsAround { get; set; }

    /// <summary>
    ///     When the key up, key down, page up, page down, mouse wheel, or spinner buttons change the value to
    ///     a value that is outside the token's range [min, max), then this variable can determine if the value wraps
    ///     to the value on the opposite side of the range. This can also be granularly set at each token.
    ///     IMPORTANT: If ValuesCarryOver is true and the Token has a CarryScope > 0, then the value will only wrap if
    ///     the tokens to the left can give or accept a carry.
    /// </summary>
    public bool ValuesWrapAround { get; set; }

    /// <summary>
    ///     Specifies if the value will wrap around if there is no carry room. The default value is true.
    ///     For example, if the Mask is 0:00, and the current value is 9:99, then increasing the right token
    ///     will do nothing if this value is false, otherwise the value 99 will change to 00.
    /// </summary>
    public bool ValuesWrapIfNoCarryRoom { get; set; }

    /// <summary>
    ///     When the value of a token is increased beyond the range, there is an option to have the tokens
    ///     to the left increase or decrease. The carry overs stop when a token is reached that does not allow carry over,
    ///     the carry scope is reached, a token contains a non-integer value, or if the token is the leftmost column and
    ///     it has no carries to give or take. The carry operations always use the token's SmallIncrement value.
    /// </summary>
    public bool ValuesCarryOver { get; set; }

    /// <summary>
    ///     Specifies the default value for token's that do not have their ByDigit value set. ByDigit is ignored
    ///     if KeepTokenSelected is true. The default value is false.
    /// </summary>
    public bool ByDigit { get; set; }

    /// <summary>
    ///     Split characters are used by the parser to determine which of the reserved culture characters, should cause
    ///     the token to split. In order to take effect, this property must be set before the Mask property is changed.
    /// </summary>
    public char[] SplitChars { get; set; }

    /// <summary>
    ///     Specifies the behaviour to use when the delete key is pressed, SelectionLength is zero, and the caret is
    ///     on an editable token, but not a literal with custom values. If true, then the character at the caret is delete, and
    ///     the text to the right will shift left to fill the gap. The caret stays where it is. If false, then the character
    ///     at the caret is deleted, and the caret hops over the character. Note: The token's PadChar is used to fill the hole
    ///     (if specified), otherwise the PromptChar is used.
    /// </summary>
    public bool DeleteKeyShiftsTextLeft { get; set; }

    /// <summary>
    ///     When typing in text, if the number of typed characters equals the number of editable characters in the selected
    ///     text, then
    ///     next character typed replaces all of the previous typed characters.
    /// </summary>
    public bool ChopRunningText { get; set; }

    /// <summary>
    ///     When text is typed or pasted, if the text exceeds the max value, then the text is replaced is replaced with the
    ///     token's
    ///     MaxValue minus the small increment. Otherwise the value doesn't change. Only applies when KeepSelectedToken is
    ///     true.
    /// </summary>
    public bool UseMaxValueIfTooLarge { get; set; }

    ///<summary>Pressing the escape key changes back to the previous text before text was pasted or typed.</summary>
    public bool EscapeKeyRevertsValue { get; set; }

    /// <summary>
    ///     When text is inputted and the values are less than the minimum values, then the existing text can be fixed by
    ///     reverting to the previous text, or by selecting the text with the closest value. The default value is to use the
    ///     previous
    ///     text that was valid.
    /// </summary>
    public ValueFixMode ValueTooSmallFixMode { get; set; }

    /// <summary>
    ///     When text is inputted and the values are less than the minimum values, then the existing text can be fixed by
    ///     reverting to the previous text, or by selecting the text with the closest value. The default value is to use the
    ///     previous
    ///     text that was valid.
    /// </summary>
    public ValueFixMode ValueTooLargeFixMode { get; set; }

    /// <summary>
    ///     Get or set a spinner for this control, which typically contains an up button and a down button.
    ///     The Spinner must extend control and implement the IUpDown interface.
    /// </summary>
    public IUpDown Spinner
    {
        get => spinner;
        set
        {
            if (spinner != null)
            {
                spinner.DownClicked -= Spinner_DownClicked;
                spinner.UpClicked -= Spinner_UpClicked;
                Controls.Remove((Control)spinner);
            }

            if (value != null)
            {
                if (!(value is Control))
                    throw new ArgumentException("Spinner must extend Control.");

                value.DownClicked += Spinner_DownClicked;
                value.UpClicked += Spinner_UpClicked;
                Controls.Add((Control)value);
            }

            spinner = value;
        }
    }

    public virtual T Value
    {
        get => currentValue;
        set => SetValueInternal(value);
    }

    public Token PreviousSelectedToken { get; private set; }

    /// <summary>
    ///     Fires an event if the text is changed and the corresponding value of that text is different than the current value,
    ///     or when the Value property is changed.
    /// </summary>
    public event ValueChangedEventHandler<T> ValueChanged;

    /// <summary>
    ///     Fires an event if the text is changed and the corresponding value of that text is different than the current value,
    ///     or when the Value property is changed. The event can be canceled, or the NewValue can be redefined.
    /// </summary>
    public event ValueChangingEventHandler<T> ValueChanging;

    ///<summary>Fires when a literal token has a custom values list, and the value of the literal is changing.</summary>
    public event TokenChangeEventHandler TokenChanging;

    /// <summary>
    ///     Fires when the Delete key or Backspace key is pressed and the caret is on a token that can be edited,
    ///     or the selected text range contains a token that can be edited.
    /// </summary>
    public event TokenDeleteEventHandler TokenDeleting;

    private void Init(bool addUpDownSpinner)
    {
        TextMaskFormat = MaskFormat.IncludePromptAndLiterals;
        CutCopyMaskFormat = MaskFormat.IncludePromptAndLiterals;
        AllowPromptAsInput = true;
        InsertKeyMode = InsertKeyMode.Overwrite;
        HideSelection = true;
        ResetOnSpace = true; // pressing space reverse to PromptChar
        ResetOnPrompt = true;
        //---
        KeepTokenSelected = false;
        CaretVisible = true;
        CaretWrapsAround = true;
        ValuesWrapAround = true;
        ValuesWrapIfNoCarryRoom = true;
        ValuesCarryOver = false;
        ByDigit = false;
        DeleteKeyShiftsTextLeft = true;

        if (addUpDownSpinner)
            Spinner = new SpinControl(); //new UpDownSpinner();

        AutoSize = true;
        SetAutoSizeMode(AutoSizeMode.GrowAndShrink);
        Size = GetPreferredSize(Size.Empty);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        PerformAutoSize();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        PerformAutoSize();
    }

    private void PerformAutoSize()
    {
        if (AutoSize)
        {
            var s = GetPreferredSize(Size.Empty);
            if (GetAutoSizeMode() == AutoSizeMode.GrowAndShrink)
            {
                Size = s;
            }
            else
            {
                var s2 = Size;
                s.Width = Math.Max(s.Width, s2.Width);
                s.Height = Math.Max(s.Height, s2.Height);
                Size = s;
            }
        }
    }

    private void Spinner_UpClicked(object sender, EventArgs e)
    {
        KeyUpDown(1);
    }

    private void Spinner_DownClicked(object sender, EventArgs e)
    {
        KeyUpDown(-1);
    }

    ///<summary>Returns the editable token at the specified caret, or null if there is no editable token at that position.</summary>
    public Token TokenAt(int caret)
    {
        for (var i = 0; i < Tokens.Count; i++)
        {
            var t = Tokens[i];
            if (caret >= t.StartIndex && t.CanEdit)
            {
                var dex = t.StartIndex + t.Length;
                if (caret < dex) return t;

                if (caret == dex)
                {
                    if (i == Tokens.Count - 1 || !Tokens[i + 1].CanEdit)
                        return t;

                    // Special case: If the caret is at the end of a token, and the start of the next
                    // is a space, then it looks more natural to use the current token.
                    var text = GetText(MaskFormat.IncludePromptAndLiterals);
                    var xx = t.StartIndex + t.Length;
                    if (xx < text.Length && char.IsWhiteSpace(text[xx]))
                        return t;
                }
            }
        }

        return null;
    }

    ///<summary>Subclasses must provide an implementation of this method.</summary>
    public abstract T TextToValue(string text);

    ///<summary>Subclasses must provide an implementation of this method.</summary>
    public abstract string ValueToText(T value);

    protected virtual void OnValueChanged(ValueChangedEventArgs<T> e)
    {
        if (ValueChanged != null)
            ValueChanged(this, e);
    }

    protected virtual void OnValueChanging(ValueChangingEventArgs<T> e)
    {
        if (ValueChanging != null)
            ValueChanging(this, e);
    }

    // returns true if the event was canceled
    protected virtual bool SetValueInternal(T newValue)
    {
        if (isValueSetting || (currentValue != null && currentValue.Equals(newValue)) ||
            (currentValue == null && newValue == null))
            return false;

        var e1 = new ValueChangingEventArgs<T>(currentValue, newValue);
        OnValueChanging(e1);
        if (e1.Cancel)
            return true;

        var text = ValueToText(newValue);
        if (text != null)
        {
            isValueSetting = true;
            isValidatedText = true;
            // don't reset running text here because new values will be set
            // while the user is typing.
            //runningText = "";
            Text = text;
            isValidatedText = false;
            isValueSetting = false;
        }

        newValue = e1.NewValue;
        var e2 = new ValueChangedEventArgs<T>(currentValue, newValue);
        currentValue = newValue;
        OnValueChanged(e2);
        return false;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        //if (e.KeyCode == Keys.Home || e.KeyCode == Keys.End || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) {
        lastSelectedCaret = SelectionStart;
        lastSelectionLength = SelectionLength;
        //}

        if (lastSelectionLength > 0)
        {
            var text = GetText(MaskFormat.IncludePromptAndLiterals);
            var subtext = text.Substring(lastSelectedCaret, lastSelectionLength);
            var nf = CultureInfo.CurrentCulture.NumberFormat;
            if (e.KeyCode == Keys.OemPeriod)
            {
                if (subtext.IndexOf(nf.CurrencyDecimalSeparator) >= 0)
                    runningText += nf.CurrencyDecimalSeparator;
                else if (subtext.IndexOf(nf.NumberDecimalSeparator) >= 0)
                    runningText += nf.NumberDecimalSeparator;
                else if (subtext.IndexOf(nf.PercentDecimalSeparator) >= 0)
                    runningText += nf.PercentDecimalSeparator;
            }
            else if (e.KeyCode == Keys.Oemcomma)
            {
                if (subtext.IndexOf(nf.CurrencyGroupSeparator) >= 0)
                    runningText += nf.CurrencyGroupSeparator;
                else if (subtext.IndexOf(nf.NumberGroupSeparator) >= 0)
                    runningText += nf.NumberGroupSeparator;
                else if (subtext.IndexOf(nf.PercentGroupSeparator) >= 0)
                    runningText += nf.PercentGroupSeparator;
            }
        }
    }

    private static string ToDecimalFormat(int offset, string text, MaskedTextProvider mp)
    {
        var nf = CultureInfo.CurrentCulture.NumberFormat;
        var s = "";
        for (var i = 0; i < text.Length; i++)
        {
            var b = mp.IsEditPosition(offset + i);
            var c = text[i];
            if (b)
            {
                if (char.IsDigit(c))
                    s += '0';
                else
                    return null;
            }
            else
            {
                if (c.ToString() == nf.NumberGroupSeparator)
                    s += ',';
                else if (c.ToString() == nf.NumberDecimalSeparator)
                    s += '.';
                else
                    s += "'" + c + "'";
            }
        }

        return s;
    }

    // The default behavior of the MaskedTextBox is to shift text to the right when new
    // text is pasted. Or shift text to the left when text is selected and a key is typed
    // This method keeps the text more consistent.
    private string GetIdealText(string currentText, string newText, out string insertedText)
    {
        insertedText = null;
        if (currentText == null)
            return newText;

        if (wasPaste || lastSelectionLength > 0)
        {
            //String insertedText = null;
            var mp = Tokens[0].OwnerTextMaskProvider;
            var padRule = TokenValuePadRule.Default;
            char? padChar = null;
            if (PreviousSelectedToken != null)
            {
                padChar = PreviousSelectedToken.PadChar;
                padRule = PreviousSelectedToken.PadRule;
            }

            //else {
            //	padChar = PromptChar;
            //}
            // is there any case where existing letters in the selection should not have the PromptChar filled in?
            if (!padChar.HasValue)
                padChar = PromptChar;


            if (wasPaste)
            {
                if (lastSelectionLength == 0)
                {
                    //------------- possibly need to fake selection length if it is currently 0??
                }

                insertedText = Clipboard.GetText();
                //if (insertedText.Length > lastSelectionLength && lastSelectionLength > 0)
                //	insertedText = insertedText.Substring(0, lastSelectionLength);

                wasPaste = false;
                runningText = insertedText;
            }
            else
            {
                if (isBackspacing)
                {
                    isBackspacing = false;
                    insertedText = "";
                    if (!padChar.HasValue)
                        padChar = PromptChar; // otherwise existing characters are kept
                }
                else
                {
                    var x = lastSelectedCaret;

                    for (; x < newText.Length; x++)
                        if (mp.IsEditPosition(x))
                            break;
                    if (x < newText.Length)
                    {
                        var c = newText[x];
                        insertedText = c.ToString();
                    }
                }

                if (lastSelectionLength > 0)
                {
                    if (ChopRunningText)
                    {
                        var isFull = false;
                        TextOverlay.Apply(runningText, currentText, lastSelectedCaret, lastSelectionLength, mp, padRule,
                            padChar, false, out isFull);
                        if (isFull)
                            runningText = "";
                    }

                    runningText += insertedText;
                    insertedText = runningText;
                }
                else
                {
                    runningText = "";
                }
            }

            if (KeepTokenSelected && UseMaxValueIfTooLarge)
            {
                var currenTokenText = currentText.Substring(lastSelectedCaret, lastSelectionLength);
                var wf = false;
                insertedText = ValidateTokenText(PreviousSelectedToken, currentText, insertedText, out wf);
            }

            var isFull2 = false;
            var idealText = TextOverlay.Apply(insertedText, currentText, lastSelectedCaret, lastSelectionLength, mp,
                padRule, padChar, false, out isFull2);
            var newText2 = currentText.Substring(0, lastSelectedCaret) + idealText +
                           currentText.Substring(lastSelectedCaret + lastSelectionLength);
            if (mp.VerifyString(newText2))
                newText = newText2;
        }


        return newText;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        if (isCanceling)
            return;

        if (allowEMSETSEL && !isValidatedText)
            // PreProcessMessage sets allowEMSETSEL to true when a key is typed
            // setting allowEMSETSEL back to false too soon prevents the default
            // MaskedTextBox behavior from moving the caret. So BeginInvoke is used
            // to give more of a delay
            BeginInvoke((Action)delegate { allowEMSETSEL = false; });

        if (maskChangingDesiredText != null)
        {
            // the text might be the same as before, or the MaskedTextBox might butcher the text
            // if the resulting text is the desired text, then the event is allowed
            // if the resulting text is different, it means the Mask butchered the text to some
            // intermediate unwanted value, so the text event is blocked. There will be a
            // assignment of the Text property to the desired value, which will fire another OnTextChanged
            // event that won't be blocked.
            var textAfterMaskChanged = GetText(MaskFormat.IncludePromptAndLiterals);
            if (textAfterMaskChanged != maskChangingDesiredText)
                return;
        }

        var text = GetText(MaskFormat.IncludePromptAndLiterals);
        if (!isValidatedText && maskChangingDesiredText == null && !isKeyUpDown)
        {
            var idealText = text;
            if (!isDeleting)
            {
                string insertedText = null;
                idealText = GetIdealText(oldText, text, out insertedText);
            }

            var wasFixed = false;
            var text2 = ValidateNewText(idealText, out wasFixed);
            if (wasFixed && lastSelectionLength > 0)
            {
                var xyz = SelectionLength;
                // if the text needed to be fixed then reset the runningText
                runningText = "";
                string insertedText = null;
                idealText = GetIdealText(oldText, text, out insertedText);
                text2 = ValidateNewText(idealText, out wasFixed);
            }

            if (text != text2)
            {
                if (wasFixed)
                    runningText = "";

                try
                {
                    isValidatedText = true;
                    Text = text2;
                }
                finally
                {
                    isValidatedText = false;
                }

                return;
            }
        }

        base.OnTextChanged(e);

        var newValue = TextToValue(text);
        var wasCanceled = SetValueInternal(newValue);

        if (wasCanceled)
        {
            // the Text needs to be set to the oldText
            // the oldText needs to be set to the oldOldText
            isCanceling = true;
            Text = oldText;
            isCanceling = false;
            oldText = oldOldText;
            InternalSelect(lastSelectedCaret, lastSelectionLength);
        }
        else
        {
            oldOldText = oldText;
            oldText = text;

            if (KeepTokenSelected)
            {
                if (PreviousSelectedToken != null)
                    SelectToken(PreviousSelectedToken);
            }
            else
            {
                var KeepTextSelected = true;
                if (lastSelectionLength > 0)
                {
                    //allowEMSETSEL = false;
                    if (KeepTextSelected)
                        InternalSelect(lastSelectedCaret, lastSelectionLength);
                    else
                        InternalSelect(lastSelectedCaret + lastSelectionLength, 0);
                }
            }
        }
    }

    public string GetText(MaskFormat format)
    {
        var origFormat = TextMaskFormat;
        TextMaskFormat = format;
        var t = Text;
        TextMaskFormat = origFormat;
        return t;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);

        // only works when called from OnGotFocus
        if (CaretVisible)
            MaskedTextBoxWin32.ShowCaret(Handle);
        else
            MaskedTextBoxWin32.HideCaret(Handle);

        //if (lastSelectionStart.HasValue) {
        //	InternalSelect(lastSelectionStart.Value, lastSelectionLength);
        //	lastSelectionStart = null;
        //}

        if (KeepTokenSelected && SelectionLength == 0)
            //Point p = Point.Empty;
            //GetCaretPos(out p);
            //int caret = GetCharIndexFromPosition(p);
            foreach (var t in Tokens)
                if (t.CanEdit)
                {
                    SelectToken(t);
                    break;
                }
    }

    private void SelectToken(int caret)
    {
        var t = TokenAt(caret);
        if (t != null)
            SelectToken(t);
    }

    private void SelectToken(Token t)
    {
        PreviousSelectedToken = t;
        var x = t.StartIndex;
        var len = t.Length;
        var includeWhitespace = t.KeepSelectedIncludesWhitespace.HasValue
            ? t.KeepSelectedIncludesWhitespace.Value
            : KeepSelectedIncludesWhitespace;

        if (!includeWhitespace)
        {
            var s = GetText(MaskFormat.IncludePromptAndLiterals);
            // Probably doesn't make sense to trim a non-white padding char,
            // as it would look weird.
            var k1 = 0;
            var k2 = 0;
            for (var i = x; i < x + len; i++)
                if (char.IsWhiteSpace(s[i]))
                    k1++;
                else
                    break;
            for (var i = x + len - 1; i >= x; i--)
                if (char.IsWhiteSpace(s[i]))
                    k2++;
                else
                    break;

            if (len - (k1 + k2) > 0)
            {
                len = len - (k1 + k2);
                x += k1;
            }
        }

        InternalSelect(x, len);
    }

    private void InternalSelect(int start, int length)
    {
        allowEMSETSEL = true;
        Select(start, length);
        lastSelectedCaret = start;
        lastSelectionLength = length;
        allowEMSETSEL = false;
    }

    private static bool IsLiteral(char c)
    {
        if (c == '\\')
            return true;

        return NON_LITERALS.IndexOf(c) < 0;
    }

    /// <summary>
    ///     Parses the mask according to the following rules:
    ///     Sequences of literals, e.g. \L\L\L are put into their own token.
    ///     Sequences of non-literals, e.g. 999 are put into their own token.
    ///     Split chars is a subset of the special culture characters reserved by MaskedTextBox.
    ///     The default splitChars used are the colon, dollar sign, and forward slash.
    ///     For example, "9999/99/99", produces 5 tokens: [9999, /, 99, /, 99]
    ///     It is done this way so that custom lists can be assigned to the tokens.
    ///     Multiple split chars in a row are grouped into a single token.
    ///     For example, 9::9.99\A\A, produces 4 tokens [9, ::, 9.99, AA]
    /// </summary>
    public static IList<Token> DefaultMaskParser(string mask, string displayText, MaskedTextProvider mp,
        IList<Token> tokens, char[] splitChars)
    {
        if (splitChars == null)
            splitChars = new[] { ':', '$', '/' };

        if (tokens != null)
        {
            // try to preserve the original list
            if (tokens.IsReadOnly)
                tokens = new List<Token>(tokens);
        }
        else
        {
            tokens = new List<Token>();
        }

        var pendingBackslash = false;
        var s = "";
        var raw = "";
        var startIndex = 0;
        var startIndexRaw = 0;
        var kk = 0;

        for (var h = 0; h < mask.Length; h++)
        {
            var c = mask[h];
            var newToken = false;

            if (pendingBackslash)
            {
                // current sequence must be a literal, so just append
                s += c; // e.g. \L\L\L   c == 'L'
                raw += c;
                pendingBackslash = false;
            }
            else
            {
                if (IsLiteral(c))
                {
                    if (c == '\\')
                        pendingBackslash = true;

                    if (raw.Length == 0 || IsLiteral(raw[0]))
                    {
                        if (c != '\\')
                            s += c;
                        raw += c; // Note: 's' does not append c since s is unescaped
                    }
                    else
                    {
                        newToken = true; // transitioning from a non-literal token to a literal token
                    }
                }
                else if (((IList<char>)splitChars).Contains(c))
                {
                    if (raw.Length == 0 || ((IList<char>)splitChars).Contains(raw[0]))
                    {
                        raw += c;
                        s += c;
                    }
                    else
                    {
                        newToken = true; // transitioning from a non-split token to a split token
                    }
                }
                else
                {
                    if (raw.Length == 0 || (!IsLiteral(raw[0]) && !((IList<char>)splitChars).Contains(raw[0])))
                    {
                        if (c != '>' && c != '<' && c != '|')
                            s += c;
                        raw += c;
                    }
                    else
                    {
                        newToken = true; // transitioning from non-edit token to an edit token
                    }
                }
            }

            if (newToken || h == mask.Length - 1)
            {
                var text = displayText.Substring(startIndex, s.Length);
                Token t = null;
                if (kk < tokens.Count)
                {
                    t = tokens[kk];
                }
                else
                {
                    t = new Token(kk, text, raw);
                    tokens.Add(t);
                }

                t.Text = text;
                t.MaskRaw = raw;
                t.StartIndex = startIndex;
                t.MaskRawStartIndex = startIndexRaw;
                t.IsLiteral =
                    IsLiteral(raw[0]) ||
                    ((IList<char>)splitChars)
                    .Contains(raw[0]); // split chars are flagged as literals since by default they cannot be edited
                t.OwnerTextMaskProvider = mp;

                startIndex += s.Length;
                startIndexRaw += raw.Length;

                kk++;

                raw = "";
                s = "";

                if (newToken)
                {
                    raw += c;
                    // these characters never appear at the start of a mask text
                    if (c != '>' && c != '<' && c != '|' && c != '\\')
                        s += c;
                }
            }
        }

        bool? toUpper = null;
        var s1 = "";
        var toUppers = new List<bool?>();
        for (var i = 0; i < mask.Length; i++)
        {
            var c = mask[i];
            if (c == '\\')
            {
                i++; // next character will always be skipped
                // should always be true
                if (i < mask.Length)
                {
                    c = mask[i];
                    s1 += c;
                    toUppers.Add(toUpper);
                }
            }
            else if (c == '>')
            {
                toUpper = true;
            }
            else if (c == '<')
            {
                toUpper = false;
            }
            else if (c == '|')
            {
                toUpper = null;
            }
            else
            {
                s1 += c;
                toUppers.Add(toUpper);
            }
        }

        foreach (var t in tokens)
        {
            t.Mask = s1.Substring(t.StartIndex, t.Length);
            var shifts = new bool?[t.Length];
            for (int i = 0, k = t.StartIndex; i < t.Length; i++, k++) shifts[i] = toUppers[k];
            t.Shifts = shifts;
        }

        //var tokens2 = new List<Token>();
        //for (int i = 0; i <= text.Length; i++)
        //	tokens2.Add(TokenAt(i));

        return tokens;
    }

    private static void VerifyParser()
    {
        TestParser(@"", "", new string[0], new string[0], new bool[0]);
        TestParser(@">", "", new[] { "" }, new[] { ">" }, new[] { false });
        TestParser(@"\L", "A", new[] { "A" }, new[] { "\\L" }, new[] { true });
        TestParser(@"\L\L", "AA", new[] { "AA" }, new[] { "\\L\\L" }, new[] { true });
        TestParser(@"\\", "\\", new[] { "\\" }, new[] { @"\\" }, new[] { true });
        TestParser(@"\\\\", "\\\\", new[] { "\\\\" }, new[] { @"\\\\" }, new[] { true });
        TestParser(@":", ":", new[] { ":" }, new[] { ":" }, new[] { true });
        TestParser(@"::", "::", new[] { "::" }, new[] { "::" }, new[] { true });
        TestParser(@"9999/99/99", "9999/99/99", new[] { "9999", "/", "99", "/", "99" },
            new[] { "9999", "/", "99", "/", "99" }, new[] { false, true, false, true, false });
        TestParser(@"99:99:99.999\ \A\M", "99:99:99.999 AM", new[] { "99", ":", "99", ":", "99.999", " AM" },
            new[] { "99", ":", "99", ":", "99.999", @"\ \A\M" }, new[] { false, true, false, true, false, true });
        TestParser(@"\T\i\m\e:99:99", "Time:99:99", new[] { "Time", ":", "99", ":", "99" },
            new[] { @"\T\i\m\e", ":", "99", ":", "99" }, new[] { true, true, false, true, false });
        TestParser(@"00\A\A\A\A\A\A\A\A\A\A00", "00AAAAAAAAAA00", new[] { "00", "AAAAAAAAAA", "00" },
            new[] { "00", @"\A\A\A\A\A\A\A\A\A\A", "00" }, new[] { false, true, false });
        TestParser(@"\A\A\A\A\A\A\A\A\A\A00", "AAAAAAAAAA00", new[] { "AAAAAAAAAA", "00" },
            new[] { @"\A\A\A\A\A\A\A\A\A\A", "00" }, new[] { true, false });
        TestParser(@"\A\A\A>>>\a\a\a", "AAAaaa", new[] { "AAA", "", "aaa" }, new[] { @"\A\A\A", ">>>", @"\a\a\a" },
            new[] { true, false, true });
        TestParser(@"\A99\L\e\t\t\e\r\s:Aa#CL?&::99:99.9999", "A00Letters:0000000::00:00.0000",
            new[] { "A", "00", "Letters", ":", "0000000", "::", "00", ":", "00.0000" },
            new[] { "\\A", "99", @"\L\e\t\t\e\r\s", ":", "Aa#CL?&", "::", "99", ":", "99.9999" },
            new[] { true, false, true, true, false, true, false, true, false });
        TestParser(@">\ZAAAAA<aaa\1\2\3\\||AAA", @"ZAAAAAaaa123\AAA", new[] { "", "Z", "AAAAAaaa", "123\\", "AAA" },
            new[] { ">", "\\Z", "AAAAA<aaa", @"\1\2\3\\", "||AAA" }, new[] { false, true, false, true, false });
        TestParser(@"Time 00:00:00.000", "Time 00:00:00.000", new[] { "Time ", "00", ":", "00", ":", "00.000" },
            new[] { "Time ", "00", ":", "00", ":", "00.000" }, new[] { true, false, true, false, true, false });
        //TestParser(@"", "", new [] { }, new [] { }, new [] { });
    }

    private static void TestParser(string mask, string displayText, string[] texts, string[] raws, bool[] isLiteral)
    {
        var tokens = DefaultMaskParser(mask, displayText, null, null, null);
        if (tokens.Count != texts.Length)
            throw new Exception(string.Format("Expected {0} tokens but got {1}.", texts.Length, tokens.Count));

        for (var i = 0; i < texts.Length; i++)
        {
            var t = tokens[i];
            if (t.Text != texts[i])
                throw new Exception(string.Format("Expected text '{0}' but got '{1}'.", texts[i], t.Text));
            if (t.MaskRaw != raws[i])
                throw new Exception(string.Format("Expected raw mask '{0}' but got '{1}'.", raws[i], t.MaskRaw));
            if (t.IsLiteral != isLiteral[i])
                throw new Exception("Token IsLiteral was an unexpected value: " + t.IsLiteral);
        }
    }

    protected virtual void ParseMask()
    {
        var text = GetText(MaskFormat.IncludePromptAndLiterals);
        var mp = MaskedTextProvider;
        var m = mp.Mask;
        var tokens = DefaultMaskParser(m, text, mp, Tokens, SplitChars);
        Tokens = tokens;
    }

    /// <summary>
    ///     The mask is parsed into tokens. If a previous mask was already parsed, then the existing tokens are
    ///     updated so that any customized token settings are preserved. To create all new tokens then set the Tokens
    ///     property to null before changing the Mask.
    /// </summary>
    protected override void OnMaskChanged(EventArgs e)
    {
        base.OnMaskChanged(e);
        ParseMask();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        runningText = "";
        if (KeepTokenSelected)
        {
            var t = TokenAt(SelectionStart);
            if (t == null)
                t = PreviousSelectedToken;
            SelectToken(t);
        }
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        lastSelectedCaret = SelectionStart;
        lastSelectionLength = SelectionLength;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // otherwise if text is selected the mouse is dragged
        // outside the MaskedTextBox bounds, the spinner turns
        // completely white
        if (Spinner != null && e.Button == MouseButtons.Left)
            Spinner.Refresh();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.Delta > 0)
            KeyUpDown(1);
        else
            KeyUpDown(-1);
    }

    private static IList<char> Combine(params IList<char>[] arr)
    {
        var list = new List<char>();
        foreach (var list2 in arr)
            list.AddRange(list2);
        return list;
    }

    /// <summary>
    ///     This method is called when a Literal token has a CustomValues list.
    /// </summary>
    protected virtual void OnTokenChanging(TokenChangeArgs e)
    {
        if (TokenChanging != null)
            TokenChanging(this, e);
    }

    /// <summary>
    ///     Increments or decrements the value of the currently selected token by a certain amount.
    ///     If amountIsSymbolic is true, then the amount can be +/- 1 which map to the token's SmallIncrement
    ///     value, or +/- 2, which maps to the token's BigIncrement value. If amountIsSymbolic is false, then
    ///     the actual amount is used.
    /// </summary>
    public virtual void KeyUpDown(decimal amount, bool amountIsSymbolic = true, Token token = null, bool select = true)
    {
        if (amount == 0)
            return;

        runningText = "";
        wasPaste = false;
        string t = null;
        var x = 0; //SelectionStart;
        var length = 0; //SelectionLength;
        if (token == null)
        {
            x = SelectionStart;
            length = SelectionLength;
            // preference is given to the left token when the caret is on a boundary,
            // so if the selection length > 0 then add 1 to the caret position to make
            // sure the correct token is selected.
            var x2 = x + length; //(length > 0 ? x+1 : x);
            token = TokenAt(x2);
        }
        else
        {
            x = token.StartIndex;
            length = token.Length;
        }

        // token null happens when the caret is in the middle
        // of a literal token with length >= 2
        if (token == null)
            return;

        if (token.ReverseUpDown)
            amount = -amount;

        if (token.CustomValues != null)
        {
            if (token.CustomValues.Count == 0)
                return;

            var list = token.CustomValues;
            var s = GetText(MaskFormat.IncludePromptAndLiterals);
            var oldText = s.Substring(token.StartIndex, token.Length);
            var oldTextWithPadding = oldText;
            if (!token.IsLiteral)
            {
                oldText = oldText.Trim(PromptChar);
                if (ResetOnSpace) oldText = oldText.Replace(PromptChar, ' ');
            }
            else
            {
                if (token.PadChar.HasValue)
                    oldText = oldText.Trim(token.PadChar.Value);
            }

            var index = -1;
            for (var i = 0; i < list.Count; i++)
            {
                var o = list[i];
                var p = o == null ? "" : o.ToString();
                if (!ResetOnSpace && !token.IsLiteral && PromptChar != ' ')
                    // for some reason, spaces are deleted by the MaskedTextBox when
                    // the PromptChar is not a space
                    p = p.Replace(" ", "");
                if (string.Compare(p, oldText, true) == 0)
                {
                    index = i;
                    break;
                }
            }

            var dir = amount < 0 ? -1 : 1;
            var wrap = token.ValueWrapsAround.HasValue ? token.ValueWrapsAround.Value : ValuesWrapAround;
            if (!wrap && ((index == 0 && dir < 0) || (index == list.Count - 1 && dir > 0)))
                return;

            if (index < 0 && dir < 0)
                index = 0;
            index += dir;

            if (index < 0)
                index = list.Count - 1;
            else if (index >= list.Count)
                index = 0;

            var newText = list[index].ToString();
            if (token.IsLiteral)
            {
                // the literal has the option of maintaining its length, or changing to a different length
                var newTextWithPadding = newText;
                if (token.PadChar.HasValue)
                {
                    var c = token.PadChar.Value;
                    if (token.PadRule == TokenValuePadRule.Left)
                        newTextWithPadding = newText.PadLeft(token.Length, c);
                    else
                        newTextWithPadding = newText.PadRight(token.Length, c);
                }

                var newTextEscaped = "";
                foreach (var c in newTextWithPadding)
                    newTextEscaped += "\\" + c;

                var oldTextEscaped = "";
                foreach (var c in oldTextWithPadding)
                    oldTextEscaped += "\\" + c;

                // now the mask needs to be changed, which is going to recreate all the tokens
                var m = Mask;
                var xx = token.MaskRawStartIndex;

                var newMask =
                    m.Substring(0, xx) + newTextEscaped +
                    m.Substring(xx + token.MaskRaw.Length); //oldTextEscaped.Length);

                // since only a literal is changing, the new text can be determined without requiring the MaskedTextProvider.
                var newFullText = s.Substring(0, token.StartIndex) + newTextWithPadding +
                                  s.Substring(token.StartIndex + token.Length);

                // gaurantee the texts are different
                //String newTextWithPadding2 = null;
                //if (newTextWithPadding.IndexOf('A') >= 0)
                //	newTextWithPadding2 = new String('B', newTextWithPadding.Length);
                //else
                //	newTextWithPadding2 = new String('A', newTextWithPadding.Length);
                //String newFullTextDiff = s.Substring(0, token.StartIndex) + newTextWithPadding2 + s.Substring(token.StartIndex + token.Length);

                // subclasses may need to adjust the text before the mask is changed. For example, for a date-picker,
                // if the day is set to 30, but the month name was changed from January to February, then the day
                // should be changed to 28. Setting the Mask fires the Text Changed property, so the text needs
                // to be fixed before that.
                var args = new TokenChangeArgs(this, token);
                args.Mask = newMask;
                args.NewTokenText = newText;
                args.NewTokenTextPadded = newTextWithPadding;
                //args.NewLiteralEscaped = newTextEscaped;
                args.CurrentTokenText = oldText;
                args.CurrentTokenTextPadded = oldTextWithPadding;
                //args.CurrentLiteralEscaped = oldTextEscaped;
                args.Text = newFullText;

                OnTokenChanging(args);
                if (args.Cancel)
                    return;

                // I've seen strange behavior where changing the Mask from 00:00:00.000\ \A\M
                // to 00:00:00.000\ \P\M had the side effect that the displayed text was changed from
                // 01:00:00.000 AM to 10:00:00.000 PM. So that is why the text is always assigned.
                t = args.Text; // a listener might set .Text to null, which is OK, but the TextChanged/ValueChanged events might not be fired

                // Changing the Mask fires an OnTextChanged event E1. As noted, the Text might be butchered.
                // Since 't' is the final Text value, then the E1 event is ignored if the changing the Mask produces
                // an intermediate unwanted result. Changing the Mask can also produce the desired text, so in that
                // case the event E1 is allowed.
                try
                {
                    maskChangingDesiredText = t;
                    Mask = args.Mask;
                }
                finally
                {
                    maskChangingDesiredText = null;
                }
            }
            else
            {
                // non-literals are restricted to the original mask characters
                var newTextOrig = newText;

                if (newText.Length > token.Length)
                {
                    newText = newText.Substring(0, token.Length);
                }
                else
                {
                    // the text must always be padded so that it fills the mask
                    var c = ' ';
                    if (token.PadChar.HasValue)
                        c = token.PadChar.Value;

                    if (token.PadRule == TokenValuePadRule.Left)
                        newText = newText.PadLeft(token.Length, c);
                    else
                        newText = newText.PadRight(token.Length, c);
                }

                var fullText = s.Substring(0, token.StartIndex) + newText +
                               s.Substring(token.StartIndex + token.Length);
                var isValid = token.OwnerTextMaskProvider.VerifyString(fullText);

                var args = new TokenChangeArgs(this, token);
                args.CurrentTokenText = oldText;
                args.CurrentTokenTextPadded = oldTextWithPadding;
                args.NewTokenText = newTextOrig;
                args.NewTokenTextPadded = newText;
                args.IsTextValid = isValid;
                args.Text = fullText;

                OnTokenChanging(args);
                if (args.Cancel)
                    return;

                t = args.Text;
            }
        }
        else
        {
            var byDigit = false;
            if (!KeepTokenSelected && Math.Abs(amount) == 1)
            {
                byDigit = token.ByDigit.HasValue ? token.ByDigit.Value : ByDigit;
                if (!byDigit)
                {
                    // if byDigit is false, but the existing token cannot be parsed
                    // as a number, then the behavior falls back to byDigit, otherwise
                    // the text could not be changed.
                    var _t = GetText(MaskFormat.IncludePromptAndLiterals);
                    var t2 = _t.Substring(token.StartIndex, token.Length).Trim();
                    decimal val = 0;
                    if (t2.Length > 0 && !decimal.TryParse(t2, NumberStyles.Any, CultureInfo.CurrentCulture, out val))
                        byDigit = true;
                }
            }

            if (byDigit)
            {
                t = GetText(MaskFormat.IncludePromptAndLiterals);

                var y = x - token.StartIndex;
                var y2 = x;
                var range = 1;
                if (y >= token.Length)
                {
                    y = token.Length - 1;
                    y2--;
                    range = 0;
                }

                IList<char> allowedChars = null;
                var c = ' ';
                for (var i = 0; i <= range; i++)
                {
                    c = t[token.StartIndex + y];
                    var cMask = token.Mask[y];

                    if (token.ByDigitChars != null)
                    {
                        allowedChars = token.ByDigitChars;
                    }
                    else
                    {
                        var shift = token.Shifts[y];
                        if (shift.HasValue)
                        {
                            if (shift.Value)
                                htAllowedCharsUpper.TryGetValue(cMask, out allowedChars);
                            else
                                htAllowedCharsLower.TryGetValue(cMask, out allowedChars);
                        }
                        else
                        {
                            htAllowedCharsBoth.TryGetValue(cMask, out allowedChars);
                        }
                    }

                    if (allowedChars != null) break;

                    // this case happens when the cursor is on a period. E.g. 00.000
                    // the period does not have any associated allowedChars, so it
                    // is skipped, and the digit to the left is checked.
                    if (y > 0)
                    {
                        y--;
                        y2--;
                    }
                }

                if (allowedChars == null)
                    return;

                if (AllowPromptAsInput)
                {
                    var pc = PromptChar;
                    if (!allowedChars.Contains(pc))
                    {
                        var list2 = new List<char>();
                        list2.Add(pc);
                        list2.AddRange(allowedChars);
                        allowedChars = list2;
                    }
                }

                var dir = amount < 0 ? -1 : 1; // direction
                var index = allowedChars.IndexOf(c);
                // if 'c' does not exist, then if increasing
                // then the first character to show is arr[0]
                // Otherwise, if decreasing then the first
                // character to show is arr[arr.length - 1]
                if (index < 0 && dir < 0)
                    index = 0;

                string t2 = null;
                var mp = token.OwnerTextMaskProvider;
                for (var i = 0; i < allowedChars.Count; i++)
                {
                    index += dir;
                    var index2 = index;
                    if (index2 < 0)
                        index2 = allowedChars.Count + index2;
                    else if (index2 >= allowedChars.Count)
                        index2 = index2 - allowedChars.Count;

                    var c2 = allowedChars[index2];
                    if (c2 == c)
                        continue;

                    // test to see if c2 combined is a valid token
                    var t3 = t.Substring(0, y2) + c2 + t.Substring(y2 + 1);
                    if (mp.VerifyString(t3))
                    {
                        var t4 = t3.Substring(token.StartIndex, token.Length);
                        long val = 0;
                        // it should be always OK to TryParse the values, because the
                        // default MinValue, MaxValue is [0...0, 9...9], which means
                        // the input would only be rejected if the user changed the
                        // MinValue or MaxValue, which implies that it is correct to
                        // reject the value.
                        if (long.TryParse(t4, out val))
                            if (val < token.MinValue || val >= token.MaxValue)
                                continue;
                        t2 = t3;
                        break;
                    }
                }

                // Either all characters were rejected or it's not possible to change roll the digit
                // such that the min/max are obeyed
                if (t2 == null || t2 == t)
                    return;

                t = t2;
            }
            else
            {
                if (amountIsSymbolic)
                {
                    if (amount == 1)
                        amount = token.SmallIncrement;
                    else if (amount == -1)
                        amount = -token.SmallIncrement;
                    else if (amount == 2)
                        amount = token.BigIncrement;
                    else if (amount == -2)
                        amount = -token.BigIncrement;
                    else
                        amount = 0;
                }

                if (amount == 0)
                    return;

                var firstTokenCanEdit = 0;
                for (var i = 0; i < Tokens.Count; i++)
                    if (Tokens[i].CanEdit)
                    {
                        firstTokenCanEdit = i;
                        break;
                    }

                var text = GetText(MaskFormat.IncludePromptAndLiterals);
                var scopes = new List<int>();
                scopes.Add(ValuesCarryOver ? token.CarryOverScope : 0);
                if (ValuesWrapIfNoCarryRoom)
                    scopes.Add(0);

                // first, try to carry over values from the left tokens. However, if there is no carry room
                // then allow the value to wrap around if that option is set.
                for (var k = 0; k < scopes.Count; k++)
                {
                    var scope = scopes[k];
                    var origScope = scope;
                    var co = true;
                    var hadCarryRoom = true;
                    t = text;

                    for (var i = token.SeqNo; i >= 0 && (scope >= 0) & co; i--)
                    {
                        // if there are variable literals, then this will need to be fixed
                        // because the token.StartIndex and token.Length will be wrong
                        var tk = Tokens[i];
                        if (!tk.CanEdit)
                            continue;

                        if (tk != token)
                        {
                            amount = amount < 0 ? -tk.SmallIncrement : tk.SmallIncrement;

                            if (amount == 0)
                                break;
                        }

                        scope--;

                        var t2 = t.Substring(tk.StartIndex, tk.Length).Trim();
                        decimal val = 0;
                        if (t2.Length > 0 &&
                            !decimal.TryParse(t2, NumberStyles.Any, CultureInfo.CurrentCulture, out val))
                            break;

                        var origVal = val;
                        val += amount;
                        var wrapValue = tk.ValueWrapsAround.HasValue ? tk.ValueWrapsAround.Value : ValuesWrapAround;
                        var wasWrapped = false;
                        if (val >= tk.MaxValue)
                        {
                            // is origVal the maximum possible value?
                            var maxValue = GetMaxValue(tk);
                            var maxMinusOne = tk.MaxValue - tk.SmallIncrement;

                            if (origVal < maxValue)
                            {
                                if (maxMinusOne > origVal)
                                    val = maxMinusOne;
                                else
                                    val = maxValue;

                                co = false;
                            }
                            else
                            {
                                if (wrapValue)
                                {
                                    val = tk.MinValue;
                                    wasWrapped = true;
                                }
                                else
                                {
                                    val = maxValue;
                                    co = false;
                                }
                            }
                        }
                        else if (val < tk.MinValue)
                        {
                            if (wrapValue)
                            {
                                if (origVal != tk.MinValue)
                                {
                                    val = tk.MinValue;
                                    co = false;
                                }
                                else
                                {
                                    val = tk.MaxValue - tk.SmallIncrement;
                                    wasWrapped = true;
                                }
                            }
                            else
                            {
                                val = tk.MinValue;
                                co = false;
                            }
                        }
                        else
                        {
                            co = false;
                        }

                        // if at the last token, the the last token was wrapped, then there was no carry room
                        // if the caret is at the first first editable token, then that token is allowed to wrap
                        // by the 'token.Seq != i' statement.
                        if (((scope < 0 && origScope > 0) || (i == firstTokenCanEdit && token.SeqNo != i)) &&
                            wasWrapped)
                            hadCarryRoom = false;

                        // the decimal.ToString(format) is leveraged to produce the output string.
                        // which is convenient for masks which contain decimals and thousands separators.
                        var format = tk.Mask.Replace('9', '0').Replace('#', '0');
                        //String t3 = val.ToString().PadLeft(tk.Length, PromptChar);
                        var t3 = val.ToString(format, CultureInfo.CurrentCulture);
                        var t4 = t.Substring(0, tk.StartIndex) + t3 + t.Substring(tk.StartIndex + tk.Length);
                        t = t4;
                    }

                    if (!hadCarryRoom)
                    {
                        if (k == scopes.Count - 1)
                            return; // will happen if ValuesWrapIfNoCarryRoom is false
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        // t is null when changing a literal that has custom values
        if (t != null)
        {
            isKeyUpDown = true;
            Text = t;
            isKeyUpDown = false;
        }

        if (select)
        {
            if (KeepTokenSelected)
                SelectToken(token);
            else
                InternalSelect(x, length);
        }
    }

    private static bool PH(char m)
    {
        return m == '.' || m == '$' || m == ',' || m == ':' || m == '/';
    }

    public override bool PreProcessMessage(ref Message msg)
    {
        if (msg.Msg == WM_KEYDOWN)
        {
            isBackspacing = false;

            if (Spinner != null)
                Spinner.Refresh(); // occassionally the spinner turns to a white rectangle

            var key = msg.WParam.ToInt32();

            if (key == UP || key == DOWN)
            {
                KeyUpDown(key == UP ? 1 : -1);
                return true;
            }

            if (key == PAGEUP || key == PAGEDOWN)
            {
                KeyUpDown(key == PAGEUP ? 2 : -2);
                return true;
            }

            if (key == RIGHT || key == LEFT || key == HOME || key == END)
            {
                runningText = "";
                if (KeepTokenSelected)
                {
                    Token t2 = null;
                    if (key == HOME)
                    {
                        for (var i = 0; i < Tokens.Count; i++)
                            if (Tokens[i].CanEdit)
                            {
                                t2 = Tokens[i];
                                break;
                            }
                    }
                    else if (key == END)
                    {
                        for (var i = Tokens.Count - 1; i >= 0; i--)
                            if (Tokens[i].CanEdit)
                            {
                                t2 = Tokens[i];
                                break;
                            }
                    }
                    else if (key == RIGHT || key == LEFT)
                    {
                        var x = SelectionStart;
                        var t = TokenAt(x);
                        //if (t == null)
                        //return false; // should never happen
                        if (key == RIGHT)
                        {
                            for (var i = t.SeqNo + 1; i < Tokens.Count; i++)
                            {
                                var t3 = Tokens[i];
                                if (t3.CanEdit)
                                {
                                    t2 = t3;
                                    break;
                                }
                            }

                            if (t2 == null && CaretWrapsAround)
                                for (var i = 0; i <= t.SeqNo; i++)
                                {
                                    var t3 = Tokens[i];
                                    if (t3.CanEdit)
                                    {
                                        t2 = t3;
                                        break;
                                    }
                                }
                        }
                        else if (key == LEFT)
                        {
                            for (var i = t.SeqNo - 1; i >= 0; i--)
                            {
                                var t3 = Tokens[i];
                                if (t3.CanEdit)
                                {
                                    t2 = t3;
                                    break;
                                }
                            }

                            if (t2 == null && CaretWrapsAround)
                                for (var i = Tokens.Count - 1; i >= 0; i--)
                                {
                                    var t3 = Tokens[i];
                                    if (t3.CanEdit)
                                    {
                                        t2 = t3;
                                        break;
                                    }
                                }
                        }
                    }

                    if (t2 != null) SelectToken(t2);

                    return true;
                }

                if (CaretWrapsAround && SelectionLength == 0)
                {
                    var x = SelectionStart;
                    var t = GetText(MaskFormat.IncludePromptAndLiterals);
                    if (key == RIGHT && x == t.Length)
                    {
                        InternalSelect(0, 0);
                        return true;
                    }

                    if (key == LEFT && x == 0)
                    {
                        InternalSelect(t.Length, 0);
                        return true;
                    }
                }
            }
            else if (key == DELETE || key == BACKSPACE)
            {
                // the default MaskedTextBox shifts all characters from the right.
                var x = SelectionStart;
                var length = SelectionLength;
                string newText = null;
                var text = GetText(MaskFormat.IncludePromptAndLiterals);
                var args = new TokenDeleteArgs();
                args.SelectionStart = x;
                args.SelectionLength = length;

                if (length == 0)
                {
                    var t = TokenAt(x);
                    string tokenText = null;
                    string newTokenText = null;
                    // determine index relative to the token text
                    var xx = x - t.StartIndex;
                    var c = t.PadChar.HasValue ? t.PadChar.Value : PromptChar;

                    // Note: Deleting a character in a Literal token is not supported.
                    // The reason 't' would be returned is if it had custom values, e.g. month names
                    // If a field allows free form values, then simply use mask characters instead.

                    if (key == BACKSPACE)
                    {
                        if (t == null || x == t.StartIndex || t.IsLiteral)
                        {
                            // more caret one right?
                            if (x > 0)
                                InternalSelect(x - 1, 0);

                            return true; // cursor is at a non-editable token
                        }

                        var BackspacePullsTextLeft = false; // maybe a future option
                        if (BackspacePullsTextLeft)
                        {
                        }
                        else
                        {
                            tokenText = text.Substring(t.StartIndex, t.Length);
                            xx--;
                            if (PH(t.Mask[xx]))
                                newTokenText = tokenText;
                            else
                                newTokenText = tokenText.Substring(0, xx) + c + tokenText.Substring(xx + 1);

                            if (tokenText == newTokenText)
                            {
                                //if (x > 0)
                                InternalSelect(x - 1, 0);
                                return true; // no change
                            }

                            args.SelectionStart = x - 1; // move caret one left
                            var a = text.Substring(0, t.StartIndex);
                            var b = text.Substring(t.StartIndex + t.Length);

                            args.NewText = a + newTokenText + b;
                            args.NewTokenTexts = new[] { newTokenText };
                            args.Substrings = new[] { a, b };
                        }
                    }
                    else
                    {
                        if (t == null || t.IsLiteral || xx >= t.Length)
                        {
                            // more caret one right?
                            if (x < text.Length && !DeleteKeyShiftsTextLeft)
                                InternalSelect(x + 1, 0);

                            return true; // cursor is at a non-editable token
                        }

                        tokenText = text.Substring(t.StartIndex, t.Length);

                        // if the characters are shifted, then any reserved characters like $ : /
                        // need to keep their position.
                        if (DeleteKeyShiftsTextLeft)
                        {
                            var arr = tokenText.ToCharArray();

                            var i = xx;
                            for (int k = xx + 1, d = 0; k < t.Length; k++)
                            {
                                var c1 = arr[k];
                                var c2 = t.Mask[k];
                                if (PH(c2))
                                {
                                    d++;
                                }
                                else
                                {
                                    arr[i] = c1;
                                    i += d + 1;
                                    d = 0;
                                }
                            }

                            arr[i] = c;
                            newTokenText = new string(arr);

                            // this doesn't work because there might be reserved characters:
                            //newTokenText = tokenText.Substring(0, xx) + tokenText.Substring(xx + 1) + c;
                        }
                        else
                        {
                            if (PH(t.Mask[xx]))
                                newTokenText = tokenText;
                            else
                                newTokenText = tokenText.Substring(0, xx) + c + tokenText.Substring(xx + 1);
                            args.SelectionStart = x + 1; // move caret one right
                        }

                        if (tokenText == newTokenText)
                        {
                            if (!DeleteKeyShiftsTextLeft && x < text.Length)
                                InternalSelect(x + 1, 0);
                            return true; // no change
                        }

                        var a = text.Substring(0, t.StartIndex);
                        var b = text.Substring(t.StartIndex + t.Length);

                        args.NewText = a + newTokenText + b;
                        args.NewTokenTexts = new[] { newTokenText };
                        args.Substrings = new[] { a, b };
                    }

                    args.CurrentTokenTexts = new[] { tokenText };
                    args.CurrentText = text;
                    args.KeyCode = key;
                    args.Owner = this;
                    args.Tokens = new[] { t };
                    OnTokenDeleting(args);
                    if (args.Cancel)
                        return true;

                    ValidateOnTokenDeleteArgs(args);
                    newText = args.NewText;
                }
                else
                {
                    if (key == BACKSPACE && runningText.Length > 0)
                    {
                        isBackspacing = true;
                        runningText = runningText.Substring(0, runningText.Length - 1);
                        return false;
                    }

                    // for each token selected in the range [x, x + length]
                    var tokens = new List<Token>();
                    var newTexts = new List<string>();
                    var currentTexts = new List<string>();
                    var substrings = new List<string>();
                    var newText2 = text;

                    Token prevToken = null;
                    foreach (var t in Tokens)
                        //if (t.Text.Length == 0) // seq of shifting characters
                        //	continue;
                        if (x < t.StartIndex + t.Length && x + length > t.StartIndex)
                        {
                            var tokenText = text.Substring(t.StartIndex, t.Length);
                            if (!t.CanEdit || t.IsLiteral || t.Text.Length == 0)
                            {
                            }
                            else
                            {
                                var x1 = t.StartIndex;
                                var l1 = t.Length;
                                if (t.StartIndex < x)
                                {
                                    x1 = x;
                                    l1 = t.StartIndex + t.Length - x;
                                }

                                if (x + length < t.StartIndex + t.Length)
                                    l1 = x + length - t.StartIndex;

                                if (l1 > length)
                                    l1 = length;

                                var c = t.PadChar.HasValue ? t.PadChar.Value : PromptChar;
                                var t1 = text.Substring(t.StartIndex, x1 - t.StartIndex);
                                var t2a = text.Substring(x1, l1);
                                var t2m = Mask.Substring(x1, l1);
                                var t2b = "";
                                for (var i = 0; i < l1; i++)
                                {
                                    var c1 = t2m[i];
                                    var c2 = t2a[i];
                                    t2b += PH(c1) ? c2 : c;
                                }

                                //for (int 
                                //t.Mask
                                //String t2 = new String(c, l1);
                                var t3 = text.Substring(x1 + l1, t.StartIndex + t.Length - (x1 + l1));
                                var newTokenText = t1 + t2b + t3;
                                newText2 = newText2.Substring(0, t.StartIndex) + newTokenText +
                                           newText2.Substring(t.StartIndex + t.Length);

                                tokens.Add(t);
                                currentTexts.Add(tokenText);
                                newTexts.Add(newTokenText);

                                //String newTokenText = tokenText.Substring(0, xx) + tokenText.Substring(xx + 1) + PromptChar;
                                if (prevToken != null)
                                {
                                    var prevEnd = prevToken.StartIndex + prevToken.Length;
                                    var len = t.StartIndex - prevEnd;
                                    var ss = text.Substring(prevEnd, len);
                                    substrings.Add(ss);
                                }

                                prevToken = t;
                            }
                        }

                    // else Token is outside selected text
                    args.CurrentText = text;
                    args.CurrentTokenTexts = currentTexts.ToArray();
                    args.KeyCode = key;
                    args.NewTokenTexts = newTexts.ToArray();
                    args.Owner = this;
                    args.Substrings = substrings.ToArray();
                    args.Tokens = tokens.ToArray();
                    args.NewText = newText2;

                    OnTokenDeleting(args);
                    if (args.Cancel)
                        return true;

                    ValidateOnTokenDeleteArgs(args);
                    newText = args.NewText;
                }

                if (newText != null && newText != text)
                {
                    isDeleting = true;
                    Text = newText;
                    isDeleting = false;
                    if (KeepTokenSelected)
                    {
                        Token t2 = null;
                        if (args.SelectionStart != x) t2 = TokenAt(args.SelectionStart);
                        if (t2 != null)
                            SelectToken(t2);
                        else
                            InternalSelect(x, length);
                    }
                    else
                    {
                        InternalSelect(args.SelectionStart, args.SelectionLength);
                    }
                }

                return true;
            }
            else
            {
                // Other key character typed. In this case, the MaskedTextBox is allowed to handle the message
                // which might result in an OnTextChanged event. The OnTextChanged applies the text in a more natural
                // way, and enforces the min/max constraints. EM_SETSEL is allowed so that if no text is currently
                // selected, the cursor moves to the next edit position.
                if (!KeepTokenSelected) allowEMSETSEL = true;
            }
        }

        return base.PreProcessMessage(ref msg);
    }

    // never called :(
    //public override String Text { ... }

    protected virtual void OnTokenDeleting(TokenDeleteArgs args)
    {
        if (TokenDeleting != null)
            TokenDeleting(this, args);
    }

    protected virtual void ValidateOnTokenDeleteArgs(TokenDeleteArgs args)
    {
        // check tokens are in the Min/Max range
        var newText = args.NewText;
        var mp = args.Tokens[0].OwnerTextMaskProvider;
        if (newText != null && !mp.VerifyString(newText))
            newText = null;

        var wasChanged = false;

        var wf = false;
        for (var i = 0; i < args.Tokens.Length; i++)
        {
            var newTokenText =
                ValidateTokenText(args.Tokens[i], args.CurrentTokenTexts[i], args.NewTokenTexts[i], out wf);
            if (newTokenText != args.NewTokenTexts[i])
                wasChanged = true;
            args.NewTokenTexts[i] = newTokenText;
        }

        if (wasChanged)
            newText = null;

        if (newText == null)
        {
            var s = ToText(args);
            if (mp.VerifyString(s))
                newText = s;
        }

        args.NewText = newText;
    }

    private static string ToText(TokenDeleteArgs args)
    {
        var text = args.CurrentText;
        for (var i = 0; i < args.Tokens.Length; i++)
        {
            var t = args.Tokens[i];
            var text2 = text.Substring(0, t.StartIndex) + args.NewTokenTexts[i] +
                        text.Substring(t.StartIndex + t.Length);
            text = text2;
        }

        return text;
    }

    protected virtual string ValidateTokenText(Token token, string currentTokenText, string newTokenText,
        out bool wasFixed)
    {
        decimal val = 0;
        wasFixed = false;
        //char c = PromptChar;
        //if (token.PadChar.HasValue)
        //	c = token.PadChar.Value;
        //bool padRight = (token.PadRule == TokenValuePadRule.Right);

        if (decimal.TryParse(newTokenText, out val))
        {
            if (val < token.MinValue)
            {
                var fm = token.ValueTooLargeFixMode.HasValue ? token.ValueTooLargeFixMode.Value : ValueTooLargeFixMode;

                if (fm == ValueFixMode.KeepExistingValue && currentTokenText != null)
                {
                    newTokenText = currentTokenText;
                    wasFixed = true;
                }
                else if (fm == ValueFixMode.TakeClosestValidValue)
                {
                    var format = token.Mask.Replace('9', '0').Replace('#', '0');
                    newTokenText = token.MinValue.ToString(format);
                    wasFixed = true;
                    //if (padRight)
                    //	newTokenText = token.MinValue.ToString().PadRight(token.Length, c);
                    //else
                    //	newTokenText = token.MinValue.ToString().PadLeft(token.Length, c);
                }
            }
            else if (val >= token.MaxValue)
            {
                var fm = token.ValueTooSmallFixMode.HasValue ? token.ValueTooSmallFixMode.Value : ValueTooSmallFixMode;
                if (fm == ValueFixMode.KeepExistingValue && currentTokenText != null)
                {
                    newTokenText = currentTokenText;
                    wasFixed = true;
                }
                else if (fm == ValueFixMode.TakeClosestValidValue)
                {
                    var v2 = GetMaxValue(token);
                    var format = token.Mask.Replace('9', '0').Replace('#', '0');
                    if (v2 >= token.MinValue || currentTokenText == null)
                        newTokenText = v2.ToString(format);
                    else
                        newTokenText = currentTokenText;

                    wasFixed = true;
                    //if (padRight)
                    //	newTokenText = (token.MaxValue - 1).ToString().PadRight(token.Length, c);
                    //else
                    //	newTokenText = (token.MaxValue - 1).ToString().PadLeft(token.Length, c);
                }
            }
        }

        return newTokenText;
    }

    private static decimal GetMaxValue(Token token)
    {
        // if the Min/Max range was [0, 0.5), then 0.5 - 1 = -0.5 which is < Min
        // So the code chooses the MinValue by looking at number of decimal places.
        // e.g. 0.5 -> 0.4, 0.50 -> 0.49, 0.500 -> 0.499 etc.
        var format = token.Mask.Replace('9', '0').Replace('#', '0');
        var x = format.LastIndexOf('.');
        var dp = x < 0 ? 0 : format.Length - (x + 1);
        decimal pow = 1;
        for (var i = 0; i < dp; i++)
            pow *= 10;
        var v2 = token.MaxValue - 1.0m / pow; // 500. would mean dp == 0
        return v2;
    }

    protected override void WndProc(ref Message m)
    {
        // when the mouse is clicked and held, the mouse is dragged, then don't change the selection
        if ((m.Msg == EM_SETSEL && !allowEMSETSEL) ||
            (m.Msg == WM_MOUSEMOVE && KeepTokenSelected)) // && KeepTokenSelected && !allowTextSelection)
            return;

        // double-click selects entire text, so must ignore the message when KeepTokenSelected is true
        if (m.Msg == WM_LBUTTONDBLCLK && KeepTokenSelected)
            return;

        if (m.Msg == WM_PASTE)
            wasPaste = true;
        else if (m.Msg == WM_CHAR)
            wasPaste = false;

        base.WndProc(ref m);
    }

    //protected virtual void UpdateSpinnerSize() {
    //	if (Spinner == null)
    //		return;
    //	Spinner.Size = Spinner.PreferredSize;
    //	if (AutoSize)
    //		this.Size = GetPreferredSize(Size.Empty);
    //}

    public override Size GetPreferredSize(Size proposedSize)
    {
        // MaskedTextBox does an inaccurate job at calculating its height,
        // but trying to change the height doesn't work as expected.
        // It seems like there are set sizes the textbox can be, probably
        // the same issue with trying to set a non-multiline TextBox's height.
        var s = base.GetPreferredSize(proposedSize);
        if (Spinner != null)
        {
            var s2 = Spinner.PreferredSize; //(Size.Empty);
            if (s2.Height > s.Height)
                s.Height = s2.Height;
            s.Width += s2.Width;
        }

        return s;
    }

    protected string ValidateNewText(string text, out bool wasFixed)
    {
        wasFixed = false;
        // validate all tokens are in the range [min, max)
        foreach (var t in Tokens)
        {
            if (t.Text.Length == 0)
                continue;

            var newTokenText = text.Substring(t.StartIndex, t.Length);
            var oldTokenText = oldText == null ? null : oldText.Substring(t.StartIndex, t.Length);
            var wasFixed2 = false;
            var tokenText = ValidateTokenText(t, oldTokenText, newTokenText, out wasFixed2);
            if (wasFixed2)
                wasFixed = true;
            if (tokenText != newTokenText)
                text = text.Substring(0, t.StartIndex) + tokenText + text.Substring(t.StartIndex + tokenText.Length);
        }

        return text;
    }

    /// <summary>
    ///     Sets KeepTokenSelected, CaretWrapsAround, ChopRunningText and HideSelection to true, and UseMaxValueIfTooLarge and
    ///     CaretVisible to false.
    ///     Also, the Cursor is changed to the default pointer, instead of the IBeam.
    /// </summary>
    public void MimicDateTimePicker()
    {
        KeepTokenSelected = true;
        CaretWrapsAround = true;
        ChopRunningText = true;
        HideSelection = true;
        UseMaxValueIfTooLarge = false;
        CaretVisible = false;
        Cursor = Cursors.Default;
        PromptChar = '0';
        ValueTooLargeFixMode = ValueFixMode.KeepExistingValue;
        ValueTooSmallFixMode = ValueFixMode.KeepExistingValue;
        ValuesCarryOver = false;
    }
}

public enum ValueFixMode
{
    KeepExistingValue = 0,
    TakeClosestValidValue = 1
}

// Provides support for hiding the character, which is useful when the KeepTokenSelected property is true.
internal static class MaskedTextBoxWin32
{
    [DllImport("user32.dll")]
    public static extern bool HideCaret(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowCaret(IntPtr hWnd);
}

// Merges the user typed input with the currently selected portion of the mask
public static class TextOverlay
{
    public static void TestAlgorithm()
    {
        //Test("", "", ""); // empty mask not allowed
        Test("", "0", "0", false);
        Test("1", "0", "1", true);
        Test("123", "0000", "0123", false);
        Test("123", "0000", "1230", false, TokenValuePadRule.Right);
        Test("1234", "0000", "1234", true);
        Test("12345", "0000", "2345", true);
        //---
        Test("1.2", "00.00", "01.20", false);
        Test("1..2", "00.00", "01.20", false);
        Test("1..2", "00.00.00", "01.00.20", false);
        Test("1..2", "00.00.00", "10.00.20", false, TokenValuePadRule.Right);
        Test("1..2", "00.00.00", "01.00.02", false, TokenValuePadRule.Left);
        //---
        Test("1", "00.00", "01.00", false);
        Test("12", "00.00", "12.00", false);
        Test("123", "00.00", "12.30", false);
        Test("1234", "00.00", "12.34", true);
        Test("123.4", "00.00", "23.40", false);
        Test("1234.5", "00.00", "34.50", false);
        Test("1234.56", "00.00", "34.56", true);
    }

    private static void Test(string inputText, string displayText, string expectedResult, bool expectedIsFull,
        TokenValuePadRule padRule = TokenValuePadRule.Default, char padChar = '0', bool keepExistingText = false)
    {
        var mp = new MaskedTextProvider(displayText);
        var isFull = false;
        var result = Apply(inputText, displayText, 0, displayText.Length, mp, padRule, padChar, keepExistingText,
            out isFull);
        if (result != expectedResult)
            throw new Exception(string.Format("Expected '{0}' but got '{1}'.", expectedResult, result));
        if (expectedIsFull != isFull)
            throw new Exception(string.Format("Expected IsFull {0} but got {1}.", expectedIsFull, isFull));
    }


    public static string Apply(string inputText, string displayText, int selectionStart, int selectionLength,
        MaskedTextProvider mp, TokenValuePadRule padRule, char? padChar, bool keepExistingText, out bool isFull)
    {
        var list1 = new List<string>();
        var list2 = new List<char>();

        var s = "";
        var n = selectionStart + selectionLength;
        for (var i = selectionStart; i < n; i++)
        {
            var b = mp.IsEditPosition(i);
            if (!b || i == n - 1)
            {
                if (!b)
                    list2.Add(displayText[i]);
                else
                    s += displayText[i];

                list1.Add(s);
                s = "";
            }
            else
            {
                s += displayText[i];
            }
        }

        return Apply(inputText, list1, list2, padRule, padChar, keepExistingText, out isFull);
    }

    public static string Apply(string inputText, List<string> list1, List<char> list2, TokenValuePadRule padRule,
        char? padChar, bool keepExistingText, out bool isFull)
    {
        var list3 = new List<string>();
        var s = "";
        var breaksRemaining = list2.Count;
        foreach (var c in inputText)
            if (list2.Contains(c))
                breaksRemaining--;

        for (var i = 0; i < inputText.Length; i++)
        {
            var c = inputText[i];

            if (list2.Contains(c) || i == inputText.Length - 1)
            {
                if (!list2.Contains(c))
                    s += c;

                list3.Add(s);
                s = "";
            }
            else
            {
                s += c;
            }

            // Auomatic breaks are inserted to allow the user to type continuously without having to
            // type the placeholder characters. For example, if the mask is 00.00 and the user types 1234,
            // then the result should be 12.34 (not 12.00 or 34.00).
            if (list3.Count < list1.Count && s.Length >= list1[list3.Count].Length && breaksRemaining > 0)
                if (s.Length > 0)
                {
                    breaksRemaining--;
                    list3.Add(s);
                    s = "";
                }
            // can this happen?
        }

        return Apply(list1, list2, list3, padRule, padChar, keepExistingText, out isFull);
    }

    public static string Apply(List<string> list1, List<char> list2, List<string> list3, TokenValuePadRule padRule,
        char? padChar, bool keepExistingText, out bool isFull)
    {
        isFull = false;
        var newValues = new string[list1.Count];

        for (var i = 0; i < list3.Count; i++)
        {
            var kk = i < list1.Count ? i : list1.Count - 1;
            var s1 = list1[kk];
            var arr = s1.ToCharArray();
            var s3 = list3[i];

            if (s3.Length < s1.Length)
            {
                // no truncate required
                if (padRule == TokenValuePadRule.Right || (i > 0 && padRule == TokenValuePadRule.Default))
                {
                    for (var j = 0; j < s3.Length; j++)
                        arr[j] = s3[j];

                    if (padChar.HasValue)
                        for (var j = s3.Length; j < s1.Length; j++)
                            arr[j] = padChar.Value;
                }
                else
                {
                    for (int j = arr.Length - 1, k = s3.Length - 1; k >= 0; j--, k--)
                        arr[j] = s3[k];

                    if (padChar.HasValue)
                        for (var j = arr.Length - s3.Length - 1; j >= 0; j--)
                            arr[j] = padChar.Value;
                }
            }
            else
            {
                // truncate required, not all characters will fit
                if (i == list1.Count - 1)
                    isFull = true;

                for (int j = arr.Length - 1, k = s3.Length - 1; j >= 0; j--, k--)
                    arr[j] = s3[k];
            }

            newValues[kk] = new string(arr);
        }

        var result = "";
        for (var i = 0; i < newValues.Length; i++)
        {
            var text = newValues[i];
            if (text == null)
            {
                if (!keepExistingText && padChar.HasValue)
                    text = new string(padChar.Value, list1[i].Length);
                else
                    text = list1[i];
            }

            result += text;
            if (i < list2.Count)
                result += list2[i];
        }

        return result;
    }
}