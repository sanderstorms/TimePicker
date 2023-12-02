using System;
using System.Collections;

namespace Opulos.Core.Localization {

// Could not use the same resx across multiple projects because
// the resx would combine with the default namespace of the assembly.
// So an assembly with a default namespace other than Opulos would
// change the path to the Designer.cs file. Instead, the Localizer
// class provides the same functionality, returning the most
// specific string available (e.g. en-us), and falling back if
// neccessary (e.g. en).
public sealed class Localizer {

	private Hashtable htCulture = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
	private Hashtable htTypes = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
	private Type ty = null;
	private String defaultLang = null;

	public Localizer(Type ty, String defaultLanguage = "en") {
		this.ty = ty;
		defaultLang = defaultLanguage;
	}

	public String Lookup(String name) {
		Type ty = typeof(Strings);
		var c = System.Threading.Thread.CurrentThread.CurrentUICulture;
		LoadStrings(ty, c.TwoLetterISOLanguageName);
		LoadStrings(ty, c.Name);
		LoadStrings(ty, defaultLang);

		foreach (String lang in new [] { c.Name, c.TwoLetterISOLanguageName, defaultLang}) {
			Hashtable ht = (Hashtable) htCulture[lang];
			if (ht != null) {
				String value = (String) ht[name];
				if (value != null)
					return value;
			}
		}

		return name;
	}

	private void LoadStrings(Type ty2, String lang) {
		String typeFullName = ty2.FullName + "_" + lang.Replace('-', '_');
		if (htTypes[typeFullName] == null) {
			htTypes[typeFullName] = "";
			Type ty = Type.GetType(typeFullName, false, true);
			if (ty != null) {
				Hashtable ht = new Hashtable();
				var fields = ty.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
				foreach (var f in fields) {
					if (f.IsLiteral && !f.IsInitOnly)
						ht[f.Name] = f.GetValue(null);
				}
				htCulture[lang] = ht;
			}
		}
	}
}

}