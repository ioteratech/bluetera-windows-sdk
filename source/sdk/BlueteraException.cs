using System;
using System.Collections.Generic;
using System.Text;

namespace Bluetera
{
    public class BlueteraException : Exception
    {
        public BlueteraException()
        {
        }

        public BlueteraException(string message)
            : base(message)
        {
        }

        public BlueteraException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
