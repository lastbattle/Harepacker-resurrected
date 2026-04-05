using System;

namespace HaCreator.MapSimulator.Fields
{
    internal sealed class FieldObjectScriptPublication
    {
        public FieldObjectScriptPublication(string scriptName, int delayMs = 0)
        {
            ScriptName = scriptName ?? string.Empty;
            DelayMs = Math.Max(0, delayMs);
        }

        public string ScriptName { get; }

        public int DelayMs { get; }
    }
}
