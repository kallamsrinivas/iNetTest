using System;


namespace ISC.iNet.DS.DomainModel
{
    public class DomainModelException : ApplicationException
    {
        public DomainModelException( string msg ) : base( msg )
        {
        }

        public DomainModelException( string msg, Exception innerException )
            : base( msg, innerException )
        {
        }
    }
}
