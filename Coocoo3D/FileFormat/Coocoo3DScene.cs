using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Core;
using Coocoo3D.Components;
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
        public bool? skinning;
        public Vector3 position;
        public Quaternion rotation;
        public Dictionary<string, string> properties;
        public Dictionary<string, _cooMaterial> materials;
        public CooSceneObjectLighting lighting;
    }
    public class CooSceneObjectLighting
    {
        public Vector3 color;
        public float range;
    }
    public class _cooMaterial
    {
        public Dictionary<string, string> textures;
        public string unionShader;
        public bool skinning;
        public bool transparent;

        public Dictionary<string, bool> bValue;
        public Dictionary<string, int> iValue;
        public Dictionary<string, float> fValue;
        public Dictionary<string, Vector2> f2Value;
        public Dictionary<string, Vector3> f3Value;
        public Dictionary<string, Vector4> f4Value;
    }
    public class Coocoo3DScene
    {
        public int formatVersion = 1;
        public List<CooSceneObject> objects;
        public Dictionary<string, string> sceneProperties;
        public Settings settings;

        static bool _func1<T>(ref Dictionary<string, T> dict, KeyValuePair<string, object> pair)
        {
            if (pair.Value is T _t1)
            {
                dict ??= new Dictionary<string, T>();
                dict[pair.Key] = _t1;
                return true;
            }
            return false;
        }
        public static Coocoo3DScene FromScene(Coocoo3DMain main)
        {
            Coocoo3DScene scene = new Coocoo3DScene();
            scene.sceneProperties = new Dictionary<string, string>();
            scene.sceneProperties.Add("skyBox", main.RPContext.skyBoxOriTex);
            scene.objects = new List<CooSceneObject>();
            scene.settings = main.CurrentScene.settings.GetClone();
            foreach (var customValue in scene.settings.Parameters)
            {
                if (_func1(ref scene.settings.fValue, customValue)) continue;
                if (_func1(ref scene.settings.f2Value, customValue)) continue;
                if (_func1(ref scene.settings.f3Value, customValue)) continue;
                if (_func1(ref scene.settings.f4Value, customValue)) continue;
                if (_func1(ref scene.settings.bValue, customValue)) continue;
                if (_func1(ref scene.settings.iValue, customValue)) continue;
            }
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
                    sceneObject.skinning = renderer.skinning;
                    foreach (var material in renderer.Materials)
                    {
                        _cooMaterial material1 = new _cooMaterial();
                        material1.skinning = material.Skinning;
                        material1.transparent = material.Transparent;
                        material1.textures = new Dictionary<string, string>(material.textures);

                        sceneObject.materials[material.Name] = material1;

                        foreach (var customValue in material.Parameters)
                        {
                            if (_func1(ref material1.fValue, customValue)) continue;
                            if (_func1(ref material1.f2Value, customValue)) continue;
                            if (_func1(ref material1.f3Value, customValue)) continue;
                            if (_func1(ref material1.f4Value, customValue)) continue;
                            if (_func1(ref material1.bValue, customValue)) continue;
                            if (_func1(ref material1.iValue, customValue)) continue;
                        }
                    }
                    scene.objects.Add(sceneObject);
                }
                var lighting = obj.GetComponent<LightingComponent>();
                if (lighting != null)
                {
                    CooSceneObject sceneObject = new CooSceneObject(obj);
                    sceneObject.type = "lighting";
                    sceneObject.lighting = new CooSceneObjectLighting();
                    sceneObject.lighting.color = lighting.Color;
                    sceneObject.lighting.range = lighting.Range;
                    scene.objects.Add(sceneObject);
                }
            }

            return scene;
        }
        void _func2<T>(Dictionary<string, T> dict, Dictionary<string, object> target)
        {
            if (dict != null)
                foreach (var f1 in dict)
                    target[f1.Key] = f1.Value;
        }
        public void ToScene(Coocoo3DMain main)
        {
            if (settings != null)
            {
                _func2(settings.fValue, settings.Parameters);
                _func2(settings.f2Value, settings.Parameters);
                _func2(settings.f3Value, settings.Parameters);
                _func2(settings.f4Value, settings.Parameters);
                _func2(settings.iValue, settings.Parameters);
                _func2(settings.bValue, settings.Parameters);

                main.CurrentScene.settings = settings;
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

                    GameObject gameObject = new GameObject();
                    gameObject.Reload2(modelPack);
                    gameObject.Name = obj.name ?? string.Empty;
                    gameObject.RotationNextFrame = obj.rotation;
                    gameObject.PositionNextFrame = obj.position;
                    gameObject.Rotation = obj.rotation;
                    gameObject.Position = obj.position;
                    var renderer = gameObject.GetComponent<MMDRendererComponent>();
                    if (obj.skinning != null)
                        renderer.skinning = (bool)obj.skinning;
                    if (obj.properties != null)
                    {
                        if (obj.properties.TryGetValue("motion", out string motion))
                        {
                            renderer.motionPath = motion;
                        }
                    }
                    if (obj.materials != null)
                    {
                        Mat2Mat(obj.materials, renderer, main);
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
                    lightingComponent.Color = new Vector3(3, 3, 3);
                    lightingComponent.Range = 10;
                    if (obj.lighting != null)
                    {
                        lightingComponent.Color = obj.lighting.color;
                        lightingComponent.Range = obj.lighting.range;
                    }

                    main.CurrentScene.AddGameObject(lighting);
                }
            }
            main.RPContext.gameDriverContext.RequireResetPhysics = true;
        }
        void Mat2Mat(Dictionary<string, _cooMaterial> materials, MMDRendererComponent renderer, Coocoo3DMain main)
        {
            foreach (var mat in renderer.Materials)
            {
                if (materials.TryGetValue(mat.Name, out _cooMaterial mat1))
                {
                    mat.Skinning = mat1.skinning;
                    mat.Transparent = mat1.transparent;

                    _func2(mat1.fValue, mat.Parameters);
                    _func2(mat1.f2Value, mat.Parameters);
                    _func2(mat1.f3Value, mat.Parameters);
                    _func2(mat1.f4Value, mat.Parameters);
                    _func2(mat1.iValue, mat.Parameters);
                    _func2(mat1.bValue, mat.Parameters);

                    if (mat1.textures != null)
                    {
                        mat.textures = new Dictionary<string, string>(mat1.textures);
                        foreach (var tex in mat.textures)
                        {
                            main.mainCaches.Texture(tex.Value);
                        }
                    }
                }
            }
        }
    }
}
