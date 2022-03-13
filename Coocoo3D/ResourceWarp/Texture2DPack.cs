using Coocoo3DGraphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
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
            catch
            {
                return false;
            }
        }

        public static byte[] GetImageData(Stream stream, out int width, out int height, out int bitPerPixel)
        {
            Image<Rgba32> image = Image.Load<Rgba32>(stream);
            var frame0 = image.Frames[0];
            byte[] bytes = new byte[frame0.Width* frame0.Height *4];
            frame0.CopyPixelDataTo(bytes);
            width = frame0.Width;
            height = frame0.Height;
            bitPerPixel = image.PixelType.BitsPerPixel;
            return bytes;
        }
        public static byte[] GetImageData(Stream stream, out int width, out int height, out int bitPerPixel, out int mipMap)
        {
            Image<Rgba32> image = Image.Load<Rgba32>(stream);
            var frame0 = image.Frames[0];
            int width1 = frame0.Width;
            int height1 = frame0.Height;
            int sizex = GetTexSize(width1);
            int sizey = GetTexSize(height1);
            if (width1 != sizex || height1 != sizey)
            {
                width1 = (width1 + sizex - 1) / sizex * sizex;
                height1 = (height1 + sizey - 1) / sizey * sizey;
                image.Mutate(x => x.Resize(width1, height1, KnownResamplers.Box));
            }
            width = width1;
            height = height1;

            bitPerPixel = image.PixelType.BitsPerPixel;

            int totalCount = frame0.Width * frame0.Height * 4;
            int totalSize1 = GetTotalSize(totalCount, width, height, out mipMap);

            byte[] bytes = new byte[totalSize1];

            frame0.CopyPixelDataTo(bytes);


            int d = totalCount;
            int bytePerPixel = d / (width1 * height1);
            while (width1 > 64 && height1 > 64)
            {
                width1 /= 2;
                height1 /= 2;
                d = bytePerPixel * width1 * height1;
                image.Mutate(x => x.Resize(width1, height1, KnownResamplers.Box));
                var frame1 = image.Frames[0];
                frame1.CopyPixelDataTo(new Span<byte>(bytes, totalCount, d));
                totalCount += d;
            }

            return bytes;
        }

        static int GetTexSize(int height1)
        {
            int sizey;
            for (sizey = 64; sizey < 8192; sizey <<= 1)
                if (sizey >= height1 * 0.95)
                    break;
            return sizey;
        }

        static int GetTotalSize(int size, int width, int height, out int level)
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
