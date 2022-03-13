using Coocoo3D.Components;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.FileFormat
{
    public static partial class VMDFormatExtension
    {
        public static void ReloadEmpty(this MMDMotion motionComponent)
        {
            lock (motionComponent)
            {
                motionComponent.BoneKeyFrameSet.Clear();
                motionComponent.MorphKeyFrameSet.Clear();
            }
        }

        public static void Reload(this MMDMotion motionComponent, VMDFormat vmd)
        {
            lock (motionComponent)
            {
                motionComponent.BoneKeyFrameSet.Clear();
                motionComponent.MorphKeyFrameSet.Clear();

                foreach (var pair in vmd.BoneKeyFrameSet)
                {
                    motionComponent.BoneKeyFrameSet.Add(pair.Key, new List<BoneKeyFrame>(pair.Value));
                }
                foreach (var pair in vmd.MorphKeyFrameSet)
                {
                    motionComponent.MorphKeyFrameSet.Add(pair.Key, new List<MorphKeyFrame>(pair.Value));
                }
            }
        }
    }
}
