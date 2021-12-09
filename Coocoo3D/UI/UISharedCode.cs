using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.FileFormat;
using Coocoo3D.Present;
using System.Numerics;
using Coocoo3D.Utility;
using Coocoo3D.Core;
using System.Threading;
using Coocoo3D.ResourceWarp;

namespace Coocoo3D.UI
{
    public static class UISharedCode
    {
        public static void LoadEntityIntoScene(Coocoo3DMain appBody, Scene scene, FileInfo pmxFile, DirectoryInfo storageFolder)
        {
            string pmxPath = pmxFile.FullName;
            ModelPack modelPack = appBody.mainCaches.GetModel(pmxPath);
            PreloadTextures(appBody, storageFolder.FullName, modelPack.pmx);
            GameObject gameObject = new GameObject();
            gameObject.Reload2(modelPack);
            scene.AddGameObject(gameObject);

            appBody.RequireRender();
        }
        public static void NewLighting(Coocoo3DMain appBody)
        {
            GameObject lighting = new GameObject();
            Components.LightingComponent lightingComponent = new Components.LightingComponent();
            lighting.AddComponent(lightingComponent);
            lighting.Name = "Lighting";
            lighting.Rotation = Quaternion.CreateFromYawPitchRoll(0, 1.3962634015954636615389526147909f, 0);
            lighting.Position = new Vector3(0, 1, 0);
            lightingComponent.Color = new Vector3(3, 3, 3);
            lightingComponent.Range = 10;
            appBody.CurrentScene.AddGameObject(lighting);
            appBody.RequireRender();
        }
        public static void NewVolume(Coocoo3DMain appBody)
        {
            GameObject volume = new GameObject();
            Components.VolumeComponent volumeComponent = new Components.VolumeComponent();
            volume.AddComponent(volumeComponent);
            volume.Name = "Volume";
            volume.Rotation = Quaternion.Identity;
            volume.Position = new Vector3(0, 25, 0);
            volumeComponent.Size = new Vector3(100, 50, 100);
            appBody.CurrentScene.AddGameObject(volume);
            appBody.RequireRender();
        }

        public static void PreloadTextures(Coocoo3DMain appBody, string storageFolder, PMXFormat pmx)
        {
            foreach (var vTex in pmx.Textures)
            {
                string relativePath = vTex.TexturePath.Replace("//", "\\").Replace('/', '\\');
                string texPath = Path.GetFullPath(relativePath, storageFolder);
                appBody.mainCaches.Texture(texPath);
            }
        }

        public static BinaryReader OpenReader(FileInfo file) => new BinaryReader(file.OpenRead());

    }
}
