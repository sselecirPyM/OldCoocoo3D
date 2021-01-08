#include "pch.h"
#include "Texture2D.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;

void Texture2D::Reload(Texture2D^ texture)
{
	m_width = texture->m_width;
	m_height = texture->m_height;
	m_texture = texture->m_texture;
	m_heapRefIndex = texture->m_heapRefIndex;
	m_mipLevels = texture->m_mipLevels;
	Status = texture->Status;
}

void Texture2D::Unload()
{
	m_width = 0;
	m_height = 0;
	m_texture.Reset();
	Status = GraphicsObjectStatus::unload;
}
