using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;
using Coocoo3D.Base;

namespace Coocoo3D.Core
{
    public class Scene
    {
        public List<GameObject> gameObjects = new List<GameObject>();
        public List<GameObject> gameObjectLoadList = new List<GameObject>();
        public List<GameObject> gameObjectRemoveList = new List<GameObject>();
        public Physics3DScene1 physics3DScene = new Physics3DScene1();

        public void AddGameObject(GameObject gameObject)
        {
            gameObject.PositionNextFrame = gameObject.Position;
            gameObject.RotationNextFrame = gameObject.Rotation;
            lock (this)
            {
                gameObjectLoadList.Add(gameObject);
            }
        }

        public void RemoveGameObject(GameObject gameObject)
        {
            lock (this)
            {
                gameObjectRemoveList.Add(gameObject);
            }
        }

        public void DealProcessList()
        {
            lock (this)
            {
                for (int i = 0; i < gameObjectLoadList.Count; i++)
                {
                    var gameObject = gameObjectLoadList[i];
                    gameObject.GetComponent<MMDRendererComponent>()?.AddPhysics(physics3DScene);
                    gameObjects.Add(gameObject);
                }
                for (int i = 0; i < gameObjectRemoveList.Count; i++)
                {
                    var gameObject = gameObjectRemoveList[i];
                    gameObject.GetComponent<MMDRendererComponent>()?.RemovePhysics(physics3DScene);
                    gameObjects.Remove(gameObject);
                }
                gameObjectLoadList.Clear();
                gameObjectRemoveList.Clear();
            }
        }
        //public void SortObjects()
        //{
        //    lock (this)
        //    {
        //        gameObjects.Clear();
        //        for (int i = 0; i < sceneObjects.Count; i++)
        //        {
        //            if ((sceneObjects[i] is GameObject gameObject))
        //            {
        //                gameObjects.Add(gameObject);
        //            }
        //        }
        //    }
        //}

        public void _ResetPhysics(IList<MMDRendererComponent> rendererComponents)
        {
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                rendererComponents[i].ResetPhysics(physics3DScene);
            }
            physics3DScene.Simulation(1 / 60.0);
        }

        public void _BoneUpdate(double playTime, float deltaTime, IList<MMDRendererComponent> rendererComponents, RenderPipeline.MainCaches caches)
        {

            UpdateGameObjects((float)playTime, caches);

            float t1 = Math.Clamp(deltaTime, -0.17f, 0.17f);
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                rendererComponents[i].PrePhysicsSync(physics3DScene);
            }
            physics3DScene.Simulation(t1 >= 0 ? t1 : -t1);
            //physics3DScene.FetchResults();
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                rendererComponents[i].PhysicsSync(physics3DScene);
            }
        }
        void UpdateGameObjects(float playTime1, RenderPipeline.MainCaches caches)
        {
            void UpdateGameObjects1(GameObject gameObject)
            {
                var renderComponent = gameObject.GetComponent<MMDRendererComponent>();
                if (renderComponent != null)
                {
                    if (caches.motions.TryGetValue(renderComponent.motionPath, out var motion))
                    {

                    }
                    renderComponent.SetMotionTime(playTime1, motion);
                }
            }
            int threshold = 1;
            if (gameObjects.Count > threshold)
            {
                Parallel.ForEach(gameObjects, UpdateGameObjects1);
            }
            else foreach (GameObject gameObject in gameObjects)
                {
                    UpdateGameObjects1(gameObject);
                }
        }

        public void Simulation(double playTime, double deltaTime, IList<MMDRendererComponent> rendererComponents, RenderPipeline.MainCaches caches, bool resetPhysics)
        {
            for (int i = 0; i < gameObjects.Count; i++)
            {
                var gameObject = gameObjects[i];
                if (gameObject.Position != gameObject.PositionNextFrame || gameObject.Rotation != gameObject.RotationNextFrame)
                {
                    gameObject.Position = gameObject.PositionNextFrame;
                    gameObject.Rotation = gameObject.RotationNextFrame;
                    gameObject.GetComponent<MMDRendererComponent>()?.TransformToNew(physics3DScene, gameObject.Position, gameObject.Rotation);

                    resetPhysics = true;
                }
            }
            if (resetPhysics)
            {
                _ResetPhysics(rendererComponents);
                _BoneUpdate(playTime, (float)deltaTime, rendererComponents, caches);
                _ResetPhysics(rendererComponents);
            }
            _BoneUpdate(playTime, (float)deltaTime, rendererComponents, caches);
        }
    }
}
