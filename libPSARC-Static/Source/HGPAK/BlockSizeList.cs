using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata.Ecma335;

namespace libHGPAK.HGPAK {

    public class BlockSizeList {

        private ulong[] sizes;
        private ulong[] offsets;

        public ulong this[int index] {
            get => sizes[index];
            set => sizes[index] = value;
        }

        public ulong getBlockSize(int index) {
            return sizes[index];
        }

        public ulong getBlockOffset( int index ) {
            return offsets[index];
        }

        public int Length => sizes.Length;

        public BlockSizeList() : this( 0 ) { }

        public BlockSizeList( ulong numBlocks ) {
            this.sizes = new ulong[numBlocks];
            this.offsets = new ulong[numBlocks];
        }

        public BlockSizeList( Stream streamIn, ulong numBlocks) {
            this.sizes = new ulong[numBlocks];
            this.offsets = new ulong[numBlocks];
            Debug.WriteLine( $"numBlocks = {numBlocks}\n" );

            BinaryReader br = new BinaryReader( streamIn );
            ulong offset = 0; //Start local offset
            for (int i = 0; i < sizes.Length; i++) {
                sizes[i] = br.ReadUInt64();
                offsets[i] = offset;
                long aligned_size = ( (long) sizes[i] + 0x0F ) & ~0x0F;
                offset += (ulong) aligned_size;
            }
        }

    }

}
