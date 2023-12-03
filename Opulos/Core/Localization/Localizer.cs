using System;
using System.Collections;
using System.Reflection;
using System.Threading;

namespace TimePicker.Opulos.Core.Localization;

// Could not use the same resx across multiple projects because
// the resx would combine with the default namespace of the assembly.
// So an assembly with a default namespace other than Opulos would
// change the path to the Designer.cs file. Instead, the Localizer
// class provides the same functionality, returning the most
// specific string available (e.g. en-us), and falling back if
// neccessary (e.g. en).
public sealed class Localizer
{
    private readonly string defaultLang;

    private readonly Hashtable htCulture = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly Hashtable htTypes = new(StringComparer.InvariantCultureIgnoreCase);
    private Type ty;

    public Localizer(Type ty, string defaultLanguage = "en")
    {
        this.ty = ty;
        defaultLang = defaultLanguage;
    }

    public string Lookup(string name)
    {
        var ty = typeof(Strings);
        var c = Thread.CurrentThread.CurrentUICulture;
        LoadStrings(ty, c.TwoLetterISOLanguageName);
        LoadStrings(ty, c.Name);
        LoadStrings(ty, defaultLang);

        foreach (var lang in new[] { c.Name, c.TwoLetterISOLanguageName, defaultLang })
        {
            var ht = (Hashtable)htCulture[lang];
            if (ht != null)
            {
                var value = (string)ht[name];
                if (value != null)
                    return value;
            }
        }

        return name;
    }

    private void LoadStrings(Type ty2, string lang)
    {
        var typeFullName = ty2.FullName + "_" + lang.Replace('-', '_');
        if (htTypes[typeFullName] == null)
        {
            htTypes[typeFullName] = "";
            var ty = Type.GetType(typeFullName, false, true);
            if (ty != null)
            {
                var ht = new Hashtable();
                var fields = ty.GetFields(BindingFlags.Public | BindingFlags.Static);
                foreach (var f in fields)
                    if (f.IsLiteral && !f.IsInitOnly)
                        ht[f.Name] = f.GetValue(null);
                htCulture[lang] = ht;
            }
        }
    }
}