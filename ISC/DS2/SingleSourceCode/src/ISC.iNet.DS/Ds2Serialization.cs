using System;
using System.IO;
using System.Threading;
using System.Xml;
using ISC.WinCE.Logger;
using ISC.iNet.DS.DomainModel;


namespace ISC.iNet.DS
{

	/// <summary>
	/// Provides methods for retrieving DS2 information from its serialization XML file.
	/// </summary>
	internal sealed class Ds2Serialization
	{
        public static readonly object FILE_SYSTEM_LOCK = new object(); // just used to synchronize on

        private const string FILE_NAME_FACTORY_INFO = @"\Reliance_Flash\Factory.xml";
        private const string FILE_NAME_FACTORY_BACK = @"\Reliance_Flash\Factory.bak";
        
		/// <summary>
		/// Private constructor to enforce only static use of class.
		/// </summary>
		private Ds2Serialization()
		{
			// Do nothing
		}


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal static DockingStation Load()
        {
            DockingStation dockingStation = new DockingStation();

            try
            {
                LoadSerialization( dockingStation );
            }
            catch ( System.IO.FileNotFoundException fnfe )
            {
                // It's expected that the file won't be found. So don't log the whole stack trace.
                Log.Error( "Ds2Serialization.Load - " + fnfe.Message );
                dockingStation = null;
            }
            catch ( Exception e )
            {
                Log.Error( "Ds2Serialization.Load", e );
                dockingStation = null;
            }

            return dockingStation;
        }

        /// <summary>
        /// Loads serialization info from XML file.
        /// </summary>
        /// The following settings are loaded by this routine...
        /// 
        /// serialNumber
        /// partNumber
        /// jobNumber
        /// setupDate
        /// setupTech
        /// hardwareVersion
        /// flowOffset
        /// 
        /// <param name="dockingStation">Settings are stuffed into this</param>
        private static void LoadSerialization( DockingStation dockingStation )
        {
            XmlElement root;
            XmlNodeList xnodes;
            XmlDocument xmlDom = new XmlDocument();

            // Load the document and read docking station information.
            xmlDom.Load( FILE_NAME_FACTORY_INFO );

            root = xmlDom.DocumentElement;

            // Get docking station's serial number.
            dockingStation.SerialNumber = root.Attributes[ "serialNumber" ].Value;

            // Get docking station's type.
            dockingStation.Type = ConvertDeviceType( root.Attributes[ "type" ].Value );

            // Get docking station's part number.
            xnodes = root.GetElementsByTagName( "partNumber" );
            if ( xnodes.Count > 0 )
                dockingStation.PartNumber = xnodes[ 0 ].InnerText;

            // Get docking station's setup technician.
            xnodes = root.GetElementsByTagName( "numberOfGasPorts" );
            if ( xnodes.Count > 0 )
                dockingStation.NumGasPorts = int.Parse( xnodes[0].InnerText );

            // Get docking station's setup technician.
            xnodes = root.GetElementsByTagName( "setupTech" );
            if ( xnodes.Count > 0 )
                dockingStation.SetupTech = xnodes[ 0 ].InnerText;

            // Get docking station's setup date.
            xnodes = root.GetElementsByTagName( "setupDate" );
            if ( xnodes.Count > 0 )
            {
                try
                {
                    dockingStation.SetupDate = Convert.ToDateTime( xnodes[ 0 ].InnerText );
                }
                catch ( Exception e )
                {
                    Log.Warning( "Error parsing SetupDate string \"" + Convert.ToDateTime( xnodes[ 0 ].InnerText + "\"" ), e ); 
                    dockingStation.SetupDate = DateTime.MinValue;
                    Log.Warning( "Defaulting to " + dockingStation.SetupDate );
                }
            }

            // Get docking station's Flow Offset value as set during interactive diagnostics.
            xnodes = root.GetElementsByTagName( "flowOffset" );
            if ( xnodes.Count > 0 ) // Might not be present in older IDS's
                dockingStation.FlowOffset = int.Parse( xnodes[ 0 ].InnerText );

            // If we found a file to import, then when this docking station
            // was a DS2, it must have been DSX DS2, which means it would not
            // have an internal reservoir.
            dockingStation.Reservoir = false;

        } // end-LoadSerialization

        /// <summary>
        /// Convert a string into the appropriate device type.
        /// </summary>
        /// <param name="type">The string to convert.</param>
        /// <returns>The appropriate device type.</returns>
        private static DeviceType ConvertDeviceType( string type )
        {
            DeviceType deviceType = DeviceType.Unknown;

            try
            {
                deviceType = (DeviceType)Enum.Parse( typeof(DeviceType), type, true );
            }
            catch ( Exception e )
            {
                Log.Error( string.Format( "ConvertDeviceType: Type=\"{0}\"", type ), e );
            }

            return deviceType;
        }
	}

	/// <summary>
	/// The exception that is thrown when the DS2 serialization  
    /// info could not be loaded  for some unexpected reason.
	/// </summary>
	public class Ds2SerializationXmlException : Exception
	{
        public Ds2SerializationXmlException( string msg, Exception e ) : base ( msg, e ) {}
	}
}