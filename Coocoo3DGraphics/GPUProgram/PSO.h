#pragma once
#include "GraphicsDevice.h"
#include "VertexShader.h"
#include "PixelShader.h"
#include "GeometryShader.h"
#include "RootSignature.h"
#include "PSODesc.h"
namespace Coocoo3DGraphics
{
	using namespace Platform;
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
		void Initialize(const Array<byte>^ vs, const Array<byte>^ gs, const Array<byte>^ ps);
		void Unload();
		int GetVariantIndex(GraphicsDevice^ deviceResources, RootSignature^ graphicsSignature, PSODesc psoDesc);
		void DelayDestroy(GraphicsDevice^ deviceResources);
	internal:
		Microsoft::WRL::ComPtr<ID3DBlob> m_vertexShader;
		Microsoft::WRL::ComPtr<ID3DBlob> m_pixelShader;
		Microsoft::WRL::ComPtr<ID3DBlob> m_geometryShader;
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
