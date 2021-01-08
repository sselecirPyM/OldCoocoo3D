#include "pch.h"
#include "BulletAPI.h"
using namespace Coocoo3DPhysics;

void BulletAPI::SetAPIUsed(Physics3D^ physics3D)
{
	physics3D->m_sdkRef = std::make_shared<Bullet>();
}
