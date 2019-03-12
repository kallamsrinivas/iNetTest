using System;
using System.Runtime.Serialization;

namespace ISC.iNet.DS.Services
{
    [Serializable]
    internal class PrinterCEException : Exception
    {
        public PrinterCEException()
        {
        }

        public PrinterCEException(string message) : base(message)
        {
        }

        public PrinterCEException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected PrinterCEException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}