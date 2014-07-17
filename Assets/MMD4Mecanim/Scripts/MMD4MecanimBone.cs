using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using MorphCategory		= MMD4MecanimData.MorphCategory;
using MorphType			= MMD4MecanimData.MorphType;
using MorphData			= MMD4MecanimData.MorphData;
using MorphMotionData	= MMD4MecanimData.MorphMotionData;
using BoneData			= MMD4MecanimData.BoneData;
using RigidBodyData		= MMD4MecanimData.RigidBodyData;
using FileType			= MMD4MecanimData.FileType;
using PMDBoneType		= MMD4MecanimData.PMDBoneType;
using PMXBoneFlags		= MMD4MecanimData.PMXBoneFlags;
using FastVector3		= MMD4MecanimCommon.FastVector3;
using FastQuaternion	= MMD4MecanimCommon.FastQuaternion;
using RigidBody			= MMD4MecanimModel.RigidBody;

public class MMD4MecanimBone : MonoBehaviour
{
	public MMD4MecanimModel	model;
	public int				boneID = -1;
	public bool				ikEnabled = false;
	public float			ikWeight = 1.0f;
	public GameObject		ikGoal;
	bool					_ikEnabledCached;
	bool					_delayedNotProcessing;

	public bool				_physicsEngineEnabled;
	public bool				_boneInherenceEnabled;

	public bool UpdateIKEnabledCached()
	{
		bool r = (_ikEnabledCached != this.ikEnabled);
		_ikEnabledCached = this.ikEnabled;
		return r;
	}

	FileType				_fileType = FileType.None;
	BoneData				_boneData;
	RigidBody				_rigidBody;
	MMD4MecanimBone			_rootBone; // localInherence only.
	MMD4MecanimBone			_parentBone;
	MMD4MecanimBone			_originalParentBone;
	MMD4MecanimBone			_inherenceParentBone;
	Vector3					_originalLocalPosition	= Vector3.zero;			// for modifiedHierarchy
	Quaternion				_originalLocalRotation	= Quaternion.identity;	// for modifiedHierarchy

	//----------------------------------------------------------------------------------------------------------------------------

	bool					_isDirtyLocalPosition = false;
	bool					_isDirtyLocalRotation = false;
	bool					_isResetLocalPosition = false;
	bool					_isResetLocalRotation = false;
	bool					_isChangedLocalPosition = false;
	bool					_isChangedLocalRotation = false;
	bool					_isUpdatedLocalPosition = false;
	bool					_isUpdatedLocalRotation = false;
	bool					_isPrefixedTransform = false;
	bool					_isPeformedTransform = false;
	bool					_isSavedLocalTransform = false;
	Vector3					_savedLocalPosition = Vector3.zero;
	Quaternion				_savedLocalRotation = Quaternion.identity;
	Vector3					_savedPosition = Vector3.zero;				// for ResetTransform
	Quaternion				_savedRotation = Quaternion.identity;		// for ResetTransform

	public Vector3 originalLocalPosition		{ get { return _originalLocalPosition; } }
	public Quaternion originalLocalRotation		{ get { return _originalLocalRotation; } }

	//----------------------------------------------------------------------------------------------------------------------------

	FastVector3				_baseLocalPosition	= FastVector3.zero;
	FastVector3				_userPosition		= FastVector3.zero;
	FastVector3				_userEulerAngles	= FastVector3.zero;
	FastQuaternion			_userRotation		= FastQuaternion.identity;
	FastVector3				_morphPosition		= FastVector3.zero;
	FastQuaternion			_morphRotation		= FastQuaternion.identity;
	FastVector3				_inherencePosition	= FastVector3.zero;
	FastQuaternion			_inherenceRotation	= FastQuaternion.identity;
	FastVector3				_externalPosition	= FastVector3.zero;
	FastQuaternion			_externalRotation	= FastQuaternion.identity;

	//----------------------------------------------------------------------------------------------------------------------------

	// For modifiedHierarchy and inherenceLocal.

	public struct CachedPosition
	{
		public bool hasCached;
		public bool hasCachedAtLeastOnce;
		public bool hasChanged;
		public Vector3 value;
		public void ClearFlags() { hasCached = hasChanged = false; }
	}

	public struct CachedRotation
	{
		public bool hasCached;
		public bool hasCachedAtLeastOnce;
		public bool hasChanged;
		public Quaternion value;
		public void ClearFlags() { hasCached = hasChanged = false; }
	}

	CachedPosition _cachedPosition = new CachedPosition();
	CachedRotation _cachedRotation = new CachedRotation();

	Vector3 cachedPosition { get { _RefreshCachedPosition(); return _cachedPosition.value; } }
	Quaternion cachedRotation { get { _RefreshCachedRotation(); return _cachedRotation.value; } }
	bool cachedPositionHasChanged { get { _RefreshCachedPosition(); return _cachedPosition.hasChanged; } }
	bool cachedRotationHasChanged { get { _RefreshCachedRotation(); return _cachedRotation.hasChanged; } }

	void _RefreshCachedPosition()
	{
		if( !_cachedPosition.hasCached ) {
			_cachedPosition.hasCached = true;
			Vector3 position = this.transform.position;
			if( _cachedPosition.hasCachedAtLeastOnce ) {
				_cachedPosition.hasChanged = (_cachedPosition.value != position);
			} else {
				_cachedPosition.hasCachedAtLeastOnce = true;
				_cachedPosition.hasChanged = true;
			}
			_cachedPosition.value = position;
		}
	}

	void _RefreshCachedRotation()
	{
		if( !_cachedRotation.hasCached ) {
			_cachedRotation.hasCached = true;
			Quaternion rotation = this.transform.rotation;
			if( _cachedRotation.hasCachedAtLeastOnce ) {
				_cachedRotation.hasChanged = (_cachedRotation.value != rotation);
			} else {
				_cachedRotation.hasCachedAtLeastOnce = true;
				_cachedRotation.hasChanged = true;
			}
			_cachedRotation.value = rotation;
		}
	}

	void _OverwriteCachedPosition()
	{
		if( _cachedPosition.hasCached ) {
			_cachedPosition.value = this.transform.position;
		}
	}
	
	void _OverwriteCachedRotation()
	{
		if( _cachedRotation.hasCached ) {
			_cachedRotation.value = this.transform.rotation;
		}
	}

	//----------------------------------------------------------------------------------------------------------------------------

	bool _isProcessingTransform;
	bool _isProcessingInherence;

	// Call from MMD4MecanimModel(IK/Physics)
	public void _ClearProcessingTransform()
	{
		_isProcessingTransform = false;
		_isProcessingInherence = false;
	}

	// Call from MMD4MecanimModel(IK/Physics)
	public void _MarkProcessingTransform()
	{
		_isProcessingTransform = true;
	}

	//----------------------------------------------------------------------------------------------------------------------------

	// Optimized for MMD4MecanimModel._processingBoneList

	int _isProcessing = -1;

	// Call from MMD4MecanimModel
	public void _InvalidateProcessing()
	{
		_isProcessing = -1;
	}
	
	// Call from MMD4MecanimModel
	public bool _IsProcessing()
	{
		if( _isProcessing != -1 ) {
			return _isProcessing != 0;
		}

		if( this.isIKDepended ||			// Under IK
		    this.isRigidBodySimulated ||	// Under Physics
		    !_userPosition.isZero ||
		    !_userRotation.isIdentity ||
		    !_morphPosition.isZero ||
		    !_morphRotation.isIdentity ) {
			_isProcessing = 1;
			return true;
		}

		if( _boneInherenceEnabled ) {
			if( _inherenceParentBone != null ) {
				_isProcessing = 1;
				return true;
			}
		}

		if( _delayedNotProcessing ) {
			_isProcessing = 1;
			return true;
		}

		// Need for ResetTransform( 2nd pass or later. )
		if( _parentBone != null && _parentBone.isIKDepended ) {
			_isProcessing = 1;
			return true;
		}

		// Need for ResetTransform( 2nd pass or later. )
		if( _parentBone != null && _parentBone.isRigidBodySimulated ) {
			_isProcessing = 1;
			return true;
		}

		// Recursivery check
		if( _isModifiedHierarchy ) {
			if( (_parentBone != null && _parentBone._IsProcessing()) ||
			    (_originalParentBone != null && _originalParentBone._IsProcessing()) ) {
				_isProcessing = 1;
				return true;
			}
		}

		_isProcessing = 0;
		return false;
	}

	//----------------------------------------------------------------------------------------------------------------------------

	public bool IsInherencePosition()
	{
		if( _boneData != null ) {
			if( _fileType == FileType.PMX ) {
				if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceTranslate) != PMXBoneFlags.None ) {
					return true;
				}
			}
		}
		
		return false;
	}
	
	public bool IsInherenceRotation()
	{
		if( _boneData != null ) {
			if( _fileType == FileType.PMD ) {
				if( _boneData.pmdBoneType == PMDBoneType.UnderRotate ||
				   _boneData.pmdBoneType == PMDBoneType.FollowRotate ) {
					return true;
				}
			} else if( _fileType == FileType.PMX ) {
				if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceRotate) != PMXBoneFlags.None ) {
					return true;
				}
			}
		}
		
		return false;
	}

	float _GetInherenceWeight()
	{
		if( _boneData != null ) {
			if( _fileType == FileType.PMD ) {
				if( _boneData.pmdBoneType == PMDBoneType.UnderRotate && _inherenceParentBone != null ) {
					return 1.0f;
				} else if( _boneData.pmdBoneType == PMDBoneType.FollowRotate && _inherenceParentBone != null ) {
					return _boneData.followCoef;
				}
			} else if( _fileType == FileType.PMX ) {
				return _boneData.inherenceWeight;
			}
		}
		
		return 0.0f;
	}

	//----------------------------------------------------------------------------------------------------------------------------

	/*
		Flow.

		Awake()
			_SetupTransform()

		LateUpdate()
			_PrefixTransform() // Not overwrite animation
			_PerformTransform() // Not overwrite animation

				_PrepareTransform2()
				SolveIK()
				_PrefixTransform2() // Overwrite animation
				_PerformTransform2() // Overwrite animation

			_PostfixTransform()
	*/

	public void _SetupTransform()
	{
		if( _boneData != null && _boneData.isRootBone ) {
			_baseLocalPosition = Vector3.zero;
		} else {
			_baseLocalPosition = _ComputeLocalPosition(); // Memo: Don't move in awake time at external.
		}
		_originalLocalPosition = _baseLocalPosition;
		_originalLocalRotation = Quaternion.identity;
		_savedLocalPosition = this.transform.localPosition; // Save tranform directly.
		_savedLocalRotation = Quaternion.identity; // Memo: Delayed check for external transform in _PrepareTransform()
		_cachedPosition.value = this.transform.position;
		_cachedRotation.value = this.transform.rotation;
	}

	// Prepare transform for 1st pass
	public void _PrepareTransform()
	{
		_isPrefixedTransform = false;
		_isPeformedTransform = false;
		_isChangedLocalPosition = false;
		_isChangedLocalRotation = false;

		// For modifiedHierarchy & inherenceLocal parent changed check.
		_cachedPosition.ClearFlags(); // Check for external transform.
		_cachedRotation.ClearFlags(); // Check for external transform.

		if( this.isIKDepended || this.isRigidBodySimulated ) { // If IKDepended / RigidBodySimulated, not check savedLocalTransform.
			_isSavedLocalTransform = false;
			return;
		}

		// Check external transform and refresh original local transform and external transform.
		Vector3 localPosition = this.transform.localPosition;
		if( !_isSavedLocalTransform || _savedLocalPosition != localPosition ) {
			_savedLocalPosition = localPosition;
			_originalLocalPosition = _ComputeLocalPosition();
			_externalPosition = _originalLocalPosition - _baseLocalPosition; // Save external position
			_isDirtyLocalPosition = !_userPosition.isZero || !_morphPosition.isZero;
			// Memo: Inherence dirty check in _PrefixTransform()
		}
		Quaternion localRotation = this.transform.localRotation;
		if( !_isSavedLocalTransform || _savedLocalRotation != localRotation ) {
			_savedLocalRotation = localRotation;
			_originalLocalRotation = _ComputeLocalRotation();
			_externalRotation = _originalLocalRotation; // Save external rotation
			_isDirtyLocalRotation = !_userRotation.isIdentity || !_morphRotation.isIdentity;
			// Memo: Inherence dirty check in _PrefixTransform()
		}
		_isSavedLocalTransform = true;
	}

	public void _PrefixTransform()
	{
		if( _isPrefixedTransform || _boneData == null ) {
			return;
		}
		_isPrefixedTransform = true;
		
		if( _isModifiedHierarchy ) { // For modifiedHierarchy only.
			if( _originalParentBone != null ) {
				if( _originalParentBone.cachedPositionHasChanged ) { // Memo: Use cache.
					_isResetLocalPosition = true;
				}
				if( _originalParentBone.cachedRotationHasChanged ) { // Memo: Use cache.
					_isResetLocalPosition = true; // If originalParentBone was moved, child bone's localPosition was moved, too.
					_isResetLocalRotation = true;
				}
			}
			if( _parentBone != null ) {
				if( _parentBone.cachedPositionHasChanged ) { // Memo: Use cache.
					_isResetLocalPosition = true;
				}
				if( _parentBone.cachedRotationHasChanged ) { // Memo: Use cache.
					_isResetLocalPosition = true; // If parentBone was moved, child bone's localPosition was moved, too.
					_isResetLocalRotation = true;
				}
			}
		}

		bool overwriteInherencePosition = _externalPosition.isZero;
		bool overwriteInherenceRotation = _externalRotation.isIdentity;
		
		// If not overwriteAnimation, don't process inherence.
		if( (overwriteInherencePosition || overwriteInherenceRotation) && _inherenceParentBone != null ) {
			_inherenceParentBone._PrefixTransform(); // Precheck recursivery.
			
			if( _fileType == FileType.PMD ) {
				if( overwriteInherenceRotation ) {
					if( _boneData.pmdBoneType == PMDBoneType.UnderRotate ||
					   _boneData.pmdBoneType == PMDBoneType.FollowRotate ) {
						_isDirtyLocalRotation |= _inherenceParentBone._isChangedLocalRotation;
					}
				}
			} else if( _fileType == FileType.PMX ) {
				if( overwriteInherencePosition ) {
					if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceTranslate) != PMXBoneFlags.None ) {
						if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceLocal) != PMXBoneFlags.None ) {
							_isDirtyLocalPosition |= _inherenceParentBone.cachedPositionHasChanged;
							if( _rootBone != null ) { // Additional check for InherenceLocal.
								_isDirtyLocalPosition |= _rootBone.cachedPositionHasChanged;
							}
						} else {
							_isDirtyLocalPosition |= _inherenceParentBone._isChangedLocalPosition;
						}
					}
				}
				if( overwriteInherenceRotation ) {
					if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceRotate) != PMXBoneFlags.None ) {
						if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceLocal) != PMXBoneFlags.None ) {
							_isDirtyLocalRotation |= _inherenceParentBone.cachedRotationHasChanged;
							if( _rootBone != null ) { // Additional check for InherenceLocal.
								_isDirtyLocalRotation |= _rootBone.cachedRotationHasChanged;
							}
						} else {
							_isDirtyLocalRotation |= _inherenceParentBone._isChangedLocalRotation;
						}
					}
				}
			}
		}

		_isResetLocalPosition |= _isDirtyLocalPosition;
		_isResetLocalRotation |= _isDirtyLocalRotation;
		_isChangedLocalPosition |= _isDirtyLocalPosition;
		_isChangedLocalRotation |= _isDirtyLocalRotation;
		_isUpdatedLocalPosition |= _isDirtyLocalPosition;
		_isUpdatedLocalRotation |= _isDirtyLocalRotation;
	}

	void _InternalPerformTransform( bool overwriteInherence )
	{
		// overwriteInherence ... 1st pass as false, 2nd pass or later, true.
		bool overwriteInherencePosition = overwriteInherence;
		bool overwriteInherenceRotation = overwriteInherence;
		if( !overwriteInherence ) {
			overwriteInherencePosition = _externalPosition.isZero;
			overwriteInherenceRotation = _externalRotation.isIdentity;
		}

		FastVector3 tempPosition = FastVector3.zero;
		FastQuaternion tempRotation = FastQuaternion.identity;
		
		if( _isDirtyLocalPosition ) {
			_isDirtyLocalPosition = false;
			
			tempPosition = _baseLocalPosition + _userPosition + _morphPosition;
			
			if( !overwriteInherencePosition ) {
				tempPosition += _externalPosition;
			} else if( _inherenceParentBone != null ) {
				if( _fileType == FileType.PMX ) {
					if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceTranslate) != PMXBoneFlags.None ) {
						float inherenceWeight = _boneData.inherenceWeight;
						Vector3 inherencePosition = Vector3.zero;
						if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceLocal) != PMXBoneFlags.None ) {
							inherencePosition = _originalParentBone.cachedPosition;
							if( _rootBone != null ) {
								inherencePosition = inherencePosition - _rootBone.cachedPosition;
							}
						} else {
							inherencePosition = _inherenceParentBone._originalLocalPosition;
						}
						_inherencePosition = inherencePosition * inherenceWeight;
					}
					tempPosition += _inherencePosition;
				}
			}
		} else {
			tempPosition = _originalLocalPosition;
		}
		
		if( _isDirtyLocalRotation ) {
			_isDirtyLocalRotation = false;
			
			if( !overwriteInherenceRotation ) {
				tempRotation = _externalRotation * _userRotation * _morphRotation;
			} else {
				tempRotation = _userRotation * _morphRotation;
			}
			
			if( overwriteInherenceRotation && _inherenceParentBone != null ) {
				float inherenceWeight = 0.0f;
				Quaternion inherenceRotation = Quaternion.identity;
				if( _fileType == FileType.PMD ) {
					if( _boneData.pmdBoneType == PMDBoneType.UnderRotate && _inherenceParentBone != null ) {
						inherenceWeight = 1.0f;
						inherenceRotation = _inherenceParentBone._originalLocalRotation;
					} else if( _boneData.pmdBoneType == PMDBoneType.FollowRotate && _inherenceParentBone != null ) {
						inherenceWeight = _boneData.followCoef;
						inherenceRotation = _inherenceParentBone._originalLocalRotation;
					}
				} else if( _fileType == FileType.PMX ) {
					inherenceWeight = _boneData.inherenceWeight;
					if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceRotate) != PMXBoneFlags.None ) {
						if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceLocal) != PMXBoneFlags.None ) {
							inherenceRotation = _originalParentBone.cachedRotation;
							if( _rootBone != null ) {
								inherenceRotation = MMD4MecanimCommon.Inverse( _rootBone.cachedRotation ) * inherenceRotation;
							}
						} else {
							inherenceRotation = _inherenceParentBone._originalLocalRotation;
						}
					}
				}
				
				if( Mathf.Abs(inherenceWeight - 1.0f) > Mathf.Epsilon ) {
					float inherenceWeightAbs = Mathf.Abs(inherenceWeight);
					if( inherenceWeightAbs < 1.0f ) {
						inherenceRotation = Quaternion.Slerp( Quaternion.identity, inherenceRotation, inherenceWeightAbs );
					} else if( inherenceWeightAbs > 1.0f ) {
						float angle;
						Vector3 axis;
						inherenceRotation.ToAngleAxis( out angle, out axis );
						inherenceRotation = Quaternion.AngleAxis( angle * inherenceWeightAbs, axis );
					}
					if( inherenceWeight < 0.0f ) {
						inherenceRotation = MMD4MecanimCommon.Inverse( inherenceRotation );
					}
				}
				
				_inherenceRotation = inherenceRotation;
				tempRotation *= _inherenceRotation;
			}
		} else {
			tempRotation = _originalLocalRotation;
		}
		
		if( _isResetLocalPosition && _isResetLocalRotation ) {
			_isResetLocalPosition = _isResetLocalRotation = false;
			_SetLocalTransform( tempPosition, tempRotation );
		} else if( _isResetLocalPosition ) {
			_isResetLocalPosition = false;
			_SetLocalPosition( tempPosition );
		} else if( _isResetLocalRotation ) {
			_isResetLocalRotation = false;
			_SetLocalRotation( tempRotation );
		}
	}

	public void _PerformTransform()
	{
		if( _isPeformedTransform || _boneData == null ) {
			return;
		}
		_isPeformedTransform = true;

		// Precheck recursivery.
		if( _isDirtyLocalPosition || _isDirtyLocalRotation ) {
			if( _inherenceParentBone != null ) {
				_inherenceParentBone._PerformTransform();
			}
		}
		if( _isModifiedHierarchy ) {
			if( _parentBone != null ) {
				_parentBone._PerformTransform();
			}
		}
		if( _originalParentBone != null ) {
			_originalParentBone._PerformTransform();
		}
	
		_InternalPerformTransform( false );
	}
	
	// Prepare transform for 2nd pass(Before SolveIK, Physics)
	public void _PrepareTransform2()
	{
		_isPrefixedTransform = false;
		_isPeformedTransform = false;
		_isChangedLocalPosition = false;
		_isChangedLocalRotation = false;

		if( this.isIKDepended || this.isRigidBodySimulated ) { // If IKDepended / RigidBodySimulated, not check savedLocalTransform.
			return;
		}

		if( !_isProcessingTransform ) { // Not IK or Physics
			if( _inherenceParentBone != null ) {
				_inherenceParentBone._PrepareTransform2();
				if( _inherenceParentBone._isProcessingTransform || _inherenceParentBone._isProcessingInherence ) {
					_isProcessingInherence = true; // Recursivery inherence check.
				}
			}

			if( _inherenceParentBone == null ||
			    (!_inherenceParentBone._isProcessingTransform && !_inherenceParentBone._isProcessingInherence) ) {
				if( _parentBone != null && _parentBone._isProcessingTransform ) {
					_savedPosition = this.transform.position;
					_savedRotation = this.transform.rotation;
				}
			}
		}
	}

	public void _PrefixTransform2()
	{
		if( _isPrefixedTransform || _boneData == null ) {
			return;
		}
		_isPrefixedTransform = true;

		// If IKDepended / RigidBodySimulated, not process 2nd pass or later.
		if( this.isIKDepended || this.isRigidBodySimulated ) {
			return;
		}

		if( !_isProcessingTransform ) { // Not IK or Physics
			if( _inherenceParentBone == null ||
			   (!_inherenceParentBone._isProcessingTransform && !_inherenceParentBone._isProcessingInherence) ) {
				// for Not InherenceTransform( ResetPosition and ResetRotation )
				if( _parentBone != null && _parentBone._isProcessingTransform ) {
					_isDirtyLocalPosition = true;
					_isDirtyLocalRotation = true;
					_isResetLocalPosition = true;
					_isResetLocalRotation = true;
					_isChangedLocalPosition = true;
					_isChangedLocalRotation = true;
					_isUpdatedLocalPosition = true;
					_isUpdatedLocalRotation = true;
				}
			} else { // for InherenceTransform && (_isProcessingTransform | _isProcessingInherence)
				_isDirtyLocalPosition |= IsInherencePosition();
				_isDirtyLocalRotation |= IsInherenceRotation();
				_isResetLocalPosition |= _isDirtyLocalPosition;
				_isResetLocalRotation |= _isDirtyLocalRotation;
				_isChangedLocalPosition |= _isDirtyLocalPosition;
				_isChangedLocalRotation |= _isDirtyLocalRotation;
				_isUpdatedLocalPosition |= _isDirtyLocalPosition;
				_isUpdatedLocalRotation |= _isDirtyLocalRotation;
			}
		}
	}

	public void _PerformTransform2()
	{
		if( _isPeformedTransform || _boneData == null ) {
			return;
		}
		_isPeformedTransform = true;
		
		if( this.isIKDepended || this.isRigidBodySimulated ) {
			return;
		}

		// Precheck recursivery.
		if( _isDirtyLocalPosition || _isDirtyLocalRotation ) {
			if( _inherenceParentBone != null ) {
				_inherenceParentBone._PerformTransform2();
			}
		}
		if( _isModifiedHierarchy ) {
			if( _parentBone != null ) {
				_parentBone._PerformTransform2();
			}
		}
		if( _originalParentBone != null ) {
			_originalParentBone._PerformTransform2();
		}

		if( _inherenceParentBone == null ||
		   (!_inherenceParentBone._isProcessingTransform && !_inherenceParentBone._isProcessingInherence) ) {
			if( _parentBone != null && _parentBone._isProcessingTransform ) {
				_isDirtyLocalPosition = _isDirtyLocalRotation = false;
				if( _isResetLocalPosition ) {
					_isResetLocalPosition = false;
					this.transform.position = _savedPosition;
					_originalLocalPosition = _ComputeLocalPosition();
				}
				if( _isResetLocalRotation ) {
					_isResetLocalRotation = false;
					this.transform.rotation = _savedRotation;
					_originalLocalRotation = _ComputeLocalRotation();
				}
			} else {
				_isDirtyLocalPosition = _isDirtyLocalRotation = false;
				_isResetLocalPosition = _isResetLocalRotation = false;
			}
			return;
		}

		_InternalPerformTransform( true );
	}

	public void _PostfixTransform()
	{
		// For modifiedHierarchy & inherenceLocal.
		_OverwriteCachedPosition();
		_OverwriteCachedRotation();
		
		if( _isUpdatedLocalPosition ) {
			_isUpdatedLocalPosition = false;
			_savedLocalPosition = this.transform.localPosition;
		}
		if( _isUpdatedLocalRotation ) {
			_isUpdatedLocalRotation = false;
			_savedLocalRotation = this.transform.localRotation;
		}
		
		if( _delayedNotProcessing ) {
			_delayedNotProcessing = false;
			if( this.model != null ) {
				this.model._InvalidateProcessingBoneList();
			}
		}
	}

	//----------------------------------------------------------------------------------------------------------------------------

	Quaternion _localRotationBeforeIK = Quaternion.identity;
	Quaternion _originalLocalRotationBeforeIK = Quaternion.identity;

	public void _PrepareIKTransform()
	{
		_localRotationBeforeIK = this.transform.localRotation;
		_originalLocalRotationBeforeIK = _originalLocalRotation;
		// Memo: Reset localRotation before IK.
		this.ikEulerAngles = Vector3.zero;
		_SetLocalRotation( Quaternion.identity );
		_isChangedLocalRotation = true; // For inherneceRotation.
		_isUpdatedLocalRotation = true; // For _PostfixTransform
	}
	
	public void _ApplyIKRotation( Quaternion ikRotation )
	{
		_ApplyLocalRotation( ikRotation );
	}
	
	public void _SetLocalRotationFromIK( Quaternion localRotation )
	{
		_SetLocalRotation( localRotation );
	}

	public void _RestoreLocalRotationFromIK()
	{
		_SetLocalRotation( _localRotationBeforeIK );
	}
	
	public void _PostfixIKTransform()
	{
		if( this.feedbackIKWeight != 1.0f ) {
			_SetLocalRotation( Quaternion.Slerp( _originalLocalRotationBeforeIK, _originalLocalRotation, this.feedbackIKWeight ) );
		}
	}

	//----------------------------------------------------------------------------------------------------------------------------

	#if MMD4MECANIM_DEBUG
	public MMD4MecanimBone	rootBone { get { return _rootBone; } }
	public MMD4MecanimBone	parentBone { get { return _parentBone; } }
	public MMD4MecanimBone	originalParentBone { get { return _originalParentBone; } }
	public MMD4MecanimBone	inherenceParentBone { get { return _inherenceParentBone; } }
	public bool				isModifiedHierarchy { get { return _isModifiedHierarchy; } }
	public Quaternion		inherenceRotation { get { return _inherenceRotation; } }
	#endif
	public float			inherenceWeight { get { return _GetInherenceWeight(); } }

	[NonSerialized]
	public Vector3			ikEulerAngles;

	public enum _SkeletonType
	{
		None,
		LeftFoot,
		RightFoot,
		LeftHip,
		RightHip,
	}

	public const string		_LeftHipSkeletonName = "LeftHip";
	public const string		_RightHipSkeletonName = "RightHip";
	public const string		_LeftFootSkeletonName = "LeftFoot";
	public const string		_RightFootSkeletonName = "RightFoot";

	[NonSerialized]
	public _SkeletonType	_skeletonType;

	public BoneData boneData { get { return _boneData; } }

	[NonSerialized]
	public bool isModelControl;
	[NonSerialized]
	public bool isIKDepended;
	[NonSerialized]
	public bool isOverwriteAfterIK;
	[NonSerialized]
	public float feedbackIKWeight;

	public bool isRigidBodySimulated
	{
		get {
			if( !_physicsEngineEnabled ) {
				return false;
			}

			if( _rigidBody != null && _rigidBody.rigidBodyData != null ) {
				if( _rigidBody.rigidBodyData.rigidBodyType != MMD4MecanimData.RigidBodyType.Kinematics ) {
					return _rigidBody.enabled;
				}
			}

			return false;
		}
	}

	void _NotifyInvalidateProcessingBoneList( bool isEnalbed )
	{
		if( isEnalbed ) {
			if( this.model != null ) {
				this.model._InvalidateProcessingBoneList();
			}
		} else {
			_delayedNotProcessing = true;
		}
	}

	public Vector3 userPosition {
		get {
			return _userPosition;
		}
		set {
			if( _userPosition != value ) {
				_isDirtyLocalPosition = true;
				bool isZeroOld = _userPosition.isZero;
				_userPosition = value;
				if( _userPosition.isZero != isZeroOld ) {
					_NotifyInvalidateProcessingBoneList( !_userPosition.isZero );
				}
			}
		}
	}

	public Vector3 userEulerAngles {
		get {
			return _userEulerAngles;
		}
		set {
			if( _userEulerAngles != value ) {
				_isDirtyLocalRotation = true;
				bool isIdentityOld = _userRotation.isIdentity;
				if( MMD4MecanimCommon.FuzzyZero( value ) ) {
					_userRotation = Quaternion.identity;
					_userEulerAngles = Vector3.zero;
				} else {
					_userRotation = Quaternion.Euler( value );
					_userEulerAngles = value;
				}
				if( isIdentityOld != _userRotation.isIdentity ) {
					_NotifyInvalidateProcessingBoneList( !_userRotation.isIdentity );
				}
			}
		}
	}

	public Quaternion userRotation {
		get {
			return _userRotation;
		}
		set {
			if( _userRotation != value ) {
				_isDirtyLocalRotation = true;
				bool isIdentityOld = _userRotation.isIdentity;
				if( MMD4MecanimCommon.FuzzyIdentity( value ) ) { // Optimized: userRotation == (0,0,0)
					_userRotation = Quaternion.identity;
					_userEulerAngles = Vector3.zero;
				} else {
					_userRotation = value;
					_userEulerAngles = value.eulerAngles;
				}
				if( isIdentityOld != _userRotation.isIdentity ) {
					_NotifyInvalidateProcessingBoneList( !_userRotation.isIdentity );
				}
			}
		}
	}

	public Vector3 morphPosition {
		get {
			return _morphPosition;
		}
		set {
			if( _morphPosition != value ) {
				_isDirtyLocalPosition = true;
				bool isZeroOld = _morphPosition.isZero;
				_morphPosition = value;
				if( isZeroOld != _morphPosition.isZero ) {
					_NotifyInvalidateProcessingBoneList( !_morphPosition.isZero );
				}
			}
		}
	}

	public Quaternion morphRotation {
		get {
			return _morphRotation;
		}
		set {
			if( _morphRotation != value ) {
				_isDirtyLocalRotation = true;
				bool isIdentity = _morphRotation.isIdentity;
				_morphRotation = value;
				if( isIdentity != _morphRotation.isIdentity ) {
					_NotifyInvalidateProcessingBoneList( !_morphRotation.isIdentity );
				}
			}
		}
	}

	// Used IK.
	public Matrix4x4 originalLocalToWorldMatrix {
		get {
			if( _isModifiedHierarchy && _originalParentBone != null ) {
				Matrix4x4 parentLocalToWorldMatrix = _originalParentBone.originalLocalToWorldMatrix;
				Matrix4x4 localMatrix = Matrix4x4.TRS( _originalLocalPosition, _originalLocalRotation, Vector3.one );
				return parentLocalToWorldMatrix * localMatrix;
			} else {
				return this.gameObject.transform.localToWorldMatrix;
			}
		}
	}

	// Used IK.
	public Matrix4x4 originalWorldToLocalMatrix {
		get {
			if( _isModifiedHierarchy ) {
				Matrix4x4 localToWorldMatrix = this.originalLocalToWorldMatrix;
				return localToWorldMatrix.inverse;
			} else {
				return this.gameObject.transform.worldToLocalMatrix;
			}
		}
	}

	bool _isModifiedHierarchy
	{
		get {
			if( _boneData.parentBoneID == _boneData.originalParentBoneID ||
			    _boneData.parentBoneID == -1 || _boneData.originalParentBoneID == -1 ||
			    _parentBone == null || _originalParentBone == null ) {
				return false;
			}
			
			return true;
		}
	}

	public void Setup()
	{
		if( this.model == null || this.model.modelData == null || this.model.modelData.boneDataList == null ||
		    this.boneID < 0 || this.boneID >= this.model.modelData.boneDataList.Length ) {
			return;
		}

		_fileType = this.model.modelData.fileType;
		_boneData = this.model.modelData.boneDataList[this.boneID];

		RigidBody[] rigidBodyList = this.model.rigidBodyList;
		if( rigidBodyList != null ) {
			for( int i = 0; i < rigidBodyList.Length; ++i ) {
				if( rigidBodyList[i] != null && rigidBodyList[i].rigidBodyData != null &&
				    rigidBodyList[i].rigidBodyData.boneID == this.boneID ) {
					if( rigidBodyList[i].rigidBodyData.rigidBodyType != MMD4MecanimData.RigidBodyType.Kinematics ) {
						_rigidBody = rigidBodyList[i];
						break;
					}
				}
			}
		}
	}
	
	public void Bind()
	{
		if( this.model == null || _boneData == null ) {
			#if MMD4MECANIM_DEBUG
			Debug.LogError("");
			#endif
			return;
		}

		// Compute SkeletonType
		if( _boneData != null && _boneData.skeletonName != null ) {
			if( _boneData.skeletonName.Contains( _LeftHipSkeletonName ) ) {
				_skeletonType = _SkeletonType.LeftHip;
			} else if( _boneData.skeletonName.Contains( _RightHipSkeletonName ) ) {
				_skeletonType = _SkeletonType.RightHip;
			} else if( _boneData.skeletonName.Contains( _LeftFootSkeletonName ) ) {
				_skeletonType = _SkeletonType.LeftFoot;
			} else if( _boneData.skeletonName.Contains( _RightFootSkeletonName ) ) {
				_skeletonType = _SkeletonType.RightFoot;
			}
		}

		if( _boneData.parentBoneID != -1 ) {
			_parentBone = this.model.GetBone( _boneData.parentBoneID );
			#if MMD4MECANIM_DEBUG
			if( _parentBone == null ) {
				Debug.LogWarning("Not found parentBoneID:" + _boneData.parentBoneID + " boneID:" + this.boneID);
			}
			#endif
		}

		if( _boneData.originalParentBoneID != -1 ) {
			_originalParentBone = this.model.GetBone( _boneData.originalParentBoneID );
			#if MMD4MECANIM_DEBUG
			if( _originalParentBone == null ) {
				Debug.LogWarning("Not found originalParentBoneID:" + _boneData.originalParentBoneID + " boneID:" + this.boneID);
			}
			#endif
		}

		if( this.model != null ) {
			if( _fileType == FileType.PMD ) {
				if( _boneData.pmdBoneType == PMDBoneType.UnderRotate ) {
					if( _boneData.targetBoneID != -1 ) {
						_inherenceParentBone = this.model.GetBone( _boneData.targetBoneID );
						#if MMD4MECANIM_DEBUG
						if( _inherenceParentBone == null ) {
							Debug.LogWarning("Not found targetBoneID:" + _boneData.targetBoneID + " boneID:" + this.boneID);
						}
						#endif
					}
				} else if( _boneData.pmdBoneType == PMDBoneType.FollowRotate ) {
					if( _boneData.childBoneID != -1 ) {
						_inherenceParentBone = this.model.GetBone( _boneData.childBoneID );
						#if MMD4MECANIM_DEBUG
						if( _inherenceParentBone == null ) {
							Debug.LogWarning("Not found childBoneID:" + _boneData.childBoneID + " boneID:" + this.boneID);
						}
						#endif
					}
				}
			} else if( _fileType == FileType.PMX ) {
				if( (_boneData.pmxBoneFlags & (PMXBoneFlags.InherenceTranslate | PMXBoneFlags.InherenceRotate)) != PMXBoneFlags.None ) {
					if( _boneData.inherenceParentBoneID != -1 ) {
						_inherenceParentBone = this.model.GetBone( _boneData.inherenceParentBoneID );
						#if MMD4MECANIM_DEBUG
						if( _inherenceParentBone == null ) {
							Debug.LogWarning("Not found inherenceParentBoneID:" + _boneData.inherenceParentBoneID + " boneID:" + this.boneID);
						}
						#endif
					}
					if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceLocal) != PMXBoneFlags.None ) {
						_rootBone = this.model.GetRootBone();
						#if MMD4MECANIM_DEBUG
						if( _rootBone == null ) {
							Debug.LogWarning("Not found rootBone: boneID:" + this.boneID);
						}
						#endif
					}
				}
			}
		}

		_SetupTransform();
	}
	
	public void Destroy()
	{
		_boneData = null;
		_parentBone = null;
		_originalParentBone = null;
		_inherenceParentBone = null;
	}

	//--------------------------------------------------------------------------------------------------------------------------------------------------------------------

	Vector3 _ComputeLocalPosition()
	{
		if( _isModifiedHierarchy && _originalParentBone != null ) {
			return _originalParentBone.gameObject.transform.InverseTransformPoint( this.gameObject.transform.position );
		} else {
			return this.gameObject.transform.localPosition;
		}
	}

	Quaternion _ComputeLocalRotation()
	{
		if( _isModifiedHierarchy && _originalParentBone != null ) {
			return MMD4MecanimCommon.Inverse( _originalParentBone.gameObject.transform.rotation ) * this.gameObject.transform.rotation;
		} else {
			return this.gameObject.transform.localRotation;
		}
	}

	void _SetLocalTransform( Vector3 localPosition, Quaternion localRotation )
	{
		_originalLocalPosition = localPosition;
		_originalLocalRotation = localRotation;
		if( _isModifiedHierarchy && _originalParentBone != null ) {
			Vector3 position = _originalParentBone.gameObject.transform.TransformPoint( _originalLocalPosition );
			Quaternion rotation = _originalParentBone.gameObject.transform.rotation * _originalLocalRotation;
			this.gameObject.transform.position = position;
			this.gameObject.transform.rotation = rotation;
		} else {
			this.transform.localPosition = localPosition;
			this.transform.localRotation = localRotation;
		}
	}

	void _SetLocalPosition( Vector3 localPosition )
	{
		_originalLocalPosition = localPosition;
		if( _isModifiedHierarchy && _originalParentBone != null ) {
			// Faster than _ApplyLocalPosition
			Vector3 position = _originalParentBone.gameObject.transform.TransformPoint( _originalLocalPosition );
			this.gameObject.transform.position = position;
		} else {
			this.gameObject.transform.localPosition = localPosition;
		}
	}

	void _SetLocalRotation( Quaternion localRotation )
	{
		_originalLocalRotation = localRotation;
		if( _isModifiedHierarchy && _originalParentBone != null ) {
			// Faster than _ApplyLocalRotation
			Vector3 position = _originalParentBone.gameObject.transform.TransformPoint( _originalLocalPosition ); // Optimized: Fixed value.(Not compute)
			Quaternion rotation = _originalParentBone.gameObject.transform.rotation * _originalLocalRotation;
			this.gameObject.transform.position = position;
			this.gameObject.transform.rotation = rotation;
		} else {
			this.gameObject.transform.localRotation = localRotation;
		}
	}

	void _ApplyLocalRotation( Quaternion localRotation )
	{
		if( _isModifiedHierarchy && _originalParentBone != null ) {
			_originalLocalRotation *= localRotation;
			Vector3 position = _originalParentBone.gameObject.transform.TransformPoint( _originalLocalPosition ); // Optimized: Fixed value.(Not compute)
			Quaternion rotation = _originalParentBone.gameObject.transform.rotation * _originalLocalRotation;
			this.gameObject.transform.position = position;
			this.gameObject.transform.rotation = rotation;
		} else {
			_originalLocalRotation = this.gameObject.transform.localRotation;
			_originalLocalRotation *= localRotation;
			this.gameObject.transform.localRotation = _originalLocalRotation;
		}
	}
}
