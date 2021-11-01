using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.DXGI;

namespace Coocoo3D.ResourceWarp
{
    public class TextureCubeUploadPack
    {
        public TextureCube texture;
        public Uploader uploader;

        public TextureCubeUploadPack(TextureCube texture, Uploader uploader)
        {
            this.texture = texture;
            this.uploader = uploader;
        }
        public static TextureCubeUploadPack FromFiles(TextureCube texture, Stream[] streams)
        {
            Uploader uploader = new Uploader();
            byte[][] datas = new byte[6][];
            int width = 0;
            int height = 0;
            int bitPerPixel = 0;
            int mipMap = 0;
            Parallel.For(0, 6, (int i) =>
            {
                if (i == 0)
                    datas[i] = Texture2DPack.GetImageData(streams[i], out width, out height, out bitPerPixel, out mipMap);
                else
                    datas[i] = Texture2DPack.GetImageData(streams[i], out _, out _, out _, out _);
            });
            byte[] t = new byte[datas.Sum(u => u.Length)];
            int c = 0;
            for (int i = 0; i < datas.Length; i++)
            {
                datas[i].CopyTo(t, c);
                c += datas[i].Length;
            }
            uploader.TextureCubeRaw(t, Format.R8G8B8A8_UNorm_SRgb, width, height, mipMap);
            return new TextureCubeUploadPack(texture, uploader);
        }
    }
}
