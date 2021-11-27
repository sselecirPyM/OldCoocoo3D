using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    //readonly when rendering
    public class RenderPipelineDynamicContext
    {
        public Settings settings;
        public List<GameObject> gameObjects = new List<GameObject>();
        public List<MMDRendererComponent> renderers = new List<MMDRendererComponent>();
        public List<VolumeComponent> volumes = new List<VolumeComponent>();
        public List<LightingData> lightings = new List<LightingData>();
        public Dictionary<MMDRendererComponent, int> findRenderer = new Dictionary<MMDRendererComponent, int>();
        public PassSetting currentPassSetting;
        public string passSettingPath;
        public int frameRenderIndex;
        public double Time;
        public double DeltaTime;
        public double RealDeltaTime;
        public bool EnableDisplay;

        public void Preprocess()
        {
            int rendererCount = 0;
            foreach (GameObject gameObject in gameObjects)
            {
                LightingComponent lightingComponent = gameObject.GetComponent<LightingComponent>();
                if (lightingComponent != null)
                {
                    lightingComponent.Position = gameObject.Position;
                    lightingComponent.Rotation = gameObject.Rotation;
                    lightings.Add(lightingComponent.GetLightingData());
                }
                VolumeComponent volume = gameObject.GetComponent<VolumeComponent>();
                if (volume != null)
                {
                    volume.Position = gameObject.Position;
                    volumes.Add(volume);
                }
                MMDRendererComponent rendererComponent = gameObject.GetComponent<MMDRendererComponent>();
                if (rendererComponent != null)
                {
                    rendererComponent.position = gameObject.Position;
                    rendererComponent.rotation = gameObject.Rotation;
                    renderers.Add(rendererComponent);
                    findRenderer[rendererComponent] = rendererCount;
                    rendererCount++;
                }
            }
            lightings.Sort();
        }

        public void FrameBegin()
        {
            gameObjects.Clear();
            lightings.Clear();
            volumes.Clear();
            renderers.Clear();
            findRenderer.Clear();
        }
    }
}
