#define _PMX_JOINWORLD_TPOSE_ONLY

using UnityEngine;
using System.Collections;
using BulletXNA;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.LinearMath;

using PMXShapeType          = MMD4MecanimBulletPMXCommon.PMXShapeType;
using PMXRigidBodyType      = MMD4MecanimBulletPMXCommon.PMXRigidBodyType;
using PMXBone               = MMD4MecanimBulletPMXBone;
using PMXModel              = MMD4MecanimBulletPMXModel;
using PMXCollider			= MMD4MecanimBulletPMXCollider;
using PMXRigidBody			= MMD4MecanimBulletPMXRigidBody;
using SimpleMotionState     = MMD4MecanimBulletPhysicsUtil.SimpleMotionState;
using KinematicMotionState  = MMD4MecanimBulletPhysicsUtil.KinematicMotionState;

using MMDModelProperty		= MMD4MecanimBulletPhysics.MMDModelProperty;
using MMDRigidBodyProperty	= MMD4MecanimBulletPhysics.MMDRigidBodyProperty;

public class MMD4MecanimBulletPMXRigidBody
{
	public PMXModel _model;
	public PMXBone _bone;
	
	public PMXModel model { get { return _model; } }
	public PMXBone bone { get { return _bone; } }
	
	// Pending: Replace to MMD4MecanimData

	int 				    _boneID = -1;
	uint	        	    _collisionGroupID;
	uint    	    	    _collisionMask;
	PMXShapeType	    	_shapeType;
	Vector3                 _shapeSize;
	Vector3                 _shapeSizeScaled;
	Vector3			        _position;
	Vector3		    	    _rotation;
	float			    	_mass;
	float				    _linearDamping;
	float				    _angularDamping;
	float				    _restitution;
	float				    _friction;
	PMXRigidBodyType	    _rigidBodyType = PMXRigidBodyType.Kinematics;
	uint		    	    _additionalFlags;

	MMDModelProperty		_modelProperty;
	MMDRigidBodyProperty	_rigidBodyProperty = new MMDRigidBodyProperty();

	CollisionShape	        _shape;
	IMotionState	    	_motionState;
	RigidBody	    	    _bulletRigidBody;

	bool					_isDisabled;
	bool				    _noBone;
	IndexedMatrix		   	_boneToBodyTransform = IndexedMatrix.Identity;
	IndexedMatrix		    _bodyToBoneTransform = IndexedMatrix.Identity;

	DiscreteDynamicsWorld   _bulletWorld;

	PMXCollider				_collider = new PMXCollider();
	IndexedMatrix			_transform = IndexedMatrix.Identity;
	bool					_isFeedbackTransform;
	bool					_isFeedbackBoneTransform;
	bool					_isTouchKinematic;

	public RigidBody bulletRigidBody { get { return _bulletRigidBody; } }
	public bool isDisabled { get { return _isDisabled; } }
	public bool isKinematic { get { return _rigidBodyType == PMXRigidBodyType.Kinematics; } }
	public bool isSimulated { get { return _rigidBodyType != PMXRigidBodyType.Kinematics; } }
	public PMXRigidBodyType rigidBodyType { get { return _rigidBodyType; } }
	public int parentBoneID { get { return (_bone != null) ? _bone.parentBoneID : -1; } }

	~MMD4MecanimBulletPMXRigidBody()
	{
		Destroy();
	}
	
	public void Destroy()
	{
		LeaveWorld();
		
		if( _bulletRigidBody != null ) {
			_bulletRigidBody.SetUserPointer( null );
			_bulletRigidBody.Cleanup();
			_bulletRigidBody = null;
		}
		_motionState = null;
		if( _shape != null ) {
			_shape.Cleanup();
			_shape = null;
		}
		
		_bulletWorld = null;
	}
	
	public bool Import( MMD4MecanimCommon.BinaryReader binaryReader )
	{
		if( !binaryReader.BeginStruct() ) {
			Debug.LogError("");
			return false;
		}

		if( _model != null ) {
			_modelProperty = _model.modelProperty;
		}
		if( _modelProperty == null ) {
			_modelProperty = new MMDModelProperty();
		}
		if( _rigidBodyProperty == null ) {
			_rigidBodyProperty = new MMDRigidBodyProperty();
		}

		_additionalFlags	= (uint)binaryReader.ReadStructInt();
		binaryReader.ReadStructInt(); // nameJp
		binaryReader.ReadStructInt(); // nameEn
		_boneID	        	= binaryReader.ReadStructInt();
		_collisionGroupID	= (uint)binaryReader.ReadStructInt();
		_collisionMask		= (uint)binaryReader.ReadStructInt();
		_shapeType			= (PMXShapeType)binaryReader.ReadStructInt();
		_rigidBodyType		= (PMXRigidBodyType)binaryReader.ReadStructInt();
		_shapeSize		    = binaryReader.ReadStructVector3();
		_position			= binaryReader.ReadStructVector3();
		_rotation			= binaryReader.ReadStructVector3();
		_mass				= binaryReader.ReadStructFloat();
		_linearDamping		= binaryReader.ReadStructFloat();
		_angularDamping		= binaryReader.ReadStructFloat();
		_restitution		= binaryReader.ReadStructFloat();
		_friction			= binaryReader.ReadStructFloat();

		_shapeSizeScaled	= _shapeSize;

		if( !binaryReader.EndStruct() ) {
			Debug.LogError("");
			return false;
		}

		_isDisabled			= (_additionalFlags & 0x01) != 0;

		if( _model != null ) {
			_shapeSize *= _model.modelToBulletScale;
			_position *= _model.modelToBulletScale;
		}

		// LH to RH
		_position.z = -_position.z;
		_rotation.x = -_rotation.x;
		_rotation.y = -_rotation.y;
		
		_boneToBodyTransform._basis = MMD4MecanimBulletPhysicsUtil.BasisRotationYXZ( ref _rotation );
		_boneToBodyTransform._origin = _position;
		_bodyToBoneTransform = _boneToBodyTransform.Inverse();

		_noBone = (_boneID < 0);
		if( _model != null ) {
			if( _noBone ) {
				_bone = _model.GetBone( 0 );
			} else {
				_bone = _model.GetBone( _boneID );
				if( _bone != null && _rigidBodyType != PMXRigidBodyType.Kinematics ) {
					_bone._rigidBody = this;
				}
			}
		}

		return true;
	}

	public void Config( MMDRigidBodyProperty rigidBodyProperty )
	{
		if( _rigidBodyProperty == null || rigidBodyProperty == null ) {
			return;
		}

		_rigidBodyProperty.Copy( rigidBodyProperty );
		_PostfixProperty();
	}

	public void SetEnabled( bool isEnabled )
	{
		_isDisabled = !isEnabled;
		if( _rigidBodyProperty != null ) {
			_rigidBodyProperty.isDisabled = isEnabled ? 0 : 1;
		}
	}

	public void FeedbackBoneToBodyTransform()
	{
		FeedbackBoneToBodyTransform( false );
	}

	public void FeedbackBoneToBodyTransform( bool forceOverwrite )
	{
		if( _bone == null || _noBone ) {
			return;
		}

		if( _rigidBodyType == PMXRigidBodyType.Kinematics ) {
			if( _motionState != null ) {
				((KinematicMotionState)_motionState).m_graphicsWorldTrans = _bone.worldTransform * _boneToBodyTransform;
			}
		} else if( forceOverwrite || _isDisabled ) {
			if( _bulletRigidBody != null ) {
				IndexedVector3 zeroVec = IndexedVector3.Zero;
				IndexedMatrix m = _bone.worldTransform * _boneToBodyTransform;
				_bulletRigidBody.SetLinearVelocity( ref zeroVec );
				_bulletRigidBody.SetAngularVelocity( ref zeroVec );
				_bulletRigidBody.ClearForces();
				_bulletRigidBody.SetCenterOfMassTransform( ref m );
			}
		}
	}

	public void ProcessPreBoneAlignment()
	{
		if( _bulletRigidBody == null || _bone == null || _noBone || _model == null || _modelProperty == null ) {
			return;
		}
		if( _rigidBodyType == PMXRigidBodyType.Kinematics || _isDisabled ) {
			return;
		}
		
		IndexedMatrix worldTransform = _bulletRigidBody.GetCenterOfMassTransform();
		IndexedMatrix boneTransform = worldTransform * _bodyToBoneTransform;

		if( _ProcessBoneAlignment(
			ref worldTransform,
			ref boneTransform,
			_modelProperty.rigidBodyPreBoneAlignmentLimitLength,
			_modelProperty.rigidBodyPreBoneAlignmentLossRate ) ) {
			_bulletRigidBody.SetCenterOfMassTransform( ref worldTransform );
		}
		
		_SetWorldTransformToBone( ref boneTransform );
	}

	public void ProcessPostBoneAlignment()
	{
		if( _bulletRigidBody == null || _bone == null || _noBone || _model == null || _modelProperty == null ) {
			return;
		}
		if( _rigidBodyType == PMXRigidBodyType.Kinematics || _isDisabled ) {
			return;
		}
		
		IndexedMatrix worldTransform = _bulletRigidBody.GetCenterOfMassTransform();
		IndexedMatrix boneTransform = worldTransform * _bodyToBoneTransform;

		if( _ProcessBoneAlignment(
			ref worldTransform,
			ref boneTransform,
			_modelProperty.rigidBodyPostBoneAlignmentLimitLength,
			_modelProperty.rigidBodyPostBoneAlignmentLossRate ) ) {
			_bulletRigidBody.SetCenterOfMassTransform( ref worldTransform );
		}

		_SetWorldTransformToBone( ref boneTransform );
	}

	public void PrepareTransform()
	{
		if( _bulletRigidBody != null ) {
			_transform = _bulletRigidBody.GetCenterOfMassTransform();
		}
	}
	
	public void ApplyTransformToBone( float deltaTime )
	{
		if( _rigidBodyType == PMXRigidBodyType.Kinematics || _bone == null || _noBone || _isDisabled || _model == null || _modelProperty == null ){
			return;
		}
		
		IndexedMatrix boneTransform = _bulletRigidBody.GetCenterOfMassTransform() * _bodyToBoneTransform;
		
		_ProcessVelocityLimit();
		
		if( _ProcessBoneAlignment( ref _transform, ref boneTransform,
		                          _modelProperty.rigidBodyPostBoneAlignmentLimitLength,
		                          _modelProperty.rigidBodyPostBoneAlignmentLossRate ) ) {
			_isFeedbackTransform = true;
		}

		if( !_model.optimizeBulletXNA ) {
			if( _ProcessForceLimitAngularVelocity( ref _transform, ref boneTransform, deltaTime ) ) {
				_isFeedbackTransform = true;
			}
		}
		
		_SetWorldTransformToBone( ref boneTransform );
	}

	public void PrepareCollider()
	{
		_isTouchKinematic		= false;
		_collider.transform		= _transform;
		_collider.shape			= (int)_shapeType;
		_collider.size			= _shapeSizeScaled;
		_collider.isKinematic	= _rigidBodyType == PMXRigidBodyType.Kinematics || _isDisabled;
		_collider.isCollision	= false;
	}

	public void ProcessCollider( MMD4MecanimBulletPMXRigidBody rigidBodyB )
	{
		if( _model == null || _modelProperty == null ) {
			return;
		}
		
		PMXRigidBody rigidBodyA = this;
		
		bool kinematicRigidBodyA = rigidBodyA._collider.isKinematic;
		bool kinematicRigidBodyB = rigidBodyB._collider.isKinematic;
		if( !kinematicRigidBodyA && kinematicRigidBodyB ) {
			rigidBodyA._isTouchKinematic = true;
		} else if( kinematicRigidBodyA && !kinematicRigidBodyB ) {
			rigidBodyB._isTouchKinematic = true;
		}
		
		if( MMD4MecanimBulletPhysicsUtil.FastCollide( rigidBodyA._collider, rigidBodyB._collider ) ) {
			if( rigidBodyA._collider.isCollision ) {
				rigidBodyA._transform = rigidBodyA._collider.transform;
				rigidBodyA._isFeedbackTransform = true;
				rigidBodyA._isFeedbackBoneTransform = true;
			}
			if( rigidBodyB._collider.isCollision ) {
				rigidBodyB._transform = rigidBodyB._collider.transform;
				rigidBodyB._isFeedbackTransform = true;
				rigidBodyB._isFeedbackBoneTransform = true;
			}
		}
	}

	public void FeedbackTransform()
	{
		if( _isFeedbackTransform ) {
			_isFeedbackTransform = false;
			if( _bulletRigidBody != null ) {
				_bulletRigidBody.SetCenterOfMassTransform( ref _transform );
			}
		}
		
		if( _isFeedbackBoneTransform ) {
			_isFeedbackBoneTransform = false;
			IndexedMatrix boneTransform = _transform * _bodyToBoneTransform;
			_SetWorldTransformToBone( ref boneTransform );
		}
	}

	public void AntiJitterTransform()
	{
		if( _bone == null || _noBone ) {
			return;
		}
		if( _rigidBodyType == PMXRigidBodyType.Kinematics ) {
			return;
		}
		if( _isDisabled ) {
			_bone.AntiJitterWorldTransformOnDisabled();
			return;
		}
		
		_bone.AntiJitterWorldTransform( _isTouchKinematic );
		_isTouchKinematic = false;
	}

	void _SetWorldTransformToBone( ref IndexedMatrix boneTransform )
	{
		if( _bone != null ) {
			_bone.worldTransform = boneTransform;
			_bone.NotifySetWorldTransform();
		}
	}

	static float _FastScl( float lhs, float rhs )
	{
		if( rhs == 1.0f ) {
			return lhs;
		} else if( lhs == 1.0f ) {
			return rhs;
		} else {
			return lhs * rhs;
		}
	}

	bool _SetupBody()
	{
		if( _bulletRigidBody != null ) {
			return true;
		}
		if( _bone == null ) {
			return false;
		}
		if( _modelProperty == null ) {
			return false;
		}

		_PostfixProperty();

		if( _shape == null ) {
			float shapeScale = -1.0f;
			if( _rigidBodyProperty != null ) {
				shapeScale = _rigidBodyProperty.shapeScale;
			}
			if( shapeScale < 0.0f ) {
				shapeScale = 1.0f;
			}

			_shapeSizeScaled = _shapeSize * shapeScale;

			if( _shapeType == PMXShapeType.Sphere ) {
				_shape = new SphereShape( _shapeSizeScaled.x );
			} else if( _shapeType == PMXShapeType.Box ) {
				_shape = new BoxShape( new IndexedVector3( _shapeSizeScaled ) );
			} else if( _shapeType == PMXShapeType.Capsule ) {
				_shape = new CapsuleShape( _shapeSizeScaled.x, _shapeSizeScaled.y );
			} else {
				return false;
			}
		}

		float mass = 0.0f;
		IndexedVector3 localInertia = IndexedVector3.Zero;
		if( _shape != null ) {
			if( _rigidBodyType != PMXRigidBodyType.Kinematics && _mass != 0.0f ) {
				mass = _FastScl( _mass, _modelProperty.rigidBodyMassRate );
				_shape.CalculateLocalInertia( mass, out localInertia );
			}
		}

#if _PMX_JOINWORLD_TPOSE_ONLY
		if( _model == null || _model.rootBone == null ) {
			return false;
		}
		IndexedMatrix startTransform = _boneToBodyTransform;
		startTransform._origin += _bone.baseOrigin + _model.rootBone.worldTransform._origin;
#else
		IndexedMatrix startTransform = _bone.worldTransform * _boneToBodyTransform;
#endif
		if( _rigidBodyType == PMXRigidBodyType.Kinematics ) {
			_motionState = new KinematicMotionState( ref startTransform );
		} else {
			_motionState = new SimpleMotionState( ref startTransform );
		}
		
		RigidBodyConstructionInfo rbInfo = new RigidBodyConstructionInfo( mass, _motionState, _shape, localInertia );
		rbInfo.m_linearDamping	    = _FastScl( _linearDamping,		_modelProperty.rigidBodyLinearDampingRate );
		rbInfo.m_angularDamping	    = _FastScl( _angularDamping,	_modelProperty.rigidBodyAngularDampingRate );
		rbInfo.m_restitution	    = _FastScl( _restitution,		_modelProperty.rigidBodyRestitutionRate );
		rbInfo.m_friction		    = _FastScl( _friction,			_modelProperty.rigidBodyFrictionRate );
		rbInfo.m_additionalDamping  = _modelProperty.rigidBodyIsAdditionalDamping;

		// for 2.79 to 2.75
		if( rbInfo.m_linearDamping < 1.0f ) {
			if( _modelProperty.rigidBodyLinearDampingLossRate > 0.0f ) {
				rbInfo.m_linearDamping *= Mathf.Max( 1.0f - _modelProperty.rigidBodyLinearDampingLossRate, 0.0f );
			}
		}
		if( rbInfo.m_angularDamping < 1.0f ) {
			if( _modelProperty.rigidBodyAngularDampingLossRate > 0.0f ) {
				rbInfo.m_angularDamping *= Mathf.Max( 1.0f - _modelProperty.rigidBodyAngularDampingLossRate, 0.0f );
			}
		}
		if( _modelProperty.rigidBodyLinearDampingLimit >= 0.0f ) {
			rbInfo.m_linearDamping = Mathf.Min( rbInfo.m_linearDamping, _modelProperty.rigidBodyLinearDampingLimit );
		}
		if( _modelProperty.rigidBodyAngularDampingLimit >= 0.0f ) {
			rbInfo.m_angularDamping = Mathf.Min( rbInfo.m_angularDamping, _modelProperty.rigidBodyAngularDampingLimit );
		}

		_bulletRigidBody = new RigidBody( rbInfo );
		if( _bulletRigidBody != null ) {
			_bulletRigidBody.SetUserPointer( this );

			if( _rigidBodyType == PMXRigidBodyType.Kinematics ) {
				_bulletRigidBody.SetCollisionFlags(_bulletRigidBody.GetCollisionFlags() | BulletXNA.BulletCollision.CollisionFlags.CF_KINEMATIC_OBJECT);
			}

			if( _model != null && !_model.optimizeBulletXNA ) {
				if( _modelProperty != null && _modelProperty.rigidBodyIsUseCcd ) {
					float ccdRadius = 0.0f;
					if( _shapeType == PMXShapeType.Sphere ) {
						ccdRadius = _shapeSizeScaled[0];
					} else if( _shapeType == PMXShapeType.Box ) {
						ccdRadius = Mathf.Min( Mathf.Min( _shapeSizeScaled[0], _shapeSizeScaled[1] ), _shapeSizeScaled[2] );
					} else if( _shapeType == PMXShapeType.Capsule ) {
						ccdRadius = _shapeSizeScaled[0];
					}
					
					float ccdThreshold = _modelProperty.rigidBodyCcdMotionThreshold * _model.unityToBulletScale;
					_bulletRigidBody.SetCcdMotionThreshold(ccdThreshold);
					_bulletRigidBody.SetCcdSweptSphereRadius(ccdRadius);
				}
			}

			if( _modelProperty != null && _modelProperty.rigidBodyIsEnableSleeping == false ) {
				_bulletRigidBody.SetSleepingThresholds( 0.0f, 0.0f );
			}

			_bulletRigidBody.SetActivationState(ActivationState.DISABLE_DEACTIVATION);
		}
		
		return true;
	}

	void _PostfixProperty()
	{
		if( _rigidBodyProperty != null && _modelProperty != null ) {
			if( _rigidBodyProperty.isDisabled >= 0 ) {
				_isDisabled = (_rigidBodyProperty.isDisabled != 0);
			}
			if( _rigidBodyProperty.shapeSize != Vector3.zero ) {
				_shapeSize = _rigidBodyProperty.shapeSize;
			}
			if( _rigidBodyProperty.isUseForceAngularVelocityLimit == -1 ) {
				_rigidBodyProperty.isUseForceAngularVelocityLimit = _modelProperty.rigidBodyIsUseForceAngularVelocityLimit ? 1 : 0;
			}
			if( _rigidBodyProperty.isUseForceAngularAccelerationLimit == -1 ) {
				_rigidBodyProperty.isUseForceAngularAccelerationLimit = _modelProperty.rigidBodyIsUseForceAngularAccelerationLimit ? 1 : 0;
			}
			if( _rigidBodyProperty.forceAngularVelocityLimit < 0.0f ) {
				_rigidBodyProperty.forceAngularVelocityLimit = _modelProperty.rigidBodyForceAngularVelocityLimit;
			}
			if( _rigidBodyProperty.linearVelocityLimit < 0.0f ) {
				_rigidBodyProperty.linearVelocityLimit = _modelProperty.rigidBodyLinearVelocityLimit;
			}
			if( _rigidBodyProperty.angularVelocityLimit < 0.0f ) {
				_rigidBodyProperty.angularVelocityLimit = _modelProperty.rigidBodyAngularVelocityLimit;
			}
			if( _rigidBodyProperty.shapeScale < 0.0f ) {
				_rigidBodyProperty.shapeScale = _modelProperty.rigidBodyShapeScale;
			}
		}
	}

	public bool JoinWorld()
	{
		if( _bulletRigidBody == null ) {
			if( !_SetupBody() ) {
				Debug.LogError( "Warning: PMXRigidBody::JoinWorld(): Body is nothing." );
				return false;
			}
		}
		if( _bulletRigidBody == null || _bulletWorld != null || _model == null || _model.bulletWorld == null ) {
			Debug.LogError( "Warning: PMXRigidBody::JoinWorld(): Nothing." );
			return false;
		}

		int groupID = (int)(1 << (int)_collisionGroupID);
		int groupMask = (int)_collisionMask;

		_bulletWorld = _model.bulletWorld;
		_bulletWorld.AddRigidBody( _bulletRigidBody,
		                          (BulletXNA.BulletCollision.CollisionFilterGroups)groupID,
		                          (BulletXNA.BulletCollision.CollisionFilterGroups)groupMask );
		return true;
	}
	
	public void LeaveWorld()
	{
		if( _bulletRigidBody != null ) {
			if( _bulletWorld != null ) {
				_bulletWorld.RemoveRigidBody( _bulletRigidBody );
			}
			_bulletRigidBody.Cleanup();
			_bulletRigidBody = null;
		}
		
		_bulletWorld = null;
	}

	void _ProcessVelocityLimit()
	{
		if( _bulletRigidBody == null || _rigidBodyProperty == null || _model == null ) {
			return;
		}
		
		if( _rigidBodyProperty.linearVelocityLimit >= 0.0f ) {
			float limitValue = _rigidBodyProperty.linearVelocityLimit * _model.unityToBulletScale;
			IndexedVector3 velocity = _bulletRigidBody.GetLinearVelocity();
			for( int i = 0; i < 3; ++i ) {
				velocity[i] = Mathf.Clamp( velocity[i], -limitValue, limitValue );
			}
			_bulletRigidBody.SetLinearVelocity( ref velocity );
		}
		if( _rigidBodyProperty.angularVelocityLimit >= 0.0f ) {
			float limitValue = _rigidBodyProperty.angularVelocityLimit * Mathf.Deg2Rad;
			IndexedVector3 velocity = _bulletRigidBody.GetAngularVelocity();
			for( int i = 0; i < 3; ++i ) {
				velocity[i] = Mathf.Clamp( velocity[i], -limitValue, limitValue );
			}
			_bulletRigidBody.SetAngularVelocity( ref velocity );
		}
	}

	bool _ProcessBoneAlignment(
		ref IndexedMatrix transform,
		ref IndexedMatrix boneTransform,
		float limitLength,
		float lossRate )
	{
		if( _model == null || _modelProperty == null || _bone == null ) {
			return false;
		}
		
		float lossLength = Mathf.Clamp01( 1.0f - lossRate );
		limitLength *= model.unityToBulletScale;
	
		if( _bone.parentBone != null ) {
			Vector3 position = boneTransform._origin;
			Vector3 parentPosition = _bone.parentBone.worldTransform._origin;
			Vector3 translate = position - parentPosition;
			Vector3 offset = _bone.parentBone.worldTransform._basis.Transpose() * translate;
			Vector3 tempPos = offset - _bone.offset;
			float tempLength = tempPos.magnitude; // pending: optimized
			
			if( tempLength > limitLength ) { // pending: optimized
				Vector3 tempPos2 = tempPos * (limitLength / tempLength) * lossLength;
				Vector3 offset2 = tempPos2 + _bone.offset;
				Vector3 translate2 = _bone.parentBone.worldTransform._basis * offset2;
				Vector3 position2 = translate2 + parentPosition;
				boneTransform._origin = position2;
				transform._origin = transform._origin + (position2 - position);
				return true;
			}
		}
		
		return false;
	}
	
	bool _ProcessForceLimitAngularVelocity(
		ref IndexedMatrix transform,
		ref IndexedMatrix boneTransform,
		float deltaTime )
	{
		if( _model == null || _modelProperty == null || _bone == null || _rigidBodyProperty == null ) {
			return false;
		}

		if( _rigidBodyProperty.isUseForceAngularVelocityLimit != 0 && deltaTime > 0.0f && _bone.parentBone != null ) {
			float maxAnglePerSecond = _rigidBodyProperty.forceAngularVelocityLimit * Mathf.Deg2Rad;
			float maxAnglePerFrame = maxAnglePerSecond * deltaTime;
			float maxThetaPerFrame = Mathf.Cos( maxAnglePerFrame );
			float maxThetaPerFrame2 = maxThetaPerFrame * maxThetaPerFrame;
			
			float xDMin = maxThetaPerFrame;
			float zDMin = maxThetaPerFrame;
			float xDMin2 = maxThetaPerFrame2;
			float zDMin2 = maxThetaPerFrame2;

			if( _bone.isSetPrevWorldTransform ) {
				bool limitedAnything = false;
				IndexedVector3 xPrev = _bone.prevWorldTransform._basis.GetColumn(0);
				IndexedVector3 zPrev = _bone.prevWorldTransform._basis.GetColumn(2);
				
				IndexedVector3 xVec = boneTransform._basis.GetColumn(0);
				IndexedVector3 zVec = boneTransform._basis.GetColumn(2);
				float xD = xVec.Dot( xPrev );
				float zD = zVec.Dot( zPrev );
				
				if( _rigidBodyProperty.isUseForceAngularAccelerationLimit != 0 && _bone.isSetPrevWorldTransform2 ) {
					IndexedVector3 xPrev2 = _bone.prevWorldTransform2._basis.GetColumn(0);
					IndexedVector3 zPrev2 = _bone.prevWorldTransform2._basis.GetColumn(2);
					IndexedVector3 xAcc = MMD4MecanimBulletPhysicsUtil.GetAngAccVector( xPrev, xPrev2 );
					IndexedVector3 zAcc = MMD4MecanimBulletPhysicsUtil.GetAngAccVector( zPrev, zPrev2 );
					float xAccD = xAcc.Dot( xPrev );
					if( xDMin > xAccD ) {
						xDMin = xAccD;
						if( xDMin < 0 ) xDMin = 0;
						xDMin2 = xDMin * xDMin;
					}
					float zAccD = zAcc.Dot( zPrev );
					if( zDMin > zAccD ) {
						zDMin = zAccD;
						if( zDMin < 0 ) zDMin = 0;
						zDMin2 = zDMin * zDMin;
					}
				}
				
				if( xD < xDMin ) {
					limitedAnything = true;
					xVec = MMD4MecanimBulletPhysicsUtil.ClampDirection(
						_bone.prevWorldTransform._basis.GetColumn(0),
						boneTransform._basis.GetColumn(0),
						xD, xDMin, xDMin2 );
				}
				if( zD < zDMin ) {
					limitedAnything = true;
					zVec = MMD4MecanimBulletPhysicsUtil.ClampDirection(
						_bone.prevWorldTransform._basis.GetColumn(2),
						boneTransform._basis.GetColumn(2),
						zD, zDMin, zDMin2 );
				}
				if( limitedAnything ) {
					IndexedVector3 yVec = zVec.Cross( xVec );
					float yLen = yVec.Length();
					if( yLen >= 0.01f ) {
						yVec *= 1.0f / yLen;
						zVec = xVec.Cross( yVec );

						for( int i = 0; i < 3; ++i ) {
							boneTransform._basis[i] = new IndexedVector3( xVec[i], yVec[i], zVec[i] );
						}
						transform._basis = boneTransform._basis * _boneToBodyTransform._basis;
						return true;
					} else {
						boneTransform._basis = _bone.prevWorldTransform._basis;
						transform._basis = boneTransform._basis * _boneToBodyTransform._basis;
						return true;
					}
				}
			}
		}

		return false;
	}
}
