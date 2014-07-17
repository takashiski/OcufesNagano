using UnityEngine;
using System.Collections;

public class Driver : MonoBehaviour {
	Vector3 moveDir = new Vector3(0,0,-1);
	float moveSpeed = 0.1f;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void FixedUpdate () {
		transform.Translate(moveDir*moveSpeed*Time.fixedDeltaTime);
	
	}
}
