using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class RingBuffer
    {
        public void Init(GraphicsDevice device, int size)
        {
            this.size = (size + 255) & ~255;

            device.device.CreateCommittedResource<ID3D12Resource>(new HeapProperties(HeapType.Upload), HeapFlags.None, ResourceDescription.Buffer((ulong)size), ResourceStates.GenericRead, out resource);
            mapped = resource.Map(0);
        }

        public IntPtr Upload(ID3D12GraphicsCommandList commandList, int size, ID3D12Resource target)
        {
            if (currentPosition + size > this.size)
            {
                currentPosition = 0;
            }
            IntPtr result = mapped + currentPosition;
            commandList.CopyBufferRegion(target, 0, resource, (ulong)currentPosition, (ulong)size);
            currentPosition = ((currentPosition + size + 255) & ~255) % this.size;

            return result;
        }

        IntPtr mapped;
        int size;
        int currentPosition;
        ID3D12Resource resource;
    }
}
