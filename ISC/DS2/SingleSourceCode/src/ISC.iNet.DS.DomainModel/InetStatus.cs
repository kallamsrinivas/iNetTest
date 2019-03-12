using System;

using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{                    
    public class InetStatus : ICloneable
    {
        #region Fields

        private DateTime _currentTime; 
        private Schema _schema = new Schema();

        private string _error;

        #endregion Fields

        #region Constructors

        public InetStatus() { }

        public InetStatus( string error ) { _error = error; }

        #endregion Constructors

        #region Properties

        /// <summary>
        /// Will be in UTC.
        /// </summary>
        public DateTime CurrentTime
        {
            get { return _currentTime; }
            set { _currentTime = value; }
        }

        public Schema Schema { get { return _schema; } }

        /// <summary>
        /// Empty if no error connecting to iNet.  Else contains the error.
        /// </summary>
        public string Error
        {
            get
            {
                if ( _error == null ) _error = string.Empty;
                return _error;
            }
            set { _error = value; }
        }

        #endregion Properties

        #region Methods

        public object Clone()
        {
            InetStatus inetStatus = (InetStatus)this.MemberwiseClone();
            inetStatus._schema = (Schema)this._schema.Clone();
            return inetStatus;
        }

        #endregion Methods

    } // end-class

} // end-namespace
