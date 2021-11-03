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
        static void Move1<T>(IList<T> source, IList<T> target)
        {
            lock (source)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    target.Add(source[i]);
                }
                source.Clear();
            }
        }

        public List<Object> loadList = new List<object>();

        public void AddObject(MMDMesh mesh)
        {
            lock (loadList)
            {
                loadList.Add(mesh);
            }
        }
        public void AddObject(TextureCubeUploadPack texture)
        {
            lock (loadList)
            {
                loadList.Add(texture);
            }
        }
        public void AddObject(Texture2DUploadPack texture)
        {
            lock (loadList)
            {
                loadList.Add(texture);
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
            Move1(loadList, another.loadList);
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
                else if(obj is Texture2DUploadPack p2)
                    graphicsContext.UploadTexture(p2.texture, p2.uploader);
                else if(obj is MMDMesh p3)
                    graphicsContext.UploadMesh(p3);
            }
            loadList.Clear();
        }
    }
}
