namespace TimePicker.Opulos.Core.Localization;

public static class Strings
{
    private static readonly Localizer s = new(typeof(Strings));

    public static string OK => s.Lookup("OK");
    public static string Cancel => s.Lookup("Cancel");
}

public sealed class Strings_en
{
    public const string OK = "&OK";
    public const string Cancel = "&Cancel";
}

public sealed class Strings_ja
{
    public const string OK = "&OK";
    public const string Cancel = "キャンセル";
}