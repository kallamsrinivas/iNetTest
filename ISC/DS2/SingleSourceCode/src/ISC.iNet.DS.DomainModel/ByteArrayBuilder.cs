using System;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
	/// <summary>
	/// Summary description for ByteArrayBuilder.
	/// </summary>
	public class ByteArrayBuilder
	{

		#region Fields

		/// <summary>
		/// The maximum size that the array can grow.
		/// </summary>
		private int _maxCapacity;

		/// <summary>
		/// The array of bytes encapsulated by this object.
		/// </summary>
		private byte[] _array;

		/// <summary>
		/// The amount of information currently stored in the byte array.
		/// </summary>
		private int _length;

		/// <summary>
		/// The encoding method to use to encode string information into bytes.
		/// </summary>
		private Encoding _encoding;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a ByteArrayBuilder with the specified capacity and the specified maximum.
		/// The bytes are encoded according to the supplied encoder.
		/// </summary>
		/// <param name="capacity">The initial capacity of the array.</param>
		/// <param name="maxCapacity">The maximum capacity of the array.</param>
		/// <param name="encoding">The encoder to use to encode everything with.</param>
		public ByteArrayBuilder( int capacity , int maxCapacity , Encoding encoding )
		{
			_encoding = encoding;
			_maxCapacity = maxCapacity;
			_length = 0;
			EnsureCapacity( capacity );
		}

		/// <summary>
		/// Creates a ByteArrayBuilder with the specified capacity and the specified maximum.
		/// The default encoding is UTF8.
		/// </summary>
		/// <param name="capacity">The initial capacity of the array.</param>
		/// <param name="maxCapacity">The maximum capacity of the array.</param>
		public ByteArrayBuilder( int capacity , int maxCapacity )
		{
			_encoding = Encoding.UTF8;
			_maxCapacity = maxCapacity;
			_length = 0;
			EnsureCapacity( capacity );
		}
		
		/// <summary>
		/// Creates a ByteArrayBuilder with the specified byte array.
		/// The bytes are encoded according to the supplied encoder.
		/// </summary>
		/// <param name="array">The array to intialize the object with.</param>
		/// <param name="encoding">The encoder to use to encode everything with.</param>
		public ByteArrayBuilder( byte[] array , Encoding encoding )
		{
			_encoding = encoding;
			_maxCapacity = int.MaxValue;
			_length = array.Length;
			_array = array;
		}

		/// <summary>
		/// Creates a ByteArrayBuilder with the specified capacity and with no maximum.
		/// The bytes are encoded according to the supplied encoder.
		/// </summary>
		/// <param name="capacity">The initial capacity of the array.</param>
		/// <param name="encoding">The encoder to use to encode everything with.</param>
		public ByteArrayBuilder( int capacity , Encoding encoding )
		{
			_encoding = encoding;
			_maxCapacity = int.MaxValue;
			_length = 0;
			EnsureCapacity( capacity );
		}

		/// <summary>
		/// Creates a ByteArrayBuilder with the specified capacity and with no maximum.
		/// The default encoding is UTF8.
		/// </summary>
		/// <param name="capacity">The initial capacity of the array.</param>
		public ByteArrayBuilder( int capacity )
		{
			_encoding = Encoding.UTF8;
			_maxCapacity = int.MaxValue;
			_length = 0;
			EnsureCapacity( capacity );
		}

		/// <summary>
		/// Creates a ByteArrayBuilder with the specified byte array.
		/// The default encoding is UTF8.
		/// </summary>
		/// <param name="array">The array to intialize the object with.</param>
		public ByteArrayBuilder( byte[] array )
		{
			_encoding = Encoding.UTF8;
			_maxCapacity = int.MaxValue;
			_length = array.Length;
			_array = array;
		}

		/// <summary>
		/// Creates a ByteArrayBuilder with a capacity of 16 and with no maximum.
		/// The bytes are encoded according to the supplied encoder.
		/// </summary>
		/// <param name="encoding">The encoder to use to encode everything with.</param>
		public ByteArrayBuilder( Encoding encoding )
		{
			_encoding = encoding;
			_maxCapacity = int.MaxValue;
			_length = 0;
			EnsureCapacity( 16 );
		}

		/// <summary>
		/// Creates a ByteArrayBuilder with a capacity of 16 and with no maximum.
		/// The default encoding is UTF8.
		/// </summary>
		public ByteArrayBuilder()
		{
			_encoding = Encoding.UTF8;
			_maxCapacity = int.MaxValue;
			_length = 0;
			EnsureCapacity( 16 );
		}

		#endregion

		#region Properties

		/// <summary>
		/// Index method that accesses elements of the underlying byte array.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// index is outside the bounds of this instance while setting a character.
		/// </exception>
		/// <exception cref="IndexOutOfRangeException">
		/// index is outside the bounds of this instance while getting a character.
		/// </exception>
		public byte this[ int index ]
		{
			get
			{
				// If it is in range.
				if ( _length > index )
				{
					return _array[ index ];
				}
				else
				{
					// Indicate index is too large.
					throw new IndexOutOfRangeException();
				}
			}
			set
			{
				// If it fits in the array.
				if ( index < _array.Length )
				{
					// Set it.
					_array[ index ] = value;

					// Guarantee the length.
					_length = Math.Max( _length , index + 1 );
				}
				else
				{
					// Indicate out of range.
					throw new ArgumentOutOfRangeException( "ByteArrayBuilder[" + index + "]" );
				}
			}
		}

		/// <summary>
		/// Returns the length of the information currently in the array.
		/// </summary>
		public int Length
		{
			get
			{
				return _length;
			}
		}

		/// <summary>
		/// Returns the maximum capacity of this array.
		/// </summary>
		public int MaxCapacity
		{
			get
			{
				return _maxCapacity;
			}
		}

		/// <summary>
		/// Return the current length of the array encapsulated by this object.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The value specified for a set operation is less than the current length of this instance.
		/// <br>-or-</br>
		/// The value specified for a set operation is greater than the maximum capacity.
		/// </exception>
		public int Capacity
		{
			get
			{
				return ( _array != null ) ? _array.Length : 0;
			}
			set
			{
				byte[] temp;
				int newCapacity;

				// If the value is greater than the maximum capacity.
				if ( ( _maxCapacity >= 0 ) && ( value > _maxCapacity ) )
					throw new ArgumentOutOfRangeException( "ByteArrayBuilder.Capacity.set - value=" + value + ", maxCapacity=" + _maxCapacity );

				// If the value is less than the current capacity.
				if ( value < Capacity )
					throw new ArgumentOutOfRangeException( "ByteArrayBuilder.Capacity.set - value=" + value + ", Capacity=" + Capacity );

				// Save the old array.
				temp = _array;
				
				// Determine the new capacity.
				newCapacity = Math.Max( value , Math.Min( Capacity * 2 , _maxCapacity ) );
				// newCapacity = ( int ) ( value * 1.1 );

				// Make the new array.
				_array = new byte[ newCapacity ];

				// If necessary, copy the old array information to the new.
				if ( _length > 0 )
				{
					for ( int n = 0 ; n < _length ; n++ )
					{
						_array[ n ] = temp[ n ];
					}
				}	
			}
		}

		#endregion

		
		#region Methods

		/// <summary>
		/// Guarantees that a minimum amount of capacity is available.
		/// </summary>
		/// <param name="amount">The number of bytes to ensure are available.</param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The amount is greater than the maximum capacity.
		/// </exception>
		public void EnsureCapacity( int amount )
		{
			if ( amount > Capacity )
			{
				Capacity = amount;
			}
		}

		/// <summary>
		/// Converts this object to a byte array.
		/// </summary>
		/// <returns>The underlying byte array encapsulated by the object.</returns>
		public byte[] toArray()
		{
			return _array;
		}

		/// <summary>
		/// Append a byte array to the encapsulated byte array.
		/// </summary>
		/// <param name="array">The array to append.</param>
		public void Append( byte[] array )
		{
			int copyAmount;

			// Guarantee as much capacity as possible.
			EnsureCapacity( Length + array.Length );

			// Determine the amount to copy.
			copyAmount = Math.Min( array.Length , Capacity - Length );

			// Copy the bytes from the array into the internal array.
			for ( int n = 0 ; n < copyAmount ; n++ , _length++ )
			{
				_array[ _length ] = array[ n ];
			}
		}

		/// <summary>
		/// Append another byte array builder to this array.
		/// </summary>
		/// <param name="builder">The array to append.</param>
		public void Append( ByteArrayBuilder builder )
		{
			Append( builder.toArray() );
		}

		/// <summary>
		/// Append a string to the encapsulated byte array using the encoder.
		/// </summary>
		/// <param name="str">The string to append.</param>
		public void Append( string str )
		{
			byte[] encodedStr;

			// Encode the string.
			encodedStr = _encoding.GetBytes( str );

			// Append it to the array.
			Append( encodedStr );
		}

		/// <summary>
		/// Append a byte to the encapsulated byte array.
		/// </summary>
		/// <param name="b">The byte to append.</param>
		public void Append( byte b )
		{
			// Ensure capacity.
			EnsureCapacity( Length + 1 );

			// If it will it, append it to the string.
			if ( ( Capacity - Length ) > 0 )
			{
				_array[ _length ] = b;
				_length++;
			}
		}

		/// <summary>
		/// Convert the byte array into a string, encoded with the proper encoder.
		/// </summary>
		/// <returns>The encoded string representation of the byte array.</returns>
		public override string ToString()
		{
			return _encoding.GetString( _array , 0 , _length );
		}

		/// <summary>
		/// This method fits the capacity of the internal array to the length of the data.
		/// </summary>
		public void FitCapacity()
		{
			byte[] temp;

			if ( _length < Capacity )
			{
				// Save the old array.
				temp = _array;
				
				// Make the new array.
				_array = new byte[ _length ];

				// If necessary, copy the old array information to the new.
				if ( _length > 0 )
				{
					for ( int n = 0 ; n < _length ; n++ )
					{
						_array[ n ] = temp[ n ];
					}
				}
			}
		}

		#endregion
	}
}
