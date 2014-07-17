using UnityEngine;
using System.Collections;
using BulletXNA;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.LinearMath;

using PMXFileType       = MMD4MecanimBulletPMXCommon.PMXFileType;
using PMXRigidBodyType  = MMD4MecanimBulletPMXCommon.PMXRigidBodyType;
using PMXBone           = MMD4MecanimBulletPMXBone;
using PMXRigidBody      = MMD4MecanimBulletPMXRigidBody;
using PMXJoint          = MMD4MecanimBulletPMXJoint;

using MMDModelProperty	= MMD4MecanimBulletPhysics.MMDModelProperty;

// Pending: Support MultiThreading for every models.
// Pending: Support physics reset command.

public class MMD4MecanimBulletPMXModel : MMD4MecanimBulletPhysicsEntity
{
	// Pending: Replace to MMD4MecanimData

	PMXFileType                 _fileType;
	float                       _unityScale;
	float                       _modelToUnityScale;
	float                       _modelToBulletScale;
	float                       _bulletToUnityScale;
	float                       _unityToBulletScale;
	bool						_optimizeBulletXNA = true;
	float						_resetWaitTime;
	float						_resetMorphTime;
	PMXBone                     _rootBone;
	PMXBone[]                   _boneList;
	PMXRigidBody[]	            _rigidBodyList;
	PMXJoint[]       			_jointList;

	PMXRigidBody[]				_simulatedRigidBodyList;

    bool                        _isJoinedWorld;
	bool					    _needResetKinematic;
	bool					    _processResetWorld;
	float					    _processResetRatio;

	MMDModelProperty			_modelProperty;

    public bool isJoinedWorld { get { return _isJoinedWorld; } }
    public PMXFileType fileType { get { return _fileType; } }
    public float modelToBulletScale { get { return _modelToBulletScale; } }
	public float bulletToUnityScale { get { return _bulletToUnityScale; } }
	public float unityToBulletScale { get { return _unityToBulletScale; } }
    public PMXBone rootBone { get { return _rootBone; } }
	public MMDModelProperty modelProperty { get { return _modelProperty; } }
	public bool optimizeBulletXNA { get { return _optimizeBulletXNA; } }

    ~MMD4MecanimBulletPMXModel()
    {
        Destroy();
    }

    public void Destroy()
    {
        if( _jointList != null ) {
            for( int i = 0; i < _jointList.Length; ++i ) {
                _jointList[i].Destroy();
            }
        }
        if( _rigidBodyList != null ) {
            for( int i = 0; i < _rigidBodyList.Length; ++i ) {
                _rigidBodyList[i].Destroy();
            }
        }
        if( _boneList != null ) {
            for( int i = 0; i < _boneList.Length; ++i ) {
                _boneList[i].Destroy();
            }
        }

		_simulatedRigidBodyList = null;

        _jointList = null;
        _rigidBodyList = null;
        _boneList = null;
        _rootBone = null;
    }

    public struct ImportProperty
    {
        public float unityScale;
		public bool useCustomResetTime;
		public float resetWaitTime;
		public float resetMorphTime;
		public bool optimizeBulletXNA;
		public MMD4MecanimBulletPhysics.MMDModelProperty mmdModelProperty;
    }

    public bool Import( MMD4MecanimCommon.BinaryReader binaryReader, ref ImportProperty importProperty )
    {
        uint fourCC = (uint)binaryReader.GetFourCC();
        if( fourCC != MMD4MecanimCommon.BinaryReader.MakeFourCC("MDL1") ) {
            char cc0 = (char)fourCC;
            char cc1 = (char)((fourCC >> 8) & 0xff);
            char cc2 = (char)((fourCC >> 16) & 0xff);
            char cc3 = (char)((fourCC >> 24) & 0xff);
            Debug.LogError( "Not supported file. " + cc0 + cc1 + cc2 + cc3 );
            return false;
        }

		_modelProperty = importProperty.mmdModelProperty;
		if( _modelProperty == null ) {
			_modelProperty = new MMDModelProperty();
		}

		if( _modelProperty.rigidBodyLinearVelocityLimit < 0.0f ) {
			_modelProperty.rigidBodyLinearVelocityLimit = 10.0f;
		}
		// Like as 2.75
		if( _modelProperty.rigidBodyLinearDampingLossRate < 0.0f ) {
			_modelProperty.rigidBodyLinearDampingLossRate = 0.05f;
		}
		if( _modelProperty.rigidBodyAngularDampingLossRate < 0.0f ) {
			_modelProperty.rigidBodyAngularDampingLossRate = 0.05f;
		}

		_optimizeBulletXNA = importProperty.optimizeBulletXNA;

        float unityScale = importProperty.unityScale;

		_resetMorphTime = 1.8f;
		_resetWaitTime = 1.2f;
		if( importProperty.useCustomResetTime ) {
			_resetMorphTime = importProperty.resetMorphTime;
			_resetWaitTime = importProperty.resetWaitTime;
		}

        binaryReader.BeginHeader();
	    _fileType = (PMXFileType)binaryReader.ReadHeaderInt();
	    binaryReader.ReadHeaderFloat(); // fileVersion;
	    binaryReader.ReadHeaderInt(); // fileVersion(BIN)
	    binaryReader.ReadHeaderInt(); // additionalFlags
	    binaryReader.ReadHeaderInt(); // vertexCount
	    binaryReader.ReadHeaderInt(); // indexCount
	    float vertexScale = binaryReader.ReadHeaderFloat();
	    float importScale = binaryReader.ReadHeaderFloat();
	    _modelToUnityScale = vertexScale * importScale; /* Unity < Mesh Scale. */
	    binaryReader.EndHeader();

	    _modelToBulletScale = 1.0f;
	    _bulletToUnityScale = 1.0f;
	    _unityToBulletScale = 1.0f;

	    if( unityScale > Mathf.Epsilon ) {
		    _bulletToUnityScale = unityScale;
	    } else {
		    _bulletToUnityScale = _modelToUnityScale;
	    }

	    if( _bulletToUnityScale > Mathf.Epsilon ) {
		    _unityToBulletScale = 1.0f / _bulletToUnityScale;
	    }

	    _modelToBulletScale = _unityToBulletScale * _modelToUnityScale;

        int fourCC_Bone = MMD4MecanimCommon.BinaryReader.MakeFourCC("BONE");
        int fourCC_IK = MMD4MecanimCommon.BinaryReader.MakeFourCC("IK__");
        int fourCC_RigidBody = MMD4MecanimCommon.BinaryReader.MakeFourCC("RGBD");
        int fourCC_Joint = MMD4MecanimCommon.BinaryReader.MakeFourCC("JOIN");

	    int structListLength = binaryReader.structListLength;
	    for( int structListIndex = 0; structListIndex < structListLength; ++structListIndex ) {
            if( !binaryReader.BeginStructList() ) {
			    Debug.LogError( "BeginStructList() failed." );
                return false;
            }

            int structFourCC = binaryReader.currentStructFourCC;
            if( structFourCC == fourCC_Bone ) {
                _boneList = new PMXBone[binaryReader.currentStructLength];
			    for( int structIndex = 0; structIndex < binaryReader.currentStructLength; ++structIndex ) {
				    PMXBone pmxBone = new PMXBone();
				    _boneList[structIndex] = pmxBone;
				    pmxBone._model = this;
				    if( !pmxBone.Import( structIndex, binaryReader ) ) {
					    Debug.LogError( "PMXBone parse error." );
                        _boneList = null;
					    return false;
				    }
				    if( pmxBone.isRootBone ) {
					    _rootBone = pmxBone;
				    }
			    }
                if( _rootBone == null && _boneList.Length > 0 ) {
                    _rootBone = _boneList[0];
                    _rootBone.isRootBone = true;
                }
				for( int i = 0; i < _boneList.Length; ++i ) {
					_boneList[i].PostfixImport();
				}
            } else if( structFourCC == fourCC_IK ) {
                // Nothing.
            } else if( structFourCC == fourCC_RigidBody ) {
			    _rigidBodyList = new PMXRigidBody[binaryReader.currentStructLength];
			    for( int structIndex = 0; structIndex < binaryReader.currentStructLength; ++structIndex ) {
				    PMXRigidBody pmxRigidBody = new PMXRigidBody();
				    _rigidBodyList[structIndex] = pmxRigidBody;
				    pmxRigidBody._model = this;
					if( !pmxRigidBody.Import( binaryReader ) ) {
					    Debug.LogError( "PMXRigidBody parse error." );
                        _rigidBodyList = null;
					    return false;
				    }
			    }
            } else if( structFourCC == fourCC_Joint ) {
			    _jointList = new PMXJoint[binaryReader.currentStructLength];
			    for( int structIndex = 0; structIndex < binaryReader.currentStructLength; ++structIndex ) {
				    PMXJoint pmxJoint = new PMXJoint();
				    _jointList[structIndex] = pmxJoint;
				    pmxJoint._model = this;
				    if( !pmxJoint.Import( binaryReader ) ) {
					    Debug.LogError( "PMXJoint parse error." );
                        _jointList = null;
					    return false;
				    }
			    }
            }

            if( !binaryReader.EndStructList() ) {
			    Debug.LogError( "EndStructList() failed." );
			    return false;
		    }
        }

		_MakeSimulatedRigidBodyList();

	    _needResetKinematic = true;
        //Debug.Log( "MMD4MecanimBulletPMXModel::Import: Success" );
        return true;
    }

	private static bool _IsParentBone( PMXBone targetBone, PMXBone bone )
	{
		for( ; bone != null; bone = bone.parentBone ) {
			if( bone == targetBone ) {
				return true;
			}
		}
		return false;
	}

	private static void _Swap( ref PMXRigidBody lhs, ref PMXRigidBody rhs )
	{
		PMXRigidBody tmp = lhs;
		lhs = rhs;
		rhs = tmp;
	}

	public void SetRigidBodyEnabled( int rigidBodyID, bool isEnabled )
	{
		unchecked {
			if( _rigidBodyList != null && (uint)rigidBodyID < (uint)_rigidBodyList.Length ) {
				_rigidBodyList[rigidBodyID].SetEnabled( isEnabled );
			}
		}
	}

    public void Update( int[] iValues, float[] fValues )
    {
		if( _boneList == null ) {
			Debug.LogError("");
			return;
		}
		if( iValues == null || iValues.Length < _boneList.Length ) {
			Debug.LogError( "Missing arguments. _boneList.Length " + _boneList.Length );
		    return;
	    }
		if( fValues == null || fValues.Length < _boneList.Length * 12 ) {
			Debug.LogError( "Missing arguments. _boneList.Length " + _boneList.Length );
		    return;
	    }

	    //Debug.Log( "PMXModel::Update" );

        int f = 0;
		for( int i = 0; i < _boneList.Length; ++i, f += 12 ) {
		    if( iValues[i] != 0 ) {
				PMXBone bone = _boneList[i];
				if( _needResetKinematic ) {
					bone.moveWorldTransform._basis.SetValue(
						fValues[f + 0], fValues[f + 3], fValues[f + 6],
						fValues[f + 1], fValues[f + 4], fValues[f + 7],
						fValues[f + 2], fValues[f + 5], fValues[f + 8] );
					
					bone.moveWorldTransform._origin = new IndexedVector3( fValues[f + 9], fValues[f + 10], fValues[f + 11] ) * _unityToBulletScale;
					bone.NotifySetMoveWorldTransform();
				} else {
					bone.worldTransform._basis.SetValue(
						fValues[f + 0], fValues[f + 3], fValues[f + 6],
						fValues[f + 1], fValues[f + 4], fValues[f + 7],
						fValues[f + 2], fValues[f + 5], fValues[f + 8] );
					
					bone.worldTransform._origin = new IndexedVector3( fValues[f + 9], fValues[f + 10], fValues[f + 11] ) * _unityToBulletScale;
					bone.NotifySetWorldTransform();
				}
		    }
	    }

        if( _needResetKinematic ) {
			_PrepareMoveWorldTransform();
        }
    }

    public void LateUpdate( int[] iValues, float[] fValues )
    {
		if( _boneList == null ) {
			Debug.LogError("");
			return;
		}
		if( iValues == null || iValues.Length < _boneList.Length ) {
			Debug.LogError( "Missing arguments. _boneList.Length " + _boneList.Length );
		    return;
	    }
		if( fValues == null || fValues.Length < _boneList.Length * 8 ) {
			Debug.LogError( "Missing arguments. _boneList.Length " + _boneList.Length );
		    return;
	    }

		bool rigidBodyIsForceTranslate = (_modelProperty != null && _modelProperty.rigidBodyIsForceTranslate);

        int f = 0;
		for( int i = 0; i < _boneList.Length; ++i, f += 8 ) {
			PMXBone bone = _boneList[i];
            PMXBone parentBone = bone.parentBone;
            //PMXRigidBodyType rigidBodyType = bone.rigidBodyType;
            if( parentBone != null && bone.isRigidBodySimulated && !bone.isRigidBodyDisabled ) { // Exclude root bone.
                parentBone.PrecheckInverseWorldBasisTransform();

                IndexedVector3 translate;
				if( !rigidBodyIsForceTranslate && !bone.isBoneTranslate ) {
					translate = bone.offsetUnityScale;
                } else { // Simulated
                    Vector3 position = bone.worldTransform._origin;
                    Vector3 parentPosition = parentBone.worldTransform._origin;
                    translate = position - parentPosition;
                    translate = parentBone.invserseWorldBasis * translate;
                    translate *= _bulletToUnityScale;
                }

                IndexedBasisMatrix transform = parentBone.invserseWorldBasis * bone.worldTransform._basis;
                IndexedQuaternion rotation = transform.GetRotation();

                fValues[f + 0] = translate.X;
                fValues[f + 1] = translate.Y;
                fValues[f + 2] = translate.Z;
                fValues[f + 3] = 1.0f;

                fValues[f + 4] = rotation.X;
                fValues[f + 5] = rotation.Y;
                fValues[f + 6] = rotation.Z;
                fValues[f + 7] = rotation.W;
            } else {
                fValues[f + 0] = 0.0f;
                fValues[f + 1] = 0.0f;
                fValues[f + 2] = 0.0f;
                fValues[f + 3] = 1.0f;

                fValues[f + 4] = 0.0f;
                fValues[f + 5] = 0.0f;
                fValues[f + 6] = 0.0f;
                fValues[f + 7] = 1.0f;
            }
        }
    }

    public void CleanupBoneTransform()
    {
        if( _boneList != null ) {
			for( int i = 0; i < _boneList.Length; ++i ) {
				_boneList[i].CleanupUpdatedWorldTransform();
            }
        }
    }

    public PMXBone GetBone( int boneID )
    {
        if( _boneList != null && (uint)boneID < (uint)_boneList.Length ) {
            return _boneList[boneID];
        }

        return null;
    }

    public PMXRigidBody GetRigidBody( int rigidBodyID )
    {
        if( _rigidBodyList != null && (uint)rigidBodyID < (uint)_rigidBodyList.Length ) {
            return _rigidBodyList[rigidBodyID];
        }

        return null;
    }

	// from MMD4MecanimBulletPhysicsWorld
	public override bool _JoinWorld()
	{
        return true;
	}

	// from MMD4MecanimBulletPhysicsWorld
	public override void _LeaveWorld()
	{
        if( _jointList != null ) {
	        for( int i = 0; i < _jointList.Length; ++i ) {
                _jointList[i].LeaveWorld();
	        }
        }
        if( _rigidBodyList != null ) {
	        for( int i = 0; i < _rigidBodyList.Length; ++i ) {
                _rigidBodyList[i].LeaveWorld();
            }
        }
	}

	// from MMD4MecanimBulletPhysicsWorld
	public override void _PreUpdateWorld( float deltaTime )
	{
		_ProcessJoinWorld();

		if( _rigidBodyList != null ) {
			for( int i = 0; i < _rigidBodyList.Length; ++i ) {
				_rigidBodyList[i].FeedbackBoneToBodyTransform();
			}
		}
		
		if( _simulatedRigidBodyList != null ) {
			for( int i = 0; i < _simulatedRigidBodyList.Length; ++i ) {
				_simulatedRigidBodyList[i].ProcessPreBoneAlignment();
			}
		}
	}

	// from MMD4MecanimBulletPhysicsWorld
	public override void _PostUpdateWorld( float deltaTime )
	{
		if( deltaTime > 0.0f ) {
			if( _rigidBodyList != null ) {
				for( int i = 0; i < _rigidBodyList.Length; ++i ) {
					_rigidBodyList[i].PrepareTransform();
				}
			}
			if( _simulatedRigidBodyList != null ) {
				for( int i = 0; i < _simulatedRigidBodyList.Length; ++i ) {
					_simulatedRigidBodyList[i].ApplyTransformToBone( deltaTime );
				}
			}

			if( !_optimizeBulletXNA ) {
				if( _modelProperty != null && _modelProperty.rigidBodyIsAdditionalCollider ) {
					DiscreteDynamicsWorld bulletWorld = this.bulletWorld;
					if( bulletWorld != null && bulletWorld.GetDispatcher() != null ) {
						IDispatcher dispatcher = bulletWorld.GetDispatcher();
						int manifolds = dispatcher.GetNumManifolds();
						for( int i = 0; i < manifolds; ++i ) {
							var manifold = dispatcher.GetManifoldByIndexInternal( i );
							RigidBody rigidBody0 = manifold.GetBody0() as RigidBody;
							RigidBody rigidBody1 = manifold.GetBody1() as RigidBody;
							if( rigidBody0 != null && rigidBody1 != null ) {
								MMD4MecanimBulletPMXRigidBody pmxRigidBody0 = rigidBody0.GetUserPointer() as MMD4MecanimBulletPMXRigidBody;
								MMD4MecanimBulletPMXRigidBody pmxRigidBody1 = rigidBody1.GetUserPointer() as MMD4MecanimBulletPMXRigidBody;
								if( pmxRigidBody0 != null && pmxRigidBody1 != null ) {
									pmxRigidBody0.ProcessCollider( pmxRigidBody1 );
								}
							}
						}
					}
				}

				if( _simulatedRigidBodyList != null ) {
					for( int i = 0; i < _simulatedRigidBodyList.Length; ++i ) {
						_simulatedRigidBodyList[i].FeedbackTransform();
					}
					for( int i = 0; i < _simulatedRigidBodyList.Length; ++i ) {
						_simulatedRigidBodyList[i].AntiJitterTransform();
					}
				}
			}
		}

        CleanupBoneTransform();

        _needResetKinematic = false;
	}

	// from MMD4MecanimBulletPhysicsWorld
	public override void _NoUpdateWorld()
	{
		_ProcessJoinWorld();

		if( _rigidBodyList != null ) {
			for( int i = 0; i < _rigidBodyList.Length; ++i ) {
				_rigidBodyList[i].FeedbackBoneToBodyTransform();
			}
		}

		if( _simulatedRigidBodyList != null ) {
			for( int i = 0; i < _simulatedRigidBodyList.Length; ++i ) {
				_simulatedRigidBodyList[i].ProcessPostBoneAlignment();
			}
		}
	}

	// from MMD4MecanimBulletPhysicsWorld
	public override float _GetResetWorldTime()
	{
	    if( !_needResetKinematic ) {
		    return 0.0f;
	    }

		return _resetMorphTime + _resetWaitTime;
	}

	// from MMD4MecanimBulletPhysicsWorld
	public override void _PreResetWorld()
	{
	    if( !_needResetKinematic ) {
		    return;
	    }

	    _processResetWorld = true;
	    _needResetKinematic = false;
	}

	// from MMD4MecanimBulletPhysicsWorld
	public override void _StepResetWorld( float elapsedTime )
	{
		if( !_processResetWorld ) {
			return;
		}
		
		if( elapsedTime < _resetMorphTime ) {
			if( _resetMorphTime > 0.0f ) {
				_processResetRatio = elapsedTime / _resetMorphTime;
				_PerformMoveWorldTransformKinematicOnly( _processResetRatio );
			}
		} else {
			if( _processResetRatio != 1.0f ) {
				_processResetRatio = 1.0f;
				_PerformMoveWorldTransformKinematicOnly( 1.0f );
			}
		}
	}
	
	// from MMD4MecanimBulletPhysicsWorld
	public override void _PostResetWorld()
	{
		_processResetWorld = false;
		
		if( _boneList != null ) {
			for( int i = 0; i < _boneList.Length; ++i ) {
				_boneList[i].CleanupMoveWorldTransform();
			}
		}
	}

	void _ProcessJoinWorld()
	{
		if( _isJoinedWorld == false ) {
			_isJoinedWorld = true;
			if( _rigidBodyList != null ) {
				for( int i = 0; i < _rigidBodyList.Length; ++i ) {
					_rigidBodyList[i].JoinWorld();
				}
			}
			if( _jointList != null ) {
				for( int i = 0; i < _jointList.Length; ++i ) {
					_jointList[i].JoinWorld();
				}
			}
		}
	}

	void _MakeSimulatedRigidBodyList()
	{
		if( _rigidBodyList != null ) {
			int simulatedRigidBodyLength = 0;
			for( int i = 0; i < _rigidBodyList.Length; ++i ) {
				if( _rigidBodyList[i].bone != null && _rigidBodyList[i].isSimulated ) {
					++simulatedRigidBodyLength;
				}
			}
			
			_simulatedRigidBodyList = new PMXRigidBody[simulatedRigidBodyLength];
			for( int i = 0, j = 0; i < _rigidBodyList.Length; ++i ) {
				if( _rigidBodyList[i].bone != null && _rigidBodyList[i].isSimulated ) {
					_simulatedRigidBodyList[j] = _rigidBodyList[i];
					++j;
				}
			}
			
			for( int i = 0; i + 1 < _simulatedRigidBodyList.Length; ++i ) {
				if( _simulatedRigidBodyList[i].parentBoneID < 0 ) {
					continue;
				}
				for( int j = i + 1; j < _simulatedRigidBodyList.Length; ++j ) {
					if( _simulatedRigidBodyList[j].parentBoneID < 0 ) {
						_Swap( ref _simulatedRigidBodyList[i], ref _simulatedRigidBodyList[j] );
						break;
					} else {
						if( _IsParentBone( _simulatedRigidBodyList[j].bone, _simulatedRigidBodyList[i].bone ) ) {
							_Swap( ref _simulatedRigidBodyList[i], ref _simulatedRigidBodyList[j] );
						}
					}
				}
			}
		}
	}

	void _PrepareMoveWorldTransform()
	{
		if( _rigidBodyList != null ) {
			for( int i = 0; i < _rigidBodyList.Length; ++i ) {
				if( _rigidBodyList[i].bone != null ) {
					_rigidBodyList[i].bone.PrepareMoveWorldTransform();
				}
			}
		}
	}
	
	void _PerformMoveWorldTransformKinematicOnly( float r )
	{
		if( _rigidBodyList != null ) {
			for( int i = 0; i < _rigidBodyList.Length; ++i ) {
				if( _rigidBodyList[i].bone != null &&
				    _rigidBodyList[i].isKinematic || _rigidBodyList[i].isDisabled ) {
					_rigidBodyList[i].bone.PerformMoveWorldTransform( r );
				}
			}
		}
	}
}
