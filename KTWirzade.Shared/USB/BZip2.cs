using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace iso_mode
{
    public class BZip2
    {
        /// <summary>
        /// Decompress the <paramref name="inStream">input</paramref> writing
        /// uncompressed data to the <paramref name="outStream">output stream</paramref>
        /// </summary>
        /// <param name="inStream">The readable stream containing data to decompress.</param>
        /// <param name="outStream">The output stream to receive the decompressed data.</param>
        /// <param name="isStreamOwner">Both streams are closed on completion if true.</param>
        public static void Decompress(Stream inStream, Stream outStream, bool isStreamOwner)
        {
            if (inStream == null)
                throw new ArgumentNullException(nameof(inStream));

            if (outStream == null)
                throw new ArgumentNullException(nameof(outStream));

            try
            {
                using (BZip2InputStream bzipInput = new BZip2InputStream(inStream))
                {
                    bzipInput.IsStreamOwner = isStreamOwner;

                    var buffer = new byte[4096];
                    
                    while (true)
                    {
                        int bytesRead = bzipInput.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            outStream.Write(buffer, 0, bytesRead);
                        }
                        else
                        {
                            outStream.Flush();
                            break;
                        }
                    }
                }
            }
            finally
            {
                if (isStreamOwner)
                {
                    // inStream is closed by the BZip2InputStream if stream owner
                    outStream.Dispose();
                }
            }
        }


        public class BZip2InputStream : Stream
        {
            #region Constants

            private const int START_BLOCK_STATE = 1;
            private const int RAND_PART_A_STATE = 2;
            private const int RAND_PART_B_STATE = 3;
            private const int RAND_PART_C_STATE = 4;
            private const int NO_RAND_PART_A_STATE = 5;
            private const int NO_RAND_PART_B_STATE = 6;
            private const int NO_RAND_PART_C_STATE = 7;

#if VECTORIZE_MEMORY_MOVE
		private static readonly int VectorSize = System.Numerics.Vector<byte>.Count;
#endif // VECTORIZE_MEMORY_MOVE

            #endregion Constants

            #region Instance Fields

            /*--
            index of the last char in the block, so
            the block size == last + 1.
            --*/
            private int last;

            /*--
            index in zptr[] of original string after sorting.
            --*/
            private int origPtr;

            /*--
            always: in the range 0 .. 9.
            The current block size is 100000 * this number.
            --*/
            private int blockSize100k;

            private bool blockRandomised;

            private int bsBuff;
            private int bsLive;
            private IChecksum mCrc = new BZip2Crc();

            private bool[] inUse = new bool[256];
            private int nInUse;

            private byte[] seqToUnseq = new byte[256];
            private byte[] unseqToSeq = new byte[256];

            private byte[] selector = new byte[BZip2Constants.MaximumSelectors];
            private byte[] selectorMtf = new byte[BZip2Constants.MaximumSelectors];

            private int[] tt;
            private byte[] ll8;

            /*--
            freq table collected to save a pass over the data
            during decompression.
            --*/
            private int[] unzftab = new int[256];

            private int[][] limit = new int[BZip2Constants.GroupCount][];
            private int[][] baseArray = new int[BZip2Constants.GroupCount][];
            private int[][] perm = new int[BZip2Constants.GroupCount][];
            private int[] minLens = new int[BZip2Constants.GroupCount];

            private readonly Stream baseStream;
            private bool streamEnd;

            private int currentChar = -1;

            private int currentState = START_BLOCK_STATE;

            private int storedBlockCRC, storedCombinedCRC;
            private int computedBlockCRC;
            private uint computedCombinedCRC;

            private int count, chPrev, ch2;
            private int tPos;
            private int rNToGo;
            private int rTPos;
            private int i2, j2;
            private byte z;

            #endregion Instance Fields

            /// <summary>
            /// Construct instance for reading from stream
            /// </summary>
            /// <param name="stream">Data source</param>
            public BZip2InputStream(Stream stream)
            {
                if (stream == null)
                    throw new ArgumentNullException(nameof(stream));
                // init arrays
                for (int i = 0; i < BZip2Constants.GroupCount; ++i)
                {
                    limit[i] = new int[BZip2Constants.MaximumAlphaSize];
                    baseArray[i] = new int[BZip2Constants.MaximumAlphaSize];
                    perm[i] = new int[BZip2Constants.MaximumAlphaSize];
                }

                baseStream = stream;
                bsLive = 0;
                bsBuff = 0;
                Initialize();
                InitBlock();
                SetupBlock();
            }

            /// <summary>
            /// Get/set flag indicating ownership of underlying stream.
            /// When the flag is true <see cref="Stream.Dispose()" /> will close the underlying stream also.
            /// </summary>
            public bool IsStreamOwner { get; set; } = true;

            #region Stream Overrides

            /// <summary>
            /// Gets a value indicating if the stream supports reading
            /// </summary>
            public override bool CanRead
            {
                get { return baseStream.CanRead; }
            }

            /// <summary>
            /// Gets a value indicating whether the current stream supports seeking.
            /// </summary>
            public override bool CanSeek
            {
                get { return false; }
            }

            /// <summary>
            /// Gets a value indicating whether the current stream supports writing.
            /// This property always returns false
            /// </summary>
            public override bool CanWrite
            {
                get { return false; }
            }

            /// <summary>
            /// Gets the length in bytes of the stream.
            /// </summary>
            public override long Length
            {
                get { return baseStream.Length; }
            }

            /// <summary>
            /// Gets the current position of the stream.
            /// Setting the position is not supported and will throw a NotSupportException.
            /// </summary>
            /// <exception cref="NotSupportedException">Any attempt to set the position.</exception>
            public override long Position
            {
                get { return baseStream.Position; }
                set { throw new NotSupportedException("BZip2InputStream position cannot be set"); }
            }

            /// <summary>
            /// Flushes the stream.
            /// </summary>
            public override void Flush()
            {
                baseStream.Flush();
            }

            /// <summary>
            /// Set the streams position.  This operation is not supported and will throw a NotSupportedException
            /// </summary>
            /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
            /// <param name="origin">A value of type <see cref="SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
            /// <returns>The new position of the stream.</returns>
            /// <exception cref="NotSupportedException">Any access</exception>
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException("BZip2InputStream Seek not supported");
            }

            /// <summary>
            /// Sets the length of this stream to the given value.
            /// This operation is not supported and will throw a NotSupportedExceptionortedException
            /// </summary>
            /// <param name="value">The new length for the stream.</param>
            /// <exception cref="NotSupportedException">Any access</exception>
            public override void SetLength(long value)
            {
                throw new NotSupportedException("BZip2InputStream SetLength not supported");
            }

            /// <summary>
            /// Writes a block of bytes to this stream using data from a buffer.
            /// This operation is not supported and will throw a NotSupportedException
            /// </summary>
            /// <param name="buffer">The buffer to source data from.</param>
            /// <param name="offset">The offset to start obtaining data from.</param>
            /// <param name="count">The number of bytes of data to write.</param>
            /// <exception cref="NotSupportedException">Any access</exception>
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException("BZip2InputStream Write not supported");
            }

            /// <summary>
            /// Writes a byte to the current position in the file stream.
            /// This operation is not supported and will throw a NotSupportedException
            /// </summary>
            /// <param name="value">The value to write.</param>
            /// <exception cref="NotSupportedException">Any access</exception>
            public override void WriteByte(byte value)
            {
                throw new NotSupportedException("BZip2InputStream WriteByte not supported");
            }

            /// <summary>
            /// Read a sequence of bytes and advances the read position by one byte.
            /// </summary>
            /// <param name="buffer">Array of bytes to store values in</param>
            /// <param name="offset">Offset in array to begin storing data</param>
            /// <param name="count">The maximum number of bytes to read</param>
            /// <returns>The total number of bytes read into the buffer. This might be less
            /// than the number of bytes requested if that number of bytes are not
            /// currently available or zero if the end of the stream is reached.
            /// </returns>
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                for (int i = 0; i < count; ++i)
                {
                    int rb = ReadByte();
                    if (rb == -1)
                    {
                        return i;
                    }

                    buffer[offset + i] = (byte)rb;
                }

                return count;
            }

            /// <summary>
            /// Closes the stream, releasing any associated resources.
            /// </summary>
            protected override void Dispose(bool disposing)
            {
                if (disposing && IsStreamOwner)
                {
                    baseStream.Dispose();
                }
            }

            /// <summary>
            /// Read a byte from stream advancing position
            /// </summary>
            /// <returns>byte read or -1 on end of stream</returns>
            public override int ReadByte()
            {
                if (streamEnd)
                {
                    return -1; // ok
                }

                int retChar = currentChar;
                switch (currentState)
                {
                    case RAND_PART_B_STATE:
                        SetupRandPartB();
                        break;

                    case RAND_PART_C_STATE:
                        SetupRandPartC();
                        break;

                    case NO_RAND_PART_B_STATE:
                        SetupNoRandPartB();
                        break;

                    case NO_RAND_PART_C_STATE:
                        SetupNoRandPartC();
                        break;

                    case START_BLOCK_STATE:
                    case NO_RAND_PART_A_STATE:
                    case RAND_PART_A_STATE:
                        break;
                }

                return retChar;
            }

            #endregion Stream Overrides

            private void MakeMaps()
            {
                nInUse = 0;
                for (int i = 0; i < 256; ++i)
                {
                    if (inUse[i])
                    {
                        seqToUnseq[nInUse] = (byte)i;
                        unseqToSeq[i] = (byte)nInUse;
                        nInUse++;
                    }
                }
            }

            private void Initialize()
            {
                char magic1 = BsGetUChar();
                char magic2 = BsGetUChar();

                char magic3 = BsGetUChar();
                char magic4 = BsGetUChar();

                if (magic1 != 'B' || magic2 != 'Z' || magic3 != 'h' || magic4 < '1' || magic4 > '9')
                {
                    streamEnd = true;
                    return;
                }

                SetDecompressStructureSizes(magic4 - '0');
                computedCombinedCRC = 0;
            }

            private void InitBlock()
            {
                char magic1 = BsGetUChar();
                char magic2 = BsGetUChar();
                char magic3 = BsGetUChar();
                char magic4 = BsGetUChar();
                char magic5 = BsGetUChar();
                char magic6 = BsGetUChar();

                if (magic1 == 0x17 && magic2 == 0x72 && magic3 == 0x45 && magic4 == 0x38 && magic5 == 0x50 && magic6 == 0x90)
                {
                    Complete();
                    return;
                }

                if (magic1 != 0x31 || magic2 != 0x41 || magic3 != 0x59 || magic4 != 0x26 || magic5 != 0x53 || magic6 != 0x59)
                {
                    BadBlockHeader();
                    streamEnd = true;
                    return;
                }

                storedBlockCRC = BsGetInt32();

                blockRandomised = (BsR(1) == 1);

                GetAndMoveToFrontDecode();

                mCrc.Reset();
                currentState = START_BLOCK_STATE;
            }

            private void EndBlock()
            {
                computedBlockCRC = (int)mCrc.Value;

                // -- A bad CRC is considered a fatal error. --
                if (storedBlockCRC != computedBlockCRC)
                {
                    CrcError();
                }

                // 1528150659
                computedCombinedCRC = ((computedCombinedCRC << 1) & 0xFFFFFFFF) | (computedCombinedCRC >> 31);
                computedCombinedCRC = computedCombinedCRC ^ (uint)computedBlockCRC;
            }

            private void Complete()
            {
                storedCombinedCRC = BsGetInt32();
                if (storedCombinedCRC != (int)computedCombinedCRC)
                {
                    CrcError();
                }

                streamEnd = true;
            }

            private void FillBuffer()
            {
                int thech = 0;

                try
                {
                    thech = baseStream.ReadByte();
                }
                catch (Exception)
                {
                    CompressedStreamEOF();
                }

                if (thech == -1)
                {
                    CompressedStreamEOF();
                }

                bsBuff = (bsBuff << 8) | (thech & 0xFF);
                bsLive += 8;
            }

            private int BsR(int n)
            {
                while (bsLive < n)
                {
                    FillBuffer();
                }

                int v = (bsBuff >> (bsLive - n)) & ((1 << n) - 1);
                bsLive -= n;
                return v;
            }

            private char BsGetUChar()
            {
                return (char)BsR(8);
            }

            private int BsGetIntVS(int numBits)
            {
                return BsR(numBits);
            }

            private int BsGetInt32()
            {
                int result = BsR(8);
                result = (result << 8) | BsR(8);
                result = (result << 8) | BsR(8);
                result = (result << 8) | BsR(8);
                return result;
            }

            private void RecvDecodingTables()
            {
                char[][] len = new char[BZip2Constants.GroupCount][];
                for (int i = 0; i < BZip2Constants.GroupCount; ++i)
                {
                    len[i] = new char[BZip2Constants.MaximumAlphaSize];
                }

                bool[] inUse16 = new bool[16];

                //--- Receive the mapping table ---
                for (int i = 0; i < 16; i++)
                {
                    inUse16[i] = (BsR(1) == 1);
                }

                for (int i = 0; i < 16; i++)
                {
                    if (inUse16[i])
                    {
                        for (int j = 0; j < 16; j++)
                        {
                            inUse[i * 16 + j] = (BsR(1) == 1);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < 16; j++)
                        {
                            inUse[i * 16 + j] = false;
                        }
                    }
                }

                MakeMaps();
                int alphaSize = nInUse + 2;

                //--- Now the selectors ---
                int nGroups = BsR(3);
                int nSelectors = BsR(15);

                for (int i = 0; i < nSelectors; i++)
                {
                    int j = 0;
                    while (BsR(1) == 1)
                    {
                        j++;
                    }

                    selectorMtf[i] = (byte)j;
                }

                //--- Undo the MTF values for the selectors. ---
                byte[] pos = new byte[BZip2Constants.GroupCount];
                for (int v = 0; v < nGroups; v++)
                {
                    pos[v] = (byte)v;
                }

                for (int i = 0; i < nSelectors; i++)
                {
                    int v = selectorMtf[i];
                    byte tmp = pos[v];
                    while (v > 0)
                    {
                        pos[v] = pos[v - 1];
                        v--;
                    }

                    pos[0] = tmp;
                    selector[i] = tmp;
                }

                //--- Now the coding tables ---
                for (int t = 0; t < nGroups; t++)
                {
                    int curr = BsR(5);
                    for (int i = 0; i < alphaSize; i++)
                    {
                        while (BsR(1) == 1)
                        {
                            if (BsR(1) == 0)
                            {
                                curr++;
                            }
                            else
                            {
                                curr--;
                            }
                        }

                        len[t][i] = (char)curr;
                    }
                }

                //--- Create the Huffman decoding tables ---
                for (int t = 0; t < nGroups; t++)
                {
                    int minLen = 32;
                    int maxLen = 0;
                    for (int i = 0; i < alphaSize; i++)
                    {
                        maxLen = Math.Max(maxLen, len[t][i]);
                        minLen = Math.Min(minLen, len[t][i]);
                    }

                    HbCreateDecodeTables(limit[t], baseArray[t], perm[t], len[t], minLen, maxLen, alphaSize);
                    minLens[t] = minLen;
                }
            }

            private void GetAndMoveToFrontDecode()
            {
                byte[] yy = new byte[256];
                int nextSym;

                int limitLast = BZip2Constants.BaseBlockSize * blockSize100k;
                origPtr = BsGetIntVS(24);

                RecvDecodingTables();
                int EOB = nInUse + 1;
                int groupNo = -1;
                int groupPos = 0;

                /*--
                Setting up the unzftab entries here is not strictly
                necessary, but it does save having to do it later
                in a separate pass, and so saves a block's worth of
                cache misses.
                --*/
                for (int i = 0; i <= 255; i++)
                {
                    unzftab[i] = 0;
                }

                for (int i = 0; i <= 255; i++)
                {
                    yy[i] = (byte)i;
                }

                last = -1;

                if (groupPos == 0)
                {
                    groupNo++;
                    groupPos = BZip2Constants.GroupSize;
                }

                groupPos--;
                int zt = selector[groupNo];
                int zn = minLens[zt];
                int zvec = BsR(zn);
                int zj;

                while (zvec > limit[zt][zn])
                {
                    if (zn > 20)
                    {
                        // the longest code
                        throw new BZip2Exception("Bzip data error");
                    }

                    zn++;
                    while (bsLive < 1)
                    {
                        FillBuffer();
                    }

                    zj = (bsBuff >> (bsLive - 1)) & 1;
                    bsLive--;
                    zvec = (zvec << 1) | zj;
                }

                if (zvec - baseArray[zt][zn] < 0 || zvec - baseArray[zt][zn] >= BZip2Constants.MaximumAlphaSize)
                {
                    throw new BZip2Exception("Bzip data error");
                }

                nextSym = perm[zt][zvec - baseArray[zt][zn]];

                while (true)
                {
                    if (nextSym == EOB)
                    {
                        break;
                    }

                    if (nextSym == BZip2Constants.RunA || nextSym == BZip2Constants.RunB)
                    {
                        int s = -1;
                        int n = 1;
                        do
                        {
                            if (nextSym == BZip2Constants.RunA)
                            {
                                s += (0 + 1) * n;
                            }
                            else if (nextSym == BZip2Constants.RunB)
                            {
                                s += (1 + 1) * n;
                            }

                            n <<= 1;

                            if (groupPos == 0)
                            {
                                groupNo++;
                                groupPos = BZip2Constants.GroupSize;
                            }

                            groupPos--;

                            zt = selector[groupNo];
                            zn = minLens[zt];
                            zvec = BsR(zn);

                            while (zvec > limit[zt][zn])
                            {
                                zn++;
                                while (bsLive < 1)
                                {
                                    FillBuffer();
                                }

                                zj = (bsBuff >> (bsLive - 1)) & 1;
                                bsLive--;
                                zvec = (zvec << 1) | zj;
                            }

                            nextSym = perm[zt][zvec - baseArray[zt][zn]];
                        } while (nextSym == BZip2Constants.RunA || nextSym == BZip2Constants.RunB);

                        s++;
                        byte ch = seqToUnseq[yy[0]];
                        unzftab[ch] += s;

                        while (s > 0)
                        {
                            last++;
                            ll8[last] = ch;
                            s--;
                        }

                        if (last >= limitLast)
                        {
                            BlockOverrun();
                        }

                        continue;
                    }
                    else
                    {
                        last++;
                        if (last >= limitLast)
                        {
                            BlockOverrun();
                        }

                        byte tmp = yy[nextSym - 1];
                        unzftab[seqToUnseq[tmp]]++;
                        ll8[last] = seqToUnseq[tmp];

                        var j = nextSym - 1;

#if VECTORIZE_MEMORY_MOVE
					// This is vectorized memory move. Going from the back, we're taking chunks of array
					// and write them at the new location shifted by one. Since chunks are VectorSize long,
					// at the end we have to move "tail" (or head actually) of the array using a plain loop.
					// If System.Numerics.Vector API is not available, the plain loop is used to do the whole copying.

					while(j >= VectorSize)
					{
						var arrayPart = new System.Numerics.Vector<byte>(yy, j - VectorSize);
						arrayPart.CopyTo(yy, j - VectorSize + 1);
						j -= VectorSize;
					}
#endif // VECTORIZE_MEMORY_MOVE

                        while (j > 0)
                        {
                            yy[j] = yy[--j];
                        }

                        yy[0] = tmp;

                        if (groupPos == 0)
                        {
                            groupNo++;
                            groupPos = BZip2Constants.GroupSize;
                        }

                        groupPos--;
                        zt = selector[groupNo];
                        zn = minLens[zt];
                        zvec = BsR(zn);
                        while (zvec > limit[zt][zn])
                        {
                            zn++;
                            while (bsLive < 1)
                            {
                                FillBuffer();
                            }

                            zj = (bsBuff >> (bsLive - 1)) & 1;
                            bsLive--;
                            zvec = (zvec << 1) | zj;
                        }

                        nextSym = perm[zt][zvec - baseArray[zt][zn]];
                        continue;
                    }
                }
            }

            private void SetupBlock()
            {
                int[] cftab = new int[257];

                cftab[0] = 0;
                Array.Copy(unzftab, 0, cftab, 1, 256);

                for (int i = 1; i <= 256; i++)
                {
                    cftab[i] += cftab[i - 1];
                }

                for (int i = 0; i <= last; i++)
                {
                    byte ch = ll8[i];
                    tt[cftab[ch]] = i;
                    cftab[ch]++;
                }

                cftab = null;

                tPos = tt[origPtr];

                count = 0;
                i2 = 0;
                ch2 = 256; /*-- not a char and not EOF --*/

                if (blockRandomised)
                {
                    rNToGo = 0;
                    rTPos = 0;
                    SetupRandPartA();
                }
                else
                {
                    SetupNoRandPartA();
                }
            }

            private void SetupRandPartA()
            {
                if (i2 <= last)
                {
                    chPrev = ch2;
                    ch2 = ll8[tPos];
                    tPos = tt[tPos];
                    if (rNToGo == 0)
                    {
                        rNToGo = BZip2Constants.RandomNumbers[rTPos];
                        rTPos++;
                        if (rTPos == 512)
                        {
                            rTPos = 0;
                        }
                    }

                    rNToGo--;
                    ch2 ^= (int)((rNToGo == 1) ? 1 : 0);
                    i2++;

                    currentChar = ch2;
                    currentState = RAND_PART_B_STATE;
                    mCrc.Update(ch2);
                }
                else
                {
                    EndBlock();
                    InitBlock();
                    SetupBlock();
                }
            }

            private void SetupNoRandPartA()
            {
                if (i2 <= last)
                {
                    chPrev = ch2;
                    ch2 = ll8[tPos];
                    tPos = tt[tPos];
                    i2++;

                    currentChar = ch2;
                    currentState = NO_RAND_PART_B_STATE;
                    mCrc.Update(ch2);
                }
                else
                {
                    EndBlock();
                    InitBlock();
                    SetupBlock();
                }
            }

            private void SetupRandPartB()
            {
                if (ch2 != chPrev)
                {
                    currentState = RAND_PART_A_STATE;
                    count = 1;
                    SetupRandPartA();
                }
                else
                {
                    count++;
                    if (count >= 4)
                    {
                        z = ll8[tPos];
                        tPos = tt[tPos];
                        if (rNToGo == 0)
                        {
                            rNToGo = BZip2Constants.RandomNumbers[rTPos];
                            rTPos++;
                            if (rTPos == 512)
                            {
                                rTPos = 0;
                            }
                        }

                        rNToGo--;
                        z ^= (byte)((rNToGo == 1) ? 1 : 0);
                        j2 = 0;
                        currentState = RAND_PART_C_STATE;
                        SetupRandPartC();
                    }
                    else
                    {
                        currentState = RAND_PART_A_STATE;
                        SetupRandPartA();
                    }
                }
            }

            private void SetupRandPartC()
            {
                if (j2 < (int)z)
                {
                    currentChar = ch2;
                    mCrc.Update(ch2);
                    j2++;
                }
                else
                {
                    currentState = RAND_PART_A_STATE;
                    i2++;
                    count = 0;
                    SetupRandPartA();
                }
            }

            private void SetupNoRandPartB()
            {
                if (ch2 != chPrev)
                {
                    currentState = NO_RAND_PART_A_STATE;
                    count = 1;
                    SetupNoRandPartA();
                }
                else
                {
                    count++;
                    if (count >= 4)
                    {
                        z = ll8[tPos];
                        tPos = tt[tPos];
                        currentState = NO_RAND_PART_C_STATE;
                        j2 = 0;
                        SetupNoRandPartC();
                    }
                    else
                    {
                        currentState = NO_RAND_PART_A_STATE;
                        SetupNoRandPartA();
                    }
                }
            }

            private void SetupNoRandPartC()
            {
                if (j2 < (int)z)
                {
                    currentChar = ch2;
                    mCrc.Update(ch2);
                    j2++;
                }
                else
                {
                    currentState = NO_RAND_PART_A_STATE;
                    i2++;
                    count = 0;
                    SetupNoRandPartA();
                }
            }

            private void SetDecompressStructureSizes(int newSize100k)
            {
                if (!(0 <= newSize100k && newSize100k <= 9 && 0 <= blockSize100k && blockSize100k <= 9))
                {
                    throw new BZip2Exception("Invalid block size");
                }

                blockSize100k = newSize100k;

                if (newSize100k == 0)
                {
                    return;
                }

                int n = BZip2Constants.BaseBlockSize * newSize100k;
                ll8 = new byte[n];
                tt = new int[n];
            }

            private static void CompressedStreamEOF()
            {
                throw new EndOfStreamException("BZip2 input stream end of compressed stream");
            }

            private static void BlockOverrun()
            {
                throw new BZip2Exception("BZip2 input stream block overrun");
            }

            private static void BadBlockHeader()
            {
                throw new BZip2Exception("BZip2 input stream bad block header");
            }

            private static void CrcError()
            {
                throw new BZip2Exception("BZip2 input stream crc error");
            }

            private static void HbCreateDecodeTables(int[] limit, int[] baseArray, int[] perm, char[] length, int minLen, int maxLen, int alphaSize)
            {
                int pp = 0;

                for (int i = minLen; i <= maxLen; ++i)
                {
                    for (int j = 0; j < alphaSize; ++j)
                    {
                        if (length[j] == i)
                        {
                            perm[pp] = j;
                            ++pp;
                        }
                    }
                }

                for (int i = 0; i < BZip2Constants.MaximumCodeLength; i++)
                {
                    baseArray[i] = 0;
                }

                for (int i = 0; i < alphaSize; i++)
                {
                    ++baseArray[length[i] + 1];
                }

                for (int i = 1; i < BZip2Constants.MaximumCodeLength; i++)
                {
                    baseArray[i] += baseArray[i - 1];
                }

                for (int i = 0; i < BZip2Constants.MaximumCodeLength; i++)
                {
                    limit[i] = 0;
                }

                int vec = 0;

                for (int i = minLen; i <= maxLen; i++)
                {
                    vec += (baseArray[i + 1] - baseArray[i]);
                    limit[i] = vec - 1;
                    vec <<= 1;
                }

                for (int i = minLen + 1; i <= maxLen; i++)
                {
                    baseArray[i] = ((limit[i - 1] + 1) << 1) - baseArray[i];
                }
            }
        }

        /// <summary>
        /// CRC-32 with unreversed data and reversed output
        /// </summary>
        /// <remarks>
        /// Generate a table for a byte-wise 32-bit CRC calculation on the polynomial:
        /// x^32+x^26+x^23+x^22+x^16+x^12+x^11+x^10+x^8+x^7+x^5+x^4+x^2+x^1+x^0.
        ///
        /// Polynomials over GF(2) are represented in binary, one bit per coefficient,
        /// with the lowest powers in the most significant bit.  Then adding polynomials
        /// is just exclusive-or, and multiplying a polynomial by x is a right shift by
        /// one.  If we call the above polynomial p, and represent a byte as the
        /// polynomial q, also with the lowest power in the most significant bit (so the
        /// byte 0xb1 is the polynomial x^7+x^3+x+1), then the CRC is (q*x^32) mod p,
        /// where a mod b means the remainder after dividing a by b.
        ///
        /// This calculation is done using the shift-register method of multiplying and
        /// taking the remainder.  The register is initialized to zero, and for each
        /// incoming bit, x^32 is added mod p to the register if the bit is a one (where
        /// x^32 mod p is p+x^32 = x^26+...+1), and the register is multiplied mod p by
        /// x (which is shifting right by one and adding x^32 mod p if the bit shifted
        /// out is a one).  We start with the highest power (least significant bit) of
        /// q and repeat for all eight bits of q.
        ///
        /// This implementation uses sixteen lookup tables stored in one linear array
        /// to implement the slicing-by-16 algorithm, a variant of the slicing-by-8
        /// algorithm described in this Intel white paper:
        ///
        /// https://web.archive.org/web/20120722193753/http://download.intel.com/technology/comms/perfnet/download/slicing-by-8.pdf
        ///
        /// The first lookup table is simply the CRC of all possible eight bit values.
        /// Each successive lookup table is derived from the original table generated
        /// by Sarwate's algorithm. Slicing a 16-bit input and XORing the outputs
        /// together will produce the same output as a byte-by-byte CRC loop with
        /// fewer arithmetic and bit manipulation operations, at the cost of increased
        /// memory consumed by the lookup tables. (Slicing-by-16 requires a 16KB table,
        /// which is still small enough to fit in most processors' L1 cache.)
        /// </remarks>
        public sealed class BZip2Crc : IChecksum
        {
            #region Instance Fields

            private const uint crcInit = 0xFFFFFFFF;
            //const uint crcXor = 0x00000000;

            private static readonly uint[] crcTable = CrcUtilities.GenerateSlicingLookupTable(0x04C11DB7, isReversed: false);

            /// <summary>
            /// The CRC data checksum so far.
            /// </summary>
            private uint checkValue;

            #endregion Instance Fields

            /// <summary>
            /// Initialise a default instance of <see cref="BZip2Crc"></see>
            /// </summary>
            public BZip2Crc()
            {
                Reset();
            }

            /// <summary>
            /// Resets the CRC data checksum as if no update was ever called.
            /// </summary>
            public void Reset()
            {
                checkValue = crcInit;
            }

            /// <summary>
            /// Returns the CRC data checksum computed so far.
            /// </summary>
            /// <remarks>Reversed Out = true</remarks>
            public long Value
            {
                get
                {
                    // Technically, the output should be:
                    //return (long)(~checkValue ^ crcXor);
                    // but x ^ 0 = x, so there is no point in adding
                    // the XOR operation
                    return (long)(~checkValue);
                }
            }

            /// <summary>
            /// Updates the checksum with the int bval.
            /// </summary>
            /// <param name = "bval">
            /// the byte is taken as the lower 8 bits of bval
            /// </param>
            /// <remarks>Reversed Data = false</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Update(int bval)
            {
                checkValue = unchecked(crcTable[(byte)(((checkValue >> 24) & 0xFF) ^ bval)] ^ (checkValue << 8));
            }

            /// <summary>
            /// Updates the CRC data checksum with the bytes taken from
            /// a block of data.
            /// </summary>
            /// <param name="buffer">Contains the data to update the CRC with.</param>
            public void Update(byte[] buffer)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                Update(buffer, 0, buffer.Length);
            }

            /// <summary>
            /// Update CRC data checksum based on a portion of a block of data
            /// </summary>
            /// <param name = "segment">
            /// The chunk of data to add
            /// </param>
            public void Update(ArraySegment<byte> segment)
            {
                Update(segment.Array, segment.Offset, segment.Count);
            }

            /// <summary>
            /// Internal helper function for updating a block of data using slicing.
            /// </summary>
            /// <param name="data">The array containing the data to add</param>
            /// <param name="offset">Range start for <paramref name="data"/> (inclusive)</param>
            /// <param name="count">The number of bytes to checksum starting from <paramref name="offset"/></param>
            private void Update(byte[] data, int offset, int count)
            {
                int remainder = count % CrcUtilities.SlicingDegree;
                int end = offset + count - remainder;

                while (offset != end)
                {
                    checkValue = CrcUtilities.UpdateDataForNormalPoly(data, offset, crcTable, checkValue);
                    offset += CrcUtilities.SlicingDegree;
                }

                if (remainder != 0)
                {
                    SlowUpdateLoop(data, offset, end + remainder);
                }
            }

            /// <summary>
            /// A non-inlined function for updating data that doesn't fit in a 16-byte
            /// block. We don't expect to enter this function most of the time, and when
            /// we do we're not here for long, so disabling inlining here improves
            /// performance overall.
            /// </summary>
            /// <param name="data">The array containing the data to add</param>
            /// <param name="offset">Range start for <paramref name="data"/> (inclusive)</param>
            /// <param name="end">Range end for <paramref name="data"/> (exclusive)</param>
            [MethodImpl(MethodImplOptions.NoInlining)]
            private void SlowUpdateLoop(byte[] data, int offset, int end)
            {
                while (offset != end)
                {
                    Update(data[offset++]);
                }
            }
        }

        internal static class CrcUtilities
        {
            /// <summary>
            /// The number of slicing lookup tables to generate.
            /// </summary>
            internal const int SlicingDegree = 16;

            /// <summary>
            /// Generates multiple CRC lookup tables for a given polynomial, stored
            /// in a linear array of uints. The first block (i.e. the first 256
            /// elements) is the same as the byte-by-byte CRC lookup table. 
            /// </summary>
            /// <param name="polynomial">The generating CRC polynomial</param>
            /// <param name="isReversed">Whether the polynomial is in reversed bit order</param>
            /// <returns>A linear array of 256 * <see cref="SlicingDegree"/> elements</returns>
            /// <remarks>
            /// This table could also be generated as a rectangular array, but the
            /// JIT compiler generates slower code than if we use a linear array.
            /// Known issue, see: https://github.com/dotnet/runtime/issues/30275
            /// </remarks>
            internal static uint[] GenerateSlicingLookupTable(uint polynomial, bool isReversed)
            {
                var table = new uint[256 * SlicingDegree];
                uint one = isReversed ? 1 : (1U << 31);

                for (int i = 0; i < 256; i++)
                {
                    uint res = (uint)(isReversed ? i : i << 24);
                    for (int j = 0; j < SlicingDegree; j++)
                    {
                        for (int k = 0; k < 8; k++)
                        {
                            if (isReversed)
                            {
                                res = (res & one) == 1 ? polynomial ^ (res >> 1) : res >> 1;
                            }
                            else
                            {
                                res = (res & one) != 0 ? polynomial ^ (res << 1) : res << 1;
                            }
                        }

                        table[(256 * j) + i] = res;
                    }
                }

                return table;
            }

            /// <summary>
            /// Mixes the first four bytes of input with <paramref name="checkValue"/>
            /// using normal ordering before calling <see cref="UpdateDataCommon"/>.
            /// </summary>
            /// <param name="input">Array of data to checksum</param>
            /// <param name="offset">Offset to start reading <paramref name="input"/> from</param>
            /// <param name="crcTable">The table to use for slicing-by-16 lookup</param>
            /// <param name="checkValue">Checksum state before this update call</param>
            /// <returns>A new unfinalized checksum value</returns>
            /// <seealso cref="UpdateDataForReversedPoly"/>
            /// <remarks>
            /// Assumes input[offset]..input[offset + 15] are valid array indexes.
            /// For performance reasons, this must be checked by the caller.
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static uint UpdateDataForNormalPoly(byte[] input, int offset, uint[] crcTable, uint checkValue)
            {
                byte x1 = (byte)((byte)(checkValue >> 24) ^ input[offset]);
                byte x2 = (byte)((byte)(checkValue >> 16) ^ input[offset + 1]);
                byte x3 = (byte)((byte)(checkValue >> 8) ^ input[offset + 2]);
                byte x4 = (byte)((byte)checkValue ^ input[offset + 3]);

                return UpdateDataCommon(input, offset, crcTable, x1, x2, x3, x4);
            }

            /// <summary>
            /// Mixes the first four bytes of input with <paramref name="checkValue"/>
            /// using reflected ordering before calling <see cref="UpdateDataCommon"/>.
            /// </summary>
            /// <param name="input">Array of data to checksum</param>
            /// <param name="offset">Offset to start reading <paramref name="input"/> from</param>
            /// <param name="crcTable">The table to use for slicing-by-16 lookup</param>
            /// <param name="checkValue">Checksum state before this update call</param>
            /// <returns>A new unfinalized checksum value</returns>
            /// <seealso cref="UpdateDataForNormalPoly"/>
            /// <remarks>
            /// Assumes input[offset]..input[offset + 15] are valid array indexes.
            /// For performance reasons, this must be checked by the caller.
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static uint UpdateDataForReversedPoly(byte[] input, int offset, uint[] crcTable, uint checkValue)
            {
                byte x1 = (byte)((byte)checkValue ^ input[offset]);
                byte x2 = (byte)((byte)(checkValue >>= 8) ^ input[offset + 1]);
                byte x3 = (byte)((byte)(checkValue >>= 8) ^ input[offset + 2]);
                byte x4 = (byte)((byte)(checkValue >>= 8) ^ input[offset + 3]);

                return UpdateDataCommon(input, offset, crcTable, x1, x2, x3, x4);
            }

            /// <summary>
            /// A shared method for updating an unfinalized CRC checksum using slicing-by-16.
            /// </summary>
            /// <param name="input">Array of data to checksum</param>
            /// <param name="offset">Offset to start reading <paramref name="input"/> from</param>
            /// <param name="crcTable">The table to use for slicing-by-16 lookup</param>
            /// <param name="x1">First byte of input after mixing with the old CRC</param>
            /// <param name="x2">Second byte of input after mixing with the old CRC</param>
            /// <param name="x3">Third byte of input after mixing with the old CRC</param>
            /// <param name="x4">Fourth byte of input after mixing with the old CRC</param>
            /// <returns>A new unfinalized checksum value</returns>
            /// <remarks>
            /// <para>
            /// Even though the first four bytes of input are fed in as arguments,
            /// <paramref name="offset"/> should be the same value passed to this
            /// function's caller (either <see cref="UpdateDataForNormalPoly"/> or
            /// <see cref="UpdateDataForReversedPoly"/>). This method will get inlined
            /// into both functions, so using the same offset produces faster code.
            /// </para>
            /// <para>
            /// Because most processors running C# have some kind of instruction-level
            /// parallelism, the order of XOR operations can affect performance. This
            /// ordering assumes that the assembly code generated by the just-in-time
            /// compiler will emit a bunch of arithmetic operations for checking array
            /// bounds. Then it opportunistically XORs a1 and a2 to keep the processor
            /// busy while those other parts of the pipeline handle the range check
            /// calculations.
            /// </para>
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint UpdateDataCommon(byte[] input, int offset, uint[] crcTable, byte x1, byte x2, byte x3, byte x4)
            {
                uint result;
                uint a1 = crcTable[x1 + 3840] ^ crcTable[x2 + 3584];
                uint a2 = crcTable[x3 + 3328] ^ crcTable[x4 + 3072];

                result = crcTable[input[offset + 4] + 2816];
                result ^= crcTable[input[offset + 5] + 2560];
                a1 ^= crcTable[input[offset + 9] + 1536];
                result ^= crcTable[input[offset + 6] + 2304];
                result ^= crcTable[input[offset + 7] + 2048];
                result ^= crcTable[input[offset + 8] + 1792];
                a2 ^= crcTable[input[offset + 13] + 512];
                result ^= crcTable[input[offset + 10] + 1280];
                result ^= crcTable[input[offset + 11] + 1024];
                result ^= crcTable[input[offset + 12] + 768];
                result ^= a1;
                result ^= crcTable[input[offset + 14] + 256];
                result ^= crcTable[input[offset + 15]];
                result ^= a2;

                return result;
            }
        }

        /// <summary>
        /// Interface to compute a data checksum used by checked input/output streams.
        /// A data checksum can be updated by one byte or with a byte array. After each
        /// update the value of the current checksum can be returned by calling
        /// <code>getValue</code>. The complete checksum object can also be reset
        /// so it can be used again with new data.
        /// </summary>
        public interface IChecksum
        {
            /// <summary>
            /// Resets the data checksum as if no update was ever called.
            /// </summary>
            void Reset();

            /// <summary>
            /// Returns the data checksum computed so far.
            /// </summary>
            long Value { get; }

            /// <summary>
            /// Adds one byte to the data checksum.
            /// </summary>
            /// <param name = "bval">
            /// the data value to add. The high byte of the int is ignored.
            /// </param>
            void Update(int bval);

            /// <summary>
            /// Updates the data checksum with the bytes taken from the array.
            /// </summary>
            /// <param name="buffer">
            /// buffer an array of bytes
            /// </param>
            void Update(byte[] buffer);

            /// <summary>
            /// Adds the byte array to the data checksum.
            /// </summary>
            /// <param name = "segment">
            /// The chunk of data to add
            /// </param>
            void Update(ArraySegment<byte> segment);
        }

        /// <summary>
        /// BZip2Exception represents exceptions specific to BZip2 classes and code.
        /// </summary>
        [Serializable]
        public class BZip2Exception : Exception
        {
            /// <summary>
            /// Initialise a new instance of <see cref="BZip2Exception" />.
            /// </summary>
            public BZip2Exception()
            {
            }

            /// <summary>
            /// Initialise a new instance of <see cref="BZip2Exception" /> with its message string.
            /// </summary>
            /// <param name="message">A <see cref="string"/> that describes the error.</param>
            public BZip2Exception(string message)
                : base(message)
            {
            }

            /// <summary>
            /// Initialise a new instance of <see cref="BZip2Exception" />.
            /// </summary>
            /// <param name="message">A <see cref="string"/> that describes the error.</param>
            /// <param name="innerException">The <see cref="Exception"/> that caused this exception.</param>
            public BZip2Exception(string message, Exception innerException)
                : base(message, innerException)
            {
            }

            /// <summary>
            /// Initializes a new instance of the BZip2Exception class with serialized data.
            /// </summary>
            /// <param name="info">
            /// The System.Runtime.Serialization.SerializationInfo that holds the serialized
            /// object data about the exception being thrown.
            /// </param>
            /// <param name="context">
            /// The System.Runtime.Serialization.StreamingContext that contains contextual information
            /// about the source or destination.
            /// </param>
            protected BZip2Exception(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }
        }

        internal static class BZip2Constants
        {
            /// <summary>
            /// Random numbers used to randomise repetitive blocks
            /// </summary>
            public readonly static int[] RandomNumbers =
            {
                619, 720, 127, 481, 931, 816, 813, 233, 566, 247,
                985, 724, 205, 454, 863, 491, 741, 242, 949, 214,
                733, 859, 335, 708, 621, 574, 73, 654, 730, 472,
                419, 436, 278, 496, 867, 210, 399, 680, 480, 51,
                878, 465, 811, 169, 869, 675, 611, 697, 867, 561,
                862, 687, 507, 283, 482, 129, 807, 591, 733, 623,
                150, 238, 59, 379, 684, 877, 625, 169, 643, 105,
                170, 607, 520, 932, 727, 476, 693, 425, 174, 647,
                73, 122, 335, 530, 442, 853, 695, 249, 445, 515,
                909, 545, 703, 919, 874, 474, 882, 500, 594, 612,
                641, 801, 220, 162, 819, 984, 589, 513, 495, 799,
                161, 604, 958, 533, 221, 400, 386, 867, 600, 782,
                382, 596, 414, 171, 516, 375, 682, 485, 911, 276,
                98, 553, 163, 354, 666, 933, 424, 341, 533, 870,
                227, 730, 475, 186, 263, 647, 537, 686, 600, 224,
                469, 68, 770, 919, 190, 373, 294, 822, 808, 206,
                184, 943, 795, 384, 383, 461, 404, 758, 839, 887,
                715, 67, 618, 276, 204, 918, 873, 777, 604, 560,
                951, 160, 578, 722, 79, 804, 96, 409, 713, 940,
                652, 934, 970, 447, 318, 353, 859, 672, 112, 785,
                645, 863, 803, 350, 139, 93, 354, 99, 820, 908,
                609, 772, 154, 274, 580, 184, 79, 626, 630, 742,
                653, 282, 762, 623, 680, 81, 927, 626, 789, 125,
                411, 521, 938, 300, 821, 78, 343, 175, 128, 250,
                170, 774, 972, 275, 999, 639, 495, 78, 352, 126,
                857, 956, 358, 619, 580, 124, 737, 594, 701, 612,
                669, 112, 134, 694, 363, 992, 809, 743, 168, 974,
                944, 375, 748, 52, 600, 747, 642, 182, 862, 81,
                344, 805, 988, 739, 511, 655, 814, 334, 249, 515,
                897, 955, 664, 981, 649, 113, 974, 459, 893, 228,
                433, 837, 553, 268, 926, 240, 102, 654, 459, 51,
                686, 754, 806, 760, 493, 403, 415, 394, 687, 700,
                946, 670, 656, 610, 738, 392, 760, 799, 887, 653,
                978, 321, 576, 617, 626, 502, 894, 679, 243, 440,
                680, 879, 194, 572, 640, 724, 926, 56, 204, 700,
                707, 151, 457, 449, 797, 195, 791, 558, 945, 679,
                297, 59, 87, 824, 713, 663, 412, 693, 342, 606,
                134, 108, 571, 364, 631, 212, 174, 643, 304, 329,
                343, 97, 430, 751, 497, 314, 983, 374, 822, 928,
                140, 206, 73, 263, 980, 736, 876, 478, 430, 305,
                170, 514, 364, 692, 829, 82, 855, 953, 676, 246,
                369, 970, 294, 750, 807, 827, 150, 790, 288, 923,
                804, 378, 215, 828, 592, 281, 565, 555, 710, 82,
                896, 831, 547, 261, 524, 462, 293, 465, 502, 56,
                661, 821, 976, 991, 658, 869, 905, 758, 745, 193,
                768, 550, 608, 933, 378, 286, 215, 979, 792, 961,
                61, 688, 793, 644, 986, 403, 106, 366, 905, 644,
                372, 567, 466, 434, 645, 210, 389, 550, 919, 135,
                780, 773, 635, 389, 707, 100, 626, 958, 165, 504,
                920, 176, 193, 713, 857, 265, 203, 50, 668, 108,
                645, 990, 626, 197, 510, 357, 358, 850, 858, 364,
                936, 638
            };

            /// <summary>
            /// When multiplied by compression parameter (1-9) gives the block size for compression
            /// 9 gives the best compression but uses the most memory.
            /// </summary>
            public const int BaseBlockSize = 100000;

            /// <summary>
            /// Backend constant
            /// </summary>
            public const int MaximumAlphaSize = 258;

            /// <summary>
            /// Backend constant
            /// </summary>
            public const int MaximumCodeLength = 23;

            /// <summary>
            /// Backend constant
            /// </summary>
            public const int RunA = 0;

            /// <summary>
            /// Backend constant
            /// </summary>
            public const int RunB = 1;

            /// <summary>
            /// Backend constant
            /// </summary>
            public const int GroupCount = 6;

            /// <summary>
            /// Backend constant
            /// </summary>
            public const int GroupSize = 50;

            /// <summary>
            /// Backend constant
            /// </summary>
            public const int NumberOfIterations = 4;

            /// <summary>
            /// Backend constant
            /// </summary>
            public const int MaximumSelectors = (2 + (900000 / GroupSize));

            /// <summary>
            /// Backend constant
            /// </summary>
            public const int OvershootBytes = 20;
        }
    }
}