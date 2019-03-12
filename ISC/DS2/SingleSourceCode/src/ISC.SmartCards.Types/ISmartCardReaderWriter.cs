using System;

namespace ISC.SmartCards.Types
{
	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// The interface that all SmartCard ReaderWriter devices must implement.
	/// </summary>
	public interface ISmartCardReaderWriter
	{

		#region Methods

		/// <summary>
		/// The method for writing data to the device.
		/// </summary>
		/// <param name="data">Data to write to the device</param>
		/// <exception cref="DeviceNotConnectedException">
		///		When the device is not connected.
		/// </exception>
		/// <exception cref="CardNotPresentException">
		///		When the smart card is not inserted.
		/// </exception>
		/// <exception cref="CardWriteException">
		///		When there is a failure to write the data.
		/// </exception>
		void Write( byte[] data );

		/// <summary>
		/// The method for reading data from the device.
		/// </summary>
		/// <param name="numberOfBytes">
		/// The number of bytes to return.
		/// </param>
		/// <returns>Data read from the device</returns>
		/// <exception cref="DeviceNotConnectedException">
		///		When the device is not connected.
		/// </exception>
		/// <exception cref="CardNotPresentException">
		///		When the smart card is not inserted.
		/// </exception>
		/// <exception cref="CardReadException">
		///		When there is a failure to read the data.
		/// </exception>
		byte[] Read( int numberOfBytes );

		/// <summary>
		/// The method for connecting to the comm port.
		/// </summary>
		/// <param name="commPort">
		/// The comm port.
		/// </param>
		void Connect( string commPort );

		#endregion

		#region Properties
		
		/// <summary>
		/// Property that indicates whether the device is connected to
		/// the computer.
		/// </summary>
		bool IsConnected
		{
			get;
		}

		/// <summary>
		/// Property that indicates whether a smart card is present.
		/// </summary>
		bool IsCardPresent
		{
			get;
		}

		#endregion

	}
	
	#region Exceptions

	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// The exception that is thrown when the device is not connected.
	/// </summary>
	public class DeviceNotConnectedException : ApplicationException
	{
		/// <summary>
		/// Intializes an instance of the DeviceNotConnectedException
		/// by setting the exception message.
		/// </summary>
		/// <param name="e">Originating exception</param>
		public DeviceNotConnectedException( Exception e ) : base( "Device is not connected!" )
		{
			// Do nothing
		}

		/// <summary>
		/// Intializes an instance of the DeviceNotConnectedException by 
		/// setting the exception message.
		/// </summary>
		public DeviceNotConnectedException() : base( "Device is not connected!" )
		{
			// Do nothing
		}
	}

	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// The exception that is thrown when the SmartCard is not inserted.
	/// </summary>
	public class CardNotPresentException : ApplicationException
	{
		/// <summary>
		/// Intializes an instance of the CardNotPresentException by setting 
		/// the exception message.
		/// </summary>
		/// <param name="e">Originating exception</param>
		public CardNotPresentException( Exception e ) : base( "Card is not inserted!" )
		{
			// Do nothing
		}

		/// <summary>
		/// Intializes an instance of the CardNotPresentException by setting 
		/// the exception message.
		/// </summary>
		public CardNotPresentException() : base( "Card is not inserted!" )
		{
			// Do nothing
		}
	}

	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// The exception that is thrown when there is an error reading
    /// from the device.
	/// </summary>
	public class FailedToReadCardException : ApplicationException
	{
		/// <summary>
		/// Intializes an instance of the FailedToReadCardException by setting 
		/// the exception message.
		/// </summary>
		/// <param name="e">Originating exception</param>
		public FailedToReadCardException( Exception e ) : base( "Could not read data from the card!" )
		{
			// Do nothing
		}

		/// <summary>
		/// Intializes an instance of the CardReadException by setting 
		/// the exception message.
		/// </summary>
		public FailedToReadCardException() : base( "Could not read data from the card!" )
		{
			// Do nothing
		}
	}

	#endregion

}
