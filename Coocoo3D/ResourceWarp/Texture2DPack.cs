﻿using Coocoo3D.Utility;
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
using SixLabors.ImageSharp.Processing;

namespace Coocoo3D.ResourceWarp
{
    public class Texture2DPack
    {
        public Texture2D texture2D = new Texture2D();
        public bool canReload = true;
        public bool useMipMap = true;

        public DateTimeOffset lastModifiedTime;
        public StorageFolder folder;
        public string relativePath;
        public GraphicsObjectStatus Status;

        public Task loadTask;

        public void Mark(GraphicsObjectStatus status)
        {
            Status = status;
            texture2D.Status = status;
        }

        public async Task<bool> ReloadTexture(IStorageItem storageItem, Uploader uploader)
        {
            if (!(storageItem is StorageFile texFile))
            {
                Mark(GraphicsObjectStatus.error);
                return false;
            }
            Mark(GraphicsObjectStatus.loading);
            try
            {
                //uploader.Texture2D(await FileIO.ReadBufferAsync(texFile), true, true);
                //byte[] data = GetImageData(await texFile.OpenStreamForReadAsync(), out int width, out int height, out _);
                byte[] data = GetImageData(await texFile.OpenStreamForReadAsync(), out int width, out int height, out _, out int mipMap);
                uploader.Texture2DRaw(data, DxgiFormat.DXGI_FORMAT_R8G8B8A8_UNORM_SRGB, width, height, mipMap);

                Status = GraphicsObjectStatus.loaded;
                return true;
            }
            catch
            {
                Mark(GraphicsObjectStatus.error);
                return false;
            }
        }

        private void GetImageData(Stream stream, Uploader uploader)
        {
            byte[] data = GetImageData(stream, out int width, out int height, out _);
            uploader.Texture2DRaw(data, DxgiFormat.DXGI_FORMAT_R8G8B8A8_UNORM_SRGB, width, height);
        }

        public static async Task<Uploader> UploaderTex2DNoMip(string uri)
        {
            return await UploaderTex2DNoMip(await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri)));
        }

        public static async Task<Uploader> UploaderTex2D(string uri)
        {
            return await UploaderTex2D(await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri)));
        }

        public static async Task<Uploader> UploaderTex2DNoMip(StorageFile file)
        {
            Uploader uploader = new Uploader();
            byte[] data = Texture2DPack.GetImageData(await file.OpenStreamForReadAsync(), out int width, out int height, out int bitPerPixel);
            uploader.Texture2DRaw(data, DxgiFormat.DXGI_FORMAT_R8G8B8A8_UNORM, width, height, 1);
            return uploader;
        }

        public static async Task<Uploader> UploaderTex2D(StorageFile file)
        {
            Uploader uploader = new Uploader();
            byte[] data = Texture2DPack.GetImageData(await file.OpenStreamForReadAsync(), out int width, out int height, out int bitPerPixel, out int mipMap);
            uploader.Texture2DRaw(data, DxgiFormat.DXGI_FORMAT_R8G8B8A8_UNORM_SRGB, width, height, mipMap);
            return uploader;
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
            width = frame0.Width;
            height = frame0.Height;
            bitPerPixel = image0.PixelType.BitsPerPixel;
            frame0.TryGetSinglePixelSpan(out Span<Rgba32> span1);

            Span<byte> castToByte = MemoryMarshal.Cast<Rgba32, byte>(span1);
            int totalSize1 = GetTotalSize(castToByte.Length, width, height, out mipMap);

            byte[] bytes = new byte[totalSize1];
            castToByte.CopyTo(bytes);

            int totalCount = castToByte.Length;
            int width1 = width;
            int height1 = height;
            int d = castToByte.Length;
            while (width1 > 64 && height1 > 64)
            {
                width1 /= 2;
                height1 /= 2;
                d /= 4;
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
            int totalCount = size;
            level = 1;
            while (width > 64 && height > 64)
            {
                width /= 2;
                height /= 2;
                d /= 4;
                totalCount += d;
                level++;
            }
            return totalCount;
        }
    }
}
