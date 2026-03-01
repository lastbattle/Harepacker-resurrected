using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Exceptions
{
    public class SerializationException : Exception
    {
        public SerializationException() : base() { }
        public SerializationException(string message) : base(message) { }
    }
}
