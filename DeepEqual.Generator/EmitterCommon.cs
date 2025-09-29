using System;

namespace DeepEqual.Generator;

internal static class EmitterCommon
{
    internal static void EmitEnsureOnce(CodeWriter w, string ensureMethodName, string guardFieldName, string lockFieldName, Action<CodeWriter> emitBody, params string[] prerequisites)
    {
        w.Line("private static int " + guardFieldName + ";");
        w.Line("private static readonly object " + lockFieldName + " = new object();");
        w.Line();

        w.Open("private static void " + ensureMethodName + "()");
        w.Open("if (System.Threading.Volatile.Read(ref " + guardFieldName + ") == 1)");
        w.Line("return;");
        w.Close();
        w.Open("lock (" + lockFieldName + ")");
        w.Open("if (System.Threading.Volatile.Read(ref " + guardFieldName + ") == 1)");
        w.Line("return;");
        w.Close();
        foreach (var line in prerequisites)
            w.Line(line);
        emitBody(w);
        w.Line("System.Threading.Volatile.Write(ref " + guardFieldName + ", 1);");
        w.Close();
        w.Close();
        w.Line();
    }

    internal static void EmitModuleInitializer(CodeWriter w, string moduleInitName, string ensureMethodName)
    {
        w.Line("[System.Runtime.CompilerServices.ModuleInitializer]");
        w.Open("internal static void " + moduleInitName + "()");
        w.Line(ensureMethodName + "();");
        w.Close();
        w.Line();
    }
}