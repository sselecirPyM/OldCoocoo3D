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

        public void SetDescriptorHeapDefault()
        {
            m_commandList.SetDescriptorHeaps(1, new ID3D12DescriptorHeap[] { graphicsDevice.cbvsrvuavHeap.heap });
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

        unsafe void _UpdateVerticesPos<T>(ID3D12GraphicsCommandList commandList, ID3D12Resource resource, ID3D12Resource uploaderResource, T[] dataBegin, int dataLength, int offset) where T : unmanaged
        {
            Range readRange = new Range(0, 0);
            Range writeRange = new Range(offset, offset + dataLength);
            void* pMapped = null;
            IntPtr ptr1 = uploaderResource.Map(0, readRange);
            pMapped = ptr1.ToPointer();
            memcpy((byte*)pMapped + offset, dataBegin, dataLength);
            uploaderResource.Unmap(0, writeRange);
            commandList.ResourceBarrierTransition(resource, ResourceStates.GenericRead, ResourceStates.CopyDestination);
            commandList.CopyBufferRegion(resource, 0, uploaderResource, (ulong)offset, (ulong)dataLength);
            commandList.ResourceBarrierTransition(resource, ResourceStates.CopyDestination, ResourceStates.GenericRead);
        }

        public void UpdateVerticesPos(MMDMeshAppend mesh, Vector3[] verticeData)
        {
            mesh.lastUpdateIndexs++;
            mesh.lastUpdateIndexs = (mesh.lastUpdateIndexs < GraphicsDevice.c_frameCount) ? mesh.lastUpdateIndexs : 0;
            _UpdateVerticesPos(m_commandList, mesh.vertexBufferPos, mesh.vertexBufferPosUpload,
                verticeData, verticeData.Length * Marshal.SizeOf(typeof(Vector3)), mesh.lastUpdateIndexs * mesh.bufferSize);
        }

        public void SetSRVTSlot(Texture2D texture, int slot)
        {
            texture.StateChange(m_commandList, ResourceStates.GenericRead);
            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuDescriptorHandle, out var gpuDescriptorHandle);
            graphicsDevice.device.CreateShaderResourceView(texture.resource, null, cpuDescriptorHandle);
            m_commandList.SetGraphicsRootDescriptorTable(currentRootSignature.srv[slot], gpuDescriptorHandle);
        }
        public void SetSRVTSlot(TextureCube texture, int slot)
        {
            int index = currentRootSignature.srv[slot];
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription();
            srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
            srvDesc.Format = texture.format;
            srvDesc.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.TextureCube;
            srvDesc.TextureCube.MipLevels = texture.mipLevels;

            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuDescriptorHandle, out var gpuDescriptorHandle);
            graphicsDevice.device.CreateShaderResourceView(texture.resource, srvDesc, cpuDescriptorHandle);
            m_commandList.SetGraphicsRootDescriptorTable(currentRootSignature.srv[slot], gpuDescriptorHandle);
        }

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
            buffer.lastUpdateIndex = (buffer.lastUpdateIndex < (GraphicsDevice.c_frameCount - 1)) ? (buffer.lastUpdateIndex + 1) : 0;
            int lastUpdateIndex = buffer.lastUpdateIndex;

            int size1 = Marshal.SizeOf(typeof(T));
            var range = new Span<T>((buffer.mappedResource + buffer.size * lastUpdateIndex).ToPointer(), data.Length);
            data.CopyTo(range);
            buffer.resourceUpload.Unmap(0, null);
        }

        unsafe public void UploadMesh(MMDMesh mesh)
        {
            var d3dDevice = graphicsDevice.device;

            ID3D12Resource bufferUpload;
            ThrowIfFailed(d3dDevice.CreateCommittedResource(
                new HeapProperties(HeapType.Upload),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)(mesh.m_verticeData.Length + mesh.m_indexCount * 4)),
                ResourceStates.GenericRead,
                null,
                out bufferUpload));
            bufferUpload.Name = "uploadbuffer mesh";
            graphicsDevice.ResourceDelayRecycle(bufferUpload);
            int offset = 0;

            IntPtr ptr1 = bufferUpload.Map(0);
            void* mapped = ptr1.ToPointer();
            if (mesh.m_verticeData.Length > 0)
            {
                if (mesh.vertexBufferView.SizeInBytes != mesh.m_verticeData.Length)
                {
                    graphicsDevice.ResourceDelayRecycle(mesh.vertexBuffer);
                    ThrowIfFailed(d3dDevice.CreateCommittedResource(
                        new HeapProperties(HeapType.Default),
                        HeapFlags.None,
                        ResourceDescription.Buffer((ulong)mesh.m_verticeData.Length),
                        ResourceStates.CopyDestination,
                        null,
                        out mesh.vertexBuffer));
                    mesh.vertexBuffer.Name = "vertex buffer";
                }

                memcpy((byte*)mapped + offset, mesh.m_verticeData, mesh.m_verticeData.Length);
                m_commandList.CopyBufferRegion(mesh.vertexBuffer, 0, bufferUpload, (ulong)offset, (ulong)mesh.m_verticeData.Length);
                offset += mesh.m_verticeData.Length;

                m_commandList.ResourceBarrierTransition(mesh.vertexBuffer, ResourceStates.CopyDestination, ResourceStates.GenericRead);
            }
            if (mesh.m_indexCount > 0)
            {
                if (mesh.indexBufferView.SizeInBytes != mesh.m_indexCount * 4)
                {
                    graphicsDevice.ResourceDelayRecycle(mesh.indexBuffer);
                    ThrowIfFailed(d3dDevice.CreateCommittedResource(
                        new HeapProperties(HeapType.Default),
                        HeapFlags.None,
                        ResourceDescription.Buffer((ulong)(mesh.m_indexCount * 4)),
                        ResourceStates.CopyDestination,
                        null,
                        out mesh.indexBuffer));
                    mesh.indexBuffer.Name = "index buffer";
                }

                memcpy((byte*)mapped + offset, mesh.m_indexData, mesh.m_indexData.Length);
                m_commandList.CopyBufferRegion(mesh.indexBuffer, 0, bufferUpload, (ulong)offset, (ulong)mesh.m_indexData.Length);
                offset += mesh.m_indexData.Length;

                m_commandList.ResourceBarrierTransition(mesh.indexBuffer, ResourceStates.CopyDestination, ResourceStates.IndexBuffer);
            }
            bufferUpload.Unmap(0, null);

            // 创建顶点/索引缓冲区视图。
            if (mesh.m_verticeData.Length > 0)
            {
                mesh.vertexBufferView.BufferLocation = mesh.vertexBuffer.GPUVirtualAddress;
                mesh.vertexBufferView.StrideInBytes = mesh.vertexStride;
                mesh.vertexBufferView.SizeInBytes = mesh.vertexStride * mesh.m_vertexCount;
            }
            if (mesh.m_indexCount > 0)
            {
                mesh.indexBufferView.BufferLocation = mesh.indexBuffer.GPUVirtualAddress;
                mesh.indexBufferView.SizeInBytes = mesh.m_indexCount * 4;
                mesh.indexBufferView.Format = Format.R32_UInt;
            }
            mesh.updated = true;
        }

        public void UploadMesh(MMDMeshAppend mesh, byte[] data)
        {
            var d3dDevice = graphicsDevice.device;
            graphicsDevice.ResourceDelayRecycle(mesh.vertexBufferPos);
            ThrowIfFailed(d3dDevice.CreateCommittedResource(
                new HeapProperties(HeapType.Default),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)mesh.bufferSize),
                ResourceStates.GenericRead,
                null,
                out mesh.vertexBufferPos));
            mesh.vertexBufferPos.Name = "meshappend";
            graphicsDevice.ResourceDelayRecycle(mesh.vertexBufferPosUpload);
            ThrowIfFailed(d3dDevice.CreateCommittedResource(
                new HeapProperties(HeapType.Upload),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)(mesh.bufferSize * GraphicsDevice.c_frameCount * 2)),
                ResourceStates.GenericRead,
                null,
                out mesh.vertexBufferPosUpload));
            mesh.vertexBufferPosUpload.Name = "uploadBuf meshappend";
            _UpdateVerticesPos(m_commandList, mesh.vertexBufferPos, mesh.vertexBufferPosUpload, data, data.Length, 0);
            mesh.vertexBufferPosViews.BufferLocation = mesh.vertexBufferPos.GPUVirtualAddress;
            mesh.vertexBufferPosViews.StrideInBytes = MMDMeshAppend.c_vertexStride;
            mesh.vertexBufferPosViews.SizeInBytes = MMDMeshAppend.c_vertexStride * mesh.posCount;
        }
        public void UpdateResource<T>(CBuffer buffer, T[] data, int sizeInByte, int dataOffset) where T : unmanaged
        {
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

            var d3dDevice = graphicsDevice.device;
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
            graphicsDevice.ResourceDelayRecycle(texture.resource);
            ThrowIfFailed(d3dDevice.CreateCommittedResource(
                new HeapProperties(HeapType.Default),
                HeapFlags.None,
                textureDesc,
                ResourceStates.CopyDestination,
                null,
                out texture.resource));
            texture.resource.Name = "texCube";
            ID3D12Resource uploadBuffer;
            ThrowIfFailed(d3dDevice.CreateCommittedResource(
                new HeapProperties(HeapType.Upload),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)uploader.m_data.Length),
                ResourceStates.GenericRead,
                null,
                out uploadBuffer));
            uploadBuffer.Name = "uploadbuffer texcube";
            graphicsDevice.ResourceDelayRecycle(uploadBuffer);

            SubresourceData[] subresources = new SubresourceData[textureDesc.MipLevels * 6];

            SubresourceData[] textureDatas = new SubresourceData[6];
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

            var d3dDevice = graphicsDevice.device;

            ResourceDescription textureDesc = new ResourceDescription();
            textureDesc.MipLevels = (ushort)uploader.m_mipLevels;
            textureDesc.Format = uploader.m_format;
            textureDesc.Width = (ulong)uploader.m_width;
            textureDesc.Height = uploader.m_height;
            textureDesc.Flags = ResourceFlags.None;
            textureDesc.DepthOrArraySize = 1;
            textureDesc.SampleDescription.Count = 1;
            textureDesc.SampleDescription.Quality = 0;
            textureDesc.Dimension = ResourceDimension.Texture2D;
            graphicsDevice.ResourceDelayRecycle(texture.resource);
            graphicsDevice.ResourceDelayRecycle(texture.depthStencilView);
            texture.depthStencilView = null;
            graphicsDevice.ResourceDelayRecycle(texture.renderTargetView);
            texture.renderTargetView = null;
            ThrowIfFailed(d3dDevice.CreateCommittedResource(
                new HeapProperties(HeapType.Default),
                HeapFlags.None,
                textureDesc,
                ResourceStates.CopyDestination,
                null,
                out texture.resource));
            texture.resource.Name = "tex2d";
            ID3D12Resource uploadBuffer;
            ThrowIfFailed(d3dDevice.CreateCommittedResource(
                new HeapProperties(HeapType.Upload),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)uploader.m_data.Length),
                 ResourceStates.GenericRead,
                null,
                out uploadBuffer));
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
            var d3dDevice = graphicsDevice.device;

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

            graphicsDevice.ResourceDelayRecycle(texture.resource);
            graphicsDevice.ResourceDelayRecycle(texture.depthStencilView);
            texture.depthStencilView = null;
            graphicsDevice.ResourceDelayRecycle(texture.renderTargetView);
            texture.renderTargetView = null;
            ClearValue clearValue = texture.dsvFormat != Format.Unknown
                ? new ClearValue(texture.dsvFormat, new DepthStencilValue(1.0f, 0))
                : new ClearValue(texture.format, new Vortice.Mathematics.Color4());
            ThrowIfFailed(d3dDevice.CreateCommittedResource(
                new HeapProperties(HeapType.Default),
                HeapFlags.None,
                textureDesc,
                ResourceStates.GenericRead,
                clearValue,
                out texture.resource));
            texture.resourceStates = ResourceStates.GenericRead;
            texture.resource.Name = "render tex2D";

            texture.Status = GraphicsObjectStatus.loaded;
        }

        public void UpdateRenderTexture(TextureCube texture)
        {
            var d3dDevice = graphicsDevice.device;

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
            graphicsDevice.ResourceDelayRecycle(texture.resource);
            ThrowIfFailed(d3dDevice.CreateCommittedResource(
                new HeapProperties(HeapType.Default),
                HeapFlags.None,
                textureDesc,
                ResourceStates.GenericRead,
                clearValue,
                out texture.resource));
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
                ThrowIfFailed(graphicsDevice.device.CreateCommittedResource<ID3D12Resource>(new HeapProperties(HeapType.Readback), HeapFlags.None,
                new ResourceDescription(ResourceDimension.Buffer, 0, (ulong)(texture.m_width * texture.m_height), 1, 1, 1, Vortice.DXGI.Format.Unknown, 0, 0, TextureLayout.Unknown, ResourceFlags.None),
                ResourceStates.CopyDestination, out texture.m_textureReadBack[i]));
                texture.m_textureReadBack[i].Name = "texture readback";
            }
        }

        public void SetMesh(MMDMesh mesh)
        {
            m_commandList.IASetPrimitiveTopology(mesh.primitiveTopology);
            m_commandList.IASetVertexBuffers(0, mesh.vertexBufferView);
            m_commandList.IASetIndexBuffer(mesh.indexBufferView);
        }

        public void SetMeshVertex(MMDMesh mesh)
        {
            m_commandList.IASetPrimitiveTopology(mesh.primitiveTopology);
            m_commandList.IASetVertexBuffers(0, mesh.vertexBufferView);
        }

        public void SetMeshVertex(MMDMeshAppend mesh)
        {
            m_commandList.IASetVertexBuffers(1, mesh.vertexBufferPosViews);
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

        public void SetComputeSRVT(Texture2D texture, int index)
        {
            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuHandle, out var gpuHandle);
            texture.StateChange(m_commandList, ResourceStates.GenericRead);
            graphicsDevice.device.CreateShaderResourceView(texture.resource, null, cpuHandle);

            m_commandList.SetComputeRootDescriptorTable(index, gpuHandle);
        }

        public void SetComputeSRVT(TextureCube texture, int index)
        {
            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuHandle, out var gpuHandle);
            texture.StateChange(m_commandList, ResourceStates.GenericRead);
            ShaderResourceViewDescription srvd = new ShaderResourceViewDescription();
            srvd.Format = texture.format;
            srvd.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.TextureCube;
            srvd.TextureCube.MipLevels = texture.mipLevels;
            srvd.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
            graphicsDevice.device.CreateShaderResourceView(texture.resource, srvd, cpuHandle);
            m_commandList.SetComputeRootDescriptorTable(index, gpuHandle);
        }

        public void SetComputeCBVR(CBuffer buffer, int index)
        {
            m_commandList.SetComputeRootConstantBufferView(index, buffer.GetCurrentVirtualAddress());
        }

        public void SetComputeCBVR(CBuffer buffer, int offset256, int size256, int index)
        {
            m_commandList.SetComputeRootConstantBufferView(index, buffer.GetCurrentVirtualAddress() + (ulong)(offset256 * 256));
        }

        public void SetComputeCBVRSlot(CBuffer buffer, int offset256, int size256, int slot)
        {
            int index = currentRootSignature.cbv[slot];
            SetComputeCBVR(buffer, offset256, size256, index);
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
            if (texture != null)
            {
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
            else
            {
                throw new NotImplementedException();
            }
        }
        public void SetComputeUAVTSlot(Texture2D texture2D, int slot)
        {
            SetComputeUAVT(texture2D, currentRootSignature.srv[slot]);
        }

        public void SetSOMesh(MeshBuffer mesh)
        {
            if (mesh != null)
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
            else
            {
                throw new NotImplementedException();
            }
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
            m_commandList.OMSetRenderTargets(null, dsv);
        }
        public void SetRTV(Texture2D RTV, Vector4 color, bool clear)
        {
            m_commandList.RSSetScissorRect(RTV.width, RTV.height);
            m_commandList.RSSetViewport(0, 0, RTV.width, RTV.height);
            RTV.StateChange(m_commandList, ResourceStates.RenderTarget);
            var rtv = RTV.GetRenderTargetView(graphicsDevice.device);
            if (clear)
                m_commandList.ClearRenderTargetView(rtv, new Vortice.Mathematics.Color4(color));
            m_commandList.OMSetRenderTargets(rtv);
        }
        public void SetRTV(Texture2D[] RTVs, Vector4 color, bool clear)
        {
            m_commandList.RSSetScissorRect(RTVs[0].width, RTVs[0].height);
            m_commandList.RSSetViewport(0, 0, RTVs[0].width, RTVs[0].height);
            CpuDescriptorHandle[] handles = new CpuDescriptorHandle[RTVs.Length];
            for (int i = 0; i < RTVs.Length; i++)
            {
                Texture2D tex = RTVs[i];
                tex.StateChange(m_commandList, ResourceStates.RenderTarget);
                handles[i] = RTVs[i].GetRenderTargetView(graphicsDevice.device);
                if (clear)
                    m_commandList.ClearRenderTargetView(handles[i], new Vortice.Mathematics.Color4(color));
            }

            m_commandList.OMSetRenderTargets(handles);
        }

        public void SetRTVDSV(Texture2D RTV, Texture2D DSV, Vector4 color, bool clearRTV, bool clearDSV)
        {
            m_commandList.RSSetScissorRect(RTV.width, RTV.height);
            m_commandList.RSSetViewport(0, 0, RTV.width, RTV.height);
            RTV.StateChange(m_commandList, ResourceStates.RenderTarget);
            DSV.StateChange(m_commandList, ResourceStates.DepthWrite);
            var rtv = RTV.GetRenderTargetView(graphicsDevice.device);
            var dsv = DSV.GetDepthStencilView(graphicsDevice.device);
            if (clearRTV)
                m_commandList.ClearRenderTargetView(rtv, new Vortice.Mathematics.Color4(color));
            if (clearDSV)
                m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
            m_commandList.OMSetRenderTargets(rtv, dsv);
        }

        public void SetRTVDSV(Texture2D[] RTVs, Texture2D DSV, Vector4 color, bool clearRTV, bool clearDSV)
        {
            m_commandList.RSSetScissorRect(RTVs[0].width, RTVs[0].height);
            m_commandList.RSSetViewport(0, 0, RTVs[0].width, RTVs[0].height);
            DSV.StateChange(m_commandList, ResourceStates.DepthWrite);
            var dsv = DSV.GetDepthStencilView(graphicsDevice.device);

            CpuDescriptorHandle[] handles = new CpuDescriptorHandle[RTVs.Length];
            for (int i = 0; i < RTVs.Length; i++)
            {
                Texture2D tex = RTVs[i];
                tex.StateChange(m_commandList, ResourceStates.RenderTarget);
                handles[i] = RTVs[i].GetRenderTargetView(graphicsDevice.device);
                if (clearRTV)
                    m_commandList.ClearRenderTargetView(handles[i], new Vortice.Mathematics.Color4(color));
            }

            if (clearDSV)
                m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
            m_commandList.OMSetRenderTargets(handles, dsv);
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

        public PSODesc psoDesc;
        public UnnamedInputLayout unnamedInputLayout;
        //public PipelineStateObject pipelineStateObject;
    }
}
