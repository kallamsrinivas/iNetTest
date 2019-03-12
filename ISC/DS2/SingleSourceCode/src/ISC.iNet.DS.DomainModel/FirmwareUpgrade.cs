using System;

using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
    public class FirmwareUpgrade
    {
        string _version;
        string _equipmentCode;
        byte[] _firmware;
        byte[] _md5Hash;
        string _equipmentSubTypeCode;
        string _equipmentFullCode;

         /// <summary>
        /// This default constructor is intended to create an instance that indicates firmware
        /// is not available.  (firmware and hash both return null.)
        /// </summary>
        public FirmwareUpgrade()
        {
        }

        public FirmwareUpgrade( string equipmentCode, string version, byte[] firmware, byte[] md5Hash, string equipmentSubTypeCode, string equipmentFullCode )
        {
            _firmware = firmware;
            _md5Hash = md5Hash;
            _version = version;
            _equipmentCode = equipmentCode;
            _equipmentSubTypeCode = equipmentSubTypeCode;
            _equipmentFullCode = equipmentFullCode;
        }

        /// <summary>
        /// The software version 
        /// </summary>
        public string Version
        {
            get { return _version; }
            set { _version = value; }
        }

        /// <summary>
        /// A byte array containing the actual firmware.
        /// </summary>
        public byte[] Firmware
        {
            get { return _firmware;  }
        }

        /// <summary>
        /// The device type that the version is intended for. e.g MX6 or GBPRO, etc.
        /// </summary>
        public string EquipmentCode
        {
            get { return _equipmentCode; }
        }

        public byte[] MD5Hash
        {
            get { return _md5Hash; }
        }

        /// <summary>
        /// The Equipment Sub Type Code e.g MX4 Ventis, Ventis LS.
        /// </summary>
        public string EquipmentSubTypeCode
        {
            get { return _equipmentSubTypeCode; }
            set { _equipmentSubTypeCode = value; }
        }     

        /// <summary>
        /// The Equipment Full Code e.g MX4, VPRO along with its sub type if any etc.
        /// </summary>
        public string EquipmentFullCode
        {
            get { return _equipmentFullCode; }
            set { _equipmentFullCode = value; }
        }      

        /// <summary>
        /// Returns the MD5Hash value as a hexidecimal string.
        /// </summary>
        /// <param name="md5Hash"></param>
        /// <returns></returns>
        static public string MD5HashToString( byte[] md5Hash )
        {
            string hash = string.Empty;

            if ( md5Hash == null ) return hash;

            foreach ( byte b in md5Hash )
                hash += b.ToString( "x2" );

            return hash;
        }


        /// <summary>
        /// Returns tru of the passed-in MD5 hash value matches this instance's
        /// MD5Hash value. 
        /// </summary>
        /// <param name="md5hash"></param>
        /// <returns></returns>
        public bool CompareMD5Hash( byte[] md5hash )
        {
            return CompareMD5Hash( this.MD5Hash, md5hash );
        }


        /// <summary>
        /// Returns true if the two specified arrays represent the same 
        /// MD5 hash value. (Basically, this routine just compares the two arrays).
        /// </summary>
        /// <param name="md5hash1"></param>
        /// <param name="md5hash2"></param>
        /// <returns></returns>
        static public bool CompareMD5Hash( byte[] md5hash1, byte[] md5hash2 )
        {
            if ( md5hash1 == null || md5hash2 == null )
                return false;

            if ( md5hash1.Length != md5hash2.Length )
                return false;

            for ( int i = 0; i < md5hash1.Length; i++ )
            {
                if ( md5hash1[ i ] != md5hash2[ i ] )
                    return false;
            }
            return true;
        }


    }  // end-class

}  // end-namespace
