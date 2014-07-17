using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BulletXNA;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.LinearMath;

using PMXShapeType          = MMD4MecanimBulletPMXCommon.PMXShapeType;

public class MMD4MecanimBulletPhysicsUtil
{
	public static float NormalizeAngle( float r )
	{
		float cirHalf = 180.0f * Mathf.Deg2Rad;
		float cirFull = 360.0f * Mathf.Deg2Rad;

		if( r > cirHalf ) {
			while( r > cirHalf ) {
				r -= cirFull;
			}
		} else if( r < -cirHalf ) {
			while( r < -cirHalf ) {
				r += cirFull;
			}
		}
		
		return r;
	}
	
	public static float ClampAngle( float r, float lower, float upper )
	{
		if( r >= lower && r <= upper ) {
			return r;
		} else {
			float l = Mathf.Abs( NormalizeAngle( r - lower ) );
			float u = Mathf.Abs( NormalizeAngle( r - upper ) );
			if( l <= u ) {
				return lower;
			} else {
				return upper;
			}
		}
	}
	
	public static IndexedVector3 ClampDirection(
		IndexedVector3 prevDir,
		IndexedVector3 dir,
		float dirDot,
		float limitTheta,
		float limitTheta2 )
	{
		if( dirDot > 0.01f ) {
			float lLen = limitTheta;
			float lLen2 = limitTheta2;
			IndexedVector3 lVec = prevDir * lLen;
			float nLen = limitTheta / dirDot;
			IndexedVector3 nVec = dir * nLen;
			IndexedVector3 qVec = nVec - lVec;
			float qLen = Mathf.Sqrt(nLen * nLen - lLen2);
			return lVec + qVec * ((1.0f / qLen) * Mathf.Sqrt(1.0f - lLen2));
		} else {
			return prevDir; // Lock for safety.
		}
	}
	
	public static IndexedVector3 GetReflVector( IndexedVector3 normal, IndexedVector3 ray )
	{
		return ray - 2.0f * normal.Dot( ray ) * normal;
	}
	
	public static IndexedVector3 GetAngAccVector( IndexedVector3 prev, IndexedVector3 prev2 )
	{
		Vector3 t = GetReflVector( prev, -prev2 );
		return GetReflVector( t, -prev );
	}

	//----------------------------------------------------------------------------------------------------------------

	public static void BlendTransform( ref IndexedMatrix m, ref IndexedMatrix lhs, ref IndexedMatrix rhs, float lhsRate )
	{
		float rhsRate = 1.0f - lhsRate;
		IndexedVector3 x = lhs._basis.GetColumn(0) * lhsRate + rhs._basis.GetColumn(0) * rhsRate;
		IndexedVector3 z = lhs._basis.GetColumn(2) * lhsRate + rhs._basis.GetColumn(2) * rhsRate;
		float xLen = x.Length();
		float zLen = z.Length();
		if( xLen < 0.01f || zLen < 0.01f ) {
			m = lhs;
			return;
		}
		
		x *= 1.0f / xLen;
		z *= 1.0f / zLen;
		IndexedVector3 y = z.Cross(ref x);
		float yLen = y.Length();
		if( yLen < 0.01f ) {
			m = lhs;
			return;
		}
		
		y *= 1.0f / yLen;
		z = x.Cross(ref y);

		m._basis[0] = new IndexedVector3( x[0], y[0], z[0] );
		m._basis[1] = new IndexedVector3( x[1], y[1], z[1] );
		m._basis[2] = new IndexedVector3( x[2], y[2], z[2] );
		m._origin = lhs._origin * lhsRate + rhs._origin * rhsRate;
	}

	public static bool HitTestSphereToSphere(
		ref Vector3 translateAtoB,
		Vector3 spherePosA,
		Vector3 spherePosB,
		float lengthAtoB,
		float lengthAtoB2 )
	{
		translateAtoB = spherePosB - spherePosA;
		float len2 = translateAtoB.sqrMagnitude;
		if( len2 < lengthAtoB2 ) {
			float len = Mathf.Sqrt( len2 );
			if( len > Mathf.Epsilon ) {
				translateAtoB *= ((1.0f) / len) * (lengthAtoB - len);
				return true;
			}
		}
		
		return false;
	}

	public static bool HitTestSphereToCapsule(
		ref Vector3 translateOrig,
		ref Vector3 translateAtoB,
		Vector3 r_spherePos,
		float sphereRadius,
		float cylinderHeightH,
		float cylinderRadius,
		float lengthAtoB,
		float lengthAtoB2 )
	{
		translateOrig.Set( 0, 0, 0 );
		translateAtoB.Set( 0, 0, 0 );
		
		// XZ(Sphere)
		float xzLen2 = r_spherePos[0] * r_spherePos[0] + r_spherePos[2] * r_spherePos[2];
		if( xzLen2 < lengthAtoB2 ) {
			float absY = Mathf.Abs(r_spherePos[1]);
			// Y(Cylinder)
			if( absY < cylinderHeightH ) {
				float xzLen = Mathf.Sqrt( xzLen2 );
				if( xzLen > Mathf.Epsilon ) {
					translateAtoB.Set( -r_spherePos[0], 0.0f, -r_spherePos[2] );
					translateAtoB *= 1.0f / xzLen;
					translateAtoB *= (lengthAtoB - xzLen);
					translateOrig.Set( 0.0f, r_spherePos[1], 0.0f );
					return true;
				} else {
					return false;
				}
			}
			float xyzLen2 = xzLen2 + (absY - cylinderHeightH) * (absY - cylinderHeightH);
			// Y(Sphere)
			if( xyzLen2 < lengthAtoB2 ) {
				float xyzLen = Mathf.Sqrt( xyzLen2 );
				if( xyzLen > Mathf.Epsilon ) {
					if( r_spherePos[1] >= 0 ) {
						translateOrig.Set( 0.0f, cylinderHeightH, 0.0f );
					} else {
						translateOrig.Set( 0.0f, -cylinderHeightH, 0.0f );
					}
					translateAtoB = translateOrig - r_spherePos;
					translateAtoB *= (1.0f / xyzLen);
					translateAtoB *= (lengthAtoB - xyzLen);
					return true;
				} else {
					return false;
				}
			}
		}
		
		return false;
	}

	public static bool HitTestSphereToBox(
		ref Vector3 translateOrig,
		ref Vector3 translateAtoB,
		Vector3 r_spherePos,
		float sphereRadius,
		float sphereRadius2,
		Vector3 boxSizeH )
	{
		translateOrig.Set( 0, 0, 0 );
		translateAtoB.Set( 0, 0, 0 );
		
		float absX = Mathf.Abs(r_spherePos[0]);
		float absY = Mathf.Abs(r_spherePos[1]);
		float absZ = Mathf.Abs(r_spherePos[2]);
		
		{
			int dim = -1;
			bool innerX = (absX <= boxSizeH[0]);
			bool innerY = (absY <= boxSizeH[1]);
			bool innerZ = (absZ <= boxSizeH[2]);
			
			if( innerX && innerY && innerZ ) {
				if( boxSizeH[0] <= Mathf.Epsilon || boxSizeH[1] <= Mathf.Epsilon || boxSizeH[2] <= Mathf.Epsilon ) {
					return false;
				}
				
				bool zeroX = (absX <= Mathf.Epsilon);
				bool zeroY = (absY <= Mathf.Epsilon);
				bool zeroZ = (absZ <= Mathf.Epsilon);
				
				float boxYZ = boxSizeH[1] / boxSizeH[2];
				float boxZX = boxSizeH[2] / boxSizeH[0];
				float boxYX = boxSizeH[1] / boxSizeH[0];
				
				if( zeroX && zeroY && zeroZ ) {
					// Nothing.
				} else if( zeroX && zeroY ) {
					dim = 2;
				} else if( zeroX && zeroZ ) {
					dim = 1;
				} else if( zeroY && zeroZ ) {
					dim = 0;
				} else if( zeroX ) {
					float rYZ = absY / absZ;
					dim = ( rYZ > boxYZ ) ? 1 : 2;
				} else if( zeroY ) {
					float rZX = absZ / absX;
					dim = ( rZX > boxZX ) ? 2 : 0;
				} else if( zeroZ ) {
					float rYX = absY / absX;
					dim = ( rYX > boxYX ) ? 1 : 0;
				} else {
					float rYX = absY / absX;
					if( rYX > boxYX ) {
						float rYZ = absY / absZ;
						dim = ( rYZ > boxYZ ) ? 1 : 2;
					} else {
						float rZX = absZ / absX;
						dim = ( rZX > boxZX ) ? 2 : 0;
					}
				}
			} else if( absX < boxSizeH[0] + sphereRadius && innerY && innerZ ) {
				dim = 0;
			} else if( innerX && absY < boxSizeH[1] + sphereRadius && innerZ ) {
				dim = 1;
			} else if( innerX && innerY && absZ < boxSizeH[2] + sphereRadius ) {
				dim = 2;
			}
			
			switch( dim ) {
			case 0: // X
			{
				float lenX = (boxSizeH[0] - absX) + sphereRadius;
				translateOrig.Set( 0.0f, r_spherePos[1], r_spherePos[2] );
				translateAtoB.Set( (r_spherePos[0] >= 0) ? -lenX : lenX, 0.0f, 0.0f );
			}
				return true;
			case 1: // Y
			{
				float lenY = (boxSizeH[1] - absY) + sphereRadius;
				translateOrig.Set( r_spherePos[0], 0.0f, r_spherePos[2] );
				translateAtoB.Set( 0.0f, (r_spherePos[1] >= 0) ? -lenY : lenY, 0.0f );
			}
				return true;
			case 2: // Z
			{
				float lenZ = (boxSizeH[2] - absZ) + sphereRadius;
				translateOrig.Set( r_spherePos[0], r_spherePos[1], 0.0f );
				translateAtoB.Set( 0.0f, 0.0f, (r_spherePos[2] >= 0) ? -lenZ : lenZ );
			}
				return true;
			}
		}

		{
			translateOrig.x = ( ( r_spherePos[0] >= 0 ) ? boxSizeH[0] : -boxSizeH[0] );
			translateOrig.y = ( ( r_spherePos[1] >= 0 ) ? boxSizeH[1] : -boxSizeH[1] );
			translateOrig.z = ( ( r_spherePos[2] >= 0 ) ? boxSizeH[2] : -boxSizeH[2] );
			float xyzLen2 = (r_spherePos - translateOrig).sqrMagnitude;
			if( xyzLen2 < sphereRadius2 ) {
				float xyzLen = Mathf.Sqrt( xyzLen2 );
				if( xyzLen > Mathf.Epsilon ) {
					translateAtoB = translateOrig - r_spherePos;
					translateAtoB *= (1.0f / xyzLen) * (sphereRadius - xyzLen);
					Vector3 movedSpherePos = r_spherePos - translateAtoB;
					float movedAbsX = Mathf.Abs(movedSpherePos.x);
					float movedAbsY = Mathf.Abs(movedSpherePos.y);
					float movedAbsZ = Mathf.Abs(movedSpherePos.z);
					bool movedInnerX = (movedAbsX <= boxSizeH.x);
					bool movedInnerY = (movedAbsY <= boxSizeH.y);
					bool movedInnerZ = (movedAbsZ <= boxSizeH.z);
					if( movedInnerX && movedInnerY && movedInnerZ ) {
						// Nothing.
					} else {
						return true;
					}
				}
			}
		}

		return false;
	}

	//----------------------------------------------------------------------------------------------------------------

	static void _FeedbackImpulse( MMD4MecanimBulletPMXCollider colliderA, Vector3 translateAtoB, Vector3 translateOrig )
	{
		colliderA.isCollision = true;
		colliderA.transform._origin -= translateAtoB;
	}
	
	static bool _FastCollideStoK( MMD4MecanimBulletPMXCollider colliderA, MMD4MecanimBulletPMXCollider colliderB )
	{
		IndexedMatrix transformB = colliderB.transform;
		IndexedMatrix transformBInv = colliderB.transform.Inverse();
		
		IndexedMatrix transformA = colliderA.transform;
		MMD4MecanimBulletPMXColliderCircles circlesA = colliderA.circles;
		Vector3[] vertices = colliderA.circles.GetTransformVertices();
		int vertexCount = vertices.Length;

		Vector3 translateOrig = Vector3.zero;
		Vector3 translateAtoB = Vector3.zero;

		switch( colliderB.shape ) {
		case (int)PMXShapeType.Sphere:
		{
			float lengthAtoB = circlesA.GetRadius() + colliderB.size[0];
			float lengthAtoB2 = lengthAtoB * lengthAtoB;
			Vector3 spherePos = colliderB.transform._origin;
			Vector3 colliderTranslate = Vector3.zero;
			{
				circlesA.Transform( transformA );
				for( int i = 0; i != vertexCount; ++i ) {
					if( HitTestSphereToSphere( ref translateAtoB, vertices[i], spherePos, lengthAtoB, lengthAtoB2 ) ) {
						translateOrig = colliderA.transform._origin;
						colliderTranslate -= translateAtoB;
						_FeedbackImpulse( colliderA, translateAtoB, translateOrig );
					}
				}
				return colliderA.isCollision;
			}
		}
		case (int)PMXShapeType.Box:
		{
			float radiusA = circlesA.GetRadius();
			float radiusA2 = circlesA.GetRadius2();
			Vector3 boxSizeH = colliderB.size;
			Vector3 colliderTranslate = Vector3.zero;
			{
				circlesA.Transform( transformBInv * transformA );
				for( int i = 0; i != vertexCount; ++i ) {
					if( HitTestSphereToBox( ref translateOrig, ref translateAtoB, vertices[i] + colliderTranslate, radiusA, radiusA2, boxSizeH ) ) {
						colliderTranslate -= translateAtoB;
						translateAtoB = transformB._basis * translateAtoB;
						_FeedbackImpulse( colliderA, translateAtoB, translateOrig );
					}
				}
				return colliderA.isCollision;
			}
		}
		case (int)PMXShapeType.Capsule:
		{
			float radiusA = circlesA.GetRadius();
			float lengthAtoB = circlesA.GetRadius() + colliderB.size[0];
			float lengthAtoB2 = lengthAtoB * lengthAtoB;
			float cylinderHeightH = Mathf.Max( colliderB.size[1] * 0.5f, 0.0f );
			float cylinderRadius = colliderB.size[0];
			Vector3 colliderTranslate = Vector3.zero;
			{
				circlesA.Transform( transformBInv * transformA );
				for( int i = 0; i != vertexCount; ++i ) {
					if( HitTestSphereToCapsule( ref translateOrig, ref translateAtoB, vertices[i] + colliderTranslate, radiusA, cylinderHeightH, cylinderRadius, lengthAtoB, lengthAtoB2 ) ) {
						colliderTranslate -= translateAtoB;
						translateAtoB = transformB._basis * translateAtoB;
						_FeedbackImpulse( colliderA, translateAtoB, translateOrig );
					}
				}
				return colliderA.isCollision;
			}
		}
		}
		
		return false;
	}
	
	public static bool FastCollide( MMD4MecanimBulletPMXCollider colliderA, MMD4MecanimBulletPMXCollider colliderB )
	{
		colliderA.Prepare();
		colliderB.Prepare();
		if( colliderA.isKinematic && colliderB.isKinematic ) {
			return false; // Not process.
		}
		if( colliderA.isKinematic ) {
			return _FastCollideStoK( colliderB, colliderA );
		} else if( colliderB.isKinematic ) {
			return _FastCollideStoK( colliderA, colliderB );
		} else {
			return false;
		}
	}

	//----------------------------------------------------------------------------------------------------------------

    public class SimpleMotionState : IMotionState
    {
        public SimpleMotionState()
            : this(IndexedMatrix.Identity)
        {
        }

        public SimpleMotionState(IndexedMatrix startTrans)
        {
            m_graphicsWorldTrans = startTrans;
        }

        public SimpleMotionState(ref IndexedMatrix startTrans)
        {
            m_graphicsWorldTrans = startTrans;
        }
		
        public virtual void GetWorldTransform(out IndexedMatrix centerOfMassWorldTrans)
        {
            centerOfMassWorldTrans = m_graphicsWorldTrans;
        }

        public virtual void SetWorldTransform(IndexedMatrix centerOfMassWorldTrans)
        {
            SetWorldTransform(ref centerOfMassWorldTrans);
        }

        public virtual void SetWorldTransform(ref IndexedMatrix centerOfMassWorldTrans)
        {
            m_graphicsWorldTrans = centerOfMassWorldTrans;
        }

        public virtual void Rotate(IndexedQuaternion iq)
        {
            IndexedMatrix im = IndexedMatrix.CreateFromQuaternion(iq);
            im._origin = m_graphicsWorldTrans._origin;
            SetWorldTransform(ref im);
        }

        public virtual void Translate(IndexedVector3 v)
        {
            m_graphicsWorldTrans._origin += v;
        }

        public IndexedMatrix m_graphicsWorldTrans;
    }
	
    public class KinematicMotionState : IMotionState
    {
        public KinematicMotionState()
        {
			m_graphicsWorldTrans = IndexedMatrix.Identity;
        }

        public KinematicMotionState(ref IndexedMatrix startTrans)
        {
            m_graphicsWorldTrans = startTrans;
        }

        public virtual void GetWorldTransform(out IndexedMatrix centerOfMassWorldTrans)
        {
            centerOfMassWorldTrans = m_graphicsWorldTrans;
        }

        public virtual void SetWorldTransform(IndexedMatrix centerOfMassWorldTrans)
        {
			// Nothing.
        }

        public virtual void SetWorldTransform(ref IndexedMatrix centerOfMassWorldTrans)
        {
			// Nothing.
        }

        public virtual void Rotate(IndexedQuaternion iq)
        {
			// Nothing.
        }

        public virtual void Translate(IndexedVector3 v)
        {
			// Nothing.
        }

        public IndexedMatrix m_graphicsWorldTrans;
    }

	public static IndexedMatrix MakeIndexedMatrix( ref Vector3 position, ref Quaternion rotation )
	{
		IndexedQuaternion indexedQuaternion = new IndexedQuaternion(ref rotation);
		IndexedBasisMatrix indexedBasisMatrix = new IndexedBasisMatrix(indexedQuaternion);
		IndexedVector3 origin = new IndexedVector3(ref position);
		return new IndexedMatrix(indexedBasisMatrix, origin);
	}

	public static void MakeIndexedMatrix( ref IndexedMatrix matrix, ref Vector3 position, ref Quaternion rotation )
	{
		matrix.SetRotation( new IndexedQuaternion(ref rotation) );
		matrix._origin = position;
	}

    public static void CopyBasis(ref IndexedBasisMatrix m0, ref Matrix4x4 m1)
    {
        m0._el0.X = m1.m00;
        m0._el1.X = m1.m10;
        m0._el2.X = m1.m20;

        m0._el0.Y = m1.m01;
        m0._el1.Y = m1.m11;
        m0._el2.Y = m1.m21;

        m0._el0.Z = m1.m02;
        m0._el1.Z = m1.m12;
        m0._el2.Z = m1.m22;
    }

    public static void CopyBasis(ref Matrix4x4 m0, ref IndexedBasisMatrix m1)
    {
        m0.m00 = m1._el0.X;
        m0.m10 = m1._el1.X;
        m0.m20 = m1._el2.X;

        m0.m01 = m1._el0.Y;
        m0.m11 = m1._el1.Y;
        m0.m21 = m1._el2.Y;

        m0.m02 = m1._el0.Z;
        m0.m12 = m1._el1.Z;
        m0.m22 = m1._el2.Z;
    }

    public static void CopyBasis(ref Matrix4x4 m0, ref Matrix4x4 m1)
    {
        m0.m00 = m1.m00;
        m0.m10 = m1.m10;
        m0.m20 = m1.m20;

        m0.m01 = m1.m01;
        m0.m11 = m1.m11;
        m0.m21 = m1.m21;

        m0.m02 = m1.m02;
        m0.m12 = m1.m12;
        m0.m22 = m1.m22;
    }

    public static void SetRotation(ref Matrix4x4 m, ref Quaternion q)
    {
        float d = q.x * q.x + q.y + q.y + q.z * q.z + q.w * q.w;
        float s = (d > Mathf.Epsilon) ? (2.0f / d) : 0.0f;
        float xs = q.x * s, ys = q.y * s, zs = q.z * s;
        float wx = q.w * xs, wy = q.w * ys, wz = q.w * zs;
        float xx = q.x * xs, xy = q.x * ys, xz = q.x * zs;
        float yy = q.y * ys, yz = q.y * zs, zz = q.z * zs;
        // el0
        m.m00 = 1.0f - (yy + zz);
        m.m01 = xy - wz;
        m.m02 = xz + wy;
        // el1
        m.m10 = xy + wz;
        m.m11 = 1.0f - (xx + zz);
        m.m12 = yz - wx;
        // el2
        m.m20 = xz - wy;
        m.m21 = yz + wx;
        m.m22 = 1.0f - (xx + yy);
    }

    public static void SetPosition(ref Matrix4x4 m, Vector3 v)
    {
        m.m03 = v.x;
        m.m13 = v.y;
        m.m23 = v.z;
    }

    public static void SetPosition(ref Matrix4x4 m, ref Vector3 v)
    {
        m.m03 = v.x;
        m.m13 = v.y;
        m.m23 = v.z;
    }

    public static Vector3 GetPosition(ref Matrix4x4 m)
    {
        return new Vector3(m.m03, m.m13, m.m23);
    }

	/*
    public static Quaternion GetRotation(ref Matrix4x4 m)
    {
        Quaternion q = new Quaternion();
        q.w = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] + m[1, 1] + m[2, 2])) / 2;
        q.x = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] - m[1, 1] - m[2, 2])) / 2;
        q.y = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] + m[1, 1] - m[2, 2])) / 2;
        q.z = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] - m[1, 1] + m[2, 2])) / 2;
        q.x *= Mathf.Sign(q.x * (m[2, 1] - m[1, 2]));
        q.y *= Mathf.Sign(q.y * (m[0, 2] - m[2, 0]));
        q.z *= Mathf.Sign(q.z * (m[1, 0] - m[0, 1]));
        return q;
    }
    */

	// from: Bullet Physics 2.79(C/C++)
	public static IndexedBasisMatrix EulerZYX( float eulerX, float eulerY, float eulerZ )
	{ 
		float ci = Mathf.Cos(eulerX);
		float cj = Mathf.Cos(eulerY);
		float ch = Mathf.Cos(eulerZ);
		float si = Mathf.Sin(eulerX);
		float sj = Mathf.Sin(eulerY);
		float sh = Mathf.Sin(eulerZ);
		float cc = ci * ch; 
		float cs = ci * sh; 
		float sc = si * ch; 
		float ss = si * sh;
		return new IndexedBasisMatrix(
				cj * ch, sj * sc - cs, sj * cc + ss,
		        cj * sh, sj * ss + cc, sj * cs - sc, 
		        -sj,      cj * si,      cj * ci);
	}

	public static IndexedBasisMatrix BasisRotationYXZ(ref Vector3 rotation)
    {
		IndexedBasisMatrix rx = EulerZYX( rotation.x, 0.0f, 0.0f );
		IndexedBasisMatrix ry = EulerZYX( 0.0f, rotation.y, 0.0f );
		IndexedBasisMatrix rz = EulerZYX( 0.0f, 0.0f, rotation.z );
        return ry * rx * rz; // Yaw-Pitch-Roll
    }
}
