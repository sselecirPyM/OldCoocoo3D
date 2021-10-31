using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3DGraphics
{
    public class Texture2D : IDisposable
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

        public int GetWidth() => width;
        public int GetHeight() => height;

        public void ReloadAsDepthStencil(int width, int height, Format format)
        {
            this.width = width;
            this.height = height;
            if (format == Format.D24_UNorm_S8_UInt)
                this.format = Format.R24_UNorm_X8_Typeless;
            else if (format == Format.D32_Float)
                this.format = Format.R32_Float;
            this.dsvFormat = format;
            this.rtvFormat = Format.Unknown;
            this.mipLevels = 1;
        }

        public void ReloadAsRenderTarget(int width, int height, Format format)
        {
            this.width = width;
            this.height = height;
            this.format = format;
            this.dsvFormat = Format.Unknown;
            this.rtvFormat = format;
            this.uavFormat = Format.Unknown;
            this.mipLevels = 1;
        }

        public void ReloadAsRTVUAV(int width, int height, Format format)
        {
            this.width = width;
            this.height = height;
            this.format = format;
            this.dsvFormat = Format.Unknown;
            this.rtvFormat = format;
            this.uavFormat = format;
            this.mipLevels = 1;
        }

        public void Reload(Texture2D texture)
        {
            this.width = texture.width;
            this.height = texture.height;
            this.resource = texture.resource;
            this.mipLevels = texture.mipLevels;
            this.Status = texture.Status;
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

        public CpuDescriptorHandle GetRenderTargetView(ID3D12Device device)
        {
            if (renderTargetView == null)
            {
                device.CreateDescriptorHeap(new DescriptorHeapDescription(DescriptorHeapType.RenderTargetView, 1), out renderTargetView);
                device.CreateRenderTargetView(resource, null, renderTargetView.GetCPUDescriptorHandleForHeapStart());
            }
            return renderTargetView.GetCPUDescriptorHandleForHeapStart();
        }

        public CpuDescriptorHandle GetDepthStencilView(ID3D12Device device)
        {
            if (depthStencilView == null)
            {
                device.CreateDescriptorHeap(new DescriptorHeapDescription(DescriptorHeapType.DepthStencilView, 1), out depthStencilView);
                device.CreateDepthStencilView(resource, null, depthStencilView.GetCPUDescriptorHandleForHeapStart());
            }
            return depthStencilView.GetCPUDescriptorHandleForHeapStart();
        }

        public Format GetFormat()
        {
            if (dsvFormat != Format.Unknown)
                return dsvFormat;
            return format;
        }

        public void Dispose()
        {
            resource?.Dispose();
            resource = null;
            renderTargetView?.Dispose();
            renderTargetView = null;
            depthStencilView?.Dispose();
            depthStencilView = null;
        }
    }
}
