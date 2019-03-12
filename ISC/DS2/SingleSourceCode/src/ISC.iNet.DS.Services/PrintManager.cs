using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Services.Resources;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services 
{
    public class PrintManager
	{
		#region Fields

		private const string PRINTERCE_LICENSE_KEY = "729D1G170X";
        //private const string BARCODECE_LICENSE_KEY = "<BarcodeCE license goes here>";

        private const int PropertyGapWidth = 500;
        private const int PageWidth = 10000;
        private const int PageCenter = PageWidth / 2;
        private const int SectionVerticalGap = 500;
        private const int CellMargin = 50;

		// title and header section are positioned independently and could overlap
        private const int TitleYCoord = 0;
		private const int HeaderYCoord = 250;

        private const int TitleFontSize = 26;
        private const int HeaderFontSize = 10;
        private const int BodyFontSize = 8;
        private const int FooterFontSize = 12;

        private const int NumBumpSensorColumns = 11;
        private const int NumBumpCylinderColumns = 5;
        private const int NumCalSensorColumns = 11;
        private const int NumCalCylinderColumns = 7;

        private const string SplitCandidateCharacters = " /-";

        private const string CREATEDBY = "DSX";

        private const double MARGINAL_SPANRESERVE = .70;

        // SGF  18-Jan-2011  INS-1301
        private CultureInfo sortPrintCulture;

		// format strings
		private const string FORMAT_DATE = "d";		// "6/15/2009"
		private const string FORMAT_DATETIME = "G";	// "6/15/2009 1:45:30 PM"
		private const string FORMAT_DECIMAL = "F2";	// 1234.57
		private const string FORMAT_INTEGER = "F0"; // 1234
		private const string FORMAT_PERCENT = "P2"; // 100.00 %
		private const string FORMAT_DATE_ENGUS = "dd MMM yyyy"; // 06 Oct 2005 
		private const string FORMAT_DATETIME_ENGUS = "dd MMM yyyy h:mm:ss tt"; // 06 Oct 2005 1:45:30 PM

		// gets set to true if communication was lost with the USB printer while printing
		private bool hasAborted;

        public object PrinterCE_Base { get; private set; }

        #endregion

        #region Constructors

        public PrintManager()
        {
		}

		#endregion

		#region Methods

		/// <summary>
        /// Create instance of PrinterCE class and pass developer's license key
        /// </summary>
        /// <returns></returns>
        private PrinterCE CreatePrinterCE()
        {
			this.hasAborted = false;

            //         PrinterCE prce = new PrinterCE(PrinterCE.EXCEPTION_LEVEL.ALL, PRINTERCE_LICENSE_KEY)
            //         {
            //             PrDialogBox = PrinterCE_Base.DIALOGBOX_ACTION.DISABLE
            //         };
            //         prce.SetupPrinter(PrinterCE_Base.PRINTER.HP_PCL, PrinterCE_Base.PORT.LPT, false); // changed port argument value back to LPT from NETPATH
            //prce.SetReportLevel = PrinterCE.REPORT_LEVEL.NO_ERRORS; // prevent pop-up Win CE alert boxes that will pause the application

            return new PrinterCE();
        }

        private bool IsUsbPrinterAttached()
        {
            if ( !Controller.IsUsbPrinterAttached() ) // We can't print if there's no printer.
            {
                Log.Debug( "No USB printer is detected." );
                return false;
            }
            Log.Debug( "USB printer is detected." );
            return true;
        }

		public void PrintTestPage()
		{
			//Log.Debug( "PrintTestPage: started." );

			//if ( !IsUsbPrinterAttached() )
			//{
			//	Log.Debug( "Can't print certificate." );
			//	Log.Debug( "PrintTestPage: finished." );
			//	return;
			//}
			
			//Log.Debug( "PrintTestPage: Creating test page..." );
			//using ( PrinterCE prce = CreatePrinterCE() )
			//{
			//	try
			//	{
			//		prce.DrawText( "iNet DS Printer Test Page", 0, 0 );
			//		prce.DrawText( "Submitted Time: " + Log.DateTimeToString( DateTime.UtcNow ), 0, 250 );

			//		int y = 250;
			//		for ( int i = 0; i < 12; i++ )
			//		{
			//			y += 500;
			//			prce.DrawText( "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 0, y );
			//			prce.DrawText( "abcdefghijklmnopqrstuvwxyz", 0, y += 250 );
			//			prce.DrawText( "1234567890`~!@#$%^&*()-_=+[]{}\\|;:<>?,./'\"", 0, y += 250 );
			//		}

			//		y += 500;
			//		prce.DrawText( "------------------------------------------------------- End of Page -------------------------------------------------------", 0, y ); 

			//		Log.Debug( "PrintTestPage: Calling EndDoc..." );
			//		prce.EndDoc(); // Print the page
			//		Log.Debug( "PrintTestPage: EndDoc finished." );
			//	}
			//	catch ( PrinterCEException pcee )
			//	{
			//		Log.Error( "PrintBumpCert: PrinterCEException" );
			//		Log.Error( pcee.Message, pcee );

			//		// An unknown issue is resulting in a lost connection with the USB printer.
			//		// An error is not thrown when using PrinterCE.NetCF.dll earlier than version 1.2.7.2.
			//		if ( prce.GetLastError == PrinterCE_Base.ERROR.COMMUNICATION_LOST )
			//		{
			//			this.hasAborted = true;
			//		}
			//	}
			//	finally
			//	{
			//		// We have a 2nd check for the lost connection with the USB printer because
			//		// we have not had enough time to test the 1.2.7.2 dll to know that an exception will 
			//		// always be thrown with the expected last error.
			//		if ( this.hasAborted == false )
			//		{
			//			// Is there now an LPT2:?
			//			this.hasAborted = Controller.IsUsbPrinterNotOnLpt1();
			//		}
			//	}
			//}

			//Log.Debug( "PrintTestPage: finished." );
		}

		public void Print( InstrumentGasResponseEvent gasEvent )
		{
            // We currently only print calibration data
            //
            // We only auto-print automated calibrations, not manual calibrations.
            // InstrumentCalibrationEvent is assumed to be automated. 
            //
            // We only auto-print automated bumps, not manual bumps.
            // InstrumentBumpTestEvent is assumed to be automated.
//			if ( ( gasEvent is InstrumentCalibrationEvent ) == false
//			&&   ( gasEvent is InstrumentBumpTestEvent ) == false )
//				return;

//			PrintManagerResources.Culture = Configuration.DockingStation.Language.Culture;

//			PrintCertificate( gasEvent );

//			// if communication was lost with the printer, attempt to reprint
//			if ( this.hasAborted )
//			{
//#if DEBUG
//				PrintManager.Reprint( gasEvent );
//#else // RELEASE, RELEASE QA
//				Log.Debug( "Reprint functionality is disabled." );
//#endif
//			}
//		}

//		private void PrintCertificate( InstrumentGasResponseEvent gasEvent )
//		{
//			try
//			{
//				if ( !IsUsbPrinterAttached() ) // We can't print if there's no printer.
//				{
//					Log.Debug( "Can't print certificate." );
//					return;
//				}

//				if ( ( gasEvent is InstrumentCalibrationEvent ) == true )
//				{
//					Log.Debug( "Printing calibration certificate." );
//					PrintCalCert( (InstrumentCalibrationEvent)gasEvent, Configuration.DockingStation.Language.Culture );
//				}
//				else if ( ( gasEvent is InstrumentBumpTestEvent ) == true )
//				{
//					Log.Debug( "Printing bump certificate." );
//					PrintBumpCert( (InstrumentBumpTestEvent)gasEvent, Configuration.DockingStation.Language.Culture );
//				}
//			}
//			catch ( Exception ex )
//			{
//				Log.Error( "Error printing " + gasEvent, ex );
//			}

		}

		private void PrintCalCert( InstrumentCalibrationEvent calEvent, CultureInfo printCulture )
		{
			//Log.Debug( "PrintCalCert: entered method" );

			//using ( PrinterCE prce = CreatePrinterCE() )
			//{
			//	try
			//	{
			//		GenerateCalCert( prce, calEvent, printCulture );
			//		prce.EndDoc(); // Print the page
			//	}
			//	catch ( PrinterCEException pcee )
			//	{
			//		Log.Error( "PrintCalCert: PrinterCEException" );
			//		Log.Error( pcee.Message, pcee );

			//		// An unknown issue is resulting in a lost connection with the USB printer.
			//		// An error is not thrown when using PrinterCE.NetCF.dll earlier than version 1.2.7.2.
			//		if ( prce.GetLastError == PrinterCE_Base.ERROR.COMMUNICATION_LOST )
			//		{
			//			this.hasAborted = true;
			//		}
			//	}
			//	finally
			//	{
			//		// We have a 2nd check for the lost connection with the USB printer because
			//		// we have not had enough time to test the 1.2.7.2 dll to know that an exception will 
			//		// always be thrown with the expected last error.
			//		if ( this.hasAborted == false )
			//		{
			//			// Is there now an LPT2:?
			//			this.hasAborted = Controller.IsUsbPrinterNotOnLpt1();
			//		}
			//	}
			//} // Dispose will take care of the Shutdown actions

			//Log.Debug( "PrintCalCert: leaving method" );
		}
		
		private void GenerateCalCert( PrinterCE prce, InstrumentCalibrationEvent calEvent, CultureInfo printCulture )
		{
			//Log.Debug( "generateCalCert: entered method" );

			//string value_NA = PrintManagerResources.Value_NA;
			//bool isPrintCultureEngUS = printCulture.Name == "en-US";

			//// special date formatting when the culture is en-US
			//string dateFormat = FORMAT_DATE;
			//string dateTimeFormat = FORMAT_DATETIME;
			//if ( isPrintCultureEngUS )
			//{
			//	dateFormat = FORMAT_DATE_ENGUS;
			//	dateTimeFormat = FORMAT_DATETIME_ENGUS;
			//}

   //         // Is there at least one sensor that has a correlation factor?
   //         bool hasFactor = calEvent.GasResponses.Find( sgr => SensorCode.IsCombustible( sgr.SensorCode ) && !String.IsNullOrEmpty( sgr.GasDetected ) ) != null;

			//// TITLE
			//prce.FontSize = TitleFontSize;
			//prce.FontBold = true;
			//prce.JustifyHoriz = PrinterCE_Base.JUSTIFY_HORIZ.CENTER;
			//prce.DrawText( PrintManagerResources.Title_CalibrationCertificate, PageCenter, TitleYCoord );

			//prce.JustifyHoriz = PrinterCE_Base.JUSTIFY_HORIZ.LEFT;

			//// HEADER SECTION

			//// OBTAIN HEADER INFORMATION

			//// Calibration Date.  It will always be specified.  It's also in UTC, so
			//// we need to convert it to the time zone's local time.
			//DateTime localCalTime = Configuration.DockingStation.TimeZoneInfo.ToLocalTime( calEvent.Time );
			//string calTime = localCalTime.ToString( dateFormat, printCulture );

			//// Part Number
			//string partNumber = calEvent.DockedInstrument.PartNumber;
			//if ( String.IsNullOrEmpty( partNumber ) )
			//	partNumber = value_NA;

			//// Job Number
			//string jobNumber = calEvent.DockedInstrument.JobNumber;
			//if ( String.IsNullOrEmpty( jobNumber ) )
			//	jobNumber = value_NA;

			//// Setup Date
			//string setupDate = calEvent.DockedInstrument.SetupDate.ToString( dateFormat, printCulture );
			//if ( String.IsNullOrEmpty( setupDate ) )
			//	setupDate = value_NA;

			//// Setup Technician
			//string setupTech = calEvent.DockedInstrument.SetupTech;
			//if ( String.IsNullOrEmpty( setupTech ) )
			//	setupTech = value_NA;

			//// Battery
			//string battery = String.Empty;
			//string batteryType = String.Empty;
			//foreach ( InstalledComponent ic in calEvent.DockedInstrument.InstalledComponents )
			//{
			//	if ( ic.Component is Battery )
			//	{
			//		battery = ic.Component.Type.Code;
			//		break;
			//	}
			//}
			//if ( String.IsNullOrEmpty( battery ) )
			//	batteryType = value_NA;
			//else
			//	batteryType = PrintManagerResources.ResourceManager.GetString( battery, printCulture );

			//// PRINT HEADER SECTION INFORMATION
			//string[] PropertyLabels = new string[]
   //             {
   //                 PrintManagerResources.Label_InstrumentSN,
   //                 PrintManagerResources.Label_CalibrationDate,
   //                 PrintManagerResources.Label_PartNumber,
   //                 PrintManagerResources.Label_JobNumber,
   //                 PrintManagerResources.Label_SetupDate,
   //                 PrintManagerResources.Label_SetupTechnician,
   //                 PrintManagerResources.Label_CreatedBy,
   //                 PrintManagerResources.Label_Battery
   //             };

			//string[] PropertyValues = new string[]
   //             {
   //                 calEvent.DockedInstrument.SerialNumber,
   //                 calTime,
   //                 partNumber,
   //                 jobNumber,
   //                 setupDate,
   //                 setupTech,
   //                 CREATEDBY,
   //                 batteryType
   //             };

			//int propertySectionBottomY = DrawPropertySection( prce, HeaderYCoord, PropertyLabels, PropertyValues );


			//// BODY SECTION

			//// set coordinates of sensor grid
			//int topSensorSectionY = propertySectionBottomY + SectionVerticalGap;
			//int leftSGRSectionX = 0; int rightSGRSectionX = PageWidth;
			//int cellMargin = CellMargin;

   //         // create 2-dimension [#SGRs + 1, #SGR properties] array of strings to hold column headers and SGR data
   //         // We only bother displaying a 'Factor' column when we know that at least one sensor has a Factor (i.e., hide the column if no sensors do).
   //         // This is in order to be consistent with certificates printed by iNet Control, because it has this behavior, too.
   //         int numColumns = hasFactor ? NumCalSensorColumns : NumCalSensorColumns - 1;
   //         string[,] sensorContents = new string[ calEvent.GasResponses.Count + 1, numColumns ];

   //         int col = 0;
   //         sensorContents[0,col++] = PrintManagerResources.Column_SensorSN;
   //         sensorContents[0,col++] = PrintManagerResources.Column_SensorType;
   //         // We only bother displaying a 'Factor' column when we know that at least one sensor has a Factor (i.e., hide the column if no sensors do).
   //         // This is in order to be consistent with certificates printed by iNet Control, because it has this behavior, too.
   //         if ( hasFactor )
   //             sensorContents[0,col++] = PrintManagerResources.Column_Factor;
   //         sensorContents[0,col++] = PrintManagerResources.Column_GasType; // cal gas type
   //         sensorContents[0,col++] = PrintManagerResources.Column_SpanGas; // cal gas concentration
   //         sensorContents[0,col++] = PrintManagerResources.Column_SpanReserve;
   //         sensorContents[0,col++] = PrintManagerResources.Column_PassedFailed;
   //         sensorContents[0,col++] = PrintManagerResources.Column_AlarmLow;
   //         sensorContents[0,col++] = PrintManagerResources.Column_AlarmHigh;
   //         sensorContents[0,col++] = PrintManagerResources.Column_AlarmTWA;
   //         sensorContents[0,col++] = PrintManagerResources.Column_AlarmSTEL;

			//// create 2-dimension [#SGRs + 1, #Cylinder properties] array of strings to hold column headers and cylinder data
			//string[,] cylinderContents = new string[calEvent.GasResponses.Count + 1, NumCalCylinderColumns];
			//cylinderContents[0, 0] = PrintManagerResources.Column_SensorSN;
			//cylinderContents[0, 1] = PrintManagerResources.Column_SensorType;
			//cylinderContents[0, 2] = PrintManagerResources.Column_CalDateTime;
			//cylinderContents[0, 3] = PrintManagerResources.Column_CylinderID;
			//cylinderContents[0, 4] = PrintManagerResources.Column_CylinderExp;
			//cylinderContents[0, 5] = PrintManagerResources.Column_ZeroCylinderID; // SGF  19-Jan-2011  INS-1204
			//cylinderContents[0, 6] = PrintManagerResources.Column_ZeroCylinderExp; // SGF  19-Jan-2011  INS-1204

			//// OBTAIN CALIBRATION DATA FOR EACH SENSOR

			//// SGF  18-Jan-2011  INS-1301  (begin)
			//sortPrintCulture = printCulture;
			//List<SensorGasResponse> sensorCals = new List<SensorGasResponse>();
			//foreach ( SensorGasResponse scal in calEvent.GasResponses )
			//	sensorCals.Add( scal );
   //         sensorCals.Sort( CompareSensorGasResponses );
			//// SGF  18-Jan-2011  INS-1301  (end)

			//int sensorIdx = 1;
			//foreach ( SensorGasResponse scal in sensorCals )  // SGF  18-Jan-2011  INS-1301
			//{
			//	try
			//	{
			//		string fullSensorSN = scal.Uid;
			//		string[] ssnParts = fullSensorSN.Split( new char[] { '#' } );
			//		string baseSensorSN = ssnParts[0];
			//		string sensorCode = ssnParts[1];
			//		string sensorType = PrintManagerResources.ResourceManager.GetString( sensorCode, printCulture );
   //                 string factor = SensorCode.IsCombustible( sensorCode ) ? scal.GasDetected : string.Empty;
			//		// The GasConcentration property should never be null.
   //                 string gasCode = scal.GasConcentration.Type.Code;  // cal gas type (code)
   //                 double concentration = scal.GasConcentration.Concentration; // cal gas concentration

			//		double spanReading = scal.Reading != double.MinValue ? scal.Reading : 0.0;
			//		double spanReserve = spanReading / concentration;

			//		Sensor sensor = calEvent.DockedInstrument.GetInstalledComponentByUid( scal.Uid ).Component as Sensor;
					
			//		// Get Alarm High, Alarm Low, Alarm TWA, and Alarm STEL properties for the current sensor
			//		string alarmHigh = value_NA;
			//		string alarmLow = value_NA;
			//		string alarmTWA = value_NA;
			//		string alarmSTEL = value_NA;

			//		if ( sensor != null && sensor.Alarm != null )
			//		{
			//			alarmHigh = sensor.Alarm.High.ToString( FORMAT_DECIMAL, printCulture );
			//			alarmLow = sensor.Alarm.Low.ToString( FORMAT_DECIMAL, printCulture );
			//			if ( sensor.Alarm.TWA > 0 )
			//			{
			//				alarmTWA = sensor.Alarm.TWA.ToString( FORMAT_DECIMAL, printCulture );
			//			}
			//			if ( sensor.Alarm.STEL > 0 )
			//			{
			//				alarmSTEL = sensor.Alarm.STEL.ToString( FORMAT_DECIMAL, printCulture );
			//			}
			//		}

   //                 col = 0;
   //                 sensorContents[sensorIdx,col++] = baseSensorSN;
   //                 sensorContents[sensorIdx,col++] = sensorType;
   //                 // We only bother displaying a 'Factor' column when we know that at least one sensor has a Factor (i.e., hide the column if no sensors do)
   //                 // This is in order to be consistent with certificates printed by iNet Control, because it has this behavior, too.
   //                 if ( hasFactor )
   //                     sensorContents[sensorIdx,col++] = !String.IsNullOrEmpty( factor ) ? PrintManagerResources.ResourceManager.GetString( factor, printCulture ) : value_NA;
			//		// When the sensor gas response indicates that the sensor passed zeroing, 
			//		// it means that the sensor was NOT subjected to calibration.
   //                 sensorContents[sensorIdx,col++] = ( scal.Status != Status.ZeroPassed ) ? PrintManagerResources.ResourceManager.GetString( gasCode, printCulture ) : value_NA;
   //                 sensorContents[sensorIdx,col++] = ( scal.Status != Status.ZeroPassed ) ? concentration.ToString( FORMAT_DECIMAL, printCulture ) : value_NA;
			//		if ( scal.Status == Status.InstrumentAborted )
			//		{
			//			// If the instrument aborted calibration, do not print a span reserve value
   //                     sensorContents[sensorIdx,col++] = String.Empty;
			//		}
			//		else
			//		{
   //                     sensorContents[sensorIdx,col++] = ( scal.Status != Status.ZeroPassed ) ? spanReserve.ToString( FORMAT_PERCENT, printCulture ) : value_NA;
			//		}
			//		// SGF  18-Jan-2011  INS-1291
			//		if ( scal.Status != Status.ZeroPassed )
			//		{
			//			if ( scal.Passed )
			//			{
			//				if ( spanReserve <= MARGINAL_SPANRESERVE )
   //                             sensorContents[sensorIdx,col++] = PrintManagerResources.Value_Marginal;
			//				else
   //                             sensorContents[sensorIdx,col++] = PrintManagerResources.Value_Passed;
			//			}
			//			else
			//			{
			//				if ( scal.Status == Status.InstrumentAborted )
   //                             sensorContents[sensorIdx,col++] = PrintManagerResources.Value_Aborted;
			//				else
   //                             sensorContents[sensorIdx,col++] = PrintManagerResources.Value_Failed;
			//			}
			//		}
			//		else
			//		{
   //                     sensorContents[sensorIdx,col++] = PrintManagerResources.Value_Skipped;
			//		}

   //                 sensorContents[sensorIdx,col++] = alarmLow;
   //                 sensorContents[sensorIdx,col++] = alarmHigh;
   //                 sensorContents[sensorIdx,col++] = alarmTWA;
   //                 sensorContents[sensorIdx,col++] = alarmSTEL;

			//		cylinderContents[sensorIdx, 0] = baseSensorSN;
			//		cylinderContents[sensorIdx, 1] = sensorType;
			//		cylinderContents[sensorIdx, 2] = Configuration.DockingStation.TimeZoneInfo.ToLocalTime( scal.Time ).ToString( dateTimeFormat, printCulture );

			//		// Get CylinderID for the current sensor' cylinder
			//		string cylinderID = string.Empty;
			//		string zeroCylinderID = string.Empty; // SGF  19-Jan-2011  INS-1204
			//		string cylinderExp = string.Empty;
			//		string zeroCylinderExp = string.Empty;

			//		// The final cylinder used for a specific usage will be later in the list.  Therefore,
			//		// we are looping backwards through the list and only using the first Cal and Zero 
			//		// cylinders found.
			//		for ( int i = scal.UsedGasEndPoints.Count - 1; i >= 0; i-- )
			//		{
			//			UsedGasEndPoint used = scal.UsedGasEndPoints[i];
						
			//			if ( used.Usage == CylinderUsage.Calibration && cylinderID == string.Empty )
			//			{
			//				if ( used.Cylinder.IsFreshAir )
			//				{
			//					cylinderID = PrintManagerResources.Value_FreshAir;
			//					cylinderExp = value_NA;
			//				}
			//				else
			//				{
			//					cylinderID = used.Cylinder.FactoryId;
			//					cylinderExp = used.Cylinder.ExpirationDate.ToString( dateFormat, printCulture );
			//				}							
			//			}
			//			else if ( used.Usage == CylinderUsage.Zero && zeroCylinderID == string.Empty )
			//			{
			//				if ( used.Cylinder.IsFreshAir )
			//				{
			//					zeroCylinderID = PrintManagerResources.Value_FreshAir;
			//					zeroCylinderExp = value_NA;
			//				}
			//				else
			//				{
			//					zeroCylinderID = used.Cylinder.FactoryId;
			//					zeroCylinderExp = used.Cylinder.ExpirationDate.ToString( dateFormat, printCulture );
			//				}
			//			}
			//		}

			//		cylinderContents[sensorIdx, 3] = cylinderID;
			//		cylinderContents[sensorIdx, 4] = cylinderExp;
			//		cylinderContents[sensorIdx, 5] = zeroCylinderID; // SGF  19-Jan-2011  INS-1204
			//		cylinderContents[sensorIdx, 6] = zeroCylinderExp;					
			//	}
			//	catch ( Exception e )
			//	{
			//		throw e;
			//	}

			//	sensorIdx++;
			//}
			//Log.Debug( "generateCalCert: done processing sensors" );

			//prce.FontSize = BodyFontSize;
			//int sensorTableBottom = DrawDataTable( prce, sensorContents, true, topSensorSectionY, leftSGRSectionX, rightSGRSectionX, cellMargin );

			//int topCylinderSectionY = sensorTableBottom + SectionVerticalGap;

			//prce.FontSize = BodyFontSize;
			//int cylinderTableBottom = DrawDataTable( prce, cylinderContents, true, topCylinderSectionY, leftSGRSectionX, rightSGRSectionX, cellMargin );


			//// FOOTER SECTION
			//int footerTop = cylinderTableBottom + SectionVerticalGap;

			//// NOTES
			//prce.FontSize = FooterFontSize;
			//prce.DrawText( PrintManagerResources.Label_Notes, 0, footerTop );

			//// SIGNATURE LINES
			//// SGF  03-Jun-2011  INS-1756 -- Activating checks for PrintPerformedBy and PrintReceivedBy to draw signature lines
			//Log.Debug( string.Format( "generateCalCert: PrintPerformedBy = {0}", Configuration.DockingStation.PrintPerformedBy.ToString() ) );
			//Log.Debug( string.Format( "generateCalCert: PrintReceivedBy = {0}", Configuration.DockingStation.PrintReceivedBy.ToString() ) );

			//int performedByY = footerTop + 500;
			//prce.FontSize = FooterFontSize;
			//if ( Configuration.DockingStation.PrintPerformedBy == true )
			//{
			//	prce.DrawText( PrintManagerResources.Label_PerformedBy, 4000, performedByY );
			//	prce.DrawLine( 5500, performedByY + 250, 10000, performedByY + 250 );
			//}

			//int receivedByY = footerTop + 1000;
			//if ( Configuration.DockingStation.PrintReceivedBy == true )
			//{
			//	prce.DrawText( PrintManagerResources.Label_ReceivedBy, 4000, receivedByY );
			//	prce.DrawLine( 5500, receivedByY + 250, 10000, receivedByY + 250 );
			//}

   //         //INS-8223: Add barcode of instrument's serial number when bump/cal certificate is generated
   //         int instrumentSnY = footerTop + 1200;
   //         prce.DrawText(calEvent.DockedInstrument.SerialNumber, 30, instrumentSnY);
   //         prce.FontName = "3 of 9 Barcode";
   //         prce.FontSize = 20;
   //         prce.DrawText(String.Concat("*", calEvent.DockedInstrument.SerialNumber, "*"), 0, instrumentSnY + 250);

			//Log.Debug( "generateCalCert: leaving method" );
		}

		private void PrintBumpCert( InstrumentBumpTestEvent bumpEvent, CultureInfo printCulture )
		{
			//Log.Debug( "PrintBumpCert: entered method" );

			//using ( PrinterCE prce = CreatePrinterCE() )
			//{
			//	try
			//	{
			//		GenerateBumpCert( prce, bumpEvent, printCulture );
			//		prce.EndDoc(); // Print the page
			//	}
			//	catch ( PrinterCEException pcee )
			//	{
			//		Log.Error( "PrintBumpCert: PrinterCEException" );
			//		Log.Error( pcee.Message, pcee );
					
			//		// An unknown issue is resulting in a lost connection with the USB printer.
			//		// An error is not thrown when using PrinterCE.NetCF.dll earlier than version 1.2.7.2.
			//		if ( prce.GetLastError == PrinterCE_Base.ERROR.COMMUNICATION_LOST )
			//		{
			//			this.hasAborted = true;
			//		}
			//	}
			//	finally
			//	{
			//		// We have a 2nd check for the lost connection with the USB printer because
			//		// we have not had enough time to test the 1.2.7.2 dll to know that an exception will 
			//		// always be thrown with the expected last error.
			//		if ( this.hasAborted == false )
			//		{
			//			// Is there now an LPT2:?
			//			this.hasAborted = Controller.IsUsbPrinterNotOnLpt1();
			//		}
			//	}
			//} // Dispose will take care of the Shutdown actions

			//Log.Debug( "PrintBumpCert: leaving method" );
		}

		private void GenerateBumpCert( PrinterCE prce, InstrumentBumpTestEvent bumpEvent, CultureInfo printCulture )
		{
			//Log.Debug( "generateBumpCert: entered method" );

			//string value_NA = PrintManagerResources.Value_NA;
			//bool isPrintCultureEngUS = printCulture.Name == "en-US";

			//// special date formatting when the culture is en-US
			//string dateFormat = FORMAT_DATE;
			//string dateTimeFormat = FORMAT_DATETIME;
			//if ( isPrintCultureEngUS )
			//{
			//	dateFormat = FORMAT_DATE_ENGUS;
			//	dateTimeFormat = FORMAT_DATETIME_ENGUS;
			//}

   //         // Is there at least one sensor that has a correlation factor?
   //         bool hasFactor = bumpEvent.GasResponses.Find( sgr => SensorCode.IsCombustible( sgr.SensorCode ) && !String.IsNullOrEmpty( sgr.GasDetected ) ) != null;

			//// TITLE
			//prce.FontSize = TitleFontSize;
			//prce.FontBold = true;
			//prce.JustifyHoriz = PrinterCE_Base.JUSTIFY_HORIZ.CENTER;
			//prce.DrawText( PrintManagerResources.Title_BumpCertificate, PageCenter, TitleYCoord );

			//prce.JustifyHoriz = PrinterCE_Base.JUSTIFY_HORIZ.LEFT;

			//// HEADER SECTION

			//// OBTAIN HEADER INFORMATION

			//// Bump Test Date. It will always be specified.  It's also in UTC, so
			//// we need to convert it to the time zone's local time.
			//DateTime localBumpTime = Configuration.DockingStation.TimeZoneInfo.ToLocalTime( bumpEvent.Time );
			//string bumpTime = localBumpTime.ToString( dateFormat, printCulture );
			
			//// Part Number
			//string partNumber = bumpEvent.DockedInstrument.PartNumber;
			//if ( String.IsNullOrEmpty( partNumber ) )
			//	partNumber = value_NA;

			//// Job Number
			//string jobNumber = bumpEvent.DockedInstrument.JobNumber;
			//if ( String.IsNullOrEmpty( jobNumber ) )
			//	jobNumber = value_NA;

			//// Setup Date
			//string setupDate = bumpEvent.DockedInstrument.SetupDate.ToString( dateFormat, printCulture );
			//if ( String.IsNullOrEmpty( setupDate ) )
			//	setupDate = value_NA;

			//// Setup Technician
			//string setupTech = bumpEvent.DockedInstrument.SetupTech;
			//if ( String.IsNullOrEmpty( setupTech ) )
			//	setupTech = value_NA;

			//// Battery
			//string battery = String.Empty;
			//string batteryType = String.Empty;
			//foreach ( InstalledComponent ic in bumpEvent.DockedInstrument.InstalledComponents )
			//{
			//	if ( ic.Component is Battery )
			//	{
			//		battery = ic.Component.Type.Code;
			//		break;
			//	}
			//}
			//if ( String.IsNullOrEmpty( battery ) )
			//	batteryType = value_NA;
			//else
			//	batteryType = PrintManagerResources.ResourceManager.GetString( battery, printCulture );

			//// Bump Threshold
			//string bumpThreshold = bumpEvent.DockedInstrument.BumpThreshold.ToString( FORMAT_INTEGER, printCulture );
			//if ( String.IsNullOrEmpty( bumpThreshold ) )
			//	bumpThreshold = value_NA;

			//// Bump Timeout
			//string bumpTimeout = bumpEvent.DockedInstrument.BumpTimeout.ToString( FORMAT_INTEGER, printCulture );
			//if ( String.IsNullOrEmpty( bumpTimeout ) )
			//	bumpTimeout = value_NA;

			//// Pump Accessory
			//string pumpAccessoryValue = value_NA;
			//switch ( bumpEvent.DockedInstrument.AccessoryPump )
			//{
			//	case AccessoryPumpSetting.Installed:
			//		pumpAccessoryValue = PrintManagerResources.Value_Installed;
			//		break;
			//	case AccessoryPumpSetting.Uninstalled:
			//		pumpAccessoryValue = PrintManagerResources.Value_Uninstalled;
			//		break;
			//}

			//// PRINT HEADER SECTION INFORMATION
			//string[] PropertyLabels = new string[]
   //             {
   //                 PrintManagerResources.Label_InstrumentSN,
   //                 PrintManagerResources.Label_BumpDate,
   //                 PrintManagerResources.Label_PartNumber,
   //                 PrintManagerResources.Label_JobNumber,
   //                 PrintManagerResources.Label_SetupDate,
   //                 PrintManagerResources.Label_SetupTechnician,
   //                 PrintManagerResources.Label_CreatedBy,
   //                 PrintManagerResources.Label_Battery,
   //                 PrintManagerResources.Label_BumpThreshold,
   //                 PrintManagerResources.Label_BumpTimeout,
   //                 PrintManagerResources.Label_PumpAccessory
   //             };

			//string[] PropertyValues = new string[]
   //             {
   //                 bumpEvent.DockedInstrument.SerialNumber,
   //                 bumpTime,
   //                 partNumber,
   //                 jobNumber,
   //                 setupDate,
   //                 setupTech,
   //                 CREATEDBY,
   //                 batteryType,
   //                 bumpThreshold,
   //                 bumpTimeout,
   //                 pumpAccessoryValue
   //             };

			//int propertySectionBottomY = DrawPropertySection( prce, HeaderYCoord, PropertyLabels, PropertyValues );


			//// BODY SECTION

			//// set coordinates of sensor grid
			//int topSensorSectionY = propertySectionBottomY + SectionVerticalGap;
			//int leftSGRSectionX = 0; int rightSGRSectionX = 10000;
			//int cellMargin = CellMargin;

			//// create 2-dimension [#SGRs + 1, #SGR properties] array of strings to hold column headers and SGR data
   //         // We only bother displaying a 'Factor' column when we know that at least one sensor has a Factor (i.e., hide the column if no sensors do).
   //         // This is in order to be consistent with certificates printed by iNet Control, because it has this behavior, too.
   //         int numColumns = hasFactor ? NumBumpSensorColumns : NumBumpSensorColumns - 1;
   //         string[,] sensorContents = new string[ bumpEvent.GasResponses.Count + 1, numColumns ];

   //         int col = 0;
   //         sensorContents[0,col++] = PrintManagerResources.Column_SensorSN;
   //         sensorContents[0,col++] = PrintManagerResources.Column_SensorType;
   //         // We only bother displaying a 'Factor' column when we know that at least one sensor has a Factor (i.e., hide the column if no sensors do).
   //         // This is in order to be consistent with certificates printed by iNet Control, because it has this behavior, too.
   //         if ( hasFactor )
   //             sensorContents[ 0,col++] = PrintManagerResources.Column_Factor;
   //         sensorContents[0,col++] = PrintManagerResources.Column_GasType; // cal gas type (code)
   //         sensorContents[0,col++] = PrintManagerResources.Column_SpanGas; // cal gas concentration
   //         sensorContents[0,col++] = PrintManagerResources.Column_SensorReading;
   //         sensorContents[0,col++] = PrintManagerResources.Column_PassedFailed;
   //         sensorContents[0,col++] = PrintManagerResources.Column_AlarmLow;
   //         sensorContents[0,col++] = PrintManagerResources.Column_AlarmHigh;
   //         sensorContents[0,col++] = PrintManagerResources.Column_AlarmTWA;
   //         sensorContents[0,col++] = PrintManagerResources.Column_AlarmSTEL;

			//// create 2-dimension [#SGRs + 1, #Cylinder properties] array of strings to hold column headers and cylinder data
			//string[,] cylinderContents = new string[bumpEvent.GasResponses.Count + 1, NumBumpCylinderColumns];
			//cylinderContents[0, 0] = PrintManagerResources.Column_SensorSN;
			//cylinderContents[0, 1] = PrintManagerResources.Column_SensorType;
			//cylinderContents[0, 2] = PrintManagerResources.Column_BumpDateTime;
			//cylinderContents[0, 3] = PrintManagerResources.Column_CylinderID;
			//cylinderContents[0, 4] = PrintManagerResources.Column_CylinderExp;


			//// OBTAIN BUMP TEST DATA FOR EACH SENSOR

			//// INS-1301  (begin)
			//sortPrintCulture = printCulture;
			//List<SensorGasResponse> sensorBumps = new List<SensorGasResponse>();
			//foreach ( SensorGasResponse sbump in bumpEvent.GasResponses )
			//	sensorBumps.Add( sbump );
			//sensorBumps.Sort( CompareSensorGasResponses );
			//// INS-1301  (end)

			//int sensorIdx = 1;
			////foreach (SENSOR_BUMP_TEST sbump in bump.sensorBumpTest)
			//foreach ( SensorGasResponse sbump in sensorBumps )  // SGF  18-Jan-2011  INS-1301
			//{
			//	try
			//	{
			//		bool wasSkipped = sbump.Status == Status.Skipped;

			//		string fullSensorSN = sbump.Uid;
			//		string[] ssnParts = fullSensorSN.Split( new char[] { '#' } );
			//		string baseSensorSN = ssnParts[0];
			//		string sensorCode = ssnParts[1];
			//		string sensorType = PrintManagerResources.ResourceManager.GetString( sensorCode, printCulture );
   //                 string factor = SensorCode.IsCombustible( sensorCode ) ? sbump.GasDetected : string.Empty;
   //                 string gasCode = sbump.GasConcentration.Type.Code; // cal gas type (code)
   //                 double concentration = sbump.GasConcentration.Concentration; // cal gas concentration
			//		double sensorReading = sbump.Reading;

			//		Sensor sensor = bumpEvent.DockedInstrument.GetInstalledComponentByUid( sbump.Uid ).Component as Sensor;

			//		// Get Alarm High, Alarm Low, Alarm TWA, and Alarm STEL properties for the current sensor
			//		string alarmHigh = value_NA;;
			//		string alarmLow = value_NA;
			//		string alarmTWA = value_NA;
			//		string alarmSTEL = value_NA;

			//		if ( sensor != null && sensor.Alarm != null )
			//		{
			//			alarmHigh = sensor.Alarm.High.ToString( FORMAT_DECIMAL, printCulture );
			//			alarmLow = sensor.Alarm.Low.ToString( FORMAT_DECIMAL, printCulture );
			//			if ( sensor.Alarm.TWA > 0 )
			//			{
			//				alarmTWA = sensor.Alarm.TWA.ToString( FORMAT_DECIMAL, printCulture );
			//			}
			//			if ( sensor.Alarm.STEL > 0 )
			//			{
			//				alarmSTEL = sensor.Alarm.STEL.ToString( FORMAT_DECIMAL, printCulture );
			//			}
			//		}

   //                 col = 0;
			//		sensorContents[sensorIdx,col++] = baseSensorSN;
   //                 sensorContents[sensorIdx,col++] = sensorType;
   //                 // We only bother displaying a 'Factor' column when we know that at least one sensor has a Factor (i.e., hide the column if no sensors do)
   //                 // This is in order to be consistent with certificates printed by iNet Control, because it has this behavior, too.
   //                 if ( hasFactor )
   //                     sensorContents[sensorIdx,col++] = !String.IsNullOrEmpty( factor ) ? PrintManagerResources.ResourceManager.GetString( factor, printCulture ) : value_NA;
   //                 sensorContents[sensorIdx,col++] = ( wasSkipped == false ) ? PrintManagerResources.ResourceManager.GetString( gasCode, printCulture ) : value_NA;
   //                 sensorContents[sensorIdx,col++] = ( wasSkipped == false ) ? concentration.ToString( FORMAT_DECIMAL, printCulture ) : value_NA;
   //                 sensorContents[sensorIdx,col++] = ( wasSkipped == false ) ? sensorReading.ToString( FORMAT_DECIMAL, printCulture ) : value_NA;

			//		// SGF  08-Jun-2011  INS-1734
			//		//sensorContents[sensorIdx, 5] = (bool)sbump.pass ? PrintManagerResources.Value_Passed : PrintManagerResources.Value_Failed;
			//		string bumpStatusResourceName = "Value_" + sbump.Status.ToString();
   //                 sensorContents[sensorIdx,col++] = PrintManagerResources.ResourceManager.GetString( bumpStatusResourceName, printCulture );

   //                 sensorContents[sensorIdx,col++] = alarmLow;
   //                 sensorContents[sensorIdx,col++] = alarmHigh;
   //                 sensorContents[sensorIdx,col++] = alarmTWA;
   //                 sensorContents[sensorIdx,col++] = alarmSTEL;

			//		cylinderContents[sensorIdx, 0] = baseSensorSN;
			//		cylinderContents[sensorIdx, 1] = sensorType;
			//		cylinderContents[sensorIdx, 2] = Configuration.DockingStation.TimeZoneInfo.ToLocalTime( sbump.Time ).ToString( dateTimeFormat, printCulture );

			//		// Get CylinderID for the current sensor' cylinder
			//		string cylinderID = string.Empty;
			//		string cylinderExp = string.Empty;

			//		// The final cylinder used for a specific usage will be later in the list.  
			//		// Therefore, we are looping backwards through the list and only using the  
			//		// first Bump cylinder found.
			//		for ( int i = sbump.UsedGasEndPoints.Count - 1; i >= 0; i-- )
			//		{
			//			UsedGasEndPoint cylUsed = sbump.UsedGasEndPoints[i];

			//			if ( cylUsed.Usage == CylinderUsage.Bump )
			//			{
			//				if ( cylUsed.Cylinder.IsFreshAir )
			//				{
			//					cylinderID = PrintManagerResources.Value_FreshAir;
			//					cylinderExp = value_NA;
			//				}
			//				else
			//				{
			//					cylinderID = cylUsed.Cylinder.FactoryId;
			//					cylinderExp = cylUsed.Cylinder.ExpirationDate.ToString( dateFormat, printCulture );
			//				}

			//				break;
			//			}
			//		}
					
			//		cylinderContents[sensorIdx, 3] = cylinderID;
			//		cylinderContents[sensorIdx, 4] = cylinderExp;
			//	}
			//	catch ( Exception e )
			//	{
			//		throw e;
			//	}

			//	sensorIdx++;
			//}
			//Log.Debug( "generateBumpCert: done processing sensors" );

			//prce.FontSize = 8;
			//int sensorTableBottom = DrawDataTable( prce, sensorContents, true, topSensorSectionY, leftSGRSectionX, rightSGRSectionX, cellMargin );

			//int topCylinderSectionY = sensorTableBottom + SectionVerticalGap;

			//prce.FontSize = 8;
			//int cylinderTableBottom = DrawDataTable( prce, cylinderContents, true, topCylinderSectionY, leftSGRSectionX, rightSGRSectionX, cellMargin );


			//// FOOTER SECTION
			//int footerTop = cylinderTableBottom + SectionVerticalGap;

			//// NOTES
			//prce.FontSize = FooterFontSize;
			//prce.DrawText( PrintManagerResources.Label_Notes, 0, footerTop );

			//// SIGNATURE LINES
			//// SGF  03-Jun-2011  INS-1756 -- Activating checks for PrintPerformedBy and PrintReceivedBy to draw signature lines
			//Log.Debug( string.Format( "generateBumpCert: PrintPerformedBy = {0}", Configuration.DockingStation.PrintPerformedBy.ToString() ) );
			//Log.Debug( string.Format( "generateBumpCert: PrintReceivedBy = {0}", Configuration.DockingStation.PrintReceivedBy.ToString() ) );

			//int performedByY = footerTop + 500;
			//prce.FontSize = FooterFontSize;
			//if ( Configuration.DockingStation.PrintPerformedBy == true )
			//{
			//	prce.DrawText( PrintManagerResources.Label_PerformedBy, 4000, performedByY );
			//	prce.DrawLine( 5500, performedByY + 250, 10000, performedByY + 250 );
			//}

			//int receivedByY = footerTop + 1000;
			//if ( Configuration.DockingStation.PrintReceivedBy == true )
			//{
			//	prce.DrawText( PrintManagerResources.Label_ReceivedBy, 4000, receivedByY );
			//	prce.DrawLine( 5500, receivedByY + 250, 10000, receivedByY + 250 );
			//}

   //         //INS-8223: Add barcode of instrument's serial number when bump/cal certificate is generated
   //         int instrumentSnY = footerTop + 1200;
   //         prce.DrawText(bumpEvent.DockedInstrument.SerialNumber, 30, instrumentSnY);
   //         prce.FontName = "3 of 9 Barcode";
   //         prce.FontSize = 20;
   //         prce.DrawText(String.Concat("*", bumpEvent.DockedInstrument.SerialNumber, "*"), 0, instrumentSnY + 250);

			//Log.Debug( "generateBumpCert: leaving method" );
		}

        private int DrawPropertySection(PrinterCE prce, int topY, string[] propLabels, string[] propValues)
        {
            return 0;
            //Log.Debug("drawPropertySection: entered method");

            //prce.FontSize = HeaderFontSize;

            //int[] PropertyLabelWidths = new int[propLabels.Length];
            //int[] PropertyValueWidths = new int[propValues.Length];

            //int numElements = propLabels.Length;

            //int maxLabelWidth = 0;
            //for (int i = 0; i < numElements; i++)
            //{
            //    PropertyLabelWidths[i] = (int)prce.GetStringWidth(propLabels[i]);
            //    if (PropertyLabelWidths[i] > maxLabelWidth)
            //        maxLabelWidth = PropertyLabelWidths[i];
            //}

            //int maxValueWidth = 0;
            //for (int i = 0; i < numElements; i++)
            //{
            //    PropertyValueWidths[i] = (int)prce.GetStringWidth(propValues[i]);
            //    if (PropertyValueWidths[i] > maxValueWidth)
            //        maxValueWidth = PropertyValueWidths[i];
            //}

            //int sectionWidth = maxLabelWidth + PropertyGapWidth + maxValueWidth;
            //int propertySectionX1 = (PageWidth - sectionWidth) / 2;
            //int propertySectionX2 = propertySectionX1 + maxLabelWidth + PropertyGapWidth;

            //int propertyStringHeight = (int)prce.GetStringHeight;

            //int[] PropertyY = new int[propLabels.Length];
            //PropertyY[0] = topY + SectionVerticalGap;
            //for (int i = 1; i < numElements; i++)
            //{
            //    PropertyY[i] = PropertyY[i - 1] + (int)(1.5 * propertyStringHeight);
            //}

            //for (int i = 0; i < numElements; i++)
            //{
            //    PrintInstrumentProperty(prce, propertySectionX1, propertySectionX2, PropertyY[i], propLabels[i], propValues[i]);
            //}
            //Log.Debug("drawPropertySection: leaving method");

            //return PropertyY[numElements - 1] + (int)(1.5 * propertyStringHeight);
        }

        private int DrawDataTable(PrinterCE prce, string[,] tableContents, bool useColumnHeaders, int topY, int leftX, int rightX, int cellMargin)
        {
            return 0;
            //Log.Debug("drawDataTable: ENTERED METHOD");

            ////calculate dimensions of tableContents
            //int numRows = tableContents.GetLength(0);
            //int numColumns = tableContents.GetLength(1);

            ////create array of int MAXWIDTHS on number of columns in tableContents
            //int[] maxColumnWidths = new int[numColumns];

            ////get text height
            //int textHeight = (int)prce.GetStringHeight;

            ////calculate height of row in drawn table (text height * 2.5, unless we allow for more than 1 string split per cell)
            //int rowHeight = (int)(textHeight * 2.5);

            ////calculate potential width of drawn table
            //int potentialTableWidth = rightX - leftX;

            ////calculate space consumed by cell margins
            //int totalCellMargin = cellMargin * 2 * numColumns;

            ////calculate space required to print values (potential width of table - total cell margins)
            //int potentialPrintWidth = potentialTableWidth - totalCellMargin;

            ////initialize pass value (1)
            //int pass = 1;
            //bool needAnotherPass = false;
            //bool[] passApplied = new bool[numColumns];
            //bool passStarted = false;
            //bool passComplete = true;

            //// TABLE WIDTH REDUCTION LOOP
            //do
            //{
            //    // initialize values in MAXWIDTHS
            //    for (int c = 0; c < numColumns; c++)
            //        maxColumnWidths[c] = 0;

            //    if (passComplete == true)
            //    {
            //        for (int c = 0; c < numColumns; c++)
            //            passApplied[c] = false;
            //        passComplete = false;
            //    }

            //    // loop through rows in tableContents
            //    for (int r = 0; r < numRows; r++)
            //    {
            //        if (r == 0 && useColumnHeaders)
            //            prce.FontBold = true;
            //        else
            //            prce.FontBold = false;

            //        // loop through columns in tableContents
            //        for (int c = 0; c < numColumns; c++)
            //        {
            //            // get width of current string
            //            int stringWidth = GetPrintedWidth(prce, tableContents[r, c]);

            //            // compare current string's width to current column maximum width
            //            if (stringWidth > maxColumnWidths[c])
            //            {
            //                // set the current column maximum width to current string's width
            //                maxColumnWidths[c] = stringWidth;
            //            }
            //        }
            //    }

            //    // sum up maximum column width values
            //    int maxTableWidth = 0;
            //    for (int c = 0; c < numColumns; c++)
            //        maxTableWidth += maxColumnWidths[c];

            //    // if the maximum table width is greater than the potential printing width, 
            //    // we need to find strings to split or otherwise reduce.
            //    if (maxTableWidth > potentialPrintWidth)
            //    {
            //        // indicate we will find a way to reduce column widths, and then we will 
            //        // re-check the table
            //        needAnotherPass = true;

            //        //  using pass value, determine approach to adjust table contents to reduce
            //        switch (pass)
            //        {
            //            case 1:
            //                // Pass 1: Split column headers
            //                for (int c = 0; c < numColumns; c++)
            //                {
            //                    string newValue = SplitStringValue(tableContents[0, c]);
            //                    if (newValue != null)
            //                        tableContents[0, c] = newValue;
            //                }

            //                pass++;
            //                break;
            //            case 2:
            //                // Pass 2: Determine widest column; split values in widest column
            //                if (!passStarted)
            //                {
            //                    for (int c = 0; c < numColumns; c++)
            //                    {
            //                        bool splitFound = false;
            //                        for (int r = 1; r < numRows && !splitFound; r++)
            //                        {
            //                            if (CanSplitValue(tableContents[r, c]) == true)
            //                                splitFound = true;
            //                        }
            //                        if (!splitFound)
            //                            passApplied[c] = true;
            //                    }
            //                    passStarted = true;
            //                }

            //                int widestColumnWidth = -1;
            //                int widestColumnIndex = -1;
            //                for (int c = 0; c < numColumns; c++)
            //                {
            //                    if (passApplied[c] == false)
            //                    {
            //                        if (maxColumnWidths[c] > widestColumnWidth)
            //                        {
            //                            widestColumnWidth = maxColumnWidths[c];
            //                            widestColumnIndex = c;
            //                        }
            //                    }
            //                }
            //                if (widestColumnIndex > -1)
            //                {
            //                    int columnHeaderWidth = GetPrintedWidth(prce, tableContents[0, widestColumnIndex]);
            //                    for (int r = 1; r < numRows; r++)
            //                    {
            //                        int valueWidth = GetPrintedWidth(prce, tableContents[r, widestColumnIndex]);
            //                        if (valueWidth > columnHeaderWidth)
            //                        {
            //                            string newValue = SplitStringValue(tableContents[r, widestColumnIndex]);
            //                            if (newValue != null)
            //                                tableContents[r, widestColumnIndex] = newValue;
            //                        }
            //                    }
            //                    passApplied[widestColumnIndex] = true;
            //                }
            //                else
            //                {
            //                    passComplete = true;
            //                    passStarted = false;
            //                    pass++;
            //                }
            //                break;
            //            default:
            //                needAnotherPass = false;
            //                break;
            //        }
            //    }
            //    else
            //    {
            //        needAnotherPass = false;
            //    }

            //} while (needAnotherPass == true);

            //// create array of int TABSTOPS with dimension (number of columns + 1)
            //int[] tabStops = new int[numColumns + 1];

            //tabStops[0] = leftX;
            //for (int c = 1; c <= numColumns; c++)
            //{
            //    tabStops[c] = tabStops[c - 1] + maxColumnWidths[c - 1] + 2 * cellMargin;
            //}

            //// draw table based on 
            ////     tableContents
            ////     useColumnHeaders
            ////     topY
            ////     numRows
            ////     rowHeight
            ////     tabStops
            //prce.FontBold = false;
            //int tableHeight = numRows * rowHeight;
            //int bottomY = topY + tableHeight;
            //prce.DrawRect(tabStops[0], topY, tabStops[numColumns], bottomY);

            //prce.FontBold = false;

            //// Draw column dividers
            //for (int c = 1; c < numColumns; c++)
            //    prce.DrawLine(tabStops[c], topY, tabStops[c], bottomY);

            //int curY = topY;

            //if (useColumnHeaders)
            //    prce.FontBold = true;

            //// Draw column headers ( row zero )
            //for (int c = 0; c < numColumns; c++)
            //{
            //    int textX = tabStops[c] + cellMargin;
            //    int textY = curY + (int)(0.25 * textHeight);
            //    prce.DrawText(tableContents[0, c], textX, textY);
            //}

            //// Draw rows of contents
            //prce.FontBold = false;
            //for (int r = 1; r < numRows; r++)
            //{
            //    curY += rowHeight;
            //    prce.DrawLine(tabStops[0], curY, tabStops[numColumns], curY);

            //    for (int c = 0; c < numColumns; c++)
            //    {
            //        int textX = tabStops[c] + cellMargin;
            //        int textY = curY + (int)(0.25 * textHeight);
            //        prce.DrawText(tableContents[r, c], textX, textY);
            //    }
            //}

            //Log.Debug("drawDataTable: about to LEAVE METHOD, with bottomY = " + bottomY);
            //return bottomY;
        }

        private int GetPrintedWidth(PrinterCE prce, string stringToPrint)
        {
            return 0;
            //int strWidth = -1;

            //int splitIndex = stringToPrint.IndexOf( Environment.NewLine );
            //if (splitIndex < 0)
            //{
            //    strWidth = (int)prce.GetStringWidth(stringToPrint);
            //    return strWidth;
            //}

            //string left = stringToPrint.Substring(0, splitIndex);
            //string right = stringToPrint.Substring( splitIndex + Environment.NewLine.Length );

            //int strWidthLeft = (int)prce.GetStringWidth(left);
            //int strWidthRight = (int)prce.GetStringWidth(right);
            //strWidth = strWidthLeft >= strWidthRight ? strWidthLeft : strWidthRight;

            //return strWidth;
        }

        private bool CanSplitValue(string currentStringValue)
        {
            char[] anyOf = SplitCandidateCharacters.ToCharArray();
            int splitIndex = currentStringValue.IndexOfAny(anyOf);
            return (splitIndex > 0);
        }

        private string SplitStringValue(string currentStringValue)
        {
            int halfwayPoint = (int)(currentStringValue.Length / 2);

            char[] anyOf = SplitCandidateCharacters.ToCharArray();

            int lastInFirstHalf = currentStringValue.LastIndexOfAny(anyOf, halfwayPoint);
            int firstInSecondHalf = currentStringValue.IndexOfAny(anyOf, halfwayPoint);
            if (lastInFirstHalf < 0 && firstInSecondHalf < 0)
                return null;

            int splitPoint = -1;
            if (lastInFirstHalf >= 0 && firstInSecondHalf < 0)
                splitPoint = lastInFirstHalf;
            else if (lastInFirstHalf < 0 && firstInSecondHalf >= 0)
                splitPoint = firstInSecondHalf;
            else // split characters found in both halves
            {
                if (halfwayPoint - lastInFirstHalf < firstInSecondHalf - halfwayPoint)
                    splitPoint = lastInFirstHalf;
                else
                    splitPoint = firstInSecondHalf;
            }

            //split the string AFTER the split character
            splitPoint++;

            string firstPart = currentStringValue.Substring(0, splitPoint);
            string lastPart = currentStringValue.Substring(splitPoint);
            string finalString = firstPart + Environment.NewLine + lastPart;
            return finalString;
        }

        private void PrintInstrumentProperty(PrinterCE prce, int labelX, int valueX, int y, string label, string propValue)
        {
            prce.FontBold = true;
            prce.DrawText(label, labelX, y);
            prce.FontBold = false;
            prce.DrawText(propValue, valueX, y);
        }

        // SGF  18-Jan-2011  INS-1301
        private string GetSensorType(string sensorSerialNumber, CultureInfo printCulture)
        {
            string[] ssnParts = sensorSerialNumber.Split(new char[] { '#' });
            if (ssnParts.Length < 2)
                return string.Empty;

            //string baseSensorSN = ssnParts[0];
            string sensorCode = ssnParts[1];
            string sensorType = PrintManagerResources.ResourceManager.GetString(sensorCode, printCulture);
            return sensorType;
        }

		// INS-1301
		private int CompareSensorGasResponses( SensorGasResponse sensor1, SensorGasResponse sensor2 )
		{
			string sensorType1 = GetSensorType( sensor1.Uid, sortPrintCulture );
			string sensorType2 = GetSensorType( sensor2.Uid, sortPrintCulture );
			return sensorType1.CompareTo( sensorType2 );
		}

		/// <summary>
		/// Attempt to reprint the certificate.
		/// </summary>
		/// <param name="gasEvent">The gas event results to be printed on a certificate.</param>
		private static void Reprint( InstrumentGasResponseEvent gasEvent )
		{
			//Master.Instance.ConsoleService.UpdateState( ConsoleState.PrinterError, ConsoleServiceResources.PRINTERERROR_USBCONNECTION );
			//string title = "PRINTER ERROR:";
			//int maxAttempts = 2;

			//// only attempt to reprint twice
			//for ( int i = 0; i < maxAttempts; i++ )
			//{
			//	Log.Debug( string.Format( "{0} This will be {1} of {2} possible reprint attempts.", title, i + 1, maxAttempts ) );

			//	// disconnect USB printer
			//	bool hasLptPort = true;

			//	Log.Debug( string.Format( "{0} Waiting indefinitely for USB printer to be disconnected.", title ) );
			//	while ( true )
			//	{
			//		hasLptPort = Controller.IsUsbPrinterOnAnyLptPort();

			//		// exit loop if no LPT ports found
			//		if ( !hasLptPort )
			//		{
			//			Log.Debug( string.Format( "{0} USB printer has been disconnected.", title ) );
			//			break;
			//		}

			//		Thread.Sleep( 1000 );
			//	}

			//	// connect USB printer
			//	bool hasOnlyLpt1Port = false;
			//	DateTime startTime = DateTime.UtcNow;
				
			//	Log.Debug( string.Format( "{0} Waiting for 3 minutes for USB printer to be connected.", title ) );
			//	while ( true )
			//	{
			//		hasOnlyLpt1Port = Controller.IsUsbPrinterOnlyOnLpt1();

			//		if ( hasOnlyLpt1Port )
			//		{
			//			Log.Debug( string.Format( "{0} USB printer has been connected.", title ) );
			//			break;
			//		}

			//		if ( ( DateTime.UtcNow - startTime ).Minutes >= 3 )
			//		{
			//			Log.Debug( string.Format( "{0} The USB printer has not been detected after 3 minutes.", title ) );
			//			break;
			//		}

			//		Thread.Sleep( 1000 );
			//	}

			//	// give up if the USB printer has not been connected after 3 minutes
			//	if (!hasOnlyLpt1Port)
			//	{
			//		Log.Debug( string.Format( "{0} The certificate will not be reprinted.", title ) );
			//		break;
			//	}

			//	// Call PrintCertificate() instead of Print() so Reprint() is not called multiple times.
			//	PrintManager printManager = new PrintManager();
			//	printManager.PrintCertificate( (InstrumentGasResponseEvent)gasEvent );

			//	if ( !printManager.hasAborted )
			//	{
			//		Log.Debug( string.Format( "{0} No issues were detected while reprinting the certificate.", title ) );
			//		break;
			//	}
			//}
		}
		
		#endregion
	}
}

