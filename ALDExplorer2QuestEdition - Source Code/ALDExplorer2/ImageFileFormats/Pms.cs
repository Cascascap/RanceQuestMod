﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using FreeImageAPI;
using System.Drawing;

namespace ALDExplorer2.ImageFileFormats
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PmsHeaderRaw
    {
        public UInt16 Signature, Version, HeaderSize;
        public byte ColorDepth, ShadowDepth;
        public UInt16 IsSprite, PaletteBank;
        public int Word0C;
        public int X, Y;
        public int Width, Height;
        public int AddressOfData, AddressOfPalette, AddressOfComment;
    }

    public class PmsHeader : ICloneable, IImageHeader
    {
        public int Signature;
        public int Version;
        public int HeaderSize;
        public int ColorDepth;
        public int ShadowDepth;
        public int IsSprite;
        public int PaletteBank;
        public int Word0C;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public int AddressOfData;
        public int AddressOfPalette;
        public int AddressOfComment;
        public byte[] ExtraData;
        //public string comment;

        public string GetComment()
        {
            return CommentUtil.GetComment(this);
        }

        public bool ParseComment(string comment)
        {
            return CommentUtil.ParseComment(this, comment);
        }

        public PmsHeader Clone()
        {
            var clone = (PmsHeader)MemberwiseClone();
            if (clone.ExtraData != null) clone.ExtraData = (byte[])clone.ExtraData.Clone();
            return clone;
        }

        public bool Validate()
        {
            if (this.Signature != 0x4D50)
            {
                return false;
            }
            if (this.X < 0 || this.Y < 0 || this.Width < 0 || this.Height < 0 || this.X > 65535 || this.X > 65535 || this.Width > 65535 || this.Height > 65535 || this.Width * this.Height >= 256 * 1024 * 1024)
            {
                return false;
            }
            int firstData = Math.Min(this.AddressOfPalette, this.AddressOfData);
            if (firstData == 0) firstData = Math.Max(this.AddressOfPalette, this.AddressOfData);
            int lastHeaderAddress = Math.Max(firstData, this.HeaderSize);
            if (lastHeaderAddress < 44 ||
                (this.AddressOfData < lastHeaderAddress && this.ShadowDepth == 0))
            {
                return false;
            }
            return true;
        }

        #region ICloneable Members

        object ICloneable.Clone()
        {
            return Clone();
        }

        #endregion

        #region IImageHeader Members

        string IImageHeader.Signature
        {
            get
            {
                return BinaryUtil.IntToString(this.Signature);
            }
            set
            {
                this.Signature = BinaryUtil.StringToInt(value);
            }
        }

        int IImageHeader.Version
        {
            get
            {
                return Version;
            }
            set
            {
                Version = value;
            }
        }

        int IImageHeader.X
        {
            get
            {
                return X;
            }
            set
            {
                X = value;
            }
        }

        int IImageHeader.Y
        {
            get
            {
                return Y;
            }
            set
            {
                Y = value;
            }
        }

        int IImageHeader.Width
        {
            get
            {
                return Width;
            }
            set
            {
                Width = value;
            }
        }

        int IImageHeader.Height
        {
            get
            {
                return Height;
            }
            set
            {
                Height = value;
            }
        }

        int IImageHeader.ColorDepth
        {
            get
            {
                return ColorDepth;
            }
            set
            {
                ColorDepth = value;
            }
        }

        public bool HasAlphaChannel
        {
            get
            {
                return ShadowDepth == 8 || (ColorDepth == 16 && AddressOfPalette != 0);
            }
            //set
            //{
            //    if (value == true)
            //    {
            //        ShadowDepth = 8;
            //    }
            //    else
            //    {
            //        ShadowDepth = 0;
            //    }
            //}
        }

        #endregion
    }

    public static class Pms
    {
        public static FreeImageBitmap LoadImage(byte[] bytes)
        {
            var pmsHeader = GetHeader(bytes);
            if (pmsHeader.X < 0 || pmsHeader.Y < 0 || pmsHeader.Width < 0 || pmsHeader.Height < 0)
            {
                return null;
            }
            int firstData = Math.Min(pmsHeader.AddressOfPalette, pmsHeader.AddressOfData);
            if (firstData == 0) firstData = Math.Max(pmsHeader.AddressOfPalette, pmsHeader.AddressOfData);
            int lastHeaderAddress = Math.Max(firstData, pmsHeader.HeaderSize);
            if (lastHeaderAddress < 44 ||
                (pmsHeader.AddressOfData < lastHeaderAddress && pmsHeader.ShadowDepth == 0) ||
                pmsHeader.AddressOfData > bytes.Length ||
                pmsHeader.AddressOfPalette > bytes.Length)
            {
                return null;
            }
            if (pmsHeader.ColorDepth == 16)
            {
                //return null;
                ushort[] imageData = GetImageData16Bit(pmsHeader, bytes);
                byte[] shadowImageData = null;
                if (pmsHeader.AddressOfPalette != 0)
                {
                    if (pmsHeader.ShadowDepth == 8 || pmsHeader.ShadowDepth == 0)
                    {
                        shadowImageData = GetImageData8Bit(pmsHeader, bytes, pmsHeader.AddressOfPalette);
                    }
                }
                FreeImageBitmap bitmap;
                unsafe
                {
                    int[] imageData2 = new int[pmsHeader.Width * pmsHeader.Height];

                    fixed (int* img32 = imageData2)
                    {
                        fixed (ushort* img16 = imageData)
                        {
                            int length = imageData2.Length;
                            if (shadowImageData == null)
                            {
                                for (int i = 0; i < length; i++)
                                {
                                    int p = img16[i];
                                    byte a = 0xFF;
                                    unchecked
                                    {
                                        int r = ((p >> 0) & 0x1F);
                                        int g = ((p >> 5) & 0x3F);
                                        int b = ((p >> 11) & 0x1F);

                                        r = (r * 0x839CE + 0x8000) >> 16;
                                        g = (g * 0x40C30 + 0x8000) >> 16;
                                        b = (b * 0x839CE + 0x8000) >> 16;

                                        p = (r << 0) |
                                            (g << 8) |
                                            (b << 16) |
                                            (a << 24);
                                    }
                                    img32[i] = p;
                                }
                            }
                            else
                            {
                                fixed (byte* img8 = shadowImageData)
                                {
                                    for (int i = 0; i < length; i++)
                                    {
                                        int p = img16[i];
                                        byte a = img8[i];
                                        unchecked
                                        {
                                            int r = ((p >> 0) & 0x1F);
                                            int g = ((p >> 5) & 0x3F);
                                            int b = ((p >> 11) & 0x1F);

                                            r = (r * 0x839CE + 0x8000) >> 16;
                                            g = (g * 0x40C30 + 0x8000) >> 16;
                                            b = (b * 0x839CE + 0x8000) >> 16;

                                            p = (r << 0) |
                                                (g << 8) |
                                                (b << 16) |
                                                (a << 24);
                                        }
                                        img32[i] = p;
                                    }
                                }
                            }
                            bitmap = new FreeImageBitmap(pmsHeader.Width, pmsHeader.Height, pmsHeader.Width * 4, 32, FREE_IMAGE_TYPE.FIT_BITMAP, (IntPtr)img32);

                            //bitmap = new FreeImageBitmap(pmsHeader.width, pmsHeader.height, pmsHeader.width * 2, 16, FREE_IMAGE_TYPE.FIT_BITMAP, (IntPtr)img16);
                        }
                    }
                }
                bitmap.Tag = pmsHeader;
                bitmap.Comment = pmsHeader.GetComment();
                return bitmap;
            }
            else
            {
                byte[] imageData;
                imageData = GetImageData8Bit(pmsHeader, bytes);
                FreeImageBitmap bitmap = new FreeImageBitmap(pmsHeader.Width, pmsHeader.Height, pmsHeader.Width, 8, FREE_IMAGE_TYPE.FIT_BITMAP, imageData);
                GetPalette(bitmap.Palette, pmsHeader, bytes);
                bitmap.Tag = pmsHeader;
                bitmap.Comment = pmsHeader.GetComment();
                return bitmap;
            }

        }

        public static void SaveImage(Stream stream, FreeImageBitmap bitmap)
        {
            string comment = bitmap.Comment;
            var pmsHeader = new PmsHeader();
            bool readComment = false;
            if (!string.IsNullOrEmpty(comment))
            {
                if (pmsHeader.ParseComment(comment))
                {
                    readComment = true;
                }
            }

            if (pmsHeader.ColorDepth == 0)
            {
                pmsHeader.ColorDepth = 8;
            }

            bool is8Bit = !readComment || pmsHeader.ColorDepth == 8;

            if (!readComment)
            {
                pmsHeader.Version = 1;
                pmsHeader.HeaderSize = 48;
            }
            if (pmsHeader.Signature == 0)
            {
                pmsHeader.Signature = 0x00004d50;
            }
            if (pmsHeader.Version == 2 && pmsHeader.HeaderSize == 0)
            {
                pmsHeader.HeaderSize = 64;
            }
            if (pmsHeader.HeaderSize < 48)
            {
                pmsHeader.HeaderSize = 48;
            }
            if (pmsHeader.HeaderSize > 64)
            {
                pmsHeader.HeaderSize = 64;
            }
            pmsHeader.AddressOfComment = 0;
            pmsHeader.Height = bitmap.Height;
            pmsHeader.Width = bitmap.Width;

            if (is8Bit)
            {
                if (bitmap.ColorDepth > 8)
                {
                    if (bitmap.ColorDepth == 32)
                    {
                        bitmap.ConvertColorDepth(FREE_IMAGE_COLOR_DEPTH.FICD_24_BPP);
                    }
                    bitmap.Quantize(FREE_IMAGE_QUANTIZE.FIQ_WUQUANT, 256);
                    //throw new ArgumentException("image must be 8-bit");
                }

                pmsHeader.AddressOfPalette = pmsHeader.HeaderSize;
                pmsHeader.AddressOfData = pmsHeader.AddressOfPalette + 768;
                pmsHeader.ColorDepth = 8;

                SaveHeader(stream, pmsHeader);
                SavePalette(stream, bitmap.Palette);
                SaveImageData8Bit(stream, bitmap);
            }
            else
            {
                bool hasAlphaChannel = false;
                var existingAlphaChannel = bitmap.GetChannel(FREE_IMAGE_COLOR_CHANNEL.FICC_ALPHA);
                if (existingAlphaChannel != null)
                {
                    bool allPixelsOpaque = AllPixelsOpaque(existingAlphaChannel);
                    hasAlphaChannel = !allPixelsOpaque;
                }
                if (bitmap.ColorDepth != 32)
                {
                    bitmap.ConvertColorDepth(FREE_IMAGE_COLOR_DEPTH.FICD_32_BPP);
                }

                bool usingAlphaChannel = pmsHeader.ShadowDepth == 8 || (pmsHeader.ShadowDepth == 0 && hasAlphaChannel);

                pmsHeader.AddressOfPalette = 0;
                pmsHeader.AddressOfData = pmsHeader.HeaderSize;
                pmsHeader.ColorDepth = 16;

                ushort[] image16 = new ushort[pmsHeader.Width * pmsHeader.Height];
                byte[] image8 = null;
                if (usingAlphaChannel)
                {
                    image8 = new byte[pmsHeader.Width * pmsHeader.Height];
                }
                int o = 0;
                for (int y = 0; y < pmsHeader.Height; y++)
                {
                    var scanline = bitmap.GetScanlineFromTop32Bit(y);

                    if (image8 == null)
                    {
                        for (int x = 0; x < pmsHeader.Width; x++)
                        {
                            unchecked
                            {
                                int p = scanline[x];
                                int b = (p >> 0) & 0xFF;
                                int g = (p >> 8) & 0xFF;
                                int r = (p >> 16) & 0xFF;
                                //int a = (p >> 24) & 0xFF;
                                b = (b * 0x1F1F + 0x8000) >> 16;
                                g = (g * 0x3F3F + 0x8000) >> 16;
                                r = (r * 0x1F1F + 0x8000) >> 16;
                                p = (b << 0) |
                                    (g << 5) |
                                    (r << 11);
                                image16[o] = (ushort)p;
                                o++;
                            }
                        }
                    }
                    else
                    {
                        for (int x = 0; x < pmsHeader.Width; x++)
                        {
                            unchecked
                            {
                                int p = scanline[x];
                                int b = (p >> 0) & 0xFF;
                                int g = (p >> 8) & 0xFF;
                                int r = (p >> 16) & 0xFF;
                                int a = (p >> 24) & 0xFF;
                                b = (b * 0x1F1F + 0x8000) >> 16;
                                g = (g * 0x3F3F + 0x8000) >> 16;
                                r = (r * 0x1F1F + 0x8000) >> 16;
                                p = (b << 0) |
                                    (g << 5) |
                                    (r << 11);
                                image16[o] = (ushort)p;
                                image8[o] = (byte)a;
                                o++;
                            }
                        }
                    }
                }
                MemoryStream ms = new MemoryStream();

                SaveImageData16Bit(ms, image16, pmsHeader.Width, pmsHeader.Height);
                int imageSize = (int)ms.Length;
                if (usingAlphaChannel)
                {
                    pmsHeader.AddressOfPalette = imageSize + pmsHeader.AddressOfData;
                }
                SaveHeader(stream, pmsHeader);
                ms.WriteTo(stream);
                ms.SetLength(0);
                if (usingAlphaChannel)
                {
                    SaveImageData8Bit(stream, image8, pmsHeader.Width, pmsHeader.Height);
                }
            }
        }

        private static bool AllPixelsOpaque(FreeImageBitmap existingAlphaChannel)
        {
            //all pixels opaque?
            for (int y = 0; y < existingAlphaChannel.Height; y++)
            {
                var scanline = existingAlphaChannel.GetScanlineFromTop8Bit(y);
                for (int x = 0; x < existingAlphaChannel.Width; x++)
                {
                    if (scanline[x] != 255)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        internal static void SavePalette(Stream stream, Palette palette)
        {
            var bw = new BinaryWriter(stream);
            for (int i = 0; i < 256; i++)
            {
                int r = palette[i].rgbRed;
                int g = palette[i].rgbGreen;
                int b = palette[i].rgbBlue;

                bw.Write((byte)r);
                bw.Write((byte)g);
                bw.Write((byte)b);
            }
        }

        public static PmsHeader GetHeader(byte[] bytes)
        {
            var ms = new MemoryStream(bytes);
            return GetHeader(ms);
        }

        public static PmsHeader GetHeader(Stream stream)
        {
            long zero = stream.Position;
            long streamLength = stream.Length;
            if (stream.Position + 44 > streamLength)
            {
                throw new InvalidDataException("PMS header is too short");
            }

            var br = new BinaryReader(stream);

            PmsHeader pms = new PmsHeader();
            pms.Signature = br.ReadUInt16();
            pms.Version = br.ReadUInt16();
            pms.HeaderSize = br.ReadUInt16();
            pms.ColorDepth = br.ReadByte();
            pms.ShadowDepth = br.ReadByte();
            pms.IsSprite = br.ReadUInt16();
            pms.PaletteBank = br.ReadUInt16();
            pms.Word0C = br.ReadInt32();
            pms.X = br.ReadInt32();
            pms.Y = br.ReadInt32();
            pms.Width = br.ReadInt32();
            pms.Height = br.ReadInt32();
            pms.AddressOfData = br.ReadInt32();
            pms.AddressOfPalette = br.ReadInt32();
            pms.AddressOfComment = br.ReadInt32();

            int lastHeaderAddress = Math.Max(pms.HeaderSize, pms.AddressOfPalette);
            int lastHeaderAddress2 = Math.Max(pms.HeaderSize, pms.AddressOfData);
            lastHeaderAddress = Math.Min(lastHeaderAddress, lastHeaderAddress2);
            if (lastHeaderAddress > streamLength - zero)
            {
                lastHeaderAddress = (int)(streamLength - zero);
            }

            int extraDataSize = lastHeaderAddress - (int)(stream.Position - zero);
            if (extraDataSize >= 1024 * 1024)
            {
                throw new InvalidDataException("Too much extra data for PMS file");
            }

            if (extraDataSize >= 0)
            {
                pms.ExtraData = br.ReadBytes(extraDataSize);
            }
            //if (pms.addressOfComment != 0)
            //{
            //    ms.Position = pms.addressOfComment;
            //    pms.comment = br.ReadStringNullTerminated();
            //}

            return pms;
        }

        public static void SaveHeader(Stream stream, PmsHeader pms)
        {
            long startPosition = stream.Position;

            var bw = new BinaryWriter(stream);
            bw.Write((ushort)pms.Signature);
            //bw.Write(ASCIIEncoding.ASCII.GetBytes("PM"));
            bw.Write((ushort)pms.Version);
            bw.Write((ushort)pms.HeaderSize);
            bw.Write((byte)pms.ColorDepth);
            bw.Write((byte)pms.ShadowDepth);
            bw.Write((ushort)pms.IsSprite);
            bw.Write((ushort)pms.PaletteBank);
            bw.Write((int)pms.Word0C);
            bw.Write((int)pms.X);
            bw.Write((int)pms.Y);
            bw.Write((int)pms.Width);
            bw.Write((int)pms.Height);
            bw.Write((int)pms.AddressOfData);
            bw.Write((int)pms.AddressOfPalette);
            bw.Write((int)pms.AddressOfComment);

            long endPosition = stream.Position;
            int currentLength = (int)(endPosition - startPosition);
            if (currentLength < pms.HeaderSize)
            {
                int extraDataLength = pms.HeaderSize - currentLength;
                if (pms.ExtraData != null && pms.ExtraData.Length >= extraDataLength)
                {
                    stream.Write(pms.ExtraData, 0, extraDataLength);
                }
                else if (pms.ExtraData != null)
                {
                    //shouldn't ever happen
                    int lengthFromArray = pms.ExtraData.Length;
                    stream.Write(pms.ExtraData, 0, lengthFromArray);
                    stream.WriteZeroes(extraDataLength - lengthFromArray);
                }
                else
                {
                    stream.WriteZeroes(extraDataLength);
                }
            }
        }

        /*
         * Get palette from raw data
         *   pal: palette to be stored 
         *   b  : raw data (pointer to palette)
        */
        internal static void GetPalette(Palette palette, PmsHeader pmsHeader, byte[] bytes)
        {
            int address = pmsHeader.AddressOfPalette;
            int i;

            for (i = 0; i < 256; i++)
            {
                palette[i] = Color.FromArgb(bytes[address + i * 3 + 0], bytes[address + i * 3 + 1], bytes[address + i * 3 + 2]);
            }
        }

        static void memcpy(byte[] dest, int destIndex, byte[] src, int srcIndex, int length)
        {
            if (length + destIndex > dest.Length)
            {
                length = dest.Length - destIndex;
            }
            Array.Copy(src, srcIndex, dest, destIndex, length);

            //for (int i = 0; i < length; i++)
            //{
            //    dest[destIndex + i] = src[srcIndex + i];
            //}
        }

        static void memset(byte[] bytes, int index, byte value, int count)
        {
            for (int i = 0; i < count; i++)
            {
                bytes[index + i] = value;
            }
        }


        /*
         * Do extract 8bit pms image
         *   pms: pms header information
         *   pic: pixel to be stored
         *   b  : raw data (pointer to pixel)
        */
        internal static byte[] GetImageData8Bit(PmsHeader pms, byte[] bytes)
        {
            return GetImageData8Bit(pms, bytes, pms.AddressOfData);
        }
        internal static byte[] GetImageData8Bit(PmsHeader pms, byte[] bytes, int addressOfData)
        {
            byte[] pic = new byte[pms.Width * pms.Height];
            int address = addressOfData;
            int c0, c1;
            int x, y, loc, l, i;
            int scanline = pms.Width;

            for (y = 0; y < pms.Height; y++)
            {
                for (x = 0; x < pms.Width; )
                {
                    int a0 = address;
                    loc = y * scanline + x;
                    c0 = bytes[address++];
                    if (c0 <= 0xf7)
                    {
                        //literal
                        pic[loc] = (byte)c0; x++;
                    }
                    else if (c0 == 0xff)
                    {
                        //copy N+3 bytes from previous scanline
                        l = bytes[address] + 3; x += l; address++;
                        memcpy(pic, loc, pic, loc - scanline, l);
                    }
                    else if (c0 == 0xfe)
                    {
                        //copy N+3 bytes from two scanlines ago
                        l = bytes[address] + 3; x += l; address++;
                        memcpy(pic, loc, pic, loc - scanline * 2, l);
                    }
                    else if (c0 == 0xfd)
                    {
                        //fill with N+4 bytes of the next value
                        l = bytes[address] + 4; x += l; address++;
                        c0 = bytes[address++];
                        memset(pic, loc, (byte)c0, l);
                    }
                    else if (c0 == 0xfc)
                    {
                        //fill with alternating bytes N+3 times
                        l = (bytes[address] + 3) * 2; x += l; address++;
                        c0 = bytes[address++]; c1 = bytes[address++];
                        for (i = 0; i < l; i += 2)
                        {
                            pic[loc + i] = (byte)c0;
                            pic[loc + i + 1] = (byte)c1;
                        }
                    }
                    else
                    {
                        //copy byte
                        pic[loc] = bytes[address++]; x++;
                    }
                    if (x > pms.Width)
                    {

                    }
                }
            }
            return pic;
        }

        internal static ushort[] GetImageData16Bit(PmsHeader pmsHeader, byte[] bytes)
        {
            ushort[] pic = new ushort[pmsHeader.Width * pmsHeader.Height];
            int bytePosition = pmsHeader.AddressOfData;

            int c0, c1, pc0, pc1;
            int x, y, i, l, loc;
            int scanline = pmsHeader.Width;

            for (y = 0; y < pmsHeader.Height; y++)
            {
                for (x = 0; x < pmsHeader.Width; )
                {
                    loc = y * scanline + x;
                    c0 = bytes[bytePosition++];
                    if (c0 <= 0xf7)
                    {
                        c1 = bytes[bytePosition++]; x++;
                        pic[loc] = (ushort)(c0 | (c1 << 8));
                    }
                    else if (c0 == 0xff)
                    {
                        l = bytes[bytePosition] + 2; x += l; bytePosition++;
                        for (i = 0; i < l; i++)
                        {
                            pic[loc + i] = pic[loc + i - scanline];
                        }
                    }
                    else if (c0 == 0xfe)
                    {
                        l = bytes[bytePosition] + 2; x += l; bytePosition++;
                        for (i = 0; i < l; i++)
                        {
                            pic[loc + i] = pic[loc + i - scanline * 2];
                        }
                    }
                    else if (c0 == 0xfd)
                    {
                        l = bytes[bytePosition] + 3; x += l; bytePosition++;
                        c0 = bytes[bytePosition++]; c1 = bytes[bytePosition++];
                        pc0 = c0 | (c1 << 8);
                        for (i = 0; i < l; i++)
                        {
                            pic[loc + i] = (ushort)pc0;
                        }
                    }
                    else if (c0 == 0xfc)
                    {
                        l = (bytes[bytePosition] + 2) * 2; x += l; bytePosition++;
                        c0 = bytes[bytePosition++]; c1 = bytes[bytePosition++]; pc0 = c0 | (c1 << 8);
                        c0 = bytes[bytePosition++]; c1 = bytes[bytePosition++]; pc1 = c0 | (c1 << 8);
                        for (i = 0; i < l; i += 2)
                        {
                            pic[loc + i] = (ushort)pc0;
                            pic[loc + i + 1] = (ushort)pc1;
                        }
                    }
                    else if (c0 == 0xfb)
                    {
                        x++;
                        pic[loc] = pic[loc - scanline - 1];
                    }
                    else if (c0 == 0xfa)
                    {
                        x++;
                        pic[loc] = pic[loc - scanline + 1];
                    }
                    else if (c0 == 0xf9)
                    {
                        l = bytes[bytePosition] + 1; x += l; bytePosition++;
                        c0 = bytes[bytePosition++]; c1 = bytes[bytePosition++];
                        pc0 = ((c0 & 0xe0) << 8) + ((c0 & 0x18) << 6) + ((c0 & 0x07) << 2);
                        pc1 = ((c1 & 0xc0) << 5) + ((c1 & 0x3c) << 3) + (c1 & 0x03);
                        pic[loc] = (ushort)(pc0 + pc1);
                        for (i = 1; i < l; i++)
                        {
                            c1 = bytes[bytePosition++];
                            pc1 = ((c1 & 0xc0) << 5) + ((c1 & 0x3c) << 3) + (c1 & 0x03);
                            pic[loc + i] = (ushort)(pc0 | pc1);
                        }
                    }
                    else
                    {
                        c0 = bytes[bytePosition++]; c1 = bytes[bytePosition++]; x++;
                        pic[loc] = (ushort)(c0 | (c1 << 8));
                    }
                }
            }
            return pic;
            //ushort[] pic = new ushort[pmsHeader.width * pmsHeader.height];
            //int c0, c1, pc0, pc1;
            //int x, y, i, l, loc;
            //int position = 0;
            //int scanline = pmsHeader.width;

            //for (y = 0; y < pmsHeader.height; y++)
            //{
            //    for (x = 0; x < pmsHeader.width; )
            //    {
            //        loc = y * scanline + x;
            //        c0 = bytes[position++];
            //        if (c0 <= 0xf7)
            //        {
            //            c1 = bytes[position++]; x++;
            //            pic[loc] = (ushort)(c0 | (c1 << 8));
            //        }
            //        else if (c0 == 0xff)
            //        {
            //            c1 = bytes[position++];
            //            //if (loc - scanline < 0)
            //            //{
            //            //    pic[loc] = (ushort)(c0 | (c1 << 8));
            //            //    x++;
            //            //}
            //            //else
            //            {
            //                l = c1 + 2; x += l; position++;
            //                for (i = 0; i < l; i++)
            //                {
            //                    pic[loc + i] = pic[loc + i - scanline];
            //                }
            //            }
            //        }
            //        else if (c0 == 0xfe)
            //        {
            //            l = bytes[position] + 2; x += l; position++;
            //            for (i = 0; i < l; i++)
            //            {
            //                pic[loc + i] = pic[loc + i - scanline * 2];
            //            }
            //        }
            //        else if (c0 == 0xfd)
            //        {
            //            l = bytes[position] + 3; x += l; position++;
            //            c0 = bytes[position++]; c1 = bytes[position++];
            //            pc0 = c0 | (c1 << 8);
            //            for (i = 0; i < l; i++)
            //            {
            //                pic[loc + i] = (ushort)pc0;
            //            }
            //        }
            //        else if (c0 == 0xfc)
            //        {
            //            l = (bytes[position] + 2) * 2; x += l; position++;
            //            c0 = bytes[position++]; c1 = bytes[position++]; pc0 = c0 | (c1 << 8);
            //            c0 = bytes[position++]; c1 = bytes[position++]; pc1 = c0 | (c1 << 8);
            //            for (i = 0; i < l; i += 2)
            //            {
            //                pic[loc + i] = (ushort)pc0;
            //                pic[loc + i + 1] = (ushort)pc1;
            //            }
            //        }
            //        else if (c0 == 0xfb)
            //        {
            //            x++;
            //            pic[loc] = pic[loc - scanline - 1];
            //        }
            //        else if (c0 == 0xfa)
            //        {
            //            x++;
            //            pic[loc] = pic[loc - scanline + 1];
            //        }
            //        else if (c0 == 0xf9)
            //        {
            //            l = bytes[position] + 1; x += l; position++;
            //            c0 = bytes[position++]; c1 = bytes[position++];
            //            pc0 = ((c0 & 0xe0) << 8) + ((c0 & 0x18) << 6) + ((c0 & 0x07) << 2);
            //            pc1 = ((c1 & 0xc0) << 5) + ((c1 & 0x3c) << 3) + (c1 & 0x03);
            //            pic[loc] = (ushort)(pc0 + pc1);
            //            for (i = 1; i < l; i++)
            //            {
            //                c1 = bytes[position++];
            //                pc1 = ((c1 & 0xc0) << 5) + ((c1 & 0x3c) << 3) + (c1 & 0x03);
            //                pic[loc + i] = (ushort)(pc0 | pc1);
            //            }
            //        }
            //        else
            //        {
            //            c0 = bytes[position++]; c1 = bytes[position++]; x++;
            //            pic[loc] = (ushort)(c0 | (c1 << 8));
            //        }
            //    }
            //}
            //return pic;
        }

        internal static void SaveImageData8Bit(Stream stream, FreeImageBitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            byte[] pic = new byte[width * height];

            int o = 0;
            for (int y = 0; y < height; y++)
            {
                var scanline = bitmap.GetScanlineFromTop8Bit(y);
                for (int x = 0; x < width; x++)
                {
                    pic[o++] = scanline[x];
                }
            }
            SaveImageData8Bit(stream, pic, width, height);
        }

        internal static void SaveImageData8Bit(Stream stream, byte[] pic, int width, int height)
        {
            int[] sizes = new int[4];

            for (int i = 0; i < pic.Length; i++)
            {
                //find one of these: RLE run, previous scanline run, two scanlines ago run, alternating RLE run
                int rleLength1 = MeasureRleRun(pic, i, width);
                int rleLength2 = MeasureRleRun2(pic, i, width);
                int prevScanlineLength = MeasurePreviousScanlineRun(pic, i, width, width);
                int prevScanlineLength2 = MeasurePreviousScanlineRun(pic, i, width, width * 2);

                if (rleLength1 < 4) rleLength1 = -1;
                if (rleLength2 < 6) rleLength2 = -1;
                if (prevScanlineLength < 3) prevScanlineLength = -1;
                if (prevScanlineLength2 < 3) prevScanlineLength2 = -1;

                sizes[0] = rleLength1;
                sizes[1] = rleLength2;
                sizes[2] = prevScanlineLength;
                sizes[3] = prevScanlineLength2;

                int maxIndex;
                int max = sizes.Max(out maxIndex);

                if (max < 3)
                {
                    maxIndex = -1;
                }

                byte b = pic[i];

                switch (maxIndex)
                {
                    case 0:
                        {
                            stream.WriteByte(0xFD);
                            stream.WriteByte((byte)(max - 4));
                            stream.WriteByte(b);
                            i += max;
                        }
                        break;
                    case 1:
                        {
                            stream.WriteByte(0xFC);
                            stream.WriteByte((byte)((max - 6) / 2));
                            stream.WriteByte(b);
                            stream.WriteByte(pic[i + 1]);
                            i += max;
                        }
                        break;
                    case 2:
                        {
                            stream.WriteByte(0xFF);
                            stream.WriteByte((byte)(max - 3));
                            i += max;
                        }
                        break;
                    case 3:
                        {
                            stream.WriteByte(0xFE);
                            stream.WriteByte((byte)(max - 3));
                            i += max;
                        }
                        break;
                    default:
                        {
                            if (b <= 0xF7)
                            {
                                stream.WriteByte(b);
                            }
                            else
                            {
                                stream.WriteByte(0xF8);
                                stream.WriteByte(b);
                            }
                            i++;
                        }
                        break;
                }
                i--;
            }
        }

        static int MeasureRleRun(byte[] pic, int i, int width)
        {
            int maxLength = 259;
            int x = i % width;
            if (maxLength > width - x)
            {
                maxLength = width - x;
            }

            byte b = pic[i];
            int i0 = i;
            i++;
            while (i < pic.Length && pic[i] == b)
            {
                if (i - i0 >= maxLength) break;
                i++;
            }
            return i - i0;
        }

        static int MeasureRleRun2(byte[] pic, int i, int width)
        {
            int maxLength = 516;
            int x = i % width;
            if (maxLength > width - x)
            {
                maxLength = width - x;
            }

            int i0 = i;
            if (i + 1 >= pic.Length) return 0;
            if (i + 2 == pic.Length) return 2;
            if (maxLength < 6) return 0;
            byte b1 = pic[i];
            byte b2 = pic[i + 1];
            i += 2;
            while (i + 1 < pic.Length && pic[i] == b1 && pic[i + 1] == b2)
            {
                if (i - i0 + 1 >= maxLength) break;
                i += 2;
            }
            return i - i0;
        }

        static int MeasurePreviousScanlineRun(byte[] pic, int i, int width, int offset)
        {
            int maxLength = 258;
            int x = i % width;
            if (maxLength > width - x)
            {
                maxLength = width - x;
            }

            int i0 = i;
            if (i - offset < 0)
            {
                return 0;
            }
            while (i < pic.Length && pic[i] == pic[i - offset])
            {
                if (i - i0 >= maxLength) break;
                i++;
            }
            return i - i0;
        }

        internal static void SaveImageData16Bit(Stream stream, ushort[] pic, int width, int height)
        {
            //<F7 = raw byte
            //FF: length = b+2, from previous scanline
            //FE: length = b+2, from two scanlines ago
            //FD: length = b+3, RLE run
            //FC: length = (b+2)*2, alternating RLE run
            //FB: one from previous scanline - 1
            //FA: one from previous scanline + 1
            //F9: length = b+1, 3 high bits for B, 2 high bits for G, 3 high bits for R,
            //                  read 2 low bits for B, 4 low bits for G, 3 low bits for R
            //F8: literal

            int[] sizes = new int[4];

            //first pass: encode anything that isn't a run as F8
            MemoryStream ms = new MemoryStream();
            int i = 0;
            for (i = 0; i < pic.Length; i++)
            {
                //find one of these: RLE run, previous scanline run, two scanlines ago run, alternating RLE run
                int rleLength1 = MeasureRleRun(pic, i, width);
                int rleLength2 = MeasureRleRun2(pic, i, width);
                int prevScanlineLength = MeasurePreviousScanlineRun(pic, i, width, width);
                int prevScanlineLength2 = MeasurePreviousScanlineRun(pic, i, width, width * 2);

                if (rleLength1 < 3) rleLength1 = -1;
                if (rleLength2 < 4) rleLength2 = -1;
                if (prevScanlineLength < 2) prevScanlineLength = -1;
                if (prevScanlineLength2 < 2) prevScanlineLength2 = -1;

                sizes[0] = rleLength1;
                sizes[1] = rleLength2;
                sizes[2] = prevScanlineLength;
                sizes[3] = prevScanlineLength2;

                int maxIndex;
                int max = sizes.Max(out maxIndex);

                if (max < 3)
                {
                    maxIndex = -1;
                }

                ushort b = pic[i];

                switch (maxIndex)
                {
                    case 0:
                        {
                            //RLE
                            ms.WriteByte(0xFD);
                            ms.WriteByte((byte)(max - 3));
                            //stream.WriteByte(b);
                            i += max;
                        }
                        break;
                    case 1:
                        {
                            //Alternating RLE
                            ms.WriteByte(0xFC);
                            ms.WriteByte((byte)((max - 4) / 2));
                            //stream.WriteByte(b);
                            //stream.WriteByte(pic[i + 1]);
                            i += max;
                        }
                        break;
                    case 2:
                        {
                            ms.WriteByte(0xFF);
                            ms.WriteByte((byte)(max - 2));
                            i += max;
                        }
                        break;
                    case 3:
                        {
                            ms.WriteByte(0xFE);
                            ms.WriteByte((byte)(max - 2));
                            i += max;
                        }
                        break;
                    default:
                        {
                            ms.WriteByte(0xF8);
                            i++;
                        }
                        break;
                }
                i--;
            }
            BinaryWriter bw = new BinaryWriter(stream);

            ////JUNK PASS: all literals
            //for (i = 0; i < pic.Length; i++)
            //{
            //    bw.Write((byte)0xF8);
            //    bw.Write((ushort)pic[i]);
            //}
            //return;


            //pass #2
            ms.Position = 0;
            i = 0;
            while (ms.Position < ms.Length)
            {
                int literalCount = 0;
                int b = ms.PeekByte();
                if (b == 0xF8)
                {
                    while (true)
                    {
                        literalCount++;
                        b = ms.ReadByte();
                        b = ms.PeekByte();
                        if (b != 0xF8)
                        {
                            break;
                        }
                    }
                    //process a bunch of literals
                    int literalsRemaining = literalCount;
                    while (literalsRemaining > 0)
                    {
                        int commonCount = GetCommonCount(pic, i, width);
                        if (commonCount > literalsRemaining) commonCount = literalsRemaining;
                        if (commonCount >= 3)
                        {
                            bw.Write((byte)0xF9);
                            bw.Write((byte)(commonCount - 1));

                            //const int mask2 = 0x19E3;
                            int p, r, g;
                            p = pic[i];
                            b = (p >> 0) & 0x1F;
                            g = (p >> 5) & 0x3F;
                            r = (p >> 11) & 0x1F;

                            int highRGB = ((b >> 2) << 0) |
                                ((g >> 4) << 3) |
                                ((r >> 2) << 5);

                            bw.Write((byte)highRGB);

                            for (int c = 0; c < commonCount; c++)
                            {
                                p = pic[i];
                                b = (p >> 0) & 0x1F;
                                g = (p >> 5) & 0x3F;
                                r = (p >> 11) & 0x1F;
                                int lowRGB = ((b & 0x03) << 0) |
                                    ((g & 0x0F) << 2) |
                                    ((r & 0x03) << 6);
                                bw.Write((byte)lowRGB);

                                i++;
                            }

                            literalsRemaining -= commonCount;
                        }
                        else
                        {
                            int p = pic[i];
                            int x = i % width;
                            if (x > 0 && i - width - 1 >= 0 && pic[i - width - 1] == p)
                            {
                                bw.Write((byte)0xFB);
                            }
                            else if (x < width && i - width + 1 >= 0 && pic[i - width + 1] == p)
                            {
                                bw.Write((byte)0xFA);
                            }
                            else
                            {
                                if ((p & 0xFF) < 0xF8)
                                {
                                    bw.Write((ushort)p);
                                }
                                else
                                {
                                    bw.Write((byte)0xF8);
                                    bw.Write((ushort)p);
                                }
                            }
                            i++;
                            literalsRemaining--;
                        }
                    }
                }
                else
                {
                    b = ms.ReadByte();
                    //process the tag
                    int lengthByte = ms.ReadByte();
                    switch (b)
                    {
                        case 0xFD:
                            //RLE
                            bw.Write((byte)b);
                            bw.Write((byte)lengthByte);
                            bw.Write((ushort)pic[i]);
                            i += lengthByte + 3;
                            break;
                        case 0xFC:
                            //Alternating RLE
                            bw.Write((byte)b);
                            bw.Write((byte)lengthByte);
                            bw.Write((ushort)pic[i]);
                            bw.Write((ushort)pic[i + 1]);
                            i += lengthByte * 2 + 4;
                            break;
                        case 0xFF:
                            //From previous scanline
                            bw.Write((byte)b);
                            bw.Write((byte)lengthByte);
                            i += lengthByte + 2;
                            break;
                        case 0xFE:
                            //from two scanlines ago
                            bw.Write((byte)b);
                            bw.Write((byte)lengthByte);
                            i += lengthByte + 2;
                            break;
                    }
                }
            }
        }

        static int MeasureRleRun(ushort[] pic, int i, int width)
        {
            int maxLength = 258;
            int x = i % width;
            if (maxLength > width - x)
            {
                maxLength = width - x;
            }

            ushort b = pic[i];
            int i0 = i;
            i++;
            while (i < pic.Length && pic[i] == b)
            {
                if (i - i0 >= maxLength) break;
                i++;
            }
            return i - i0;
        }

        static int MeasureRleRun2(ushort[] pic, int i, int width)
        {
            int maxLength = 514;
            int x = i % width;
            if (maxLength > width - x)
            {
                maxLength = width - x;
            }

            int i0 = i;
            if (i + 1 >= pic.Length) return 0;
            if (i + 2 == pic.Length) return 2;
            if (maxLength < 6) return 0;
            ushort b1 = pic[i];
            ushort b2 = pic[i + 1];
            i += 2;
            while (i + 1 < pic.Length && pic[i] == b1 && pic[i + 1] == b2)
            {
                if (i - i0 + 1 >= maxLength) break;
                i += 2;
            }
            return i - i0;
        }

        static int MeasurePreviousScanlineRun(ushort[] pic, int i, int width, int offset)
        {
            int maxLength = 257;
            int x = i % width;
            if (maxLength > width - x)
            {
                maxLength = width - x;
            }

            int i0 = i;
            if (i - offset < 0)
            {
                return 0;
            }
            while (i < pic.Length && pic[i] == pic[i - offset])
            {
                if (i - i0 >= maxLength) break;
                i++;
            }
            return i - i0;
        }

        private static int GetCommonCount(ushort[] pic, int i, int width)
        {
            const int mask = 0xE61C;
            int maxLength = 256;
            int x = i % width;
            if (maxLength > width - x)
            {
                maxLength = width - x;
            }
            int i0 = i;

            int first = pic[i] & mask;
            while (i < pic.Length && ((pic[i] & mask) == first))
            {
                if (i - i0 >= maxLength) break;
                i++;
            }
            return i - i0;
        }


    }

}
