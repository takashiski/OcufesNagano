//#define MMD4MECANIM_KEEPIKTARGETBONE // Comment out as simulated MMD(Not keeped ik target bone.)

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
using IKData			= MMD4MecanimData.IKData;
using IKLinkData		= MMD4MecanimData.IKLinkData;
using FileType			= MMD4MecanimData.FileType;

using Bone				= MMD4MecanimBone;

// Pending: Support IK Rotation morphing.

public partial class MMD4MecanimModel
{
	public class IK
	{
		public const float	IKSkipDistance = 1e-13f;

		public const bool	IKInnerLockEnabled = true;
		public const bool	IKInnerLockKneeEnabled = true;
		public const float	IKInnerLockKneeClamp = 1.0f / 16.0f;
		public const float	IKInnerLockKneeRatioU = 0.1f;
		public const float	IKInnerLockKneeRatioL = 0.4f;
		public const float	IKInnerLockKneeScale = 8.0f;

		public const bool	IKMuscleEnabled = true;
		public const bool	IKMuscleHipEnabled = true;
		public const bool	IKMuscleFootEnabled = true;

		public const float	IKMuscleHipUpperXAngle = 176.0f;
		public const float	IKMuscleHipLowerXAngle = 86.0f;
		public const float	IKMuscleHipInnerYAngle = 45.0f;
		public const float	IKMuscleHipOuterYAngle = 90.0f;
		public const float	IKMuscleHipInnerZAngle = 30.0f;
		public const float	IKMuscleHipOuterZAngle = 90.0f;

		public const float	IKMuscleFootUpperXAngle = 70.0f;
		public const float	IKMuscleFootLowerXAngle = 90.0f;
		public const float	IKMuscleFootInnerYAngle = 25.0f;
		public const float	IKMuscleFootOuterYAngle = 25.0f;
		public const float	IKMuscleFootInnerZAngle = 12.5f;
		public const float	IKMuscleFootOuterZAngle = 0.0f;

		public static readonly Vector3 IKMuscleLeftHipLowerAngle = new Vector3( -IKMuscleHipUpperXAngle, -IKMuscleHipOuterYAngle, -IKMuscleHipOuterZAngle );
		public static readonly Vector3 IKMuscleLeftHipUpperAngle = new Vector3(  IKMuscleHipLowerXAngle,  IKMuscleHipInnerYAngle,  IKMuscleHipInnerZAngle );
		public static readonly Vector3 IKMuscleRightHipLowerAngle = new Vector3( -IKMuscleHipUpperXAngle, -IKMuscleHipInnerYAngle, -IKMuscleHipInnerZAngle );
		public static readonly Vector3 IKMuscleRightHipUpperAngle = new Vector3(  IKMuscleHipLowerXAngle,  IKMuscleHipOuterYAngle,  IKMuscleHipOuterZAngle );

		public static readonly Vector3 IKMuscleLeftFootLowerAngle = new Vector3( -IKMuscleFootUpperXAngle, -IKMuscleFootOuterYAngle, -IKMuscleFootOuterZAngle );
		public static readonly Vector3 IKMuscleLeftFootUpperAngle = new Vector3(  IKMuscleFootLowerXAngle,  IKMuscleFootInnerYAngle,  IKMuscleFootInnerZAngle );
		public static readonly Vector3 IKMuscleRightFootLowerAngle = new Vector3( -IKMuscleFootUpperXAngle, -IKMuscleFootInnerYAngle, -IKMuscleFootInnerZAngle );
		public static readonly Vector3 IKMuscleRightFootUpperAngle = new Vector3(  IKMuscleFootLowerXAngle,  IKMuscleFootOuterYAngle,  IKMuscleFootOuterZAngle );

		public enum IKAxis
		{
			None,
			X,
			Y,
			Z,
			Free,
		}

		MMD4MecanimModel	_model;
		FileType			_fileType;
		int					_ikID;
		IKData				_ikData;

		public int ikID { get { return _ikID; } }
		public IKData ikData { get { return _ikData; } }

		public class IKLink
		{
			public IKLinkData	ikLinkData;
			public IKAxis		ikAxis;
			public Bone			bone;
		}

		Bone				_destBone;
		Bone				_targetBone;
		IKLink[]			_ikLinkList;

		public Bone destBone { get { return _destBone; } }
		public Bone targetBone { get { return _targetBone; } }
		public IKLink[] ikLinkList { get { return _ikLinkList; } }

		public bool ikEnabled {
			get {
				if(_destBone != null ) {
					return _destBone.ikEnabled;
				}
				return false;
			}
			set {
				if( _destBone != null ) {
					_destBone.ikEnabled = value;
				}
			}
		}

		public float ikWeight {
			get {
				if(_destBone != null ) {
					return _destBone.ikWeight;
				}
				return 0.0f;
			}
			set {
				if( _destBone != null ) {
					_destBone.ikWeight = value;
				}
			}
		}

		public GameObject ikGoal {
			get {
				if(_destBone != null ) {
					return _destBone.ikGoal;
				}
				return null;
			}
			set {
				if( _destBone != null ) {
					_destBone.ikGoal = value;
				}
			}
		}
		
		public IK( MMD4MecanimModel model, int ikID )
		{
			if( model == null || model.modelData == null || model.modelData.ikDataList == null ||
			    ikID >= model.modelData.ikDataList.Length ) {
				Debug.LogError("");
				return;
			}
			
			_model	= model;
			_ikID	= ikID;
			_ikData	= model.modelData.ikDataList[ikID];
			if( _model.modelData != null ) {
				_fileType = _model.modelData.fileType;
			}

			if( _ikData != null ) {
				_destBone = model.GetBone( _ikData.destBoneID );
				_targetBone = model.GetBone( _ikData.targetBoneID );
				if( _ikData.ikLinkDataList != null ) {
					_ikLinkList = new IKLink[_ikData.ikLinkDataList.Length];
					for( int i = 0; i < _ikData.ikLinkDataList.Length; ++i ) {
						_ikLinkList[i] = new IKLink();
						_ikLinkList[i].ikLinkData = _ikData.ikLinkDataList[i];
						if( _ikLinkList[i].ikLinkData != null ) {
							_ikLinkList[i].bone = model.GetBone( _ikLinkList[i].ikLinkData.ikLinkBoneID );
							Vector3 lowerLimit = _ikLinkList[i].ikLinkData.lowerLimitAsDegree;
							Vector3 upperLimit = _ikLinkList[i].ikLinkData.upperLimitAsDegree;
							if( MMD4MecanimCommon.FuzzyZero(lowerLimit[1]) && MMD4MecanimCommon.FuzzyZero(upperLimit[1]) &&
							    MMD4MecanimCommon.FuzzyZero(lowerLimit[2]) && MMD4MecanimCommon.FuzzyZero(upperLimit[2]) ) {
								_ikLinkList[i].ikAxis = IKAxis.X;
							} else if( MMD4MecanimCommon.FuzzyZero(lowerLimit[0]) && MMD4MecanimCommon.FuzzyZero(upperLimit[0]) &&
							           MMD4MecanimCommon.FuzzyZero(lowerLimit[2]) && MMD4MecanimCommon.FuzzyZero(upperLimit[2]) ) {
								_ikLinkList[i].ikAxis = IKAxis.Y;
							} else if( MMD4MecanimCommon.FuzzyZero(lowerLimit[0]) && MMD4MecanimCommon.FuzzyZero(upperLimit[0]) &&
							           MMD4MecanimCommon.FuzzyZero(lowerLimit[1]) && MMD4MecanimCommon.FuzzyZero(upperLimit[1]) ) {
								_ikLinkList[i].ikAxis = IKAxis.Z;
							} else {
								_ikLinkList[i].ikAxis = IKAxis.Free;
							}
						}
					}
				}
			}
		}

		bool _Precheck()
		{
			if( _model == null || _ikData == null || _ikLinkList == null ) {
				return false;
			}
			if( _destBone == null || _destBone.gameObject == null || _targetBone == null || _targetBone.gameObject == null ) {
				return false;
			}
			if( _destBone.isRigidBodySimulated ) {
				return false;
			}
			
			for( int i = 0; i < _ikLinkList.Length; ++i ) {
				if( _ikLinkList[i].ikLinkData == null ||
				   _ikLinkList[i].bone == null ||
				   _ikLinkList[i].bone.boneData == null ||
				   _ikLinkList[i].bone.gameObject == null ) {
					return false;
				}
				if( _ikLinkList[i].bone.isRigidBodySimulated ) {
					return false;
				}
			}
			
			return true;
		}

		public void MarkProcessingTransform()
		{
			if( _destBone != null ) {
				_destBone._MarkProcessingTransform();
			}
			if( _targetBone != null ) {
				_targetBone._MarkProcessingTransform();
			}
			if( _ikLinkList != null ) {
				for( int i = 0; i < _ikLinkList.Length; ++i ) {
					if( _ikLinkList[i].bone != null ) {
						_ikLinkList[i].bone._MarkProcessingTransform();
					}
				}
			}
		}

		public void MarkIKDepended()
		{
			if( !_Precheck() || !this.ikEnabled ) {
				return;
			}

			float ikWeight = this.ikWeight;

			if( _destBone != null ) {
				_destBone.isIKDepended = true;
				_destBone.feedbackIKWeight = Mathf.Max(_destBone.feedbackIKWeight, ikWeight);
			}
			if( _targetBone != null ) {
				_targetBone.isIKDepended = true;
				_targetBone.feedbackIKWeight = Mathf.Max(_targetBone.feedbackIKWeight, ikWeight);
			}
			if( _ikLinkList != null ) {
				for( int i = 0; i < _ikLinkList.Length; ++i ) {
					if( _ikLinkList[i].bone != null ) {
						_ikLinkList[i].bone.isIKDepended = true;
						_ikLinkList[i].bone.feedbackIKWeight = Mathf.Max(_ikLinkList[i].bone.feedbackIKWeight, ikWeight);
					}
				}
			}
		}

		void _PrepareIKTransform()
		{
			if( !this.ikEnabled ) {
				return;
			}

			if( _destBone != null ) {
				_destBone._PrepareIKTransform();
			}
			if( _targetBone != null ) {
				_targetBone._PrepareIKTransform();
			}
			if( _ikLinkList != null ) {
				for( int i = 0; i < _ikLinkList.Length; ++i ) {
					if( _ikLinkList[i].bone != null ) {
						_ikLinkList[i].bone._PrepareIKTransform();
					}
				}
			}
		}

		public void Destroy()
		{
			_model = null;
			_ikData = null;
			_destBone = null;
			_targetBone = null;
		}

		public void Solve()
		{
			if( _destBone == null || !_destBone.ikEnabled ) {
				return;
			}
			if( _destBone.ikGoal != null ) {
				_destBone.transform.position = _destBone.ikGoal.transform.position;
			}

			_PrepareIKTransform();

			if( _fileType == FileType.PMD ) {
				SolvePMD();
			} else if( _fileType == FileType.PMX ) {
				SolvePMX();
			}
		}

		static void _InnerLockR( ref float lowerAngle, ref float upperAngle, float innerLockScale )
		{
			float lm = Mathf.Max( (upperAngle - lowerAngle) * innerLockScale, 0.0f );
			float l = lowerAngle + lm; // Anti Gimbal Lock for 1st phase.(Inner lcok)
			float u = upperAngle - lm; // Anti Gimbal Lock for 1st phase.(Inner lcok)
			lowerAngle = l;
			upperAngle = u;
		}

		static void _InnerLockR( ref Vector3 lowerAngle, ref Vector3 upperAngle, float innerLockScale )
		{
			_InnerLockR( ref lowerAngle.x, ref upperAngle.x, innerLockScale );
			_InnerLockR( ref lowerAngle.y, ref upperAngle.y, innerLockScale );
			_InnerLockR( ref lowerAngle.z, ref upperAngle.z, innerLockScale );
		}

		static void _IKMuscle( Bone bone, Quaternion q, int ite, Vector3 lowerAngle, Vector3 upperAngle )
		{
			// Memo: Not use innerLock.
			Vector3 eulerAngles = Vector3.zero;
			if( ite == 0 ) {
				q = bone.originalLocalRotation * q;
				eulerAngles = MMD4MecanimCommon.NormalizeAsDegree( q.eulerAngles );
			} else { // Fix for Unity.(Unstable eulerAngles near 90.)
				eulerAngles = q.eulerAngles;
				eulerAngles.x = MMD4MecanimCommon.NormalizeAsDegree( eulerAngles.x ) + bone.ikEulerAngles.x;
				eulerAngles.y = MMD4MecanimCommon.NormalizeAsDegree( eulerAngles.y ) + bone.ikEulerAngles.y;
				eulerAngles.z = MMD4MecanimCommon.NormalizeAsDegree( eulerAngles.z ) + bone.ikEulerAngles.z;
			}
			eulerAngles.x = _ClampEuler( eulerAngles.x, lowerAngle.x, upperAngle.x, ite == 0 );
			eulerAngles.y = _ClampEuler( eulerAngles.y, lowerAngle.y, upperAngle.y, ite == 0 );
			eulerAngles.z = _ClampEuler( eulerAngles.z, lowerAngle.z, upperAngle.z, ite == 0 );

			bone.ikEulerAngles = eulerAngles;
			bone._SetLocalRotationFromIK( Quaternion.Euler( eulerAngles ) );
		}

		static bool _GetIKMuscle( Bone bone, ref Vector3 lowerAngle, ref Vector3 upperAngle )
		{
			if( IKMuscleEnabled && IKMuscleHipEnabled && bone._skeletonType == Bone._SkeletonType.LeftHip ) {
				lowerAngle = IKMuscleLeftHipLowerAngle;
				upperAngle = IKMuscleLeftHipUpperAngle;
				return true;
			} else if( IKMuscleEnabled && IKMuscleHipEnabled && bone._skeletonType == Bone._SkeletonType.RightHip ) {
				lowerAngle = IKMuscleRightHipLowerAngle;
				upperAngle = IKMuscleRightHipUpperAngle;
				return true;
			} else if( IKMuscleEnabled && IKMuscleFootEnabled && bone._skeletonType == Bone._SkeletonType.LeftFoot ) {
				lowerAngle = IKMuscleLeftFootLowerAngle;
				upperAngle = IKMuscleLeftFootUpperAngle;
				return true;
			} else if( IKMuscleEnabled && IKMuscleFootEnabled && bone._skeletonType == Bone._SkeletonType.RightFoot ) {
				lowerAngle = IKMuscleRightFootLowerAngle;
				upperAngle = IKMuscleRightFootUpperAngle;
				return true;
			} else {
				return false;
			}
		}

		static void _IKMuscle( Bone bone, Quaternion q, int ite )
		{
			Vector3 lowerAngle = Vector3.zero;
			Vector3 upperAngle = Vector3.zero;
			if( _GetIKMuscle( bone, ref lowerAngle, ref upperAngle ) ) {
				_IKMuscle( bone, q, ite, lowerAngle, upperAngle );
			} else {
				bone._ApplyIKRotation( q );
			}
		}

		float _GetInnerLockKneeAngleR()
		{
			int ikLinkListLength = _ikLinkList.Length;
			Vector3 destPos = _destBone.gameObject.transform.position;

			Vector3 targetPos = _targetBone.gameObject.transform.position;
			Vector3 rootPos = _ikLinkList[ikLinkListLength - 1].bone.transform.position;
			float length0 = (destPos - rootPos).sqrMagnitude;
			float length1 = (targetPos - rootPos).sqrMagnitude;

			float innerAngleR = 0.0f;
			if( length1 > Mathf.Epsilon ) {
				if( length0 < length1 ) {
					float r = length0 / length1;
					if( r > 1.0f - IKInnerLockKneeRatioL ) {
						innerAngleR = (r - (1.0f - IKInnerLockKneeRatioL)) / IKInnerLockKneeRatioL;
					}
				} else if( length0 - length1 < length1 * IKInnerLockKneeRatioU ) {
					innerAngleR = 1.0f - (length0 - length1) / (length1 * IKInnerLockKneeRatioU);
				}
				innerAngleR = Mathf.Clamp01( innerAngleR * IKInnerLockKneeScale );
			}
			return innerAngleR;
		}

		public void SolvePMD()
		{
			if( !_Precheck() ) {
				return;
			}

			#if MMD4MECANIM_KEEPIKTARGETBONE
			Quaternion targetRotation = _targetBone.gameObject.transform.localRotation;
			#endif

			Vector3 destPos = _destBone.gameObject.transform.position;
			float angleConstraint = _ikData.angleConstraint;
			int ikLinkListLength = _ikLinkList.Length;
			int iteration = _ikData.iteration;

			if( ikLinkListLength == 0 || iteration <= 0 ) {
				return;
			}

			float innerLockKneeAngleR = 0.0f;
			if( IKInnerLockEnabled && IKInnerLockKneeEnabled ) {
				bool isKnee = false;
				for( int i = 0; i < ikLinkListLength && !isKnee; ++i ) {
					isKnee = _ikLinkList[i].bone.boneData.isKnee;
				}
				if( isKnee ) {
					innerLockKneeAngleR = _GetInnerLockKneeAngleR() * IKInnerLockKneeClamp;
				}
			}

			for( int ite = 0; ite < iteration; ++ite ) {
				bool processedAnything = false;
				for( int i = 0; i < ikLinkListLength; ++i ) {
					Vector3 targetPos = _targetBone.gameObject.transform.position;
					
					Vector3 localDestVec = destPos;
					Vector3 localTargetVec = targetPos;
					
					{
						Matrix4x4 inverseTransform = _ikLinkList[i].bone.originalWorldToLocalMatrix;
						localDestVec = inverseTransform.MultiplyPoint3x4( localDestVec );
						localTargetVec = inverseTransform.MultiplyPoint3x4( localTargetVec );
						localDestVec.Normalize();
						localTargetVec.Normalize();
						Vector3 tempVec = localDestVec - localTargetVec;
						if( Vector3.Dot( tempVec, tempVec ) < IKSkipDistance ) {
							continue;
						}
					}

					processedAnything = true;
					Vector3 axis = Vector3.Cross( localTargetVec, localDestVec );
					if( _ikLinkList[i].bone.boneData.isKnee ) {
						if( axis.x >= 0.0f ) {
							axis.Set( 1.0f, 0.0f, 0.0f );
						} else {
							axis.Set( -1.0f, 0.0f, 0.0f );
						}
					} else {
						axis.Normalize();
					}
					
					float dot = Vector3.Dot( localTargetVec, localDestVec );
					dot = Mathf.Clamp( dot, -1.0f, 1.0f );
					
					float rx = Mathf.Acos(dot) * 0.5f;
					rx = Mathf.Min( rx, angleConstraint * (float)((i + 1) * 2) );

					float rs = Mathf.Sin( rx );
					
					Quaternion q = Quaternion.identity;
					q.x = axis.x * rs;
					q.y = axis.y * rs;
					q.z = axis.z * rs;
					q.w = Mathf.Cos( rx );
					
					if( _ikLinkList[i].bone.boneData.isKnee ) {
						bool inverseAngle = (ite == 0);
						Vector3 eulerAngles = Vector3.zero;
						float lowerAngle = 0.5f;
						float upperAngle = 180.0f;

						Vector3 muscleLowerAngle = Vector3.zero;
						Vector3 muscleUpperAngle = Vector3.zero;
						if( _GetIKMuscle( _ikLinkList[i].bone, ref muscleLowerAngle, ref muscleUpperAngle ) ) {
							lowerAngle = Mathf.Max( lowerAngle, muscleLowerAngle.x );
							upperAngle = Mathf.Min( upperAngle, muscleUpperAngle.x );
						}

						if( ite == 0 ) {
							if( IKInnerLockEnabled && IKInnerLockKneeEnabled ) {
								_InnerLockR( ref lowerAngle, ref upperAngle, innerLockKneeAngleR );
							}
							q = _ikLinkList[i].bone.originalLocalRotation * q;
							eulerAngles = MMD4MecanimCommon.NormalizeAsDegree( q.eulerAngles );
						} else { // Fix for Unity.(Unstable eulerAngles near 90.)
							eulerAngles = q.eulerAngles;
							eulerAngles.x = MMD4MecanimCommon.NormalizeAsDegree( eulerAngles.x ) + _ikLinkList[i].bone.ikEulerAngles.x;
						}
						eulerAngles.x = _ClampEuler( eulerAngles.x, lowerAngle, upperAngle, inverseAngle );
						eulerAngles.y = 0.0f;
						eulerAngles.z = 0.0f;
						_ikLinkList[i].bone.ikEulerAngles = eulerAngles;
						_ikLinkList[i].bone._SetLocalRotationFromIK( Quaternion.Euler( eulerAngles ) );
					} else {
						_IKMuscle( _ikLinkList[i].bone, q, ite );
					}
				}
				if( !processedAnything ) {
					break;
				}
			}
			
			#if MMD4MECANIM_KEEPIKTARGETBONE
			_targetBone.gameObject.transform.localRotation = targetRotation;
			#endif
		}

		static float _ClampEuler(float r, float lower, float upper, bool inverse)
		{
			if( r < lower ) {
				if( inverse ) {
					float inv = lower * 2.0f - r;
					if( inv <= upper ) {
						return inv;
					}
				}
				return lower;
			} else if( r > upper ) {
				if( inverse ) {
					float inv = upper * 2.0f - r;
					if( inv >= lower ) {
						return inv;
					}
				}
				
				return upper;
			}
			
			return r;
		}

		public void SolvePMX()
		{
			if( !_Precheck() ) {
				return;
			}

			#if MMD4MECANIM_KEEPIKTARGETBONE
			Quaternion targetRotation = _targetBone.gameObject.transform.localRotation;
			#endif

			Vector3 destPos = _destBone.gameObject.transform.position;
			float angleConstraint = _ikData.angleConstraint;
			int ikLinkListLength = _ikLinkList.Length;
			int iteration = _ikData.iteration;

			float innerLockKneeAngleR = 0.0f;
			if( IKInnerLockEnabled && IKInnerLockKneeEnabled ) {
				bool isKnee = false;
				for( int i = 0; i < ikLinkListLength && !isKnee; ++i ) {
					isKnee = _ikLinkList[i].bone.boneData.isKnee;
				}
				if( isKnee ) {
					innerLockKneeAngleR = _GetInnerLockKneeAngleR() * IKInnerLockKneeClamp;
				}
			}

			for( int ite = 0; ite < iteration; ++ite ) {
				bool processedAnything = false;
				for( int i = 0; i < ikLinkListLength; ++i ) {
					Vector3 targetPos = _targetBone.gameObject.transform.position;

					Vector3 localDestVec = destPos;
					Vector3 localTargetVec = targetPos;

					{
						Matrix4x4 inverseTransform = _ikLinkList[i].bone.originalWorldToLocalMatrix;
						localDestVec = inverseTransform.MultiplyPoint3x4( localDestVec );
						localTargetVec = inverseTransform.MultiplyPoint3x4( localTargetVec );
						localDestVec.Normalize();
						localTargetVec.Normalize();
						Vector3 tempVec = localDestVec - localTargetVec;
						if( Vector3.Dot( tempVec, tempVec ) < IKSkipDistance ) {
							continue;
						}
					}

					processedAnything = true;
					Vector3 axis = Vector3.Cross( localTargetVec, localDestVec );
					if( _ikLinkList[i].ikLinkData.hasAngleJoint ) {
						if( _ikLinkList[i].ikAxis == IKAxis.X ) {
							// X Limit
							if( axis.x >= 0.0f ) {
								axis.Set( 1.0f, 0.0f, 0.0f );
							} else {
								axis.Set( -1.0f, 0.0f, 0.0f );
							}
						} else if( _ikLinkList[i].ikAxis == IKAxis.Y ) {
							// Y Limit
							if( axis.y >= 0.0f ) {
								axis.Set( 0.0f, 1.0f, 0.0f );
							} else {
								axis.Set( 0.0f, -1.0f, 0.0f );
							}
						} else if(_ikLinkList[i].ikAxis == IKAxis.Z ) {
							// Z Limit
							if( axis.z >= 0.0f ) {
								axis.Set( 0.0f, 0.0f, 1.0f );
							} else {
								axis.Set( 0.0f, 0.0f, -1.0f );
							}
						} else {
							axis.Normalize();
						}
					} else {
						axis.Normalize();
					}

					float dot = Vector3.Dot( localTargetVec, localDestVec );
					dot = Mathf.Clamp( dot, -1.0f, 1.0f );

					float rx = Mathf.Acos(dot) * 0.5f;
					rx = Mathf.Min( rx, angleConstraint * (float)((i + 1) * 2) );
					
					float rs = Mathf.Sin( rx );

					Quaternion q = Quaternion.identity;
					q.x = axis.x * rs;
					q.y = axis.y * rs;
					q.z = axis.z * rs;
					q.w = Mathf.Cos( rx );

					bool inverseAngle = (ite == 0);
					if( _ikLinkList[i].ikLinkData.hasAngleJoint ) {
						Vector3 eulerAngles = Vector3.zero;
						if( ite == 0 ) {
							q = _ikLinkList[i].bone.originalLocalRotation * q;
							eulerAngles = MMD4MecanimCommon.NormalizeAsDegree( q.eulerAngles );
						} else { // Fix for Unity.(Unstable eulerAngles near 90.)
							eulerAngles = MMD4MecanimCommon.NormalizeAsDegree( q.eulerAngles );
							eulerAngles += _ikLinkList[i].bone.ikEulerAngles;
						}
						Vector3 lowerLimit = _ikLinkList[i].ikLinkData.lowerLimitAsDegree;
						Vector3 upperLimit = _ikLinkList[i].ikLinkData.upperLimitAsDegree;

						Vector3 muscleLowerAngle = Vector3.zero;
						Vector3 muscleUpperAngle = Vector3.zero;
						if( _GetIKMuscle( _ikLinkList[i].bone, ref muscleLowerAngle, ref muscleUpperAngle ) ) {
							lowerLimit = Vector3.Max( lowerLimit, muscleLowerAngle );
							upperLimit = Vector3.Min( upperLimit, muscleUpperAngle );
						}

						if( IKInnerLockEnabled && IKInnerLockEnabled && ite == 0 && _ikLinkList[i].bone.boneData.isKnee ) {
							_InnerLockR( ref lowerLimit, ref upperLimit, innerLockKneeAngleR );
						}
						if( _ikLinkList[i].ikAxis == IKAxis.X ) {
							// X Limit
							eulerAngles.x = _ClampEuler( eulerAngles.x, lowerLimit[0], upperLimit[0], inverseAngle );
							eulerAngles.y = 0.0f;
							eulerAngles.z = 0.0f;
						} else if( _ikLinkList[i].ikAxis == IKAxis.Y ) {
							// Y Limit
							eulerAngles.x = 0.0f;
							eulerAngles.y = _ClampEuler( eulerAngles.y, lowerLimit[1], upperLimit[1], inverseAngle );
							eulerAngles.z = 0.0f;
						} else if( _ikLinkList[i].ikAxis == IKAxis.Z ) {
							// Z Limit
							eulerAngles.x = 0.0f;
							eulerAngles.y = 0.0f;
							eulerAngles.z = _ClampEuler( eulerAngles.z, lowerLimit[2], upperLimit[2], inverseAngle );
						} else {
							// Anti Gimbal Lock
							if( lowerLimit.x >= -180.0f && upperLimit.x <= 180.0f ) {
								eulerAngles[0] = Mathf.Clamp( eulerAngles[0], -176.0f, 176.0f );
							} else if( lowerLimit.y >= -180.0f && upperLimit.y <= 180.0f ) {
								eulerAngles[1] = Mathf.Clamp( eulerAngles[1], -176.0f, 176.0f );
							} else {
								eulerAngles[2] = Mathf.Clamp( eulerAngles[2], -176.0f, 176.0f );
							}
							eulerAngles.x = _ClampEuler( eulerAngles.x, lowerLimit[0], upperLimit[0], inverseAngle );
							eulerAngles.y = _ClampEuler( eulerAngles.y, lowerLimit[1], upperLimit[1], inverseAngle );
							eulerAngles.z = _ClampEuler( eulerAngles.z, lowerLimit[2], upperLimit[2], inverseAngle );
						}
						_ikLinkList[i].bone.ikEulerAngles = eulerAngles;
						_ikLinkList[i].bone._SetLocalRotationFromIK( Quaternion.Euler( eulerAngles ) );
					} else {
						_IKMuscle( _ikLinkList[i].bone, q, ite );
					}
				}
				if( !processedAnything ) {
					break;
				}
			}

			#if MMD4MECANIM_KEEPIKTARGETBONE
			_targetBone.gameObject.transform.localRotation = targetRotation;
			#endif
		}
	}
}
