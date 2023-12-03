using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace TimePicker.Opulos.Core.UI;

/// <summary>
///     When a token is changed, by the up/down arrows, page up/page down keys, the spinner buttons, or by typing, then
///     a token changing event is fired.
/// </summary>
public class TokenChangeArgs : CancelEventArgs
{
    public TokenChangeArgs(MaskedTextBox source, Token token)
    {
        Source = source;
        Token = token;
        IsTextValid = true;
    }

    ///<summary>The token's owning MaskedTextBox control.</summary>
    public MaskedTextBox Source { get; private set; }

    /// <summary>
    ///     This field is assigned the value of what the MaskTextBox Text property will be
    ///     (as calculated by the MaskedTextBox
    ///     <T>
    ///         implementation). Lisetners can change this Text value
    ///         if the Text would not be valid. For example, if the month token was changed to Feb, but the
    ///         day token was still at 30, then the listener could change the text to Feb 28.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    ///     A flag that indicates if the Text is valid. The Text should always be
    ///     valid, however it is possible to specify a custom values list on a non-literal token
    ///     that does not pass validation.
    /// </summary>
    public bool IsTextValid { get; internal set; }

    ///<summary>The source Token object that was changed.</summary>
    public Token Token { get; internal set; }

    ///<summary>The current value of the token text with the padding characters removed.</summary>
    public string CurrentTokenText { get; internal set; }

    /// <summary>The value of the object's text from the custom values list by calling the object's ToString() method.</summary>
    public string NewTokenText { get; internal set; }

    /// <summary>
    ///     The text of the current token as it currently appears in the MaskedTextBox. Note: the visual represenation
    ///     of the text might not be the same, as the MaskedTextBox may display spaces as the prompt character.
    /// </summary>
    public string CurrentTokenTextPadded { get; internal set; }

    /// <summary>
    ///     The new token's text with padding. The padding only applies to tokens that have a
    ///     custom values list. For non-literal tokens, the number of characters in the original mask represents
    ///     maximum allowable length. If the selected value from the list is longer, then it is truncated,
    ///     and this field contains the truncated value.
    /// </summary>
    public string NewTokenTextPadded { get; internal set; }

    /// <summary>
    ///     The new mask text using the NewLiteralEscaped text. It's possible for event listeners
    ///     to set this value, which will set as the new Mask property after the event. Only Tokens that are
    ///     literals that have a CustomValues list will change the mask. Literal tokens have to change the
    ///     mask because the new mask contains the escaped text (ToString) of selected item.
    /// </summary>
    public string Mask { get; set; }
}

public class TokenDeleteArgs : CancelEventArgs
{
    public string CurrentText;
    public string[] CurrentTokenTexts;
    public int KeyCode;
    public string NewText;
    public string[] NewTokenTexts;

    public MaskedTextBox Owner;

    /// </summary>
    /// By default, when text is deleted, the same selection length is maintained. This value can be changed to
    /// change which text is selected after the delete.
    /// </summary>
    public int SelectionLength = 0;

    /// <summary>
    ///     Be default, when text is deleted, the same selection start is maintained. This value can be changed to
    ///     change which text is selected after the delete.
    /// </summary>
    public int SelectionStart = 0;

    public string[] Substrings;
    public Token[] Tokens;
}

public delegate void TokenChangeEventHandler(object sender, TokenChangeArgs e);

public delegate void TokenDeleteEventHandler(object sender, TokenDeleteArgs e);

public delegate void ValueChangedEventHandler<T>(object sender, ValueChangedEventArgs<T> e);

public delegate void ValueChangingEventHandler<T>(object sender, ValueChangingEventArgs<T> e);

public class ValueChangedEventArgs<T> : EventArgs
{
    public ValueChangedEventArgs(T oldValue, T newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public T OldValue { get; private set; }
    public T NewValue { get; private set; }
}

public class ValueChangingEventArgs<T> : CancelEventArgs
{
    public ValueChangingEventArgs(T oldValue, T newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public T OldValue { get; private set; }
    public T NewValue { get; set; }
}