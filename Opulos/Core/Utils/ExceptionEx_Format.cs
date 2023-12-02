using System;
using System.Text;

namespace Opulos.Core.Utils {

public static partial class ExceptionEx {

	private const String ignore1 = "End of stack trace from previous location where exception was thrown";
	private const String ignore2 = "at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()";
	private const String ignore3 = "Opulos.Core.Utils.ExceptionEx.Rethrow";

	public static String FormatStackTrace(String stackTrace, bool includeSystem) {
		StringBuilder sb = new StringBuilder();
		String[] arr = ("" + stackTrace).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

		foreach (String s in arr) {
			if (s.IndexOf(ignore1) >= 0 || s.IndexOf(ignore2) >= 0 || s.IndexOf(ignore3) >= 0)
				continue;

			String t = Format(s);
			if (sb.Length == 0 || includeSystem || !t.StartsWith("System.")) { // t.StartsWith("Opulos")) {
				if (sb.Length > 0)
					sb.AppendLine();
				sb.Append(t);
			}
		}
		return sb.ToString();
	}

	// Condenses the error stack output line.
	// E.g.
	// at System.Drawing.Image.FromFile(String filename, Boolean useEmbeddedColorManagement)
	//        -> System.Drawing.Image.FromFile
	// at Opulos.NüWorkflow.SplashScreen.CreateIcon(String filename, Color transparentColor, Byte alpha) in c:\Opulos\NüWorkflow\SplashScreen.cs:line 894
	//        -> Opulos.NüWorkflow.SplashScreen.CreateIcon:894
	private static String Format(String s) {
		 s = s.Trim();
		 if (s.StartsWith("at "))
			s = s.Substring(3);

		int b1 = s.IndexOf('(');
		int b2 = s.IndexOf(')');
		if (b1 >= 0 && b2 >= 0)
			s = s.Substring(0, b1) + s.Substring(b2+1);

		// remove '__' generic markups, e.g:
		// at Opulos.NüWorkflow.UI.WorkflowRunPanel.<>c__DisplayClass38.<queryLastResultAndAdd>b__34(Object o)
		// becomes:
		// Opulos.NüWorkflow.UI.WorkflowRunPanel.queryLastResultAndAdd
		int dd = 0;
		while ((dd = s.IndexOf("__")) > 0) {
			int a = dd - 1;
			int b = dd + 2;
			for (int i = a; i >= 0; i--) {
				char c = s[i];
				if (Char.IsLetterOrDigit(c))
					a--;
				else
					break;
			}
			for (int i = a; i >= 0; i--) {
				char c = s[i];
				if (c == '<' || c == '>')
					a--;
				else
					break;
			}

			for (int i = b; i < s.Length; i++) {
				char c = s[i];
				if (Char.IsLetterOrDigit(c))
					b++;
				else
					break;
			}
			for (int i = b; i < s.Length; i++) {
				char c = s[i];
				if (c == '<' || c == '>' || c == '.')
					b++;
				else
					break;
			}

			s = s.Substring(0, a+1) + s.Substring(b);
		}

		// remove the generic markup
		while ((dd = s.IndexOf("`")) > 0) {
			int a = dd;
			int b = dd + 1;
			while (b < s.Length) {
				char c = s[b];
				if (char.IsDigit(c))
					b++;
				else
					break;
			}
			s = s.Substring(0, a) + s.Substring(b);
		}
		// sometimes it can happen there is remaining angle brackets, e.g:
		// at Namespace.ClassName.<Main>b__0(Object o) in c:\...\...\Program.cs:line 42
		// becomes: Namespace.ClassName.<Main:42
		// so remove the remaining angle brackets
		s = s.Replace("<", "").Replace(">", "");
		String[] arr = s.Split(' ');
		if (arr.Length == 0)
			return "";
		if (arr.Length == 1)
			return arr[0];
		return arr[0] + ":" + arr[arr.Length - 1];
	}
}
}