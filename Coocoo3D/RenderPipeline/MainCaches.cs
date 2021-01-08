using Coocoo3D.FileFormat;
using Coocoo3D.ResourceWarp;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Coocoo3D.RenderPipeline
{
    public class MainCaches
    {
        public Dictionary<string, Texture2DPack> TextureCaches = new Dictionary<string, Texture2DPack>();
        public Dictionary<string, RPShaderPack> RPShaderPackCaches = new Dictionary<string, RPShaderPack>();

        public Dictionary<string, ModelPack> ModelPackCaches = new Dictionary<string, ModelPack>();

        public SingleLocker textureTaskLocker;
        public SingleLocker shaderTaskLocker;
        public SingleLocker modelTaskLocker;
        public void ReloadTextures(ProcessingList processingList, Action _RequireRender)
        {
            if (textureTaskLocker.GetLocker())
            {
                List<Texture2DPack> packs = new List<Texture2DPack>();
                lock (TextureCaches)
                    foreach (var texturePack in TextureCaches.Values)
                        packs.Add(texturePack);

                for (int i = 0; i < packs.Count; i++)
                {
                    var tex = packs[i];
                    if (tex.loadLocker.GetLocker())
                    {
                        Task.Factory.StartNew(async (object a) =>
                        {
                            Texture2DPack texturePack1 = (Texture2DPack)a;
                            try
                            {
                                var file = await texturePack1.folder.GetFileAsync(texturePack1.relativePath);
                                var attr = await file.GetBasicPropertiesAsync();
                                if (attr.DateModified != texturePack1.lastModifiedTime || texturePack1.texture2D.Status != GraphicsObjectStatus.loaded)
                                {
                                    Uploader uploader = new Uploader();
                                    if (await texturePack1.ReloadTexture(file, uploader))
                                        processingList.AddObject(new Texture2DUploadPack(texturePack1.texture2D, uploader));
                                    _RequireRender();
                                    texturePack1.lastModifiedTime = attr.DateModified;
                                }
                            }
                            catch
                            {
                                texturePack1.Mark(GraphicsObjectStatus.error);
                                _RequireRender();
                            }
                            finally
                            {
                                texturePack1.loadLocker.FreeLocker();
                            }
                        }, tex);
                    }
                }
                textureTaskLocker.FreeLocker();
            }
        }
        public void ReloadShaders(ProcessingList processingList, RPAssetsManager RPAssetsManager, Action _RequireRender)
        {
            if (shaderTaskLocker.GetLocker())
            {
                List<RPShaderPack> packs = new List<RPShaderPack>();
                lock (RPShaderPackCaches)
                    foreach (var shaderPack in RPShaderPackCaches.Values)
                        packs.Add(shaderPack);

                for (int i = 0; i < packs.Count; i++)
                {
                    var shaderPack1 = packs[i];
                    if (shaderPack1.loadLocker.GetLocker())
                    {
                        Task.Factory.StartNew(async (object a) =>
                        {
                            RPShaderPack pack1 = (RPShaderPack)a;
                            try
                            {
                                var file = await pack1.folder.GetFileAsync(pack1.relativePath);
                                var attr = await file.GetBasicPropertiesAsync();
                                if (attr.DateModified != pack1.lastModifiedTime || pack1.Status == GraphicsObjectStatus.error)
                                {
                                    pack1.lastModifiedTime = attr.DateModified;
                                    pack1.Mark(GraphicsObjectStatus.loading);
                                    _RequireRender();
                                    if (await pack1.Reload(file, RPAssetsManager, processingList))
                                    {

                                    }
                                    _RequireRender();
                                }
                            }
                            catch
                            {
                                pack1.Mark(GraphicsObjectStatus.error);
                                _RequireRender();
                            }
                            finally
                            {
                                pack1.loadLocker.FreeLocker();
                            }
                        }, shaderPack1);
                    }
                }
                shaderTaskLocker.FreeLocker();
            }
        }
    }
}
