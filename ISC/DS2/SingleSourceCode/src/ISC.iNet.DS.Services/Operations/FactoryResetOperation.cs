using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;



namespace ISC.iNet.DS.Services
{
    public class FactoryResetOperation : FactoryResetAction, IOperation
    {
        public FactoryResetOperation() { }

        public FactoryResetOperation( FactoryResetAction factoryResetAction ) : base( factoryResetAction ) 
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>null.</returns>
        public DockingStationEvent Execute()
        {
            Controller.Buzz( 0.1 );

            Log.Warning( string.Format( "{0} invoking PrepareForReset", Name ) );
            Master.Instance.PrepareForReset();

            // Just delete the databases.  They will automatically get restored to blank databases after we reboot.

            DS.DataAccess.DataAccess.DatabaseDelete( Controller.INET_DB_NAME );

            DS.DataAccess.DataAccess.DatabaseDelete( Controller.INETQ_DB_NAME );

            if (FullReset == true)  // SGF  29-Apr-2011  INS-3563  modifying the behavior to reset to defaults, etc. IFF this is considered a "full" reset
            {
                Log.Info( "Resetting configuration to factory defaults..." );

                Configuration.ResetToDefaults();

				// Purge the log messages and turn off the LogToFile feature.
				Log.PurgeLogFiles( true );
            }

            Controller.Buzz( 0.1 );
            Controller.PerformSoftReset();

            return null;
        }
    }
}
