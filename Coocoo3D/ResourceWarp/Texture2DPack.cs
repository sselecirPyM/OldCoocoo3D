using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using System.IO;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace Coocoo3D.ResourceWarp
{
    public class Texture2DPack
    {
        public Texture2D texture2D = new Texture2D();

        public DateTimeOffset lastModifiedTime;
        public StorageFolder folder;
        public string relativePath;
        public SingleLocker loadLocker;

        public GraphicsObjectStatus Status;
        public void Mark(GraphicsObjectStatus status)
        {
            Status = status;
            texture2D.Status = status;
        }

        public async Task<bool> ReloadTexture(IStorageItem storageItem, Uploader uploader)
        {
            Mark(GraphicsObjectStatus.loading);
            if (!(storageItem is StorageFile texFile))
            {
                Mark(GraphicsObjectStatus.error);
                return false;
            }
            try
            {
                uploader.Texture2D(await FileIO.ReadBufferAsync(texFile), true, true);
                //byte[] data = GetImageData(await texFile.OpenStreamForReadAsync(), out int width, out int height, out _);
                //uploader.Texture2DRaw(data, DxgiFormat.DXGI_FORMAT_R8G8B8A8_UNORM_SRGB, width, height);

                Status = GraphicsObjectStatus.loaded;
                return true;
            }
            catch
            {
                Mark(GraphicsObjectStatus.error);
                return false;
            }
        }

        private void GetImageData(Stream stream,Uploader uploader)
        {
            byte[] data = GetImageData(stream, out int width, out int height, out _);
            uploader.Texture2DRaw(data, DxgiFormat.DXGI_FORMAT_R8G8B8A8_UNORM_SRGB, width, height);
        }
        private byte[] GetImageData(Stream stream, out int width, out int height, out int bitPerPixel)
        {
            Image image0 = Image.Load(stream);
            //image0.PixelType.BitsPerPixel;

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
    }
}
