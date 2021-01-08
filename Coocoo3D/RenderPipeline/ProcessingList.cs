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
        public List<IRenderTexture> RenderTextureUpdateList = new List<IRenderTexture>();
        public List<MMDMesh> MMDMeshLoadList = new List<MMDMesh>();
        public List<MeshAppendUploadPack> MMDMeshLoadList2 = new List<MeshAppendUploadPack>();
        public List<StaticBuffer> staticBufferList = new List<StaticBuffer>();
        public List<ReadBackTexture2D> readBackTextureList = new List<ReadBackTexture2D>();
        public List<TwinBuffer> twinBufferList = new List<TwinBuffer>();
        public List<PObject>[] pobjectLists = new List<PObject>[] { new List<PObject>(), new List<PObject>(), new List<PObject>(), };
        public List<ComputePO>[] computePObjectLists = new List<ComputePO>[] { new List<ComputePO>(), };

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
        public void AddObject(IRenderTexture texture)
        {
            lock (RenderTextureUpdateList)
            {
                RenderTextureUpdateList.Add(texture);
            }
        }
        public void AddObject(ReadBackTexture2D texture)
        {
            lock (readBackTextureList)
            {
                readBackTextureList.Add(texture);
            }
        }
        public void AddObject(StaticBuffer buffer)
        {
            lock (staticBufferList)
            {
                staticBufferList.Add(buffer);
            }
        }
        public void AddObject(TwinBuffer buffer)
        {
            lock (twinBufferList)
            {
                twinBufferList.Add(buffer);
            }
        }
        /// <summary>添加到上传列表</summary>
        public void UL(PObject pObject, int slot)
        {
            lock (pobjectLists[slot])
            {
                pobjectLists[slot].Add(pObject);
            }
        }
        public void UL(ComputePO pObject, int slot)
        {
            lock (computePObjectLists[slot])
            {
                computePObjectLists[slot].Add(pObject);
            }
        }

        public void MoveToAnother(ProcessingList another)
        {
            Move1(TextureCubeLoadList, another.TextureCubeLoadList);
            Move1(Texture2DLoadList, another.Texture2DLoadList);
            Move1(RenderTextureUpdateList, another.RenderTextureUpdateList);
            Move1(MMDMeshLoadList, another.MMDMeshLoadList);
            Move1(MMDMeshLoadList2, another.MMDMeshLoadList2);
            Move1(readBackTextureList, another.readBackTextureList);
            Move1(staticBufferList, another.staticBufferList);
            Move1(twinBufferList, another.twinBufferList);
            for (int i = 0; i < pobjectLists.Length; i++)
                Move1(pobjectLists[i], another.pobjectLists[i]);
            for (int i = 0; i < computePObjectLists.Length; i++)
                Move1(computePObjectLists[i], another.computePObjectLists[i]);
        }

        public void Clear()
        {
            TextureCubeLoadList.Clear();
            Texture2DLoadList.Clear();
            RenderTextureUpdateList.Clear();
            MMDMeshLoadList.Clear();
            MMDMeshLoadList2.Clear();
            readBackTextureList.Clear();
            staticBufferList.Clear();
            twinBufferList.Clear();
            for (int i = 0; i < pobjectLists.Length; i++)
                pobjectLists[i].Clear();
            for (int i = 0; i < computePObjectLists.Length; i++)
                computePObjectLists[i].Clear();
        }

        public bool IsEmpty()
        {
            for (int i = 0; i < pobjectLists.Length; i++)
                if (pobjectLists[i].Count == 0)
                    return false;
            for (int i = 0; i < computePObjectLists.Length; i++)
                if (computePObjectLists[i].Count == 0)
                    return false;

            return TextureCubeLoadList.Count == 0 &&
                 Texture2DLoadList.Count == 0 &&
                RenderTextureUpdateList.Count == 0 &&
                MMDMeshLoadList.Count == 0 &&
                MMDMeshLoadList2.Count == 0 &&
                staticBufferList.Count == 0 &&
                readBackTextureList.Count == 0;
        }

        public void UnsafeAdd(IRenderTexture texture)
        {
            RenderTextureUpdateList.Add(texture);
        }

        public void UnsafeAdd(MMDMesh mesh)
        {
            MMDMeshLoadList.Add(mesh);
        }

        public void UnsafeAdd(ReadBackTexture2D texture)
        {
            readBackTextureList.Add(texture);
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
            for (int i = 0; i < staticBufferList.Count; i++)
                graphicsContext.UploadBuffer1(staticBufferList[i]);
        }
        public void _DealStep2(GraphicsContext graphicsContext, DeviceResources deviceResources)
        {
            for (int i = 0; i < MMDMeshLoadList.Count; i++)
                MMDMeshLoadList[i].ReleaseUploadHeapResource();
            for (int i = 0; i < staticBufferList.Count; i++)
                staticBufferList[i].ReleaseUploadHeapResource();

            for (int i = 0; i < RenderTextureUpdateList.Count; i++)
                graphicsContext.UpdateRenderTexture(RenderTextureUpdateList[i]);
            for (int i = 0; i < readBackTextureList.Count; i++)
                graphicsContext.UpdateReadBackTexture(readBackTextureList[i]);
            for (int i = 0; i < twinBufferList.Count; i++)
                twinBufferList[i].Initialize(deviceResources);
        }
    }
}
