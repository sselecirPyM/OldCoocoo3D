using Coocoo3DGraphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Vortice.DXGI;
using ImageMagick;

namespace Coocoo3D.ResourceWarp
{
    public class Texture2DPack
    {
        public Texture2D texture2D = new Texture2D();
        public bool canReload = true;
        public bool srgb = true;
        public string fullPath;

        public GraphicsObjectStatus Status;

        public Task loadTask;

        public void Mark(GraphicsObjectStatus status)
        {
            Status = status;
            texture2D.Status = status;
        }

        public bool ReloadTexture(FileInfo storageItem, Uploader uploader)
        {
            if (!(storageItem is FileInfo texFile))
            {
                return false;
            }
            try
            {
                switch (storageItem.Extension.ToLower())
                {
                    case ".hdr":
                    case ".exr":
                    case ".tif":
                    case ".tiff":
                    case ".dds":
                        {
                            var img = new MagickImage(storageItem);
                            byte[] data = img.ToByteArray(MagickFormat.Rgba);
                            int d = data.Length / img.Width / img.Height;
                            uploader.Texture2DRawLessCopy(data, d == 8 ? Format.R16G16B16A16_UNorm : Format.R8G8B8A8_UNorm_SRgb, img.Width, img.Height, 1);

                        }
                        break;
                    default:
                        {
                            byte[] data = GetImageData(texFile.OpenRead(), out int width, out int height, out _, out int mipMap);
                            uploader.Texture2DRawLessCopy(data, srgb ? Format.R8G8B8A8_UNorm_SRgb : Format.R8G8B8A8_UNorm, width, height, mipMap);
                        }
                        break;
                }

                Status = GraphicsObjectStatus.loaded;
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public static byte[] GetImageData(Stream stream, out int width, out int height, out int bitPerPixel)
        {
            Image image0 = Image.Load(stream);
            Image<Rgba32> image = (Image<Rgba32>)image0;
            var frame0 = image.Frames[0];
            frame0.TryGetSinglePixelSpan(out Span<Rgba32> span1);
            Span<byte> castToByte = MemoryMarshal.Cast<Rgba32, byte>(span1);
            byte[] bytes = new byte[castToByte.Length];
            castToByte.CopyTo(bytes);
            width = frame0.Width;
            height = frame0.Height;
            bitPerPixel = image0.PixelType.BitsPerPixel;
            return bytes;
        }
        public static byte[] GetImageData(Stream stream, out int width, out int height, out int bitPerPixel, out int mipMap)
        {
            Image image0 = Image.Load(stream);
            Image<Rgba32> image = (Image<Rgba32>)image0;
            var frame0 = image.Frames[0];
            int width1 = frame0.Width;
            int height1 = frame0.Height;
            int sizex = 32;
            for (sizex = 64; sizex < 8192; sizex <<= 1)
                if (sizex >= width1 * 0.95)
                    break;
            int sizey = 32;
            for (sizey = 64; sizey < 8192; sizey <<= 1)
                if (sizey >= height1 * 0.95)
                    break;
            if (width1 != sizex || height1 != sizey)
            {
                width1 = (width1 + sizex - 1) / sizex * sizex;
                height1 = (height1 + sizey - 1) / sizey * sizey;
                image.Mutate(x => x.Resize(width1, height1, KnownResamplers.Box));
            }
            width = width1;
            height = height1;

            bitPerPixel = image0.PixelType.BitsPerPixel;
            frame0.TryGetSinglePixelSpan(out Span<Rgba32> span1);

            Span<byte> castToByte = MemoryMarshal.Cast<Rgba32, byte>(span1);
            int totalSize1 = GetTotalSize(castToByte.Length, width, height, out mipMap);

            byte[] bytes = new byte[totalSize1];
            castToByte.CopyTo(bytes);

            int totalCount = castToByte.Length;
            int d = castToByte.Length;
            int bytePerPixel = d / (width1 * height1);
            while (width1 > 64 && height1 > 64)
            {
                width1 /= 2;
                height1 /= 2;
                d = bytePerPixel * width1 * height1;
                image.Mutate(x => x.Resize(width1, height1, KnownResamplers.Box));
                var frame1 = image.Frames[0];
                frame1.TryGetSinglePixelSpan(out Span<Rgba32> span2);
                Span<byte> castToByte1 = MemoryMarshal.Cast<Rgba32, byte>(span2);
                castToByte1.CopyTo(new Span<byte>(bytes, totalCount, d));
                totalCount += d;
            }

            return bytes;
        }
        public static int GetTotalSize(int size, int width, int height, out int level)
        {
            int d = size;
            int bytePerPixel = d / (width * height);
            int totalCount = size;
            level = 1;
            while (width > 64 && height > 64)
            {
                width /= 2;
                height /= 2;
                d = bytePerPixel * width * height;
                totalCount += d;
                level++;
            }
            return totalCount;
        }
    }
}
