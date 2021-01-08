#pragma once
#include "PhysX/PhysXRigidBody.h"
#include "PhysX/PhysXScene.h"
#include "PhysX/PhysXJoint.h"
#include "Bullet/BulletRigidBody.h"
#include "Bullet/BulletScene.h"
#include "Bullet/BulletJoint.h"

const UINT MAX_UNION_SCENE_STRUCTURE_SIZE = max(sizeof(Coocoo3DNative::PhysXScene),sizeof(Coocoo3DNative::BulletScene));
const UINT MAX_UNION_RIGID_BODY_STRUCTURE_SIZE = max(sizeof(Coocoo3DNative::PhysXRigidBody),sizeof(Coocoo3DNative::BulletRigidBody));
const UINT MAX_UNION_JOINT_STRUCTURE_SIZE = max(sizeof(Coocoo3DNative::PhysXJoint),sizeof(Coocoo3DNative::BulletJoint));