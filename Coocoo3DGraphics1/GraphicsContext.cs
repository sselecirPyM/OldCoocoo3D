﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Vortice.Direct3D12;
using Vortice.Direct3D;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;
using System.Runtime.InteropServices;
using System.IO;

namespace Coocoo3DGraphics
{
    public class GraphicsContext
    {
        const int D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING = 5768;

        GraphicsDevice graphicsDevice;
        ID3D12GraphicsCommandList4 m_commandList;
        public RootSignature currentRootSignature;
        RootSignature _currentGraphicsRootSignature;
        RootSignature _currentComputeRootSignature;

        public Dictionary<int, object> slots = new Dictionary<int, object>();

        public RTPSO currentRTPSO;
        public PSO currentPSO;

        public Dictionary<int, ulong> currentCBVs = new Dictionary<int, ulong>();
        public Dictionary<int, ulong> currentSRVs = new Dictionary<int, ulong>();
        public Dictionary<int, ulong> currentUAVs = new Dictionary<int, ulong>();

        public void Reload(GraphicsDevice device)
        {
            this.graphicsDevice = device;
        }

        public bool SetPSO(ComputeShader computeShader)
        {
            if (!computeShader.computeShaders.TryGetValue(currentRootSignature.rootSignature, out ID3D12PipelineState pipelineState))
            {
                ComputePipelineStateDescription desc = new ComputePipelineStateDescription();
                desc.ComputeShader = computeShader.data;
                desc.RootSignature = currentRootSignature.rootSignature;
                if (graphicsDevice.device.CreateComputePipelineState(desc, out pipelineState).Failure)
                {
                    return false;
                }

                computeShader.computeShaders[currentRootSignature.rootSignature] = pipelineState;
            }

            m_commandList.SetPipelineState(pipelineState);
            InReference(pipelineState);
            return true;
        }

        public bool SetPSO(PSO pso, in PSODesc desc)
        {
            int variantIndex = pso.GetVariantIndex(graphicsDevice, currentRootSignature, desc);
            if (variantIndex != -1)
            {
                m_commandList.SetPipelineState(pso.m_pipelineStates[variantIndex]);
                InReference(pso.m_pipelineStates[variantIndex]);
                currentPSO = pso;
                currentPSODesc = desc;
                return true;
            }
            return false;
        }

        public bool SetPSO(RTPSO pso)
        {
            if (!graphicsDevice.IsRayTracingSupport()) return false;
            if (pso == null) return false;

            var device = graphicsDevice.device;
            if (pso.so == null)
            {
                if (pso.exports == null || pso.exports.Length == 0) return false;

                pso.globalRootSignature?.Dispose();
                pso.globalRootSignature = new RootSignature();
                pso.globalRootSignature.ReloadCompute(pso.shaderAccessTypes);
                pso.globalRootSignature.Sign1(graphicsDevice);

                List<StateSubObject> stateSubObjects = new List<StateSubObject>();

                List<ExportDescription> exportDescriptions = new List<ExportDescription>();
                foreach (var export in pso.exports)
                    exportDescriptions.Add(new ExportDescription(export));

                stateSubObjects.Add(new StateSubObject(new DxilLibraryDescription(pso.datas, exportDescriptions.ToArray())));
                stateSubObjects.Add(new StateSubObject(new HitGroupDescription("emptyhitgroup", HitGroupType.Triangles, null, null, null)));
                foreach (var hitGroup in pso.hitGroups)
                {
                    stateSubObjects.Add(new StateSubObject(new HitGroupDescription(hitGroup.name, HitGroupType.Triangles, hitGroup.anyHit, hitGroup.closestHit, hitGroup.intersection)));
                }
                if (pso.localShaderAccessTypes != null)
                {
                    pso.localRootSignature?.Dispose();
                    pso.localRootSignature = new RootSignature();
                    pso.localRootSignature.ReloadLocalRootSignature(pso.localShaderAccessTypes);
                    pso.localRootSignature.Sign1(graphicsDevice, 1);
                    pso.localSize += pso.localShaderAccessTypes.Length * 8;
                    stateSubObjects.Add(new StateSubObject(new LocalRootSignature(pso.localRootSignature.rootSignature)));
                    string[] hitGroups = new string[pso.hitGroups.Length];
                    for (int i = 0; i < pso.hitGroups.Length; i++)
                        hitGroups[i] = pso.hitGroups[i].name;
                    stateSubObjects.Add(new StateSubObject(new SubObjectToExportsAssociation(stateSubObjects[stateSubObjects.Count - 1], hitGroups)));
                }

                stateSubObjects.Add(new StateSubObject(new RaytracingShaderConfig(64, 20)));
                stateSubObjects.Add(new StateSubObject(new SubObjectToExportsAssociation(stateSubObjects[stateSubObjects.Count - 1], pso.exports)));
                stateSubObjects.Add(new StateSubObject(new RaytracingPipelineConfig(2)));
                stateSubObjects.Add(new StateSubObject(new GlobalRootSignature(pso.globalRootSignature.rootSignature)));
                var result = device.CreateStateObject(new StateObjectDescription(StateObjectType.RaytracingPipeline, stateSubObjects.ToArray()), out pso.so);
                if (result.Failure)
                    return false;
            }
            SetRootSignature(pso.globalRootSignature);
            currentRTPSO = pso;
            m_commandList.SetPipelineState1(pso.so);
            return true;
        }

        void SetRTTopAccelerationStruct(RTTopLevelAcclerationStruct accelerationStruct)
        {
            if (!accelerationStruct.initialized)
            {
                accelerationStruct.initialized = true;
                if (graphicsDevice.scratchResource == null)
                {
                    CreateUAVBuffer(134217728, ref graphicsDevice.scratchResource, ResourceStates.UnorderedAccess);
                }
                int instanceCount = accelerationStruct.instances.Count;
                RaytracingInstanceDescription[] raytracingInstanceDescriptions = new RaytracingInstanceDescription[instanceCount];
                for (int i = 0; i < instanceCount; i++)
                {
                    RTInstance instance = accelerationStruct.instances[i];
                    var btas = instance.accelerationStruct;
                    var mesh = btas.mesh;
                    var meshOverride = btas.meshOverride;
                    if (!btas.initialized)
                    {
                        ulong pos;
                        if (meshOverride != null && meshOverride.vtBuffers.TryGetValue(0, out var v0))
                            pos = v0.vertex.GPUVirtualAddress;
                        else
                            pos = mesh.vtBuffers[0].vertex.GPUVirtualAddress;
                        BuildRaytracingAccelerationStructureInputs inputs = new BuildRaytracingAccelerationStructureInputs();
                        inputs.Type = RaytracingAccelerationStructureType.BottomLevel;
                        inputs.Layout = ElementsLayout.Array;
                        inputs.GeometryDescriptions = new RaytracingGeometryDescription[]
                        {
                            new RaytracingGeometryDescription(new RaytracingGeometryTrianglesDescription(new GpuVirtualAddressAndStride(pos, 12),
                            Format.R32G32B32_Float,
                            mesh.m_vertexCount,
                            0,
                            mesh.indexBuffer.GPUVirtualAddress + (ulong)btas.startIndex * 4,
                            Format.R32_UInt,
                            btas.indexCount)),
                        };
                        InReference(mesh.vtBuffers[0].vertex);
                        InReference(mesh.indexBuffer);
                        inputs.DescriptorsCount = 1;
                        RaytracingAccelerationStructurePrebuildInfo info = graphicsDevice.device.GetRaytracingAccelerationStructurePrebuildInfo(inputs);

                        CreateUAVBuffer((int)info.ResultDataMaxSizeInBytes, ref btas.resource, ResourceStates.RaytracingAccelerationStructure);
                        BuildRaytracingAccelerationStructureDescription brtas = new BuildRaytracingAccelerationStructureDescription();
                        brtas.Inputs = inputs;
                        brtas.ScratchAccelerationStructureData = graphicsDevice.scratchResource.GPUVirtualAddress;
                        brtas.DestinationAccelerationStructureData = btas.resource.GPUVirtualAddress;

                        m_commandList.BuildRaytracingAccelerationStructure(brtas);
                        m_commandList.ResourceBarrierUnorderedAccessView(btas.resource);
                        InReference(btas.resource);
                        RaytracingInstanceDescription raytracingInstanceDescription = new RaytracingInstanceDescription();
                        raytracingInstanceDescription.AccelerationStructure = (long)btas.resource.GPUVirtualAddress;
                        raytracingInstanceDescription.InstanceContributionToHitGroupIndex = (Vortice.UInt24)(uint)i;
                        raytracingInstanceDescription.InstanceID = (Vortice.UInt24)(uint)i;
                        raytracingInstanceDescription.InstanceMask = instance.instanceMask;
                        raytracingInstanceDescription.Transform = GetMatrix3X4(Matrix4x4.Transpose(instance.transform));
                        raytracingInstanceDescriptions[i] = raytracingInstanceDescription;
                        btas.initialized = true;
                    }
                }
                graphicsDevice.superRingBuffer.Upload(raytracingInstanceDescriptions, out ulong gpuAddr);
                BuildRaytracingAccelerationStructureInputs tpInputs = new BuildRaytracingAccelerationStructureInputs();
                tpInputs.Layout = ElementsLayout.Array;
                tpInputs.Type = RaytracingAccelerationStructureType.TopLevel;
                tpInputs.DescriptorsCount = accelerationStruct.instances.Count;
                tpInputs.InstanceDescriptions = (long)gpuAddr;

                RaytracingAccelerationStructurePrebuildInfo info1 = graphicsDevice.device.GetRaytracingAccelerationStructurePrebuildInfo(tpInputs);
                CreateUAVBuffer((int)info1.ResultDataMaxSizeInBytes, ref accelerationStruct.resource, ResourceStates.RaytracingAccelerationStructure);
                InReference(accelerationStruct.resource);
                BuildRaytracingAccelerationStructureDescription trtas = new BuildRaytracingAccelerationStructureDescription();
                trtas.Inputs = tpInputs;
                trtas.DestinationAccelerationStructureData = accelerationStruct.resource.GPUVirtualAddress;
                trtas.ScratchAccelerationStructureData = graphicsDevice.scratchResource.GPUVirtualAddress;
                m_commandList.BuildRaytracingAccelerationStructure(trtas);
            }
        }

        public unsafe void DispatchRays(int width, int height, int depth, RayTracingCall call)
        {
            SetRTTopAccelerationStruct(call.tpas);
            const int D3D12ShaderIdentifierSizeInBytes = 32;
            ID3D12StateObjectProperties pRtsoProps = currentRTPSO.so.QueryInterface<ID3D12StateObjectProperties>();
            InReference(currentRTPSO.so);
            DispatchRaysDescription dispatchRaysDescription = new DispatchRaysDescription();
            dispatchRaysDescription.Width = width;
            dispatchRaysDescription.Height = height;
            dispatchRaysDescription.Depth = depth;
            dispatchRaysDescription.HitGroupTable = new GpuVirtualAddressRangeAndStride();
            dispatchRaysDescription.MissShaderTable = new GpuVirtualAddressRangeAndStride();

            currentRootSignature = currentRTPSO.globalRootSignature;
            SetSRVRSlot(call.tpas.resource.GPUVirtualAddress, 0);
            byte[] data = new byte[32];
            MemoryStream memoryStream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(memoryStream);
            memcpy(data, pRtsoProps.GetShaderIdentifier(call.rayGenShader).ToPointer(), D3D12ShaderIdentifierSizeInBytes);
            writer.Write(data);

            {
                int cbvOffset = 0;
                int srvOffset = 0;
                int uavOffset = 0;
                foreach (var access in currentRTPSO.shaderAccessTypes)
                {
                    if (access == ResourceAccessType.SRV)
                    {
                        srvOffset++;
                    }
                    else if (access == ResourceAccessType.SRVTable)
                    {
                        if (call.SRVs != null && call.SRVs.TryGetValue(srvOffset, out object srv0))
                        {
                            if (srv0 is Texture2D tex2d)
                            {
                                if (!call.srvFlags.ContainsKey(srvOffset))
                                    SetSRVTSlot(tex2d, srvOffset);
                                else
                                    SetSRVTSlotLinear(tex2d, srvOffset);
                            }
                            else if (srv0 is TextureCube texCube)
                                SetSRVTSlot(texCube, srvOffset);
                            else if (srv0 is GPUBuffer buffer)
                                SetSRVTSlot(buffer, srvOffset);
                        }

                        srvOffset++;
                    }
                    else if (access == ResourceAccessType.CBV)
                    {
                        if (call.CBVs != null && call.CBVs.TryGetValue(cbvOffset, out object cbv0))
                        {
                            if (cbv0 is byte[] cbvData)
                                SetCBVRSlot<byte>(cbvData, cbvOffset);
                            else if (cbv0 is Matrix4x4[] cbvDataM)
                                SetCBVRSlot<Matrix4x4>(cbvDataM, cbvOffset);
                            else if (cbv0 is Vector4[] cbvDataF4)
                                SetCBVRSlot<Vector4>(cbvDataF4, cbvOffset);
                        }

                        cbvOffset++;
                    }
                    else if (access == ResourceAccessType.UAVTable)
                    {
                        if (call.UAVs != null && call.UAVs.TryGetValue(uavOffset, out object uav0))
                        {
                            if (uav0 is Texture2D tex2d)
                                SetUAVTSlot(tex2d, uavOffset);
                            else if (uav0 is GPUBuffer buffer)
                                SetUAVTSlot(buffer, uavOffset);
                        }
                        uavOffset++;
                    }
                }
            }

            ulong gpuaddr;
            int length1 = (int)memoryStream.Position;
            graphicsDevice.superRingBuffer.Upload(new Span<byte>(memoryStream.GetBuffer(), 0, length1), out gpuaddr);
            dispatchRaysDescription.RayGenerationShaderRecord = new GpuVirtualAddressRange(gpuaddr, (ulong)length1);
            writer.Seek(0, SeekOrigin.Begin);

            foreach (var inst in call.tpas.instances)
            {
                if (inst.hitGroupName != null)
                {
                    var mesh = inst.accelerationStruct.mesh;
                    var meshOverride = inst.accelerationStruct.meshOverride;
                    memcpy(data, pRtsoProps.GetShaderIdentifier(inst.hitGroupName).ToPointer(), D3D12ShaderIdentifierSizeInBytes);
                    writer.Write(data);
                    writer.Write(mesh.indexBuffer.GPUVirtualAddress + (ulong)inst.accelerationStruct.startIndex * 4);
                    for (int i = 0; i < 3; i++)
                    {
                        if (meshOverride != null && meshOverride.vtBuffers.TryGetValue(i, out var meshX1))
                        {
                            writer.Write(meshX1.vertex.GPUVirtualAddress);
                        }
                        else
                            writer.Write(mesh.vtBuffers[i].vertex.GPUVirtualAddress);
                    }
                    int cbvOffset = 0;
                    int srvOffset = 0;
                    foreach (var access in currentRTPSO.localShaderAccessTypes)
                    {
                        if (access == ResourceAccessType.CBV)
                        {
                            if (inst.CBVs != null && inst.CBVs.TryGetValue(cbvOffset, out object cbv0))
                            {
                                if (cbv0 is byte[] cbvData)
                                    _RTWriteGpuAddr<byte>(cbvData, writer);
                                else if (cbv0 is Matrix4x4[] cbvDataM)
                                    _RTWriteGpuAddr<Matrix4x4>(cbvDataM, writer);
                                else if (cbv0 is Vector4[] cbvDataF4)
                                    _RTWriteGpuAddr<Vector4>(cbvDataF4, writer);
                                else
                                    writer.Write((ulong)0);
                            }
                            else
                                writer.Write((ulong)0);
                            cbvOffset++;
                        }
                        else if (access == ResourceAccessType.SRV)
                        {
                            srvOffset++;
                        }
                        else if (access == ResourceAccessType.SRVTable)
                        {
                            if (inst.SRVs != null && inst.SRVs.TryGetValue(srvOffset, out object srv0))
                            {
                                if (srv0 is Texture2D tex2d)
                                    writer.Write(GetSRVHandle(tex2d).Ptr);
                                else if (srv0 is GPUBuffer buffer)
                                    writer.Write(GetSRVHandle(buffer).Ptr);
                                else if (srv0 is ID3D12Resource resource)
                                    writer.Write(InReferenceAddr(resource));
                                else
                                    writer.Write((ulong)0);
                            }
                            else
                                writer.Write((ulong)0);
                            srvOffset++;
                        }
                    }
                    var newPos = align_to(64, (int)memoryStream.Position) - (int)memoryStream.Position;
                    for (int k = 0; k < newPos; k++)
                    {
                        writer.Write((byte)0);
                    }
                }
                else
                {
                    memcpy(data, pRtsoProps.GetShaderIdentifier("emptyhitgroup").ToPointer(), D3D12ShaderIdentifierSizeInBytes);
                    writer.Write(data);
                    for (int i = 0; i < currentRTPSO.localSize - D3D12ShaderIdentifierSizeInBytes; i++)
                    {
                        writer.Write((byte)0);
                    }
                    var newPos = align_to(64, (int)memoryStream.Position) - (int)memoryStream.Position;
                    for (int k = 0; k < newPos; k++)
                    {
                        writer.Write((byte)0);
                    }
                }
            }
            if (memoryStream.Position > 0)
            {
                length1 = (int)memoryStream.Position;
                graphicsDevice.superRingBuffer.Upload(new Span<byte>(memoryStream.GetBuffer(), 0, length1), out gpuaddr);
                dispatchRaysDescription.HitGroupTable = new GpuVirtualAddressRangeAndStride(gpuaddr, (ulong)length1, (ulong)(length1 / call.tpas.instances.Count));
            }
            writer.Seek(0, SeekOrigin.Begin);

            if (call.missShaders != null && call.missShaders.Length > 0)
            {
                foreach (var missShader in call.missShaders)
                {
                    memcpy(data, pRtsoProps.GetShaderIdentifier(missShader).ToPointer(), D3D12ShaderIdentifierSizeInBytes);
                    writer.Write(data);
                }

                length1 = (int)memoryStream.Position;
                graphicsDevice.superRingBuffer.Upload(new Span<byte>(memoryStream.GetBuffer(), 0, length1), out gpuaddr);
                dispatchRaysDescription.MissShaderTable = new GpuVirtualAddressRangeAndStride(gpuaddr, (ulong)length1, (ulong)(length1 / call.missShaders.Length));
            }
            writer.Seek(0, SeekOrigin.Begin);

            pRtsoProps.Dispose();
            PipelineBindingCompute();
            m_commandList.DispatchRays(dispatchRaysDescription);
        }

        public void SetInputLayout(UnnamedInputLayout inputLayout)
        {
            currentInputLayout = inputLayout;
        }

        public void SetSRVTSlotLinear(Texture2D texture, int slot) => currentSRVs[slot] = GetSRVHandle(texture, true).Ptr;

        public void SetSRVTSlot(Texture2D texture, int slot) => currentSRVs[slot] = GetSRVHandle(texture).Ptr;

        public void SetSRVTSlot(TextureCube texture, int slot) => currentSRVs[slot] = GetSRVHandle(texture).Ptr;

        public void SetSRVTSlot(GPUBuffer buffer, int slot) => currentSRVs[slot] = GetSRVHandle(buffer).Ptr;

        public void SetSRVTLim(TextureCube texture, int mips, int slot) => currentSRVs[slot] = GetSRVHandleWithMip(texture, mips).Ptr;

        void SetSRVRSlot(ulong gpuAddr, int slot) => currentSRVs[slot] = gpuAddr;

        public void SetCBVRSlot(CBuffer buffer, int offset256, int size256, int slot) => currentCBVs[slot] = buffer.GetCurrentVirtualAddress() + (ulong)(offset256 * 256);

        public void SetCBVRSlot<T>(Span<T> data, int slot) where T : unmanaged
        {
            graphicsDevice.superRingBuffer.Upload(data, out ulong addr);
            currentCBVs[slot] = addr;
        }

        public void SetUAVTSlot(Texture2D texture2D, int slot) => currentUAVs[slot] = GetUAVHandle(texture2D).Ptr;
        public void SetUAVTSlot(TextureCube textureCube, int slot) => currentUAVs[slot] = GetUAVHandle(textureCube).Ptr;
        public void SetUAVTSlot(GPUBuffer buffer, int slot) => currentUAVs[slot] = GetUAVHandle(buffer).Ptr;

        public void SetUAVTSlot(TextureCube texture, int mipIndex, int slot)
        {
            var d3dDevice = graphicsDevice.device;
            //texture.StateChange(m_commandList, ResourceStates.UnorderedAccess);
            texture.SetPartResourceState(m_commandList, ResourceStates.UnorderedAccess, mipIndex, 1);
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
            currentUAVs[slot] = gpuHandle.Ptr;
            InReference(texture.resource);
        }

        public unsafe void UpdateCBStaticResource<T>(CBuffer buffer, ID3D12GraphicsCommandList commandList, Span<T> data) where T : unmanaged
        {
            commandList.ResourceBarrierTransition(buffer.resource, ResourceStates.GenericRead, ResourceStates.CopyDestination);

            graphicsDevice.superRingBuffer.Upload<T>(m_commandList, data, buffer.resource);

            commandList.ResourceBarrierTransition(buffer.resource, ResourceStates.CopyDestination, ResourceStates.GenericRead);
        }

        public void UpdateCBResource<T>(CBuffer buffer, ID3D12GraphicsCommandList commandList, Span<T> data) where T : unmanaged
        {
            graphicsDevice.superRingBuffer.Upload(data, out buffer.gpuRefAddress);
        }

        unsafe public void UploadMesh(Mesh mesh)
        {
            foreach (var vtBuf in mesh.vtBuffers)
            {
                int dataLength = vtBuf.Value.data.Length;
                int index1 = mesh.vtBuffersDisposed.FindIndex(u => u.actualLength >= dataLength && u.actualLength <= dataLength * 2 + 256);
                if (index1 != -1)
                {
                    vtBuf.Value.vertex = mesh.vtBuffersDisposed[index1].vertex;
                    vtBuf.Value.actualLength = mesh.vtBuffersDisposed[index1].actualLength;
                    m_commandList.ResourceBarrierTransition(vtBuf.Value.vertex, ResourceStates.GenericRead, ResourceStates.CopyDestination);

                    mesh.vtBuffersDisposed.RemoveAt(index1);
                }
                else
                {
                    CreateBuffer(dataLength + 256, ref vtBuf.Value.vertex);
                    vtBuf.Value.actualLength = dataLength + 256;
                }

                vtBuf.Value.vertex.Name = "vertex buffer" + vtBuf.Key;

                graphicsDevice.superRingBuffer.Upload<byte>(m_commandList, vtBuf.Value.data, vtBuf.Value.vertex);
                m_commandList.ResourceBarrierTransition(vtBuf.Value.vertex, ResourceStates.CopyDestination, ResourceStates.GenericRead);
                InReference(vtBuf.Value.vertex);

                vtBuf.Value.vertexBufferView.BufferLocation = vtBuf.Value.vertex.GPUVirtualAddress;
                vtBuf.Value.vertexBufferView.StrideInBytes = dataLength / mesh.m_vertexCount;
                vtBuf.Value.vertexBufferView.SizeInBytes = dataLength;
            }

            foreach (var vtBuf in mesh.vtBuffersDisposed)
                vtBuf.vertex.Release();
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
                graphicsDevice.superRingBuffer.Upload<byte>(m_commandList, mesh.m_indexData, mesh.indexBuffer);

                m_commandList.ResourceBarrierTransition(mesh.indexBuffer, ResourceStates.CopyDestination, ResourceStates.GenericRead);
                InReference(mesh.indexBuffer);
                mesh.indexBufferView.BufferLocation = mesh.indexBuffer.GPUVirtualAddress;
                mesh.indexBufferView.SizeInBytes = mesh.m_indexCount * 4;
                mesh.indexBufferView.Format = Format.R32_UInt;
            }
        }

        public void BeginUpdateMesh(Mesh mesh)
        {

        }

        unsafe public void UpdateMesh<T>(Mesh mesh, Span<T> data, int slot) where T : unmanaged
        {
            int size1 = Marshal.SizeOf(typeof(T));
            int sizeInBytes = data.Length * size1;

            if (!mesh.vtBuffers.TryGetValue(slot, out var vtBuf))
            {
                vtBuf = mesh.AddBuffer(slot);
            }


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

            vtBuf.vertex.Name = "vertex buffer" + slot;

            graphicsDevice.superRingBuffer.Upload(m_commandList, data, vtBuf.vertex);

            m_commandList.ResourceBarrierTransition(vtBuf.vertex, ResourceStates.CopyDestination, ResourceStates.GenericRead);
            InReference(vtBuf.vertex);
            vtBuf.vertexBufferView.BufferLocation = vtBuf.vertex.GPUVirtualAddress;
            vtBuf.vertexBufferView.StrideInBytes = sizeInBytes / mesh.m_vertexCount;
            vtBuf.vertexBufferView.SizeInBytes = sizeInBytes;
        }

        public void EndUpdateMesh(Mesh mesh)
        {
            foreach (var vtBuf in mesh.vtBuffersDisposed)
                vtBuf.vertex.Release();
            mesh.vtBuffersDisposed.Clear();
        }

        public void UpdateResource<T>(CBuffer buffer, T[] data, int sizeInByte, int dataOffset) where T : unmanaged
        {
            int size1 = Marshal.SizeOf(typeof(T));
            UpdateResource(buffer, new Span<T>(data, dataOffset, sizeInByte / size1));
        }
        public void UpdateResource<T>(CBuffer buffer, Span<T> data) where T : unmanaged
        {
            if (buffer.Mutable)
                UpdateCBResource(buffer, m_commandList, data);
            else
                UpdateCBStaticResource(buffer, m_commandList, data);
        }

        //public unsafe void UploadTexture(TextureCube texture, Uploader uploader)
        //{
        //    texture.width = uploader.m_width;
        //    texture.height = uploader.m_height;
        //    texture.mipLevels = uploader.m_mipLevels;
        //    texture.format = uploader.m_format;

        //    ResourceDescription textureDesc = new ResourceDescription();
        //    textureDesc.MipLevels = (ushort)uploader.m_mipLevels;
        //    textureDesc.Format = uploader.m_format;
        //    textureDesc.Width = (ulong)uploader.m_width;
        //    textureDesc.Height = uploader.m_height;
        //    textureDesc.Flags = ResourceFlags.None;
        //    textureDesc.DepthOrArraySize = 6;
        //    textureDesc.SampleDescription.Count = 1;
        //    textureDesc.SampleDescription.Quality = 0;
        //    textureDesc.Dimension = ResourceDimension.Texture2D;

        //    int bitsPerPixel = (int)GraphicsDevice.BitsPerPixel(textureDesc.Format);
        //    CreateResource(textureDesc, null, ref texture.resource);

        //    texture.resource.Name = "texCube";
        //    ID3D12Resource uploadBuffer = null;
        //    CreateBuffer(uploader.m_data.Length, ref uploadBuffer, ResourceStates.GenericRead, HeapType.Upload);
        //    uploadBuffer.Name = "uploadbuffer texcube";
        //    graphicsDevice.ResourceDelayRecycle(uploadBuffer);

        //    SubresourceData[] subresources = new SubresourceData[textureDesc.MipLevels * 6];
        //    for (int i = 0; i < 6; i++)
        //    {
        //        int width = (int)textureDesc.Width;
        //        int height = textureDesc.Height;
        //        IntPtr pdata = Marshal.UnsafeAddrOfPinnedArrayElement(uploader.m_data, (uploader.m_data.Length / 6) * i);
        //        for (int j = 0; j < textureDesc.MipLevels; j++)
        //        {
        //            SubresourceData subresourcedata = new SubresourceData();
        //            subresourcedata.DataPointer = pdata;
        //            subresourcedata.RowPitch = (IntPtr)(width * bitsPerPixel / 8);
        //            subresourcedata.SlicePitch = (IntPtr)(width * height * bitsPerPixel / 8);
        //            pdata += width * height * bitsPerPixel / 8;

        //            subresources[i * textureDesc.MipLevels + j] = subresourcedata;
        //            width /= 2;
        //            height /= 2;
        //        }
        //    }

        //    UpdateSubresources(m_commandList, texture.resource, uploadBuffer, 0, 0, textureDesc.MipLevels * 6, subresources);

        //    m_commandList.ResourceBarrierTransition(texture.resource, ResourceStates.CopyDestination, ResourceStates.GenericRead);
        //    texture.resourceStates = ResourceStates.GenericRead;

        //    texture.Status = GraphicsObjectStatus.loaded;
        //}

        public unsafe void UploadTexture(Texture2D texture, Uploader uploader)
        {
            texture.width = uploader.m_width;
            texture.height = uploader.m_height;
            texture.mipLevels = uploader.m_mipLevels;
            texture.format = uploader.m_format;

            var textureDesc = Texture2DDescription(texture);
            texture.depthStencilView?.Release();
            texture.depthStencilView = null;
            texture.renderTargetView?.Release();
            texture.renderTargetView = null;

            CreateResource(textureDesc, null, ref texture.resource);

            texture.resource.Name = texture.Name ?? "tex2d";
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
            InReference(texture.resource);
            texture.resourceStates = ResourceStates.GenericRead;

            texture.Status = GraphicsObjectStatus.loaded;
        }

        public void UpdateRenderTexture(Texture2D texture)
        {
            var textureDesc = Texture2DDescription(texture);

            texture.depthStencilView?.Release();
            texture.depthStencilView = null;
            texture.renderTargetView?.Release();
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
            //texture.resourceStates = ResourceStates.GenericRead;
            texture.InitResourceState(ResourceStates.GenericRead);
            texture.resource.Name = "render texCube";

            texture.Status = GraphicsObjectStatus.loaded;
        }

        public void UpdateDynamicBuffer(GPUBuffer buffer)
        {
            CreateUAVBuffer(buffer.size, ref buffer.resource);
            buffer.resource.Name = buffer.Name;
            buffer.resourceStates = ResourceStates.UnorderedAccess;
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

        public void SetMesh(Mesh mesh)
        {
            m_commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            foreach (var vtBuf in mesh.vtBuffers)
            {
                m_commandList.IASetVertexBuffers(vtBuf.Key, vtBuf.Value.vertexBufferView);
                InReference(vtBuf.Value.vertex);
            }
            m_commandList.IASetIndexBuffer(mesh.indexBufferView);
            InReference(mesh.indexBuffer);
        }

        public void SetMesh(Mesh mesh, Mesh meshOverride)
        {
            m_commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            foreach (var vtBuf in mesh.vtBuffers)
            {
                if (!meshOverride.vtBuffers.ContainsKey(vtBuf.Key))
                {
                    m_commandList.IASetVertexBuffers(vtBuf.Key, vtBuf.Value.vertexBufferView);
                    InReference(vtBuf.Value.vertex);
                }
            }
            foreach (var vtBuf in meshOverride.vtBuffers)
            {
                m_commandList.IASetVertexBuffers(vtBuf.Key, vtBuf.Value.vertexBufferView);
                InReference(vtBuf.Value.vertex);
            }
            m_commandList.IASetIndexBuffer(mesh.indexBufferView);
            InReference(mesh.indexBuffer);
        }

        //public void SetMeshVertex(Mesh mesh)
        //{
        //    m_commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        //    foreach (var vtBuf in mesh.vtBuffers)
        //    {
        //        m_commandList.IASetVertexBuffers(vtBuf.Key, vtBuf.Value.vertexBufferView);
        //        InReference(vtBuf.Value.vertex);
        //    }
        //}

        //public void SetMeshIndex(Mesh mesh)
        //{
        //    m_commandList.IASetIndexBuffer(mesh.indexBufferView);
        //    InReference(mesh.indexBuffer);
        //}

        public void CopyTexture(ReadBackTexture2D target, Texture2D texture2D, int index)
        {
            var backBuffer = texture2D.resource;
            texture2D.StateChange(m_commandList, ResourceStates.CopySource);

            PlacedSubresourceFootPrint footPrint = new PlacedSubresourceFootPrint();
            footPrint.Footprint.Width = target.m_width;
            footPrint.Footprint.Height = target.m_height;
            footPrint.Footprint.Depth = 1;
            footPrint.Footprint.RowPitch = (target.m_width * 4 + 255) & ~255;
            footPrint.Footprint.Format = texture2D.format;
            TextureCopyLocation Dst = new TextureCopyLocation(target.m_textureReadBack[index], footPrint);
            TextureCopyLocation Src = new TextureCopyLocation(backBuffer, 0);
            m_commandList.CopyTextureRegion(Dst, 0, 0, 0, Src, null);
        }

        public void RSSetScissorRect(int left, int top, int right, int bottom)
        {
            m_commandList.RSSetScissorRect(new Vortice.RawRect(left, top, right, bottom));
        }
        public void RSSetScissorRectAndViewport(int left, int top, int right, int bottom)
        {
            m_commandList.RSSetScissorRect(new Vortice.RawRect(left, top, right, bottom));
            m_commandList.RSSetViewport(left, top, right - left, bottom - top);
        }

        public void Begin()
        {
            m_commandList = graphicsDevice.GetCommandList();
            m_commandList.Reset(graphicsDevice.GetCommandAllocator());
            m_commandList.SetDescriptorHeaps(1, new ID3D12DescriptorHeap[] { graphicsDevice.cbvsrvuavHeap.heap });
            ClearState();
        }

        public void ClearState()
        {
            currentPSO = null;
            currentRTPSO = null;
            _currentGraphicsRootSignature = null;
            _currentComputeRootSignature = null;
            currentRootSignature = null;
            currentInputLayout = null;
            currentCBVs.Clear();
            currentSRVs.Clear();
            currentUAVs.Clear();
        }

        public void ClearScreen(Vector4 color)
        {
            var handle1 = graphicsDevice.rtvHeap.GetTempCpuHandle();
            graphicsDevice.device.CreateRenderTargetView(graphicsDevice.GetRenderTarget(m_commandList), null, handle1);
            m_commandList.ClearRenderTargetView(handle1, new Vortice.Mathematics.Color(color));
        }
        public void SetRTV(Texture2D RTV, Vector4 color, bool clear) => SetRTVDSV(RTV, null, color, clear, false);

        public void SetRTV(IList<Texture2D> RTVs, Vector4 color, bool clear) => SetRTVDSV(RTVs, null, color, clear, false);

        public void SetDSV(Texture2D texture, bool clear)
        {
            m_commandList.RSSetScissorRect(texture.width, texture.height);
            m_commandList.RSSetViewport(0, 0, texture.width, texture.height);
            texture.StateChange(m_commandList, ResourceStates.DepthWrite);
            var dsv = texture.GetDepthStencilView(graphicsDevice.device);
            InReference(texture.depthStencilView);
            InReference(texture.resource);
            if (clear)
                m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
            m_commandList.OMSetRenderTargets(new CpuDescriptorHandle[0], dsv);
        }

        public void SetRTVDSV(Texture2D RTV, Texture2D DSV, Vector4 color, bool clearRTV, bool clearDSV)
        {
            m_commandList.RSSetScissorRect(RTV.width, RTV.height);
            m_commandList.RSSetViewport(0, 0, RTV.width, RTV.height);
            RTV.StateChange(m_commandList, ResourceStates.RenderTarget);
            var rtv = RTV.GetRenderTargetView(graphicsDevice.device);
            InReference(RTV.renderTargetView);
            InReference(RTV.resource);
            if (clearRTV)
                m_commandList.ClearRenderTargetView(rtv, new Vortice.Mathematics.Color4(color));
            if (DSV != null)
            {
                DSV.StateChange(m_commandList, ResourceStates.DepthWrite);
                var dsv = DSV.GetDepthStencilView(graphicsDevice.device);
                InReference(DSV.depthStencilView);
                InReference(DSV.resource);
                if (clearDSV)
                    m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
                m_commandList.OMSetRenderTargets(rtv, dsv);
            }
            else
            {
                m_commandList.OMSetRenderTargets(rtv);
            }
        }

        public void SetRTVDSV(TextureCube RTV, Texture2D DSV, Vector4 color, int faceIndex, bool clearRTV, bool clearDSV)
        {
            m_commandList.RSSetScissorRect(RTV.width, RTV.height);
            m_commandList.RSSetViewport(0, 0, RTV.width, RTV.height);
            RTV.SetResourceState(m_commandList, ResourceStates.RenderTarget, 0, faceIndex);
            var rtv = RTV.GetRenderTargetView(graphicsDevice.device, 0, faceIndex);
            InReference(RTV.renderTargetView);
            InReference(RTV.resource);
            if (clearRTV)
                m_commandList.ClearRenderTargetView(rtv, new Vortice.Mathematics.Color4(color));
            if (DSV != null)
            {
                DSV.StateChange(m_commandList, ResourceStates.DepthWrite);
                var dsv = DSV.GetDepthStencilView(graphicsDevice.device);
                InReference(DSV.depthStencilView);
                InReference(DSV.resource);
                if (clearDSV)
                    m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
                m_commandList.OMSetRenderTargets(rtv, dsv);
            }
            else
            {
                m_commandList.OMSetRenderTargets(rtv);
            }
        }

        public void SetRTVDSV(IList<Texture2D> RTVs, Texture2D DSV, Vector4 color, bool clearRTV, bool clearDSV)
        {
            m_commandList.RSSetScissorRect(RTVs[0].width, RTVs[0].height);
            m_commandList.RSSetViewport(0, 0, RTVs[0].width, RTVs[0].height);

            CpuDescriptorHandle[] handles = new CpuDescriptorHandle[RTVs.Count];
            for (int i = 0; i < RTVs.Count; i++)
            {
                RTVs[i].StateChange(m_commandList, ResourceStates.RenderTarget);
                handles[i] = RTVs[i].GetRenderTargetView(graphicsDevice.device);
                InReference(RTVs[i].renderTargetView);
                InReference(RTVs[i].resource);
                if (clearRTV)
                    m_commandList.ClearRenderTargetView(handles[i], new Vortice.Mathematics.Color4(color));
            }
            if (DSV != null)
            {
                DSV.StateChange(m_commandList, ResourceStates.DepthWrite);
                var dsv = DSV.GetDepthStencilView(graphicsDevice.device);
                InReference(DSV.depthStencilView);
                InReference(DSV.resource);
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
            ClearState();
            this.currentRootSignature = rootSignature;
            rootSignature.GetRootSignature(graphicsDevice);
        }

        public void SetRenderTargetScreen(Vector4 color, bool clearScreen)
        {
            var size = graphicsDevice.m_outputSize;

            m_commandList.RSSetScissorRect((int)size.X, (int)size.Y);
            m_commandList.RSSetViewport(0, 0, (int)size.X, (int)size.Y);
            var renderTargetView = graphicsDevice.GetRenderTargetView(m_commandList);
            if (clearScreen)
                m_commandList.ClearRenderTargetView(renderTargetView, new Vortice.Mathematics.Color4(color));
            m_commandList.OMSetRenderTargets(renderTargetView);
        }

        public void Draw(int vertexCount, int startVertexLocation)
        {
            PipelineBinding();
            m_commandList.DrawInstanced(vertexCount, 1, startVertexLocation, 0);
        }

        public void DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
        {
            PipelineBinding();
            DrawIndexedInstanced(indexCount, 1, startIndexLocation, baseVertexLocation, 0);
        }

        public void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
        {
            PipelineBinding();
            m_commandList.DrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
        }

        void PipelineBindingCompute()
        {
            if (_currentComputeRootSignature != currentRootSignature)
            {
                _currentComputeRootSignature = currentRootSignature;
                _currentGraphicsRootSignature = null;
                m_commandList.SetComputeRootSignature(currentRootSignature.rootSignature);
            }
            int cbvOffset = 0;
            int srvOffset = 0;
            int uavOffset = 0;
            for (int i = 0; i < currentRootSignature.descs.Length; i++)
            {
                ResourceAccessType d = currentRootSignature.descs[i];
                if (d == ResourceAccessType.CBV)
                {
                    if (currentCBVs.TryGetValue(cbvOffset, out ulong addr))
                        m_commandList.SetComputeRootConstantBufferView(i, addr);
                    cbvOffset++;
                }
                else if (d == ResourceAccessType.CBVTable)
                {
                    if (currentCBVs.TryGetValue(cbvOffset, out ulong addr))
                        m_commandList.SetComputeRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                    cbvOffset++;
                }
                else if (d == ResourceAccessType.SRV)
                {
                    if (currentSRVs.TryGetValue(srvOffset, out ulong addr))
                        m_commandList.SetComputeRootShaderResourceView(i, addr);
                    srvOffset++;
                }
                else if (d == ResourceAccessType.SRVTable)
                {
                    if (currentSRVs.TryGetValue(srvOffset, out ulong addr))
                        m_commandList.SetComputeRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                    srvOffset++;
                }
                else if (d == ResourceAccessType.UAV)
                {
                    if (currentUAVs.TryGetValue(uavOffset, out ulong addr))
                        m_commandList.SetComputeRootUnorderedAccessView(i, addr);
                    uavOffset++;
                }
                else if (d == ResourceAccessType.UAVTable)
                {
                    if (currentUAVs.TryGetValue(uavOffset, out ulong addr))
                        m_commandList.SetComputeRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                    uavOffset++;
                }
            }
        }

        void PipelineBinding()
        {
            if (_currentGraphicsRootSignature != currentRootSignature)
            {
                _currentGraphicsRootSignature = currentRootSignature;
                _currentComputeRootSignature = null;
                m_commandList.SetGraphicsRootSignature(currentRootSignature.rootSignature);
            }
            int cbvOffset = 0;
            int srvOffset = 0;
            int uavOffset = 0;
            for (int i = 0; i < currentRootSignature.descs.Length; i++)
            {
                ResourceAccessType d = currentRootSignature.descs[i];
                if (d == ResourceAccessType.CBV)
                {
                    if (currentCBVs.TryGetValue(cbvOffset, out ulong addr))
                        m_commandList.SetGraphicsRootConstantBufferView(i, addr);
                    cbvOffset++;
                }
                else if (d == ResourceAccessType.CBVTable)
                {
                    if (currentCBVs.TryGetValue(cbvOffset, out ulong addr))
                        m_commandList.SetGraphicsRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                    cbvOffset++;
                }
                else if (d == ResourceAccessType.SRV)
                {
                    if (currentSRVs.TryGetValue(srvOffset, out ulong addr))
                        m_commandList.SetGraphicsRootShaderResourceView(i, addr);
                    srvOffset++;
                }
                else if (d == ResourceAccessType.SRVTable)
                {
                    if (currentSRVs.TryGetValue(srvOffset, out ulong addr))
                        m_commandList.SetGraphicsRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                    srvOffset++;
                }
                else if (d == ResourceAccessType.UAV)
                {
                    if (currentUAVs.TryGetValue(uavOffset, out ulong addr))
                        m_commandList.SetGraphicsRootUnorderedAccessView(i, addr);
                    uavOffset++;
                }
                else if (d == ResourceAccessType.UAVTable)
                {
                    if (currentUAVs.TryGetValue(uavOffset, out ulong addr))
                        m_commandList.SetGraphicsRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                    uavOffset++;
                }
            }
        }

        public void Dispatch(int x, int y, int z)
        {
            PipelineBindingCompute();
            m_commandList.Dispatch(x, y, z);
        }

        public void EndCommand()
        {
            if (present)
                graphicsDevice.EndRenderTarget(m_commandList);
            m_commandList.Close();
        }

        public void Execute()
        {
            graphicsDevice.commandQueue.ExecuteCommandList(m_commandList);
            graphicsDevice.ReturnCommandList(m_commandList);
            m_commandList = null;
            if (present)
                graphicsDevice.Present(presentVsync);
            present = false;
            foreach (var resource in referenceThisCommand)
            {
                graphicsDevice.ResourceDelayRecycle(resource);
            }
            referenceThisCommand.Clear();
        }

        public void Present(bool vsync)
        {
            present = true;
            presentVsync = vsync;
        }

        public static void BeginAlloctor(GraphicsDevice device)
        {
            device.GetCommandAllocator().Reset();
        }

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
        void CreateUAVBuffer(int bufferLength, ref ID3D12Resource resource, ResourceStates resourceStates = ResourceStates.UnorderedAccess)
        {
            graphicsDevice.ResourceDelayRecycle(resource);
            ThrowIfFailed(graphicsDevice.device.CreateCommittedResource(
                new HeapProperties(HeapType.Default),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)bufferLength, ResourceFlags.AllowUnorderedAccess),
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

        void _RTWriteGpuAddr<T>(Span<T> data, BinaryWriter writer) where T : unmanaged
        {
            graphicsDevice.superRingBuffer.Upload(data, out ulong addr);
            writer.Write(addr);
        }

        GpuDescriptorHandle GetUAVHandle(Texture2D texture)
        {
            texture.StateChange(m_commandList, ResourceStates.UnorderedAccess);
            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuDescriptorHandle, out var gpuDescriptorHandle);
            graphicsDevice.device.CreateUnorderedAccessView(texture.resource, null, null, cpuDescriptorHandle);
            InReference(texture.resource);
            return gpuDescriptorHandle;
        }

        GpuDescriptorHandle GetUAVHandle(TextureCube texture)
        {
            //texture.StateChange(m_commandList, ResourceStates.UnorderedAccess);
            texture.SetAllResourceState(m_commandList, ResourceStates.UnorderedAccess);
            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuDescriptorHandle, out var gpuDescriptorHandle);
            graphicsDevice.device.CreateUnorderedAccessView(texture.resource, null, new UnorderedAccessViewDescription()
            {
                Format = Format.R32_Typeless,
                ViewDimension = UnorderedAccessViewDimension.Buffer,
                Texture2DArray = new Texture2DArrayUnorderedAccessView
                {
                    ArraySize = 6,
                },
            }, cpuDescriptorHandle);
            InReference(texture.resource);
            return gpuDescriptorHandle;
        }
        GpuDescriptorHandle GetUAVHandle(GPUBuffer buffer)
        {
            buffer.StateChange(m_commandList, ResourceStates.UnorderedAccess);
            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuDescriptorHandle, out var gpuDescriptorHandle);
            graphicsDevice.device.CreateUnorderedAccessView(buffer.resource, null, new UnorderedAccessViewDescription()
            {
                Format = Format.R32_Typeless,
                ViewDimension = UnorderedAccessViewDimension.Buffer,
                Buffer = new BufferUnorderedAccessView()
                {
                    Flags = BufferUnorderedAccessViewFlags.Raw,
                    NumElements = buffer.size / 4
                }
            }, cpuDescriptorHandle);
            InReference(buffer.resource);
            return gpuDescriptorHandle;
        }
        GpuDescriptorHandle GetSRVHandle(GPUBuffer buffer)
        {
            buffer.StateChange(m_commandList, ResourceStates.GenericRead);
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription();
            srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;

            srvDesc.Format = Format.R32_Typeless;
            srvDesc.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Buffer;
            srvDesc.Buffer.FirstElement = 0;
            srvDesc.Buffer.NumElements = buffer.size / 4;
            srvDesc.Buffer.Flags = BufferShaderResourceViewFlags.Raw;

            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuDescriptorHandle, out var gpuDescriptorHandle);
            graphicsDevice.device.CreateShaderResourceView(buffer.resource, srvDesc, cpuDescriptorHandle);
            InReference(buffer.resource);
            return gpuDescriptorHandle;
        }
        GpuDescriptorHandle GetSRVHandle(Texture2D texture, bool linear = false)
        {
            texture.StateChange(m_commandList, ResourceStates.GenericRead);
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription();
            srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
            var format = texture.format;
            if (linear && format == Format.R8G8B8A8_UNorm_SRgb)
                format = Format.R8G8B8A8_UNorm;
            srvDesc.Format = format;
            srvDesc.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Texture2D;
            srvDesc.Texture2D.MipLevels = texture.mipLevels;

            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuDescriptorHandle, out var gpuDescriptorHandle);
            graphicsDevice.device.CreateShaderResourceView(texture.resource, srvDesc, cpuDescriptorHandle);
            InReference(texture.resource);
            return gpuDescriptorHandle;
        }

        GpuDescriptorHandle GetSRVHandle(TextureCube texture)
        {
            texture.SetAllResourceState(m_commandList, ResourceStates.GenericRead);
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription();
            srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
            srvDesc.Format = texture.format;
            srvDesc.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.TextureCube;
            srvDesc.TextureCube.MipLevels = texture.mipLevels;

            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuDescriptorHandle, out var gpuDescriptorHandle);
            graphicsDevice.device.CreateShaderResourceView(texture.resource, srvDesc, cpuDescriptorHandle);
            InReference(texture.resource);
            return gpuDescriptorHandle;
        }
        GpuDescriptorHandle GetSRVHandleWithMip(TextureCube texture, int mips)
        {
            //texture.StateChange(m_commandList, ResourceStates.GenericRead);
            texture.SetPartResourceState(m_commandList, ResourceStates.GenericRead, 0, mips);
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription();
            srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
            srvDesc.Format = texture.format;
            srvDesc.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.TextureCube;
            srvDesc.TextureCube.MipLevels = mips;

            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuDescriptorHandle, out var gpuDescriptorHandle);
            graphicsDevice.device.CreateShaderResourceView(texture.resource, srvDesc, cpuDescriptorHandle);
            InReference(texture.resource);
            return gpuDescriptorHandle;
        }
        void InReference(ID3D12Object iD3D12Object)
        {
            if (referenceThisCommand.Add(iD3D12Object))
                iD3D12Object.AddRef();
        }
        ulong InReferenceAddr(ID3D12Resource iD3D12Object)
        {
            if (referenceThisCommand.Add(iD3D12Object))
                iD3D12Object.AddRef();
            return iD3D12Object.GPUVirtualAddress;
        }
        public HashSet<ID3D12Object> referenceThisCommand = new HashSet<ID3D12Object>();

        public PSODesc currentPSODesc;
        public UnnamedInputLayout currentInputLayout;

        public bool present;
        public bool presentVsync;
    }
}
