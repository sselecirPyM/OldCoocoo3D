using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{
    //场景类有助于用户在两个场景间复制数据，而不是有助于销毁物体。
    public class Scene
    {
        //unsafe
        public List<MMD3DEntity> Entities = new List<MMD3DEntity>();
        //unsafe
        public List<Lighting> Lightings = new List<Lighting>();
        public ObservableCollection<ISceneObject> sceneObjects = new ObservableCollection<ISceneObject>();

        public List<MMD3DEntity> EntityLoadList = new List<MMD3DEntity>();
        public List<Lighting> LightingLoadList = new List<Lighting>();
        public List<MMD3DEntity> EntityRemoveList = new List<MMD3DEntity>();
        public List<MMD3DEntity> EntityRefreshList = new List<MMD3DEntity>();
        public List<Lighting> LightingRemoveList = new List<Lighting>();

        public void AddSceneObject(MMD3DEntity entity)
        {
            lock (this)
            {
                EntityLoadList.Add(entity);
            }
            sceneObjects.Add(entity);
        }
        public void AddSceneObject(Lighting lighting)
        {
            lock (this)
            {
                LightingLoadList.Add(lighting);
            }
            sceneObjects.Add(lighting);
        }
        public void RemoveSceneObject(MMD3DEntity entity)
        {
            lock (this)
            {
                EntityRemoveList.Add(entity);
            }
        }
        public void RemoveSceneObject(Lighting lighting)
        {
            lock (this)
            {
                LightingRemoveList.Add(lighting);
            }
        }
        public void DealProcessList(Coocoo3DPhysics.Physics3DScene physics3DScene)
        {
            lock (this)
            {
                for (int i = 0; i < EntityLoadList.Count; i++)
                {
                    Entities.Add(EntityLoadList[i]);
                    EntityLoadList[i].boneComponent.AddPhysics(physics3DScene);
                }
                for (int i = 0; i < LightingLoadList.Count; i++)
                {
                    Lightings.Add(LightingLoadList[i]);
                }
                for (int i = 0; i < EntityRemoveList.Count; i++)
                {
                    EntityRemoveList[i].boneComponent.RemovePhysics(physics3DScene);
                    Entities.Remove(EntityRemoveList[i]);
                }
                for (int i = 0; i < EntityRefreshList.Count; i++)
                {
                    EntityRefreshList[i].boneComponent.RemovePhysics(physics3DScene);
                    EntityRefreshList[i].boneComponent.AddPhysics(physics3DScene);
                }
                for (int i = 0; i < LightingRemoveList.Count; i++)
                {
                    Lightings.Remove(LightingRemoveList[i]);
                }
                EntityLoadList.Clear();
                LightingLoadList.Clear();
                EntityRemoveList.Clear();
                EntityRefreshList.Clear();
                LightingRemoveList.Clear();
            }
        }
        public void SortObjects()
        {
            lock (this)
            {
                Entities.Clear();
                Lightings.Clear();
                for (int i = 0; i < sceneObjects.Count; i++)
                {
                    if (sceneObjects[i] is MMD3DEntity entity)
                    {
                        Entities.Add(entity);
                    }
                    else if (sceneObjects[i] is Lighting lighting)
                    {
                        Lightings.Add(lighting);
                    }
                }
            }
        }
    }
}
