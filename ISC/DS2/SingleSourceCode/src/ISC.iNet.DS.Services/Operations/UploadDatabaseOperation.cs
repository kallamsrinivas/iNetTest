using ISC.iNet.DS.DomainModel;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Core;
using System.IO;
using System;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
    /// <summary>
    /// </summary>
    public class UploadDatabaseOperation : UploadDatabaseAction, IOperation
    {
        #region Constructors

        /// <summary>
        /// </summary>
        public UploadDatabaseOperation() { }

        public UploadDatabaseOperation( UploadDatabaseAction uploadDatabaseAction ) : base( uploadDatabaseAction ) { }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// </summary>
        /// <returns>Docking station event</returns>
        public DockingStationEvent Execute()
        {
            UploadDatabaseEvent uploadEvent = new UploadDatabaseEvent( this );

            CompressDatabase( uploadEvent );
#if DEBUG
            // UncompressDatabase( uploadEvent );  // unit test.  Temporarily uncomment it to run it.
#endif

            return uploadEvent;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// System.IO.Packaging.ZipPackage can handle .zip files, but it is not supported in Compact Framework.
        /// We therefore use the freeware SharpZipLib to do our uncompression.
        /// </remarks>
        /// <param name="uploadEvent"></param>
        private void CompressDatabase( UploadDatabaseEvent uploadEvent )
        {
            string dbFilePath = Controller.FLASHCARD_PATH + Controller.INET_DB_NAME;

            FileInfo finfo = new FileInfo( dbFilePath );
            long totalLength = finfo.Length;

            using ( FileStream dbFileStream = new FileStream( dbFilePath, FileMode.Open ) )
            {
                MemoryStream outputMemStream = new MemoryStream();
                ZipOutputStream zipStream = new ZipOutputStream( outputMemStream );

                // We just use default compression leve, which I think is actually 6.
                zipStream.SetLevel( Deflater.DEFAULT_COMPRESSION ); // 0-9, 9 being the highest compression, 0 being no compression

                ZipEntry newEntry = new ZipEntry( Controller.INET_DB_NAME  );
                newEntry.DateTime = DateTime.UtcNow;

                zipStream.PutNextEntry( newEntry );

                // Assign the entry's size to what the file size is expected to be when
                // uncompressed.  This is necessary to get Window's built-in extractor to work,
                // according to SharpZipLib sample code.
                newEntry.Size = finfo.Length;

                DateTime startTime = DateTime.UtcNow;

                // Copy the file into the zip stream.  The ZipStream 
                // compresses the data as it's fed into it.
                const int bufferSize = 8196;
                long logThreshold = bufferSize * 50;
                byte[] buffer = new byte[ bufferSize ];
                int bytesRead;
                long totalBytesRead = 0;
                while ( ( bytesRead = dbFileStream.Read( buffer, 0, bufferSize ) ) > 0 )
                {
                    zipStream.Write( buffer, 0, bytesRead );

                    totalBytesRead += bytesRead;

                    if ( ( totalBytesRead % logThreshold ) == 0 )
                        LogCompression( totalBytesRead, totalLength, outputMemStream.Length );
                }

                zipStream.CloseEntry();

                zipStream.IsStreamOwner = false;    // False stops the Close also Closing the underlying stream.
                zipStream.Close();          // Must finish the ZipOutputStream before using outputMemStream.

                outputMemStream.Position = 0;

                DateTime endTime = DateTime.UtcNow;
                TimeSpan elapsed = endTime - startTime;
                LogCompression( totalBytesRead, totalLength, outputMemStream.Length );
                Log.Debug( string.Format( "Compression finished. Elapsed time: {0} seconds.", (int)elapsed.TotalSeconds ) );

                // ToArray is the cleaner and easiest to use correctly with the penalty of duplicating allocated memory.
                uploadEvent.File = outputMemStream.ToArray();
                // Alternative:  GetBuffer returns a raw buffer raw and so you need to account for the true length yourself.
                //byte[] byteArrayOut = outputMemStream.GetBuffer();
                //long len = outputMemStream.Length;

                uploadEvent.FileName = newEntry.Name + ".zip";
            }
        }

        private void LogCompression( long totalBytesRead, long totalLength, long zipStreamLength )
        {
            float percentProcessed = ( (float)totalBytesRead / (float)totalLength ) * 100f;
            float compression = ( (float)zipStreamLength / (float)totalBytesRead ) * 100f;

            Log.Debug( string.Format( "Compressed {0} of {1} bytes ({2}%%) down to {3} bytes ({4}%%).",
                totalBytesRead, totalLength, (int)percentProcessed, zipStreamLength, (int)compression ) );
        }

#if DEBUG
/***
        /// <summary>
        /// This function is just a unit test that can be used to verify that CompressDatabase()
        /// works correctly by trying to uncompress what it just compressed.
        /// </summary>
        /// <param name="uploadEvent"></param>
        private void UncompressDatabase( UploadDatabaseEvent uploadEvent )
        {
            string funcMsg = Name + ".UncompressDatabase: ";

            Log.Debug( string.Format( "{0}Extracting contents of {1} byte zip file \"{2}\"...",
                funcMsg, uploadEvent.File.Length, uploadEvent.FileName ) );

            try
            {
                using ( MemoryStream memStream = new MemoryStream( uploadEvent.File, false ) )
                {
                    // System.IO.Packaging.ZipPackage can handle .zip files, but it is not supported in Compact Framework.
                    // We therefore use the freeware SharpZipLib to do our uncompression.
                    using ( ZipInputStream s = new ZipInputStream( memStream ) )
                    {
                        ZipEntry zipEntry;

                        // we assume the first entry is the one and only file we want to uncompress.
                        if ( ( zipEntry = s.GetNextEntry() ) != null ) 
                        {
                            Log.Debug( string.Format( "{0}ZipEntry Name=\"{1}\", Compressed size={2}, Uncompressed size={3}", funcMsg, zipEntry.Name, zipEntry.CompressedSize, zipEntry.Size ) );

                            string deflatedFileName = "deflateTest_" + Path.GetFileName( zipEntry.Name );

                            if ( deflatedFileName == String.Empty )
                                deflatedFileName = "unknown.file";

                            deflatedFileName = "deflateTest_" + deflatedFileName;

                            using ( FileStream fileStream = new FileStream( Controller.FLASHCARD_PATH + deflatedFileName, FileMode.CreateNew ) )
                            {
                                byte[] buf = new byte[8192];
                                while ( true )
                                {
                                    int size = s.Read( buf, 0, buf.Length );
                                    if ( size <= 0 ) break;
                                    fileStream.Write( buf, 0, size );
                                }

                                fileStream.Close();
                            }

                        }  // end-while GetNextEntry

                    }  // end-using ZipInputStream

                }  // end-using MemoryStream
            }
            catch ( Exception e )
            {
                throw new ApplicationException( "Error uncompressing file.", e );
            }
        }
***/
#endif

        #endregion Methods
    }
}
