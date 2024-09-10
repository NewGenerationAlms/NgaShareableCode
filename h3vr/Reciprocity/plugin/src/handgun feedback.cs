using BepInEx;
using FistVR;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using BepInEx.Configuration;
using Valve.VR.InteractionSystem;

public class Velocityhandlehandgun : MonoBehaviour
{
	private FistVR.HandgunSlide bolt;
	public float bolt_velocity;
	private float boltpos;
	private float boltposprev;
	private Vector3 boltacpos;
	public float bolt_velocityrot;
	private float boltrot;
	private float boltrotprev;
	public float forcemuilt;
	public GameObject handposgameobject;
	public float dampingFactor;
	public float max_force = 10;
	public Vector3 Dampening;

	// Use this for initialization.
	void Start()
	{
		bolt = gameObject.GetComponent<FistVR.Handgun>().Slide;
		boltpos = bolt.m_slideZ_current;
		boltposprev = boltpos;
	}
	// Update is called once per frame.
	void Update()
	{
		boltposprev = boltpos;
		boltpos = bolt.m_slideZ_current;
		bolt_velocity = boltpos - boltposprev;
	}

	// Effects force when slide is being grabbed.
	private void FixedUpdate()
	{
		if (handposgameobject == null) { // NGA:ADD sanity null check
			return;
		}
		if (bolt.m_hand != null)
		{
			Dampening = GetComponent<Rigidbody>().velocity * -1;
			Vector3 Error = bolt.m_hand.TouchSphere.transform.position - handposgameobject.transform.position;
			GetComponent<Rigidbody>().AddForceAtPosition(Error * 1000 * forcemuilt + Dampening, handposgameobject.transform.position, ForceMode.Acceleration);
		}
		if (bolt.m_hand == null)
		{
			Destroy(handposgameobject);
		}
	}

	// Called on BeginInteraction to allow slide-grab force-feedback.
	public void grab()
	{
		handposgameobject = new GameObject();
		handposgameobject.transform.position = bolt.m_hand.TouchSphere.transform.position;
		handposgameobject.transform.parent = bolt.transform;
	}
	
}
