using System;
using System.Text;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Services.Resources;


namespace ISC.iNet.DS.Services
{
    /// <summary>
    /// Summary description for Detail.
    /// </summary>
    internal class DetailsBuilder
    {
        private const string NEWLINE = "\n";

        private StringBuilder _builder;

        /// <summary>
        /// The table for language entries.
        /// </summary>

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="builder">StringBuilder to format into</param>
        internal DetailsBuilder( string details )
        {
            _builder = new StringBuilder( details, 5000 );
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="builder">StringBuilder to format into</param>
        internal DetailsBuilder() : this( string.Empty ) { }

        /// <summary>
        /// 
        /// </summary>
        public override string ToString()
        {
            return _builder.ToString();
        }

        internal void AddDockingStation( DockingStation dockingStation )
        {
            Add( "",  DiagnosticResources.DETAILS_DOCKINGSTATION_HEADER, "" );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_SERIALNUMBER, dockingStation.SerialNumber );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_TYPE, dockingStation.Type );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_PART_NUMBER, dockingStation.PartNumber );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_NUM_GAS_PORTS, dockingStation.NumGasPorts.ToString() );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_RESERVOIR, dockingStation.Reservoir ? DiagnosticResources.TRUE : DiagnosticResources.FALSE );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_SETUP_DATE, dockingStation.SetupDate );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_SETUP_TECH, dockingStation.SetupTech );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_HARDWARE_VERSION, dockingStation.HardwareVersion );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_SOFTWARE_VERSION, dockingStation.SoftwareVersion );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_MAC_ADDRESS, dockingStation.NetworkSettings.MacAddress );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_IP_ADDRESS, dockingStation.NetworkSettings.IpAddress );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_LANGUAGE, dockingStation.Language );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_MENU_LOCKED, dockingStation.MenuLocked );
            Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_USE_AUDIBLE_ALARM, dockingStation.UseAudibleAlarm ); // SGF  Feb-24-2009  DSW-136
        }

        /// <summary>
        /// Add a detail to a string _builder, with the identification and value formatted.
        /// </summary>
        /// <param name="indent">The ammount to indent the detail by.</param>
        /// <param name="label">The label of the detail to add.</param>
        /// <param name="theValue">The value of the detail.</param>
        internal string Add( string indent, string label, string theValue )
        {
            string detail;
            if ( label.Length > 0 )
                detail = string.Format( "{0,-40} {1}", indent + label + ":", theValue );
            else
                detail = string.Format( "{0,-40} {1}", indent + label + " ", theValue );
            _builder.Append( detail );
            _builder.Append( "\n" );

            return detail;
        }

        /// <summary>
        /// Add an integer to the details.
        /// </summary>
        /// <param name="indent">The indentation for the option.</param>
        /// <param name="label">The label of the detail to add.</param>
        /// <param name="integer">The integer to add.</param>
        internal string Add( string indent, string label, int integer )
        {
            return Add( indent, label, integer.ToString() );
        }

        /// <summary>
        /// Add a double to the details.
        /// </summary>
        /// <param name="indent">The indentation for the option.</param>
        /// <param name="label">The label of the detail to add.</param>
        /// <param name="number">The double to add.</param>
        internal string Add( string indent, string label, double number )
        {
            return Add( indent, label, number == double.MinValue ? string.Empty : number.ToString() );
        }

        /// <summary>
        /// Add a blank line to the details.
        /// </summary>
        internal string AddNewLine()
        {
            _builder.Append( NEWLINE );

            return NEWLINE;
        }

        /// <summary>
        /// Add a specific line of text to the details.
        /// </summary>
        internal string Add( string text )
        {
            _builder.Append( text );

            return text;
        }

        /// <summary>
        /// Add a enumerated type to the details.
        /// </summary>
        /// <param name="indent">The indentation to place before the details.</param>
        /// <param name="label">The label of the detail to add.</param>
        /// <param name="theEnum">The enumerated type to add to the details.</param>
        internal string Add( string indent, string label, Enum theEnum )
        {
            return Add( indent, label, GetText( theEnum ) );
        }

        /// <summary>
        /// Add a boolean to the details.
        /// </summary>
        /// <param name="indent">The indentation to place before the details.</param>
        /// <param name="label">The label of the detail to add.</param>
        /// <param name="type">The component type to add to the details.</param>
        internal string Add( string indent, string label, bool boolean )
        {
            return Add( indent, label, GetText( boolean ) );
        }

        /// <summary>
        /// Add a language to the details.
        /// </summary>
        /// <param name="indent">The indentation to place before the details.</param>
        /// <param name="label">The label of the detail to add.</param>
        /// <param name="lang">The language to add to the details.</param>
        internal string Add( string indent, string label, Language lang )
        {
            return Add( indent, label, GetText( lang ) );
        }

        /// <summary>
        /// Add a time as a detail.
        /// </summary>
        /// <param name="indent">The indentation for the detail.</param>
        /// <param name="label">The label of the detail.</param>
        /// <param name="time">The time to translate.</param>
        internal string Add( string indent, string label, DateTime time )
        {
            return Add( indent, label, GetText( time ) );
        }

        /// <summary>
        /// Retrieve a translated value.
        /// </summary>
        /// <param name="boolean">The value to translate.</param>
        /// <returns>The translated value.</returns>
        internal string GetText( bool boolean )
        {
            return boolean ? DiagnosticResources.TRUE : DiagnosticResources.FALSE;
        }

        /// <summary>
        /// Retrieve a translated value.
        /// </summary>
        /// <param name="language">The value to translate.</param>
        /// <returns>The translated value.</returns>
        internal string GetText( Language language )
        {
            return GetText( language.Code );
        }

        /// <summary>
        /// Retrieve a translated value.
        /// </summary>
        /// <param name="theEnum">The value to translate.</param>
        /// <returns>The translated value.</returns>
        internal string GetText( Enum theEnum )
        {
            return GetText( theEnum.ToString() );
        }

        internal string GetText( string textId )
        {
            // SGF  27-Jan-2011  INS-2869
            string text = DiagnosticResources.ResourceManager.GetString( textId, DiagnosticResources.Culture );
            return ( text == null ) ? textId : text;
        }

        /// <summary>
        /// Translate a DateTime into the appropriate format for the language.
        /// </summary>
        /// <param name="time">The time to translate.</param>
        /// <returns>The string representation.</returns>
        internal string GetText(DateTime time)
        {
            switch (Configuration.DockingStation.Language.Code)
            {
                case Language.French:
                    return time.Day.ToString().PadLeft(2, '0') + "/" + time.Month.ToString().PadLeft(2, '0') + "/" + time.Year.ToString() + " " + time.Hour.ToString().PadLeft(2, '0') + ":" + time.Minute.ToString().PadLeft(2, '0');

                case Language.German:
                    return time.Day.ToString().PadLeft(2, '0') + "." + time.Month.ToString().PadLeft(2, '0') + "." + time.Year.ToString() + " " + time.Hour.ToString().PadLeft(2, '0') + ":" + time.Minute.ToString().PadLeft(2, '0');

                case Language.Spanish:  // SGF  23-May-2011  INS-1741
                    return time.Day.ToString().PadLeft(2, '0') + "/" + time.Month.ToString().PadLeft(2, '0') + "/" + time.Year.ToString() + " " + time.Hour.ToString().PadLeft(2, '0') + ":" + time.Minute.ToString().PadLeft(2, '0');

                case Language.PortugueseBrazil:  // SGF  1-Oct-2012  INS-1656
                    return time.Day.ToString().PadLeft(2, '0') + "/" + time.Month.ToString().PadLeft(2, '0') + "/" + time.Year.ToString() + " " + time.Hour.ToString().PadLeft(2, '0') + ":" + time.Minute.ToString().PadLeft(2, '0');

                default: // Language.English
                    return time.ToShortDateString() + " " + time.ToShortTimeString();
            }
        }


    }  // end-class

} // end-namespace
