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
        public Dictionary<string, DataSetStruct<BoneKeyFrame>> BoneKeyFrameSet { get; set; } = new Dictionary<string, DataSetStruct<BoneKeyFrame>>();
        public Dictionary<string, DataSetStruct<MorphKeyFrame>> MorphKeyFrameSet { get; set; } = new Dictionary<string, DataSetStruct<MorphKeyFrame>>();

        const float c_framePerSecond = 30;
        public class DataSetStruct<T> : IList<T>
        {
            public DataSetStruct(IEnumerable<T> list)
            {
                dataSet = new List<T>(list);
            }
            public int PrevIndex;
            List<T> dataSet;

            public T this[int index] { get => ((IList<T>)dataSet)[index]; set => ((IList<T>)dataSet)[index] = value; }

            public int Count => ((ICollection<T>)dataSet).Count;

            public bool IsReadOnly => ((ICollection<T>)dataSet).IsReadOnly;

            public void Add(T item)
            {
                ((ICollection<T>)dataSet).Add(item);
            }

            public void Clear()
            {
                ((ICollection<T>)dataSet).Clear();
            }

            public bool Contains(T item)
            {
                return ((ICollection<T>)dataSet).Contains(item);
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                ((ICollection<T>)dataSet).CopyTo(array, arrayIndex);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return ((IEnumerable<T>)dataSet).GetEnumerator();
            }

            public int IndexOf(T item)
            {
                return ((IList<T>)dataSet).IndexOf(item);
            }

            public void Insert(int index, T item)
            {
                ((IList<T>)dataSet).Insert(index, item);
            }

            public bool Remove(T item)
            {
                return ((ICollection<T>)dataSet).Remove(item);
            }

            public void RemoveAt(int index)
            {
                ((IList<T>)dataSet).RemoveAt(index);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)dataSet).GetEnumerator();
            }
        }
        //这个函数缓存之前的结果，顺序访问能提高性能。
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
            var cacheIndex = keyframeSet.PrevIndex;
            if (cacheIndex < keyframeSet.Count - 1 && keyframeSet[cacheIndex].Frame < frame && keyframeSet[cacheIndex + 1].Frame > frame)
            {
                return ComputeKeyFrame(keyframeSet[cacheIndex], keyframeSet[cacheIndex + 1]);
            }
            else if (cacheIndex < keyframeSet.Count - 2 && keyframeSet[cacheIndex + 1].Frame < frame && keyframeSet[cacheIndex + 2].Frame > frame)
            {
                keyframeSet.PrevIndex++;
                return ComputeKeyFrame(keyframeSet[cacheIndex + 1], keyframeSet[cacheIndex + 2]);
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
            keyframeSet.PrevIndex = left;
            return ComputeKeyFrame(keyframeSet[left], keyframeSet[right]);
        }
        //这个函数缓存之前的结果，顺序访问能提高性能。
        public MorphKeyFrame GetMorphMotion(string key, float time)
        {
            if (!MorphKeyFrameSet.TryGetValue(key, out var keyframeSet))
            {
                return new MorphKeyFrame();
            }
            float frame = Math.Max(time * c_framePerSecond, 0);
            MorphKeyFrame ComputeKeyFrame(MorphKeyFrame _left, MorphKeyFrame _right)
            {
                float amount = (frame - _left.Frame) / (_right.Frame - _left.Frame);
                MorphKeyFrame newKeyFrame = new MorphKeyFrame();
                newKeyFrame.Frame = (int)MathF.Round(frame);
                newKeyFrame.Weight = (1 - amount) * _left.Weight + amount * _right.Weight;
                return newKeyFrame;
            }
            var cacheIndex = keyframeSet.PrevIndex;
            if (cacheIndex < keyframeSet.Count - 1 && keyframeSet[cacheIndex].Frame <= frame && keyframeSet[cacheIndex + 1].Frame >= frame)
            {
                return ComputeKeyFrame(keyframeSet[cacheIndex], keyframeSet[cacheIndex + 1]);
            }
            else if (cacheIndex < keyframeSet.Count - 2 && keyframeSet[cacheIndex + 1].Frame <= frame && keyframeSet[cacheIndex + 2].Frame >= frame)
            {
                keyframeSet.PrevIndex++;
                return ComputeKeyFrame(keyframeSet[cacheIndex + 1], keyframeSet[cacheIndex + 2]);
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
            MorphKeyFrame keyFrameLeft = keyframeSet[left];
            MorphKeyFrame keyFrameRight = keyframeSet[right];
            keyframeSet.PrevIndex = left;
            return ComputeKeyFrame(keyFrameLeft, keyFrameRight);
        }

        public float GetMorphWeight(string key, float time)
        {
            if (!MorphKeyFrameSet.TryGetValue(key, out var keyframeSet))
            {
                return 0.0f;
            }
            float frame = Math.Max(time * c_framePerSecond, 0);
            float ComputeKeyFrame(MorphKeyFrame _left, MorphKeyFrame _right)
            {
                float amount = (frame - _left.Frame) / (_right.Frame - _left.Frame);
                return (1 - amount) * _left.Weight + amount * _right.Weight;
            }
            var cacheIndex = keyframeSet.PrevIndex;
            if (cacheIndex < keyframeSet.Count - 1 && keyframeSet[cacheIndex].Frame <= frame && keyframeSet[cacheIndex + 1].Frame >= frame)
            {
                return ComputeKeyFrame(keyframeSet[cacheIndex], keyframeSet[cacheIndex + 1]);
            }
            else if (cacheIndex < keyframeSet.Count - 2 && keyframeSet[cacheIndex + 1].Frame <= frame && keyframeSet[cacheIndex + 2].Frame >= frame)
            {
                keyframeSet.PrevIndex++;
                return ComputeKeyFrame(keyframeSet[cacheIndex + 1], keyframeSet[cacheIndex + 2]);
            }
            int left = 0;
            int right = keyframeSet.Count - 1;
            if (left == right) return keyframeSet[left].Weight;
            if (keyframeSet[right].Frame < frame) return keyframeSet[right].Weight;

            while (right - left > 1)
            {
                int mid = (right + left) / 2;
                if (keyframeSet[mid].Frame > frame)
                    right = mid;
                else
                    left = mid;
            }
            MorphKeyFrame keyFrameLeft = keyframeSet[left];
            MorphKeyFrame keyFrameRight = keyframeSet[right];
            keyframeSet.PrevIndex = left;
            return ComputeKeyFrame(keyFrameLeft, keyFrameRight);

        }

        //在GetMorphWeight之后使用
        public void GetABWeight(string key, float time, out float AWeight, out float BWeight)
        {
            if (!MorphKeyFrameSet.TryGetValue(key, out var keyframeSet))
            {
                AWeight = 0.0f;
                BWeight = 0.0f;
                return;
            }
            int frameA = (int)MathF.Floor(Math.Max(time * c_framePerSecond, 0));
            int frameB = frameA + 1;
            var cacheIndex = keyframeSet.PrevIndex;
            float ComputeKeyFrame(MorphKeyFrame _left, MorphKeyFrame _right, int frame)
            {
                float amount = (float)(frame - _left.Frame) / (_right.Frame - _left.Frame);
                return (1 - amount) * _left.Weight + amount * _right.Weight;
            }
            float GetWeight(int frame)
            {
                if (cacheIndex < keyframeSet.Count - 1 && keyframeSet[cacheIndex].Frame <= frame && keyframeSet[cacheIndex + 1].Frame >= frame)
                {
                    return ComputeKeyFrame(keyframeSet[cacheIndex], keyframeSet[cacheIndex + 1], frame);
                }
                else if (cacheIndex < keyframeSet.Count - 2 && keyframeSet[cacheIndex + 1].Frame <= frame && keyframeSet[cacheIndex + 2].Frame >= frame)
                {
                    return ComputeKeyFrame(keyframeSet[cacheIndex + 1], keyframeSet[cacheIndex + 2], frame);
                }
                else if (keyframeSet[keyframeSet.Count - 1].Frame <= frame)
                {
                    return keyframeSet[keyframeSet.Count - 1].Weight;
                }
                else throw new NotImplementedException("ABWeight Error");
            }
            AWeight = GetWeight(frameA);
            BWeight = GetWeight(frameB);
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
        /// <summary>
        /// 线程安全
        /// </summary>
        public static void Reload(this MMDMotionComponent motionComponent, VMDFormat vmd)
        {
            lock (motionComponent)
            {
                motionComponent.BoneKeyFrameSet.Clear();
                motionComponent.MorphKeyFrameSet.Clear();

                foreach (var pair in vmd.BoneKeyFrameSet)
                {
                    motionComponent.BoneKeyFrameSet.Add(pair.Key, new MMDMotionComponent.DataSetStruct<BoneKeyFrame>(pair.Value));
                }
                foreach (var pair in vmd.MorphKeyFrameSet)
                {
                    motionComponent.MorphKeyFrameSet.Add(pair.Key, new MMDMotionComponent.DataSetStruct<MorphKeyFrame>(pair.Value));
                }
            }
        }
    }
}