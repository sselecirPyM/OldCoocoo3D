using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
