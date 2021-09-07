using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline.Wrap
{
    public class SBufferGroup
    {
        public int slienceSize;
        public int bufferSize;
        public int sliencesPerBuffer;
        public int sizeD256;
        public int count = 0;
        public List<CBuffer> constantBuffers = new List<CBuffer>();
        public DeviceResources deviceResources;

        byte[] tempBuffer;
        int lastUpdateBufferIndex = 0;
        public void Reload(DeviceResources deviceResources, int slienceSize, int bufferSize)
        {
            this.slienceSize = slienceSize;
            this.bufferSize = bufferSize;
            this.deviceResources = deviceResources;
            sliencesPerBuffer = bufferSize / slienceSize;
            sizeD256 = slienceSize / 256;
            tempBuffer = new byte[bufferSize];
            SetSlienceCount(1);
        }

        public void SetSlienceCount(int count)
        {
            this.count = count;
            int slience1 = (count + sliencesPerBuffer - 1) / sliencesPerBuffer;
            while (constantBuffers.Count < slience1)
            {
                CBuffer buffer1 = new CBuffer();
                deviceResources.InitializeSBuffer(buffer1, bufferSize);
                constantBuffers.Add(buffer1);
            }
        }

        public void UpdateBuffer(GraphicsContext graphicsContext, byte[] data, int bufferIndex)
        {
            graphicsContext.UpdateResource(constantBuffers[bufferIndex], data, (uint)bufferSize, 0);
        }

        public void UpdateSlience(GraphicsContext graphicsContext, byte[] data, int dataOffset, int dataLength, int slienceIndex)
        {
            int slience1 = slienceIndex / sliencesPerBuffer;
            int slience2 = slienceIndex % sliencesPerBuffer;
            if (lastUpdateBufferIndex == slience1)
            {
                Array.Copy(data, dataOffset, tempBuffer, slience2 * slienceSize, dataLength);
            }
            else
            {
                graphicsContext.UpdateResource(constantBuffers[lastUpdateBufferIndex], tempBuffer, (uint)bufferSize, 0);
                SetSlienceCount(slience1 + 1);
                lastUpdateBufferIndex = slience1;
                Array.Copy(data, dataOffset, tempBuffer, slience2 * slienceSize, dataLength);
            }

        }

        public void UpdateSlienceComplete(GraphicsContext graphicsContext)
        {
            if (count == 0) return;
            graphicsContext.UpdateResource(constantBuffers[lastUpdateBufferIndex], tempBuffer, (uint)bufferSize, 0);
            lastUpdateBufferIndex = 0;
        }

        public void SetCBVRSlot(GraphicsContext graphicsContext, int slienceIndex, int slot)
        {
            int slience1 = slienceIndex / sliencesPerBuffer;
            int slience2 = slienceIndex % sliencesPerBuffer;
            graphicsContext.SetCBVRSlot(constantBuffers[slience1], sizeD256 * slience2, sizeD256, slot);
        }

        public void SetComputeCBVRSlot(GraphicsContext graphicsContext, int slienceIndex, int slot)
        {
            int slience1 = slienceIndex / sliencesPerBuffer;
            int slience2 = slienceIndex % sliencesPerBuffer;
            graphicsContext.SetComputeCBVRSlot(constantBuffers[slience1], sizeD256 * slience2, sizeD256, slot);
        }

        public void SetComputeCBVR(GraphicsContext graphicsContext, int slienceIndex, int index)
        {
            int slience1 = slienceIndex / sliencesPerBuffer;
            int slience2 = slienceIndex % sliencesPerBuffer;
            graphicsContext.SetComputeCBVR(constantBuffers[slience1], sizeD256 * slience2, sizeD256, index);
        }
    }
}
