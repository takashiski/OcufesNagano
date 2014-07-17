using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

using Morph				= MMD4MecanimModel.Morph;
using MorphCategory		= MMD4MecanimData.MorphCategory;
using MorphType			= MMD4MecanimData.MorphType;
using MorphData			= MMD4MecanimData.MorphData;
using MorphMotionData	= MMD4MecanimData.MorphMotionData;

public class MMD4MecanimAnimMorphHelper : MonoBehaviour
{
	public string							animName = "";
	public string							playingAnimName = "";

	public bool								animEnabled = true;
	public bool								animPauseOnEnd = false;
	public bool								initializeOnAwake = false;
	public bool								animSyncToAudio = true;
	public float							morphSpeed = 0.1f;
	public bool								overrideWeight = false;

	[System.Serializable]
	public class Anim
	{
		public string						animName;
		public TextAsset					animFile;
		public AudioClip					audioClip;
		
		[NonSerialized]
		public MMD4MecanimData.AnimData		animData;

		public struct MorphMotion
		{
			public MMD4MecanimModel.Morph	morph;
			public int						lastKeyFrameIndex;
		}
		
		[NonSerialized]
		public MorphMotion[]				morphMotionList;
	}

	public Anim[]							animList;

	private bool							_initialized;
	private MMD4MecanimModel				_model;
	private AudioSource						_audioSource;
	private Anim							_currentAnim;
	private Anim							_playingAudioAnim;
	private float							_animTime;
	private float							_morphWeight;
	private float							_weight2;
	private HashSet<MMD4MecanimModel.Morph>	_inactiveModelMorphSet = new HashSet<MMD4MecanimModel.Morph>();

	public virtual bool isProcessing
	{
		get {
			if( _IsPlayingAnim() ) {
				return true;
			}
			if( _inactiveModelMorphSet.Count != 0 ) {
				return true;
			}
			
			return false;
		}
	}
	
	public virtual bool isAnimating
	{
		get {
			if( _IsPlayingAnim() ) {
				return true;
			}
			if( _inactiveModelMorphSet.Count != 0 ) {
				return true;
			}
			
			return false;
		}
	}

	public void PlayAnim( string animName )
	{
		this.animName = animName;
	}

	public void StopAnim()
	{
		this.playingAnimName = "";
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
		_UpdateAnim();
		_UpdateAnim2();
		_UpdateMorph();
	}

	void _Initialize()
	{
		if( _initialized ) {
			return;
		}

		_initialized = true;
		_model = this.gameObject.GetComponent< MMD4MecanimModel >();
		if( _model == null ) {
			return;
		}

		_model.Initialize();

		bool isEnableAudioClip = false;
		if( this.animList != null ) {
			for( int i = 0; i < this.animList.Length; ++i ) {
				_InitializeAnim( this.animList[i] );
				isEnableAudioClip |= (this.animList[i].audioClip != null);
			}
		}

		if( isEnableAudioClip ) {
			_audioSource = _model.GetAudioSource();
		}
	}

	void _InitializeAnim( Anim anim )
	{
		if( anim == null || _model == null || _model.modelData == null || _model.morphList == null ) {
			return;
		}

		anim.animData = MMD4MecanimData.BuildAnimData( anim.animFile );
		if( anim.animData == null ) {
			return;
		}

		MMD4MecanimData.MorphMotionData[] morphMotionData = anim.animData.morphMotionDataList;
		if( morphMotionData != null ) {
			anim.morphMotionList = new MMD4MecanimAnimMorphHelper.Anim.MorphMotion[morphMotionData.Length];
			for( int n = 0; n < morphMotionData.Length; ++n ) {
				anim.morphMotionList[n].morph = _model.GetMorph( morphMotionData[n].name, true );
			}
		}
	}

	void _UpdateAnim()
	{
		if( !this.animEnabled ) {
			_StopAnim();
			return;
		}
		if( _currentAnim != null ) {
			if( string.IsNullOrEmpty(this.playingAnimName) ||
			   _currentAnim.animName == null || this.playingAnimName != _currentAnim.animName ) {
				_StopAnim();
			}
		}
		if( _currentAnim == null && !string.IsNullOrEmpty(this.animName) ) {
			if( this.animList != null ) {
				for( int i = 0; i < this.animList.Length; ++i ) {
					if( this.animList[i].animName != null && this.animList[i].animName == this.animName ) {
						_PlayAnim( this.animList[i] );
						break;
					}
				}
			}
		}
		if( _currentAnim != null ) {
			if( _currentAnim.animData != null ) {
				if( !this.animPauseOnEnd ) {
					if( _animTime == (float)(_currentAnim.animData.maxFrame / 30.0f) ) {
						_StopAnim();
					}
				}
			} else {
				_StopAnim();
			}
		}
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

		float f_frameNo = _animTime * 30.0f;
		int frameNo = (int)f_frameNo;

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

		// Postfix for animWeight2
		if( _currentAnim.morphMotionList != null ) {
			for( int i = 0; i < _currentAnim.morphMotionList.Length; ++i ) {
				if( _currentAnim.morphMotionList[i].morph != null ) {
					_currentAnim.morphMotionList[i].morph.weight2 = _weight2;
				}
			}
		}

		if( _currentAnim.audioClip != null && _audioSource != null && _audioSource.isPlaying && this.animSyncToAudio ) {
			_animTime = _audioSource.time;
		} else {
			_animTime += Time.deltaTime;
		}

		if( _currentAnim.animData != null ) {
			_animTime = Mathf.Min( _animTime, (float)(_currentAnim.animData.maxFrame / 30.0f) );
		} else {
			_animTime = 0.0f;
		}
	}

	bool _IsPlayingAnim()
	{
		if( _currentAnim != null && _currentAnim.animData != null ) {
			return _animTime < (float)(_currentAnim.animData.maxFrame / 30.0f);
		}

		return false;
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
		_animTime = 0.0f;
		this.animName = "";
		if( anim != null ) {
			this.playingAnimName = anim.animName;
		}

		if( _currentAnim != null && _inactiveModelMorphSet != null ) {
			if( _currentAnim.morphMotionList != null ) {
				for( int i = 0; i < _currentAnim.morphMotionList.Length; ++i ) {
					_currentAnim.morphMotionList[i].lastKeyFrameIndex = 0;
					MMD4MecanimModel.Morph morph = _currentAnim.morphMotionList[i].morph;
					if( morph != null ) {
						_inactiveModelMorphSet.Remove( morph );
					}
				}
			}
		}
	}

	void _StopAnim()
	{
		this.playingAnimName = "";

		if( _currentAnim != null && _inactiveModelMorphSet != null ) {
			if( _currentAnim.morphMotionList != null ) {
				for( int i = 0; i < _currentAnim.morphMotionList.Length; ++i ) {
					_currentAnim.morphMotionList[i].lastKeyFrameIndex = 0;
					MMD4MecanimModel.Morph morph = _currentAnim.morphMotionList[i].morph;
					if( morph != null && (morph.weight != 0.0f || morph.weight2 != 0.0f) ) {
						_inactiveModelMorphSet.Add( morph );
					}
				}
			}
			_currentAnim = null;
			_animTime = 0.0f;
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
			MMD4MecanimCommon.Approx( ref _weight2, this.overrideWeight ? 1.0f : 0.0f, stepValue );
		} else {
			MMD4MecanimCommon.Approx( ref _weight2, 0.0f, stepValue );
		}
		if( _inactiveModelMorphSet != null ) {
			foreach( var morph in _inactiveModelMorphSet ) {
				MMD4MecanimCommon.Approx( ref morph.weight, 0.0f, stepValue );
				MMD4MecanimCommon.Approx( ref morph.weight2, 0.0f, stepValue );
			}
			_inactiveModelMorphSet.RemoveWhere( s => s.weight == 0.0f && s.weight2 == 0.0f );
		}
	}
}
