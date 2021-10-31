using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3DGraphics
{
    public class TextureCube
    {
        public ID3D12Resource resource;
        public string Name;
        public ResourceStates resourceStates;
        public ID3D12DescriptorHeap renderTargetView;
        public ID3D12DescriptorHeap depthStencilView;
        public int width;
        public int height;
        public int mipLevels;
        public Format format;
        public Format rtvFormat;
        public Format dsvFormat;
        public Format uavFormat;
        public GraphicsObjectStatus Status;

        public void ReloadAsRTVUAV(int width, int height, int mipLevels, Format format)
        {
            this.width = width;
            this.height = height;
            this.mipLevels = mipLevels;
            this.format = format;
            this.dsvFormat = Format.Unknown;
            this.rtvFormat = format;
            this.uavFormat = format;
        }

        public void ReloadAsDSV(int width, int height, Format format)
        {
            this.width = width;
            this.height = height;
            this.mipLevels = 1;
            if (format == Format.D24_UNorm_S8_UInt)
                this.format = Format.R24_UNorm_X8_Typeless;
            else if (format == Format.D32_Float)
                this.format = Format.R32_Float;
            this.dsvFormat = format;
            this.rtvFormat = Format.Unknown;
            this.uavFormat = Format.Unknown;
        }

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
    }
}
