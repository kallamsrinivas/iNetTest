using System;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DomainModel
{
    public class Schema : ICloneable
    {
        private string _accountNum;
        private bool _activated;

        /// <summary>
        /// Indicates whether or not the docking station has been activated in iNet or not.
        /// </summary>
        public virtual bool Activated
        {
            get
            {
#if DEBUG
                if ( _activated )
                    ISC.WinCE.Logger.Log.Assert( string.IsNullOrEmpty( AccountNum ) == false, "Should not activated but not have an account number." );
#endif
                return _activated;
            }
            set
            {
                _activated = value;
            }
        }

        /// <summary>
        /// Indicates whether or not the account is a manufacturing account.
        /// </summary>
        public bool IsManufacturing { get; set; }

        /// <summary>
        /// Indicates the service type of the account
        /// </summary>
        public virtual string ServiceCode { get; set; }
       
        /// <summary>
        /// The version number of the database schema.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Date is in UTC.
        /// </summary>
        public DateTime? CylindersVersion { get; set; }

        /// <summary>
        /// Date is in UTC.
        /// </summary>
        public DateTime? SchedulesVersion { get; set; }

        /// <summary>
        /// Date is in UTC.
        /// </summary>
        public DateTime? EventJournalsVersion { get; set; }

        /// <summary>
        /// Date is in UTC.
        /// </summary>
        public DateTime? SettingsVersion { get; set; }

        /// <summary>
        /// Date is in UTC.
        /// </summary>
        public DateTime? EquipmentVersion { get; set; }

        /// <summary>
        /// //suresh 03-Feb-2012 INS-2622
        /// Date is in UTC.
        /// </summary>
        public DateTime? CriticalErrorsVersion { get; set; }
        
        /// <summary>
        /// If activated, then returns true if all 'version' dates are non-Null,
        /// which means everything has been synched for the account at least once.
        /// <para>
        /// If non-activated, then it always returns true., since we don't need data from
        /// iNet to operate in cal station mode.
        /// </para>
        /// </summary>
        public virtual bool Synchronized
        {
            get
            {
                if ( !Activated ) 
                {
                    // If we're not activated, then we're in cal station mode and we don't need to sync any iNet data.
                    return true;
                }
                else // Activated
                {
                    return this.CylindersVersion != null
                    && this.SchedulesVersion != null
                    && this.SettingsVersion != null
                    //Disabling check of event journals.  Necessary because for brand new accounts, there probably
                    //won't be any event journals, so the server will return us a null date.
                    //&& this.EventJournalsVersion != null
                    && this.CriticalErrorsVersion != null; //suresh 03-Feb-2012 INS-2622
                }
            }
        }

        public virtual string AccountNum
        {
            get
            {
                if ( _accountNum == null )
                    _accountNum = string.Empty;
                return _accountNum;
            }
            set
            {
                _accountNum = value;
            }
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public void Log()
        {
            WinCE.Logger.Log.Debug( WinCE.Logger.Log.Dashes );
            WinCE.Logger.Log.Debug( "Schema Version:           " + Version );
            WinCE.Logger.Log.Debug( "Schema Account Num:       " + AccountNum );
            WinCE.Logger.Log.Debug( "Schema Activated:         " + Activated );
            WinCE.Logger.Log.Debug( "Schema IsManufacturing:   " + IsManufacturing);
            WinCE.Logger.Log.Debug( "Schema Cylinders Version: " + WinCE.Logger.Log.DateTimeToString( CylindersVersion ) );
            WinCE.Logger.Log.Debug( "Schema Events Version:    " + WinCE.Logger.Log.DateTimeToString( EventJournalsVersion ) );
            WinCE.Logger.Log.Debug( "Schema Schedules Version: " + WinCE.Logger.Log.DateTimeToString( SchedulesVersion ) ) ;
            WinCE.Logger.Log.Debug( "Schema Settings Version:  " + WinCE.Logger.Log.DateTimeToString( SettingsVersion ) );
            WinCE.Logger.Log.Debug( "Schema Equipment Version: " + WinCE.Logger.Log.DateTimeToString( EquipmentVersion ) );
            WinCE.Logger.Log.Debug( "Schema Errors Version:    " + WinCE.Logger.Log.DateTimeToString( CriticalErrorsVersion ) ); //suresh 03-Feb-2012 INS-2622
            WinCE.Logger.Log.Debug( "Schema Service Code:      " + ServiceCode );            
            WinCE.Logger.Log.Debug( WinCE.Logger.Log.Dashes );
        }
    }
}
