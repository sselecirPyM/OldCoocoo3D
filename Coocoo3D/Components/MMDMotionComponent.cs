using Coocoo3D.Components;
using Coocoo3D.FileFormat;
using Coocoo3D.MMDSupport;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public class MMDMotionComponent
    {
        public Dictionary<string, List<BoneKeyFrame>> BoneKeyFrameSet { get; set; } = new Dictionary<string, List<BoneKeyFrame>>();
        public Dictionary<string, List<MorphKeyFrame>> MorphKeyFrameSet { get; set; } = new Dictionary<string, List<MorphKeyFrame>>();

        const float c_framePerSecond = 30;
        public BoneKeyFrame GetBoneMotion(string key, float time)
        {
            if (!BoneKeyFrameSet.TryGetValue(key, out var keyframeSet))
            {
                return new BoneKeyFrame() { Rotation = Quaternion.Identity };
            }
            if (keyframeSet.Count == 0) return new BoneKeyFrame() { Rotation = Quaternion.Identity };
            float frame = Math.Max(time * c_framePerSecond, 0);
            BoneKeyFrame ComputeKeyFrame(BoneKeyFrame _Left, BoneKeyFrame _Right)
            {
                float _getAmount(Interpolator interpolator, float _a)
                {
                    if (interpolator.ax == interpolator.ay && interpolator.bx == interpolator.by)
                        return _a;
                    var _curve = Utility.CubicBezierCurve.Load(interpolator.ax, interpolator.ay, interpolator.bx, interpolator.by);
                    return _curve.Sample(_a);
                }
                float t = (frame - _Left.Frame) / (_Right.Frame - _Left.Frame);
                float amountR = _getAmount(_Right.rInterpolator, t);
                float amountX = _getAmount(_Right.xInterpolator, t);
                float amountY = _getAmount(_Right.yInterpolator, t);
                float amountZ = _getAmount(_Right.zInterpolator, t);


                BoneKeyFrame newKeyFrame = new BoneKeyFrame();
                newKeyFrame.Frame = (int)MathF.Round(frame);
                newKeyFrame.rotation = Quaternion.Slerp(_Left.rotation, _Right.rotation, amountR);
                newKeyFrame.translation = new Vector3(amountX, amountY, amountZ) * _Right.translation + new Vector3(1 - amountX, 1 - amountY, 1 - amountZ) * _Left.translation;

                return newKeyFrame;
            }

            int left = 0;
            int right = keyframeSet.Count - 1;
            if (left == right) return keyframeSet[left];
            if (keyframeSet[right].Frame < frame) return keyframeSet[right];

            while (right - left > 1)
            {
                int mid = (right + left) / 2;
                if (keyframeSet[mid].Frame > frame)
                    right = mid;
                else
                    left = mid;
            }
            return ComputeKeyFrame(keyframeSet[left], keyframeSet[right]);
        }

        public float GetMorphWeight(string key, float time, out float AWeight, out float BWeight)
        {
            if (!MorphKeyFrameSet.TryGetValue(key, out var keyframeSet))
            {
                AWeight = 0;
                BWeight = 0;
                return 0.0f;
            }
            int left = 0;
            int right = keyframeSet.Count - 1;
            float indexFrame = Math.Max(time * c_framePerSecond, 0);

            float ComputeKeyFrame(MorphKeyFrame _left, MorphKeyFrame _right, float frame)
            {
                float amount = (float)(frame - _left.Frame) / (_right.Frame - _left.Frame);
                return (1 - amount) * _left.Weight + amount * _right.Weight;
            }

            if (keyframeSet.Count == 1)
            {
                AWeight = BWeight = keyframeSet[0].Weight;
                return AWeight;
            }

            if (keyframeSet[right].Frame < indexFrame)
            {
                AWeight = BWeight = keyframeSet[right].Weight;
                return AWeight;
            }

            while (right - left > 1)
            {
                int mid = (right + left) / 2;
                if (keyframeSet[mid].Frame > indexFrame)
                    right = mid;
                else
                    left = mid;
            }
            MorphKeyFrame keyFrameLeft = keyframeSet[left];
            MorphKeyFrame keyFrameRight = keyframeSet[right];

            var cacheIndex = left;
            int frameA = (int)MathF.Floor(Math.Max(time * c_framePerSecond, 0));
            int frameB = frameA + 1;
            float ComputeKeyFrame1(MorphKeyFrame _left, MorphKeyFrame _right, int frame)
            {
                float amount = (float)(frame - _left.Frame) / (_right.Frame - _left.Frame);
                return (1 - amount) * _left.Weight + amount * _right.Weight;
            }
            float GetWeight(int frame)
            {
                if (cacheIndex < keyframeSet.Count - 1 && keyframeSet[cacheIndex].Frame <= frame && keyframeSet[cacheIndex + 1].Frame >= frame)
                {
                    return ComputeKeyFrame1(keyframeSet[cacheIndex], keyframeSet[cacheIndex + 1], frame);
                }
                else if (cacheIndex < keyframeSet.Count - 2 && keyframeSet[cacheIndex + 1].Frame <= frame && keyframeSet[cacheIndex + 2].Frame >= frame)
                {
                    return ComputeKeyFrame1(keyframeSet[cacheIndex + 1], keyframeSet[cacheIndex + 2], frame);
                }
                else if (keyframeSet[keyframeSet.Count - 1].Frame <= frame)
                {
                    return keyframeSet[keyframeSet.Count - 1].Weight;
                }
                else throw new NotImplementedException("ABWeight Error");
            }
            AWeight = GetWeight(frameA);
            BWeight = GetWeight(frameB);

            return ComputeKeyFrame(keyFrameLeft, keyFrameRight, indexFrame);
        }
    }
}


namespace Coocoo3D.FileFormat
{
    public static partial class VMDFormatExtension
    {
        public static void ReloadEmpty(this MMDMotionComponent motionComponent)
        {
            lock (motionComponent)
            {
                motionComponent.BoneKeyFrameSet.Clear();
                motionComponent.MorphKeyFrameSet.Clear();
            }
        }

        public static void Reload(this MMDMotionComponent motionComponent, VMDFormat vmd)
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