using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;
using Coocoo3DPhysics;

namespace Coocoo3D.Core
{
    public class Scene
    {
        public ObservableCollection<ISceneObject> sceneObjects = new ObservableCollection<ISceneObject>();
        public List<MMD3DEntity> Entities = new List<MMD3DEntity>();
        public List<MMD3DEntity> EntityLoadList = new List<MMD3DEntity>();
        public List<MMD3DEntity> EntityRemoveList = new List<MMD3DEntity>();
        public List<GameObject> gameObjects = new List<GameObject>();
        public List<GameObject> gameObjectLoadList = new List<GameObject>();
        public List<GameObject> gameObjectRemoveList = new List<GameObject>();
        public Physics3DScene physics3DScene = new Physics3DScene();

        public void AddGameObject(GameObject gameObject)
        {
            gameObject.PositionNextFrame = gameObject.Position;
            gameObject.RotationNextFrame = gameObject.Rotation;
            lock (this)
            {
                gameObjectLoadList.Add(gameObject);
            }
            sceneObjects.Add(gameObject);
        }

        public void RemoveGameObject(GameObject gameObject)
        {
            lock (this)
            {
                gameObjectRemoveList.Add(gameObject);
            }
        }

        public void AddSceneObject(MMD3DEntity entity)
        {
            lock (this)
            {
                EntityLoadList.Add(entity);
            }
            sceneObjects.Add(entity);
        }
        public void RemoveSceneObject(MMD3DEntity entity)
        {
            lock (this)
            {
                EntityRemoveList.Add(entity);
            }
        }
        public void DealProcessList()
        {
            lock (this)
            {
                for (int i = 0; i < EntityLoadList.Count; i++)
                {
                    Entities.Add(EntityLoadList[i]);
                    EntityLoadList[i].rendererComponent.AddPhysics(physics3DScene);
                }
                for (int i = 0; i < EntityRemoveList.Count; i++)
                {
                    EntityRemoveList[i].rendererComponent.RemovePhysics(physics3DScene);
                    Entities.Remove(EntityRemoveList[i]);
                }
                EntityLoadList.Clear();
                EntityRemoveList.Clear();

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
        public void SortObjects()
        {
            lock (this)
            {
                Entities.Clear();
                gameObjects.Clear();
                for (int i = 0; i < sceneObjects.Count; i++)
                {
                    if (sceneObjects[i] is MMD3DEntity entity)
                    {
                        Entities.Add(entity);
                    }
                    else if ((sceneObjects[i] is GameObject gameObject))
                    {
                        gameObjects.Add(gameObject);
                    }
                }
            }
        }

        public void _ResetPhysics(IList<MMDRendererComponent> rendererComponents)
        {
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                rendererComponents[i].ResetPhysics(physics3DScene);
            }
            physics3DScene.Simulate(1 / 60.0);
            physics3DScene.FetchResults();
        }

        public void _BoneUpdate(double playTime, float deltaTime, IList<MMDRendererComponent> rendererComponents, IList<MMD3DEntity> entities)
        {
            void UpdateEntities(float playTime1)
            {
                int threshold = 1;
                if (entities.Count > threshold)
                {
                    Parallel.ForEach(entities, (MMD3DEntity e) =>
                    {
                        e.rendererComponent.motionComponent = e.motionComponent;
                        e.rendererComponent.SetMotionTime(playTime1);
                    });
                }
                else foreach (MMD3DEntity e in entities)
                    {
                        e.rendererComponent.motionComponent = e.motionComponent;
                        e.rendererComponent.SetMotionTime(playTime1);
                    }
            }

            void UpdateGameObjects(float playTime1)
            {
                int threshold = 1;
                if (gameObjects.Count > threshold)
                {
                    Parallel.ForEach(gameObjects, (GameObject gameObject) =>
                    {
                        var renderComponent = gameObject.GetComponent<MMDRendererComponent>();
                        if (renderComponent != null)
                        {
                            var motionComponent = gameObject.GetComponent<MMDMotionComponent>();
                            renderComponent.motionComponent = motionComponent;
                            renderComponent.SetMotionTime(playTime1);
                        }
                    });
                }
                else foreach (GameObject gameObject in gameObjects)
                    {
                        var renderComponent = gameObject.GetComponent<MMDRendererComponent>();
                        if (renderComponent != null)
                        {
                            var motionComponent = gameObject.GetComponent<MMDMotionComponent>();
                            renderComponent.motionComponent = motionComponent;
                            renderComponent.SetMotionTime(playTime1);
                        }
                    }
            }
            UpdateEntities((float)playTime);
            UpdateGameObjects((float)playTime);

            float t1 = Math.Clamp(deltaTime, -0.17f, 0.17f);
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                rendererComponents[i].PrePhysicsSync(physics3DScene);
            }
            physics3DScene.Simulate(t1 >= 0 ? t1 : -t1);
            physics3DScene.FetchResults();
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                rendererComponents[i].PhysicsSync(physics3DScene);
            }
        }

        public void Simulation(double playTime, double deltaTime, IList<MMDRendererComponent> rendererComponents, IList<MMD3DEntity> entities, bool resetPhysics)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.Position != entity.PositionNextFrame || entity.Rotation != entity.RotationNextFrame)
                {
                    entity.Position = entity.PositionNextFrame;
                    entity.Rotation = entity.RotationNextFrame;
                    entity.rendererComponent.TransformToNew(physics3DScene, entity.Position, entity.Rotation);

                    resetPhysics = true;
                }
            }
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
                _BoneUpdate(playTime, (float)deltaTime, rendererComponents, entities);
                _ResetPhysics(rendererComponents);
            }
            _BoneUpdate(playTime, (float)deltaTime, rendererComponents, entities);
        }
    }
}
