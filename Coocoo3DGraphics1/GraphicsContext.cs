using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Vortice.Direct3D12;
using Vortice.Direct3D;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;
using System.Runtime.InteropServices;

namespace Coocoo3DGraphics
{
    public class GraphicsContext
    {
        const int D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING = 5768;

        GraphicsDevice graphicsDevice;
        ID3D12GraphicsCommandList4 m_commandList;
        public RootSignature currentRootSignature;

        public Dictionary<int, object> slots = new Dictionary<int, object>();

        public void Reload(GraphicsDevice device)
        {
            this.graphicsDevice = device;
        }

        public void SetPSO(ComputeShader computeShader)
        {
            ID3D12PipelineState pipelineState = null;

            if (!computeShader.computeShaders.TryGetValue(currentRootSignature.rootSignature, out pipelineState))
            {
                ComputePipelineStateDescription desc = new ComputePipelineStateDescription();
                desc.ComputeShader = computeShader.data;
                desc.RootSignature = currentRootSignature.rootSignature;
                ThrowIfFailed(graphicsDevice.device.CreateComputePipelineState(desc, out pipelineState));

                computeShader.computeShaders[currentRootSignature.rootSignature] = pipelineState;
            }

            m_commandList.SetPipelineState(pipelineState);
        }

        public void SetPSO(PSO pObject, int variantIndex)
        {
            m_commandList.SetPipelineState(pObject.m_pipelineStates[variantIndex]);
        }

        public void SetSRVTSlot(Texture2D texture, int slot) => m_commandList.SetGraphicsRootDescriptorTable(currentRootSignature.srv[slot], GetSRVHandle(texture));

        public void SetSRVTSlot(TextureCube texture, int slot) => m_commandList.SetGraphicsRootDescriptorTable(currentRootSignature.srv[slot], GetSRVHandle(texture));

        public void SetCBVRSlot(CBuffer buffer, int offset256, int size256, int slot)
        {
            int index = currentRootSignature.cbv[slot];
            SetCBVR(buffer, offset256, size256, index);
        }

        public unsafe void UpdateCBStaticResource<T>(CBuffer buffer, ID3D12GraphicsCommandList commandList, Span<T> data) where T : unmanaged
        {
            buffer.lastUpdateIndex = (buffer.lastUpdateIndex < (GraphicsDevice.c_frameCount - 1)) ? (buffer.lastUpdateIndex + 1) : 0;
            int lastUpdateIndex = buffer.lastUpdateIndex;

            Range readRange = new Range(0, 0);
            IntPtr ptr = buffer.resourceUpload.Map(0, readRange);
            int size1 = Marshal.SizeOf(typeof(T));
            var range = new Span<T>((ptr + buffer.size * lastUpdateIndex).ToPointer(), data.Length);
            data.CopyTo(range);
            buffer.resourceUpload.Unmap(0, null);
            commandList.ResourceBarrierTransition(buffer.resource, ResourceStates.GenericRead, ResourceStates.CopyDestination);
            commandList.CopyBufferRegion(buffer.resource, 0, buffer.resourceUpload, (ulong)(buffer.size * lastUpdateIndex), (ulong)(size1 * data.Length));
            commandList.ResourceBarrierTransition(buffer.resource, ResourceStates.CopyDestination, ResourceStates.GenericRead);
        }

        public unsafe void UpdateCBResource<T>(CBuffer buffer, ID3D12GraphicsCommandList commandList, Span<T> data) where T : unmanaged
        {
            buffer.lastUpdateIndex = (buffer.lastUpdateIndex + 1) % GraphicsDevice.c_frameCount;
            int lastUpdateIndex = buffer.lastUpdateIndex;

            int size1 = Marshal.SizeOf(typeof(T));
            var range = new Span<T>((buffer.mappedResource + buffer.size * lastUpdateIndex).ToPointer(), data.Length);
            data.CopyTo(range);
        }

        unsafe public void UploadMesh(MMDMesh mesh)
        {
            foreach (var vtBuf in mesh.vtBuffers)
            {
                int index1 = mesh.vtBuffersDisposed.FindIndex(u => u.actualLength >= vtBuf.data.Length && u.actualLength <= vtBuf.data.Length * 2 + 256);
                if (index1 != -1)
                {
                    vtBuf.vertex = mesh.vtBuffersDisposed[index1].vertex;
                    vtBuf.actualLength = mesh.vtBuffersDisposed[index1].actualLength;
                    m_commandList.ResourceBarrierTransition(vtBuf.vertex, ResourceStates.GenericRead, ResourceStates.CopyDestination);

                    mesh.vtBuffersDisposed.RemoveAt(index1);
                }
                else
                {
                    CreateBuffer(vtBuf.data.Length + 256, ref vtBuf.vertex);
                    vtBuf.actualLength = vtBuf.data.Length + 256;
                }

                vtBuf.vertex.Name = "vertex buffer" + vtBuf.slot;

                IntPtr ptr1 = graphicsDevice.superRingBuffer.Upload(m_commandList, vtBuf.data.Length, vtBuf.vertex);
                memcpy((byte*)ptr1.ToPointer(), vtBuf.data, vtBuf.data.Length);

                m_commandList.ResourceBarrierTransition(vtBuf.vertex, ResourceStates.CopyDestination, ResourceStates.GenericRead);
                vtBuf.vertexBufferView.BufferLocation = vtBuf.vertex.GPUVirtualAddress;
                vtBuf.vertexBufferView.StrideInBytes = vtBuf.data.Length / mesh.m_vertexCount;
                vtBuf.vertexBufferView.SizeInBytes = vtBuf.data.Length;
            }

            foreach (var vtBuf in mesh.vtBuffersDisposed)
                graphicsDevice.ResourceDelayRecycle(vtBuf.vertex);
            mesh.vtBuffersDisposed.Clear();

            if (mesh.m_indexCount > 0)
            {
                if (mesh.indexActualLength < mesh.m_indexCount * 4)
                {
                    CreateBuffer(mesh.m_indexCount * 4, ref mesh.indexBuffer);
                    mesh.indexActualLength = mesh.m_indexCount * 4;
                    mesh.indexBuffer.Name = "index buffer";
                }
                else
                {
                    m_commandList.ResourceBarrierTransition(mesh.indexBuffer, ResourceStates.GenericRead, ResourceStates.CopyDestination);
                }
                IntPtr ptr1 = graphicsDevice.superRingBuffer.Upload(m_commandList, mesh.m_indexData.Length, mesh.indexBuffer);
                memcpy((byte*)ptr1.ToPointer(), mesh.m_indexData, mesh.m_indexData.Length);


                m_commandList.ResourceBarrierTransition(mesh.indexBuffer, ResourceStates.CopyDestination, ResourceStates.GenericRead);
                mesh.indexBufferView.BufferLocation = mesh.indexBuffer.GPUVirtualAddress;
                mesh.indexBufferView.SizeInBytes = mesh.m_indexCount * 4;
                mesh.indexBufferView.Format = Format.R32_UInt;
            }
            mesh.updated = true;
        }

        public void BeginUpdateMesh(MMDMesh mesh)
        {

        }

        unsafe public void UpdateMesh<T>(MMDMesh mesh, Span<T> data, int slot) where T : unmanaged
        {
            int size1 = Marshal.SizeOf(typeof(T));
            int sizeInBytes = data.Length * size1;

            var vtBufIndex = mesh.vtBuffers.FindIndex(u => u.slot == slot);
            if (vtBufIndex == -1)
            {
                mesh.AddBuffer(slot, size1);
                vtBufIndex = mesh.vtBuffers.Count - 1;
            }
            var vtBuf = mesh.vtBuffers[vtBufIndex];
            int index1 = mesh.vtBuffersDisposed.FindIndex(u => u.actualLength == sizeInBytes);
            if (index1 != -1)
            {
                vtBuf.vertex = mesh.vtBuffersDisposed[index1].vertex;
                vtBuf.actualLength = mesh.vtBuffersDisposed[index1].actualLength;
                m_commandList.ResourceBarrierTransition(vtBuf.vertex, ResourceStates.GenericRead, ResourceStates.CopyDestination);

                mesh.vtBuffersDisposed.RemoveAt(index1);
            }
            else
            {
                CreateBuffer(sizeInBytes, ref vtBuf.vertex);
                vtBuf.actualLength = sizeInBytes;
            }

            vtBuf.vertex.Name = "vertex buffer" + vtBuf.slot;
;
            IntPtr ptr1 = graphicsDevice.superRingBuffer.Upload(m_commandList, sizeInBytes, vtBuf.vertex);
            memcpy((byte*)ptr1.ToPointer(), data, sizeInBytes);

            m_commandList.ResourceBarrierTransition(vtBuf.vertex, ResourceStates.CopyDestination, ResourceStates.GenericRead);
            vtBuf.vertexBufferView.BufferLocation = vtBuf.vertex.GPUVirtualAddress;
            vtBuf.vertexBufferView.StrideInBytes = sizeInBytes / mesh.m_vertexCount;
            vtBuf.vertexBufferView.SizeInBytes = sizeInBytes;


            mesh.updated = true;
        }

        public void EndUpdateMesh(MMDMesh mesh)
        {
            foreach (var vtBuf in mesh.vtBuffersDisposed)
                graphicsDevice.ResourceDelayRecycle(vtBuf.vertex);
            mesh.vtBuffersDisposed.Clear();
        }

        public void UpdateResource<T>(CBuffer buffer, T[] data, int sizeInByte, int dataOffset) where T : unmanaged
        {
            UpdateResource(buffer, new Span<T>(data, dataOffset, sizeInByte));
        }
        public void UpdateResource<T>(CBuffer buffer, Span<T> data) where T : unmanaged
        {
            if (buffer.Mutable)
                UpdateCBResource(buffer, m_commandList, data);
            else
                UpdateCBStaticResource(buffer, m_commandList, data);
        }

        public unsafe void UploadTexture(TextureCube texture, Uploader uploader)
        {
            texture.width = uploader.m_width;
            texture.height = uploader.m_height;
            texture.mipLevels = uploader.m_mipLevels;
            texture.format = uploader.m_format;

            ResourceDescription textureDesc = new ResourceDescription();
            textureDesc.MipLevels = (ushort)uploader.m_mipLevels;
            textureDesc.Format = uploader.m_format;
            textureDesc.Width = (ulong)uploader.m_width;
            textureDesc.Height = uploader.m_height;
            textureDesc.Flags = ResourceFlags.None;
            textureDesc.DepthOrArraySize = 6;
            textureDesc.SampleDescription.Count = 1;
            textureDesc.SampleDescription.Quality = 0;
            textureDesc.Dimension = ResourceDimension.Texture2D;

            int bitsPerPixel = (int)GraphicsDevice.BitsPerPixel(textureDesc.Format);
            CreateResource(textureDesc, null, ref texture.resource);

            texture.resource.Name = "texCube";
            ID3D12Resource uploadBuffer = null;
            CreateBuffer(uploader.m_data.Length, ref uploadBuffer, ResourceStates.GenericRead, HeapType.Upload);
            uploadBuffer.Name = "uploadbuffer texcube";
            graphicsDevice.ResourceDelayRecycle(uploadBuffer);

            SubresourceData[] subresources = new SubresourceData[textureDesc.MipLevels * 6];
            for (int i = 0; i < 6; i++)
            {
                int width = (int)textureDesc.Width;
                int height = textureDesc.Height;
                IntPtr pdata = Marshal.UnsafeAddrOfPinnedArrayElement(uploader.m_data, (uploader.m_data.Length / 6) * i);
                for (int j = 0; j < textureDesc.MipLevels; j++)
                {
                    SubresourceData subresourcedata = new SubresourceData();
                    subresourcedata.DataPointer = pdata;
                    subresourcedata.RowPitch = (IntPtr)(width * bitsPerPixel / 8);
                    subresourcedata.SlicePitch = (IntPtr)(width * height * bitsPerPixel / 8);
                    pdata += width * height * bitsPerPixel / 8;

                    subresources[i * textureDesc.MipLevels + j] = subresourcedata;
                    width /= 2;
                    height /= 2;
                }
            }

            UpdateSubresources(m_commandList, texture.resource, uploadBuffer, 0, 0, textureDesc.MipLevels * 6, subresources);

            m_commandList.ResourceBarrierTransition(texture.resource, ResourceStates.CopyDestination, ResourceStates.GenericRead);
            texture.resourceStates = ResourceStates.GenericRead;

            texture.Status = GraphicsObjectStatus.loaded;
        }

        public unsafe void UploadTexture(Texture2D texture, Uploader uploader)
        {
            texture.width = uploader.m_width;
            texture.height = uploader.m_height;
            texture.mipLevels = uploader.m_mipLevels;
            texture.format = uploader.m_format;

            ResourceDescription textureDesc = Texture2DDescription(texture);
            graphicsDevice.ResourceDelayRecycle(texture.depthStencilView);
            texture.depthStencilView = null;
            graphicsDevice.ResourceDelayRecycle(texture.renderTargetView);
            texture.renderTargetView = null;

            CreateResource(textureDesc, null, ref texture.resource);

            texture.resource.Name = "tex2d";
            ID3D12Resource uploadBuffer = null;
            CreateBuffer(uploader.m_data.Length, ref uploadBuffer, ResourceStates.GenericRead, HeapType.Upload);
            uploadBuffer.Name = "uploadbuffer tex";
            graphicsDevice.ResourceDelayRecycle(uploadBuffer);

            SubresourceData[] subresources = new SubresourceData[textureDesc.MipLevels];

            IntPtr pdata = Marshal.UnsafeAddrOfPinnedArrayElement(uploader.m_data, 0);
            int bitsPerPixel = (int)GraphicsDevice.BitsPerPixel(textureDesc.Format);
            int width = (int)textureDesc.Width;
            int height = textureDesc.Height;
            for (int i = 0; i < textureDesc.MipLevels; i++)
            {
                SubresourceData subresourcedata = new SubresourceData();
                subresourcedata.DataPointer = pdata;
                subresourcedata.RowPitch = (IntPtr)(width * bitsPerPixel / 8);
                subresourcedata.SlicePitch = (IntPtr)(width * height * bitsPerPixel / 8);
                pdata += width * height * bitsPerPixel / 8;

                subresources[i] = subresourcedata;
                width /= 2;
                height /= 2;
            }

            UpdateSubresources(m_commandList, texture.resource, uploadBuffer, 0, 0, textureDesc.MipLevels, subresources);

            m_commandList.ResourceBarrierTransition(texture.resource, ResourceStates.CopyDestination, ResourceStates.GenericRead);
            texture.resourceStates = ResourceStates.GenericRead;

            texture.Status = GraphicsObjectStatus.loaded;
        }

        public void UpdateRenderTexture(Texture2D texture)
        {
            var textureDesc = Texture2DDescription(texture);

            graphicsDevice.ResourceDelayRecycle(texture.depthStencilView);
            texture.depthStencilView = null;
            graphicsDevice.ResourceDelayRecycle(texture.renderTargetView);
            texture.renderTargetView = null;
            ClearValue clearValue = texture.dsvFormat != Format.Unknown
                ? new ClearValue(texture.dsvFormat, new DepthStencilValue(1.0f, 0))
                : new ClearValue(texture.format, new Vortice.Mathematics.Color4());
            CreateResource(textureDesc, clearValue, ref texture.resource, ResourceStates.GenericRead);
            texture.resourceStates = ResourceStates.GenericRead;
            texture.resource.Name = "render tex2D";

            texture.Status = GraphicsObjectStatus.loaded;
        }

        ResourceDescription Texture2DDescription(Texture2D texture)
        {
            ResourceDescription textureDesc = new ResourceDescription();
            textureDesc.MipLevels = (ushort)texture.mipLevels;
            if (texture.dsvFormat != Format.Unknown)
                textureDesc.Format = texture.dsvFormat;
            else
                textureDesc.Format = texture.format;
            textureDesc.Width = (ulong)texture.width;
            textureDesc.Height = texture.height;
            textureDesc.Flags = ResourceFlags.None;

            if (texture.dsvFormat != Format.Unknown)
                textureDesc.Flags |= ResourceFlags.AllowDepthStencil;
            if (texture.rtvFormat != Format.Unknown)
                textureDesc.Flags |= ResourceFlags.AllowRenderTarget;
            if (texture.uavFormat != Format.Unknown)
                textureDesc.Flags |= ResourceFlags.AllowUnorderedAccess;

            textureDesc.DepthOrArraySize = 1;
            textureDesc.SampleDescription.Count = 1;
            textureDesc.SampleDescription.Quality = 0;
            textureDesc.Dimension = ResourceDimension.Texture2D;
            return textureDesc;
        }

        public void UpdateRenderTexture(TextureCube texture)
        {
            ResourceDescription textureDesc = new ResourceDescription();
            textureDesc.MipLevels = (ushort)texture.mipLevels;
            if (texture.dsvFormat != Format.Unknown)
                textureDesc.Format = texture.dsvFormat;
            else
                textureDesc.Format = texture.format;
            textureDesc.Width = (ulong)texture.width;
            textureDesc.Height = texture.height;
            textureDesc.Flags = ResourceFlags.None;
            if (texture.dsvFormat != Format.Unknown)
                textureDesc.Flags |= ResourceFlags.AllowDepthStencil;
            if (texture.rtvFormat != Format.Unknown)
                textureDesc.Flags |= ResourceFlags.AllowRenderTarget;
            if (texture.uavFormat != Format.Unknown)
                textureDesc.Flags |= ResourceFlags.AllowUnorderedAccess;
            textureDesc.DepthOrArraySize = 6;
            textureDesc.SampleDescription.Count = 1;
            textureDesc.SampleDescription.Quality = 0;
            textureDesc.Dimension = ResourceDimension.Texture2D;

            ClearValue clearValue = texture.dsvFormat != Format.Unknown
                ? new ClearValue(texture.dsvFormat, new DepthStencilValue(1.0f, 0))
                : new ClearValue(texture.format, new Vortice.Mathematics.Color4());
            CreateResource(textureDesc, clearValue, ref texture.resource, ResourceStates.GenericRead);
            texture.resourceStates = ResourceStates.GenericRead;
            texture.resource.Name = "render texCube";

            texture.Status = GraphicsObjectStatus.loaded;
        }

        public void UpdateReadBackTexture(ReadBackTexture2D texture)
        {
            if (texture.m_textureReadBack != null)
                foreach (var tex in texture.m_textureReadBack)
                    graphicsDevice.ResourceDelayRecycle(tex);
            if (texture.m_textureReadBack == null)
                texture.m_textureReadBack = new ID3D12Resource[3];
            for (int i = 0; i < texture.m_textureReadBack.Length; i++)
            {
                CreateBuffer(texture.m_width * texture.m_height * texture.bytesPerPixel, ref texture.m_textureReadBack[i], heapType: HeapType.Readback);
                texture.m_textureReadBack[i].Name = "texture readback";
            }
        }

        public void SetMesh(MMDMesh mesh)
        {
            m_commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            foreach (var vtBuf in mesh.vtBuffers)
                m_commandList.IASetVertexBuffers(vtBuf.slot, vtBuf.vertexBufferView);
            m_commandList.IASetIndexBuffer(mesh.indexBufferView);
        }

        public void SetMeshVertex(MMDMesh mesh)
        {
            m_commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            foreach (var vtBuf in mesh.vtBuffers)
                m_commandList.IASetVertexBuffers(vtBuf.slot, vtBuf.vertexBufferView);
        }

        public void SetMeshIndex(MMDMesh mesh)
        {
            m_commandList.IASetIndexBuffer(mesh.indexBufferView);
        }

        public void SetMesh(MeshBuffer mesh)
        {
            VertexBufferView vbv = new VertexBufferView();
            vbv.BufferLocation = mesh.resource.GPUVirtualAddress;
            vbv.StrideInBytes = MeshBuffer.c_vbvStride;
            vbv.SizeInBytes = MeshBuffer.c_vbvStride * mesh.m_size;
            mesh.StateChange(m_commandList, ResourceStates.GenericRead);
            m_commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            m_commandList.IASetVertexBuffers(0, vbv);
        }

        public void SetComputeSRVT(Texture2D texture, int index) => m_commandList.SetComputeRootDescriptorTable(index, GetSRVHandle(texture));

        public void SetComputeSRVT(TextureCube texture, int index) => m_commandList.SetComputeRootDescriptorTable(index, GetSRVHandle(texture));

        public void SetComputeCBVR(CBuffer buffer, int index) => m_commandList.SetComputeRootConstantBufferView(index, buffer.GetCurrentVirtualAddress());

        public void SetComputeCBVR(CBuffer buffer, int offset256, int size256, int index) => m_commandList.SetComputeRootConstantBufferView(index, buffer.GetCurrentVirtualAddress() + (ulong)(offset256 * 256));

        public void SetComputeCBVRSlot(CBuffer buffer, int offset256, int size256, int slot)
        {
            SetComputeCBVR(buffer, offset256, size256, currentRootSignature.cbv[slot]);
        }

        public void SetComputeUAVT(Texture2D texture2D, int index)
        {
            texture2D.StateChange(m_commandList, ResourceStates.GenericRead);
            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuDescriptorHandle, out var gpuDescriptorHandle);
            graphicsDevice.device.CreateShaderResourceView(texture2D.resource, null, cpuDescriptorHandle);
            m_commandList.SetComputeRootDescriptorTable(index, gpuDescriptorHandle);
        }
        public void SetComputeUAVT(TextureCube texture, int mipIndex, int index)
        {
            var d3dDevice = graphicsDevice.device;
            texture.StateChange(m_commandList, ResourceStates.UnorderedAccess);
            if (!(mipIndex < texture.mipLevels))
            {
                throw new ArgumentOutOfRangeException();
            }
            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuHandle, out var gpuHandle);
            d3dDevice.CreateUnorderedAccessView(texture.resource, null, new UnorderedAccessViewDescription()
            {
                ViewDimension = UnorderedAccessViewDimension.Texture2DArray,
                Texture2DArray = new Texture2DArrayUnorderedAccessView() { MipSlice = mipIndex, ArraySize = 6 },
                Format = texture.uavFormat,
            }, cpuHandle);
            m_commandList.SetComputeRootDescriptorTable(index, gpuHandle);
        }
        public void SetComputeUAVTSlot(Texture2D texture2D, int slot)
        {
            SetComputeUAVT(texture2D, currentRootSignature.srv[slot]);
        }

        public void SetSOMesh(MeshBuffer mesh)
        {
            mesh.StateChange(m_commandList, ResourceStates.CopyDestination);
            WriteBufferImmediateParameter[] parameter = { new WriteBufferImmediateParameter(mesh.resource.GPUVirtualAddress + (ulong)mesh.m_size * MeshBuffer.c_vbvStride, 0) };

            m_commandList.WriteBufferImmediate(1, parameter, new[] { WriteBufferImmediateMode.MarkerIn });

            mesh.StateChange(m_commandList, ResourceStates.StreamOut);

            StreamOutputBufferView temp = new StreamOutputBufferView();
            temp.BufferLocation = mesh.resource.GPUVirtualAddress;
            temp.BufferFilledSizeLocation = mesh.resource.GPUVirtualAddress + (ulong)(mesh.m_size * MeshBuffer.c_vbvStride);
            temp.SizeInBytes = (ulong)(mesh.m_size * MeshBuffer.c_vbvStride);

            m_commandList.SOSetTargets(0, 1, new[] { temp });
        }

        static readonly StreamOutputBufferView[] zeroStreamOutputBufferView = new StreamOutputBufferView[1];
        public void SetSOMeshNone()
        {
            m_commandList.SOSetTargets(0, 1, zeroStreamOutputBufferView);
        }

        public void CopyTexture(ReadBackTexture2D target, Texture2D texture2d, int index)
        {
            var d3dDevice = graphicsDevice.device;
            var backBuffer = texture2d.resource;
            texture2d.StateChange(m_commandList, ResourceStates.CopySource);

            PlacedSubresourceFootPrint footPrint = new PlacedSubresourceFootPrint();
            footPrint.Footprint.Width = target.m_width;
            footPrint.Footprint.Height = target.m_height;
            footPrint.Footprint.Depth = 1;
            footPrint.Footprint.RowPitch = (target.m_width * 4 + 255) & ~255;
            footPrint.Footprint.Format = texture2d.format;
            TextureCopyLocation Dst = new TextureCopyLocation(target.m_textureReadBack[index], footPrint);
            TextureCopyLocation Src = new TextureCopyLocation(backBuffer, 0);
            m_commandList.CopyTextureRegion(Dst, 0, 0, 0, Src, null);
        }

        public void RSSetScissorRect(int left, int top, int right, int bottom)
        {
            m_commandList.RSSetScissorRect(new Vortice.RawRect(left, top, right, bottom));
        }
        public void Begin()
        {
            m_commandList = graphicsDevice.GetCommandList();
            m_commandList.Reset(graphicsDevice.GetCommandAllocator());
            m_commandList.SetDescriptorHeaps(1, new ID3D12DescriptorHeap[] { graphicsDevice.cbvsrvuavHeap.heap });
        }

        //public void SetPipelineState(PipelineStateObject pipelineStateObject, PSODesc psoDesc)
        //{
        //    this.pipelineStateObject = pipelineStateObject;
        //    this.psoDesc = psoDesc;
        //}

        public void ClearScreen(Vector4 color)
        {
            var handle1 = graphicsDevice.rtvHeap.GetTempCpuHandle();
            graphicsDevice.device.CreateRenderTargetView(graphicsDevice.GetRenderTarget(), null, handle1);
            m_commandList.ClearRenderTargetView(handle1, new Vortice.Mathematics.Color(color));
        }

        public void SetDSV(Texture2D texture, bool clear)
        {
            m_commandList.RSSetScissorRect(texture.width, texture.height);
            m_commandList.RSSetViewport(0, 0, texture.width, texture.height);
            texture.StateChange(m_commandList, ResourceStates.DepthWrite);
            var dsv = texture.GetDepthStencilView(graphicsDevice.device);
            if (clear)
                m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
            m_commandList.OMSetRenderTargets(new CpuDescriptorHandle[0], dsv);
        }
        public void SetRTV(Texture2D RTV, Vector4 color, bool clear) => SetRTVDSV(RTV, null, color, clear, false);

        public void SetRTV(Texture2D[] RTVs, Vector4 color, bool clear) => SetRTVDSV(RTVs, null, color, clear, false);

        public void SetRTVDSV(Texture2D RTV, Texture2D DSV, Vector4 color, bool clearRTV, bool clearDSV)
        {
            m_commandList.RSSetScissorRect(RTV.width, RTV.height);
            m_commandList.RSSetViewport(0, 0, RTV.width, RTV.height);
            RTV.StateChange(m_commandList, ResourceStates.RenderTarget);
            var rtv = RTV.GetRenderTargetView(graphicsDevice.device);
            if (clearRTV)
                m_commandList.ClearRenderTargetView(rtv, new Vortice.Mathematics.Color4(color));
            if (DSV != null)
            {
                DSV.StateChange(m_commandList, ResourceStates.DepthWrite);
                var dsv = DSV.GetDepthStencilView(graphicsDevice.device);
                if (clearDSV)
                    m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
                m_commandList.OMSetRenderTargets(rtv, dsv);
            }
            else
            {
                m_commandList.OMSetRenderTargets(rtv);
            }
        }

        public void SetRTVDSV(Texture2D[] RTVs, Texture2D DSV, Vector4 color, bool clearRTV, bool clearDSV)
        {
            m_commandList.RSSetScissorRect(RTVs[0].width, RTVs[0].height);
            m_commandList.RSSetViewport(0, 0, RTVs[0].width, RTVs[0].height);

            CpuDescriptorHandle[] handles = new CpuDescriptorHandle[RTVs.Length];
            for (int i = 0; i < RTVs.Length; i++)
            {
                Texture2D tex = RTVs[i];
                tex.StateChange(m_commandList, ResourceStates.RenderTarget);
                handles[i] = RTVs[i].GetRenderTargetView(graphicsDevice.device);
                if (clearRTV)
                    m_commandList.ClearRenderTargetView(handles[i], new Vortice.Mathematics.Color4(color));
            }
            if (DSV != null)
            {
                DSV.StateChange(m_commandList, ResourceStates.DepthWrite);
                var dsv = DSV.GetDepthStencilView(graphicsDevice.device);
                if (clearDSV)
                    m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);

                m_commandList.OMSetRenderTargets(handles, dsv);
            }
            else
            {
                m_commandList.OMSetRenderTargets(handles);
            }
        }

        public void SetRootSignature(RootSignature rootSignature)
        {
            this.currentRootSignature = rootSignature;
            m_commandList.SetGraphicsRootSignature(rootSignature.rootSignature);
        }

        public void SetRootSignatureCompute(RootSignature rootSignature)
        {
            this.currentRootSignature = rootSignature;
            m_commandList.SetComputeRootSignature(rootSignature.rootSignature);
        }

        public void ResourceBarrierScreen(ResourceStates before, ResourceStates after)
        {
            m_commandList.ResourceBarrierTransition(graphicsDevice.GetRenderTarget(), before, after);
        }

        public void SetRenderTargetScreen(Vector4 color, bool clearScreen)
        {
            var size = graphicsDevice.m_d3dRenderTargetSize;

            m_commandList.RSSetScissorRect((int)size.X, (int)size.Y);
            m_commandList.RSSetViewport(0, 0, (int)size.X, (int)size.Y);
            if (clearScreen)
                m_commandList.ClearRenderTargetView(graphicsDevice.GetRenderTargetView(), new Vortice.Mathematics.Color4(color));
            m_commandList.OMSetRenderTargets(graphicsDevice.GetRenderTargetView());
        }

        public void Draw(int vertexCount, int startVertexLocation)
        {
            //m_commandList.SetPipelineState(pipelineStateObject.GetState(graphicsDevice, psoDesc, currentRootSignature, unnamedInputLayout));
            m_commandList.DrawInstanced(vertexCount, 1, startVertexLocation, 0);
        }

        public void DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
        {
            DrawIndexedInstanced(indexCount, 1, startIndexLocation, baseVertexLocation, 0);
        }

        public void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
        {
            //m_commandList.SetPipelineState(pipelineStateObject.GetState(graphicsDevice, psoDesc, currentRootSignature, unnamedInputLayout));
            m_commandList.DrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
        }

        public void Dispatch(int x, int y, int z)
        {
            m_commandList.Dispatch(x, y, z);
        }

        public void EndCommand()
        {
            m_commandList.Close();
        }

        public void Execute()
        {
            graphicsDevice.commandQueue.ExecuteCommandList(m_commandList);
            graphicsDevice.ReturnCommandList(m_commandList);
            m_commandList = null;
        }
        public static void BeginAlloctor(GraphicsDevice device)
        {
            device.GetCommandAllocator().Reset();
        }

        void SetCBVR(CBuffer buffer, int index)
        {
            m_commandList.SetGraphicsRootConstantBufferView(index, buffer.GetCurrentVirtualAddress());
        }

        void SetCBVR(CBuffer buffer, int offset256, int size256, int index)
        {
            m_commandList.SetGraphicsRootConstantBufferView(index, buffer.GetCurrentVirtualAddress() + (ulong)(offset256 * 256));
        }
        int l = 0;

        void CreateBuffer(int bufferLength, ref ID3D12Resource resource, ResourceStates resourceStates = ResourceStates.CopyDestination, HeapType heapType = HeapType.Default)
        {
            graphicsDevice.ResourceDelayRecycle(resource);
            ThrowIfFailed(graphicsDevice.device.CreateCommittedResource(
                new HeapProperties(heapType),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)bufferLength),
                resourceStates,
                null,
                out resource));
        }

        void CreateResource(ResourceDescription resourceDescription, ClearValue? clearValue, ref ID3D12Resource resource, ResourceStates resourceStates = ResourceStates.CopyDestination, HeapType heapType = HeapType.Default)
        {
            graphicsDevice.ResourceDelayRecycle(resource);
            ThrowIfFailed(graphicsDevice.device.CreateCommittedResource(
                new HeapProperties(heapType),
                HeapFlags.None,
                resourceDescription,
                resourceStates,
                clearValue,
                out resource));
        }

        GpuDescriptorHandle GetSRVHandle(Texture2D texture)
        {
            texture.StateChange(m_commandList, ResourceStates.GenericRead);
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription();
            srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
            srvDesc.Format = texture.format;
            srvDesc.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Texture2D;
            srvDesc.Texture2D.MipLevels = texture.mipLevels;

            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuDescriptorHandle, out var gpuDescriptorHandle);
            graphicsDevice.device.CreateShaderResourceView(texture.resource, srvDesc, cpuDescriptorHandle);
            return gpuDescriptorHandle;
        }

        GpuDescriptorHandle GetSRVHandle(TextureCube texture)
        {
            texture.StateChange(m_commandList, ResourceStates.GenericRead);
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription();
            srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
            srvDesc.Format = texture.format;
            srvDesc.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.TextureCube;
            srvDesc.TextureCube.MipLevels = texture.mipLevels;

            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuDescriptorHandle, out var gpuDescriptorHandle);
            graphicsDevice.device.CreateShaderResourceView(texture.resource, srvDesc, cpuDescriptorHandle);
            return gpuDescriptorHandle;
        }

        public PSODesc psoDesc;
        public UnnamedInputLayout unnamedInputLayout;
        //public PipelineStateObject pipelineStateObject;
    }
}
