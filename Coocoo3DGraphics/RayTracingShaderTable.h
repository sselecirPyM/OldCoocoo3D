#pragma once
namespace Coocoo3DGraphics
{
	struct CooRayTracingParamLocal2
	{
		D3D12_GPU_VIRTUAL_ADDRESS cbv3;
		D3D12_GPU_VIRTUAL_ADDRESS srv0_1;
		D3D12_GPU_VIRTUAL_ADDRESS srv1_1;
		D3D12_GPU_DESCRIPTOR_HANDLE srv2_1;
		D3D12_GPU_DESCRIPTOR_HANDLE srv3_1;
		D3D12_GPU_DESCRIPTOR_HANDLE srv4_1;
		D3D12_GPU_DESCRIPTOR_HANDLE srv5_1;
		D3D12_GPU_DESCRIPTOR_HANDLE srv6_1;
	};
	public ref class RayTracingShaderTable sealed
	{
	public:
	internal:
		Microsoft::WRL::ComPtr<ID3D12Resource> m_missShaderTable;
		Microsoft::WRL::ComPtr<ID3D12Resource> m_hitGroupShaderTable;
		Microsoft::WRL::ComPtr<ID3D12Resource> m_rayGenShaderTable;

		UINT m_rayGenerateShaderTableStrideInBytes;
		UINT m_hitGroupShaderTableStrideInBytes;
		UINT m_missShaderTableStrideInBytes;

		std::vector<CooRayTracingParamLocal2> arguments;
	};
}
