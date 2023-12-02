using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Opulos.Core.Utils {

// Use like ArgumentException, but in cases where a stack trace is needed.
public class InvalidTypeException : Exception {
	public virtual String ParamName { get; private set; }

	public InvalidTypeException(String message, String paramName) : base(message) {
		this.ParamName = paramName;
	}
}

public class FatalApplicationException : Exception {
	public FatalApplicationException(String message, Exception innerException) : base(message, innerException) {}
}

public class ArgumentWarning : ArgumentException {
	public ArgumentWarning(String message) : base(message) {}
	public ArgumentWarning(String message, String paramName) : base(message, paramName) {}
	public ArgumentWarning(String message, String paramName, Exception innerException) : base(message, paramName, innerException) {}
}

public class MultiArgumentException : Exception {

	private Hashtable htParamName = new Hashtable(StringComparer.OrdinalIgnoreCase);
	private List<ArgumentException> errors = new List<ArgumentException>();

	public MultiArgumentException() : base() {}

	public MultiArgumentException(IList<ArgumentException> errors) : base() {
		if (errors != null) {
			foreach (ArgumentException ex in errors)
				Add(ex);
		}
	}

	public int Count {
		get {
			return errors.Count;
		}
	}

	public void Add(String paramName, String message, bool isWarning = false) {
		if (isWarning)
			Add(new ArgumentWarning(message, paramName));
		else
			Add(new ArgumentException(message, paramName));
	}

	public void Add(Exception ex, String paramName = "") {
		ArgumentException ex2 = ex as ArgumentException;
		if (ex2 == null)
			ex2 = new ArgumentException(ex.Message, paramName ?? "", ex);

		if (String.IsNullOrWhiteSpace(paramName))
			paramName = ex2.ParamName;

		errors.Add(ex2);
		String n = paramName ?? "";
		List<ArgumentException> list = (List<ArgumentException>) htParamName[n];
		if (list == null) {
			list = new List<ArgumentException>();
			htParamName[n] = list;
		}
		list.Add(ex2);
	}

	public override String Message {
		get {
			return String.Join("\n\n", errors.Select(ex => GetOriginalMessage(ex)));;
		}
	}


	public String[] ParamNames {
		get {
			return htParamName.Keys.Cast<String>().ToArray();
		}
	}

	///<summary>Returns true if all ArgumentExceptions are actually ArgumentWarning objects. Returns false if there are no associated ArgumentException objects, or one or more is not an ArgumentWarning.</summary>
	public bool IsWarning(String paramName) {
		List<ArgumentException> list = (List<ArgumentException>) htParamName[paramName ?? ""];
		if (list == null || list.Count == 0) return false;
		return (list.Count(ex => ex is ArgumentWarning) == list.Count);		
	}

	public ArgumentException[] GetExceptions(String paramName) {
		List<ArgumentException> list = (List<ArgumentException>) htParamName[paramName ?? ""];
		return (list == null ? new ArgumentException[0] : list.ToArray());
	}

	public String GetMessages(String paramName) {
		ArgumentException[] arr = GetExceptions(paramName);
		StringBuilder sb = new StringBuilder();
		for (int i = 0; i < arr.Length; i++) {
			if (i > 0)
				sb.AppendLine().AppendLine();
			sb.Append(GetOriginalMessage(arr[i]));
		}
		return sb.ToString();
	}

	private static String GetOriginalMessage(ArgumentException ex) {
		String m = ex.Message;
		if (String.IsNullOrEmpty(ex.ParamName))
			return m;
		int x = m.LastIndexOf("\r\n");
		if (x >= 0)
			m = m.Substring(0, x); // trim off the \r\nParameter name: ...
		return m;
	}

	public void ThrowIfRequired(bool allowWarnings) {
		if (errors.Count == 0)
			return;
		if (!allowWarnings)
			throw this;
		bool allWarnings = (errors.Count(ex => ex is ArgumentWarning) == errors.Count);
		if (!allWarnings)
			throw this;
	}

	public void ThrowIf(InvokeMethod iv) {
		if (errors.Count == 0)
			return;
		if (iv == InvokeMethod.YesNormal) {
			bool onlyWarnings = (errors.Count(ex => ex is ArgumentWarning) == errors.Count);
			if (onlyWarnings)
				return;		
		}
		throw this;
	}

	public bool HasErrors {
		get {
			return errors.FirstOrDefault(ex => !(ex is ArgumentWarning)) != null;
		}
	}
}

public enum AppendStackTrace {
	Yes,
	YesIfUnknown,
	No
}

public static partial class ExceptionEx {

	public static String TagLine = "Press CTRL+C to copy the message.";
	public static String ProductTitle = "";

	static ExceptionEx() {
		ProductTitle = AssemblyUtils.GetDefaultMainWindowTitle();
	}

	public static bool IsKnownException(this Exception ex) {
		if (ex is TargetInvocationException && ex.InnerException != null)
			ex = ex.InnerException;

		if (ex is ArgumentException) {
			if (ex.Message.StartsWith("Cannot set column"))
				return false; // occurs when inserting a Row with MaxLength violation into a DataTable (need to know where).

			// Any class exception that subclasses ArgumentException (e.g. ArgumentNullException, ArgumentOutOfRangeException)  will report the full stack trace
			if (ex.GetType().IsAssignableFrom(typeof(ArgumentException))) {
				// if the stack trace starts at a System. class then report the entire stack trace
				String st = ex.StackTrace; // st is null when a new ArgumentException object is created
				if (st == null || st.IndexOf("   at System.") != 0)
					return true;
			}
		}

		if (ex.GetType().Name == "MySqlException") {
			String m = ex.Message;
			// error code is -2147467259 which doesn't match up with 1022 from the documentation
			if (m.StartsWith("Duplicate entry", StringComparison.InvariantCultureIgnoreCase))
				return true;
		}

		return false;
	}

	///<summary>Appends the exception's message and the decendent inner exception messags to a single String. Each
	///message is separated by a single newline character. A message that already exists in a previously seen
	///message is ignored, thus preventing redundant messages.</summary>
	/*public static String GetAllMessages(this Exception ex) {
		if (ex == null)
			return "";

		StringBuilder sb = new StringBuilder();
		List<String> msgs = new List<String>();
		while (ex != null) {
			String m = ex.Message;
			if (!String.IsNullOrEmpty(m)) {

				bool found = false;
				foreach (String m2 in msgs) {
					if (m2.IndexOf(m, StringComparison.InvariantCultureIgnoreCase) >= 0) {
						found = true;
						break;
					}
				}
				if (!found) {
					if (sb.Length > 0)
						sb.Append("\n");

					sb.Append(m);
					msgs.Add(m);
				}
			}
			ex = ex.InnerException;
		}
		return sb.ToString();
	}*/

	// ErrorCode is useless
	//private static String GetMessage(Exception ex) {
	//	String m = ex.Message;
	//	if (ex.GetType().Name == "MySqlException") {
	//		var ec = GetErrorCode(ex);
	//		if (ec.HasValue)
	//			m += " (Error code: " + ec.Value + ")";
	//	}
	//	return m;
	//}

	public static String GetTypesAndMessages(this Exception ex, String indent = "  ") {
		StringBuilder sb = new StringBuilder();
		int count = 0;
		while (ex != null) {
			if (count > 0)
				sb.AppendLine();

			for (int i = 0; i < count; i++)
				sb.Append(indent);

			String typeName = ex.GetType().FullName;
			String message = ex.Message;
			sb.Append(typeName).Append(":").Append(message);
			ex = ex.InnerException;
			count++;
		}
		return sb.ToString();
	}

	public static String GetAllMessages(this Exception ex, bool appendTagLine = true, AppendStackTrace appendStackTrace = AppendStackTrace.YesIfUnknown, bool skipFirstTargetInvocationException = true, StackTrace callingThreadStackTrace = null, int frameStartIndex = 0) {
		if (skipFirstTargetInvocationException && ex is TargetInvocationException && ex.InnerException != null)
			ex = ex.InnerException;

		StringBuilder sb = new StringBuilder();
		List<String> msgs = new List<String>();

		bool includeFullStackTrace = false; // includes the System. method lines in the stack trace

		Exception ex2 = ex;
		while (ex2 != null) {
			if (ex2 is FatalApplicationException)
				includeFullStackTrace = true;

			bool found = false;
			String em = ex2.Message;//GetMessage(ex2);
			foreach (String m in msgs) {
				if (m.IndexOf(em, StringComparison.InvariantCultureIgnoreCase) >= 0) {
					found = true;
					break;
				}
			}

			if (!found) {
				if (sb.Length > 0)
					sb.AppendLine();

				sb.Append(em);
				msgs.Add(em);				
			}

			//if (includeStackTrace && !ex2.IsKnownException() || includeFullStackTrace) {
			if (includeFullStackTrace || appendStackTrace == AppendStackTrace.Yes || appendStackTrace == AppendStackTrace.YesIfUnknown && !ex2.IsKnownException()) {
				String text = FormatStackTrace(ex2.StackTrace, includeFullStackTrace); // (includeFullStackTrace ? ex2.StackTrace : 
				if (text != null && text.Length > 0) {
					sb.AppendLine();
					sb.AppendLine(">" + ex2.GetType().FullName);
					sb.Append(text);
				}
			}
			ex2 = ex2.InnerException;
		}

		if (callingThreadStackTrace != null)
			AppendTo(callingThreadStackTrace, includeFullStackTrace, sb, frameStartIndex);

		if (appendTagLine) {
			if (!String.IsNullOrEmpty(ProductTitle))
				sb.AppendLine().AppendLine().Append(ProductTitle);

			sb.AppendLine().AppendLine();
			sb.Append(TagLine);
		}

		return sb.ToString();
	}

	public static String GetAllMessages(IList<Exception> errList, bool? appendTagLine = null) {
		StringBuilder sb = new StringBuilder();
		if (errList.Count > 1)
			sb.AppendLine(errList.Count + " Errors in Total:").AppendLine();

		bool allExceptionsKnown = true;
		foreach (Exception ex in errList) {
			if (!ex.IsKnownException()) {
				allExceptionsKnown = false;
				break;
			}
		}

		for (int i = 0; i < errList.Count; i++) {
			var e = errList[i];
			if (i > 0)
				sb.AppendLine().AppendLine();

			if (errList.Count > 1)
				sb.Append((i + 1) + ") ");

			bool atl = false;
			// only append the tagline for the last exception and only if at least one is unknown
			if (i == errList.Count - 1 && !allExceptionsKnown)
				atl = !appendTagLine.HasValue || appendTagLine.Value;

			sb.Append(e.GetAllMessages(atl));
		}
		return sb.ToString();
	}

	public static String ToString(StackTrace st, bool includeSystem, int startIndex) {
		StringBuilder sb = new StringBuilder();
		AppendTo(st, includeSystem, sb, startIndex);
		return sb.ToString();
	}

	private static bool IsNewline(char c) {
		return c == '\n' || c == '\r';
	}

	public static void AppendTo(StackTrace st, bool includeSystem, StringBuilder sb, int startIndex) {
		int n = st.FrameCount;
		bool needsNewline = (sb.Length > 0 && !IsNewline(sb[sb.Length - 1]));
		for (int i = startIndex; i < n; i++) {
			StackFrame f = st.GetFrame(i);
			MethodBase method = f.GetMethod();
			Type ty = method.DeclaringType;
			String fn = ty.FullName;
			if (!includeSystem && fn.StartsWith("System.") && sb.Length > 0)
				break;

			if (needsNewline) {
				sb.AppendLine();
				needsNewline = false;
			}

			int x = fn.LastIndexOf('`');
			if (x > 0)
				fn = fn.Substring(0, x);
			sb.Append(fn);
			if (ty.ContainsGenericParameters && !ty.IsGenericTypeDefinition) {
				AppendGenericParameters(sb, ty.GetGenericArguments());
			}
			sb.Append('.');
			sb.Append(method.Name);
			if (!method.IsGenericMethodDefinition)
				AppendGenericParameters(sb, method.GetGenericArguments());
			int line = f.GetFileLineNumber();
			if (line > 0)
				sb.Append(':').Append(line);
			needsNewline = true;
		}
	}

	private static void AppendGenericParameters(StringBuilder sb, Type[] arr) {
		if (arr == null || arr.Length == 0)
			return;

		sb.Append('<');
		for (int j = 0; j < arr.Length; j++) {
			if (j > 0)
				sb.Append(',');
			sb.Append(arr[j].Name);
		}
		sb.Append('>');
	}

	//private static int? GetErrorCode(Exception ex) {
	//	String errorCode = "" + GetValue(ex.GetType(), "ErrorCode", ex);
	//	int ec = 0;
	//	if (int.TryParse(errorCode, out ec))
	//		return ec;
	//	return null;
	//}

	//private static Object GetValue(Type ty, String name, Object obj) {
	//	var p = ty.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
	//	if (p != null)
	//		return p.GetValue(obj, null);
	//	var f = ty.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetField);
	//	if (f != null)
	//		return f.GetValue(obj);
	//	return null;
	//}
}

}