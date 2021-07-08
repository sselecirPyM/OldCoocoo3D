#pragma once
#include "DeviceResources.h"
#include "MMDMesh.h"
#include "Texture2D.h"
#include "TextureCube.h"
#include "RenderTexture2D.h"
#include "RenderTextureCube.h"
#include "CBuffer.h"
//#include "SBuffer.h"
#include "GraphicsSignature.h"
#include "RayTracingScene.h"
#include "GPUProgram/ComputeShader.h"
#include "GPUProgram/PSO.h"
#include "ReadBackTexture2D.h"
#include "TwinBuffer.h"
#include "MeshBuffer.h"
#include "Uploader.h"
#include "MMDMeshAppend.h"
#include "RayTracingStateObject.h"
#include "RayTracingASGroup.h"
#include "RayTracingShaderTable.h"
#include "RayTracingTopAS.h"
#include "RayTracingInstanceGroup.h"

namespace Coocoo3DGraphics
{
	public enum struct D3D12ResourceStates
	{
		_COMMON = 0,
		_VERTEX_AND_CONSTANT_BUFFER = 0x1,
		_INDEX_BUFFER = 0x2,
		_RENDER_TARGET = 0x4,
		_UNORDERED_ACCESS = 0x8,
		_DEPTH_WRITE = 0x10,
		_DEPTH_READ = 0x20,
		_NON_PIXEL_SHADER_RESOURCE = 0x40,
		_PIXEL_SHADER_RESOURCE = 0x80,
		_STREAM_OUT = 0x100,
		_INDIRECT_ARGUMENT = 0x200,
		_COPY_DEST = 0x400,
		_COPY_SOURCE = 0x800,
		_RESOLVE_DEST = 0x1000,
		_RESOLVE_SOURCE = 0x2000,
		_RAYTRACING_ACCELERATION_STRUCTURE = 0x400000,
		_GENERIC_READ = (((((0x1 | 0x2) | 0x40) | 0x80) | 0x200) | 0x800),
		_PRESENT = 0,
		_PREDICATION = 0x200,
		_VIDEO_DECODE_READ = 0x10000,
		_VIDEO_DECODE_WRITE = 0x20000,
		_VIDEO_PROCESS_READ = 0x40000,
		_VIDEO_PROCESS_WRITE = 0x80000,
		_VIDEO_ENCODE_READ = 0x200000,
		_VIDEO_ENCODE_WRITE = 0x800000
	};
	public ref class GraphicsContext sealed
	{
	public:
		static GraphicsContext^ Load(DeviceResources^ deviceResources);
		void Reload(DeviceResources^ deviceResources);
		void ClearTextureRTV(RenderTextureCube^ texture);
		void SetPSO(ComputeShader^ computeShader);
		void SetPSO(PSO^ pObject, int variantIndex);
		void UpdateResource(CBuffer^ buffer, const Platform::Array<byte>^ data, UINT sizeInByte, int dataOffset);
		void UpdateResource(CBuffer^ buffer, const Platform::Array<Windows::Foundation::Numerics::float4x4>^ data, UINT sizeInByte, int dataOffset);
		void UpdateResourceRegion(CBuffer^ buffer, UINT bufferDataOffset, const Platform::Array<byte>^ data, UINT sizeInByte, int dataOffset);
		void UpdateResourceRegion(CBuffer^ buffer, UINT bufferDataOffset, const Platform::Array<Windows::Foundation::Numerics::float4x4>^ data, UINT sizeInByte, int dataOffset);
		void UpdateVerticesPos(MMDMeshAppend^ mesh, const Platform::Array<Windows::Foundation::Numerics::float3>^ verticeData, int index);
		void SetSRVTSlot(ITexture2D^ texture, int slot);
		void SetSRVTSlot(ITextureCube^ texture, int slot);
		//void SetSRVTFace(RenderTextureCube^ texture, int face, int index);
		void SetCBVRSlot(CBuffer^ buffer, int offset256, int size256, int slot);
		void SetUAVT(RenderTexture2D^ texture, int index);
		void SetComputeSRVT(ITexture2D^ texture, int index);
		void SetComputeSRVT(ITextureCube^ texture, int index);
		//void SetComputeSRVTFace(RenderTextureCube^ texture, int face, int index);
		void SetComputeSRVR(TwinBuffer^ mesh, int bufIndex, int index);
		void SetComputeSRVR(MeshBuffer^ mesh, int startLocation, int index);
		void SetComputeSRVRIndex(MMDMesh^ mesh, int startLocation, int index);
		void SetComputeCBVR(CBuffer^ buffer, int index);
		void SetComputeCBVR(CBuffer^ buffer, int offset256, int size256, int index);
		void SetComputeCBVRSlot(CBuffer^ buffer, int offset256, int size256, int slot);
		void SetComputeUAVR(MeshBuffer^ mesh, int startLocation, int index);
		void SetComputeUAVR(TwinBuffer^ buffer, int bufIndex, int index);
		void SetComputeUAVT(RenderTexture2D^ texture, int index);
		void SetComputeUAVT(RenderTextureCube^ texture, int mipIndex, int index);
		void SetComputeUAVTSlot(RenderTexture2D^ texture, int slot);
		void SetRayTracingStateObject(RayTracingStateObject^ stateObject);
		void SetSOMesh(MeshBuffer^ mesh);
		void SetSOMeshNone();
		void Draw(int vertexCount, int startVertexLocation);
		void DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation);
		void DrawIndexedInstanced(int indexCount, int startIndexLocation, int baseVertexLocation, int instanceCount, int startInstanceLocation);
		void DoRayTracing(RayTracingScene^ rayTracingScene, int width, int height, int raygenIndex);
		void UploadMesh(MMDMesh^ mesh);
		void UploadMesh(MMDMeshAppend^ mesh, const Platform::Array<byte>^ data);
		void UploadTexture(TextureCube^ texture, Uploader^ uploader);
		void UploadTexture(Texture2D^ texture, Uploader^ uploader);
		void UpdateRenderTexture(IRenderTexture^ texture);
		void UpdateReadBackTexture(ReadBackTexture2D^ texture);
		void Copy(TextureCube^ source, RenderTextureCube^ dest);
		void CopyBackBuffer(ReadBackTexture2D^ target, int index);
		void Dispatch(int x, int y, int z);
		void DispatchRay(RayTracingShaderTable^ rtst, int x, int y, int z);
		void Prepare(RayTracingScene^ rtas, int meshCount);
		void BuildBottomAccelerationStructures(RayTracingScene^ rayTracingAccelerationStructure, MeshBuffer^ mesh, MMDMesh^ indexBuffer, int vertexBegin, int indexBegin, int indexCount);
		void BuildBASAndParam(RayTracingScene^ rayTracingAccelerationStructure, MeshBuffer^ mesh, MMDMesh^ indexBuffer, UINT instanceMask, int vertexBegin, int indexBegin, int indexCount, Texture2D^ diff, CBuffer^ mat, int offset256);
		void BuildTopAccelerationStructures(RayTracingScene^ rtas);
		void BuildShaderTable(RayTracingScene^ rts, const Platform::Array<Platform::String^>^ raygenShaderNames, const Platform::Array<Platform::String^>^ missShaderNames, const Platform::Array <Platform::String^>^ hitGroupNames, int instances);
		void Prepare(RayTracingASGroup^ asGroup);
		void Prepare(RayTracingInstanceGroup^ rtig);
		void BuildBTAS(RayTracingASGroup^ asGroup, MeshBuffer^ mesh, MMDMesh^ indexBuffer, int vertexBegin, int indexBegin, int indexCount);
		void BuildInst(RayTracingInstanceGroup^ rtig, RayTracingASGroup^ asGroup, int instId, int i2hitGroup, UINT instMask);
		void TestShaderTable(RayTracingShaderTable^ rtst, RayTracingStateObject^ rtso, const Platform::Array<Platform::String^>^ raygenShaderNames, const Platform::Array<Platform::String^>^ missShaderNames);
		void TestShaderTable2(RayTracingShaderTable^ rtst, RayTracingStateObject^ rtso, RayTracingASGroup^ asGroup, const Platform::Array<Platform::String^>^ hitGroupNames);
		void BuildTPAS(RayTracingInstanceGroup^ rtis, RayTracingTopAS^ rttas, RayTracingASGroup^ asGroup);
		void SetTPAS(RayTracingTopAS^ rttas, RayTracingStateObject^ rtso, int slot);
		void SetMesh(MMDMesh^ mesh);
		void SetMeshVertex(MMDMesh^ mesh);
		void SetMeshVertex(MMDMeshAppend^ mesh);
		void SetMeshIndex(MMDMesh^ mesh);
		void SetMesh(MeshBuffer^ mesh);
		void SetDSV(RenderTexture2D^ texture, bool clear);
		void SetDSV(RenderTextureCube^ texture, int face, bool clear);
		void SetRTV(RenderTexture2D^ RTV, Windows::Foundation::Numerics::float4 color, bool clear);
		void SetRTV(const Platform::Array <RenderTexture2D^>^ RTVs, Windows::Foundation::Numerics::float4 color, bool clear);
		void SetRTVDSV(RenderTexture2D^ RTV, RenderTexture2D^ DSV, Windows::Foundation::Numerics::float4 color, bool clearRTV, bool clearDSV);
		void SetRTVDSV(const Platform::Array <RenderTexture2D^>^ RTVs, RenderTexture2D^ DSV, Windows::Foundation::Numerics::float4 color, bool clearRTV, bool clearDSV);
		void SetRootSignature(GraphicsSignature^ rootSignature);
		void SetRootSignatureCompute(GraphicsSignature^ rootSignature);
		void SetRootSignatureRayTracing(RayTracingScene^ rootSignature);
		void SetRootSignatureRayTracing(GraphicsSignature^ rootSignature);
		void ResourceBarrierScreen(D3D12ResourceStates before, D3D12ResourceStates after);
		void SetRenderTargetScreen(Windows::Foundation::Numerics::float4 color, RenderTexture2D^ DSV, bool clearScreen, bool clearDSV);
		void SetRenderTargetScreen(Windows::Foundation::Numerics::float4 color, bool clearScreen);
		static void BeginAlloctor(DeviceResources^ deviceResources);
		void SetDescriptorHeapDefault();
		void BeginCommand();
		void EndCommand();
		void BeginEvent();
		void EndEvent();
		void Execute();
	internal:
		void SetCBVR(CBuffer^ buffer, int index);
		void SetCBVR(CBuffer^ buffer, int offset256, int size256, int index);
		void SetSRVT(ITexture2D^ texture, int index);
		void SetSRVT(ITextureCube^ texture, int index);
		DeviceResources^ m_deviceResources;
		GraphicsSignature^ m_currentSign;
		Microsoft::WRL::ComPtr<ID3D12GraphicsCommandList4>	m_commandList;
	};
}