using Coocoo3D.MMDSupport;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Present
{
    public class CameraMotion
    {
        const float c_framePerSecond = 30;
        public List<CameraKeyFrame> cameraKeyFrames;
        int cacheIndex;
        //这个函数缓存之前的结果，顺序访问能提高性能。
        public CameraKeyFrame GetCameraMotion(float time)
        {
            if (cameraKeyFrames == null)
            {
                return new CameraKeyFrame() { FOV = 30, distance = 45 };
            }
            if (cameraKeyFrames.Count == 0) return new CameraKeyFrame() { FOV = 30, distance = 45 };
            float frame = Math.Max(time * c_framePerSecond, 0);
            int _cacheIndex = cacheIndex;
            CameraKeyFrame ComputeKeyFrame(CameraKeyFrame _Left, CameraKeyFrame _Right)
            {
                float _getAmount(Interpolator interpolator, float _a)
                {
                    if (interpolator.ax == interpolator.ay && interpolator.bx == interpolator.by)
                        return _a;
                    var _curve = Utility.CubicBezierCurve.Load(interpolator.ax, interpolator.ay, interpolator.bx, interpolator.by);
                    return _curve.Sample(_a);
                }
                float t = (frame - _Left.Frame) / (_Right.Frame - _Left.Frame);
                float amountX = _getAmount(_Right.mxInterpolator, t);
                float amountY = _getAmount(_Right.myInterpolator, t);
                float amountZ = _getAmount(_Right.mzInterpolator, t);
                float amountR = _getAmount(_Right.rInterpolator, t);
                float amountD = _getAmount(_Right.dInterpolator, t);
                float amountF = _getAmount(_Right.fInterpolator, t);


                CameraKeyFrame newKeyFrame = new CameraKeyFrame();
                newKeyFrame.Frame = (int)MathF.Round(frame);
                newKeyFrame.position = new Vector3(amountX, amountY, amountZ) * _Right.position + new Vector3(1 - amountX, 1 - amountY, 1 - amountZ) * _Left.position;
                newKeyFrame.rotation = _Right.rotation * amountR + _Left.rotation * (1 - amountR);
                newKeyFrame.distance = _Right.distance * amountD + _Left.distance * (1 - amountD);
                newKeyFrame.FOV = _Right.FOV * amountF + _Left.FOV * (1 - amountF);
                if (newKeyFrame.FOV<0)
                {

                }

                return newKeyFrame;
            }
            if (_cacheIndex < cameraKeyFrames.Count - 1 && cameraKeyFrames[_cacheIndex].Frame < frame && cameraKeyFrames[_cacheIndex + 1].Frame > frame)
            {
                return ComputeKeyFrame(cameraKeyFrames[_cacheIndex], cameraKeyFrames[_cacheIndex + 1]);
            }
            else if (_cacheIndex < cameraKeyFrames.Count - 2 && cameraKeyFrames[_cacheIndex + 1].Frame < frame && cameraKeyFrames[_cacheIndex + 2].Frame > frame)
            {
                cacheIndex++;
                return ComputeKeyFrame(cameraKeyFrames[_cacheIndex + 1], cameraKeyFrames[_cacheIndex + 2]);
            }
            int left = 0;
            int right = cameraKeyFrames.Count - 1;
            if (left == right) return cameraKeyFrames[left];
            if (cameraKeyFrames[right].Frame < frame) return cameraKeyFrames[right];

            while (right - left > 1)
            {
                int mid = (right + left) / 2;
                if (cameraKeyFrames[mid].Frame > frame)
                    right = mid;
                else
                    left = mid;
            }
            cacheIndex = left;
            return ComputeKeyFrame(cameraKeyFrames[left], cameraKeyFrames[right]);
        }
    }
}
