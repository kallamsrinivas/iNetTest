using System;
using System.Collections.Generic;
using System.Diagnostics;



namespace ISC.SmartCards.Types
{	
	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides data encapsulation for a SmartCard's information.
	/// </summary>
	public class SmartCard : ICloneable
	{
		#region Internal Classes

		///////////////////////////////////////////////////////////////////////
		/// <summary>
		/// Provides data encapsulation for a SmartCard's contents.
		/// </summary>
		private class ContentEntry
		{

			#region Fields
			
			/// <summary>
			/// The object held in the entry.
			/// </summary>
			private object _content;

			/// <summary>
			/// The serializer that can serialize the content.
			/// </summary>
			private ISerializer _serializer;
			
			#endregion

			#region Properties

			/// <summary>
			/// Returns the entry's object contents.
			/// </summary>
			public object Content
			{
				get
				{
					return _content;
				}
				set
				{
					_content = value;
				}
			}

			/// <summary>
			/// Returns the serializer for the object.
			/// </summary>
			public ISerializer Serializer
			{
				get
				{
					return _serializer;
				}
				set
				{
					_serializer = value;
				}
			}
			
			#endregion

		}

		#endregion
				
		#region Fields

		/// <summary>
		/// The time before which, no SmartCards were made.
		/// </summary>
		private const string DATE_MIN = "1/1/2003";

		/// <summary>
		/// The part number of the smart card.
		/// </summary>
		private string _partNumber = "1710-9729";

		/// <summary>
		/// The date the card had data written to it.
		/// </summary>
		private DateTime _programDate;

		/// <summary>
		/// The objects and their serializers held in the card's storage.
		/// </summary>
		private List<ContentEntry> _contents;

		#endregion
		
		#region Constructors

		/// <summary>
		/// Initializes a new instance of SmartCard class. It receives a
		/// data it could not have been programmed on.
		/// </summary>
		public SmartCard()
		{
			Initialize();
		}

		#endregion

		#region Properties

		/// <summary>
		/// The number of objects stored in the SmartCard.
		/// </summary>
		public int ContentCount
		{
			get
			{
				return _contents.Count;
			}
		}

		/// <summary>
		/// Has the SmartCard been programmed already?
		/// </summary>
		public bool IsProgrammed
		{
			get
			{
				return ( _programDate > DateTime.Parse( DATE_MIN ) );
			}
		}

		/// <summary>
		/// Gets or sets the SmartCard's part number.
		/// </summary>
		public string PartNumber
		{
			get
			{
				if ( _partNumber == null )
				{
					_partNumber = string.Empty;
				}

				return _partNumber;
			}
			set
			{
				if ( value == null )
				{
					_partNumber = null;
				}
				else
				{
					_partNumber = value.Trim().ToUpper();
				}
			}
		}

		/// <summary>
		/// Gets or sets the SmartCard's program date.
		/// </summary>
		public DateTime ProgramDate
		{
			get
			{
				return _programDate;
			}
			set
			{
				_programDate = value;
			}
		}
		
		#endregion

		#region Methods

		/// <summary>
		/// Initializes the local variables. It is called by any constructors.
		/// </summary>
		public void Initialize()
		{
			// Provide an invalid date for the program date.
			_programDate = DateTime.Parse( DATE_MIN );

			// Make the array list for holding the contents.
            _contents = new List<ContentEntry>();
		}

		/// <summary>
		/// This method checks the value of the properties that need to be
		/// validated and throws an exception if any of them are not valid.
		/// </summary>
		/// <exception cref="InvalidSmartCardPropertyExpception">
		/// Thrown when the program date is invalid.
		/// </exception>
		public void ValidateProperties()
		{
			// Make sure smartcard program time is valid.
			if ( ( ProgramDate != DateTime.MinValue ) &&
				 ( ProgramDate < DateTime.Parse( DATE_MIN ) ) )
			{
				throw new InvalidSmartCardPropertyException( "ProgramDate" );
			}
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a
		/// SmartCard object.
		/// </summary>
		/// <returns>Cloned SmartCard object</returns>
		public virtual object Clone()
		{
			SmartCard smartCard;

			// Allocate the new SmartCard.
			smartCard = new SmartCard();

			// Grab the properties.
			smartCard.PartNumber = PartNumber;
			smartCard.ProgramDate = ProgramDate;

			// Grab all the objects and serializers.
			// Note: Shallow copy.
			for ( int i = 0 ; i < _contents.Count ; i++ )
			{
				smartCard._contents.Add( _contents[ i ] );
			}

			return smartCard;
		}

		/// <summary>
		/// Adds a new object and its serializer to the contents of
		/// this SmartCard.
		/// </summary>
		/// <param name="theObject">The object to the contents.</param>
		/// <param name="theSerializer">The serializer for the object.</param>
		/// <returns>The index of the object in the contents.</returns>
		/// <exception cref="InvalidSmartCardPropertyException">
		/// If either theObject or theSerializer is null.
		/// </exception>
		public int Add( object theObject , ISerializer theSerializer )
		{
			ContentEntry newEntry;

			// Check for invalid arguments
			if ( ( theObject == null ) || ( theSerializer == null ) )
				throw new InvalidSmartCardPropertyException( "Contents" );

			// Create the new entry.
			newEntry = new ContentEntry();
			newEntry.Content = theObject;
			newEntry.Serializer = theSerializer;

			_contents.Add( newEntry );

            // Return the index of the addition.
            return _contents.Count - 1;
		}

		/// <summary>
		/// Removes the indicated object from the SmartCard's contents.
		/// </summary>
		/// <param name="index">The index to remove.</param>
		public void RemoveAt( int index )
		{
			_contents.RemoveAt( index );
		}

		/// <summary>
		/// Clears all of the SmartCard's contents.
		/// </summary>
		public void Clear()
		{
			_contents.Clear();
		}

		/// <summary>
		/// Gets the object in the SmartCard's contents at the index.
		/// </summary>
		/// <param name="index">The index of the item to retrieve.</param>
		/// <returns>The object at that index.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// If the index is too large. 
		/// </exception>
		public object GetContent( int index )
		{
			return ( ( ContentEntry ) _contents[ index ] ).Content;
		}

		/// <summary>
		/// Gets the serializer in the SmartCard's contents at the index.
		/// </summary>
		/// <param name="index">The index of the serializer to retrieve.</param>
		/// <returns>The serializer at that index.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// If the index is too large. 
		/// </exception>
		public ISerializer GetSerializer( int index )
		{
			return ( ( ContentEntry ) _contents[ index ] ).Serializer;
		}

		#endregion

	}

	#region Exceptions

	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// The exception that is thrown when a property of SmartCard class 
	/// has an invalid value.
	/// </summary>
	public class InvalidSmartCardPropertyException : ApplicationException
	{
		/// <summary>
		/// Initializes a new instance of the InvalidSmartCardPropertyException
		/// class by setting the exception message.
		/// </summary>
		public InvalidSmartCardPropertyException() :  base( "Invalid smartcard property value!" )
		{
			// Do nothing
		}

		/// <summary>
		/// Initializes a new instance of the InvalidSmartCardPropertyException
		/// class by setting the exception message and name of the invalid
	    /// property.
		/// </summary>
		/// <param name="propertyName">Property name</param>
		public InvalidSmartCardPropertyException( string propertyName ) : 
			base( "Invalid smartcard property value! (" + propertyName + ")" )
		{
			// Do nothing
		}
	}

	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// The exception that is thrown when a SmartCard object is invalid.
	/// </summary>
	public class InvalidSmartCardException : ApplicationException
	{
		/// <summary>
		/// Initializes a new instance of the InvalidSmartCardException class
		/// by setting the exception message and name of the invalid property.
		/// </summary>
		/// <param name="e">The exception to incapsulate.</param>
		public InvalidSmartCardException( Exception e ) : base( "Invalid SmartCard!" )
		{
			// Do nothing
		}

		/// <summary>
		/// Initializes a new instance of the InvalidSmartCardException class
		/// by setting the exception message.
		/// </summary>
		public InvalidSmartCardException() : base( "Invalid SmartCard!" )
		{
			// Do nothing
		}

	}

	#endregion

#if DEBUG
	#region TestClass

	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Handles unit testing of the SmartCard class.
	/// </summary>
	public class SmartCardTester
	{
	    /// <summary>
      	/// The unit test of the SmartCard class.
    	/// </summary>
		public static void Test()
		{
			try 
			{
				SmartCard card , cloneCard;
				
				card = new SmartCard();

				// Do the assertions.
				Debug.Assert( card.IsProgrammed == false ,
					"SmartCard begins programmed." );
				Debug.Assert( card.ProgramDate.CompareTo( DateTime.Parse( "1/1/2003" ) ) == 0 ,
					"SmartCard has incorrect starting date." );
				Debug.Assert( card.ContentCount == 0 ,
					"SmartCard has contents when created." );
				
				Debug.Assert( card.PartNumber.CompareTo( string.Empty ) == 0 ,
					"SmartCard has a PartNumber when created." );

				// TODO: validate the content methods.
	
				card.PartNumber = "1002-3440";
				Debug.Assert( card.PartNumber.CompareTo( "1002-3440" ) == 0 ,
					"SmartCard cannot assign/return PartNumber correctly." );
	
				card.ProgramDate = DateTime.Parse( "10/23/1969" );
				Debug.Assert( card.ProgramDate.CompareTo( DateTime.Parse( "10/23/1969" ) ) == 0 ,
					"SmartCard cannot assign/return ProgramDate correctly." );

				// Test ValidateProperties, the properties for the object
				// are already invalid.
				try 
				{
					card.ValidateProperties();
					Debug.Assert( false , "SmartCard is not validating dates correctly." );
				}
				catch ( InvalidSmartCardPropertyException )
				{
				}
	
				cloneCard = ( SmartCard ) card.Clone();
				Debug.Assert( card.ProgramDate.CompareTo( cloneCard.ProgramDate ) == 0 ,
					"SmartCard did not clone the ProgramDate correctly." );
				Debug.Assert( card.PartNumber.CompareTo( cloneCard.PartNumber ) == 0 ,
					"SmartCard did not clone the PartNumber correctly." );
			}
			catch ( Exception e )
			{
				Console.WriteLine( "Exception in SmartCardTester.Test(): {0}" , e.ToString() );
			}
		}
	}

	#endregion
#endif // DEBUG

}
