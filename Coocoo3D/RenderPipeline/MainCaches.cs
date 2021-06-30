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

        public Dictionary<string, ModelPack> ModelPackCaches = new Dictionary<string, ModelPack>();

        private SingleLocker textureTaskLocker;
        public void ReloadTextures(ProcessingList processingList, Action _RequireRender)
        {
            if (textureTaskLocker.GetLocker())
            {
                List<Texture2DPack> packs = new List<Texture2DPack>();
                lock (TextureCaches)
                    packs.AddRange(TextureCaches.Values);

                for (int i = 0; i < packs.Count; i++)
                {
                    var tex = packs[i];
                    if (tex.folder == null) continue;
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
    }
}
