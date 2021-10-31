#pragma once
#include "GraphicsDevice.h"
namespace Coocoo3DGraphics
{
	public enum struct PrimitiveTopology
	{
		Undefined = 0,
		PointList = 1,
		LineList = 2,
		LineStrip = 3,
		TriangleList = 4,
		TriangleStrip = 5,
		LineListAdjacency = 10,
		LineStripAdjacency = 11,
		TriangleListAdjacency = 12,
		TriangleStripAdjacency = 13,
		PatchListWith1ControlPoints = 33,
		PatchListWith2ControlPoints = 34,
		PatchListWith3ControlPoints = 35,
		PatchListWith4ControlPoints = 36,
		PatchListWith5ControlPoints = 37,
		PatchListWith6ControlPoints = 38,
		PatchListWith7ControlPoints = 39,
		PatchListWith8ControlPoints = 40,
		PatchListWith9ControlPoints = 41,
		PatchListWith10ControlPoints = 42,
		PatchListWith11ControlPoints = 43,
		PatchListWith12ControlPoints = 44,
		PatchListWith13ControlPoints = 45,
		PatchListWith14ControlPoints = 46,
		PatchListWith15ControlPoints = 47,
		PatchListWith16ControlPoints = 48,
		PatchListWith17ControlPoints = 49,
		PatchListWith18ControlPoints = 50,
		PatchListWith19ControlPoints = 51,
		PatchListWith20ControlPoints = 52,
		PatchListWith21ControlPoints = 53,
		PatchListWith22ControlPoints = 54,
		PatchListWith23ControlPoints = 55,
		PatchListWith24ControlPoints = 56,
		PatchListWith25ControlPoints = 57,
		PatchListWith26ControlPoints = 58,
		PatchListWith27ControlPoints = 59,
		PatchListWith28ControlPoints = 60,
		PatchListWith29ControlPoints = 61,
		PatchListWith30ControlPoints = 62,
		PatchListWith31ControlPoints = 63,
		PatchListWith32ControlPoints = 64
	};
	public ref class MMDMesh sealed
	{
	public:
		static MMDMesh^ Load1(const Platform::Array<byte>^ verticeData, const Platform::Array<int>^ indexData, int vertexStride, PrimitiveTopology pt);

		void Reload1(const Platform::Array<byte>^ verticeData, const Platform::Array<int>^ indexData, int vertexStride, PrimitiveTopology pt);
		void Reload1(const Platform::Array<byte>^ verticeData, const Platform::Array<byte>^ indexData, int vertexStride, PrimitiveTopology pt);
		void ReloadNDCQuad();
		//void ReloadCube();
		//void ReloadCubeWire();
		virtual ~MMDMesh();
		int GetIndexCount();
		int GetVertexCount();
		void SetIndexFormat(Format format);
		property Platform::Array<byte>^ m_verticeData;
	internal:
		bool updated = false;
		int m_indexCount;
		int m_vertexCount;
		const static UINT c_indexStride = sizeof(UINT);
		UINT m_vertexStride;
		Microsoft::WRL::ComPtr<ID3DBlob> m_indexData;

		D3D_PRIMITIVE_TOPOLOGY m_primitiveTopology = D3D_PRIMITIVE_TOPOLOGY_UNDEFINED;
		Microsoft::WRL::ComPtr<ID3D12Resource>				m_vertexBuffer;
		D3D12_VERTEX_BUFFER_VIEW m_vertexBufferView;
		Microsoft::WRL::ComPtr<ID3D12Resource>				m_indexBuffer;
		D3D12_INDEX_BUFFER_VIEW m_indexBufferView;
	};
}

