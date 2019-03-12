using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.SmartCards;
using ISC.SmartCards.Types;
using ISC.WinCE;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS
{
    public class SmartCardManager
    {
        /// <summary>
        /// IDS API: Reads the smart card.
        /// </summary>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int ReadSmartCard( byte id, byte[] data, uint bufferSize, uint* byteRead );

        /// <summary>
        /// IDS API: Writes the smart card.
        /// </summary>
        [DllImport( "sdk.dll" )]
        private static extern int WriteSmartCard( byte id, byte[] data, uint bufferSize );

        /// <summary>
        /// IDS API: Gets the pressure switch state ( "full" or "low") for a given cylinder.
        /// </summary>
        /// <param name="id">Input parameter: the port number to read the pressure for.</param>
        /// <param name="data">Output paramter: 0 if full, 1 if low. </param>
        /// <returns>1 for success, 0 for error.</returns>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int GetPressureSwitchState( byte id, ushort* data );

        /// <summary>
        /// IDS API: Gets value indicating whether a pressure switch is present or not.
        /// </summary>
        /// <returns>1 for success, 0 for error.</returns>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int GetPressureSwitchPresence( byte id, ushort* data );

        /// <summary>
        /// IDS API: Gets value indicating whether there is a card in a given slot.
        /// </summary>
        /// <returns>0 for success, 1 for error</returns>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int GetSmartCardPresence( byte id, ushort* data );

        /// <summary>
        /// Retrieves docking station's cylinder information at a given valve ( position ID ).
        /// </summary>
        /// <param name="position">Position ID of the valve that the cylinder is installed on</param>
        /// <returns>Retrieved cylinder</returns>
        public static Cylinder ReadCard( int position )
        {
            const int CARD_TRIES = 10;
            const int CARD_BUF_SIZE = 127;
            const int CARD_RETRY_SLEEPTIME = 1000;  // 1 sec

            string funcName = "ReadCard(" + position + "): ";

            Log.Debug( funcName + "..." );

            byte[] dataBuf = new byte[ CARD_BUF_SIZE ]; // Make the buffer.
            int readTries;
            for ( readTries = 1; readTries <= CARD_TRIES; readTries++ )
            {
                if ( readTries > 1 )
                    Thread.Sleep( CARD_RETRY_SLEEPTIME );

                if ( !IsCardPresent( position ) ) // Check for the presence of a card to read.
                    return null;

                int driverErrorCode = -1;
                uint bytesRead = 0;
                int driverTries;

                for ( driverTries = 1; driverTries <= CARD_TRIES; driverTries++ )
                {
                    for ( int i = 0; i < CARD_BUF_SIZE; i++ )
                        dataBuf[ i ] = 0;  // zero out the buffer
                    bytesRead = 0;

                    if ( driverTries > 1 || readTries > 1 )
                        Thread.Sleep( CARD_RETRY_SLEEPTIME );

                    unsafe
                    {
                        // Need to ensure that we zero out bytesRead before each call because the 
                        // the call seems to add on to whatever the current value is.
                        // ReadSmartCard returns zero if successful, or a nonzero error code on failure
                        driverErrorCode = ReadSmartCard( (byte)position, dataBuf, CARD_BUF_SIZE, &bytesRead );
                    }

                    if ( driverErrorCode == 0 ) break;

                    Log.Debug( funcName + "driverErrorCode=" + driverErrorCode + ", driverTries=" + driverTries + ", bytesRead=" + bytesRead );

                }  // end-for driverTries 

                if ( driverErrorCode != 0 )  // Driver failure?
                {
                    // If we still are getting an error trying to read the card after max retries,
                    // then the docking station may be in some unstable state that it can't get out of.
                    // Time to punt, i.e., reboot the docking station.
                    // Incase the IDS is so bad off that it's going to be continually trying and failing
                    // to read the card causing it to continously reboot, we also display an LCD
                    // message so the user knows why their docking station is rebooting.
                    LCD.Display( "<a>SMARTCARD " + position + "</a><a>DRIVER ERROR</a><a>" + driverErrorCode + "</a>" );
                    //Log.Error( funcName + "Rebooting, driverErrorCode=" + driverErrorCode + " driverTries=" + CARD_TRIES );
                    Log.Error( funcName + "Driver failed, driverErrorCode=" + driverErrorCode + " driverTries=" + CARD_TRIES );
                    Thread.Sleep( 5000 );
                    //RebootDockingStation(); 
                    return null;
                }

                Log.Debug( funcName + "driver successful, driverTries=" + driverTries );

                // Find the last non-zero byte we read, then convert byte buffer to ascii string.
                if ( bytesRead > 0 )
                    while ( dataBuf[ bytesRead - 1 ] == '\0' ) bytesRead--;
                string cardXML = Encoding.ASCII.GetString( dataBuf, 0, (int)bytesRead );

                Log.Debug( funcName + "Deserializing Card contents (attempt " + readTries + "): \"" + cardXML + "\"" );

                // Deserialize (parse) the XML data into a SmartCard instance.
                // If there's something wrong with the XML (due to corrupted 
                // card, etc.), this call will throw an exception.
                ISerializer cardSerializer = new SmartCardXMLSerializer();
                SmartCard smartCard = null;
                try
                {
                    smartCard = (SmartCard)cardSerializer.Deserialize( cardXML );
                }
                catch ( Exception e )
                {
                    Log.Error( funcName + "Deserializing, tries=" + readTries, e );
                    continue; // May have misread the card (even though driver returned no error).  Retry again.
                }

                Log.Debug( funcName + "ContentCount=" + smartCard.ContentCount
                                                + " PartNumber=" + smartCard.PartNumber
                                                + " ProgramDate=" + Log.DateTimeToString(smartCard.ProgramDate) );

                // Cycle through the contents looking for a cylinder.
                for ( int i = 0; i < smartCard.ContentCount; i++ )
                {
                    object cardContent = smartCard.GetContent( i );
                    if ( cardContent is Cylinder )  // Success? Dump the cylinder's contents then return it.
                    {
                        Cylinder cylinder = (Cylinder)cardContent;
                        Log.Debug( funcName + "Deserializing successful, tries=" + readTries );
#if DEBUG
                        // FOR debugging purposes only... create a fake zero-air cylinder.
                        //if ( position == 1 )
                        //{
                        //    cylinder.PartNumber = "1810-0693";
                        //    cylinder.ExpirationDate = new DateTime( 2011, 06, 15 );
                        //    cylinder.FactoryId = "20110405-01";
                        //}
#endif
                        Log.Debug( "Cylinder Part Number:     " + cylinder.PartNumber );
                        Log.Debug( "Cylinder Expiration Date: " + Log.DateTimeToString(cylinder.ExpirationDate) );
                        Log.Debug( "Cylinder Refill Date:     " + Log.DateTimeToString(cylinder.RefillDate) );
                        Log.Debug( "Cylinder Factory ID:      " + cylinder.FactoryId );
                        return cylinder;
                    }
                    else if ( cardContent != null )
                        Log.Error( funcName + "Non-Cylinder found on card. Content is [" + cardContent.ToString() + "] tries=" + readTries );

                } // end-for smartCard.ContentCount

                // If we make it to here, we must not have found a Cylinder instance
                // within the card's content.
                Log.Error( funcName + "No Cylinder found on card, tries=" + readTries );

            } // end-for readTries

            LCD.Display( "<a>SMARTCARD " + position + "</a><a>ERROR</a>" );
            //Log.Error( funcName + "Rebooting.  Failed to successfully read smartcard, tries=" + CARD_TRIES );
            Log.Error( funcName + "Failed to successfully read smartcard " + position + ", tries=" + CARD_TRIES );
            Thread.Sleep( 5000 );
            //RebootDockingStation(); 
            return null;
        }

        /// <summary>
        /// Writes the cylinder information into the iGas card on the given position ID.
        /// </summary>
        /// <param name="position">The position ID of the valve</param>
        /// <param name="cylinder">The cylinder inforatmion</param>
        public static void WriteCard(int position, Cylinder cylinder)
        {
            const int CARD_TRIES = 10;
            const int CARD_BUF_SIZE = 127;
            const int CARD_RETRY_SLEEPTIME = 1000;  // 1 sec

            string funcName = "WriteCard(" + position + "): ";

            Log.Debug(funcName + "...");

            SmartCard smartCard = new SmartCard();
            smartCard.ProgramDate = DateTime.Now;
            smartCard.Add(cylinder, new CylinderXMLSerializer());

            SmartCardXMLSerializer smartCardXMLSerializer = new SmartCardXMLSerializer();
            string smartCardXML = smartCardXMLSerializer.Serialize(smartCard);

            if (smartCardXML.Length > CARD_BUF_SIZE)
                throw new Exception("New data to write in smart card is greater than smart Card EPROM size");

            byte[] data = new byte[CARD_BUF_SIZE]; // Make the buffer.

            for (int i = 0; i < CARD_BUF_SIZE; i++)
            {
                if (i < smartCardXML.Length)
                    data[i] = Convert.ToByte(smartCardXML[i]);
                else
                    data[i] = 0; // nullify the remaining CARD_BUF_SIZE
            }

            int driverErrorCode = -1;

            for (int retry = 1; retry <= CARD_TRIES; retry++)
            {
                if (retry > 1)
                    Thread.Sleep(CARD_RETRY_SLEEPTIME);

                if (!IsCardPresent(position)) // Check for the presence of a card to read.
                {
                    throw new Exception("iGas card is NOT present at postion : " + position);
                }

                // ReadSmartCard returns zero if successful, or a nonzero error code on failure
                unsafe
                {
                    driverErrorCode = WriteSmartCard((byte)position, data, CARD_BUF_SIZE);
                }

                if (driverErrorCode == 0) 
                    break;

                Log.Debug(funcName + " reTry = " + retry);

            } // end-for retry

            if (driverErrorCode != 0)
            {
                throw new Exception("Failed to write smartcard at postion : " + position + " after " + CARD_TRIES + "retries");
            }
            return;
        }


        /// <summary>
        /// Returns a value indicating whether there is smart card present in a given slot.
        /// </summary>
        /// <param name="position">The card slot number</param>
        /// <returns>True if there is a card in the slot, 0 if not.</returns>
        public static bool IsCardPresent( int position )
        {
            ushort data = 0;
            int error = 0;

            for ( int attempt = 1; attempt <= 10; attempt++ )
            {
                unsafe
                {
                    error = GetSmartCardPresence( Convert.ToByte( position ), &data ); // returns 1 on error, 0 on success
                }

                if ( error == 0 ) // success?
                    break;

                Log.Error( string.Format( "Error calling GetSmartCardPresence({0}), attempt {1}, GetLastError={2}", position, attempt, WinCeApi.GetLastError() ) );
                Thread.Sleep( 100 );
            }

            if ( error != 0 ) // error?
            {
                int lastError = WinCeApi.GetLastError();
                Log.Error( string.Format( "Failure in GetSmartCardPresence({0}), GetLastError={1}", position, lastError ) );
                throw new DeviceDriverException( DeviceHardware.SmartCardPresence, position, lastError );
            }

            return data != 0;
        }

        /// <summary>
        /// Checks to see if a pressure switch is present.
        /// </summary>
        /// <param name="position">The position of the switch to check</param>
        /// <returns>True, if there is a pressure switch present</returns>
        public static bool IsPressureSwitchPresent( int position )
        {
            ushort data = 0;
            int success = 0;

            for ( int attempt = 1; attempt <= 10; attempt++ )
            {
                unsafe
                {
                    success = GetPressureSwitchPresence( Convert.ToByte( position ), &data ); // returns 1 on success, 0 on error
                }

                if ( success != 0 ) // success?
                    break;

                Log.Error( string.Format( "Error calling GetPressureSwitchPresence({0}), attempt {1}, GetLastError={2}", position, attempt, WinCeApi.GetLastError() ) );
                Thread.Sleep( 100 );
            }

            if ( success == 0 ) // error ?
            {
                int lastError = WinCeApi.GetLastError();
                Log.Error( string.Format( "Failure in GetPressureSwitchPresence({0}), GetLastError={1}", position, lastError ) );
                throw new DeviceDriverException( DeviceHardware.PressureSwitchPresence, position, lastError );
            }

            return data != 0;
        }


        /// <summary>
        /// Checks a given pressure switch and returns.
        /// </summary>
        /// <param name="position">The position of the switch to check</param>
        /// <returns>True, if the pressure is good</returns>
        public static bool CheckPressureSwitch( int position )
        {
            if ( !IsPressureSwitchPresent( position ) )
                return true;

            int success = 0;
            ushort data = 0;

            for ( int attempt = 1; attempt <= 10; attempt++ )
            {
                unsafe
                {
                    success = GetPressureSwitchState( Convert.ToByte( position ), &data ); // returns 1 on success, 0 on error
                }

                if ( success != 0 ) // success?
                    break;

                Log.Error( string.Format( "Error calling GetPressureSwitchState({0}), attempt {1}, GetLastError={2}", position, attempt, WinCeApi.GetLastError() ) );
                Thread.Sleep( 100 );
            }

            if ( success == 0 ) // error?
            {
                int lastError = WinCeApi.GetLastError();
                Log.Error( string.Format( "Failure in GetPressureSwitchState({0}), GetLastError={1}", position, lastError ) );
                throw new DeviceDriverException( DeviceHardware.PressureSwitchState, position, lastError );
            }

            return data == 0;
        }


    } // end-class
}
