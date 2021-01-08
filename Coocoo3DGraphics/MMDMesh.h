#pragma once
#include "DeviceResources.h"
namespace Coocoo3DGraphics
{
	public enum struct PrimitiveTopology
	{
		_UNDEFINED = 0,
		_POINTLIST = 1,
		_LINELIST = 2,
		_LINESTRIP = 3,
		_TRIANGLELIST = 4,
		_TRIANGLESTRIP = 5,
		_LINELIST_ADJ = 10,
		_LINESTRIP_ADJ = 11,
		_TRIANGLELIST_ADJ = 12,
		_TRIANGLESTRIP_ADJ = 13,
		_1_CONTROL_POINT_PATCHLIST = 33,
		_2_CONTROL_POINT_PATCHLIST = 34,
		_3_CONTROL_POINT_PATCHLIST = 35,
		_4_CONTROL_POINT_PATCHLIST = 36,
		_5_CONTROL_POINT_PATCHLIST = 37,
		_6_CONTROL_POINT_PATCHLIST = 38,
		_7_CONTROL_POINT_PATCHLIST = 39,
		_8_CONTROL_POINT_PATCHLIST = 40,
		_9_CONTROL_POINT_PATCHLIST = 41,
		_10_CONTROL_POINT_PATCHLIST = 42,
		_11_CONTROL_POINT_PATCHLIST = 43,
		_12_CONTROL_POINT_PATCHLIST = 44,
		_13_CONTROL_POINT_PATCHLIST = 45,
		_14_CONTROL_POINT_PATCHLIST = 46,
		_15_CONTROL_POINT_PATCHLIST = 47,
		_16_CONTROL_POINT_PATCHLIST = 48,
		_17_CONTROL_POINT_PATCHLIST = 49,
		_18_CONTROL_POINT_PATCHLIST = 50,
		_19_CONTROL_POINT_PATCHLIST = 51,
		_20_CONTROL_POINT_PATCHLIST = 52,
		_21_CONTROL_POINT_PATCHLIST = 53,
		_22_CONTROL_POINT_PATCHLIST = 54,
		_23_CONTROL_POINT_PATCHLIST = 55,
		_24_CONTROL_POINT_PATCHLIST = 56,
		_25_CONTROL_POINT_PATCHLIST = 57,
		_26_CONTROL_POINT_PATCHLIST = 58,
		_27_CONTROL_POINT_PATCHLIST = 59,
		_28_CONTROL_POINT_PATCHLIST = 60,
		_29_CONTROL_POINT_PATCHLIST = 61,
		_30_CONTROL_POINT_PATCHLIST = 62,
		_31_CONTROL_POINT_PATCHLIST = 63,
		_32_CONTROL_POINT_PATCHLIST = 64,
	};
	public ref class MMDMesh sealed
	{
	public:
		static MMDMesh^ Load1(const Platform::Array<byte>^ verticeData, const Platform::Array<int>^ indexData, int vertexStride, PrimitiveTopology pt);
		//在上传GPU之前是无法使用的。使用GraphicsContext::void UploadMesh(MMDMesh^ mesh)上传。
		void Reload1(const Platform::Array<byte>^ verticeData, const Platform::Array<int>^ indexData, int vertexStride, PrimitiveTopology pt);
		void ReloadNDCQuad();
		void ReloadCube();
		void ReloadCubeWire();
		void ReleaseUploadHeapResource();
		static void CopyPosData(Platform::WriteOnlyArray<Windows::Foundation::Numerics::float3>^ Target, const Platform::Array<byte>^ source);
		virtual ~MMDMesh();

		property Platform::Array<byte>^ m_verticeData;
		property int m_indexCount;
		property int m_vertexCount;
	internal:
		const static UINT c_indexStride = sizeof(UINT);
		UINT m_vertexStride;
		Microsoft::WRL::ComPtr<ID3DBlob> m_indexData;

		D3D_PRIMITIVE_TOPOLOGY m_primitiveTopology = D3D_PRIMITIVE_TOPOLOGY_UNDEFINED;

		Microsoft::WRL::ComPtr<ID3D12Resource>				m_vertexBuffer;
		D3D12_VERTEX_BUFFER_VIEW m_vertexBufferView;
		Microsoft::WRL::ComPtr<ID3D12Resource>				m_indexBuffer;
		D3D12_INDEX_BUFFER_VIEW m_indexBufferView;
		Microsoft::WRL::ComPtr<ID3D12Resource> m_vertexBufferUpload;
		Microsoft::WRL::ComPtr<ID3D12Resource> m_indexBufferUpload;
	};
}

