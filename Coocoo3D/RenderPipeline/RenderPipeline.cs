using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Coocoo3D.RenderPipeline
{
    public abstract class RenderPipeline
    {
        public const int c_maxCameraPerRender = 2;

        public abstract void PrepareRenderData(RenderPipelineContext context);

        public abstract void RenderCamera(RenderPipelineContext context);

        public volatile bool Ready;

        protected Texture2D TextureStatusSelect(Texture2D texture, Texture2D loading, Texture2D unload, Texture2D error)
        {
            if (texture == null) return error;
            if (texture.Status == GraphicsObjectStatus.loaded)
                return texture;
            else if (texture.Status == GraphicsObjectStatus.loading)
                return loading;
            else if (texture.Status == GraphicsObjectStatus.unload)
                return unload;
            else
                return error;
        }

        protected PObject PObjectStatusSelect(PObject pObject, PObject loading, PObject unload, PObject error)
        {
            if (pObject == null) return unload;
            if (pObject.Status == GraphicsObjectStatus.unload)
                return unload;
            else if (pObject.Status == GraphicsObjectStatus.loaded)
                return pObject;
            else if (pObject.Status == GraphicsObjectStatus.loading)
                return loading;
            else
                return error;
        }

        protected async Task ReloadPixelShader(PixelShader pixelShader, string uri)
        {
            pixelShader.Reload(await ReadFile(uri));
        }
        protected async Task ReloadVertexShader(VertexShader vertexShader, string uri)
        {
            vertexShader.Reload(await ReadFile(uri));
        }
        protected async Task ReloadGeometryShader(GeometryShader geometryShader, string uri)
        {
            geometryShader.Reload(await ReadFile(uri));
        }
        protected async Task<IBuffer> ReadFile(string uri)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri));
            return await FileIO.ReadBufferAsync(file);
        }
        protected async Task<byte[]> ReadAllBytes(string uri)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri));
            var stream = await file.OpenReadAsync();
            DataReader dataReader = new DataReader(stream);
            await dataReader.LoadAsync((uint)stream.Size);
            byte[] data = new byte[stream.Size];
            dataReader.ReadBytes(data);
            stream.Dispose();
            dataReader.Dispose();
            return data;
        }
    }
}
