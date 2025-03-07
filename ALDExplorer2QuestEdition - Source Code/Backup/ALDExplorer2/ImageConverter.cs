﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using FreeImageAPI;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using ZLibNet;
using System.Diagnostics;
using System.Globalization;

namespace ALDExplorer2
{
    using ImageFileFormats;
    public interface IImageHeader
    {
        string Signature { get; set; }
        int Version { get; set; }
        int X { get; set; }
        int Y { get; set; }
        int Width { get; set; }
        int Height { get; set; }
        int ColorDepth { get; set; }
        bool HasAlphaChannel { get; }

        string GetComment();
        bool ParseComment(string comment);
    }

    public class CommentUtil
    {
        public static string GetComment(object obj)
        {
            var type = obj.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            fields = GetIntStringAndByteArrayFields(fields);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var fieldValue = field.GetValue(obj);
                if (fieldValue != null)
                {
                    sb.Append(field.Name);
                    sb.Append(" = ");
                    string stringValue = fieldValue.ToString();
                    if (field.FieldType == typeof(string))
                    {
                        stringValue = stringValue.Replace("\\", "\\\\");
                        stringValue = stringValue.Replace("\"", "\\\"");
                        stringValue = stringValue.Replace("\n", "\\n");
                        stringValue = stringValue.Replace("\r", "\\r");
                        stringValue = stringValue.Replace("\t", "\\t");
                        stringValue = "\"" + stringValue + "\"";
                    }
                    if (field.FieldType == typeof(byte[]))
                    {
                        var byteArray = fieldValue as byte[];
                        stringValue = "\"" + byteArray.ToHexString() + "\"";
                    }
                    sb.Append(stringValue);
                }
                if (i < fields.Length - 1)
                {
                    sb.Append(", ");
                }
            }
            return sb.ToString();
        }

        private static FieldInfo[] GetIntStringAndByteArrayFields(FieldInfo[] fields)
        {
            fields = fields.Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(string) || f.FieldType == typeof(byte[])).ToArray();
            return fields;
        }

        public static string ReadToken(TextReader tr)
        {
            StringBuilder sb = new StringBuilder();

            //eat white space
            int c = tr.Peek();
            if (c == -1)
            {
                return null;
            }
            while (Char.IsWhiteSpace((char)c))
            {
                c = tr.Read();
                c = tr.Peek();
                if (c == -1)
                {
                    return null;
                }
            }
            if (c == '=' || c == ',')
            {
                c = tr.Read();
                sb.Append((char)c);
            }
            else if (c == '"')
            {
                c = tr.Read();
                //read a quoted string
                while (true)
                {
                    c = tr.Read();
                    if (c == '\\')
                    {
                        c = tr.Read();
                        switch (c)
                        {
                            case 'r':
                                sb.Append('\r');
                                break;
                            case 'n':
                                sb.Append('\n');
                                break;
                            case 't':
                                sb.Append('\t');
                                break;
                            default:
                                sb.Append((char)c);
                                break;
                        }
                    }
                    else if (c == '"')
                    {
                        break;
                    }
                    else if (c == -1)
                    {
                        return null;
                    }
                    sb.Append((char)c);
                }
            }
            else if (c >= '0' && c <= '9')
            {
                while (c >= '0' && c <= '9')
                {
                    c = tr.Read();
                    sb.Append((char)c);
                    c = tr.Peek();
                }
            }
            else
            {
                while (!Char.IsWhiteSpace((char)c) && !(c == ',' || c == '=') && c != -1)
                {
                    c = tr.Read();
                    sb.Append((char)c);
                    c = tr.Peek();
                }
            }
            return sb.ToString();
        }

        public static bool ParseComment(object obj, string comment)
        {
            if (comment == null) return false;
            var sr = new StringReader(comment);
            var fields = GetIntStringAndByteArrayFields(obj.GetType().GetFields());
            var dic = new Dictionary<string, FieldInfo>();
            foreach (var field in fields)
            {
                dic.Add(field.Name.ToUpperInvariant(), field);
            }

            FieldInfo currentField = null;
            for (string token = ReadToken(sr); token != null; token = ReadToken(sr))
            {
                if (dic.ContainsKey(token.ToUpperInvariant()))
                {
                    currentField = dic[token.ToUpperInvariant()];
                }
                else if (token == "=")
                {
                    token = ReadToken(sr);
                    if (token == null) return false;
                    if (currentField == null) return false;
                    if (currentField.FieldType == typeof(int))
                    {
                        int intValue;
                        if (int.TryParse(token, out intValue))
                        {
                            currentField.SetValue(obj, intValue);
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else if (currentField.FieldType == typeof(string))
                    {
                        currentField.SetValue(obj, token);
                    }
                    else if (currentField.FieldType == typeof(byte[]))
                    {
                        var bytes = BinaryUtil.GetBytesFromHexString(token);
                        if (bytes == null)
                        {
                            return false;
                        }
                        currentField.SetValue(obj, bytes);
                    }
                    currentField = null;
                }
                else if (token == ",")
                {

                }
                else
                {
                    return false;
                }
            }
            return true;
        }
    }

    public partial class ImageConverter
    {
        public static FreeImageBitmap LoadVsp(byte[] bytes)
        {
            try
            {
                return Vsp.LoadImage(bytes);
            }
            catch
            {
                return null;
            }
        }

        public static FreeImageBitmap LoadPms(byte[] bytes)
        {
            if (Debugger.IsAttached)
            {
                return Pms.LoadImage(bytes);
            }

            try
            {
                return Pms.LoadImage(bytes);
            }
            catch
            {
                return null;
            }
        }

        public static FreeImageBitmap LoadQnt(byte[] bytes)
        {
            try
            {
                return Qnt.LoadImage(bytes);
            }
            catch
            {
                return null;
            }
        }

        public static QntHeader LoadQntHeader(Stream stream)
        {
            var qntHeader = Qnt.GetQntHeader(stream);
            if (qntHeader == null || !qntHeader.Validate())
            {
                return null;
            }
            return qntHeader;
        }

        public static PmsHeader LoadPmsHeader(Stream stream)
        {
            long zero = stream.Position;
            long streamLength = stream.Length - zero;
            PmsHeader pmsHeader = null;

            try
            {
                pmsHeader = Pms.GetHeader(stream);
            }
            catch
            {
                return null;
            }

            if (pmsHeader.X < 0 || pmsHeader.Y < 0 || pmsHeader.Width < 0 || pmsHeader.Height < 0)
            {
                return null;
            }
            int firstData = Math.Min(pmsHeader.AddressOfPalette, pmsHeader.AddressOfData);
            if (firstData == 0) firstData = Math.Max(pmsHeader.AddressOfPalette, pmsHeader.AddressOfData);
            int lastHeaderAddress = Math.Max(firstData, pmsHeader.HeaderSize);

            if (lastHeaderAddress < 44 ||
                (pmsHeader.AddressOfData < lastHeaderAddress && pmsHeader.ShadowDepth == 0) ||
                pmsHeader.AddressOfData > streamLength ||
                pmsHeader.AddressOfPalette > streamLength)
            {
                return null;
            }

            return pmsHeader;
        }

        public static FreeImageBitmap LoadDcf(byte[] bytes)
        {
            try
            {
                return Dcf.LoadImage(bytes);
            }
            catch
            {
                return null;
            }
        }

        public static void SaveVsp(Stream stream, FreeImageBitmap bitmap)
        {
            Vsp.SaveImage(stream, bitmap);
        }

        public static void SavePms(Stream stream, FreeImageBitmap bitmap)
        {
            Pms.SaveImage(stream, bitmap);
        }

        public static void SaveQnt(Stream stream, FreeImageBitmap bitmap)
        {
            Qnt.SaveImage(stream, bitmap);
        }

        public static FreeImageBitmap LoadAjp(byte[] bytes)
        {
            try
            {
                return Ajp.LoadImage(bytes);
            }
            catch
            {
                return null;
            }
        }

        public static FreeImageBitmap LoadAjp(Stream stream)
        {
            try
            {
                return Ajp.LoadImage(stream);
            }
            catch
            {
                return null;
            }
        }

        public static AjpHeader LoadAjpHeader(Stream stream)
        {
            long oldPosition = stream.Position;
            var br = new BinaryReader(stream);
            var ajpHeader = Ajp.ReadAjpHeader(br);
            stream.Position = oldPosition;
            return ajpHeader;
        }

        public static void LoadAjp(byte[] bytes, out byte[] jpegFile, out FreeImageBitmap alpha, out AjpHeader ajpHeader)
        {
            Ajp.LoadImage(bytes, out jpegFile, out alpha, out ajpHeader);
        }

        public static void LoadAjp(Stream stream, out byte[] jpegFile, out FreeImageBitmap alpha, out AjpHeader ajpHeader)
        {
            Ajp.LoadImage(stream, out jpegFile, out alpha, out ajpHeader);
        }

        public static bool PaletteMatches(Palette palette1, Palette palette2, int startColor, int endColor)
        {
            for (int i = startColor; i < endColor; i++)
            {
                if ((palette1[i].uintValue & 0xFFFFFF) != (palette2[i].uintValue & 0xFFFFFF))
                {
                    return false;
                }
            }
            return true;
        }

        public static void RemapPalette(FreeImageBitmap bitmap, Palette newPalette)
        {
            if (bitmap.ColorDepth > 8)
            {
                bitmap.Quantize(FREE_IMAGE_QUANTIZE.FIQ_WUQUANT, 256, newPalette);
            }

            var sourcePal = bitmap.Palette.AsArray.Select(c => c.uintValue).ToArray();
            var destPal = newPalette.AsArray.Select(c => c.uintValue).ToArray();
            int[] srcToDest = new int[256];
            for (int i = 0; i < 256; i++)
            {
                srcToDest[i] = -1;
            }

            //first map identical colors
            {
                Dictionary<int, int> rgbToPaletteIndex = new Dictionary<int, int>();
                for (int i = 0; i < 256; i++)
                {
                    int c = (int)(destPal[i] & 0xFFFFFF);
                    if (!rgbToPaletteIndex.ContainsKey(c))
                    {
                        rgbToPaletteIndex.Add(c, i);
                    }
                }
                for (int i = 0; i < 256; i++)
                {
                    int c = (int)(sourcePal[i] & 0xFFFFFF);
                    if (rgbToPaletteIndex.ContainsKey(c))
                    {
                        srcToDest[i] = rgbToPaletteIndex[c];
                    }
                }
            }

            //map remaining colors
            {
                for (int i = 0; i < 256; i++)
                {
                    if (srcToDest[i] == -1)
                    {
                        int c = (int)(sourcePal[i] & 0xFFFFFF);
                        int minDistance = int.MaxValue;
                        int minIndex = -1;
                        for (int j = 0; j < 256; j++)
                        {
                            int c2 = (int)(destPal[j] & 0xFFFFFF);
                            int distance = GetDistance(c, c2);
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                minIndex = j;
                            }
                        }
                        srcToDest[i] = minIndex;
                    }
                }
            }
            byte[] sequence = new byte[256];
            byte[] srcToDestByte = new byte[256];
            {
                for (int i = 0; i < 256; i++)
                {
                    sequence[i] = (byte)i;
                    srcToDestByte[i] = (byte)srcToDest[i];
                }
            }

            bitmap.ApplyPaletteIndexMapping(sequence, srcToDestByte, 256, false);
            bitmap.Palette.AsArray = newPalette.AsArray;
        }

        public static unsafe void RemapPalette(FreeImageBitmap bitmap, FreeImageBitmap referenceImage, int numberOfColors)
        {
            using (FreeImageBitmap bitmap32 = bitmap.GetColorConvertedInstance(FREE_IMAGE_COLOR_DEPTH.FICD_32_BPP))
            {
                if (referenceImage.Height < bitmap.Height || referenceImage.Width < bitmap.Width)
                {
                    int h2 = referenceImage.Height;
                    if (h2 < bitmap.Height)
                    {
                        h2 = bitmap.Height;
                    }
                    int w2 = referenceImage.Width;
                    if (w2 < bitmap.Width)
                    {
                        w2 = bitmap.Width;
                    }
                    referenceImage.EnlargeCanvas<byte>(0, 0, w2 - referenceImage.Width, h2 - referenceImage.Height, 0);
                }

                //FreeImageBitmap reference32 = bitmap.GetColorConvertedInstance(FREE_IMAGE_COLOR_DEPTH.FICD_32_BPP);

                if (bitmap.ColorDepth != 8)
                {
                    bitmap.ConvertColorDepth(FREE_IMAGE_COLOR_DEPTH.FICD_08_BPP);
                }
                int* newPalette = (int*)(referenceImage.Palette.BaseAddress);

                int h = bitmap.Height;
                int w = bitmap.Width;

                //Ties are broken in ColorToIndex by popularity in the original image
                Dictionary<int, int> ColorToIndex = new Dictionary<int, int>();
                int[] ColorHistogram = new int[256];
                for (int y = 0; y < h; y++)
                {
                    byte* srcScanline = (byte*)(referenceImage.GetScanlineFromTop8Bit(y).BaseAddress);
                    for (int x = 0; x < w; x++)
                    {
                        int c = srcScanline[x];
                        ColorHistogram[c]++;
                    }
                }

                //add each palette color to the dictionary
                for (int i = 0; i < numberOfColors; i++)
                {
                    int c = newPalette[i] & 0xFFFFFF;
                    if (ColorToIndex.ContainsKey(c))
                    {
                        int otherIndex = ColorToIndex[c];
                        if (ColorHistogram[i] > ColorHistogram[otherIndex])
                        {
                            ColorToIndex[c] = i;
                        }
                    }
                    else
                    {
                        ColorToIndex[c] = i;
                    }
                }

                for (int y = 0; y < h; y++)
                {
                    int* trueColorScanline = (int*)(bitmap32.GetScanlineFromTop32Bit(y).BaseAddress);
                    byte* srcScanline = (byte*)(bitmap.GetScanlineFromTop8Bit(y).BaseAddress);
                    byte* referenceScanline = (byte*)(referenceImage.GetScanlineFromTop8Bit(y).BaseAddress);

                    for (int x = 0; x < w; x++)
                    {
                        int srcColor = trueColorScanline[x] & 0xFFFFFF;
                        int refIndex = referenceScanline[x];
                        int refColor = newPalette[refIndex] & 0xFFFFFF;
                        if (srcColor == refColor)
                        {
                            srcScanline[x] = (byte)refIndex;
                        }
                        else
                        {
                            if (ColorToIndex.ContainsKey(srcColor))
                            {
                                int newColorIndex = ColorToIndex[srcColor];
                                if (refColor == (newPalette[newColorIndex] & 0xFFFFFF))
                                {
                                    srcScanline[x] = (byte)refIndex;
                                }
                                else
                                {
                                    srcScanline[x] = (byte)newColorIndex;
                                }
                            }
                            else
                            {
                                float minDistance = float.MaxValue;
                                int minIndex = 0;
                                for (int i = 0; i < numberOfColors; i++)
                                {
                                    int c = newPalette[i] & 0xFFFFFF;
                                    float distance = GetDistanceF(srcColor, c);
                                    if (distance < minDistance)
                                    {
                                        minIndex = i;
                                        minDistance = distance;
                                    }
                                    else if (distance == minDistance && ColorHistogram[i] > ColorHistogram[minIndex])
                                    {
                                        minIndex = i;
                                    }
                                }
                                if (refColor == (newPalette[minIndex] & 0xFFFFFF))
                                {
                                    srcScanline[x] = (byte)refIndex;
                                }
                                else
                                {
                                    srcScanline[x] = (byte)minIndex;
                                }
                                ColorToIndex.Add(srcColor, minIndex);
                            }
                        }
                    }
                }
                bitmap.Palette.AsArray = referenceImage.Palette.AsArray;
            }
        }

        private static uint[] PaletteToArray(Palette newPalette)
        {
            var destPal = newPalette.AsArray.Select(c => c.uintValue).ToArray();
            return destPal;
        }

        static float GetDistanceF(int c1, int c2)
        {
            int r1 = c1 & 0xFF;
            int g1 = (c1 & 0xFF00) >> 8;
            int b1 = (c1 & 0xFF0000) >> 16;

            int r2 = c2 & 0xFF;
            int g2 = (c2 & 0xFF00) >> 8;
            int b2 = (c2 & 0xFF0000) >> 16;

            float Y1 = 0.299f * r1 + 0.587f * g1 + 0.114f * b1 + 0;
            float Cb1 = -0.169f * r1 - 0.331f * g1 + 0.499f * b1 + 128;
            float Cr1 = 0.499f * r1 - 0.418f * g1 - 0.0813f * b1 + 128;

            float Y2 = 0.299f * r2 + 0.587f * g2 + 0.114f * b2 + 0;
            float Cb2 = -0.169f * r2 - 0.331f * g2 + 0.499f * b2 + 128;
            float Cr2 = 0.499f * r2 - 0.418f * g2 - 0.0813f * b2 + 128;

            float yDistance = Y2 - Y1;
            float cbDistance = Cb1 - Cb2;
            float crDistance = Cr1 - Cr2;

            return yDistance * yDistance + cbDistance * cbDistance + crDistance * crDistance;
        }
        static int GetDistance(int c1, int c2)
        {
            int rDistance = ((c1 & 0xFF) - (c2 & 0xFF));
            int gDistance = (((c1 & 0xFF00) >> 8) - (c2 & 0xFF00) >> 8);
            int bDistance = (((c1 & 0xFF0000) >> 16) - (c2 & 0xFF0000) >> 16);
            return rDistance * rDistance + gDistance * gDistance + bDistance * bDistance;
        }

        public static void SaveAjp(Stream stream, FreeImageBitmap bitmap)
        {
            Ajp.SaveImage(stream, bitmap);
        }

        public static void SaveAjp(Stream stream, byte[] jpegFile, FreeImageBitmap alpha, AjpHeader ajpHeader)
        {
            Ajp.SaveImage(stream, jpegFile, alpha, ajpHeader);
        }

        public static byte[] LoadIphMetadata(byte[] fileBytes)
        {
            return null;
            //bool SaveMetadata(const wchar_t *fileName, const wchar_t *iphFileName, ImageInfo &image)
            //{
            //    FILE *file = NULL;
            //    FILE *iphFile = NULL;

            //    file = _wfopen(fileName, L"wb");
            //    iphFile = _wfopen(iphFileName, L"rb");

            //    if (iphFile == NULL || file == NULL)
            //    {
            //        if (file != NULL) fclose(file);
            //        if (iphFile != NULL) fclose(iphFile);
            //        return false;
            //    }

            //    fwrite(&image.f, sizeof(int), 1, file);
            //    fwrite(&image.x, sizeof(int), 1, file);
            //    fwrite(&image.y, sizeof(int), 1, file);
            //    fwrite(&image.cellWidth, sizeof(int), 1, file);
            //    fwrite(&image.cellHeight, sizeof(int), 1, file);
            //    fwrite(&image.transparentColor, sizeof(int), 1, file);

            //    fseek(iphFile, 0x08, SEEK_SET);
            //    fread(&image.fourcc, sizeof(int), 1, iphFile);
            //    fwrite(&image.fourcc,sizeof(int), 1, file);
            //    fseek(iphFile, 0x52, SEEK_SET);
            //    image.format = 0;
            //    fread(&image.format, 2, 1, iphFile);
            //    fwrite(&image.format, sizeof(int), 1, file);

            //    fclose(file);
            //    fclose(iphFile);
            //    return true;	
            //}

        }

        public static FreeImageBitmap LoadIph(byte[] fileBytes)
        {
            return Iph.LoadIph(fileBytes);
        }

        private static bool IphHeaderIsValid(byte[] fileBytes)
        {
            return Iph.IphHeaderIsValid(fileBytes);
        }

        public static void SaveIph(Stream stream, FreeImageBitmap bitmap)
        {
            Iph.SaveIph(stream, bitmap);
        }

        public static FreeImageBitmap LoadMsk(byte[] fileBytes)
        {
            throw new NotImplementedException();
        }

        public static FreeImageBitmap LoadAgf(byte[] fileBytes)
        {
            return Agf.LoadAgf(fileBytes);
        }

        public static void SaveAgf(Stream stream, FreeImageBitmap bitmap)
        {
            Agf.SaveAgf(stream, bitmap);
        }
    }

    public static partial class Extensions
    {
        public static Scanline<byte> GetScanlineFromTop8Bit(this FreeImageBitmap bitmap, int y)
        {
            return bitmap.GetScanline<byte>(bitmap.Height - 1 - y);
        }
        public static Scanline<RGBTRIPLE> GetScanlineFromTop24Bit(this FreeImageBitmap bitmap, int y)
        {
            return bitmap.GetScanline<RGBTRIPLE>(bitmap.Height - 1 - y);
        }
        public static Scanline<int> GetScanlineFromTop32Bit(this FreeImageBitmap bitmap, int y)
        {
            return bitmap.GetScanline<int>(bitmap.Height - 1 - y);
        }
        public static Scanline<FI4BIT> GetScanlineFromTop4Bit(this FreeImageBitmap bitmap, int y)
        {
            return bitmap.GetScanline<FI4BIT>(bitmap.Height - 1 - y);
        }
    }

    public static class SwfToAffConverter
    {
        static byte[] xorKey = new byte[] { 0xC8, 0xBB, 0x8F, 0xB7, 0xED, 0x43, 0x99, 0x4A, 0xA2, 0x7E, 0x5B, 0xB0, 0x68, 0x18, 0xF8, 0x88, 0x53 };

        public static byte[] ConvertSwfToAff(byte[] swfBytes)
        {
            MemoryStream ms = new MemoryStream();
            ConvertSwfToAff(swfBytes, ms);
            return ms.ToArray();
        }

        public static void ConvertSwfToAff(byte[] swfBytes, Stream outputStream)
        {
            var bw = new BinaryWriter(outputStream);

            //"AFF\0", 1, filesize, 0x4D2

            bw.Write(ASCIIEncoding.ASCII.GetBytes("AFF"));
            bw.Write((byte)0);
            bw.Write((int)1);
            bw.Write((int)swfBytes.Length + 16);
            bw.Write((int)0x4D2);

            //screw around with first 0x40 bytes of SWF file
            int count = Math.Min(swfBytes.Length, 0x40);

            for (int i = 0; i < count; i++)
            {
                int i2 = i & 0x0F;
                swfBytes[i] ^= xorKey[i2];
            }

            bw.Write(swfBytes);
            bw.Flush();
        }

        public static void ConvertSwfToAff(string swfFileName, string affFileName)
        {
            var swfBytes = File.ReadAllBytes(swfFileName);

            using (FileStream fs = new FileStream(affFileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                ConvertSwfToAff(swfBytes, fs);

                fs.Flush();
                fs.Close();
            }
        }

        public static byte[] ConvertAffToSwf(byte[] bytes)
        {
            return ConvertAffToSwf(new MemoryStream(bytes));
        }

        public static byte[] ConvertAffToSwf(Stream inputStream)
        {
            var br = new BinaryReader(inputStream);
            byte[] affHeader = br.ReadBytes(16);
            byte[] swfBytes = br.ReadBytes((int)br.BaseStream.Length - 16);
            int count = Math.Min(swfBytes.Length, 0x40);
            for (int i = 0; i < count; i++)
            {
                int i2 = i & 0x0F;
                swfBytes[i] ^= xorKey[i2];
            }
            return swfBytes;
        }

        public static void ConvertAffToSwf(string affFileName, string swfFileName)
        {
            //not yet tested
            using (FileStream fs = new FileStream(affFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var swfBytes = ConvertAffToSwf(fs);
                File.WriteAllBytes(swfFileName, swfBytes);
            }
        }
    }

    public static partial class BinaryUtil
    {
        static Encoding shiftJis = Encoding.GetEncoding("shift_jis");

        public static string IntToString(int intValue)
        {
            var bytes = BitConverter.GetBytes(intValue);
            int indexOfZero = Array.IndexOf<byte>(bytes, 0);
            if (indexOfZero == -1) indexOfZero = 4;
            return shiftJis.GetString(bytes, 0, indexOfZero);
        }

        public static int StringToInt(string stringValue)
        {
            byte[] bytes = shiftJis.GetBytes(stringValue);
            if (bytes.Length > 4)
            {
                bytes = bytes.Take(4).ToArray();
            }
            else if (bytes.Length < 4)
            {
                bytes = bytes.Concat(Enumerable.Repeat((Byte)0, 4 - bytes.Length)).ToArray();
            }
            return BitConverter.ToInt32(bytes, 0);
        }

        public static byte[] GetBytesFromHexString(string hexString)
        {
            if ((hexString.Length & 1) == 1)
            {
                return null;
            }
            List<byte> bytes = new List<byte>(hexString.Length / 2);
            for (int i = 0; i < hexString.Length; i += 2)
            {
                string hexPair = hexString.Substring(i, 2);
                byte b;
                if (byte.TryParse(hexPair, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out b))
                {
                    bytes.Add(b);
                }
                else
                {
                    return null;
                }
            }
            return bytes.ToArray();
        }

        public static unsafe string FixedBytesToString(byte* bytes, int bufferLength, Encoding encoding)
        {
            unsafe
            {
                int strLen = 0;
                {
                    int i;
                    for (i = 0; i < bufferLength; i++)
                    {
                        if (bytes[i] == 0)
                        {
                            break;
                        }
                    }
                    strLen = i;
                }
                byte[] fileNameBytes = new byte[strLen];
                Marshal.Copy((IntPtr)bytes, fileNameBytes, 0, strLen);
                return encoding.GetString(fileNameBytes);
            }
        }

        public static unsafe void StringToFixedBytes(byte* bytes, int bufferLength, string str, Encoding encoding)
        {
            unsafe
            {
                byte[] fileNameBytes = encoding.GetBytes(str);
                int lengthToCopy = Math.Min(fileNameBytes.Length, bufferLength);
                Marshal.Copy(fileNameBytes, 0, (IntPtr)bytes, lengthToCopy);
            }

        }
    }
}
