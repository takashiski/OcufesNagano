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
using FileType			= MMD4MecanimData.FileType;
using PMDBoneType		= MMD4MecanimData.PMDBoneType;
using PMXBoneFlags		= MMD4MecanimData.PMXBoneFlags;

// pending: Inherence RigidBody support
// pending: Transform after physics support

public partial class MMD4MecanimModel
{
	// from _InitializeModel()
	void _BindBone()
	{
		Transform transform = this.gameObject.transform;
		foreach( Transform trn in transform ) {
			_BindBone( trn );
		}
	}
	
	void _BindBone( Transform trn )
	{
		if( !string.IsNullOrEmpty( trn.gameObject.name ) ) {
			int boneID = 0;
			if( _modelData.boneDataDictionary.TryGetValue( trn.gameObject.name, out boneID ) ) {
				MMD4MecanimBone bone = trn.gameObject.GetComponent< MMD4MecanimBone >();
				if( bone == null ) {
					bone = trn.gameObject.AddComponent< MMD4MecanimBone >();
				}
				bone.model = this;
				bone.boneID = boneID;
				bone.Setup();
				this.boneList[boneID] = bone;
				if( this.boneList[boneID].boneData != null && this.boneList[boneID].boneData.isRootBone ) {
					_rootBone = this.boneList[boneID];
				}
			}
		}
		foreach( Transform t in trn ) {
			_BindBone( t );
		}
	}
	
	//--------------------------------------------------------------------------------------------------------------------------------------------

	public void _InvalidateProcessingBoneList()
	{
		_isDirtyProcessingBoneList = true;
	}

	void _UpdateProcessingBoneList()
	{
		if( _sortedBoneList == null ) {
			return;
		}

		bool isUpdated = false;
		if( _ikEnabledCached != this.ikEnabled ) {
			_ikEnabledCached = this.ikEnabled;
			isUpdated = true;
		}
		if( _boneInherenceEnabledCached != this.boneInherenceEnabled ) {
			_boneInherenceEnabledCached = this.boneInherenceEnabled;
			isUpdated = true;
		}
		if( _physicsEngineCached != this.physicsEngine ) {
			_physicsEngineCached = this.physicsEngine;
			isUpdated = true;
		}

		if( this.ikList != null ) {
			for( int i = 0; i < this.ikList.Length; ++i ) {
				if( this.ikList[i] != null && this.ikList[i].destBone != null ) {
					isUpdated |= this.ikList[i].destBone.UpdateIKEnabledCached();
				}
			}
		}

		if( isUpdated || _isDirtyProcessingBoneList || _processingBoneList == null ) {
			_isDirtyProcessingBoneList = false;
			_MarkDepended();

			for( int i = 0; i < _sortedBoneList.Length; ++i ) {
				if( _sortedBoneList[i] != null ) {
					_sortedBoneList[i]._InvalidateProcessing();
				}
			}

			if( _processingBoneList == null ) {
				_processingBoneList = new List<MMD4MecanimBone>();
			} else {
				_processingBoneList.Clear();
			}
			for( int i = 0; i < _sortedBoneList.Length; ++i ) {
				if( _sortedBoneList[i] != null && _sortedBoneList[i]._IsProcessing() ) {
					_processingBoneList.Add( _sortedBoneList[i] );
				}
			}

			#if MMD4MECANIM_DEBUG
			Debug.Log ( this.gameObject.name + ": ProcessingBoneList ReCreated. " + _processingBoneList.Count + "/" + _sortedBoneList.Length );
			#endif
		}
	}

	void _MarkDepended()
	{
		if( _sortedBoneList == null ) {
			return;
		}

		for( int i = 0; i < _sortedBoneList.Length; ++i ) {
			if( _sortedBoneList[i] != null ) {
				_sortedBoneList[i].isIKDepended = false;
				_sortedBoneList[i].feedbackIKWeight = 0.0f;
				_sortedBoneList[i]._physicsEngineEnabled = (_physicsEngineCached != PhysicsEngine.None);
				_sortedBoneList[i]._boneInherenceEnabled = _boneInherenceEnabledCached;
			}
		}
		if( _ikEnabledCached && this.ikList != null ) {
			for( int n = 0; n < this.ikList.Length; ++n ) {
				if( this.ikList[n] != null ) {
					this.ikList[n].MarkIKDepended();
				}
			}
		}
	}

	void _UpdateBone()
	{
		// Nothing.
	}

	void _LateUpdateBone()
	{
		_UpdateProcessingBoneList();

		if( _processingBoneList != null ) {
			_MarkDepended(); // Memo: Feedback IKWeight only.

			for( int i = 0; i < _processingBoneList.Count; ++i ) {
				if( _processingBoneList[i] != null ) {
					_processingBoneList[i]._PrepareTransform();
					_processingBoneList[i]._PrefixTransform();
					_processingBoneList[i]._PerformTransform();
				}
			}

			if( this.ikEnabled && this.ikList != null ) {
				if( _modelData != null && _modelData.fileType == FileType.PMD ) {
					for( int i = 0; i < _processingBoneList.Count; ++i ) {
						if( _processingBoneList[i] != null ) {
							_processingBoneList[i]._ClearProcessingTransform();
						}
					}

					for( int n = 0; n < this.ikList.Length; ++n ) {
						if( this.ikList[n] != null && this.ikList[n].ikEnabled ) {
							this.ikList[n].MarkProcessingTransform();
						}
					}

					for( int i = 0; i < _processingBoneList.Count; ++i ) {
						if( _processingBoneList[i] != null ) {
							_processingBoneList[i]._PrepareTransform2();
						}
					}

					for( int n = 0; n < this.ikList.Length; ++n ) {
						if( this.ikList[n] != null && this.ikList[n].ikEnabled ) {
							this.ikList[n].Solve();
						}
					}

					for( int i = 0; i < _processingBoneList.Count; ++i ) {
						if( _processingBoneList[i] != null ) {
							_processingBoneList[i]._PrefixTransform2();
							_processingBoneList[i]._PerformTransform2();
						}
					}
				} else if( _modelData != null && _modelData.fileType == FileType.PMX ) {
					for( int n = 0; n < this.ikList.Length; ++n ) {
						if( this.ikList[n] != null && this.ikList[n].ikEnabled ) {
							for( int i = 0; i < _processingBoneList.Count; ++i ) {
								if( _processingBoneList[i] != null ) {
									_processingBoneList[i]._ClearProcessingTransform();
								}
							}
							
							this.ikList[n].MarkProcessingTransform();
							
							for( int i = 0; i < _processingBoneList.Count; ++i ) {
								if( _processingBoneList[i] != null ) {
									_processingBoneList[i]._PrepareTransform2();
								}
							}
							
							this.ikList[n].Solve();
							
							for( int i = 0; i < _processingBoneList.Count; ++i ) {
								if( _processingBoneList[i] != null ) {
									_processingBoneList[i]._PrefixTransform2();
									_processingBoneList[i]._PerformTransform2();
								}
							}
						}
					}
				}
			}

			for( int i = 0; i < _processingBoneList.Count; ++i ) {
				if( _processingBoneList[i] != null ) {
					_processingBoneList[i]._PostfixTransform();
				}
			}
		}

		_UpdatePPHBones();
	}

	//--------------------------------------------------------------------------------------------------------------------------------------------

	bool _isGenericAnimation {
		get {
			if( _animator == null ) {
				return false;
			}

			AnimationInfo[] animationInfos = _animator.GetCurrentAnimationClipState(0);
			if( animationInfos == null || animationInfos.Length == 0 ) {
				return false;
			}
			return !_animator.isHuman;
		}
	}

	//--------------------------------------------------------------------------------------------------------------------------------------------

	void _InitializePPHBones()
	{
		if( _animator == null || _animator.avatar == null || !_animator.avatar.isValid || !_animator.avatar.isHuman ) {
			return;
		}
		{
			Transform leftShoulderTransform = _animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
			Transform leftArmTransform = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
			if( leftShoulderTransform != null && leftArmTransform != null ) {
				PPHBone pphBone = new PPHBone( PPHType.Shoulder, leftShoulderTransform.gameObject );
				pphBone.AddChildSkeleton( leftArmTransform.gameObject );
				_pphBones.Add( pphBone );
			}
		}
		{
			Transform rightShoulderTransform = _animator.GetBoneTransform(HumanBodyBones.RightShoulder);
			Transform rightArmTransform = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
			if( rightShoulderTransform != null && rightArmTransform != null ) {
				PPHBone pphBone = new PPHBone( PPHType.Shoulder, rightShoulderTransform.gameObject );
				pphBone.AddChildSkeleton( rightArmTransform.gameObject );
				_pphBones.Add( pphBone );
			}
		}
	}

	public void ForceUpdatePPHBones()
	{
		_UpdatePPHBones();
	}

	void _UpdatePPHBones()
	{
		if( !this.pphEnabled ) {
			return;
		}
		if( _pphBones == null ) {
			return;
		}
		if( _animator == null ) {
			return;
		}

		bool isNoAnimation = false;
		AnimationInfo[] animationInfos = _animator.GetCurrentAnimationClipState(0);
		if( animationInfos == null || animationInfos.Length == 0 ) {
			isNoAnimation = true;
			if( !this.pphEnabledNoAnimation ) {
				return; // No playing animation.
			}
		}
		
		float pphRate = 0.0f;
		if( isNoAnimation ) {
			pphRate = 1.0f; // pphEnabledNoAnimation
		} else {
			foreach( AnimationInfo animationInfo in animationInfos ) {
				if( !animationInfo.clip.name.EndsWith( ".vmd" ) ) {
					pphRate += animationInfo.weight;
				}
			}
			if( pphRate <= Mathf.Epsilon ) {
				return;
			}
		}
		
		float pphShoulderFixRate = this.pphShoulderFixRate * pphRate;
		
		for( int i = 0; i < _pphBones.Count; ++i ) {
			if( _pphBones[i].pphType == PPHType.Shoulder && this.pphShoulderEnabled ) {
				_UpdatePPHBone( _pphBones[i], pphShoulderFixRate );
			}
		}
	}
	
	static void _UpdatePPHBone( PPHBone pphBone, float fixRate )
	{
		if( pphBone == null || pphBone.target == null ) {
			return;
		}
		if( fixRate <= Mathf.Epsilon ) {
			return;
		}
		Quaternion rotation = pphBone.target.transform.localRotation;
		if( Mathf.Abs(rotation.x) <= Mathf.Epsilon &&
		   Mathf.Abs(rotation.y) <= Mathf.Epsilon &&
		   Mathf.Abs(rotation.z) <= Mathf.Epsilon &&
		   Mathf.Abs(rotation.w - 1.0f) <= Mathf.Epsilon ) {
			return;
		}
		
		pphBone.SnapshotChildRotations();
		
		if( fixRate >= 1.0f - Mathf.Epsilon ) {
			pphBone.target.transform.localRotation = Quaternion.identity;
		} else {
			rotation = Quaternion.Slerp( rotation, Quaternion.identity, fixRate );
			pphBone.target.transform.localRotation = rotation;
		}
		
		pphBone.RestoreChildRotations();
	}
}
