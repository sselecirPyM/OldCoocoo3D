#pragma once
#include "DeviceResources.h"
#include "VertexShader.h"
#include "PixelShader.h"
#include "GeometryShader.h"
#include "GraphicsSignature.h"
#include "PSODesc.h"
namespace Coocoo3DGraphics
{
	struct _PSODesc1
	{
		PSODesc desc;
		ID3D12RootSignature* rootSignature;
	};
	public ref class PSO sealed
	{
	public:
		property GraphicsObjectStatus Status;

		void Initialize(VertexShader^ vs, GeometryShader^ gs, PixelShader^ ps);
		void Unload();
		int GetVariantIndex(DeviceResources^ deviceResources, GraphicsSignature^ graphicsSignature, PSODesc psoDesc);
		void DelayDestroy(DeviceResources^ deviceResources);
	internal:
		VertexShader^ m_vertexShader;
		PixelShader^ m_pixelShader;
		GeometryShader^ m_geometryShader;
		static const UINT c_indexPipelineStateSkinning = 0;

		std::vector<Microsoft::WRL::ComPtr<ID3D12PipelineState>> m_pipelineStates;
		std::vector<_PSODesc1> m_psoDescs;

		inline void ClearState()
		{
			m_vertexShader = nullptr;
			m_pixelShader = nullptr;
			m_geometryShader = nullptr;
			m_pipelineStates.clear();
			m_psoDescs.clear();
		}
	};
}
