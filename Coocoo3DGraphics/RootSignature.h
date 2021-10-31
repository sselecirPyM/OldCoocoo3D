#pragma once
#include "GraphicsDevice.h"
namespace Coocoo3DGraphics {
	public enum struct GraphicSignatureDesc
	{
		CBV,
		SRV,
		UAV,
		CBVTable,
		SRVTable,
		UAVTable,
	};
	public value struct GraphicsRootParameter
	{
		GraphicSignatureDesc typeDesc;
		int index;
	};
	public ref class RootSignature sealed
	{
	public:
		void ReloadSkinning(GraphicsDevice^ graphicsDevice);
		void Reload(GraphicsDevice^ graphicsDevice, const Platform::Array<GraphicSignatureDesc>^ Descs);
		void ReloadCompute(GraphicsDevice^ graphicsDevice, const Platform::Array<GraphicSignatureDesc>^ Descs);
		void RayTracingLocal(GraphicsDevice^ graphicsDevice);
		void Unload();
	internal:
		void Sign1(GraphicsDevice^ deviceResources, const Platform::Array<GraphicSignatureDesc>^ Descs, Microsoft::WRL::ComPtr<ID3D12RootSignature>& m_sign, D3D12_ROOT_SIGNATURE_FLAGS flags);
		Microsoft::WRL::ComPtr<ID3D12RootSignature> m_rootSignature;
		std::map<int, int> m_cbv;
		std::map<int, int> m_srv;
		std::map<int, int> m_uav;
	};
}
