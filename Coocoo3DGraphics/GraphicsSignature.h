#pragma once
#include "DeviceResources.h"
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
	public ref class GraphicsSignature sealed
	{
	public:
		void ReloadMMD(DeviceResources^ deviceResources);
		void ReloadSkinning(DeviceResources^ deviceResources);
		void Reload(DeviceResources^ deviceResources, const Platform::Array<GraphicSignatureDesc>^ Descs);
		void ReloadCompute(DeviceResources^ deviceResources, const Platform::Array<GraphicSignatureDesc>^ Descs);
		void Unload();
	internal:
		Microsoft::WRL::ComPtr<ID3D12RootSignature> m_rootSignature;
	};
}
