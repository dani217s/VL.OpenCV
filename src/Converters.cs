﻿using OpenCvSharp;
using System;
using System.Runtime.CompilerServices;
using VL.Lib.Basics.Imaging;

namespace VL.OpenCV
{
    public static class Converters
    {
        class MatImage : IImage
        {
            class Data : IImageData
            {
                readonly Mat FMat;
                readonly ImageInfo FInfo;

                public Data(Mat mat, ImageInfo info)
                {
                    FMat = mat;
                    FInfo = info;
                }

                public IntPtr Pointer => FMat.Data;
                public int ScanSize => FInfo.ScanSize;
                public int Size => FInfo.ImageSize;
                public void Dispose() { }
            }

            readonly Mat FMat;
            readonly PixelFormat FFormat;
            readonly bool FIsOwner;

            public MatImage(Mat mat, PixelFormat format, bool isOwner)
            {
                FMat = mat;
                FFormat = format;
                FIsOwner = isOwner;
            }

            public ImageInfo Info => new ImageInfo(FMat.Width, FMat.Height, FFormat);

            public bool IsVolatile => !FIsOwner;

            public void Dispose()
            {
                if (FIsOwner)
                    FMat.Dispose();
            }

            public IImageData GetData() => new Data(FMat, Info);
        }

        public static IImage ToImage(this Mat input, bool takeOwnership)
        {
            if (input == DefaultMat.Damon)
                takeOwnership = false;
            var format = input.Type().ToPixelFormat();
            return new MatImage(input, format, takeOwnership);
        }

        public static unsafe Mat ToMat(this IImage input)
        {
            var info = input.Info;
            var type = info.Format.ToMatType(info.OriginalFormat);
            using (var srcData = input.GetData())
            {
                var size = srcData.Size;
                var dstData = new byte[size];
                var src = srcData.Pointer.ToPointer();
                fixed (byte* dst = dstData)
                    Unsafe.CopyBlock(dst, src, (uint)size);
                return new Mat(info.Height, info.Width, type, dstData, srcData.ScanSize);
            }
        }

        public static unsafe void ToMat(this IImage input, Mat dstMat)
        {
            var info = input.Info;
            var type = info.Format.ToMatType(info.OriginalFormat);
            using (var srcData = input.GetData())
            {
                var size = srcData.Size;
                var src = srcData.Pointer.ToPointer();
                dstMat.Create(info.Height, info.Width, type);
                Unsafe.CopyBlock(dstMat.DataPointer, src, (uint)size);
            }
        }

        public static PixelFormat ToPixelFormat(this MatType type)
        {
            switch (type.Depth)
            {
                case MatType.CV_8U:
                    switch (type.Channels)
                    {
                        case 1:
                            return PixelFormat.R8;
                        case 4:
                            return PixelFormat.B8G8R8A8;
                    }
                    break;
                case MatType.CV_32F:
                    switch (type.Channels)
                    {
                        case 1:
                            return PixelFormat.R32F;
                    }
                    break;
            }
            throw new UnsupportedMatTypeException(type);
        }

        public static MatType ToMatType(this PixelFormat format, string originalFormat)
        {
            switch (format)
            {
                case PixelFormat.R8:
                    return MatType.CV_8UC1;
                case PixelFormat.R32F:
                    return MatType.CV_32FC1;
                case PixelFormat.B8G8R8A8:
                    return MatType.CV_8UC4;
                case PixelFormat.Unknown:
                    if (originalFormat.Equals("bgr", StringComparison.OrdinalIgnoreCase)) // HACK
                        return MatType.CV_8UC3;
                    break;
            }
            throw new UnsupportedPixelFormatException(format);
        }

        /// <summary>
        /// Wraps the Cv2.CvtColor in order to simplify the enums presented to the end user.
        /// </summary>
        /// <param name="input">Orignal image</param>
        /// <param name="output">Destination image</param>
        /// <param name="sourceCode">ColorConversionSourceCodes corresponding to the original image</param>
        /// <param name="targetCode">ColorConversionTargetCodes in which the resulting image should be</param>
        /// <param name="channels"></param>
        public static void ConvertColorFormat(InputArray input, OutputArray output, ColorConversionSourceCodes sourceCode, ColorConversionTargetCodes targetCode, int channels)
        {
            var code = sourceCode + "2" + targetCode;
            ColorConversionCodes result;
            if (Enum.TryParse(code, out result))
                Cv2.CvtColor(input, output, result, channels);
            else
                throw new Exception("Specified conversion code does not exist: " + code);
        }
    }
}