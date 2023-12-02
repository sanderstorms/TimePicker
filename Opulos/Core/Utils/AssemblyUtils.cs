using System;
using System.IO;
using System.Reflection;
using System.Resources;

namespace Opulos.Core.Utils {

public static class AssemblyUtils {

	public static String GetMainWindowTitleStartsWith() {
		return ProductName + " v";
	}

	public static String GetDefaultMainWindowTitle() {
		String title = ProductName + " " + GetProductVersion();
		if (System.Diagnostics.Debugger.IsAttached)
			title += " - Debugger Attached";
		return title;
	}

	public static String GetProductVersion() {
		return "v" + getProgramCompiledDate().ToString("yyyy'.'MM'.'dd");
	}

	public static String ProductName {
		get {
			//var ata = (System.Reflection.AssemblyTitleAttribute) System.Reflection.Assembly.GetEntryAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyTitleAttribute), false)[0];
			//String title = ata.Title;
			var apa = (System.Reflection.AssemblyProductAttribute) System.Reflection.Assembly.GetEntryAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyProductAttribute), false)[0];
			String productName = apa.Product;
			return productName;
		}
	}


	public static ResourceManager GetResourceManager() {
		Assembly assembly = Assembly.GetEntryAssembly(); //this.GetType().Assembly;
		if (assembly == null)
			assembly = Assembly.GetExecutingAssembly();

		String[] names = assembly.GetManifestResourceNames();
		String name = null;
		if (names.Length == 1)
			name = Path.GetFileNameWithoutExtension(names[0]); // trim off the .resources extension
		else {
			//e.g. "Opulos.NüPortal.Properties.Resources.resources"
			//String @namespace = assembly.EntryPoint.DeclaringType.Namespace; // Opulos.NuPortal
			//name = @namespace + ".Properties.Resources"; // don't add the .resources extension
			foreach (String n in names) {
				if (n.EndsWith(".Properties.Resources.resources")) {
					name = Path.GetFileNameWithoutExtension(n);
					break;
				}
			}
		}
		// if the .resources extension is left on, then a resource not found exception will be thrown.
		return new ResourceManager(name, assembly);
	}

	// Requires that the version is specified as 1.0.* in Properties\AssemblyInfo.cs
	// Has to be located within the current assemly
	public static DateTime getProgramCompiledDate() {
		var assembly = System.Reflection.Assembly.GetEntryAssembly();
		if (assembly == null) // assembly is null when used through a Microsoft Debug Visualizer
			assembly = System.Reflection.Assembly.GetExecutingAssembly();

		int numDays = 0;
		if (assembly != null) {
			Version v = assembly.GetName().Version;
			numDays = v.Build;
		}
		// v.Build = days since Jan. 1, 2000
		DateTime date = new DateTime(2000, 1, 1);
		return date.AddDays(numDays);
	}
}
}