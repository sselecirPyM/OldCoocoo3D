using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{
    public class Scene
    {
        public ObservableCollection<ISceneObject> sceneObjects = new ObservableCollection<ISceneObject>();
        public List<MMD3DEntity> Entities = new List<MMD3DEntity>();
        public List<MMD3DEntity> EntityLoadList = new List<MMD3DEntity>();
        public List<MMD3DEntity> EntityRemoveList = new List<MMD3DEntity>();
        public List<MMD3DEntity> EntityRefreshList = new List<MMD3DEntity>();
        public List<GameObject> gameObjects = new List<GameObject>();
        public List<GameObject> gameObjectLoadList = new List<GameObject>();
        public List<GameObject> gameObjectRemoveList = new List<GameObject>();

        public void AddGameObject(GameObject gameObject)
        {
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
        public void DealProcessList(Coocoo3DPhysics.Physics3DScene physics3DScene)
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
                for (int i = 0; i < EntityRefreshList.Count; i++)
                {
                    EntityRefreshList[i].rendererComponent.RemovePhysics(physics3DScene);
                    EntityRefreshList[i].rendererComponent.AddPhysics(physics3DScene);
                }
                EntityLoadList.Clear();
                EntityRemoveList.Clear();
                EntityRefreshList.Clear();

                for (int i = 0; i < gameObjectLoadList.Count; i++)
                {
                    gameObjects.Add(gameObjectLoadList[i]);
                }
                for (int i = 0; i < gameObjectRemoveList.Count; i++)
                {
                    gameObjects.Remove(gameObjectRemoveList[i]);
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
                for (int i = 0; i < sceneObjects.Count; i++)
                {
                    if (sceneObjects[i] is MMD3DEntity entity)
                    {
                        Entities.Add(entity);
                    }
                }
            }
        }
    }
}
