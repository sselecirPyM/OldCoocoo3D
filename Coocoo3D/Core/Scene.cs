using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;
using Coocoo3D.Base;
using System.Numerics;
using Coocoo3D.RenderPipeline;

namespace Coocoo3D.Core
{
    public class _physicsObjects
    {
        public List<Physics3DRigidBody1> rigidbodies = new List<Physics3DRigidBody1>();
        public List<Physics3DJoint1> joints = new List<Physics3DJoint1>();
    }
    public class Scene
    {
        public Settings settings = new Settings()
        {
            BackgroundColor = new Vector4(0, 0.3f, 0.3f, 0.0f),
            Wireframe = false,
            SkyBoxLightMultiplier = 3.0f,
            SkyBoxMaxQuality = 256,
            ShadowMapResolution = 2048,
            EnableAO = true,
            EnableShadow = true,
            EnableBloom = true,
            BloomIntensity = 0.1f,
            BloomRange = 0.1f,
            BloomThreshold = 1.1f,
        };

        public List<GameObject> gameObjects = new List<GameObject>();
        public List<GameObject> gameObjectLoadList = new List<GameObject>();
        public List<GameObject> gameObjectRemoveList = new List<GameObject>();
        public Physics3DScene1 physics3DScene = new Physics3DScene1();

        public Dictionary<MMDRendererComponent, _physicsObjects> physicsObjects = new Dictionary<MMDRendererComponent, _physicsObjects>();

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
                    var renderComponent = gameObject.GetComponent<MMDRendererComponent>();

                    gameObjects.Add(gameObject);
                }
                for (int i = 0; i < gameObjectRemoveList.Count; i++)
                {
                    var gameObject = gameObjectRemoveList[i];
                    gameObjects.Remove(gameObject);
                    var renderComponent = gameObject.GetComponent<MMDRendererComponent>();
                    if (renderComponent != null && physicsObjects.TryGetValue(renderComponent, out var phyObj))
                    {
                        RemovePhysics(renderComponent, phyObj.rigidbodies, phyObj.joints);
                        physicsObjects.Remove(renderComponent);
                    }
                }
                gameObjectLoadList.Clear();
                gameObjectRemoveList.Clear();
            }
        }

        public void _ResetPhysics(IList<MMDRendererComponent> rendererComponents)
        {
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                var r = rendererComponents[i];
                var phyO = GetOrCreatePhysics(r);

                r.UpdateAllMatrix();
                for (int j = 0; j < r.rigidBodyDescs.Count; j++)
                {
                    var desc = r.rigidBodyDescs[j];
                    if (desc.Type == 0) continue;
                    int index = desc.AssociatedBoneIndex;
                    if (index == -1) continue;
                    var mat1 = r.bones[index].GeneratedTransform * r.LocalToWorld;
                    Matrix4x4.Decompose(mat1, out _, out var rot, out _);
                    physics3DScene.ResetRigidBody(phyO.rigidbodies[j], Vector3.Transform(desc.Position, mat1), rot * desc.Rotation);
                }
            }
            physics3DScene.Simulation(1 / 60.0);
        }

        _physicsObjects GetOrCreatePhysics(MMDRendererComponent r)
        {
            if (!physicsObjects.TryGetValue(r, out var _PhysicsObjects))
            {
                _PhysicsObjects = new _physicsObjects();
                AddPhysics(r, _PhysicsObjects.rigidbodies, _PhysicsObjects.joints);
                physicsObjects[r] = _PhysicsObjects;
            }
            return _PhysicsObjects;
        }

        void AddPhysics(MMDRendererComponent r, List<Physics3DRigidBody1> rigidbodies, List<Physics3DJoint1> joints)
        {
            for (int j = 0; j < r.rigidBodyDescs.Count; j++)
            {
                rigidbodies.Add(new Physics3DRigidBody1());
                var desc = r.rigidBodyDescs[j];
                physics3DScene.AddRigidBody(rigidbodies[j], desc);
            }
            for (int j = 0; j < r.jointDescs.Count; j++)
            {
                joints.Add(new Physics3DJoint1());
                var desc = r.jointDescs[j];
                physics3DScene.AddJoint(joints[j], rigidbodies[desc.AssociatedRigidBodyIndex1], rigidbodies[desc.AssociatedRigidBodyIndex2], desc);
            }
        }
        void RemovePhysics(MMDRendererComponent r, List<Physics3DRigidBody1> rigidbodies, List<Physics3DJoint1> joints)
        {
            for (int j = 0; j < rigidbodies.Count; j++)
            {
                physics3DScene.RemoveRigidBody(rigidbodies[j]);
            }
            for (int j = 0; j < joints.Count; j++)
            {
                physics3DScene.RemoveJoint(joints[j]);
            }
            rigidbodies.Clear();
            joints.Clear();
        }
        void PrePhysicsSync(MMDRendererComponent r, List<Physics3DRigidBody1> rigidbodies)
        {
            for (int i = 0; i < r.rigidBodyDescs.Count; i++)
            {
                var desc = r.rigidBodyDescs[i];
                if (desc.Type != 0) continue;
                int index = desc.AssociatedBoneIndex;

                Matrix4x4 mat2 = Matrix4x4.CreateFromQuaternion(desc.Rotation) * Matrix4x4.CreateTranslation(desc.Position) * r.bones[index].GeneratedTransform * r.LocalToWorld;
                physics3DScene.MoveRigidBody(rigidbodies[i], mat2);
            }
        }

        void PhysicsSyncBack(MMDRendererComponent r, List<Physics3DRigidBody1> rigidbodies, List<Physics3DJoint1> joints)
        {
            Matrix4x4.Decompose(r.WorldToLocal, out _, out var q1, out var t1);
            for (int i = 0; i < r.rigidBodyDescs.Count; i++)
            {
                var desc = r.rigidBodyDescs[i];
                if (desc.Type == 0) continue;
                int index = desc.AssociatedBoneIndex;
                if (index == -1) continue;
                r.bones[index]._generatedTransform = Matrix4x4.CreateTranslation(-desc.Position) * Matrix4x4.CreateFromQuaternion(rigidbodies[i].GetRotation() / desc.Rotation * q1)
                    * Matrix4x4.CreateTranslation(Vector3.Transform(rigidbodies[i].GetPosition(), r.WorldToLocal));
            }
            r.UpdateMatrices(r.PhysicsNeedUpdateMatrixIndexs);

            r.UpdateAppendBones();
        }

        public void TransformToNew(MMDRendererComponent r, Vector3 position, Quaternion rotation, List<Physics3DRigidBody1> rigidbodies)
        {
            //r.LocalToWorld = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
            //Matrix4x4.Invert(r.LocalToWorld, out r.WorldToLocal);
            for (int i = 0; i < r.rigidBodyDescs.Count; i++)
            {
                var desc = r.rigidBodyDescs[i];
                if (desc.Type != RigidBodyType.Kinematic) continue;
                int index = desc.AssociatedBoneIndex;
                var bone = r.bones[index];
                Matrix4x4 mat2 = Matrix4x4.CreateFromQuaternion(desc.Rotation) * Matrix4x4.CreateTranslation(desc.Position) * r.bones[index].GeneratedTransform * r.LocalToWorld;
                physics3DScene.MoveRigidBody(rigidbodies[i], mat2);
            }
        }

        void _BoneUpdate(double playTime, float deltaTime, IList<MMDRendererComponent> rendererComponents, RenderPipeline.MainCaches caches)
        {
            UpdateGameObjects((float)playTime, rendererComponents, caches);

            float t1 = Math.Clamp(deltaTime, -0.17f, 0.17f);
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                var r = rendererComponents[i];
                var _PhysicsObjects = GetOrCreatePhysics(r);
                PrePhysicsSync(r, _PhysicsObjects.rigidbodies);
            }
            physics3DScene.Simulation(t1 >= 0 ? t1 : -t1);
            //physics3DScene.FetchResults();
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                var r = rendererComponents[i];
                physicsObjects.TryGetValue(r, out var _PhysicsObjects);
                PhysicsSyncBack(r, _PhysicsObjects.rigidbodies, _PhysicsObjects.joints);
            }
        }
        void UpdateGameObjects(float playTime1, IList<MMDRendererComponent> rendererComponents, RenderPipeline.MainCaches caches)
        {
            void UpdateGameObjects1(MMDRendererComponent rendererComponent)
            {
                rendererComponent?.SetMotionTime(playTime1, caches.GetMotion(rendererComponent.motionPath));
            }
            int threshold = 1;
            if (gameObjects.Count > threshold)
            {
                Parallel.ForEach(rendererComponents, UpdateGameObjects1);
            }
            else foreach (MMDRendererComponent rendererComponent in rendererComponents)
                {
                    UpdateGameObjects1(rendererComponent);
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
                    var renderComponent = gameObject.GetComponent<MMDRendererComponent>();
                    if (renderComponent != null)
                    {
                        var phyObj = GetOrCreatePhysics(renderComponent);
                        TransformToNew(renderComponent, gameObject.Position, gameObject.Rotation, phyObj.rigidbodies);
                        resetPhysics = true;
                    }

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

    public struct Settings
    {
        public Vector4 BackgroundColor;
        public bool Wireframe;
        public DebugRenderType DebugRenderType;

        public float SkyBoxLightMultiplier;
        public int SkyBoxMaxQuality;
        public int ShadowMapResolution;

        public bool EnableAO;
        public bool EnableShadow;
        public bool EnableBloom;
        public float BloomThreshold;
        public float BloomIntensity;
        public float BloomRange;
    }
}
