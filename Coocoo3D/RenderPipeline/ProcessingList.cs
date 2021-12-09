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
        public List<object> loadList = new List<object>();

        public void AddObject(MMDMesh mesh)
        {
            lock (loadList)
            {
                loadList.Add(mesh);
            }
        }
        public void AddObject(Texture2D texture2D, Uploader uploader)
        {
            lock (loadList)
            {
                loadList.Add(new Texture2DUploadPack(texture2D, uploader));
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
                if (obj is Texture2DUploadPack p2)
                    graphicsContext.UploadTexture(p2.texture, p2.uploader);
                else if (obj is MMDMesh p3)
                    graphicsContext.UploadMesh(p3);
            }
            loadList.Clear();
        }
    }
}
