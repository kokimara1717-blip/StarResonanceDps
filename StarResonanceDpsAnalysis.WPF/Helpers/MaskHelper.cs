using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpCompress.Compressors.LZMA;
using StarResonanceDpsAnalysis.WPF.Config;
using ZXing;
using ZXing.Common;

namespace StarResonanceDpsAnalysis.WPF.Helpers
{
    public static class MaskHelper
    {
        private static readonly byte[] SteganographyVersion = [0, 1];
        private const int DataMatrixImageSize = 128;
        private const int LzmaMaxDictionarySize = 1 << 16;
        private const int LzmaMaxFastBytes = 64;
        private const string Byte2StrEncoding = "ISO-8859-1";

        /*
           WARNING: DO NOT forget to increment SteganographyVersion when this format is changed.

           Version[0,1]: [..SteganographyVersion, isRawData, ..data]
               when isRawData == 1: `data` does not need to decompressed; otherwise, it needs decompression.
         */
        private static string SteganographyText 
        {
            get 
            {
                return $"{BuildInfo.GetVersion()}-{BuildInfo.GetBuildTime()}";
            }
        }
        private static BitmapSource? _steganographyImage;

        public static BitmapSource SteganographyImage
        {
            get
            {
                _steganographyImage ??= CreateDataMatrixImageFromText(SteganographyText);
                return _steganographyImage;
            }
        }

        private static BitmapSource CreateDataMatrixImageFromText(string text)
        {
            Debug.WriteLine(text);

            var inputBytes = Encoding.UTF8.GetBytes(text);
            var compressedBytes = CompressUtf8TextWithLzma(inputBytes);

            (byte isRaw, byte[] data) = compressedBytes.Length > inputBytes.Length 
                ? ((byte)1, inputBytes) 
                : ((byte)0, compressedBytes);

            byte[] payloadBytes = [
                ..SteganographyVersion,
                isRaw,
                ..data
            ];
            var dataMatrixPayload = Encoding.GetEncoding(Byte2StrEncoding).GetString(payloadBytes);

            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.DATA_MATRIX,
                Options = new EncodingOptions
                {
                    Height = DataMatrixImageSize,
                    Width = DataMatrixImageSize,
                    Margin = 0,
                    PureBarcode = true,
                }
            };

            var pixelData = writer.Write(dataMatrixPayload);
            var grayPixels = ConvertBgra32ToGray8(pixelData.Pixels, pixelData.Width, pixelData.Height);
            var checkerboardPixels = BuildQuadrantCheckerboardGray8(grayPixels, pixelData.Width, pixelData.Height);

            var bitmap = BitmapSource.Create(
                pixelData.Width * 2,
                pixelData.Height * 2,
                96d,
                96d,
                PixelFormats.Gray8,
                null,
                checkerboardPixels,
                pixelData.Width * 2);

            bitmap.Freeze();
            return bitmap;
        }

        private static byte[] CompressUtf8TextWithLzma(byte[] inputBytes)
        {
            using var input = new MemoryStream(inputBytes, writable: false);
            using var output = new MemoryStream();

            var encoderProperties = new LzmaEncoderProperties(true, LzmaMaxDictionarySize, LzmaMaxFastBytes);
            using (var encoder = new LzmaStream(encoderProperties, false, output))
            {
                input.CopyTo(encoder);
            }

            return output.ToArray();
        }

        private static byte[] ConvertBgra32ToGray8(byte[] bgraPixels, int width, int height)
        {
            var grayscalePixels = new byte[width * height];
            for (var pixelIndex = 0; pixelIndex < grayscalePixels.Length; pixelIndex++)
            {
                // Data Matrix output is monochrome; using the blue channel is enough.
                grayscalePixels[pixelIndex] = bgraPixels[pixelIndex * 4];
            }

            return grayscalePixels;
        }

        private static byte[] BuildQuadrantCheckerboardGray8(byte[] sourcePixels, int width, int height)
        {
            var doubledWidth = width * 2;
            var doubledHeight = height * 2;
            var outputPixels = new byte[doubledWidth * doubledHeight];

            for (var y = 0; y < height; y++)
            {
                var srcRowOffset = y * width;
                var dstTopRowOffset = y * doubledWidth;
                var dstBottomRowOffset = (y + height) * doubledWidth;

                for (var x = 0; x < width; x++)
                {
                    var src = sourcePixels[srcRowOffset + x];
                    var inv = (byte)(255 - src);

                    // TL: normal, TR: inverted, BL: inverted, BR: normal
                    outputPixels[dstTopRowOffset + x] = src;
                    outputPixels[dstTopRowOffset + width + x] = inv;
                    outputPixels[dstBottomRowOffset + x] = inv;
                    outputPixels[dstBottomRowOffset + width + x] = src;
                }
            }

            return outputPixels;
        }
    }
}
