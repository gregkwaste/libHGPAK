using System;
using System.Runtime.InteropServices;

namespace libHGPAK.HGPAK {
    using System.Collections.Generic;
    using System.IO;
    using Interop;

    [ByteOrder( Endian.Little )]
    [StructLayout( LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 0x01, Size = HGPAK_HEADER.TOC_ENTRY_SIZE )]
    public struct FileEntry {

        [MarshalAs( UnmanagedType.ByValArray, SizeConst = 0x10 )]
        [StructMeta( ToStringMethod = "_nameDigestToString" )]
        /* 0x00 [0x10] */ public byte[] nameDigest;
        private string _nameDigestToString() => Utils.BytesToHex( this.nameDigest );

        /// <summary>The uncompressed offset of the file.</summary>
        /* 0x10 [0x08] */ public UInt64 uncompressedOffset;

        /// <summary>The size of the uncompressed file.</summary>
        /* 0x18 [0x08] */ public UInt64 uncompressedFileSize;

        #region // Methods

        public override string ToString() => ToString( null );
        public string ToString( string heading ) => StructMeta.StructToString( heading, this );

        #endregion

    } /* 0x1E */

    public class FileList {

        private FileEntry[] entries;

        public FileEntry this[int index] {
            get => entries[index];
            set => entries[index] = value;
        }

        public int Length => entries.Length;

        #region // Methods

        public FileList() : this( 0 ) { }

        public FileList( UInt64 numFiles ) => entries = new FileEntry[numFiles];

        public FileList( Stream streamIn, UInt64 numFiles ) : this( numFiles ) {
            for ( ulong i = 0; i < numFiles; i++ ) {
                entries[i] = Unmanaged.BlitStruct<FileEntry>( streamIn );
            }
        }

        #endregion
    }

}
