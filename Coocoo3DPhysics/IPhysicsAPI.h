#pragma once
namespace Coocoo3DPhysics
{
	using namespace Windows::Foundation::Numerics;
	class IPhysicsAPI
	{
	public:
		virtual void Init() = 0;
		virtual void InitScene(void* _scene) = 0;
		virtual void SceneAddRigidBody(void* _scene, void* _rigidBody, float3 position, quaternion rotation, float3 scale, float mass, float restitution, float friction, float movDampling, float rotDampling, UINT8 shape, UINT8 type, UINT8 collisionGroup, UINT collisionMask) = 0;
		virtual void SceneAddJoint(void* _scene, void* _joint, float3 position, quaternion rotation, void* _rigidBody1, void* _rigidBody2,
			float3 PositionMinimum,
			float3 PositionMaximum,
			float3 RotationMinimum,
			float3 RotationMaximum,
			float3 PositionSpring,
			float3 RotationSpring) = 0;
		virtual void SceneResetRigidBody(void* _scene, void* _rigidBody, float3 position, quaternion rotation) = 0;
		virtual void SceneSimulate(void* _scene, double time) = 0;
		virtual void SceneFetchResults(void* _scene) = 0;
		virtual void SceneMoveRigidBody(void* _scene, void* _rigidBody, float3 position, quaternion rotation) = 0;
		virtual void SceneMoveRigidBody(void* _scene, void* _rigidBody, float4x4 matrix) = 0;
		virtual float3 SceneGetRigidBodyPosition(void* _scene, void* _rigidBody) = 0;
		virtual quaternion SceneGetRigidBodyRotation(void* _scene, void* _rigidBody) = 0;
		virtual float4x4 SceneGetRigidBodyTransform(void* _scene, void* _rigidBody) = 0;
		virtual void SceneSetGravitation(void* _scene, float3 gravitation) = 0;
		virtual void SceneRemoveRigidBody(void* _scene, void* _rigidBody) = 0;
		virtual void SceneRemoveJoint(void* _scene, void* _joint) = 0;
	};
}