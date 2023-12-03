using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace TimePicker.Opulos.Core.Utils;

public static class ExceptionEx_Rethrow
{
    public static void Rethrow(this Exception ex)
    {
        if (ex == null)
            return;

        if (ex is TargetInvocationException && ex.InnerException != null)
            ex = ex.InnerException;

        // requires NET4.5 or higher. If using lower version then comment out this line.
        ExceptionDispatchInfo.Capture(ex).Throw();
        throw ex; // avoids compiler complaints
    }
}