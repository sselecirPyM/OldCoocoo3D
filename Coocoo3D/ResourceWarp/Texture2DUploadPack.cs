using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
