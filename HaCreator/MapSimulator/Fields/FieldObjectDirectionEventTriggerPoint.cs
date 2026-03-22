using System;

namespace HaCreator.MapSimulator.Fields
{
    internal sealed class FieldObjectDirectionEventTriggerPoint
    {
        public FieldObjectDirectionEventTriggerPoint(int x, int y, string[] scriptNames)
        {
            X = x;
            Y = y;
            ScriptNames = scriptNames ?? Array.Empty<string>();
        }

        public int X { get; }

        public int Y { get; }

        public string[] ScriptNames { get; }
    }
}
