using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services.Resources;
using ISC.Instrument.TypeDefinition;
using ISC.WinCE.Logger;


/* Standard Date and Time Format Strings
 * =====================================
 * The "d" standard format specifier represents a custom date and time format string 
 * that is defined by a specific culture's DateTimeFormatInfo.ShortDatePattern property.
 * 6/15/2009 1:45:30 PM -> 6/15/2009 (en-US)
 * 6/15/2009 1:45:30 PM -> 15/06/2009 (fr-FR)
 * 
 * The "G" standard format specifier represents a combination of the short date ("d") 
 * and long time ("T") patterns, separated by a space.
 * 6/15/2009 1:45:30 PM -> 6/15/2009 1:45:30 PM (en-US)
 * 6/15/2009 1:45:30 PM -> 15/06/2009 13:45:30 (es-ES)
 * 
 * Standard Numeric Format Strings
 * ===============================
 * The fixed-point ("F) format specifier converts a number to a string of the form 
 * "-ddd.ddd…" where each "d" indicates a digit (0-9). The string starts with a minus 
 * sign if the number is negative.  The precision specifier indicates the desired 
 * number of decimal places. If the precision specifier is omitted, the current 
 * NumberFormatInfo.NumberDecimalDigits property supplies the numeric precision.
 * 1234.567 ("F", en-US) -> 1234.57
 * 1234.567 ("F", de-DE) -> 1234,57
 * 1234 ("F1", en-US) -> 1234.0
 * 1234 ("F1", de-DE) -> 1234,0
 * 
 * The percent ("P") format specifier multiplies a number by 100 and converts it to a 
 * string that represents a percentage. The precision specifier indicates the desired 
 * number of decimal places. If the precision specifier is omitted, the default 
 * numeric precision supplied by the current PercentDecimalDigits property is used.
 * 1 ("P", en-US) -> 100.00 %
 * 1 ("P", fr-FR) -> 100,00 %
 * -0.39678 ("P1", en-US) -> -39.7 %
 * -0.39678 ("P1", fr-FR) -> -39,7 %
 */

namespace ISC.iNet.DS.Services
{
	/// <summary>
	/// This class writes events to csv files with the expectation that they will be
	/// opened in Excel on a PC with a matching culture setting.
	/// </summary>
	internal class CsvFileManager
	{
		#region Fields

		private string _fileName;
		private string _filePath;
		private List<string> _fieldNames;
		private int _maxSensorCapacity;
		private string _logLabel = "FileManager: ";
		private const string CREATED_BY = "DSX";
		private const double MARGINAL_SPANRESERVE = .70;
		private char[] _trimChars = new char[] { ':', ' ' };
		private bool _wasEventSaved;
		private const int CSV_BUFFER_SIZE = 300;
		private StringBuilder _sb = new StringBuilder( CSV_BUFFER_SIZE );
		private string _valueNA; // used only for datalog

		// culture variables
		private CultureInfo _cultureInfo;
		private string _listSeparator;

		// format strings
		private const string FORMAT_DATE = "d";		// "6/15/2009"
		private const string FORMAT_DATETIME = "G";	// "6/15/2009 1:45:30 PM"
		private const string FORMAT_DECIMAL = "F2";	// 1234.57
		private const string FORMAT_INTEGER = "F0"; // 1234
		private const string FORMAT_PERCENT = "P2"; // 100.00 %

        public object FileHelper { get; private set; }

        #endregion

        #region Constructors

        internal CsvFileManager()
		{
			
		}

		#endregion

		#region Methods

		/// <summary>
		/// Checks if the instrument has been undocked.  If it has, a message is appended 
		/// to the end of the csv file and then an exception will be thrown.
		/// </summary>
		/// <param name="sw">a streamwriter that is ready to write to the csv file</param>
		private void CheckForUndockedInstrument( StreamWriter sw )
		{
			if ( !Controller.IsDocked() )
			{
				Log.Warning( string.Format( "{0}Instrument was undocked!", _logLabel ) );

				if ( sw != null )
				{
					sw.WriteLine( CsvFileManagerResources.Text_UndockedInstrument );
				}

				// if the instrument was undocked there is no reason to continue saving the datalog
				throw new InstrumentNotDockedException();
			}
		}

		/// <summary>
		/// Pulls the specific values out of the passed in event in the same order that the
		/// _fieldNames were defined in the corresponding Save method.  The values are then
		/// returned as a csv string.
		/// </summary>
		/// <param name="bumpEvent">the event containing the values to format as a csv record</param>
		/// <returns>a csv string containing the values of the event</returns>
		private string ConvertBumpEventToCsv( InstrumentBumpTestEvent bumpEvent )
		{
			List<string> bumpRecord = new List<string>();

			// Instrument SN
			bumpRecord.Add( PrepareValueStringAlwaysQuote( bumpEvent.DockedInstrument.SerialNumber ) );

			// Bump Date
			bumpRecord.Add( PrepareValueString( Configuration.DockingStation.TimeZoneInfo.ToLocalTime( bumpEvent.Time ).ToString( FORMAT_DATE, this._cultureInfo ) ) );

			// Part Number
			bumpRecord.Add( PrepareValueString( bumpEvent.DockedInstrument.PartNumber ) );

			// Job Number
			bumpRecord.Add( PrepareValueStringAlwaysQuote( bumpEvent.DockedInstrument.JobNumber ) );

			// Setup Date
			bumpRecord.Add( PrepareValueString( bumpEvent.DockedInstrument.SetupDate.ToString( FORMAT_DATE, this._cultureInfo ) ) );

			// Setup Technician
			bumpRecord.Add( PrepareValueString( bumpEvent.DockedInstrument.SetupTech ) );

			// Created By
			bumpRecord.Add( PrepareValueString( CREATED_BY ) );

			// Battery
			bumpRecord.Add( PrepareValueString( GetBatteryText( bumpEvent.DockedInstrument.InstalledComponents ) ) );

			// Bump Threshold
			bumpRecord.Add( PrepareValueString( bumpEvent.DockedInstrument.BumpThreshold.ToString( FORMAT_INTEGER, this._cultureInfo ) ) );

			// Bump Timeout
			bumpRecord.Add( PrepareValueString( bumpEvent.DockedInstrument.BumpTimeout.ToString( FORMAT_INTEGER, this._cultureInfo ) ) );

			// Accessory Pump
			bumpRecord.Add( PrepareValueString( GetAccessoryPumpText( bumpEvent.DockedInstrument.AccessoryPump ) ) );

			for ( int i = 1; i <= _maxSensorCapacity; i++ )
			{
				if ( bumpEvent.GasResponses.Count < i )
				{
					// Pad the list with 13 empty string values every time that the 
					// count of gas responses is less than the max sensor capacity.
					// There are 13 fields to record per sensor for bump tests.
					for ( int j = 0; j < 13; j++ )
					{
						bumpRecord.Add( string.Empty );
					}

					continue;
				}

				// i starts at 1 not 0
				SensorGasResponse sgr = bumpEvent.GasResponses[i - 1];
				bool wasSkipped = sgr.Status == Status.Skipped;

				// Sensor SN (1)
				bumpRecord.Add( PrepareValueStringAlwaysQuote( sgr.SerialNumber ) );

				// Sensor Type (2)
				bumpRecord.Add( PrepareValueString( PrintManagerResources.ResourceManager.GetString( sgr.SensorCode ) ) );

				if ( !wasSkipped )
				{
					// Gas Type (3)
					bumpRecord.Add( PrepareValueString( PrintManagerResources.ResourceManager.GetString( sgr.GasConcentration.Type.Code ) ) );

					// Span Gas (4)
					bumpRecord.Add( PrepareValueString( sgr.GasConcentration.Concentration.ToString( FORMAT_DECIMAL, this._cultureInfo ) ) );

					// Sensor Reading (5)
					bumpRecord.Add( PrepareValueString( sgr.Reading.ToString( FORMAT_DECIMAL, this._cultureInfo ) ) );
				}
				else
				{
					string valueNA = PrepareValueString( PrintManagerResources.Value_NA );

					// if the sensor was skipped, then don't print gas type (3), span gas (4) and sensor reading (5)
					bumpRecord.Add( valueNA );
					bumpRecord.Add( valueNA );
					bumpRecord.Add( valueNA );
				}

				// Passed/Failed (6)
				bumpRecord.Add( PrepareValueString( PrintManagerResources.ResourceManager.GetString( "Value_" + sgr.Status.ToString() ) ) );

				Sensor sensor = bumpEvent.DockedInstrument.GetInstalledComponentByUid( sgr.Uid ).Component as Sensor;

				// Alarm Low (7)
				bumpRecord.Add( PrepareValueString( sensor.Alarm.Low.ToString( FORMAT_DECIMAL, this._cultureInfo ) ) );

				// Alarm High (8)
				bumpRecord.Add( PrepareValueString( sensor.Alarm.High.ToString( FORMAT_DECIMAL, this._cultureInfo ) ) );

				// Alarm TWA (9)
				bumpRecord.Add( PrepareValueString( GetHygieneAlarmText( sensor.Alarm.TWA ) ) );

				// Alarm STEL (10)
				bumpRecord.Add( PrepareValueString( GetHygieneAlarmText( sensor.Alarm.STEL ) ) );

				// Bump Date/Time (11)
				bumpRecord.Add( PrepareValueString( Configuration.DockingStation.TimeZoneInfo.ToLocalTime( sgr.Time ).ToString( FORMAT_DATETIME, this._cultureInfo ) ) );

				string cylinderID, cylinderExp;
				GetUsedGasEndPointTextForBump( sgr.UsedGasEndPoints, out cylinderID, out cylinderExp );

				// Cylinder ID (12)
				bumpRecord.Add( PrepareValueStringAlwaysQuote( cylinderID ) );

				// Cylinder Exp (13)
				bumpRecord.Add( PrepareValueString( cylinderExp ) );
			}

			return ConvertPreparedListToCsv( bumpRecord );
		}

		/// <summary>
		/// Pulls the specific values out of the passed in event in the same order that the
		/// _fieldNames were defined in the corresponding Save method.  The values are then
		/// returned as a csv string.
		/// </summary>
		/// <param name="calEvent">the event containing the values to format as a csv record</param>
		/// <returns>a csv string containing the values of the event</returns>
		private string ConvertCalEventToCsv( InstrumentCalibrationEvent calEvent )
		{
			List<string> calRecord = new List<string>();

			// Instrument SN
			calRecord.Add( PrepareValueStringAlwaysQuote( calEvent.DockedInstrument.SerialNumber ) );

			// Calibration Date
			calRecord.Add( PrepareValueString( Configuration.DockingStation.TimeZoneInfo.ToLocalTime( calEvent.Time ).ToString( FORMAT_DATE, this._cultureInfo ) ) );

			// Part Number
			calRecord.Add( PrepareValueString( calEvent.DockedInstrument.PartNumber ) );

			// Job Number
			calRecord.Add( PrepareValueStringAlwaysQuote( calEvent.DockedInstrument.JobNumber ) );

			// Setup Date
			calRecord.Add( PrepareValueString( calEvent.DockedInstrument.SetupDate.ToString( FORMAT_DATE, this._cultureInfo ) ) );

			// Setup Technician
			calRecord.Add( PrepareValueString( calEvent.DockedInstrument.SetupTech ) );

			// Created By
			calRecord.Add( PrepareValueString( CREATED_BY ) );

			// Battery
			calRecord.Add( PrepareValueString( GetBatteryText( calEvent.DockedInstrument.InstalledComponents ) ) );

			// Accessory Pump
			calRecord.Add( PrepareValueString( GetAccessoryPumpText( calEvent.DockedInstrument.AccessoryPump ) ) );

			for ( int i = 1; i <= _maxSensorCapacity; i++ )
			{
				if ( calEvent.GasResponses.Count < i )
				{
					// Pad the list with 15 empty string values every time that the 
					// count of gas responses is less than the max sensor capacity.
					// There are 15 fields to record per sensor for calibrations.
					for ( int j = 0; j < 15; j++ )
					{
						calRecord.Add( string.Empty );
					}

					continue;
				}

				// i starts at 1 not 0
				SensorGasResponse sgr = calEvent.GasResponses[i - 1];
				bool wasSkipped = sgr.Status == Status.ZeroPassed; // ClO2 sensors are not calibrated
				double spanReserve = sgr.Reading / sgr.GasConcentration.Concentration;

				// Sensor SN (1)
				calRecord.Add( PrepareValueStringAlwaysQuote( sgr.SerialNumber ) );

				// Sensor Type (2)
				calRecord.Add( PrepareValueString( PrintManagerResources.ResourceManager.GetString( sgr.SensorCode ) ) );

				if ( !wasSkipped )
				{
					// Gas Type (3)
					calRecord.Add( PrepareValueString( PrintManagerResources.ResourceManager.GetString( sgr.GasConcentration.Type.Code ) ) );

					// Span Gas (4)
					calRecord.Add( PrepareValueString( sgr.GasConcentration.Concentration.ToString( FORMAT_DECIMAL, this._cultureInfo ) ) );

					// Span Reserve (5)
					if ( sgr.Status != Status.InstrumentAborted )
					{
						calRecord.Add( PrepareValueString( spanReserve.ToString( FORMAT_PERCENT, this._cultureInfo ) ) );
					}
					else
					{
						// if the instrument aborted calibration, do not print a span reserve value
						calRecord.Add( string.Empty );
					}
				}
				else
				{
					string valueNA = PrepareValueString( PrintManagerResources.Value_NA );

					// if the sensor was skipped, then don't print gas type (3), span gas (4) and sensor reading (5) 
					calRecord.Add( valueNA );
					calRecord.Add( valueNA );
					calRecord.Add( valueNA );
				}

				// Passed/Failed (6)
				calRecord.Add( PrepareValueString( GetCalPassedFailedText( wasSkipped, sgr.Passed, sgr.Status, spanReserve ) ) );

				Sensor sensor = calEvent.DockedInstrument.GetInstalledComponentByUid( sgr.Uid ).Component as Sensor;

				// Alarm Low (7)
				calRecord.Add( PrepareValueString( sensor.Alarm.Low.ToString( FORMAT_DECIMAL, this._cultureInfo ) ) );

				// Alarm High (8)
				calRecord.Add( PrepareValueString( sensor.Alarm.High.ToString( FORMAT_DECIMAL, this._cultureInfo ) ) );

				// Alarm TWA (9)
				calRecord.Add( PrepareValueString( GetHygieneAlarmText( sensor.Alarm.TWA ) ) );

				// Alarm STEL (10)
				calRecord.Add( PrepareValueString( GetHygieneAlarmText( sensor.Alarm.STEL ) ) );

				// Cal Date/Time (11)
				calRecord.Add( PrepareValueString( Configuration.DockingStation.TimeZoneInfo.ToLocalTime( sgr.Time ).ToString( FORMAT_DATETIME, this._cultureInfo ) ) );

				string cylinderID, cylinderExp;
				string zeroCylinderID, zeroCylinderExp;
				GetUsedGasEndPointTextForCal( sgr.UsedGasEndPoints, out cylinderID, out cylinderExp, out zeroCylinderID, out zeroCylinderExp );

				// Cylinder ID (12)
				calRecord.Add( PrepareValueStringAlwaysQuote( cylinderID ) );

				// Cylinder Exp (13)
				calRecord.Add( PrepareValueString( cylinderExp ) );

				// Zero Cylinder ID (14)
				calRecord.Add( PrepareValueStringAlwaysQuote( zeroCylinderID ) );

				// Zero Cylinder Exp (15)
				calRecord.Add( PrepareValueString( zeroCylinderExp ) );
			}

			return ConvertPreparedListToCsv( calRecord );
		}

		/// <summary>
		/// Splits comma (or semicolon) separated values.  Assumes the values do not contain
		/// any commas (or semicolons) as this method is only intended to be used on the header row
		/// of an existing csv file created by this docking station.
		/// </summary>
		/// <param name="csv">comma separated values</param>
		/// <returns>a list of value strings</returns>
		private List<string> ConvertCsvStringToList( string csv )
		{
			// comma or semicolon expected, multi-character list separator not supported
			if ( this._listSeparator.Length != 1 )
			{
				throw new ArgumentException( "The list separator for the current culture is not a single character." );
			}

			string[] values = csv.Split( new char[] { this._listSeparator[0] } );

			return new List<string>( values );
		}

		/// <summary>
		/// Returns a single datalog reading for each datalog sensor session as a single csv record using the indexes and objects provided.
		/// </summary>
		/// <param name="periodIndex">the index of the datalog period to use</param>
		/// <param name="readingIndex">the index of the datalog reading to use (within the specified datalog period)</param>
		/// <param name="sessionHelper">contains datalog session level values to log</param>
		/// <param name="sensorSessions">contains the sensor readings to log</param>
		/// <returns></returns>
		private string ConvertDatalogReadingToCsv( int periodIndex, int readingIndex, DatalogSessionHelper sessionHelper, List<DatalogSensorSessionHelper> sensorSessionHelpers )
		{
			List<string> datalogRecord = new List<string>();

			// PeriodNumber
			datalogRecord.Add( sessionHelper.PeriodNumber.ToString( FORMAT_INTEGER, this._cultureInfo ) );

			// User 
			datalogRecord.Add( sessionHelper.User );

			// Site 
			datalogRecord.Add( sessionHelper.Site );

			// ReadingTime
			datalogRecord.Add( sessionHelper.ReadingTime.ToString( FORMAT_DATETIME, this._cultureInfo ) );

			for ( int i = 0; i < sensorSessionHelpers.Count; i++ )
			{
				// get the gas readings to log one sensor at a time
				DatalogReadingInfo readingInfo = sensorSessionHelpers[i].GetNextReading( periodIndex, readingIndex );

				// only log the temperature from the first sensor session
				if ( i == 0 )
				{
					// Temperature
					datalogRecord.Add( readingInfo.Temperature.ToString( FORMAT_INTEGER, this._cultureInfo ) );
				}

				// Sensor Gas Reading
				datalogRecord.Add( readingInfo.Reading.ToString( FORMAT_DECIMAL, this._cultureInfo ) );

				if ( readingInfo.IsStelTwaEligible )
				{
					// Sensor TWA
					datalogRecord.Add( readingInfo.Twa.ToString( FORMAT_DECIMAL, this._cultureInfo ) );

					// Sensor STEL
					datalogRecord.Add( readingInfo.Stel.ToString( FORMAT_DECIMAL, this._cultureInfo ) );
				}
				else
				{
					// Sensor TWA and STEL (not applicable)
					datalogRecord.Add( this._valueNA );
					datalogRecord.Add( this._valueNA );
				}
			}

			return ConvertListToCsv( datalogRecord );
		}

		/// <summary>
		/// This method converts a list of strings into a single csv record.
		/// Values with commas and double quotes will be handled appropriately.
		/// The csv record will not contain the newline character(s).
		/// </summary>
		/// <param name="list">a list of strings to be converted into a single csv record</param>
		/// <returns>a single csv record</returns>
		private string ConvertListToCsv( List<string> list )
		{
			_sb.Length = 0;

			if ( list.Count > 0 )
			{
				_sb.Append( PrepareValueString( list[0] ) );
			}
			for ( int i = 1; i < list.Count; i++ )
			{
				_sb.Append( this._listSeparator + PrepareValueString( list[i] ) );
			}

			return _sb.ToString();
		}

		/// <summary>
		/// This method converts a list of prepared strings into a single csv record.
		/// The csv record will not contain the newline character(s).
		/// </summary>
		/// <param name="list">a list of strings to be converted into a single csv record</param>
		/// <returns>a single csv record</returns>
		private string ConvertPreparedListToCsv( List<string> list )
		{
			_sb.Length = 0;

			if ( list.Count > 0 )
			{
				// values were already prepared
				_sb.Append( list[0] );
			}
			for ( int i = 1; i < list.Count; i++ )
			{
				// values were already prepared
				_sb.Append( this._listSeparator + list[i] );
			}

			return _sb.ToString();
		}

		/// <summary>
		/// Creates a new file and writes the provided field names on a single line
		/// separated by commas.
		/// </summary>
		/// <param name="filePath"></param>
		/// <param name="fieldNames"></param>
		private void CreateCsvFile( string filePath, List<string> fieldNames )
		{
			string csv = ConvertListToCsv( fieldNames );

			Log.Debug( string.Format( "{0}Creating new file {1}", _logLabel, filePath ) );
			// UTF8 text files work better with Microsoft applications (Excel) when the byte order mark 
			// (BOM) prefixes the text.  File.CreateText uses UTF8, but does not use the BOM.  When opened 
			// with a text editor that is interpreting the text as Latin-1 or code page 1252, the BOM 
			// (EF BB BF) will appear at the beginning of the file as character string "ï»¿".
			using ( StreamWriter sw = new StreamWriter( filePath, false, new UTF8Encoding(true) ) )
			{
				LogCsv( csv );
				sw.WriteLine( csv );
			}

			// Correct file created timestamp from UTC to local time.
			//FileHelper.SetCreationTime( filePath, Configuration.GetLocalTime() );
		}

		/// <summary>
		/// Compares the first csv line in the provided file path with the provided list of strings 
		/// to see if they have contain the same fields. (Capitalization is not ignored.)
		/// </summary>
		/// <param name="filePath">the location of the existing csv file</param>
		/// <param name="expectedFieldNames">the list of fields</param>
		/// <returns>true if the actual fields match the expected list, otherwise false</returns>
		private bool DoFieldsMatch( string filePath, List<string> expectedFieldNames )
		{
			string s = null;

			using ( StreamReader sr = File.OpenText( filePath ) )
			{
				// read only the first line which should contain the field names
				s = sr.ReadLine();
			}

			if ( s == null )
			{
				Log.Debug( string.Format( "{0}{1} is completely empty", _logLabel, filePath ) );

				// no field names were found
				return false;
			}

			List<string> actualFieldNames = ConvertCsvStringToList( s );

			if ( expectedFieldNames.Count != actualFieldNames.Count )
			{
				Log.Debug( string.Format( "{0}{1} has a different quantity of fields", _logLabel, filePath ) );

				// if the expected and actual lists do not contain the same
				// number of fields they are not the same
				return false;
			}

			for ( int i = 0; i < expectedFieldNames.Count; i++ )
			{
				if ( expectedFieldNames[i] != actualFieldNames[i] )
				{
					Log.Debug( string.Format( "{0}{1} has different fields than what is expected", _logLabel, filePath ) );

					// found a mismatch in fields
					return false;
				}
			}

			Log.Debug( string.Format( "{0}{1} has the expected list of fields", _logLabel, filePath ) );

			// the actual field names match the expected field names
			return true;
		}
		
		/// <summary>
		/// Gets the localized display text for the accessory pump.
		/// </summary>
		/// <param name="pumpSetting">the accessory pump state</param>
		/// <returns>localized display text for the accessory pump</returns>
		private string GetAccessoryPumpText( AccessoryPumpSetting pumpSetting )
		{
			switch ( pumpSetting )
			{
				case AccessoryPumpSetting.Installed:
					return PrintManagerResources.Value_Installed;
				case AccessoryPumpSetting.Uninstalled:
					return PrintManagerResources.Value_Uninstalled;
				default:
					return PrintManagerResources.Value_NA;
			}
		}

		/// <summary>
		/// Gets the localized display text for the first battery found within a list
		/// of installed components.
		/// </summary>
		/// <param name="installedComponents">the list of installed components</param>
		/// <returns>localized display text for the battery component</returns>
		private string GetBatteryText( List<InstalledComponent> installedComponents )
		{
			string batteryCode = string.Empty;
			string battery = string.Empty;

			// find the first battery code
			foreach ( InstalledComponent ic in installedComponents )
			{
				if ( ic.Component is Battery )
				{
					batteryCode = ic.Component.Type.Code;
					break;
				}
			}

			// get the display text for the battery code
			if ( String.IsNullOrEmpty( batteryCode ) )
			{
				battery = PrintManagerResources.Value_NA;
			}
			else
			{
				battery = PrintManagerResources.ResourceManager.GetString( batteryCode );
			}

			return battery;
		}

		/// <summary>
		/// Gets the ID and expiration date of the final cylinder used for the bump test.
		/// </summary>
		/// <param name="cylindersUsed">the list of cylinders used during the gas operation</param>
		/// <param name="cylinderID">the ID of the final cylinder used for the bump test</param>
		/// <param name="cylinderExp">the expiration date of the final cylinder used for the bump test</param>
		private void GetUsedGasEndPointTextForBump( List<UsedGasEndPoint> usedGasEndPoints, out string cylinderID, out string cylinderExp )
		{
			cylinderID = string.Empty;
			cylinderExp = string.Empty;

			// The final cylinder used for a specific usage will be later in the list.  
			// Therefore, we are looping backwards through the list and only using the  
			// first cylinder found that matches the desired usage.
			for ( int i = usedGasEndPoints.Count - 1; i >= 0; i-- )
			{
				UsedGasEndPoint used = usedGasEndPoints[i];

				if ( used.Usage == CylinderUsage.Bump )
				{
					if ( used.Cylinder.IsFreshAir )
					{
						cylinderID = PrintManagerResources.Value_FreshAir;
						cylinderExp = PrintManagerResources.Value_NA;
					}
					else
					{
						cylinderID = used.Cylinder.FactoryId;
						cylinderExp = used.Cylinder.ExpirationDate.ToString( FORMAT_DATE, this._cultureInfo );
					}

					break;
				}
			}
		}
		
		/// <summary>
		/// Gets the ID and expiration date of the final cylinder used for zeroing and calibration.
		/// </summary>
		/// <param name="cylindersUsed">the list of cylinders used during the gas operation</param>
		/// <param name="cylinderID">the ID of the final cylinder used for the calibration</param>
		/// <param name="cylinderExp">the expiration date of the final cylinder used for the calibration</param>
		/// <param name="zeroCylinderID">the ID of the final cylinder used for zeroing</param>
		/// <param name="zeroCylinderExp">the expiration date of the final cylinder used for zeroing</param>
		private void GetUsedGasEndPointTextForCal( List<UsedGasEndPoint> usedGasEndPoints, out string cylinderID, out string cylinderExp, out string zeroCylinderID, out string zeroCylinderExp )
		{
			cylinderID = string.Empty;
			cylinderExp = string.Empty;
			zeroCylinderID = string.Empty;
			zeroCylinderExp = string.Empty;

			// The final cylinder used for a specific usage will be later in the list.  
			// Therefore, we are looping backwards through the list and only using the  
			// first cylinder found that matches the desired usage.
			for ( int i = usedGasEndPoints.Count - 1; i >= 0; i-- )
			{
				UsedGasEndPoint used = usedGasEndPoints[i];

				if ( used.Usage == CylinderUsage.Calibration && cylinderID == string.Empty )
				{
					if ( used.Cylinder.IsFreshAir )
					{
						cylinderID = PrintManagerResources.Value_FreshAir;
						cylinderExp = PrintManagerResources.Value_NA;
					}
					else
					{
						cylinderID = used.Cylinder.FactoryId;
						cylinderExp = used.Cylinder.ExpirationDate.ToString( FORMAT_DATE, this._cultureInfo );
					}

					break;
				}
				else if ( used.Usage == CylinderUsage.Zero && zeroCylinderID == string.Empty )
				{
					if ( used.Cylinder.IsFreshAir )
					{
						zeroCylinderID = PrintManagerResources.Value_FreshAir;
						zeroCylinderExp = PrintManagerResources.Value_NA;
					}
					else
					{
						zeroCylinderID = used.Cylinder.FactoryId;
						zeroCylinderExp = used.Cylinder.ExpirationDate.ToString( FORMAT_DATE, this._cultureInfo );
					}
				}
			}
		}

		/// <summary>
		/// Gets the localized display text for the Passed/Failed status field for a calibration.
		/// </summary>
		/// <param name="wasSkipped">was the sensor skipped (e.g. ClO2)</param>
		/// <param name="sgr">the </param>
		/// <param name="spanReserve"></param>
		/// <returns></returns>
		private string GetCalPassedFailedText( bool wasSkipped, bool isPassedCal, Status calStatus, double spanReserve )
		{
			string calStatusText = string.Empty;
			if ( !wasSkipped )
			{
				if ( isPassedCal )
				{
					// passed statuses
					if ( spanReserve > MARGINAL_SPANRESERVE )
					{
						// 71 - 100+%
						calStatusText = PrintManagerResources.Value_Passed;
					}
					else
					{
						// 50 - 70%
						calStatusText = PrintManagerResources.Value_Marginal;
					}
				}
				else
				{
					// failed statuses
					if ( calStatus == Status.InstrumentAborted )
					{
						calStatusText = PrintManagerResources.Value_Aborted;
					}
					else
					{
						calStatusText = PrintManagerResources.Value_Failed;
					}
				}
			}
			else
			{
				// sensor was not calibrated
				calStatusText = PrintManagerResources.Value_Skipped;
			}

			return calStatusText;
		}

		/// <summary>
		/// Gets the localized text for a TWA or STEL alarm.
		/// </summary>
		/// <param name="alarm">the TWA or STEL alarm</param>
		/// <returns>the text to print for the provided TWA or STEL alarm setting</returns>
		private string GetHygieneAlarmText( double alarm )
		{
			if ( alarm <= 0 )
			{
				return PrintManagerResources.Value_NA;
			}

			return alarm.ToString( FORMAT_DECIMAL, this._cultureInfo );
		}

		/// <summary>
		/// Gets a list of indexes of sensor sesssions that are loggable.  This will only return indexes of 
		/// sensors in an OK state, or the VIRTUAL sensor instead of the sensors being virtualized.
		/// </summary>
		/// <param name="sensorSessions">the sensor sessions to evaluate for logging</param>
		/// <returns>a list of indexes that indicate which sensor sessions to log</returns>
		private List<int> GetLoggableSensorSessionIndexes( List<DatalogSensorSession> sensorSessions )
		{
			List<int> sensorIndexes = new List<int>();

			// If TX1 has 2 sensors installed there will be a VIRTUAL 
			// sensor in the datalog (even if one has a fault)
			if ( Configuration.DockingStation.Type == DeviceType.TX1 )
			{
				for ( int i = 0; i < sensorSessions.Count; i++ )
				{
					if ( sensorSessions[i].Status != SensorStatuses.OK )
					{
						continue;
					}

					// TX1 will not have more than one virtual sensor,
					// so if one is found, it should be the only sensor logged
					if ( sensorSessions[i].IsVirtual )
					{						
						sensorIndexes.Clear();
						sensorIndexes.Add( i );
						break;
					}

					// this sensor may have its session logged
					sensorIndexes.Add( i );
				}
			}
			// Ventis Pro Series and SafeCore instruments will not log DualSensed physical sensors for  
			// a given gas code if a virtual sensor with an OK status is found for the same gas.  
			// Non-DualSensed physical sensors with an OK status will be logged even if a 
			// virtual sensor exists for the same gas.
			else if ( Master.Instance.SwitchService.Instrument.Type == DeviceType.VPRO ||
					  Master.Instance.SwitchService.Instrument.Type == DeviceType.SC ) 
			{
				// build dictionary of virtualized gases
				Dictionary<string,bool> virtualGases = new Dictionary<string,bool>();
				for ( int i = 0; i < sensorSessions.Count; i++ )
				{
					// Virtual sensors should not be logged unless they have an OK status.
					// Virtual sensors will NEVER have bit 9 (DualSense) set.
					if ( sensorSessions[i].IsVirtual && sensorSessions[i].Status == SensorStatuses.OK )
					{
						// only one entry is needed per gas code 
						virtualGases[sensorSessions[i].Gas.Code] = true;
					}
				}

				// indicate which sensors will have their sessions logged
				for ( int i = 0; i < sensorSessions.Count; i++ )
				{
					// Virtual sensors should not be logged unless they have an OK status.
					// Virtual sensors will NEVER have bit 9 (DualSense) set.
					if ( sensorSessions[i].IsVirtual && sensorSessions[i].Status == SensorStatuses.OK )
					{
						// logging multiple virtual sensors with the same gas code is okay
						sensorIndexes.Add( i );
					}
					else
					{
						if ( ( sensorSessions[i].Status & SensorStatuses.DualSense ) == SensorStatuses.DualSense 
							&& virtualGases.ContainsKey( sensorSessions[i].Gas.Code ) )
						{
							// This DualSensed physical sensor will not be logged because we have a 
							// virtual sensor of the same gas to log instead.  
							// We don't need to check for a DualSensed sensor that doesn't have its gas virtualized.  
							// If at least one sensor of a DualSensed pair is working, there should be a virtual
							// sensor as well with an OK status.
							continue;
						}
						else if ( sensorSessions[i].Status != SensorStatuses.OK )
						{
							// this physical sensor will not be logged because it does
							// not have a status of OK
							continue;
						}
						else
						{
							// this sensor will have its session logged
							sensorIndexes.Add( i );
						}
					}
				}
			}
			else // all other supported instruments
			{
				for ( int i = 0; i < sensorSessions.Count; i++ )
				{
					if ( sensorSessions[i].Status != SensorStatuses.OK )
					{
						continue;
					}

					// this sensor will have its session logged
					sensorIndexes.Add( i );
				}
			}

			Log.Debug( string.Format( "{0}{1} of {2} sensor sessions will be written to the file.", _logLabel, sensorIndexes.Count, sensorSessions.Count ) );

			return sensorIndexes;
		}

		/// <summary>
		/// Initializes the max sensor capacity for instruments that can be docked on the current docking station.
		/// </summary>
		private void InitializeMaxSensorCapacity()
		{
			if ( Configuration.DockingStation.Type == DeviceType.MX4 ) _maxSensorCapacity = new VentisProDefinition( string.Empty, EquipmentSubType.VentisPro5 ).MaxSensorCapacity; // Ventis Pro5 has the max sensor capacity of any instrument supported by the MX4 docking station
			else if ( Configuration.DockingStation.Type == DeviceType.MX6 ) _maxSensorCapacity = new Mx6Definition().MaxSensorCapacity;
			else if ( Configuration.DockingStation.Type == DeviceType.SC ) _maxSensorCapacity = new SafeCoreDefinition().MaxSensorCapacity;
			else if ( Configuration.DockingStation.Type == DeviceType.TX1 ) _maxSensorCapacity = new Tx1Definition().MaxSensorCapacity;
			else if ( Configuration.DockingStation.Type == DeviceType.GBPRO ) _maxSensorCapacity = new GbProDefinition().MaxSensorCapacity;
			else if ( Configuration.DockingStation.Type == DeviceType.GBPLS ) _maxSensorCapacity = new GbPlusDefinition().MaxSensorCapacity;
			else throw new System.NotSupportedException( "\"" + Configuration.DockingStation.Type.ToString() + "\" is not a supported instrument." );
		}

		/// <summary>
		/// Writes a long csv record out to the debug log by splitting the string into 
		/// 90 characters per line.
		/// </summary>
		/// <param name="csv">the string to be written to the debug log</param>
		private void LogCsv( string csv )
		{
			const int CHUNK_SIZE = 90;
			int i = 0;
			string partial;

			for ( ; i < csv.Length - CHUNK_SIZE; i += CHUNK_SIZE )
			{
				partial = csv.Substring( i, CHUNK_SIZE );
				Log.Debug( string.Format( "{0}{1}-->", _logLabel, partial ) );
			}
			partial = csv.Substring( i );
			Log.Debug( string.Format( "{0}{1}", _logLabel, partial ) );
		}

		/// <summary>
		/// If the string value has a comma (or semicolon) or a double quote if will need 
		/// to be surrounded with double quotes.  Newline characters should also be
		/// considered. However, as newline characters never appear on the cal and 
		/// bump certificates it will not be checked.  Double quotes that are 
		/// supposed to appear within the value need escaped by a second double quote.
		/// </summary>
		/// <param name="value">single column value that needs formatted before being written to a csv file</param>
		/// <returns>single column value that is formatted and is now ready to be written to a csv file</returns>
		private string PrepareValueString( string value )
		{
			bool hasSeparator = false;
			bool hasDoubleQuote = false;

			if ( value.IndexOf( '"' ) >= 0 )
			{
				hasDoubleQuote = true;
			}

			// typically a comma or a semicolon
			if ( value.IndexOf( this._listSeparator ) >= 0 )
			{
			    hasSeparator = true;
			}

			if ( hasDoubleQuote )
			{
				// replace " with ""
				value = value.Replace( "\"", "\"\"" );
			}

			// the entire value needs surrounded with double quotes if it 
			// contains a comma or double quotes
			if ( hasDoubleQuote || hasSeparator )
			{
				value = "\"" + value + "\"";
			}

			return value;
		}

		/// <summary>
		/// The string value will always be surrounded with double quotes.  Newline 
		/// characters should also be considered. However, as newline characters never 
		/// appear on the cal and bump certificates it will not be checked.  Double quotes 
		/// that are supposed to appear within the value need escaped by a second double quote.
		/// </summary>
		/// <param name="value">single column value that needs formatted before being written to a csv file</param>
		/// <returns>single column value that is formatted and is now ready to be written to a csv file</returns>
		private string PrepareValueStringAlwaysQuote( string value )
		{
			if ( value.IndexOf( '"' ) >= 0 )
			{
				// replace " with ""
				value = value.Replace( "\"", "\"\"" );
			}

			// the entire value will be surrounded with double quotes; 
			// this is to provide better support for Excel when the csv file is not
			// properly imported (e.g. sensor s/n that only contains numbers 1408290012)
			return "\"" + value + "\"";
		}

		/// <summary>
		/// Moves an incompatible file by renaming it.  The new file name
		/// contains a unique number at the end of it.
		/// e.g. BumpTests_13034J3-001.csv to BumpTests_13034J3-001_1.csv
		/// </summary>
		/// <param name="filePath">the path of the file to rename</param>
		private void RenameCsvFile( string filePath )
		{
			int extIndex = filePath.LastIndexOf( '.' );
			string filePathNoExt = filePath.Substring( 0, extIndex );
			string fileExt = filePath.Substring( extIndex );

			int i = 0;
			string newFilePath;

			do
			{
				i++;
				newFilePath = filePathNoExt + "_" + i + fileExt;
			}
			while ( File.Exists( newFilePath ) );

			Log.Debug( string.Format( "{0}Moving and renaming {1} to {2}", _logLabel, filePath, newFilePath ) );
			File.Move( filePath, newFilePath );
		}

		/// <summary>
		/// Saves certain instrument events to a connected USB drive.
		/// </summary>
		/// <param name="instrumentEvent">an event that could be saved</param>
		public void Save( InstrumentGasResponseEvent instrumentEvent )
		{
			// only store bumps and cals
			if ( !( instrumentEvent is InstrumentBumpTestEvent ||
				instrumentEvent is InstrumentCalibrationEvent ) )
			{
				return;
			}

			_wasEventSaved = false;
			bool showStatus = false;
			
			try
			{
				// need a USB drive attached to continue
				if ( !Controller.IsUsbDriveAttached( _logLabel ) )
				{
					Log.Debug( string.Format( "{0}Can't save event to file.", _logLabel ) );
					return;
				}

				// we know that there is a USB drive attached so show the saving to file outcome on the LCD
				showStatus = true;

				Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.FILEMANAGER_SAVING );
				// sleeping to give the Console service time to show the saving message and 
				// for the user to be able to read it
				Thread.Sleep( 2000 );

				this._cultureInfo = Configuration.DockingStation.Language.Culture;
				this._listSeparator = this._cultureInfo.TextInfo.ListSeparator;

				PrintManagerResources.Culture = this._cultureInfo;
				CsvFileManagerResources.Culture = this._cultureInfo;
				InitializeMaxSensorCapacity();

				if ( instrumentEvent is InstrumentBumpTestEvent )
				{
					Save( (InstrumentBumpTestEvent)instrumentEvent );
					_wasEventSaved = true;
				}
				else if ( instrumentEvent is InstrumentCalibrationEvent )
				{
					Save( (InstrumentCalibrationEvent)instrumentEvent );
					_wasEventSaved = true;
				}
			}
			catch ( Exception ex )
			{
				_wasEventSaved = false;
				Log.Error( string.Format( "{0}Error saving {1} to file.", _logLabel, instrumentEvent ), ex );
			}
			finally
			{
				// only show a status message on the LCD if we know a USB drive 
				if ( showStatus )
				{
					if ( _wasEventSaved )
					{
						Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.FILEMANAGER_SUCCEEDED );
						// sleeping to give the Console service time to show the saving message and 
						// for the user to be able to read it
						Thread.Sleep( 3000 );
					}
					else
					{
						Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.FILEMANAGER_FAILED );
						// sleeping to give the Console service time to show the saving message and 
						// for the user to be able to read it
						Thread.Sleep( 10000 );
					}
				}
			}
		}

		/// <summary>
		/// Saves the results of a bump test event to a connected USB drive in csv format.
		/// An unhandled exception will occur if saving to the USB drive failed.
		/// </summary>
		/// <param name="bumpEvent">the event to be saved</param>
		private void Save( InstrumentBumpTestEvent bumpEvent )
		{
			// e.g. BumpTests_13040NG-006.csv
			_fileName = CsvFileManagerResources.Filename_BumpTests_ + Configuration.DockingStation.SerialNumber + ".csv";
			_filePath = Controller.USB_DRIVE_PATH + _fileName;

			Log.Debug( string.Format( "{0}File for Bump Tests - {1}", _logLabel, _filePath ) );

			// build localized list of expected column headers
			_fieldNames = new List<string>();

			// instrument fields
			// these fields have colons that need removed
			_fieldNames.Add( PrintManagerResources.Label_InstrumentSN.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_BumpDate.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_PartNumber.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_JobNumber.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_SetupDate.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_SetupTechnician.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_CreatedBy.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_Battery.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_BumpThreshold.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_BumpTimeout.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_PumpAccessory.TrimEnd( _trimChars ) );

			for ( int i = 1; i <= _maxSensorCapacity; i++ )
			{
				string suffix = " " + i;

				// sensor fields
				_fieldNames.Add( PrintManagerResources.Column_SensorSN + suffix );
				_fieldNames.Add( PrintManagerResources.Column_SensorType + suffix );
				_fieldNames.Add( PrintManagerResources.Column_GasType + suffix );
				_fieldNames.Add( PrintManagerResources.Column_SpanGas + suffix );
				_fieldNames.Add( PrintManagerResources.Column_SensorReading + suffix );
				_fieldNames.Add( PrintManagerResources.Column_PassedFailed + suffix );
				_fieldNames.Add( PrintManagerResources.Column_AlarmLow + suffix );
				_fieldNames.Add( PrintManagerResources.Column_AlarmHigh + suffix );
				_fieldNames.Add( PrintManagerResources.Column_AlarmTWA + suffix );
				_fieldNames.Add( PrintManagerResources.Column_AlarmSTEL + suffix );

				// cylinder fields
				_fieldNames.Add( PrintManagerResources.Column_BumpDateTime + suffix );
				_fieldNames.Add( PrintManagerResources.Column_CylinderID + suffix );
				_fieldNames.Add( PrintManagerResources.Column_CylinderExp + suffix );
			}

			if ( File.Exists( _filePath ) )
			{
				Log.Debug( string.Format( "{0}{1} was found", _logLabel, _filePath ) );

				if (!DoFieldsMatch( _filePath, _fieldNames ))
				{
					RenameCsvFile( _filePath );

					// create new file with column headers
					CreateCsvFile( _filePath, _fieldNames );
				}
			}
			else
			{
				Log.Debug( string.Format( "{0}{1} was not found", _logLabel, _filePath ) );

				// create new file with column headers
				CreateCsvFile( _filePath, _fieldNames );
			}

			string csv = ConvertBumpEventToCsv( bumpEvent );
			LogCsv( csv );
			
			using ( StreamWriter sw = File.AppendText( _filePath ) )
			{
				sw.WriteLine( csv );
			}

			// Correct file modified timestamp from UTC to local time.
			//FileHelper.SetLastWriteTime( _filePath, Configuration.GetLocalTime() );

			Log.Debug( string.Format( "{0}Bump Test was saved to USB drive successfully", _logLabel ) );
		}
		
		/// <summary>
		/// Saves the results of a calibration event to a connected USB drive in csv format.
		/// An unhandled exception will occur if saving to the USB drive failed.
		/// </summary>
		/// <param name="calEvent">the event to be saved</param>
		private void Save( InstrumentCalibrationEvent calEvent )
		{
			// e.g. Calibrations_13040NG-006.csv
			_fileName = CsvFileManagerResources.Filename_Calibrations_ + Configuration.DockingStation.SerialNumber + ".csv";
			_filePath = Controller.USB_DRIVE_PATH + _fileName;

			Log.Debug( string.Format( "{0}File for Calibrations - {1}", _logLabel, _filePath ) );

			// build localized list of expected column headers
			_fieldNames = new List<string>();

			// instrument fields
			// these fields have colons that need removed
			_fieldNames.Add( PrintManagerResources.Label_InstrumentSN.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_CalibrationDate.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_PartNumber.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_JobNumber.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_SetupDate.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_SetupTechnician.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_CreatedBy.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_Battery.TrimEnd( _trimChars ) );
			_fieldNames.Add( PrintManagerResources.Label_PumpAccessory.TrimEnd( _trimChars ) );

			for ( int i = 1; i <= _maxSensorCapacity; i++ )
			{
				string suffix = " " + i;

				// sensor fields
				_fieldNames.Add( PrintManagerResources.Column_SensorSN + suffix );
				_fieldNames.Add( PrintManagerResources.Column_SensorType + suffix );
				_fieldNames.Add( PrintManagerResources.Column_GasType + suffix );
				_fieldNames.Add( PrintManagerResources.Column_SpanGas + suffix );
				_fieldNames.Add( PrintManagerResources.Column_SpanReserve + suffix );
				_fieldNames.Add( PrintManagerResources.Column_PassedFailed + suffix );
				_fieldNames.Add( PrintManagerResources.Column_AlarmLow + suffix );
				_fieldNames.Add( PrintManagerResources.Column_AlarmHigh + suffix );
				_fieldNames.Add( PrintManagerResources.Column_AlarmTWA + suffix );
				_fieldNames.Add( PrintManagerResources.Column_AlarmSTEL + suffix );

				// cylinder fields
				_fieldNames.Add( PrintManagerResources.Column_CalDateTime + suffix );
				_fieldNames.Add( PrintManagerResources.Column_CylinderID + suffix );
				_fieldNames.Add( PrintManagerResources.Column_CylinderExp + suffix );
				_fieldNames.Add( PrintManagerResources.Column_ZeroCylinderID + suffix );
				_fieldNames.Add( PrintManagerResources.Column_ZeroCylinderExp + suffix );
			}

			if ( File.Exists( _filePath ) )
			{
				Log.Debug( string.Format( "{0}{1} was found", _logLabel, _filePath ) );

				if ( !DoFieldsMatch( _filePath, _fieldNames ) )
				{
					RenameCsvFile( _filePath );

					// create new file with column headers
					CreateCsvFile( _filePath, _fieldNames );
				}
			}
			else
			{
				Log.Debug( string.Format( "{0}{1} was not found", _logLabel, _filePath ) );

				// create new file with column headers
				CreateCsvFile( _filePath, _fieldNames );
			}

			string csv = ConvertCalEventToCsv( calEvent );
			//Log.Debug( string.Format( "{0}{1}", _logLabel, csv ) );
			LogCsv( csv );

			using ( StreamWriter sw = File.AppendText( _filePath ) )
			{
				sw.WriteLine( csv );
			}

			// Correct file modified timestamp from UTC to local time.
			//FileHelper.SetLastWriteTime( _filePath, Configuration.GetLocalTime() );

			Log.Debug( string.Format( "{0}Calibration was saved to USB drive successfully", _logLabel ) );
		}

		/// <summary>
		/// Saves the downloaded datalog to a connected USB drive in csv format.  Each session
		/// will be stored in its own csv file.  If the event cannot be saved to the USB drive, 
		/// an exception will be thrown.
		/// </summary>
		/// <param name="datalogEvent">The event containing the datalog to save to the USB drive.</param>
		public void Save( InstrumentDatalogDownloadEvent datalogEvent )
		{
			_wasEventSaved = false;
			int sessionCount = -1;

			try
			{
				// need a USB drive attached to continue
				if ( !Controller.IsUsbDriveAttached( _logLabel ) )
				{
					Log.Debug( string.Format( "{0}Can't save event to file.", _logLabel ) );
				}
				else
				{
					this._cultureInfo = Configuration.DockingStation.Language.Culture;
					this._listSeparator = this._cultureInfo.TextInfo.ListSeparator;
					
					CsvFileManagerResources.Culture = this._cultureInfo;
					PrintManagerResources.Culture = this._cultureInfo;

					this._valueNA = CsvFileManagerResources.Text_NotApplicable;

					sessionCount = datalogEvent.InstrumentSessions.Count;
					Log.Debug( string.Format( "{0}Event contains {1} datalog sessions.", _logLabel, sessionCount ) );

					if ( sessionCount != 0 )
					{
						// only show the saving message if there is something to save
						Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.FILEMANAGER_SAVING );

						// sleeping to give the Console service time to show the saving message and 
						// for the user to be able to read it
						Thread.Sleep( 2000 );
					}

					for (int i = 0; i < sessionCount; i++)
					{
						Log.Debug( string.Format( "{0}Processing datalog session {1} of {2}.", _logLabel, i + 1, sessionCount ) );
						// each datalog session will be saved to its own file
						SaveDatalogSession( datalogEvent.InstrumentSessions[i] );
					}

					_wasEventSaved = true;
					Log.Debug( string.Format( "{0}All datalog sessions were saved to the USB drive successfully.", _logLabel ) );
				}
			}
			catch ( Exception ex )
			{
				_wasEventSaved = false;
				Log.Error( string.Format( "{0}Error saving {1} to file.", _logLabel, datalogEvent ), ex );
			}
			finally
			{
				if ( _wasEventSaved )
				{
					if ( sessionCount != 0 )
					{
						// only show the status message if there was something saved
						Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.FILEMANAGER_SUCCEEDED );

						// sleeping to give the Console service time to show the saving message and 
						// for the user to be able to read it
						Thread.Sleep( 3000 );
					}
				}
				else
				{
					Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.FILEMANAGER_FAILED );
					// sleeping to give the Console service time to show the saving message and 
					// for the user to be able to read it
					Thread.Sleep( 10000 );

					// need to make the DS go Unavailable so the instrument's datalog is not cleared
					throw new Exception( "Datalog was not saved to USB drive." );
				}
			}
		}

		/// <summary>
		/// Saves the datalog session to a connected USB drive in csv format.
		/// An unhandled exception will occur if saving to the USB drive failed.
		/// </summary>
		/// <param name="session">the datalog session to save</param>
		private void SaveDatalogSession( DatalogSession session )
		{
			// e.g. Datalog_13041C2-008_20140813_1327_1.csv
			StringBuilder fileName = new StringBuilder();
			fileName.Append(CsvFileManagerResources.Filename_Datalog_);
			fileName.Append(session.SerialNumber);
			fileName.Append(session.Session.ToString("_yyyyMMdd_HHmm_"));
			fileName.Append(session.SessionNumber);
			fileName.Append(".csv");
			
			_fileName = fileName.ToString();
			_filePath = Controller.USB_DRIVE_PATH + _fileName;

			Log.Debug( string.Format( "{0}File for datalog session - {1}", _logLabel, _filePath ) );

			// build localized list of expected column headers
			_fieldNames = new List<string>();

			// datalog fields
			_fieldNames.Add( CsvFileManagerResources.Column_A_PeriodNumber );
			_fieldNames.Add( CsvFileManagerResources.Column_B_User );
			_fieldNames.Add( CsvFileManagerResources.Column_C_Site );
			_fieldNames.Add( CsvFileManagerResources.Column_D_ReadingTime );
			_fieldNames.Add( CsvFileManagerResources.Column_E_Temp );

			// get the index of just the ok (or virtual) sensor sessions that should be logged
			List<int> logThese = GetLoggableSensorSessionIndexes( session.SensorSessions );

			if ( logThese.Count < 1 )
			{
				Log.Debug( string.Format( "{0}No loggable sensors found, datalog session will not be saved to USB drive.", _logLabel ) );
				return;
			}

			List<DatalogSensorSession> loggableSensorSessions = new List<DatalogSensorSession>(logThese.Count);
			for ( int i = 0; i < logThese.Count; i++ )
			{
				// x will hold the next sensor session index to use
				int x = logThese[i];

				// create a separate list that contains the references of only the loggable sensor sessions
				loggableSensorSessions.Add(session.SensorSessions[x]);
				
				// e.g. " (H2S)"
				string gasSymbolSuffix = " (" + loggableSensorSessions[i].Gas.Symbol + ")";

				// sensor fields
				_fieldNames.Add( PrintManagerResources.ResourceManager.GetString( loggableSensorSessions[i].Gas.Code, Configuration.DockingStation.Language.Culture ) + gasSymbolSuffix ); // e.g. Hydrogen Sulfide (H2S)
				_fieldNames.Add( CsvFileManagerResources.Column_G_SensorTWA + gasSymbolSuffix ); // e.g. TWA (H2S)
				_fieldNames.Add( CsvFileManagerResources.Column_H_SensorSTEL + gasSymbolSuffix ); // e.g. STEL (H2S)
			}

			CheckForUndockedInstrument( null );

			// the file name used should not already exist
			if ( File.Exists( _filePath ) )
			{
				Log.Warning( string.Format( "{0}{1} was found and will be deleted", _logLabel, _filePath ) );
				File.Delete( _filePath );
			}

			// create new file with column headers
			CreateCsvFile( _filePath, _fieldNames );

			try
			{

				// a helper class is used to hold required info from the DatalogSession that is needed for logging 
				DatalogSessionHelper sessionHelper = new DatalogSessionHelper( session.User, session.RecordingInterval, session.TWATimeBase );

				// writes sensor sessions to provided file path
				SaveDatalogSensorSessions( _filePath, sessionHelper, loggableSensorSessions );

				// add text to bottom of csv file if there was session corruption
				if ( session.CorruptionException != null )
				{
					Log.Debug( string.Format( "{0}Datalog session corruption detected.", _logLabel ) );

					using ( StreamWriter sw = File.AppendText( _filePath ) )
					{
						sw.WriteLine( CsvFileManagerResources.Text_SessionCorruptionDetected );
					}
				}
			}
			finally
			{
				try
				{
					// Correct file modified timestamp from UTC to local time.
					//FileHelper.SetLastWriteTime( _filePath, Configuration.GetLocalTime() );
				}
				catch (Exception ex)
				{
					// swallowing this yummy exception;
					// do not need to worry if the only thing that failed was updating the file's modified date to local time
					Log.Error( string.Format( "{0}Error updating file's last modified date to local time.", _logLabel ), ex );
				}
			}
		}

		/// <summary>
		/// Calculates the exposure readings for the provided sensor sessions.  Validates the sensor sessions to ensure they can be written
		/// out in a tabular form. If no issues are found, the datalog readings will be appended 
		/// </summary>
		/// <param name="filePath">the path of the file to append the datalog readings to</param>
		/// <param name="sessionHelper">contains session level values</param>
		/// <param name="sensorSessions">the sensor sessions that will be validated and logged</param>
		private void SaveDatalogSensorSessions( string filePath, DatalogSessionHelper sessionHelper, List<DatalogSensorSession> sensorSessions )
		{
			int sensorCount = sensorSessions.Count;
			int periodCount = sensorSessions[0].ReadingPeriods.Count;

			// all OK sensors should have the exact same number of uncompressed readings for each period; 
			// the below code will populate a 2D array to ensure the reading counts match between sensors so 
			// it can be written out in a tabular form; an unhandled exception will be thrown if they do not match
			// SS - Sensor Session; P - Period
			// e.g. SS0	SS1	SS2
			// P0	75	75	75
			// P1	120	120	120
			int[,] readingCount = new int[sensorCount, periodCount];

			for (int i = 0; i < sensorCount; i++)
			{
				// this should never be true
				if ( sensorSessions[i].ReadingPeriods.Count != periodCount )
				{
					Log.Error( string.Format( "{0}Sensor session at index {1} has an unexpected number of periods!", _logLabel, i ) );
					throw new Exception( "Unexpected quantity of reading periods detected in datalog sensor session." );
				}

				for (int j = 0; j < periodCount; j++)
				{
					int periodReadings = 0;
					
					// sum the readings for the current period
					for ( int k = 0; k < sensorSessions[i].ReadingPeriods[j].Readings.Count; k++ )
					{
						// sum the counts of compressed readings
						periodReadings += sensorSessions[i].ReadingPeriods[j].Readings[k].Count;
					}

					// e.g. SS0, P0 has 75 readings
					readingCount[i, j] = periodReadings;
				}

				// already looping through each sensor session so building the twa and stel readings here for use later
				sensorSessions[i].CalculateExposure( sessionHelper.RecordingInterval, sessionHelper.TwaTimeBase );
			}

			// verify all periods have the same reading counts across sensors
			for ( int i = 0; i < periodCount; i++ )
			{
				for ( int j = 1; j < sensorCount; j++ )
				{
					if ( readingCount[j - 1, i] != readingCount[j, i] ) 
					{
						Log.Error( string.Format( "{0}Unexpected quantity of readings detected in datalog period {1}!", _logLabel, i ) );
						throw new Exception( "Unexpected quantity of readings detected in datalog period." );
					}
				}
			}

			// build list of helpers that will efficiently keep track of next reading to log for the sensor session that it wraps
			List<DatalogSensorSessionHelper> sensorSessionHelpers = new List<DatalogSensorSessionHelper>(sensorCount);
			for ( int i = 0; i < sensorCount; i++ )
			{
				sensorSessionHelpers.Add( new DatalogSensorSessionHelper( sensorSessions[i] ) );
			}

			// file is left open because there could be thousands of records to write out
			using ( StreamWriter sw = File.AppendText( filePath ) )
			{
				for ( int i = 0; i < periodCount; i++ )
				{
					// periods were verified above to have the same number of readings;
					// so just using the number of readings from the first sensor
					int periodReadings = readingCount[0, i];

					// all sensors should have the same period, start time and site;
					// so just using the values from the first sensor
					int periodNumber = sensorSessions[0].ReadingPeriods[i].Period;
					DateTime periodStartTime = sensorSessions[0].ReadingPeriods[i].Time;
					string site = sensorSessions[0].ReadingPeriods[i].Location;

					Log.Debug( string.Format( "{0}Saving {1} readings for period {2}.", _logLabel, periodReadings, periodNumber ) );

					// updating values per the start of a new datalog period
					sessionHelper.StartNewPeriod( periodNumber, site, periodStartTime );

					for ( int j = 0; j < periodReadings; j++ )
					{
						// one reading for each sensor session is returned as a csv record
						string csv = ConvertDatalogReadingToCsv( i, j, sessionHelper, sensorSessionHelpers );

						// write one record to csv file
						sw.WriteLine( csv );
						
						// log progress in case it takes a while
						if ( ( j + 1 ) % 5000 == 0 )
						{
							Log.Debug( string.Format( "{0}{1} of {2} readings written.", _logLabel, j + 1, periodReadings ) );
							CheckForUndockedInstrument( sw );
						}

						// the period timestamp is the time of the first reading;
						// so increment after the first reading of the period has been processed
						sessionHelper.IncrementReadingTime();
					}

					CheckForUndockedInstrument( sw );
				}
			}
		}
		
		#endregion
	}

	/// <summary>
	/// A helper class used for convenient storage of values related to writing 
	/// a datalog session out to a csv file that are not specific to any sensor.
	/// </summary>
	internal class DatalogSessionHelper
	{
		#region Fields

		// changes with each reading
		DateTime _readingTime;

		// these fields can change with each new period
		int _periodNumber;
		string _site;

		// these fields will not change for the entirety of the session
		string _user;
		int _recordingInterval;
		int _twaTimeBase;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes only the values that will never change for the datalog session.
		/// </summary>
		internal DatalogSessionHelper( string user, int recordingInterval, int twaTimeBase )
		{
			this._user = user;
			this._recordingInterval = recordingInterval;
			this._twaTimeBase = twaTimeBase;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the time of the current datalog reading that will be logged.  Use the setter 
		/// to initialize the first reading time at the beginning of a period. 
		/// </summary>
		internal DateTime ReadingTime
		{
			get
			{
				return _readingTime;
			}
			set
			{
				_readingTime = value;
			}
		}
		/// <summary>
		/// Gets the number of the current datalog period that will be logged.
		/// </summary>
		internal int PeriodNumber
		{
			get
			{
				return _periodNumber;
			}
		}
		/// <summary>
		/// Gets the site/location for the current datalog reading that will be logged.
		/// This value may change when there is a new datalog period.
		/// </summary>
		internal string Site
		{
			get
			{
				if ( _site == null )
				{
					_site = String.Empty;
				}

				return _site;
			}
		}
		/// <summary>
		/// Gets the user for the current datalog session being logged.
		/// </summary>
		internal string User
		{
			get
			{
				if ( _user == null )
				{
					_user = String.Empty;
				}

				return _user;
			}
		}
		/// <summary>
		/// Gets the recording interval for the current datalog session being logged.
		/// </summary>
		internal int RecordingInterval
		{
			get
			{
				return _recordingInterval;
			}
		}
		/// <summary>
		/// Gets the TWA time base for the current datalog session being logged.
		/// </summary>
		internal int TwaTimeBase
		{
			get
			{
				return _twaTimeBase;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Sets values that may change between datalog periods.  Also initializes the reading time
		/// for the first reading of the datalog period.
		/// </summary>
		internal void StartNewPeriod( int periodNumber, string site, DateTime readingTime )
		{
			this._periodNumber = periodNumber;
			this._site = site;
			this._readingTime = readingTime;
		}

		/// <summary>
		/// This method should be called to increment the reading time after the first reading of the period 
		/// has been logged.
		/// </summary>
		internal void IncrementReadingTime()
		{
			this._readingTime = this._readingTime.AddSeconds( this._recordingInterval );
		}

		#endregion
	}

	/// <summary>
	/// A helper class that wraps a single DatalogSensorSession for convenient storage
	/// of values containing the context of the next sensor reading that will be 
	/// written out to a csv file.
	/// </summary>
	internal class DatalogSensorSessionHelper
	{
		#region Fields

		private DatalogSensorSession _sensorSession;
		private bool _isStelTwaEligible;
		private int _startIndex = -1;
		private int _lastPeriodIndex = -1;
		private int _minCount = 0;
		private int _maxCount = 0;
		private readonly DatalogExposure _emptyExposure = new DatalogExposure();

		#endregion

		#region Constructors

		internal DatalogSensorSessionHelper( DatalogSensorSession sensorSession )
		{
			this._sensorSession = sensorSession;
			this._isStelTwaEligible = GasCode.IsStelTwaEligible( sensorSession.Gas.Code );
		}

		#endregion

		#region Methods

		internal DatalogReadingInfo GetNextReading( int periodIndex, int readingIndex )
		{
			// when the period changes the count of compressed readings also change;
			// therefore the start index of compressed readings should be reset 
			if ( this._lastPeriodIndex != periodIndex )
			{
				this._lastPeriodIndex = periodIndex;
				this._startIndex = 0;
				this._maxCount = 0;
				this._minCount = 0;
			}

			DatalogReadingInfo readingInfo = null;

			// readings that match were compressed; 
			// loop through the compressed readings until we find the uncompressed index that was requested 
			for ( int i = this._startIndex; i < this._sensorSession.ReadingPeriods[periodIndex].Readings.Count; i++ )
			{
				// get the current compressed datalog reading to evaluate
				DatalogReading datalogReading = this._sensorSession.ReadingPeriods[periodIndex].Readings[i];
				this._maxCount += datalogReading.Count;

				// determine if the current set of compressed readings contains the index we are interested in;
				// readingIndex is 0-based and _maxCount is 1-based
				if ( readingIndex < this._maxCount )
				{
					// the twa and stel readings that correspond to the reading index
					DatalogExposure exposure;

					// exposure should have a count that matches the parent datalogReading object or the count should be 0
					if ( this._isStelTwaEligible && datalogReading.Exposure.Count != 0 )
					{
						// at least one exposure reading in the list of exposure readings for the compressed datalog readings is non-zero;
						// _maxCount - _minCount == datalogReading.Exposure.Count 
						exposure = datalogReading.Exposure[readingIndex - this._minCount];
					}
					else
					{
						// an empty exposure list means twa and stel readings were all 0
						exposure = _emptyExposure;
					}

					// get the current reading values for the provided sensor session
					readingInfo = new DatalogReadingInfo( datalogReading.Temperature, datalogReading.Reading, exposure.TwaReading, exposure.StelReading, this._isStelTwaEligible );

					// update values to make getting the next reading quick
					if ( readingIndex + 1 != this._maxCount )
					{
						this._startIndex = i;
						this._maxCount = this._minCount;
					}
					else
					{
						this._startIndex = i + 1;
						this._minCount = this._maxCount; 

						// the compressed datalog reading is no longer needed so clear it out to reclaim some memory
						datalogReading = null;
					}

					break;
				}

				// due to storing next reading context this line should never be used;
				// set for use in the next iteration of the loop
				this._minCount = this._maxCount;
			}

			// readingInfo should never be null
			return readingInfo;
		}

		#endregion
	}

	/// <summary>
	/// A helper class used for convenient storage of values related to writing 
	/// a datalog session out to a csv file that are specific to one sensor's gas reading.
	/// </summary>
	internal class DatalogReadingInfo
	{
		#region Fields

		private int _temperature;
		private float _reading;
		private float _twa;
		private float _stel;
		private bool _isStelTwaEligible;

		#endregion

		#region Constructors

		internal DatalogReadingInfo( int temperature, float reading, float twa, float stel, bool isStelTwaEligible )
		{
			this._temperature = temperature;
			this._reading = reading;
			this._twa = twa;
			this._stel = stel;
			this._isStelTwaEligible = isStelTwaEligible;
		}

		#endregion

		#region Properties

		internal int Temperature
		{
			get
			{
				return this._temperature;
			}
		}
		internal float Reading
		{
			get
			{
				return this._reading;
			}
		}
		internal float Twa
		{
			get
			{
				return this._twa;
			}
		}
		internal float Stel
		{
			get
			{
				return this._stel;
			}
		}
		internal bool IsStelTwaEligible
		{
			get
			{
				return this._isStelTwaEligible;
			}
		}

		#endregion
	}
}
