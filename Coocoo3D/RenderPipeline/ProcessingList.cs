using Coocoo3D.ResourceWarp;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class ProcessingList
    {
        public List<Object> loadList = new List<object>();

        public void AddObject(MMDMesh mesh)
        {
            lock (loadList)
            {
                loadList.Add(mesh);
            }
        }
        public void AddObject(TextureCube texture, Uploader uploader)
        {
            lock (loadList)
            {
                loadList.Add(new TextureCubeUploadPack( texture,uploader));
            }
        }
        public void AddObject(Texture2D texture2D, Uploader uploader)
        {
            lock (loadList)
            {
                loadList.Add(new ResourceWarp.Texture2DUploadPack(texture2D, uploader));
            }
        }
        public void AddObject(Texture2D texture2D, int width, int height, Vector4 color)
        {
            lock (loadList)
            {
                loadList.Add(Texture2DUploadPack.Pure(texture2D, width, height, color));
            }
        }

        public void MoveToAnother(ProcessingList another)
        {
            var temp = another.loadList;
            another.loadList = loadList;
            loadList = temp;
            loadList.Clear();
        }

        public bool IsEmpty()
        {
            return loadList.Count == 0;
        }

        public void _DealStep1(GraphicsContext graphicsContext)
        {
            foreach (var obj in loadList)
            {
                if (obj is TextureCubeUploadPack p1)
                    graphicsContext.UploadTexture(p1.texture, p1.uploader);
                else if (obj is Texture2DUploadPack p2)
                    graphicsContext.UploadTexture(p2.texture, p2.uploader);
                else if (obj is MMDMesh p3)
                    graphicsContext.UploadMesh(p3);
            }
            loadList.Clear();
        }
    }
}
