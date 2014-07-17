using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

using Morph				= UnityChanMorph.Morph;
using MorphCategory		= MMD4MecanimData.MorphCategory;
using MorphType			= MMD4MecanimData.MorphType;
using MorphData			= MMD4MecanimData.MorphData;
using MorphMotionData	= MMD4MecanimData.MorphMotionData;

public class MMD4MecanimUnityChanMorphHelper : MonoBehaviour
{
	public bool								playManually = false;
	public string							manualAnimatorStateName = "";
	public string							playingAnimatorStateName = "";

	public bool								animEnabled = true;
	public bool								animPauseOnEnd = false;
	public bool								initializeOnAwake = false;
	public bool								animSyncToAudio = true;
	public float							morphSpeed = 0.1f;

	[System.Serializable]
	public class Anim
	{
		public string						animatorStateName;
		public int							animatorStateNameHash;
		public TextAsset					animFile;
		public AudioClip					audioClip;
		
		[NonSerialized]
		public MMD4MecanimData.AnimData		animData;

		public struct MorphMotion
		{
			public Morph					morph;
			public int						lastKeyFrameIndex;
		}
		
		[NonSerialized]
		public MorphMotion[]				morphMotionList;
	}

	public Morph[]							morphList;
	public Anim[]							animList;

	private bool							_initialized;
	private UnityChanMorph					_unityChanMorph;
	private Animator						_animator;
	private AudioSource						_audioSource;
	private Anim							_currentAnim;
	private Anim							_playingAudioAnim;
	private float							_manualAnimTime;
	private float							_morphWeight;
	private float							_prevDeltaTime;
	private HashSet<Morph>					_inactiveModelMorphSet = new HashSet<Morph>();

	public void PlayManually( string animatorStateName )
	{
		this.playManually = true;
		this.manualAnimatorStateName = animatorStateName;
	}
	
	public void StopManually()
	{
		this.playManually = false;
		this.playingAnimatorStateName = "";
	}

	void Awake()
	{
		if( this.initializeOnAwake ) {
			_Initialize();
		}
	}

	void Start()
	{
		_Initialize();
	}

	void Update()
	{
		if( _prevDeltaTime == 0.0f ) {
			_prevDeltaTime = Time.deltaTime;
		}

		_UpdateAnim();
		_UpdateAnim2();
		_UpdateMorph();

		_prevDeltaTime = Time.deltaTime;
	}

	void _Initialize()
	{
		if( _initialized ) {
			return;
		}

		_initialized = true;
		_animator = this.gameObject.GetComponent< Animator >();
		_unityChanMorph = this.gameObject.GetComponent< UnityChanMorph >();
		if( _unityChanMorph == null ) {
			return;
		}

		bool isEnableAudioClip = false;
		if( this.animList != null ) {
			for( int i = 0; i < this.animList.Length; ++i ) {
				_InitializeAnim( this.animList[i] );
				isEnableAudioClip |= (this.animList[i].audioClip != null);
			}
		}

		if( isEnableAudioClip ) {
			_audioSource = this.gameObject.GetComponent<AudioSource>();
			if( _audioSource == null ) {
				_audioSource = this.gameObject.AddComponent<AudioSource>();
			}
		}
	}

	void _InitializeAnim( Anim anim )
	{
		if( anim == null || _unityChanMorph == null ) {
			return;
		}

		if( anim.animatorStateName != null ) {
			anim.animatorStateNameHash = Animator.StringToHash(anim.animatorStateName);
		}

		anim.animData = MMD4MecanimData.BuildAnimData( anim.animFile );
		if( anim.animData == null ) {
			return;
		}

		MMD4MecanimData.MorphMotionData[] morphMotionData = anim.animData.morphMotionDataList;
		if( morphMotionData != null ) {
			anim.morphMotionList = new Anim.MorphMotion[morphMotionData.Length];
			for( int n = 0; n < morphMotionData.Length; ++n ) {
				anim.morphMotionList[n].morph = _unityChanMorph.GetMorph( morphMotionData[n].name, false );
			}
			for( int n = 0; n < morphMotionData.Length; ++n ) {
				Morph morph = _unityChanMorph.GetMorph( morphMotionData[n].name, true );
				if( morph != null ) {
					bool findAnything = false;
					for( int m = 0; m < morphMotionData.Length && !findAnything; ++m ) {
						findAnything = (anim.morphMotionList[m].morph == morph);
					}
					if( !findAnything ) {
						anim.morphMotionList[n].morph = morph;
					}
				}
			}
		}
	}

	void _UpdateAnim()
	{
		if( !this.animEnabled ) {
			_StopAnim();
			return;
		}

		bool playingAnything = false;
		if( this.playManually ) {
			if( _currentAnim != null ) {
				if( string.IsNullOrEmpty(this.playingAnimatorStateName) ||
					_currentAnim.animatorStateName == null ||
				    _currentAnim.animatorStateName != this.playingAnimatorStateName ) {
					_StopAnim();
				}
			}

			if( _currentAnim == null && !string.IsNullOrEmpty(this.manualAnimatorStateName) ) {
				if( this.animList != null ) {
					for( int i = 0; i < this.animList.Length; ++i ) {
						if( this.animList[i].animatorStateName != null &&
							this.animList[i].animatorStateName == this.manualAnimatorStateName ) {
							_PlayAnim( this.animList[i] );
							break;
						}
					}
				}
			}

			if( _currentAnim != null ) {
				if( _currentAnim.animData != null ) {
					if( !this.animPauseOnEnd ) {
						if( _manualAnimTime == (float)(_currentAnim.animData.maxFrame / 30.0f) ) {
							_StopAnim();
						} else {
							playingAnything = true;
						}
					}
				} else {
					_StopAnim();
				}
			}
			
			if( playingAnything ) {
				float animationTime = _manualAnimTime;
				float f_frameNo = _manualAnimTime * 30.0f;
				int frameNo = (int)f_frameNo;
				_UpdateAnim( animationTime, f_frameNo, frameNo );

				// Sync with Manually.
				if( _currentAnim.audioClip != null && _audioSource != null && _audioSource.isPlaying && this.animSyncToAudio ) {
					_manualAnimTime = _audioSource.time;
				} else {
					_manualAnimTime += Time.deltaTime;
				}
				
				if( _currentAnim.animData != null ) {
					_manualAnimTime = Mathf.Min( _manualAnimTime, (float)(_currentAnim.animData.maxFrame / 30.0f) );
				} else {
					_manualAnimTime = 0.0f;
				}
			}
		} else {
			if( this._animator != null && this.animList != null ) {
				AnimatorStateInfo animatorStateInfo = this._animator.GetCurrentAnimatorStateInfo(0);
				
				int nameHash = animatorStateInfo.nameHash;
				float animationTime = animatorStateInfo.normalizedTime * animatorStateInfo.length;
				float f_animationFrameNo = animationTime * 30.0f;
				int animationFrameNo = (int)f_animationFrameNo;
				
				for( int i = 0; i < this.animList.Length; ++i ) {
					if( this.animList[i] != null && this.animList[i].animatorStateNameHash == nameHash ) {
						if( _currentAnim != this.animList[i] ) {
							_PlayAnim( this.animList[i] );
						}
						_UpdateAnim( animationTime, f_animationFrameNo, animationFrameNo );
						playingAnything = true;
					}
				}
			}
			if( !playingAnything ) {
				_StopAnim();
			}
		}
	}

	void _UpdateAnim( float animationTime, float f_frameNo, int frameNo )
	{
		if( _currentAnim == null ) {
			return;
		}

		if( _playingAudioAnim != null && _playingAudioAnim != _currentAnim ) {
			if( _audioSource != null ) {
				if( _audioSource.clip == _playingAudioAnim.audioClip ) {
					_audioSource.Stop();
					_audioSource.clip = null;
				}
			}
			_playingAudioAnim = null;
		}
		
		if( _playingAudioAnim == null && _currentAnim.audioClip != null ) {
			_playingAudioAnim = _currentAnim;
			if( _audioSource != null ) {
				if( _audioSource.clip != _playingAudioAnim.audioClip ) {
					_audioSource.clip = _playingAudioAnim.audioClip;
					_audioSource.Play();
				} else {
					if( !_audioSource.isPlaying ) {
						_audioSource.Play();
					}
				}
			}
		}

		// Sync with Animator.
		if( !this.playManually && _currentAnim.audioClip != null && this.animSyncToAudio ) {
			if( _audioSource != null && _audioSource.isPlaying ) {
				float audioTime = _audioSource.time;
				if( audioTime == 0.0f ) { // Support for delayed.
					_animator.speed = 0.0f;
				} else {
					float deltaTime = (_prevDeltaTime + Time.deltaTime) * 0.5f;
					float diffTime = audioTime - animationTime;
					if( Mathf.Abs( diffTime ) <= deltaTime ) {
						_animator.speed = 1.0f;
						//Debug.Log( "Safe" );
					} else {
						if( deltaTime > Mathf.Epsilon && deltaTime < 0.1f ) {
							float targetSpeed = 1.0f + diffTime / deltaTime;
							targetSpeed = Mathf.Clamp( targetSpeed, 0.5f, 2.0f );
							if( _animator.speed == 0.0f ) {
								_animator.speed = targetSpeed;
							} else {
								_animator.speed = _animator.speed * 0.95f + targetSpeed * 0.05f;
							}
						} else {
							//Debug.Log("Force synchronized.");
							_audioSource.time = animationTime; // Force synchronize.
							_animator.speed = 1.0f;
						}
						//Debug.Log( "Unsafe:" + diffTime + ":" + deltaTime + ":" + (diffTime / deltaTime) + ":" + _animator.speed );
					}
				}
			} else {
				_animator.speed = 1.0f;
			}
		}

		for( int i = 0; i < _currentAnim.morphMotionList.Length; ++i ) {
			Anim.MorphMotion morphMotion = _currentAnim.morphMotionList[i];
			MorphMotionData morphMotionData = _currentAnim.animData.morphMotionDataList[i];
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
			morph.weight = motionMorphData.weights[keyFrameIndex0];
		} else if( frameNo >= frameNo1 ) {
			morph.weight = motionMorphData.weights[keyFrameIndex1];
		} else {
			float r1 = (f_frameNo - f_frameNo0) / (f_frameNo1 - f_frameNo0);
			r1 = Mathf.Clamp( r1, 0.0f, 1.0f );
			float r0 = 1.0f - r1;
			morph.weight =
				motionMorphData.weights[keyFrameIndex0] * r0 +
					motionMorphData.weights[keyFrameIndex1] * r1;
		}
		if( _morphWeight != 1.0f ) {
			morph.weight *= _morphWeight;
		}
	}
	
	void _ProcessKeyFrame( Morph morph, MorphMotionData motionMorphData, int keyFrameIndex )
	{
		morph.weight = motionMorphData.weights[keyFrameIndex];
		if( _morphWeight != 1.0f ) {
			morph.weight *= _morphWeight;
		}
	}

	void _PlayAnim( Anim anim )
	{
		_StopAnim();

		_currentAnim = anim;
		_manualAnimTime = 0.0f;
		this.manualAnimatorStateName = "";
		if( anim != null ) {
			this.playingAnimatorStateName = anim.animatorStateName;
		}

		if( _currentAnim != null && _inactiveModelMorphSet != null ) {
			if( _currentAnim.morphMotionList != null ) {
				for( int i = 0; i < _currentAnim.morphMotionList.Length; ++i ) {
					_currentAnim.morphMotionList[i].lastKeyFrameIndex = 0;
					Morph morph = _currentAnim.morphMotionList[i].morph;
					if( morph != null ) {
						_inactiveModelMorphSet.Remove( morph );
					}
				}
			}
		}
	}

	void _StopAnim()
	{
		if( _currentAnim != null && _inactiveModelMorphSet != null ) {
			if( _currentAnim.morphMotionList != null ) {
				for( int i = 0; i < _currentAnim.morphMotionList.Length; ++i ) {
					_currentAnim.morphMotionList[i].lastKeyFrameIndex = 0;
					Morph morph = _currentAnim.morphMotionList[i].morph;
					if( morph != null && morph.weight != 0.0f ) {
						_inactiveModelMorphSet.Add( morph );
					}
				}
			}
			_currentAnim = null;
			_manualAnimTime = 0.0f;
			this.playingAnimatorStateName = "";
		}
	}

	void _UpdateAnim2()
	{
		if( _playingAudioAnim != null && _currentAnim == null ) {
			if( _audioSource != null ) {
				if( _audioSource.clip == _playingAudioAnim.audioClip ) {
					_audioSource.Stop();
					_audioSource.clip = null;
				}
			}
			_playingAudioAnim = null;
		}
	}

	void _UpdateMorph()
	{
		float stepValue = 1.0f;
		if( this.morphSpeed > 0.0f ) {
			stepValue = Time.deltaTime / this.morphSpeed;
		}

		if( _currentAnim != null ) {
			MMD4MecanimCommon.Approx( ref _morphWeight, 1.0f, stepValue );
		} else {
			MMD4MecanimCommon.Approx( ref _morphWeight, 0.0f, stepValue );
		}
		if( _inactiveModelMorphSet != null ) {
			foreach( var morph in _inactiveModelMorphSet ) {
				MMD4MecanimCommon.Approx( ref morph.weight, 0.0f, stepValue );
			}
			_inactiveModelMorphSet.RemoveWhere( s => s.weight == 0.0f );
		}
	}
}
