using Coocoo3D.ResourceWarp;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public List<TextureCubeUploadPack> TextureCubeLoadList = new List<TextureCubeUploadPack>();
        public List<Texture2DUploadPack> Texture2DLoadList = new List<Texture2DUploadPack>();
        public List<MMDMesh> MMDMeshLoadList = new List<MMDMesh>();
        public List<MeshAppendUploadPack> MMDMeshLoadList2 = new List<MeshAppendUploadPack>();

        public void AddObject(MMDMesh mesh)
        {
            lock (MMDMeshLoadList)
            {
                MMDMeshLoadList.Add(mesh);
            }
        }
        public void AddObject(MeshAppendUploadPack mesh)
        {
            lock (MMDMeshLoadList2)
            {
                MMDMeshLoadList2.Add(mesh);
            }
        }
        public void AddObject(TextureCubeUploadPack texture)
        {
            lock (TextureCubeLoadList)
            {
                TextureCubeLoadList.Add(texture);
            }
        }
        public void AddObject(Texture2DUploadPack texture)
        {
            lock (Texture2DLoadList)
            {
                Texture2DLoadList.Add(texture);
            }
        }

        public void MoveToAnother(ProcessingList another)
        {
            Move1(TextureCubeLoadList, another.TextureCubeLoadList);
            Move1(Texture2DLoadList, another.Texture2DLoadList);
            Move1(MMDMeshLoadList, another.MMDMeshLoadList);
            Move1(MMDMeshLoadList2, another.MMDMeshLoadList2);
        }

        public bool IsEmpty()
        {
            return TextureCubeLoadList.Count == 0 &&
                 Texture2DLoadList.Count == 0 &&
                MMDMeshLoadList.Count == 0 &&
                MMDMeshLoadList2.Count == 0;
        }

        public void _DealStep1(GraphicsContext graphicsContext)
        {
            for (int i = 0; i < TextureCubeLoadList.Count; i++)
                graphicsContext.UploadTexture(TextureCubeLoadList[i].texture, TextureCubeLoadList[i].uploader);
            for (int i = 0; i < Texture2DLoadList.Count; i++)
                graphicsContext.UploadTexture(Texture2DLoadList[i].texture, Texture2DLoadList[i].uploader);
            for (int i = 0; i < MMDMeshLoadList.Count; i++)
                graphicsContext.UploadMesh(MMDMeshLoadList[i]);
            for (int i = 0; i < MMDMeshLoadList2.Count; i++)
                graphicsContext.UploadMesh(MMDMeshLoadList2[i].mesh, MMDMeshLoadList2[i].data);

            TextureCubeLoadList.Clear();
            Texture2DLoadList.Clear();
            MMDMeshLoadList.Clear();
            MMDMeshLoadList2.Clear();
        }
    }
}
