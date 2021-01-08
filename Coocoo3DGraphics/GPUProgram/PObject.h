#pragma once
#include "DeviceResources.h"
#include "VertexShader.h"
#include "PixelShader.h"
#include "GeometryShader.h"
#include "GraphicsSignature.h"
#include "GraphicsObjectStatus.h"
namespace Coocoo3DGraphics
{
	public enum struct CullMode
	{
		back = 0,
		none = 1,
		front = 2,
	};
	public enum struct BlendState
	{
		none = 0,
		alpha = 1,
		add = 2,
	};
	public enum struct eInputLayout
	{
		mmd = 0,
		postProcess = 1,
		skinned = 2,
	};
	public enum struct D3D12PrimitiveTopologyType
	{
		UNDEFINED = 0,
		POINT = 1,
		LINE = 2,
		TRIANGLE = 3,
		PATCH = 4
	};
	public ref class PObject sealed
	{
	public:
		property GraphicsObjectStatus Status;
		void Reload(DeviceResources^ deviceResources, GraphicsSignature^ graphicsSignature, eInputLayout type, BlendState blendState, VertexShader^ vertexShader, GeometryShader^ geometryShader, PixelShader^ pixelShader, DxgiFormat rtvFormat, DxgiFormat depthFormat);
		void Reload(DeviceResources^ deviceResources, GraphicsSignature^ graphicsSignature, eInputLayout type, BlendState blendState, VertexShader^ vertexShader, GeometryShader^ geometryShader, PixelShader^ pixelShader, DxgiFormat rtvFormat, DxgiFormat depthFormat, D3D12PrimitiveTopologyType primitiveTopologyType);
		//使用Upload上传GPU
		void ReloadDepthOnly(VertexShader^ vs, PixelShader^ ps, int depthOffset, DxgiFormat depthFormat);
		//使用Upload上传GPU
		void ReloadSkinning(VertexShader^ vs, GeometryShader^ gs);
		//使用Upload上传GPU
		void ReloadDrawing(BlendState blendState, VertexShader^ vs, GeometryShader^ gs, PixelShader^ ps, DxgiFormat rtvFormat, DxgiFormat depthFormat);
		void ReloadDrawing(BlendState blendState, VertexShader^ vs, GeometryShader^ gs, PixelShader^ ps, DxgiFormat rtvFormat, DxgiFormat depthFormat,int renderTargetCount);
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
		BlendState m_blendState;
		D3D12_PRIMITIVE_TOPOLOGY_TYPE m_primitiveTopologyType;
		int m_depthBias;
		int m_renderTargetCount;
		Microsoft::WRL::ComPtr<ID3D12PipelineState>			m_pipelineState[6];

		inline void ClearState()
		{
			m_vertexShader = nullptr;
			m_pixelShader = nullptr;
			m_geometryShader = nullptr;
			m_renderTargetFormat = DXGI_FORMAT_UNKNOWN;
			m_depthFormat = DXGI_FORMAT_UNKNOWN;
			m_useStreamOutput = false;
			m_blendState = BlendState::none;
			m_depthBias = 0;
			m_renderTargetCount = 0;
			m_primitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE_UNDEFINED;
		}
	};
}
