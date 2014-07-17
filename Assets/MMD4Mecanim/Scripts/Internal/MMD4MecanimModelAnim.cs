using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using MorphCategory				= MMD4MecanimData.MorphCategory;
using MorphType					= MMD4MecanimData.MorphType;
using MorphAutoLumninousType	= MMD4MecanimData.MorphAutoLumninousType;
using MorphData					= MMD4MecanimData.MorphData;
using MorphMotionData			= MMD4MecanimData.MorphMotionData;
using BoneData					= MMD4MecanimData.BoneData;
using RigidBodyData				= MMD4MecanimData.RigidBodyData;

using Bone						= MMD4MecanimBone;

public partial class MMD4MecanimModel
{
	void _InitializeAnimatoion()
	{
		this._animator = this.GetComponent< Animator >();
		
		_animMorphCategoryWeights = new float[(int)MorphCategory.Max];
		
		if( !Application.isPlaying ) {
			return; // for Editor
		}
		
		if( _modelData == null ) {
			return;
		}
		
		bool isEnableAudioClip = false;
		if( this.animList != null ) {
			for( int i = 0; i < this.animList.Length; ++i ) {
				if( this.animList[i] == null ) {
					continue;
				}
				
				isEnableAudioClip |= (this.animList[i].audioClip != null);
				
				if( this.animList[i].animFile == null ) {
					Debug.LogWarning( this.gameObject.name + ":animFile is nothing." );
					continue;
				}
				
				this.animList[i].animData = MMD4MecanimData.BuildAnimData( this.animList[i].animFile );
				if( this.animList[i].animData == null ) {
					Debug.LogError( this.gameObject.name + ":animFile is unsupported format." );
					continue;
				}
				
				this.animList[i].animatorStateNameHash = Animator.StringToHash( this.animList[i].animatorStateName );
				
				MMD4MecanimData.MorphMotionData[] morphMotionData = this.animList[i].animData.morphMotionDataList;
				if( morphMotionData != null ) {
					this.animList[i].morphMotionList = new MMD4MecanimModel.Anim.MorphMotion[morphMotionData.Length];
					
					for( int n = 0; n < morphMotionData.Length; ++n ) {
						this.animList[i].morphMotionList[n].morph = this.GetMorph( morphMotionData[n].name, false );
					}
					for( int n = 0; n < morphMotionData.Length; ++n ) {
						if( this.animList[i].morphMotionList[n].morph == null ) {
							Morph morph = this.GetMorph( morphMotionData[n].name, true );
							if( morph != null ) {
								bool findAnything = false;
								for( int m = 0; m < morphMotionData.Length && !findAnything; ++m ) {
									findAnything = (this.animList[i].morphMotionList[m].morph == morph);
								}
								if( !findAnything ) {
									this.animList[i].morphMotionList[n].morph = morph;
								}
							}
						}
					}
				}
			}
		}
		
		if( isEnableAudioClip ) {
			GetAudioSource();
		}
	}
	
	void _UpdateAnim()
	{
		_currentAnim = null;
		if( !this.animEnabled ) {
			return;
		}
		if( this._animator != null && this.animList != null ) {
			AnimatorStateInfo animatorStateInfo = this._animator.GetCurrentAnimatorStateInfo(0);
			
			int nameHash = animatorStateInfo.nameHash;
			float animationTime = animatorStateInfo.normalizedTime * animatorStateInfo.length;
			float f_animationFrameNo = animationTime * 30.0f;
			int animationFrameNo = (int)f_animationFrameNo;
			
			for( int i = 0; i < this.animList.Length; ++i ) {
				_UpdateAnim( this.animList[i], nameHash, animationTime, f_animationFrameNo, animationFrameNo );
			}
		}
	}
	
	void _UpdateAnim( MMD4MecanimModel.Anim animation, int nameHash, float animationTime, float f_frameNo, int frameNo )
	{
		if( animation == null ) {
			return;
		}
		if( string.IsNullOrEmpty(animation.animatorStateName) || animation.animatorStateNameHash != nameHash ) {
			return;
		}
		
		_currentAnim = animation;
		if( _playingAudioAnim != null && _playingAudioAnim != _currentAnim ) {
			if( this.audioSource != null ) {
				if( this.audioSource.clip == _playingAudioAnim.audioClip ) {
					this.audioSource.Stop();
					this.audioSource.clip = null;
				}
			}
			_playingAudioAnim = null;
		}
		
		if( _playingAudioAnim == null && _currentAnim.audioClip != null ) {
			_playingAudioAnim = _currentAnim;
			if( this.audioSource != null ) {
				if( this.audioSource.clip != _playingAudioAnim.audioClip ) {
					this.audioSource.clip = _playingAudioAnim.audioClip;
					this.audioSource.Play();
				} else {
					if( !this.audioSource.isPlaying ) {
						this.audioSource.Play();
					}
				}
			}
		}
		if( _currentAnim.audioClip != null && this.animSyncToAudio ) {
			if( this.audioSource != null && this.audioSource.isPlaying ) {
				float audioTime = this.audioSource.time;
				if( audioTime == 0.0f ) { // Support for delayed.
					_animator.speed = 0.0f;
				} else {
					float deltaTime = (_prevDeltaTime + Time.deltaTime) * 0.5f;
					float diffTime = audioTime - animationTime;
					if( Mathf.Abs( diffTime ) <= deltaTime ) {
						_animator.speed = 1.0f;
						//Debug.Log( "Safe" );
					} else {
						if( deltaTime > Mathf.Epsilon ) {
							float targetSpeed = 1.0f + diffTime / deltaTime;
							targetSpeed = Mathf.Clamp( targetSpeed, 0.5f, 2.0f );
							if( _animator.speed == 0.0f ) {
								_animator.speed = targetSpeed;
							} else {
								_animator.speed = _animator.speed * 0.95f + targetSpeed * 0.05f;
							}
						} else {
							_animator.speed = 1.0f;
						}
						//Debug.Log( "Unsafe:" + diffTime + ":" + deltaTime + ":" + (diffTime / deltaTime) + ":" + _animator.speed );
					}
				}
			} else {
				_animator.speed = 1.0f;
			}
		}
		
		if( animation.morphMotionList != null && animation.animData != null && animation.animData.morphMotionDataList != null ) {
			for( int i = 0; i < animation.morphMotionList.Length; ++i ) {
				MMD4MecanimModel.Anim.MorphMotion morphMotion = animation.morphMotionList[i];
				MorphMotionData morphMotionData = animation.animData.morphMotionDataList[i];
				if( morphMotion.morph == null ) {
					continue;
				}
				
				if( morphMotionData.frameNos == null ||
				   morphMotionData.f_frameNos == null ||
				   morphMotionData.weights == null ) {
					continue;
				}
				
				if( morphMotion.lastKeyFrameIndex < morphMotionData.frameNos.Length &&
				   morphMotionData.frameNos[morphMotion.lastKeyFrameIndex] > frameNo ) {
					morphMotion.lastKeyFrameIndex = 0;
				}
				
				bool isProcessed = false;
				for( int keyFrameIndex = morphMotion.lastKeyFrameIndex; keyFrameIndex < morphMotionData.frameNos.Length; ++keyFrameIndex ) {
					int keyFrameNo = morphMotionData.frameNos[keyFrameIndex];
					if( frameNo >= keyFrameNo ) {
						morphMotion.lastKeyFrameIndex = keyFrameIndex;
					} else {
						if( morphMotion.lastKeyFrameIndex + 1 < morphMotionData.frameNos.Length ) {
							_ProcessKeyFrame2( morphMotion.morph, morphMotionData,
							                  morphMotion.lastKeyFrameIndex + 0,
							                  morphMotion.lastKeyFrameIndex + 1,
							                  frameNo, f_frameNo );
							isProcessed = true;
						}
						break;
					}
				}
				if( !isProcessed ) {
					if( morphMotion.lastKeyFrameIndex < morphMotionData.frameNos.Length ) {
						_ProcessKeyFrame( morphMotion.morph, morphMotionData,
						                 morphMotion.lastKeyFrameIndex );
					}
				}
			}
		}
	}
	
	void _UpdateAnim2()
	{
		if( _playingAudioAnim != null && _currentAnim == null ) {
			if( this.audioSource != null ) {
				if( this.audioSource.clip == _playingAudioAnim.audioClip ) {
					this.audioSource.Stop();
					this.audioSource.clip = null;
				}
			}
			if( _playingAudioAnim.audioClip != null && this.animSyncToAudio ) {
				_animator.speed = 1.0f;
			}
			_playingAudioAnim = null;
		}
	}
	
	void _ProcessKeyFrame2(
		Morph morph, MorphMotionData motionMorphData,
		int keyFrameIndex0,
		int keyFrameIndex1,
		int frameNo, float f_frameNo )
	{
		int frameNo0 = motionMorphData.frameNos[keyFrameIndex0];
		int frameNo1 = motionMorphData.frameNos[keyFrameIndex1];
		float f_frameNo0 = motionMorphData.f_frameNos[keyFrameIndex0];
		float f_frameNo1 = motionMorphData.f_frameNos[keyFrameIndex1];
		if( frameNo <= frameNo0 || frameNo1 - frameNo0 == 1 ) { /* memo: Don't interpolate adjacent keyframes. */
			morph._animWeight = motionMorphData.weights[keyFrameIndex0];
		} else if( frameNo >= frameNo1 ) {
			morph._animWeight = motionMorphData.weights[keyFrameIndex1];
		} else {
			float r1 = (f_frameNo - f_frameNo0) / (f_frameNo1 - f_frameNo0);
			r1 = Mathf.Clamp( r1, 0.0f, 1.0f );
			float r0 = 1.0f - r1;
			morph._animWeight =
				motionMorphData.weights[keyFrameIndex0] * r0 +
					motionMorphData.weights[keyFrameIndex1] * r1;
		}
	}
	
	void _ProcessKeyFrame( Morph morph, MorphMotionData motionMorphData, int keyFrameIndex )
	{
		morph._animWeight = motionMorphData.weights[keyFrameIndex];
	}
}
