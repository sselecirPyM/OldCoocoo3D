#include "pch.h"
#include "MMDMeshAppend.h"
using namespace Coocoo3DGraphics;

void MMDMeshAppend::Reload(int count)
{
	m_posCount = count;
	m_bufferSize = (count * c_vertexStride + 255) & ~255;
}
