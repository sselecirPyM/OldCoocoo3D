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
        public List<SBuffer> constantBuffers = new List<SBuffer>();

        byte[] tempBuffer;
        int lastUpdateBufferIndex = 0;
        public void Reload(int slienceSize, int bufferSize)
        {
            this.slienceSize = slienceSize;
            this.bufferSize = bufferSize;
            sliencesPerBuffer = bufferSize / slienceSize;
            sizeD256 = slienceSize / 256;
            tempBuffer = new byte[bufferSize];
        }

        public void SetSlienceCount(DeviceResources deviceResources, int count)
        {
            int slience1 = (count + sliencesPerBuffer - 1) / sliencesPerBuffer;
            while (constantBuffers.Count < slience1)
            {
                SBuffer buffer1 = new SBuffer();
                buffer1.Reload(deviceResources, bufferSize);
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
                lastUpdateBufferIndex = slience1;
                Array.Copy(data, dataOffset, tempBuffer, slience2 * slienceSize, dataLength);
            }

        }

        public void UpdateSlienceComplete(GraphicsContext graphicsContext)
        {
            graphicsContext.UpdateResource(constantBuffers[lastUpdateBufferIndex], tempBuffer, (uint)bufferSize, 0);
            lastUpdateBufferIndex = 0;
        }

        public void SetCBVR(GraphicsContext graphicsContext, int slienceIndex, int slot)
        {
            int slience1 = slienceIndex / sliencesPerBuffer;
            int slience2 = slienceIndex % sliencesPerBuffer;
            graphicsContext.SetCBVR(constantBuffers[slience1], sizeD256 * slience2, sizeD256, slot);
        }
    }
}
