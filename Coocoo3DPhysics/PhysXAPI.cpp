#include "pch.h"
#include "PhysXAPI.h"
#include "PhysX/PhysX.h"
using namespace Coocoo3DPhysics;

void PhysXAPI::SetAPIUsed(Physics3D^ physics3D)
{
#ifdef USE_PHYSX
	physics3D->m_sdkRef = std::make_shared<PhysX>();
#else
	throw ref new Platform::NotImplementedException("For use PhysX the coocoo3DPhysics must compiled with define 'USE_PHYSX'");
#endif
}
