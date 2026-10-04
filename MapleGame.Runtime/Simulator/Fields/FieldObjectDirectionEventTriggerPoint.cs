using System;

namespace HaCreator.MapSimulator.Fields
{
    internal sealed class FieldObjectDirectionEventTriggerPoint
    {
        public FieldObjectDirectionEventTriggerPoint(int x, int y, FieldObjectScriptPublication[] scriptPublications)
        {
            X = x;
            Y = y;
            ScriptPublications = scriptPublications ?? Array.Empty<FieldObjectScriptPublication>();
        }

        public int X { get; }

        public int Y { get; }

        public FieldObjectScriptPublication[] ScriptPublications { get; }
    }
}
