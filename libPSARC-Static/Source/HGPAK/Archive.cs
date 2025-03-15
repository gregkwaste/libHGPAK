using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Transactions;
using ZstdSharp;


namespace libHGPAK.HGPAK {

    public class InvalidArchiveException : Exception {
        public InvalidArchiveException() : base( "Invalid archive." ) { }
    }

    public class Archive : IDisposable {
        public Header header;

        public FileList fileEntries;

        public BlockSizeList blockSizes;

        public List<string> filePaths;

        public Stream streamIn;
        private readonly bool keepOpen;

        //Compression Cache
        private byte[] outBuffer = new byte[HGPAK_HEADER.UNCOMPRESSED_BLOCK_SIZE];
        private int cachedBlockId = -1;
        private int cachedDecompSize = 0;
        
        #region // Methods

        public Archive() {
            this.header = new Header();
            this.fileEntries = null;
            this.blockSizes = null;
            this.filePaths = null;
        }

        [Conditional( "DEBUG" )]
        private static void DebugLog( object obj ) => Debug.WriteLine( obj );

        [Conditional( "DEBUG" )]
        private static void DebugLogPosition( long position ) => DebugLog( string.Format( "\n0x{0:X8} ({0})\n", position ) );

        [Conditional( "DEBUG" )]
        private static void DebugLogFileEntries( FileList fileEntries, int count ) {
            count = Math.Min( fileEntries.Length, count );
            for ( int i = 0; i < count; i++ ) DebugLog( fileEntries[i].ToString( $"{typeof( FileEntry )}[{i}]" ) );
        }

        [Conditional( "DEBUG" )]
        private static void DebugLogBlockSizes( BlockSizeList blockSizes, int count ) {
            count = Math.Min( blockSizes.Length, count );
            for ( int i = 0; i < count; i++ ) DebugLog( $"blockSizes[{i}] = {blockSizes[i]}" );
        }

        public Archive( string archivePath, bool keepOpen = false ) : this( File.OpenRead( archivePath ), keepOpen ) { }
        public Archive( Stream stream, bool keepOpen = false ) {
            this.streamIn = stream;
            this.keepOpen = keepOpen;

            bool isHGPAK = Header.IsValid(streamIn);

            if ( !isHGPAK ) throw new InvalidArchiveException();

            header = new Header( streamIn );
            DebugLog( header );

            DebugLogPosition( streamIn.Position );

            fileEntries = new FileList( streamIn, header.tocEntryCount );
            DebugLogFileEntries( fileEntries, 5 );

            DebugLogPosition( streamIn.Position );

            ulong length = (header.dataOffset - (ulong) streamIn.Position) / 0x8;
            blockSizes = new BlockSizeList( streamIn, length);
            DebugLogBlockSizes( blockSizes, 5 );

            DebugLogPosition( streamIn.Position );

            //Extract manifest (first file of the archive and save filepaths
            filePaths = new List<string>();

            using ( var reader = new StreamReader( ExtractFile( fileEntries[0] ) ) ) {
                while ( !reader.EndOfStream )
                    filePaths.Add( reader.ReadLine() );
            }
            
        }

        public void Dispose() {
            if ( !keepOpen ) streamIn.Close();
        }

        public int GetFileIndex( string filePath ) {
            return (filePaths.Contains( filePath )) ? filePaths.IndexOf( filePath ) + 1 : -1;
        }

        public Stream ExtractFile( string filePath, Stream streamOut = null ) {
            return ExtractFile( GetFileIndex( filePath ), streamOut );
        }

        public Stream ExtractFile( int fileIndex, Stream streamOut = null ) {
             return ExtractFile( fileEntries[fileIndex], streamOut );
        }

        public Stream ExtractFile(FileEntry fileEntry, Stream streamOut = null ) {
            var in_buffer = new byte[HGPAK_HEADER.UNCOMPRESSED_BLOCK_SIZE];

            ulong offset = fileEntry.uncompressedOffset - header.dataOffset;
            var block_index = (int) (offset / HGPAK_HEADER.UNCOMPRESSED_BLOCK_SIZE); //First Block Index
            var block_offset = offset - ( (ulong) block_index * HGPAK_HEADER.UNCOMPRESSED_BLOCK_SIZE ); //First Block Offset (Local)
            
            
            long size = (long) fileEntry.uncompressedFileSize;
            long total = 0;

            streamOut = streamOut ?? new MemoryStream( (int) fileEntry.uncompressedFileSize);
            long startPosition = streamOut.Position;

            Decompressor _decomp = new Decompressor();
            while (total < size){
                //Parse current block
                int block_comp_size = header.isCompressed > 0 ? (int) blockSizes.getBlockSize(block_index) : HGPAK_HEADER.UNCOMPRESSED_BLOCK_SIZE;
                var file_offset = (header.isCompressed > 0 ? (long) blockSizes.getBlockOffset( block_index ) : (long) HGPAK_HEADER.UNCOMPRESSED_BLOCK_SIZE * block_index) + (long) header.dataOffset;

                //Console.WriteLine( string.Format($"{file_offset} {block_comp_size} {block_index}"));
                if (cachedBlockId != block_index ) {
                    streamIn.Seek( file_offset, SeekOrigin.Begin );
                    streamIn.Read( in_buffer, 0, block_comp_size );
                }
                
                int block_decomp_size = 0;

                bool isBlockCompressed = block_comp_size == HGPAK_HEADER.UNCOMPRESSED_BLOCK_SIZE ? false : true;

                if ( isBlockCompressed ) {

                    //Use caching to prevent continuous decompression
                    if (cachedBlockId != block_index ) {
                        bool success = _decomp.TryUnwrap( in_buffer, 0, block_comp_size,
                        outBuffer, 0, HGPAK_HEADER.UNCOMPRESSED_BLOCK_SIZE, out cachedDecompSize );
                        cachedBlockId = block_index;
                    }

                    block_decomp_size = cachedDecompSize; //Fetch cached or updated value
                    block_decomp_size = Math.Min( block_decomp_size - (int) block_offset, (int) (size - total ));
                    streamOut.Write( outBuffer, (int) block_offset, block_decomp_size );

                } else {

                    block_decomp_size = Math.Min( block_comp_size - (int) block_offset, (int) (size - total) );
                    streamOut.Write( in_buffer, (int) block_offset, block_decomp_size );
                }

                block_index += 1;
                total += block_decomp_size;
                block_offset = 0; //After the first block has been parsed this should be 0
            }

            streamOut.Flush();
            streamOut.Position = 0; //Reset Stream
            return streamOut;
        } 

        /*
            public Stream ExtractFile( FileEntry fileEntry, Stream streamOut = null) {
                
                // loop until all blocks have been read
                while ( total < size ) {
                    uint blockSize = blockSizes[index];

                    if (blockSize == 0) {
                        //Uncompressed block with size equal to the maxblocksize
                        blockSize = header.maxBlockSize;
                    }

                    streamIn.Read( buffer, 0, (int) blockSize );

                    var zOut = new ComponentAce.Compression.Libs.zlib.ZOutputStream( streamOut );

                    //Detect zlib blocks
                    if (blockSize != size && buffer[0] == 0x78 &&
                        (buffer[1] == 0x01 || buffer[1] == 0x9C || buffer[1] == 0xDA || buffer[1] == 0x5E) ) {
                        try {
                            zOut.Write( buffer, 0, (int) blockSize );
                            zOut.Flush();
                            total += zOut.TotalOut;
                        } catch {
                            //If decompression fails it was probably an uncompressed part as well
                            streamOut.Write( buffer, 0, (int) blockSize );
                            total += blockSize;
                        }
                    } else {
                        //Partial uncompressed block
                        streamOut.Write( buffer, 0, (int) blockSize );
                        total += blockSize;
                    }

                    index++;
                }


                streamOut.Flush();
                streamOut.Position = startPosition;
                return streamOut;
            }
            */

            public static bool IsValid( Stream streamIn ) => Header.IsValid( streamIn );

        #endregion
    }

}
