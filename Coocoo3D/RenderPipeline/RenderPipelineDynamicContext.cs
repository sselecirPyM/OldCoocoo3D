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
    public class RenderPipelineDynamicContext
    {
        public Settings settings;
        //public List<MMD3DEntity> entities = new List<MMD3DEntity>();
        public List<GameObject> gameObjects = new List<GameObject>();
        public List<Components.MMDRendererComponent> renderers = new List<Components.MMDRendererComponent>();
        public List<Components.VolumeComponent> volumes = new List<VolumeComponent>();
        //public MMD3DEntity selectedEntity;
        public List<LightingData> lightings = new List<LightingData>();
        public List<LightingData> selectedLightings = new List<LightingData>();
        public List<CameraData> cameras = new List<CameraData>();
        public PassSetting currentPassSetting;
        public int VertexCount;
        public int frameRenderIndex;
        public int progressiveRenderIndex;
        public double Time;
        public double DeltaTime;
        public double RealDeltaTime;
        public bool EnableDisplay;

        public int GetSceneObjectVertexCount()
        {
            int count = 0;
            for (int i = 0; i < renderers.Count; i++)
            {
                count += renderers[i].meshVertexCount;
            }
            VertexCount = count;
            return count;
        }

        public void Preprocess()
        {
            //foreach (MMD3DEntity entity in entities)
            //{
            //    entity.rendererComponent.position = entity.Position;
            //    entity.rendererComponent.rotation = entity.Rotation;
            //    renderers.Add(entity.rendererComponent);
            //}
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
                }
            }
            lightings.Sort();
        }

        public void ClearCollections()
        {
            //entities.Clear();
            gameObjects.Clear();
            lightings.Clear();
            volumes.Clear();
            renderers.Clear();
            selectedLightings.Clear();
            cameras.Clear();
        }
    }
}
