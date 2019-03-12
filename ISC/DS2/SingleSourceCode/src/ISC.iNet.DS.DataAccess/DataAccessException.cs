using System;
using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DataAccess
{
    public enum DataAccessErrorCode
    {
        NullId = -1,
        UniqueContraintViolation = -2,
        UpdateDeleteRuleViolation = -3
    }

    public class DataAccessException : ApplicationException
    {
        private DataAccessErrorCode _errorCode = 0;

        public DataAccessException( string msg ) : base( msg )
        {
        }

        public DataAccessException( DataAccessErrorCode errorCode ) : base()
        {
            _errorCode = errorCode;
        }

        public DataAccessException( string msg, DataAccessErrorCode errorCode )
            : base( string.Format( "{0} (DataAccessErrorCode = {1})", msg, errorCode ) )
        {
            _errorCode = errorCode;
        }

        public DataAccessException( string msg, Exception innerException, DataAccessErrorCode errorCode )
            : base( string.Format( "{0} (DataAccessErrorCode = {1})", msg, errorCode ), innerException )
        {
            _errorCode = errorCode;
        }

        public DataAccessException( string msg, Exception innerException )
            : base( msg, innerException )
        {
        }

        public DataAccessErrorCode ErrorCode
        {
            get
            {
                return _errorCode;
            }
        }
    }
}
