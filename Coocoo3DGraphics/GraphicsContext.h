#pragma once
#include "GraphicsDevice.h"
#include "MMDMesh.h"
#include "Texture2D.h"
#include "TextureCube.h"
#include "CBuffer.h"
#include "RootSignature.h"
#include "RayTracingScene.h"
#include "GPUProgram/ComputeShader.h"
#include "GPUProgram/PSO.h"
#include "ReadBackTexture2D.h"
#include "MeshBuffer.h"
#include "Uploader.h"
#include "MMDMeshAppend.h"

namespace Coocoo3DGraphics
{
	public enum struct ResourceStates
	{
        Common = 0,
        Present = 0,
        None = 0,
        VertexAndConstantBuffer = 1,
        IndexBuffer = 2,
        RenderTarget = 4,
        UnorderedAccess = 8,
        DepthWrite = 16,
        DepthRead = 32,
        NonPixelShaderResource = 64,
        PixelShaderResource = 128,
        AllShaderResource = 192,
        StreamOut = 256,
        IndirectArgument = 512,
        Predication = 512,
        CopyDestination = 1024,
        CopySource = 2048,
        GenericRead = 2755,
        ResolveDestination = 4096,
        ResolveSource = 8192,
        VideoDecodeRead = 65536,
        VideoDecodeWrite = 131072,
        VideoProcessRead = 262144,
        VideoProcessWrite = 524288,
        VideoEncodeRead = 2097152,
        RaytracingAccelerationStructure = 4194304,
        VideoEncodeWrite = 8388608,
        ShadingRateSource = 16777216
	};
	public ref class GraphicsContext sealed
	{
	public:
		static GraphicsContext^ Load(GraphicsDevice^ graphicsDevice);
		void Reload(GraphicsDevice^ graphicsDevice);
		//void ClearTextureRTV(TextureCube^ texture);
		void SetPSO(ComputeShader^ computeShader);
		void SetPSO(PSO^ pObject, int variantIndex);
		void UpdateResource(CBuffer^ buffer, const Platform::Array<byte>^ data, int sizeInByte, int dataOffset);
		void UpdateResource(CBuffer^ buffer, const Platform::Array<Windows::Foundation::Numerics::float4x4>^ data, int sizeInByte, int dataOffset);
		void UpdateVerticesPos(MMDMeshAppend^ mesh, const Platform::Array<Windows::Foundation::Numerics::float3>^ verticeData);
		void SetSRVTSlot(Texture2D^ texture, int slot);
		void SetSRVTSlot(TextureCube^ texture, int slot);
		//void SetSRVTFace(RenderTextureCube^ texture, int face, int index);
		void SetCBVRSlot(CBuffer^ buffer, int offset256, int size256, int slot);
		//void SetUAVT(Texture2D^ texture, int index);
		void SetComputeSRVT(Texture2D^ texture, int index);
		void SetComputeSRVT(TextureCube^ texture, int index);
		//void SetComputeSRVR(MeshBuffer^ mesh, int startLocation, int index);
		//void SetComputeSRVRIndex(MMDMesh^ mesh, int startLocation, int index);
		void SetComputeCBVR(CBuffer^ buffer, int index);
		void SetComputeCBVR(CBuffer^ buffer, int offset256, int size256, int index);
		void SetComputeCBVRSlot(CBuffer^ buffer, int offset256, int size256, int slot);
		//void SetComputeUAVR(MeshBuffer^ mesh, int startLocation, int index);
		void SetComputeUAVT(Texture2D^ texture, int index);
		void SetComputeUAVT(TextureCube^ texture, int mipIndex, int index);
		void SetComputeUAVTSlot(Texture2D^ texture, int slot);
		void SetSOMesh(MeshBuffer^ mesh);
		void SetSOMeshNone();
		void Draw(int vertexCount, int startVertexLocation);
		void DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation);
		//void DrawIndexedInstanced(int indexCount, int startIndexLocation, int baseVertexLocation, int instanceCount, int startInstanceLocation);
		void DoRayTracing(RayTracingScene^ rayTracingScene, int width, int height, int raygenIndex);
		void UploadMesh(MMDMesh^ mesh);
		void UploadMesh(MMDMeshAppend^ mesh, const Platform::Array<byte>^ data);
		void UploadTexture(TextureCube^ texture, Uploader^ uploader);
		void UploadTexture(Texture2D^ texture, Uploader^ uploader);
		void UpdateRenderTexture(Texture2D^ texture);
		void UpdateRenderTexture(TextureCube^ texture);
		void UpdateReadBackTexture(ReadBackTexture2D^ texture);
		void CopyTexture(ReadBackTexture2D^ target, Texture2D^ texture2d, int index);
		void RSSetScissorRect(int left, int top, int right, int buttom);
		void Dispatch(int x, int y, int z);
		void Prepare(RayTracingScene^ rtas, int meshCount);
		void BuildBottomAccelerationStructures(RayTracingScene^ rayTracingAccelerationStructure, MeshBuffer^ mesh, MMDMesh^ indexBuffer, int vertexBegin, int indexBegin, int indexCount);
		void BuildBASAndParam(RayTracingScene^ rayTracingAccelerationStructure, MeshBuffer^ mesh, MMDMesh^ indexBuffer, UINT instanceMask, int vertexBegin, int indexBegin, int indexCount, Texture2D^ diff, CBuffer^ mat, int offset256);
		void BuildTopAccelerationStructures(RayTracingScene^ rtas);
		void BuildShaderTable(RayTracingScene^ rts, const Platform::Array<Platform::String^>^ raygenShaderNames, const Platform::Array<Platform::String^>^ missShaderNames, const Platform::Array <Platform::String^>^ hitGroupNames, int instances);
		void SetMesh(MMDMesh^ mesh);
		void SetMeshVertex(MMDMesh^ mesh);
		void SetMeshVertex(MMDMeshAppend^ mesh);
		void SetMeshIndex(MMDMesh^ mesh);
		void SetMesh(MeshBuffer^ mesh);
		void SetDSV(Texture2D^ texture, bool clear);
		//void SetDSV(RenderTextureCube^ texture, int face, bool clear);
		void SetRTV(Texture2D^ RTV, Windows::Foundation::Numerics::float4 color, bool clear);
		void SetRTV(const Platform::Array <Texture2D^>^ RTVs, Windows::Foundation::Numerics::float4 color, bool clear);
		void SetRTVDSV(Texture2D^ RTV, Texture2D^ DSV, Windows::Foundation::Numerics::float4 color, bool clearRTV, bool clearDSV);
		void SetRTVDSV(const Platform::Array <Texture2D^>^ RTVs, Texture2D^ DSV, Windows::Foundation::Numerics::float4 color, bool clearRTV, bool clearDSV);
		void SetRootSignature(RootSignature^ rootSignature);
		void SetRootSignatureCompute(RootSignature^ rootSignature);
		void SetRootSignatureRayTracing(RayTracingScene^ rootSignature);
		void SetRootSignatureRayTracing(RootSignature^ rootSignature);
		void ResourceBarrierScreen(ResourceStates before, ResourceStates after);
		void SetRenderTargetScreen(Windows::Foundation::Numerics::float4 color, bool clearScreen);
		static void BeginAlloctor(GraphicsDevice^ deviceResources);
		void SetDescriptorHeapDefault();
		void Begin();
		void EndCommand();
		//void BeginEvent();
		//void EndEvent();
		void Execute();
	internal:
		void SetCBVR(CBuffer^ buffer, int index);
		void SetCBVR(CBuffer^ buffer, int offset256, int size256, int index);
		void SetSRVT(Texture2D^ texture, int index);
		void SetSRVT(TextureCube^ texture, int index);
		GraphicsDevice^ m_graphicsDevice;
		RootSignature^ m_currentSign;
		Microsoft::WRL::ComPtr<ID3D12GraphicsCommandList4>	m_commandList;
	};
}