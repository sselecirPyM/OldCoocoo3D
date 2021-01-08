#include "pch.h"
#include "MMDMesh.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
using namespace DirectX;

MMDMesh^ MMDMesh::Load1(const Platform::Array<byte>^ verticeData, const Platform::Array<int>^ indexData, int vertexStride, PrimitiveTopology pt)
{
	MMDMesh^ mmdMesh = ref new MMDMesh();
	mmdMesh->Reload1(verticeData, indexData, vertexStride, pt);
	return mmdMesh;
}

void MMDMesh::Reload1(const Platform::Array<byte>^ verticeData, const Platform::Array<int>^ indexData, int vertexStride, PrimitiveTopology pt)
{
	m_vertexStride = vertexStride;
	m_vertexCount = verticeData->Length / m_vertexStride;
	m_indexCount = indexData->Length;
	m_primitiveTopology = (D3D_PRIMITIVE_TOPOLOGY)pt;
	m_verticeData = verticeData;

	D3DCreateBlob(indexData->Length * sizeof(UINT), &m_indexData);
	memcpy(m_indexData->GetBufferPointer(), indexData->begin(), indexData->Length * sizeof(UINT));
}

struct OnlyPosition
{
	DirectX::XMFLOAT3 pos;
};
void MMDMesh::ReloadNDCQuad()
{
	OnlyPosition positions[]
	{
		XMFLOAT3(-1,-1,0),
		XMFLOAT3(-1, 1,0),
		XMFLOAT3(1,-1,0),
		XMFLOAT3(1, 1,0),
	};
	unsigned int indices[] =
	{
		0, 1, 2,
		2, 1, 3,
	};
	m_vertexStride = sizeof(OnlyPosition);
	m_vertexCount = _countof(positions);
	m_indexCount = _countof(indices);
	m_primitiveTopology = D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST;
	m_verticeData = ref new Platform::Array<byte, 1>(sizeof(positions));


	memcpy(m_verticeData->begin(), positions, sizeof(positions));
	D3DCreateBlob(sizeof(indices), &m_indexData);
	memcpy(m_indexData->GetBufferPointer(), indices, sizeof(indices));
}

void MMDMesh::ReloadCube()
{
	OnlyPosition positions[]
	{
		XMFLOAT3(-0.5f,-0.5f,-0.5f),
		XMFLOAT3(-0.5f,-0.5f,0.5f),
		XMFLOAT3(-0.5f,0.5f,-0.5f),
		XMFLOAT3(-0.5f,0.5f,0.5f),
		XMFLOAT3(0.5f,-0.5f,-0.5f),
		XMFLOAT3(0.5f,-0.5f,0.5f),
		XMFLOAT3(0.5f,0.5f,-0.5f),
		XMFLOAT3(0.5f,0.5f,0.5f),
	};
	unsigned int indices[] =
	{
		0, 2, 1,
		1, 2, 3,

		4, 5, 6,
		5, 7, 6,

		0, 1, 5,
		0, 5, 4,

		2, 6, 7,
		2, 7, 3,

		0, 4, 6,
		0, 6, 2,

		1, 3, 7,
		1, 7, 5,
	};
	m_vertexStride = sizeof(OnlyPosition);
	m_vertexCount = _countof(positions);
	m_indexCount = _countof(indices);
	m_primitiveTopology = D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST;
	m_verticeData = ref new Platform::Array<byte, 1>(sizeof(positions));


	memcpy(m_verticeData->begin(), positions, sizeof(positions));
	D3DCreateBlob(sizeof(indices), &m_indexData);
	memcpy(m_indexData->GetBufferPointer(), indices, sizeof(indices));
}

void MMDMesh::ReloadCubeWire()
{
	OnlyPosition positions[]
	{
		XMFLOAT3(-0.5f,-0.5f,-0.5f),
		XMFLOAT3(-0.5f,-0.5f,0.5f),
		XMFLOAT3(-0.5f,0.5f,-0.5f),
		XMFLOAT3(-0.5f,0.5f,0.5f),
		XMFLOAT3(0.5f,-0.5f,-0.5f),
		XMFLOAT3(0.5f,-0.5f,0.5f),
		XMFLOAT3(0.5f,0.5f,-0.5f),
		XMFLOAT3(0.5f,0.5f,0.5f),
	};
	unsigned int indices[] =
	{
		0, 1,
		2, 3,
		4, 5,
		6, 7,

		0, 2,
		1, 3,
		4, 6,
		5, 7,

		0, 4,
		1, 5,
		2, 6,
		3, 7,
	};
	m_vertexStride = sizeof(OnlyPosition);
	m_vertexCount = _countof(positions);
	m_indexCount = _countof(indices);
	m_primitiveTopology = D3D_PRIMITIVE_TOPOLOGY_LINELIST;
	m_verticeData = ref new Platform::Array<byte, 1>(sizeof(positions));


	memcpy(m_verticeData->begin(), positions, sizeof(positions));
	D3DCreateBlob(sizeof(indices), &m_indexData);
	memcpy(m_indexData->GetBufferPointer(), indices, sizeof(indices));
}

void MMDMesh::ReleaseUploadHeapResource()
{
	m_vertexBufferUpload.Reset();
	m_indexBufferUpload.Reset();
}

void MMDMesh::CopyPosData(Platform::WriteOnlyArray<Windows::Foundation::Numerics::float3>^ Target, const Platform::Array<byte>^ source)
{
	memcpy(Target->begin(), source->begin(), source->Length);
}

MMDMesh::~MMDMesh()
{
}
