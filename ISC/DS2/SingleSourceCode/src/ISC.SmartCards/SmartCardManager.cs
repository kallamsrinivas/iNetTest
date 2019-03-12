using System;
using System.Text;
using ISC.SmartCards.Types;
using ISC.iNet.DS.DomainModel;

using System.Diagnostics;

namespace ISC.SmartCards
{
	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// The class that controls reading and writing SmartCards in the device.
	/// </summary>
	public class SmartCardManager
	{

		#region Fields

	    /// <summary>
	    /// The SmartCardReaderWriter to use for information storage
	    /// and retrieval.
	    /// </summary>
		private ISmartCardReaderWriter _readerWriter;

		#endregion

		#region Constructors

		/// <summary>
		/// Initialize the object.
		/// </summar>
		public SmartCardManager( ISmartCardReaderWriter readerWriter )
		{
			_readerWriter = readerWriter;
		}

		#endregion

		#region Properties

		///<summary>
		///Provide access to the object representing the device.
		///</summary>
		public ISmartCardReaderWriter ReaderWriter
		{
			get
			{
				return _readerWriter;
			}
			set
			{
				_readerWriter = value;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Persists the serialized SmartCard to the _readerWriter;
		/// </summary>
		/// <param name="card">
		/// The SmartCard to persist to the device.
		/// </param>
		/// <exception cref="DeviceNotConnectedException">
		///		When the device is not connected.
		/// </exception>
		/// <exception cref="CardNotPresentException">
		///		When the smart card is not inserted.
		/// </exception>
		/// <exception cref="CardWriteException">
		///		When there is a failure to write the data.
		/// </exception>
		public void Write( SmartCard card  )
		{
			ISerializer cardSerial;
			string cardXML;
			byte[] theData;

			cardSerial = new SmartCardXMLSerializer();
			cardXML = cardSerial.Serialize( card );

			// Transform the data into a byte array.
			theData = new byte[ cardXML.Length ];

			for ( int n = 0 ; n < theData.Length ; n++ )
			{

				// Or just extract the low bits.
				theData[ n ] = Convert.ToByte( cardXML[ n ] );
			}

			_readerWriter.Write( theData );
		}

		/// <summary>
		/// Deserializes the supplied SmartCard from the _readerWriter.
		/// </summary>
		/// <param name="numberOfBytes">
		/// The number of bytes to return to the calling application.
		/// Specify 0 to return the maximum number of bytes stored on the device.
		/// </param>
		/// <returns>The SmartCard read from the device.</returns>
		/// <exception cref="DeviceNotConnectedException">
		///		When the device is not connected.
		/// </exception>
		/// <exception cref="CardNotPresentException">
		///		When the smart card is not inserted.
		/// </exception>
		/// <exception cref="CardReadException">
		///		When there is a failure to read the data.
		/// </exception>
		public SmartCard Read( int numberOfBytes )
		{
			byte[] theData;
			ISerializer cardSerial;
			SmartCard card;

			theData = _readerWriter.Read( numberOfBytes );

			// Deserialize the data.
			cardSerial = new SmartCardXMLSerializer();

			card = ( SmartCard ) cardSerial.Deserialize( Encoding.ASCII.GetString( theData , 0 , theData.Length ) );

			return card;
		}

		/// <summary>
		/// Reads a specified number of bytes from the SmartCard.
		/// </summary>
		/// <param name="numberOfBytes">Number of bytes to read from the SmartCard.</param>
		/// <returns>An array of bytes representing the values stored on the SmartCard.</returns>
		public byte[] ReadBytes( int numberOfBytes )
		{

			return _readerWriter.Read( numberOfBytes );
		}

		#endregion

	}

#if DEBUG
	#region TextClass

	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// The unit test class for the SmartCardManager class.
	/// </summary>
	public class SmartCardManagerTester 
	{
	    /// <summary
	    /// The unit tests.
	    /// </summary>
		public static void Test ()
		{
			try
			{
				// TODO
			}
			catch ( Exception )
			{
			}
		}
	}

	#endregion
#endif // DEBUG
}
