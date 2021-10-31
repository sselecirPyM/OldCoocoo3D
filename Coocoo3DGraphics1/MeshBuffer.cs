using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class MeshBuffer
    {
        public void StateChange(ID3D12GraphicsCommandList commandList, ResourceStates states)
        {
            if (states != resourceStates)
            {
                commandList.ResourceBarrierTransition(resource, resourceStates, states);
                resourceStates = states;
            }
            else if (states == ResourceStates.UnorderedAccess)
            {
                commandList.ResourceBarrierUnorderedAccessView(resource);
            }
        }
        public ID3D12Resource resource;
        public ResourceStates resourceStates;
        public int m_size;

        public const int c_vbvOffset = 64;
        public const int c_vbvStride = 64;
    }
}
