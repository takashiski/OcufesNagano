using UnityEngine;
using System.Collections;
using BulletXNA;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.LinearMath;

public class MMD4MecanimBulletPMXCommon
{
	// Pending: Replace to MMD4MecanimData
	public enum PMXFileType
	{
		None,
		PMD,
		PMX,
	}
	
	public enum PMDBoneType
	{
		Rotate,
		RotateAndMove,
		IKDestination,
		Unknown,
		UnderIK,
		UnderRotate,
		IKTarget,
		NoDisp,
		Twist,
		FollowRotate,
	}

	[System.Flags]
	public enum PMXBoneFlag
	{
		None						= 0,
		Destination					= 0x0001,
		Rotate						= 0x0002,
		Translate					= 0x0004,
		Visible						= 0x0008,
		Controllable				= 0x0010,
		IK							= 0x0020,
		IKChild						= 0x0040,
		InherenceLocal				= 0x0080,
		InherenceRotate				= 0x0100,
		InherenceTranslate			= 0x0200,
		FixedAxis					= 0x0400,
		LocalAxis					= 0x0800,
		TransformAfterPhysics		= 0x1000,
		TransformExternalParent		= 0x2000,
	}

	public enum PMXShapeType
	{
		Sphere,
		Box,
		Capsule,
	}
	
	public enum PMXRigidBodyType
	{
		Kinematics,
		Simulated,
		SimulatedAligned,
	}
}
