using UnityEngine;
using System.Collections;
using BulletXNA;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.LinearMath;

using PMXShapeType          = MMD4MecanimBulletPMXCommon.PMXShapeType;

public class MMD4MecanimBulletPMXColliderCircles
{
	public MMD4MecanimBulletPMXColliderCircles( int shape_, Vector3 size_ )
	{
		_shape = shape_;
		_size = size_;
		
		switch( _shape ) {
		case (int)PMXShapeType.Sphere:
			_radius = size_[0] * _radiusBias;
			_diameter = _radius * 2.0f;
			if( _radius > Mathf.Epsilon ) {
				_vertices = new Vector3[1];
				_vertices[0] = Vector3.zero;
			}
			break;
		case (int)PMXShapeType.Box:
			_radius = Mathf.Min( Mathf.Min( size_[0], size_[1] ), size_[2] ) * _radiusBias;
			_diameter = _radius * 2.0f;
			if( _radius > Mathf.Epsilon ) {
				int idx = 0;
				Segment segX = _ComputeSegment( size_[0] * 2.0f * _radiusBias );
				Segment segY = _ComputeSegment( size_[1] * 2.0f * _radiusBias );
				Segment segZ = _ComputeSegment( size_[2] * 2.0f * _radiusBias );
				if( size_[0] <= size_[1] && size_[0] <= size_[2] ) {
					_vertices = new Vector3[segY.count * segZ.count];
					float posY = segY.start;
					for( int y = 0; y < segY.count; ++y, posY += segY.interval ) {
						float posZ = segZ.start;
						for( int z = 0; z < segZ.count; ++z, posZ += segZ.interval, ++idx ) {
							_vertices[idx] = new Vector3( 0.0f, posY, posZ );
						}
					}
				} else if( size_[1] <= size_[0] && size_[1] <= size_[2] ) {
					_vertices = new Vector3[segX.count * segZ.count];
					float posX = segX.start;
					for( int x = 0; x < segX.count; ++x, posX += segX.interval ) {
						float posZ = segZ.start;
						for( int z = 0; z < segZ.count; ++z, posZ += segZ.interval, ++idx ) {
							_vertices[idx] = new Vector3( posX, 0.0f, posZ );
						}
					}
				} else {
					_vertices = new Vector3[segX.count * segY.count];
					float posX = segX.start;
					for( int x = 0; x < segX.count; ++x, posX += segX.interval ) {
						float posY = segY.start;
						for( int y = 0; y < segY.count; ++y, posY += segY.interval, ++idx ) {
							_vertices[idx] = new Vector3( posX, posY, 0.0f );
						}
					}
				}
			}
			break;
		case (int)PMXShapeType.Capsule:
			_radius = size_[0] * _radiusBias;
			_diameter = _radius * 2.0f;
			if( _radius > Mathf.Epsilon ) {
				Segment segY = _ComputeSegment( (size_[1] + size_[0] * 2.0f) * _radiusBias );
				_vertices = new Vector3[segY.count];
				float posY = segY.start;
				for( int y = 0; y < segY.count; ++y, posY += segY.interval ) {
					_vertices[y] = new Vector3( 0, posY, 0 );
				}
			}
			break;
		}
		
		_radius2 = _radius * _radius;
		
		if( _vertices != null ) {
			_transformVertices = new Vector3[_vertices.Length];
		} else {
			_transformVertices = new Vector3[0];
		}
	}
	
	public void Transform( IndexedMatrix transform )
	{
		if( _transformVertices != null && _vertices != null ) {
			for( int i = 0; i < _vertices.Length; ++i ) {
				_transformVertices[i] = transform * _vertices[i];
			}
		}
	}
	
	public void ForceTranslate( Vector3 movedTranslate )
	{
		if( _transformVertices != null ) {
			for( int i = 0; i < _transformVertices.Length; ++i ) {
				_transformVertices[i] += movedTranslate;
			}
		}
	}
	
	public Vector3[] GetTransformVertices()
	{
		return _transformVertices;
	}
	
	public float GetRadius()
	{
		return _radius;
	}
	
	public float GetRadius2()
	{
		return _radius2;
	}

	public Vector3 size { get { return _size; } }

	int				_shape;
	Vector3			_size;
	Vector3[]		_vertices;
	Vector3[]		_transformVertices;
	float			_radius;
	float			_radius2;
	float			_diameter;
	int				_segments = 3;
	float			_radiusBias = 0.98f;
	
	struct Segment
	{
		public int count;
		public float start;
		public float interval;
	};
	
	Segment _ComputeSegment( float len_ )
	{
		if(_radius <= Mathf.Epsilon) {
			return new Segment();
		}
		Segment seg = new Segment();
		seg.count = Mathf.Max((int)(len_ / _diameter) + 1, _segments);
		if( seg.count > 1 ) {
			float lenInternal = Mathf.Max(len_ - _diameter, 0.0f);
			seg.start = -lenInternal * 0.5f;
			seg.interval = lenInternal / (float)(seg.count - 1);
		}
		return seg;
	}
}

public class MMD4MecanimBulletPMXCollider
{
	public IndexedMatrix		transform		= IndexedMatrix.Identity;
	public int					shape			= -1;
	public Vector3				size			= Vector3.zero;
	public bool					isKinematic		= false;
	public bool					isCollision		= false;
	public MMD4MecanimBulletPMXColliderCircles circles = null;
	
	public void Prepare()
	{
		isCollision = false;
		if( !isKinematic ) {
			if( circles == null ) {
				circles = new MMD4MecanimBulletPMXColliderCircles( shape, size );
			}
		}
	}
}
