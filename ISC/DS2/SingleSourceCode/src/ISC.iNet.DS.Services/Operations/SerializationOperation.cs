using System;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.iNet;
using ISC.WinCE;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Provides functionality to update the ids's configuration setting.
    /// </summary>
    public class SerializationOperation : SerializationAction , IOperation
    {
        #region Constructors
		
        /// <summary>
        /// Creates a new instance of SerializationOperation class.
        /// </summary>
        public SerializationOperation() 
        {
            // Do Nothing
        }

        public SerializationOperation( SerializationAction serializationAction )
            : base( serializationAction )
        {
        }

        #endregion

        #region Methods

        /// <summary>
        /// Executes an instrument set server ip address settings operation.
        /// </summary>
        /// <returns>Docking station event</returns>
        public DockingStationEvent Execute()
        {
            // First connect to iNet by doing an ExchangeStatus call. The intention is that we
            // get back an account number needed to subsequently upload the DockingStation info
            // (this will probably be a manufacturing account).  Also, we need to set the clock
            // to the proper time in order get a proper SetupDate.

            // NOTE: IF THIS ExchangeStatus FAILS (THE WEB SERVICE RETURNS NULL), A LIKELY
            // CULPRIT IS THAT THE SERVER HAS NOT BEEN CORRECTLY CONFIGURED TO KNOW WHAT IT'S
            // MANUFACTURING ACCOUNT IS!!!

            InetStatus inetStatus = null;

            using ( InetDownloader inet = new InetDownloader( DockingStation, Configuration.Schema ) )
            {
                Log.Info( "Calling ExchangeStatus for new S/N " + DockingStation.SerialNumber );
                inetStatus = inet.ExchangeStatus( Name, string.Empty, null, null, true );

                // TODO - what should we do with the error?
                if ( inetStatus.Error != string.Empty )
                    throw new ApplicationException( inetStatus.Error );

                Log.Info( string.Format( "ExchangeStatus successful for S/N {0}!", DockingStation.SerialNumber ) );

                // TODO - will the following ever happen? We need the current time in order
                // to properly set the SetupDate.
                if ( inetStatus.CurrentTime == DomainModelConstant.NullDateTime )
                    throw new ApplicationException( "No current time returned by iNet." ); // TODO

                // TODO - will this ever happen?
                if ( inetStatus.Schema.AccountNum == string.Empty )
                    throw new ApplicationException( "No account number returned by iNet" );

                // TODO - what about the time zone?  CurrentTime will be in UTC.  We probably
                // need to make sure that we set the time in the context of Eastern so that
                // SetupDate is in Eastern.
                SystemTime.SetSystemTime( inetStatus.CurrentTime );

            }
            // Upload the docking station's info to Inet.  

            Log.Info( string.Format( "Uploading new serialization info to iNet (S/N {0})", DockingStation.SerialNumber ) );

            using ( InetUploader inet = new InetUploader( DockingStation, Configuration.Schema ) )
            {
                string uploadError = inet.UploadDockingStation( DockingStation, DateTime.UtcNow, inetStatus.Schema.AccountNum, Configuration.DockingStation.TimeZoneInfo );
                if ( uploadError != string.Empty )
                    throw new ApplicationException( uploadError );  // should we handle failure in a better way?
            }

            Log.Info( string.Format( "Upload successful!", DockingStation.SerialNumber ) );

            // If we make it to here, then we must have successfully uploaded the serialization
            // info to iNet and we assume that iNet now has knowledge of this docking station.
            // We now actually save the serialization info.

            if (ShouldSave == true) // SGF  29-Apr-2011  INS-3563  Added if-statement
                Configuration.Serialize(DockingStation);

            return null;
        }

        #endregion
    }
}
