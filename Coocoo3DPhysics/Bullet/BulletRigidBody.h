#pragma once
#include "btBulletDynamicsCommon.h"
namespace Coocoo3DNative
{
	using namespace Windows::Foundation::Numerics;
	class BulletRigidBody
	{
	public:
		btRigidBody* m_rigidBody = nullptr;
		btCollisionShape* m_collisionShape = nullptr;

		float3 m_position;
		quaternion m_rotation;
		float m_mass;
		UINT8 m_shape;
		UINT8 m_collisionGroup;
		UINT16 m_collisionMask;
		float3 m_scale;
		float m_movDampling;
		float m_rotDampling;
		float m_restitution;
		float m_friction;
		UINT m_type;
	};
}