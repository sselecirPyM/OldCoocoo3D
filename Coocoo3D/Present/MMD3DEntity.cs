using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3DGraphics;
using System.ComponentModel;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWarp;

namespace Coocoo3D.Present
{
    public class MMD3DEntity : ISceneObject, INotifyPropertyChanged
    {
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.Identity;
        public Vector3 PositionNextFrame;
        public Quaternion RotationNextFrame = Quaternion.Identity;
        public bool NeedTransform;
        public bool LockMotion;

        public string Name;
        public string Description;
        public string ModelPath;

        public MMDRendererComponent rendererComponent = new MMDRendererComponent();
        public MMDBoneComponent boneComponent = new MMDBoneComponent();
        public MMDMotionComponent motionComponent = new MMDMotionComponent();
        public MMDMorphStateComponent morphStateComponent = new MMDMorphStateComponent();

        public bool ComponentReady = false;

        public event PropertyChangedEventHandler PropertyChanged;
        public void PropChange(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }


        public float PrevUpdateTime;
        public void SetMotionTime(float time)
        {
            if (!ComponentReady) return;
            if (!LockMotion)
            {
                PrevUpdateTime = time;
                lock (motionComponent)
                {
                    morphStateComponent.SetPose(motionComponent, time);
                    morphStateComponent.ComputeWeight();
                    boneComponent.SetPose(motionComponent, morphStateComponent, time);
                }
            }
            else
            {
                morphStateComponent.ComputeWeight();
                boneComponent.SetPose2(morphStateComponent);
            }
            boneComponent.ComputeMatricesData();
            rendererComponent.SetPose(morphStateComponent);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
namespace Coocoo3D.FileFormat
{
    public static partial class PMXFormatExtension
    {
        public static void Reload2(this MMD3DEntity entity, ProcessingList processingList, ModelPack modelPack, List<Texture2D> textures, string ModelPath)
        {
            var modelResource = modelPack.pmx;
            entity.Name = string.Format("{0} {1}", modelResource.Name, modelResource.NameEN);
            entity.Description = string.Format("{0}\n{1}", modelResource.Description, modelResource.DescriptionEN);
            entity.ModelPath = ModelPath;
            entity.motionComponent.ReloadEmpty();

            ReloadModel(entity, processingList, modelPack, textures);
        }

        public static void ReloadModel(this MMD3DEntity entity, ProcessingList processingList, ModelPack modelPack, List<Texture2D> textures)
        {
            entity.ComponentReady = false;
            var modelResource = modelPack.pmx;
            entity.morphStateComponent.Reload(modelResource);
            entity.boneComponent.Reload(modelResource);

            entity.rendererComponent.Reload(modelPack);
            processingList.AddObject(new MeshAppendUploadPack(entity.rendererComponent.meshAppend, entity.rendererComponent.meshPosData));
            //processingList.AddObject(entity.rendererComponent.meshParticleBuffer);
            entity.rendererComponent.textures = textures;

            entity.ComponentReady = true;
        }
    }
}
