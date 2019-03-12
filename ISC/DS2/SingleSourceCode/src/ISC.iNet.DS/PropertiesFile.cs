using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS
{
    /// <summary>
    /// Implements a file that is used to store properties and configurable parameters.
    /// Each line stores a single property, with the format of each line being "propertyName=value".
    /// e.g. "serialNumber=123456".
    /// </summary>
    /// <remarks>
    /// Properties files are commonly used in Java applications.  We've implemented and use
    /// them here due to their simplicity over the XML that used to do the same thing in DS2 but
    /// was overly complicated and hard to maintain.
    /// </remarks>
    internal class PropertiesFile
    {
        private string _fileName;
        private Dictionary<string, string> _properties;

        internal PropertiesFile( string fileName )
        {
            _fileName = fileName;
            _properties = new Dictionary<string, string>();
        }

        internal PropertiesFile( string fileName, Dictionary<string, string> properties )
        {
            _fileName = fileName;
            _properties = new Dictionary<string, string>( properties );
        }

        /// <summary>
        /// An empty string is returned if the attribute doesn't exist.
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns>A null is returned if the attribute doesn't exist.</returns>
        internal string this[ string attribute ]
        {
            get
            {
                string value = null;
                return _properties.TryGetValue( attribute, out value ) ? value : null;
            }
            set
            {
                _properties[ attribute ] = value;
            }
        }

        internal IEnumerable<string> Attributes
        {
            get
            {
                return _properties.Keys;
            }           
        }
        internal void Load()
        {
            _properties.Clear();

            lock ( Controller.NandPersistenceLock )
            {
                using ( StreamReader sr = File.OpenText( _fileName ) )
                {
                    string line;
                    while ( ( line = sr.ReadLine() ) != null )
                    {
                        // Assuming each line in the file has the format "key=value",
                        // load up all the key/value pairs into the dictionary.
                        int equalsIndex = line.IndexOf( "=" );
                        string key = line.Substring( 0, equalsIndex );
                        string value = line.Substring( equalsIndex + 1 );

                        _properties[ key ] = value;
                    }
                }
            }
        }

        internal void Save()
        {
            lock ( Controller.NandPersistenceLock )
            {
                using ( StreamWriter sw = File.CreateText( _fileName ) )
                {
                    // For each key/value pair in the dictionary, write a line to
                    // the file with the format "key=value",

                    foreach ( string key in _properties.Keys )
                    {
                        string value = _properties[ key ];

                        sw.WriteLine( key + "=" + value );
                    }
                }

                Thread.Sleep( 1000 ); // Don't trust the file system to return to us before save is fully committed.

                Verify();
            }
        }

        private void Verify()
        {
            lock ( Controller.NandPersistenceLock )
            {
                if ( !File.Exists( _fileName ) )
                    throw new ConfigurationException( "PropertiesFile.Verify: \"" + _fileName + "\" not found." );

                PropertiesFile verificationFile = new PropertiesFile( _fileName );
                verificationFile.Load();

                // Verify that all all expected attributes exist in the actual file.
                foreach ( string attribute in this.Attributes )
                {
                    string expectedValue = this[ attribute ];

                    string actualValue = verificationFile[ attribute ];

                    if ( actualValue != expectedValue )
                        throw new ConfigurationException( string.Format( "Verify failed: Mismatch for attribute {0}. expected=\"{1}\", actual=\"{2}\"",
                            attribute, expectedValue, actualValue ) );
                }

                // Now do the reverse: Verify that the actual file contains nothing
                // not in the expected file.

                foreach ( string attribute in verificationFile.Attributes )
                {
                    string expectedValue = verificationFile[ attribute ];

                    string actualValue = this[ attribute ];

                    if ( actualValue != expectedValue )
                        throw new ConfigurationException( string.Format( "Verify failed: Mismatch for attribute {0}. expected=\"{1}\", actual=\"{2}\"",
                            attribute, expectedValue, actualValue ) );
                }
            }
        }

        internal static void Delete( string fileName )
        {
            lock ( Controller.NandPersistenceLock )
            {
                if ( File.Exists( fileName ) )
                {
                    Log.Debug( string.Format( "DELETING FILE \"{0}\"", fileName ) );

                    try
                    {
                        File.Delete( fileName );
                        Thread.Sleep( 1000 ); // I don't trust the file system to return before deletion is fully committed.
                    }
                    catch ( Exception e )
                    {
                        Log.Error( "Error deleting " + fileName, e );
                    }
                }
                else
                    Log.Debug( string.Format( "File \"{0}\" does not appear to exist.  Nothing to delete.", fileName ) );
            }
        }
    }
}
