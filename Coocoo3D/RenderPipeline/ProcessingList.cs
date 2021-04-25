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
        public List<ReadBackTexture2D> readBackTextureList = new List<ReadBackTexture2D>();
        public List<ShaderWarp1> pobjectList = new List<ShaderWarp1>();

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
        public void AddObject(ReadBackTexture2D texture)
        {
            lock (readBackTextureList)
            {
                readBackTextureList.Add(texture);
            }
        }
        /// <summary>添加到上传列表</summary>
        public void UL(ShaderWarp1 pObject)
        {
            lock (pobjectList)
            {
                pobjectList.Add(pObject);
            }
        }

        public void MoveToAnother(ProcessingList another)
        {
            Move1(TextureCubeLoadList, another.TextureCubeLoadList);
            Move1(Texture2DLoadList, another.Texture2DLoadList);
            Move1(MMDMeshLoadList, another.MMDMeshLoadList);
            Move1(MMDMeshLoadList2, another.MMDMeshLoadList2);
            Move1(readBackTextureList, another.readBackTextureList);
            Move1(pobjectList, another.pobjectList);
        }

        public void Clear()
        {
            TextureCubeLoadList.Clear();
            Texture2DLoadList.Clear();
            MMDMeshLoadList.Clear();
            MMDMeshLoadList2.Clear();
            readBackTextureList.Clear();
            pobjectList.Clear();
        }

        public bool IsEmpty()
        {
            if (pobjectList.Count == 0)
                return false;

            return TextureCubeLoadList.Count == 0 &&
                 Texture2DLoadList.Count == 0 &&
                MMDMeshLoadList.Count == 0 &&
                MMDMeshLoadList2.Count == 0 &&
                readBackTextureList.Count == 0;
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
        }
        public void _DealStep2(GraphicsContext graphicsContext, DeviceResources deviceResources)
        {
            for (int i = 0; i < pobjectList.Count; i++)
                pobjectList[i].pipelineState.Initialize(pobjectList[i].vs, pobjectList[i].gs, pobjectList[i].ps);
            for (int i = 0; i < MMDMeshLoadList.Count; i++)
                MMDMeshLoadList[i].ReleaseUploadHeapResource();

            for (int i = 0; i < readBackTextureList.Count; i++)
                graphicsContext.UpdateReadBackTexture(readBackTextureList[i]);
        }
    }
}
