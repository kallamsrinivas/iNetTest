using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS
{

    /// <summary>
    /// Determines whether the docking station is in "service mode" or not.
    /// Service mode is enabled whenever a USB flash drive is inserted that
    /// contains a special file.  It is also automatically enabled whenever
    /// the docking station is not serialized.
    /// </summary>
    internal class ServiceMode
    {
        private static readonly string FILE_PATH = Controller.USB_DRIVE_PATH + "$erviceMode.dat";

        private const string CONTENTS_ASCII = "2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, Industrial Scientific Corporation iNet Docking Station Service Mode, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97, 101";

        private static readonly byte[] CONTENTS_BINARY;

        private static RijndaelManaged _rijndael;

        static ServiceMode()
        {
            // We use this same initialization string for both they key and IV.
            const string initString = "*iNet*iNet*iNet*";

            // The point here is not to completely encrypt the file contents and
            // make it 'uncrackable'.
            // We're merely attempting to create a file that has known contents, but
            // that is not easily creatable by an end-user. (i.e., we don't want to
            // just have a simple text file since some customers would likely learn after
            // a time that to enable service mode, they just need to use something
            // like Notepad to create the file.
            //
            // We use Rijndael encryption since it is completely implemented in managed
            // code.  The other .Net encryption classes require that WinCE's native-code 
            // cryptography components are on the device.
            _rijndael = new RijndaelManaged();
            _rijndael.KeySize = 128;
            _rijndael.Key = Encoding.ASCII.GetBytes( initString ); // Need a 128-bit key, so initString needs to be 16-chars (128 bits)
            _rijndael.IV = Encoding.ASCII.GetBytes( initString );  // With RijndaelManaged, the IV must be 16 bytes.
            // Encrypt the string to an array of bytes.
            CONTENTS_BINARY = EncryptStringToBytes_AES( CONTENTS_ASCII );

            string s = DecryptStringFromBytes_AES( CONTENTS_BINARY );
        }

        internal ServiceMode()
        {

        }

        internal bool IsServiceMode()
        {
            Log.Debug( string.Format( "IsServiceMode: Looking for \"{0}\".", FILE_PATH ) );

            try
            {
                if ( !File.Exists( FILE_PATH ) )
                {
                    Log.Debug( string.Format( "IsServiceMode: False.  \"{0}\" not found.", FILE_PATH ) );
                    return false;
                }

                using ( FileStream fs = File.OpenRead( FILE_PATH ) )
                {
                    using ( BinaryReader br = new BinaryReader( fs ) )
                    {
                        byte[] encryptedContents = br.ReadBytes( CONTENTS_BINARY.Length );

                        // No need to decrypt if the number of bytes in the file is less than what expect.
                        if ( encryptedContents.Length != CONTENTS_BINARY.Length )
                        {
                            Log.Warning( string.Format( "IsServiceMode: False.  Size of \"{0}\" is wrong.", FILE_PATH ) );
                            return false;
                        }

                        string contentsAscii = DecryptStringFromBytes_AES( encryptedContents );

                        if ( contentsAscii != CONTENTS_ASCII )
                        {
                            Log.Warning( string.Format( "IsServiceMode: False.  Contents of \"{0}\" are illegal.", FILE_PATH ) );
                            return false;
                        }
                    }
                }
            }
            catch ( Exception e )
            {
                Log.Error( "IsServiceMode", e );
                return false;
            }

            Log.Debug( "IsServiceMode: True" );

            return true;
        }


        internal void CreateServiceModeFile()
        {
            Log.Info( string.Format( "Creating service mode file \"{0}\".", FILE_PATH ) );

            // If the file already exists, clear the read-only bit so we can overwrite it.
            try
            {
                if ( File.Exists( FILE_PATH ) )
                {
                    FileInfo fi = new FileInfo( FILE_PATH );
                    fi.Attributes &= ~FileAttributes.ReadOnly;
                }
            }
            catch ( Exception e )
            {
                Log.Error( string.Format( "ERROR clearing read-only attribute on \"{0}\".", FILE_PATH ), e );
                return;
            }

            try
            {
                using ( FileStream fs = File.Create( FILE_PATH ) )
                {
                    using ( BinaryWriter bw = new BinaryWriter( fs ) )
                    {
                        bw.Write( CONTENTS_BINARY );
                    }
                }
            }
            catch ( Exception e )
            {
                Log.Error( string.Format( "ERROR creating file \"{0}\"", FILE_PATH ), e );
                return;
            }

            try
            {
                FileInfo fi = new FileInfo( FILE_PATH );
                fi.Attributes |= FileAttributes.ReadOnly; // | FileAttributes.Hidden;
            }
            catch ( Exception e )
            {
                Log.Error( string.Format( "ERROR setting attributes on file \"{0}\"", FILE_PATH ), e );
                return;
            }

            // Wait a moment for flash card to finish flushing.  We probably don't need to do this,
            // but ya never know.
            Thread.Sleep( 1000 );

            Log.Info( string.Format( "SUCCESS!  Service mode file \"{0}\" created.", FILE_PATH ) );
        }

        private static void WriteRepeatedByte( BinaryWriter bw, byte b, int repeat )
        {
            for ( int i = 1; i <= repeat; i++ )
                bw.Write( b );
        }



        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// Adapted from example code shown on the MSDN docoumentation page for the RijndaelManaged class 
        /// </remarks>
        /// <param name="plainText"></param>
        /// <param name="Key"></param>
        /// <param name="IV"></param>
        /// <returns></returns>
        private static byte[] EncryptStringToBytes_AES( string plainText )
        {
            // Declare the streams used to encrypt to an in memory array of bytes.
            MemoryStream msEncrypt = null;
            CryptoStream csEncrypt = null;
            StreamWriter swEncrypt = null;

            try
            {
                // Create a decrytor to perform the stream transform.
                ICryptoTransform encryptor = _rijndael.CreateEncryptor( _rijndael.Key, _rijndael.IV );

                // Create the streams used for encryption.
                msEncrypt = new MemoryStream();
                csEncrypt = new CryptoStream( msEncrypt, encryptor, CryptoStreamMode.Write );
                swEncrypt = new StreamWriter( csEncrypt );

                swEncrypt.Write( plainText ); // Write all data to the stream.
            }
            finally // Clean things up.
            {
                if ( swEncrypt != null ) swEncrypt.Close();
                if ( csEncrypt != null ) csEncrypt.Close();
                if ( msEncrypt != null ) msEncrypt.Close();
            }

            return msEncrypt.ToArray();  // Return the encrypted bytes from the memory stream.
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// Adapted from example code on the MSDN docoumentation page for the RijndaelManaged class 
        /// </remarks>
        /// <param name="cipherText"></param>
        /// <param name="Key"></param>
        /// <param name="IV"></param>
        /// <returns></returns>
        private static string DecryptStringFromBytes_AES( byte[] cipherText )
        {
            // Declare the streams used to decrypt to an in memory array of bytes.
            MemoryStream msDecrypt = null;
            CryptoStream csDecrypt = null;
            StreamReader srDecrypt = null;

            try
            {
                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = _rijndael.CreateDecryptor( _rijndael.Key, _rijndael.IV );

                // Create the streams used for decryption.
                msDecrypt = new MemoryStream( cipherText );
                csDecrypt = new CryptoStream( msDecrypt, decryptor, CryptoStreamMode.Read );
                srDecrypt = new StreamReader( csDecrypt );

                // Read the decrypted bytes from the decrypting stream and place them in a string.
                return srDecrypt.ReadToEnd();
            }
            finally // Clean things up.
            {
                if ( srDecrypt != null ) srDecrypt.Close();
                if ( csDecrypt != null ) csDecrypt.Close();
                if ( msDecrypt != null ) msDecrypt.Close();
            }
        }

    }

}
