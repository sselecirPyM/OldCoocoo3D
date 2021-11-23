using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Core;
using Coocoo3D.Components;
using Coocoo3D.UI;
using System.IO;
using Coocoo3D.Present;
using Coocoo3D.ResourceWarp;

namespace Coocoo3D.FileFormat
{
    public class CooSceneObject
    {
        public CooSceneObject()
        {

        }
        public CooSceneObject(GameObject obj)
        {
            name = obj.Name;
            position = obj.Position;
            rotation = obj.Rotation;
        }
        public string type;
        public string path;
        public string name;
        public Vector3 position;
        public Quaternion rotation;
        public Dictionary<string, string> properties;
        public Dictionary<string, _cooMaterial> materials;
        public CooSceneObjectLighting lighting;
    }
    public class CooSceneObjectLighting
    {
        public Vector3 color;
    }
    public class _cooMaterial
    {
        public float metallic;
        public float roughness;
        public Dictionary<string, string> textures;
        public string unionShader;
    }
    public class Coocoo3DScene
    {
        public int formatVersion = 1;
        public List<CooSceneObject> objects;
        public Dictionary<string, string> sceneProperties;

        public static Coocoo3DScene FromScene(Coocoo3DMain main)
        {
            Coocoo3DScene scene = new Coocoo3DScene();
            scene.sceneProperties = new Dictionary<string, string>();
            scene.sceneProperties.Add("skyBox", main.RPContext.skyBoxOriTex);
            scene.sceneProperties.Add("skyBoxMultiplier", main.settings.SkyBoxLightMultiplier.ToString());
            scene.objects = new List<CooSceneObject>();
            foreach (var obj in main.CurrentScene.gameObjects)
            {
                var renderer = obj.GetComponent<MMDRendererComponent>();
                if (renderer != null)
                {
                    CooSceneObject sceneObject = new CooSceneObject(obj);
                    sceneObject.type = "mmdModel";
                    sceneObject.path = renderer.meshPath;
                    sceneObject.properties = new Dictionary<string, string>();
                    sceneObject.properties.Add("motion", renderer.motionPath);
                    sceneObject.materials = new Dictionary<string, _cooMaterial>();
                    foreach (var material in renderer.Materials)
                    {
                        _cooMaterial material1 = new _cooMaterial();
                        material1.metallic = material.innerStruct.Metallic;
                        material1.roughness = material.innerStruct.Roughness;
                        material1.unionShader = material.unionShader;
                        material1.textures = new Dictionary<string, string>(material.textures);

                        sceneObject.materials[material.Name] = material1;
                    }
                    scene.objects.Add(sceneObject);
                }
                var lighting = obj.GetComponent<LightingComponent>();
                if (lighting != null)
                {
                    CooSceneObject sceneObject = new CooSceneObject(obj);
                    sceneObject.type = "lighting";
                    sceneObject.lighting = new CooSceneObjectLighting();
                    sceneObject.lighting.color = new Vector3(lighting.Color.X, lighting.Color.Y, lighting.Color.Z);
                    scene.objects.Add(sceneObject);
                }
            }

            return scene;
        }
        public void ToScene(Coocoo3DMain main)
        {
            if (sceneProperties.TryGetValue("skyBoxMultiplier", out string multipler1) && float.TryParse(multipler1, out float multipler))
            {
                main.settings.SkyBoxLightMultiplier = multipler;
            }
            if (sceneProperties.TryGetValue("skyBox", out string skyBox))
            {
                main.RPContext.skyBoxOriTex = skyBox;
                main.RPContext.SkyBoxChanged = true;
            }
            foreach (var obj in objects)
            {
                if (obj.type == "mmdModel")
                {
                    string pmxPath = obj.path;
                    ModelPack modelPack = main.mainCaches.GetModel(pmxPath);
                    UISharedCode.PreloadTextures(main, Path.GetDirectoryName(obj.path), modelPack.pmx);

                    GameObject gameObject = new GameObject();
                    gameObject.Reload2(modelPack);
                    gameObject.Name = obj.name ?? string.Empty;
                    gameObject.RotationNextFrame = obj.rotation;
                    gameObject.PositionNextFrame = obj.position;
                    gameObject.Rotation = obj.rotation;
                    gameObject.Position = obj.position;
                    var renderer = gameObject.GetComponent<MMDRendererComponent>();
                    if (obj.properties != null)
                    {
                        if (obj.properties.TryGetValue("motion", out string motion))
                        {
                            renderer.motionPath = motion;
                        }
                    }
                    if (obj.materials != null)
                    {
                        foreach (var mat in renderer.Materials)
                        {
                            if (obj.materials.TryGetValue(mat.Name, out _cooMaterial mat1))
                            {
                                mat.innerStruct.Metallic = mat1.metallic;
                                mat.innerStruct.Roughness = mat1.roughness;
                                mat.unionShader = mat1.unionShader;
                                if (mat1.textures != null)
                                    mat.textures = new Dictionary<string, string>(mat1.textures);
                            }
                        }
                    }
                    main.CurrentScene.AddGameObject(gameObject);
                }
                else if (obj.type == "lighting")
                {
                    GameObject lighting = new GameObject();
                    LightingComponent lightingComponent = new LightingComponent();
                    lighting.AddComponent(lightingComponent);
                    lighting.Name = obj.name ?? string.Empty;
                    lighting.Rotation = obj.rotation;
                    lighting.Position = obj.position;
                    lightingComponent.Color = new Vector4(3, 3, 3, 1);
                    if (obj.lighting != null)
                    {
                        lightingComponent.Color = new Vector4(obj.lighting.color, 1);
                    }

                    lightingComponent.Range = 10;
                    main.CurrentScene.AddGameObject(lighting);
                }
            }
            main.RPContext.gameDriverContext.RequireResetPhysics = true;
        }
    }
}
