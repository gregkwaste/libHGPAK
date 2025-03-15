using System;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using libHGPAK.Interop;

namespace libHGPAK.HGPAK {

    static public class HGPAK_HEADER {
        public const string MAGIC = "HGPAK";
        public const int SIZE = 48;
        public const int TOC_ENTRY_SIZE = 0x20; // bytes - 16 md5 hash, uncompressed offset, uncompressed length
        public const int ITEM_SIZE_GRAIN = 0x07; // item data padded up to nearest 8 bytes
        public const int BLOCK_SIZE_GRAIN = 0x07; // compressed block padded up to nearest 16 bytes
        public const int UNCOMPRESSED_BLOCK_SIZE = 0x10000; // 64kB

    }

    /// <summary>Header struct for HGPAK (as) file format.</summary>
    [ByteOrder( Endian.Little )]
    [StructLayout( LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 0x04, Size = 0x20 )]
    public struct Header {
        internal static readonly uint MAGIC = BitConverter.ToUInt32( [0x48, 0x47, 0x50, 0x41, 0x4B, 0x00, 0x00, 0x00], 0 );
        internal const uint VERSION = 0x2; //As of 01/2025
        /// <summary>Should always be "HGPAK".</summary>
        /// 
        //[MarshalAs( UnmanagedType.ByValArray, SizeConst = 5 )]
        [StructMeta( ToStringMethod = "_magicToString" ), ByteOrder( Endian.Little )]
        /* 0x00 [0x08] */ public readonly UInt64 magic;
        private string _magicToString() => Encoding.ASCII.GetString( BitConverter.GetBytes( magic ) );

        /// <summary>PSARC format version. High short is Major. Low short is Minor.</summary>

        /* 0x08 [0x08] */ public readonly UInt64 version;
        
        // the above properties are not serialized

        /// <summary>Total number of Entries in TOC.</summary>
        /* 0x10 [0x08] */ public UInt64 tocEntryCount;

        /// <summary>Total number of Blocks in the PAK file.</summary>
        /* 0x18 [0x08] */ public UInt64 blockCount;

        /// <summary>Whether the blocks are compressed or not.</summary>
        /* 0x20 [0x08] */ public UInt64 isCompressed;

        /// <summary>Offset where the Data starts.</summary>
        /* 0x28 [0x08] */ public UInt64 dataOffset;

        #region // Methods
        public Header(uint dataOffset = 0, bool isCompressed=true, uint numEntries = 0x20, uint numBlocks = 0, uint maxBlockSize = (1 << 16)
        ) {
            this.magic = MAGIC;
            this.version = VERSION;
            this.isCompressed = (UInt64) (isCompressed ? 1 : 0) << (8*7);
            this.dataOffset = dataOffset;
            this.tocEntryCount = numEntries;
            this.blockCount = numBlocks;
        }

        public Header( byte[] bytes, int offset = 0 ) : this() => this = Unmanaged.BlitStruct<Header>( bytes, offset );
        public Header( Stream streamIn ) : this() => this = Unmanaged.BlitStruct<Header>( streamIn );

        public override string ToString() => ToString( null );
        public string ToString( string heading ) => StructMeta.StructToString( heading, this );

        public static bool IsValidMagicID( uint magic ) => (magic == MAGIC);
        public static bool IsValidMagicID( byte[] bytes ) => IsValidMagicID( BitConverter.ToUInt32( bytes, 0 ) );

        public static bool IsValid( Stream streamIn ) {
            var position = streamIn.Position;
            if ( streamIn.Length < Marshal.SizeOf<Header>() ) return false;
            var magic = new byte[sizeof( uint )];
            streamIn.Read( magic, 0, magic.Length );
            streamIn.Position = position;
            return IsValidMagicID( magic );
        }

        #endregion

    } /* 0x20 */

}
