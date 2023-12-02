using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Resources;

namespace Opulos.Core.Utils;

public static class AssemblyUtils
{
    public static string ProductName
    {
        get
        {
            //var ata = (System.Reflection.AssemblyTitleAttribute) System.Reflection.Assembly.GetEntryAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyTitleAttribute), false)[0];
            //String title = ata.Title;
            var apa = (AssemblyProductAttribute)Assembly.GetEntryAssembly()
                .GetCustomAttributes(typeof(AssemblyProductAttribute), false)[0];
            var productName = apa.Product;
            return productName;
        }
    }

    public static string GetMainWindowTitleStartsWith()
    {
        return ProductName + " v";
    }

    public static string GetDefaultMainWindowTitle()
    {
        var title = ProductName + " " + GetProductVersion();
        if (Debugger.IsAttached)
            title += " - Debugger Attached";
        return title;
    }

    public static string GetProductVersion()
    {
        return "v" + getProgramCompiledDate().ToString("yyyy'.'MM'.'dd");
    }


    public static ResourceManager GetResourceManager()
    {
        var assembly = Assembly.GetEntryAssembly(); //this.GetType().Assembly;
        if (assembly == null)
            assembly = Assembly.GetExecutingAssembly();

        var names = assembly.GetManifestResourceNames();
        string name = null;
        if (names.Length == 1)
            name = Path.GetFileNameWithoutExtension(names[0]); // trim off the .resources extension
        else
            //e.g. "Opulos.NüPortal.Properties.Resources.resources"
            //String @namespace = assembly.EntryPoint.DeclaringType.Namespace; // Opulos.NuPortal
            //name = @namespace + ".Properties.Resources"; // don't add the .resources extension
            foreach (var n in names)
                if (n.EndsWith(".Properties.Resources.resources"))
                {
                    name = Path.GetFileNameWithoutExtension(n);
                    break;
                }

        // if the .resources extension is left on, then a resource not found exception will be thrown.
        return new ResourceManager(name, assembly);
    }

    // Requires that the version is specified as 1.0.* in Properties\AssemblyInfo.cs
    // Has to be located within the current assemly
    public static DateTime getProgramCompiledDate()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly == null) // assembly is null when used through a Microsoft Debug Visualizer
            assembly = Assembly.GetExecutingAssembly();

        var numDays = 0;
        if (assembly != null)
        {
            var v = assembly.GetName().Version;
            numDays = v.Build;
        }

        // v.Build = days since Jan. 1, 2000
        var date = new DateTime(2000, 1, 1);
        return date.AddDays(numDays);
    }
}