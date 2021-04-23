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
        public InShaderSettings inShaderSettings;
        public List<MMD3DEntity> entities = new List<MMD3DEntity>();
        public List<GameObject> gameObjects = new List<GameObject>();
        public List<Components.MMDRendererComponent> rendererComponents = new List<Components.MMDRendererComponent>();
        public MMD3DEntity selectedEntity;
        public List<LightingData> lightings = new List<LightingData>();
        public List<LightingData> selectedLightings = new List<LightingData>();
        public List<CameraData> cameras = new List<CameraData>();
        public int VertexCount;
        public int frameRenderIndex;
        public int progressiveRenderIndex;
        public double Time;
        public double DeltaTime;
        public bool EnableDisplay;

        public int GetSceneObjectVertexCount()
        {
            int count = 0;
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                count += rendererComponents[i].meshVertexCount;
            }
            VertexCount = count;
            return count;
        }

        public void Preprocess()
        {
            for (int i = 0; i < entities.Count; i++)
            {
                MMD3DEntity entity = entities[i];
                entity.rendererComponent.position = entity.Position;
                entity.rendererComponent.rotation = entity.Rotation;
                rendererComponents.Add(entity.rendererComponent);
            }
            for (int i = 0; i < gameObjects.Count; i++)
            {
                GameObject gameObject = gameObjects[i];
                LightingComponent lightingComponent = gameObject.GetComponent<LightingComponent>();
                if (lightingComponent != null)
                {
                    lightingComponent.Position = gameObject.Position;
                    lightingComponent.Rotation = gameObject.Rotation;
                    lightings.Add(lightingComponent.GetLightingData());
                }
            }
            lightings.Sort();
        }

        public void ClearCollections()
        {
            entities.Clear();
            gameObjects.Clear();
            lightings.Clear();
            rendererComponents.Clear();
            selectedLightings.Clear();
            cameras.Clear();
        }
    }
}
