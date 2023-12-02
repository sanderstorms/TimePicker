using System;
using System.Text;

namespace Opulos.Core.Utils;

public static partial class ExceptionEx
{
    private const string ignore1 = "End of stack trace from previous location where exception was thrown";
    private const string ignore2 = "at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()";
    private const string ignore3 = "Opulos.Core.Utils.ExceptionEx.Rethrow";

    public static string FormatStackTrace(string stackTrace, bool includeSystem)
    {
        var sb = new StringBuilder();
        var arr = ("" + stackTrace).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var s in arr)
        {
            if (s.IndexOf(ignore1) >= 0 || s.IndexOf(ignore2) >= 0 || s.IndexOf(ignore3) >= 0)
                continue;

            var t = Format(s);
            if (sb.Length == 0 || includeSystem || !t.StartsWith("System."))
            {
                // t.StartsWith("Opulos")) {
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
    private static string Format(string s)
    {
        s = s.Trim();
        if (s.StartsWith("at "))
            s = s.Substring(3);

        var b1 = s.IndexOf('(');
        var b2 = s.IndexOf(')');
        if (b1 >= 0 && b2 >= 0)
            s = s.Substring(0, b1) + s.Substring(b2 + 1);

        // remove '__' generic markups, e.g:
        // at Opulos.NüWorkflow.UI.WorkflowRunPanel.<>c__DisplayClass38.<queryLastResultAndAdd>b__34(Object o)
        // becomes:
        // Opulos.NüWorkflow.UI.WorkflowRunPanel.queryLastResultAndAdd
        var dd = 0;
        while ((dd = s.IndexOf("__")) > 0)
        {
            var a = dd - 1;
            var b = dd + 2;
            for (var i = a; i >= 0; i--)
            {
                var c = s[i];
                if (char.IsLetterOrDigit(c))
                    a--;
                else
                    break;
            }

            for (var i = a; i >= 0; i--)
            {
                var c = s[i];
                if (c == '<' || c == '>')
                    a--;
                else
                    break;
            }

            for (var i = b; i < s.Length; i++)
            {
                var c = s[i];
                if (char.IsLetterOrDigit(c))
                    b++;
                else
                    break;
            }

            for (var i = b; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '<' || c == '>' || c == '.')
                    b++;
                else
                    break;
            }

            s = s.Substring(0, a + 1) + s.Substring(b);
        }

        // remove the generic markup
        while ((dd = s.IndexOf("`")) > 0)
        {
            var a = dd;
            var b = dd + 1;
            while (b < s.Length)
            {
                var c = s[b];
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
        var arr = s.Split(' ');
        if (arr.Length == 0)
            return "";
        if (arr.Length == 1)
            return arr[0];
        return arr[0] + ":" + arr[arr.Length - 1];
    }
}