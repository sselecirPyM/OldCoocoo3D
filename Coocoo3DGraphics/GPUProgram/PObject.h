#pragma once
#include "DeviceResources.h"
#include "VertexShader.h"
#include "PixelShader.h"
#include "GeometryShader.h"
#include "GraphicsSignature.h"
#include "PSODesc.h"
namespace Coocoo3DGraphics
{
	public ref class PObject sealed
	{
	public:
		property GraphicsObjectStatus Status;
		void Initialize(DeviceResources^ deviceResources, GraphicsSignature^ graphicsSignature, EInputLayout type, EBlendState blendState, VertexShader^ vertexShader, GeometryShader^ geometryShader, PixelShader^ pixelShader, DxgiFormat rtvFormat, DxgiFormat depthFormat);
		void Initialize(DeviceResources^ deviceResources, GraphicsSignature^ graphicsSignature, EInputLayout type, EBlendState blendState, VertexShader^ vertexShader, GeometryShader^ geometryShader, PixelShader^ pixelShader, DxgiFormat rtvFormat, DxgiFormat depthFormat, ED3D12PrimitiveTopologyType primitiveTopologyType);
		//使用Upload上传GPU
		void InitializeDepthOnly(VertexShader^ vs, PixelShader^ ps, int depthOffset, DxgiFormat depthFormat);
		//使用Upload上传GPU
		void InitializeSkinning(VertexShader^ vs, GeometryShader^ gs);
		//使用Upload上传GPU
		void InitializeDrawing(EBlendState blendState, VertexShader^ vs, GeometryShader^ gs, PixelShader^ ps, DxgiFormat rtvFormat, DxgiFormat depthFormat);
		void InitializeDrawing(EBlendState blendState, VertexShader^ vs, GeometryShader^ gs, PixelShader^ ps, DxgiFormat rtvFormat, DxgiFormat depthFormat, int renderTargetCount);
		bool Upload(DeviceResources^ deviceResources, GraphicsSignature^ graphicsSignature);
		void Unload();
	internal:
		VertexShader^ m_vertexShader;
		PixelShader^ m_pixelShader;
		GeometryShader^ m_geometryShader;
		static const UINT c_indexPipelineStateSkinning = 0;

		bool m_useStreamOutput;
		bool m_isDepthOnly;
		DXGI_FORMAT m_renderTargetFormat;
		DXGI_FORMAT m_depthFormat;
		EBlendState m_blendState;
		D3D12_PRIMITIVE_TOPOLOGY_TYPE m_primitiveTopologyType;
		int m_depthBias;
		int m_renderTargetCount;
		Microsoft::WRL::ComPtr<ID3D12PipelineState>			m_pipelineState[6];
		std::vector<Microsoft::WRL::ComPtr<ID3D12PipelineState>> m_pipelineStates;

		inline void ClearState()
		{
			m_vertexShader = nullptr;
			m_pixelShader = nullptr;
			m_geometryShader = nullptr;
			m_renderTargetFormat = DXGI_FORMAT_UNKNOWN;
			m_depthFormat = DXGI_FORMAT_UNKNOWN;
			m_useStreamOutput = false;
			m_blendState = EBlendState::none;
			m_depthBias = 0;
			m_renderTargetCount = 0;
			m_primitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE_UNDEFINED;
		}
	};
}
