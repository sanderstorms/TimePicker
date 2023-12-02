using System;

namespace Opulos.Core.Localization {

public static class Strings {

	private static Localizer s = new Localizer(typeof(Strings));

	public static String OK { get { return s.Lookup("OK"); }}
	public static String Cancel { get { return s.Lookup("Cancel"); }}
}

public sealed class Strings_en {
	public const String OK = "&OK";
	public const String Cancel = "&Cancel";
}

public sealed class Strings_ja {
	public const String OK = "&OK";
	public const String Cancel = "キャンセル";
}

}