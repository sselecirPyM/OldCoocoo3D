using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.ResourceWarp
{
    public class Texture2DUploadPack
    {
        public Texture2D texture;
        public Uploader uploader;

        public Texture2DUploadPack (Texture2D texture, Uploader uploader)
        {
            this.texture = texture;
            this.uploader = uploader;
        }

        public static Texture2DUploadPack Pure(Texture2D texture,int width,int height,Vector4 color)
        {
            Uploader uploader = new Uploader();
            uploader.Texture2DPure(width, height,color);
            return new Texture2DUploadPack(texture, uploader);
        }
    }
}
