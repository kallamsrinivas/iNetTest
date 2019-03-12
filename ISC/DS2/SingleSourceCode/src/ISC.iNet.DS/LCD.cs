using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS
{
    public class LCD
    {
        internal enum Type
        {
            Reserved = -1,

            /// <summary>
            /// LCD manufactured by Phoenix Display International, Inc.
            /// </summary>
            Phoenix = 1,

            /// <summary>
            /// Legacy LCD manufactured by Okaya Electric.
            /// </summary>
            Okaya = 0
        }

        #region Fields

        private static bool _lcdInitialized;

        private static readonly object _lcdLock = new object();

		private static string[] _htmlTextualEntities =
			{
				"nbsp" , "iexcl" , "cent" , "pound" , "curren" , "yen" , "brvbar" , "sect" ,
				"uml" , "copy" , "ordf" , "laquo" , "not" , "shy" , "reg" , "macr" , "deg" ,
				"plusmn" , "sup2" , "sup3" , "acute" , "micro" , "para" , "middot" , "cedil" ,
				"sup1" , "ordm" , "raquo" , "frac14" , "frac12" , "frac34" , "iquest" ,
				"Agrave" , "Aacute" , "Acirc" , "Atilde" , "Auml" , "Aring" , "AElig" ,
				"Ccedil" , "Egrave" , "Eacute" , "Ecirc" , "Euml" , "Igrave" , "Iacute" ,
				"Icirc" , "Iuml" , "ETH" , "Ntilde" , "Ograve" , "Oacute" , "Ocirc" , "Otilde" ,
				"Ouml" , "times" , "Oslash" , "Ugrave" , "Uacute" , "Ucirc" , "Uuml" , "Yacute" ,
				"THORN" , "szlig" , 
				"agrave" , "aacute" , "acirc" , "atilde" , "auml" , "aring" , "aelig" ,
				"ccedil" , "egrave" , "eacute" , "ecirc" , "euml" , "igrave" , "iacute" ,
				"icirc" , "iuml" , "eth" , "ntilde" , "ograve" , "oacute" , "ocirc" , "otilde" ,
				"ouml" , "divide" , "oslash" , "ugrave" , "uacute" , "ucirc" , "uuml" , "yacute" ,
				"thorn" , "yuml"
			};

		// decimalEntityRegex = new Regex( "&#(\\d+);" );
		private static Regex _decimalEntityRegex = new Regex( @"\(\*#(\d+)\*\)" );

		// textualEntityRegex = new Regex( "&([^;]+);" );
		private static Regex _textualEntityRegex = new Regex( @"\(\*([^\*]+)\*\)" );

		// hexidecimalEntityRegex = new Regex( "&#x([0-9AaBbCcDdEeFf]+);" );
		private static Regex _hexidecimalEntityRegex = new Regex( @"\(\*#x([0-9AaBbCcDdEeFf]+)\*\)" );

        public static readonly char NOT_CONNECTED_ICON_CHAR = '\x001b';
        public static readonly char UPLOADING_ICON_CHAR = '\x001c';
        public static readonly char CHECKMARK_ICON_LEFT_CHAR = '\x001e';
        public static readonly char CHECKMARK_ICON_RIGHT_CHAR = '\x001f';

        /// <summary>
        /// Number of rows of characters the LCD can display.
        /// </summary>
        public const int MAX_COLUMNS = 16;
        /// <summary>
        /// Maximum columsn of characters the LCD can display.
        /// </summary>
        public const int MAX_ROWS = 7; 

        private static int _logCounter = 0;
        private const int MAX_LOG_MSGS_DISPLAYED = 6;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        static LCD()
        {
            if ( !_lcdInitialized )
            {
                Initialize();
                _lcdInitialized = true;
            }
        }

        /// <summary>
        /// Private constructor.  This class is intended to only be used statically.
        /// </summary>
        private LCD() { }

        #endregion Constructors

        #region DLLImports

        /// <summary>
        /// BSP SDK API: Initializes the LCD.
        /// </summary>
        /// <returns></returns>
        [DllImport( "sdk.dll" )]
        private static extern int LCDInit();

        /// <summary>
        /// BSP SDK API: Clears the LCD.
        /// </summary>
        [DllImport( "sdk.dll" )]
        private static extern int LCDClear();

        /// <summary>
        /// BSP SDK API: Sets the language.
        /// </summary>
        [DllImport( "sdk.dll" )]
        private static extern int LCDSetLanguage( byte data );

        /// <summary>
        /// BSP SDK API: Sets the constrast.
        /// </summary>
        [DllImport( "sdk.dll" )]
        private static extern int LCDSetContrast( byte contrast );

        /// <summary>
        /// BSP SDK API: Writes to LCD.
        /// </summary>
        [DllImport( "sdk.dll" )]
        private static extern int LCDWriteHigh( byte data , byte highBit , byte row , byte col , byte invert );

        /// <summary>
        /// BSP SDK API: Writes to LCD.
        /// </summary>
        [DllImport( "sdk.dll" )]
        private static extern int LCDWrite( byte data , byte row , byte col , byte invert );

        /// <summary>
        /// BSP SDK API: Writes to LCD.
        /// </summary>
        [DllImport( "sdk.dll" )]
        private static extern int LCDWriteChar( byte[] data , byte size , byte row , byte column , byte inverten );

        /// <summary>
        /// BSP SDK API: Sets the backlight's state.
        /// </summary>
        [DllImport( "sdk.dll" )]
        private static extern int LCDSetBackLightState( byte state );

        /// <summary>
        /// BSP SDK API: Queries the peripheral board for the type of LCD installed.
        /// Should only be called if board revision is Phoenix.
        /// </summary>
        /// <param name="MSB_Status"></param>
        /// <param name="LCB_Status"></param>
        /// <returns></returns>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int GetLCDType( byte* MSB_Status, byte* LCB_Status );

        #endregion DLLImports

        private static int Initialize()
        {
            return LCDInit();
        }

        public static int Clear()
        {
            return LCDClear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wrapMsg"></param>
        /// <returns></returns>
        public static string WrapString( string wrapMsg )
        {
            string lineStart = "<a>";
            string lineEnd = "</a>";

            if ( wrapMsg == string.Empty )
                return wrapMsg;

            // The strings in the resource file can force line breaks to occur by having
            // "\r\n" sequences in them.  Parse these out, and then wrap them separately.
            // 
            // e.g. "Line One\r\nLine Two" need to eventually become "<a>Line One</a><a>Line Two</a>".

            string[] msgs = wrapMsg.Split( "\n".ToCharArray() );

            for ( int i = 0; i < msgs.Length; i++ )
                msgs[i] = msgs[i].TrimEnd( "\r".ToCharArray() );

            wrapMsg = string.Empty;

            foreach ( string msg in msgs )
            {
                // Get all of the parts.
                string[] msgParts = msg.Split( " ".ToCharArray() );

                string tmpMsg = string.Empty;

                foreach ( string part in msgParts )
                {
                    if ( ( part != null ) && ( part != string.Empty ) )
                    {
                        if ( ( tmpMsg.Length + part.Length + 1 ) > DS.LCD.MAX_COLUMNS ) // fixed == NUM_COLS
                        {
                            wrapMsg += lineStart + tmpMsg + lineEnd;
                            tmpMsg = part;
                        }
                        else
                        {
                            if ( tmpMsg != string.Empty )
                                tmpMsg += " ";
                            tmpMsg += part;
                        }
                    }
                }
                if ( tmpMsg != string.Empty )
                    wrapMsg += lineStart + tmpMsg + lineEnd;
            }
            return wrapMsg;
        }

        public static int SetLanguage(Language language)
        {
            // Set the language on the IDS's display
            byte langID;
            switch (language.Code.ToUpper())
            {
                case Language.French:
                    langID = 2;
                    break;
                case Language.Spanish:
                    langID = 3;
                    break;
                case Language.German:
                    langID = 1;
                    break;
                case Language.PortugueseBrazil:  // SGF  1-Oct-2012  INS-1656
                    langID = 3;  // Just specify the Spanish language code, as we don't need a distinct code for Portuguese (Brazil).
                    break;
                default:
                    langID = 0; // English
                    break;
            }

            return LCDSetLanguage(langID);
        }

		private static int WriteHigh( byte data , byte highBit , byte row , byte col , byte invert )
        {
            return LCDWriteHigh( data, highBit, row, col, invert );
        }

		private static int Write( byte data , byte row , byte col , byte invert )
        {
            return LCDWrite( data, row, col, invert );
        }

		private static int WriteChar( byte[] data , byte size , byte row , byte column , byte inverten )
        {
            return LCDWriteChar( data, size, row, column, inverten );
        }

        #region Methods

        static internal Type GetLcdType()
        {
            byte MSB; // most significant byte
            byte LSB; // least significant byte

            unsafe
            {
                GetLCDType( &MSB, &LSB );
            }

            Type lcdType;

            if ( MSB == 1 )
            {
                if ( LSB == 1 )
                    lcdType = Type.Okaya;
                else
                    lcdType = Type.Reserved;
            }
            else // MSB = 0
            {
                if ( LSB == 1 )
                    lcdType = Type.Reserved;
                else
                    lcdType = Type.Phoenix;
            }

            Log.Debug( string.Format( "GetLcdType: MSB={0}, LSB={1}, LCD Type is \"{2}\".", MSB, LSB, lcdType.ToString() ) );

            return lcdType;
        }

        /// <summary>
		/// Turns the LCD backlight off.
		/// </summary>
		public static bool Backlight
		{
            set
            {
                LCDSetBackLightState( value ? (byte)1 : (byte)0  );
            }
		}

		public static void DisplayTestCharacters()
		{
			int	row = 0;
			int column = 0;

            Clear();

          	for ( int i = 0 ; i < 256 ; i++ )
            {

			    WriteHigh( Convert.ToByte( i & 0x007F ) , Convert.ToByte( i & 0x0080 ) , Convert.ToByte( row ) , Convert.ToByte( column ) , Convert.ToByte( 0 ) );
				WriteHigh( Convert.ToByte( i & 0x007F ) , Convert.ToByte( i & 0x0080 ) , Convert.ToByte( row + 1 ) , Convert.ToByte( column ) , Convert.ToByte( 1 ) );

				column++;

				if ( column >= 15 )
				{
					column = 0;
					row += 2;
				}

				if ( row >= 6 )
				{
					while ( Controller.GetKeyPress().Key == Controller.Key.None ) { /* Do Nothing */ }

					row = 0;

                    Clear();
				}
			}
		}

		/// <summary>
		/// Writes a specific character to the screen in the location specified.
		/// </summary>
		/// <param name="data">The pixel data for the character.</param>
		/// <param name="row">The row to place the character in.</param>
		/// <param name="column">The column to place the character at.</param>
		/// <param name="inverted">Whether to invert the character.</param>
		public static void Display( byte[] data , int row , int column , bool inverted )
		{
            byte invert = inverted? (byte)1 : (byte)0;

			WriteChar( data , ( byte ) data.Length, ( byte ) row , ( byte ) column , invert );
		}

		/// <summary>
		/// Write text to the LCD, vertically centered.
		/// The text will be centered on the LCD.
		/// The text that is enclosed in <a></a> is displayed normal.  
		/// The text that is enclosed in <b></b> is displayed inverted.
		/// Note: Each segment of tagged text is printed on a separate line.
		/// </summary>
		/// <param name="text">The text to be written</param>
        public static void Display( string text )
        {
            _logCounter = 0;
            Display( text, true );
        }

        /// <summary>
        /// Write text to the LCD, vertically centered.
        /// The text will be centered on the LCD.
        /// The text that is enclosed in <a></a> is displayed normal.  
        /// The text that is enclosed in <b></b> is displayed inverted.
        /// Note: Each segment of tagged text is printed on a separate line.
        /// </summary>
        /// <param name="text">The text to be written</param>
        public static void DisplayReady(string text)
        {
            Display(text, true);
            if (_logCounter < MAX_LOG_MSGS_DISPLAYED)
                _logCounter++;
        }

        /// <summary>
        /// Completely invert the screen.
        /// </summary>
        public static void BlackScreen()
        {
            Display( "<b>                </b><b>                </b><b>                </b><b>                </b><b>                </b><b>                </b><b>                </b>" );
        }

        /// <summary>
        /// Write text to the LCD. The text will be centered on the LCD.
        /// The text that is enclosed in <a></a> is displayed normal.  
        /// The text that is enclosed in <b></b> is displayed inverted.
        /// Note: Each segment of tagged text is printed on a separate line.
        /// </summary>
        /// <param name="text">The text to be written</param>
        /// <param name="centerVertically">Indicates if message should be
        /// automatically centered vertically on the screen.  If false,
        /// then text is displayed starting on the first row.
        /// </param>
        public static void Display( string text, bool centerVertically )
        {
            LogDisplay( text );

            lock ( _lcdLock )
            {
                Clear();

                if ( text.Replace( " " , string.Empty ).IndexOf( "<a>" ) < 0 
                &&   text.Replace( " " , string.Empty ).IndexOf( "<b>" ) < 0 )
                    text = "<a>" + text + "</a>";

                // Convert html text entities to the appropriate latin-1 byte values.
                text = ConvertEntities( text );

                // Split the text into tag and line parts.
                string[] lines = text.Split( "<>".ToCharArray() );

                // Determine the number of lines in the text
                int numLines = 0;
                foreach ( string line in lines )
                {
                    if ( ( line.Replace( " " , string.Empty ) == "/a" ) || ( line.Replace( " " , string.Empty ) == "/b" ) )
                        numLines++;
                }

                // Determine the first line to print to.
                int printRow = 0;
                if ( centerVertically )
                   printRow = Math.Max( ( ( MAX_ROWS - numLines + 1 ) / 2 ) , 0 );

                int excessLines = numLines - MAX_ROWS;

                // Initialize a few variables.
                bool printString = false;
                string  outLine = null;
                byte printInverse = 0;

                // Handle each line.
                foreach ( string line in lines )
                {
                    // Don't try and display lines that go beyond the maximum rows the LCD is capable of. Trying to do so
                    // causes problems in the LCD driver such as corrupted or frozen display.  (INS-5331 / INS-5438, 1/5/2015, JMP)
                    if ( printRow >= MAX_ROWS )
                        break;

                    if ( ( line.Trim() == "a" ) || ( line.Trim() == "b" ) )
                    {
                        printString = true; // The string is to be printed.
                    }
                    else if ( ( line.Replace( " " , string.Empty ) == "/a" ) || ( line.Replace( " " , string.Empty ) == "/b" ) )
                    {
                        bool outLineEmpty = ( outLine == null ) || ( outLine == string.Empty );

                        // If there is a string to print.
                        if ( !outLineEmpty )
                        {
                            printInverse = line.Replace( " " , string.Empty ) == "/b" ? (byte)1 : (byte)0;

                            // Determine the first column to print to.
                            int firstColumn = outLine.Length < MAX_COLUMNS ? (MAX_COLUMNS - outLine.Length + 1 ) / 2 : 0;

                            // Print the line.
                            for ( int i = 0 ; i < Math.Min( MAX_COLUMNS - firstColumn , outLine.Length ) ; i++ )
                            {
                                WriteHigh( Convert.ToByte( ( outLine[ i ] & 0x007F ) ) , Convert.ToByte( outLine[ i ] & 0x0080 ) , Convert.ToByte( printRow ) , Convert.ToByte( firstColumn + i ) , Convert.ToByte( printInverse ) );
                            }
                            outLine = null; // Clear the printed line.
                        }

                        // Watch out for text with so many lines that it exceeds the max lines of the LCD.
                        // In too many lines detected, then strip out blank lines as needed to try and 
                        // get everything to fit.
                        if ( outLineEmpty && ( excessLines > 0 ) )
                            excessLines--;
                        else
                            printRow++;

                        printString = false;
                    }
                    else if ( printString )
                    {
                        outLine = line; // It it is just a text line and it is in proper tags, set it to be printed.
                    }

                }  // end-foreach

            }  // end-lock
		}

        private static void LogDisplay( string text )
        {
#if DEBUG
            if ( text.Contains( "<a><a>" ) )
            {
                Log.Warning( "WARNING: LCD DISPLAY TEXT IS MALFORMED!..." );
                Log.Warning( string.Format( "[{0}]", text ) );
            }
#endif
            if ( Log.Level < LogLevel.Debug || _logCounter >= MAX_LOG_MSGS_DISPLAYED )
                return;

            // Scrub the string of control chars that are used for 
            // icons on the lcd.  we convert them to displayable chars.
            // We can't simply use string.Replace to do this as it seems 
            // to sometimes miss the control characters.
            StringBuilder sb = new StringBuilder( text.Length );
            for ( int i = 0; i < text.Length; i++ )
            {
                char c = text[ i ];

                if ( c == NOT_CONNECTED_ICON_CHAR )
                    c = 'X';
                else if ( c == UPLOADING_ICON_CHAR )
                    c = '^';
                else if ( c == CHECKMARK_ICON_LEFT_CHAR )
                    c = '*';
                else if ( c == CHECKMARK_ICON_RIGHT_CHAR )
                    c = ' ';

                sb.Append( c );
            }

            text = sb.ToString();

            // Scrub the text of 'html' tags, because the tags will cause XML
            // serialization errors if/when the logged text is uploaded to inet.
            text = text.Replace( "<a>", "  " );
            text = text.Replace( "<b>", "  " );
            text = text.Replace( "</a>", "  |" );
            text = text.Replace( "</b>", "  |" );

            text = "LCD Message: [" + text + "]";

            Log.Debug( text );
        }

		/// <summary>
		/// MatchEvaluator delegate for replace html entities of the form: &egrave;
		/// </summary>
		/// <param name="match">The match to evaluate.</param>
		/// <returns>The string representation of the textual entity.</returns>
		private static string ReplaceTextualEntity( Match match )
		{
			int decimalValue;

			// If its a success.
			if ( match.Success )
			{
				// Check each group for a valid sub-group.
				foreach ( Group grp in match.Groups )
				{
					// The group must be smaller than the match's length.
					if ( grp.Length < match.Length )
					{
						// Determine the decimal value of the textual reference.
						switch ( grp.Value )
						{
							case "gt" :

								decimalValue = 62;

								break;

							case "lt" :

								decimalValue = 60;

								break;

							case "quot" :

								decimalValue = 34;

								break;

							case "amp" :

								decimalValue = 38;

								break;

							default :

								decimalValue = -1;

								// Search for all of the others.
								for ( int i = 0 ; i < _htmlTextualEntities.Length ; i++ )
								{
									if ( _htmlTextualEntities[ i ] == grp.Value )
									{
										decimalValue = 160 + i;
										break;
									}
								}

								break;
						}

						if ( decimalValue != -1 )
						{
							// Convert it to a character.
							char finalChar = Convert.ToChar( decimalValue );
						
							// Return the string version.
							//return "" + finalChar;
                            return finalChar.ToString();
						}
					}
				}
			}

			// Otherwise don't replace it with anything different.
			return match.Value;
		}

		/// <summary>
		/// MatchEvaluator delegate for replace html entities of the form: &#xFF;
		/// </summary>
		/// <param name="match">The match to evaluate.</param>
		/// <returns>The string representation of the hexidecimal entity.</returns>
		private static string ReplaceHexidecimalEntity( Match match )
		{
			// If its a success.
			if ( match.Success )
			{
				// Check each group for a valid sub-group.
				foreach ( Group grp in match.Groups )
				{
					// The group must be smaller than the match's length.
					if ( grp.Length < match.Length )
					{
						// Get the decimal value.
						int decimalValue = int.Parse( grp.Value , System.Globalization.NumberStyles.AllowHexSpecifier );

						// Convert it to a character.
						char finalChar = Convert.ToChar( decimalValue );
						
						// Return the string version.
						//return "" + finalChar;
                        return finalChar.ToString();
					}
				}
			}

			// Otherwise don't replace it with anything different.
			return match.Value;
		}

		/// <summary>
		/// MatchEvaluator delegate for replace html entities of the form: &#160;
		/// </summary>
		/// <param name="match">The match to evaluate.</param>
		/// <returns>The string representation of the decimal entity.</returns>
		private static string ReplaceDecimalEntity( Match match )
		{
			// If its a success.
			if ( match.Success )
			{
				// Check each group for a valid sub-group.
				foreach ( Group grp in match.Groups )
				{
					// The group must be smaller than the match's length.
					if ( grp.Length < match.Length )
					{
						// Get the decimal value.
						int decimalValue = int.Parse( grp.Value );

						// Convert it to a character.
						char finalChar = Convert.ToChar( decimalValue );
						
						// Return the string version.
						//return "" + finalChar;
                        return finalChar.ToString();
					}
				}
			}

			// Otherwise don't replace it with anything different.
			return match.Value;
		}

		/// <summary>
		/// Convert all html text entities in the string into the appropriate
		/// iso 8869-1 (latin-1) value.
		/// </summary>
		/// <param name="text">The string that may include html text entities.</param>
		/// <returns>The converted string with replaced text entities.</returns>
		public static string ConvertEntities( string text )
		{
            // Replace decimal format html entities.
			text = _decimalEntityRegex.Replace( text , new MatchEvaluator( ReplaceDecimalEntity ) );

            // Replace hexidecimal format html entities.
			text = _hexidecimalEntityRegex.Replace( text , new MatchEvaluator( ReplaceHexidecimalEntity ) );

            // Replace hexidecimal format html entities.
			text = _textualEntityRegex.Replace( text , new MatchEvaluator( ReplaceTextualEntity ) );

			// Return the modified text.
			return text;
        }

        #endregion Methods

    }
}
