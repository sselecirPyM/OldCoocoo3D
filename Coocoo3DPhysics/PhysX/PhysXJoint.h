#pragma once

#ifdef USE_PHYSX
#include "PxPhysicsAPI.h"
namespace Coocoo3DNative
{
	class PhysXJoint
	{
	public:
		physx::PxJoint* m_joint;
	};
}
#else

namespace Coocoo3DNative
{
	class PhysXJoint
	{
	};
}
#endif