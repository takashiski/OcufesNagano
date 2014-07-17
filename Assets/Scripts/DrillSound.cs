using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class DrillSound : MonoBehaviour {
	public AudioClip[] se = new AudioClip[3];
	private AudioSource source;
	// Use this for initialization
	void Awake() {
		source = this.GetComponent<AudioSource>();
		source.clip = se[0];
		source.loop = true;
		source.Play();
	}
	
	// Update is called once per frame
	void Update () {
		if(!source.isPlaying)
			source.Play ();
	}
}
