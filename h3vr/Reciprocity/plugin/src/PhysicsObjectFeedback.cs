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
public class PhysicsObjectFeedback : MonoBehaviour
{
	public Vector3 velocity;
    public Vector3 torque;
    public float ForceMuilt;
    public float DampeningMuilt;
    public Vector3 error;
	public Vector3 dampening;
    public float maxforce = 50.0f; // Max Force
    public float angularForce = 10.0f;   // Torque magnitude
    public float angularDampening = 0.5f; // Dampening factor
    public FVRPhysicalObject physic;
    public GameObject altgripgameobject;
    public bool altheld;
    public bool altheldprev;
    public BoxCollider[] colliders;
    public CapsuleCollider[] Capcolliders;
    public MeshRenderer mesh;
    public float weight;
    private bool firstframe = true;
    private bool ishandgun;
    private bool isclosedbolt;
    private bool isopenbolt;
    private bool istubefed;
    private bool isboltaction;
    private bool isammo;
    public bool latchGrabbed = false;
    // Use this for initialization
    void Start()
	{
        physic = gameObject.GetComponent<FVRPhysicalObject>();
        if(physic.GetComponent<FVRFireArmRound>() != null)
        {
            isammo = true;
        }
    }
    protected Quaternion RotOptimize(Quaternion q)
    {
        if (q.w < 0.0f)
        {
            q.x *= -1.0f;
            q.y *= -1.0f;
            q.z *= -1.0f;
            q.w *= -1.0f;
        }

        return q;
    }
    Vector3 RotAxis(Quaternion q)
    {
        var n = new Vector3(q.x, q.y, q.z);
        return n.normalized;
    }
    private void SingleHandedGripFU() {
        // Does rotation-hand alignment.
        Debug.Log("SingleHanded START");
        // Define the local pitch offset as -90 degrees around the local X-axis
        Quaternion pitchOffset = Quaternion.Euler(0f + -physic.PoseOverride.localEulerAngles.x, 0f, 0f);
        // Apply the pitch offset to the target rotation
        Quaternion adjustedTargetRotation = physic.m_handRot * pitchOffset;
        // Calculate the difference between the adjusted target rotation and the current rotation
        Quaternion rotationDifference = adjustedTargetRotation * Quaternion.Inverse(transform.rotation);
        // Convert the rotation difference to an axis-angle representation
        rotationDifference.ToAngleAxis(out float angle, out Vector3 axis);
        // Apply torque to align with the adjusted target rotation
        if (angle > 180f) angle -= 360f; // Adjust angle to ensure shortest path
        Vector3 torque = axis * (angle * Mathf.Deg2Rad) * angularForce;
        torque = Vector3.ClampMagnitude(torque, physic.RootRigidbody.mass * maxforce);
        physic.RootRigidbody.AddTorque(torque, ForceMode.VelocityChange);
        // Apply angular velocity damping
        Vector3 dampingTorque = -physic.RootRigidbody.angularVelocity * angularDampening;
        dampingTorque = Vector3.ClampMagnitude(dampingTorque, physic.RootRigidbody.mass * maxforce);
        physic.RootRigidbody.AddTorque(dampingTorque, ForceMode.VelocityChange);
        Debug.Log("SingleHanded END");
    }
    void FixedUpdate()
    {
        if (physic.IsHeld)
        {
            if (physic.AltGrip != null)
            {
                if (physic.AltGrip.IsHeld)
                {
                    var point = new GameObject();
                    point.transform.rotation = Quaternion.LookRotation(physic.m_handPos - physic.AltGrip.m_handPos, physic.m_hand.transform.up);
                    point.transform.position = physic.transform.position;
                    var offset = physic.transform.InverseTransformPoint(physic.AltGrip.PoseOverride.position);
                    dampening = physic.RootRigidbody.GetPointVelocity(physic.AltGrip.PoseOverride.position) * -0.2f * DampeningMuilt;
                    error = (physic.AltGrip.m_hand.PalmTransform.position - physic.AltGrip.PoseOverride.position) * ForceMuilt;
                    error = Vector3.ClampMagnitude(error * 7, physic.RootRigidbody.mass * maxforce);
                    physic.RootRigidbody.AddForceAtPosition(error, physic.AltGrip.PoseOverride.position, ForceMode.VelocityChange);
                    dampening = Vector3.ClampMagnitude(dampening, physic.RootRigidbody.mass * maxforce);
                    physic.RootRigidbody.AddForceAtPosition(dampening, physic.AltGrip.PoseOverride.position, ForceMode.VelocityChange);
                    Quaternion pitchOffset = Quaternion.Euler(0f + -physic.PoseOverride.localEulerAngles.x, 0f, 0f);
                    Vector3 position;
                    Vector3 vector4;
                    
                    position = physic.AltGrip.GetPalmPos(physic.m_doesDirectParent);
                    vector4 = base.transform.InverseTransformPoint(physic.AltGrip.PoseOverride.position);
                    Vector3 vector5 = base.transform.InverseTransformPoint(position);
                    Vector3 vector6 = base.transform.InverseTransformPoint(physic.PoseOverride.position);
                    float z = Mathf.Max(physic.PoseOverride.localPosition.z + 0.05f, vector5.z);
                    Vector3 position2 = new Vector3(vector5.x - vector4.x, vector5.y - vector4.y, z);
                    Vector3 vector7 = base.transform.TransformPoint(position2);
                    Vector3 vector8 = Vector3.Cross(vector7 - base.transform.position, physic.m_hand.Input.Right);
                    Quaternion adjustedTargetRotation = Quaternion.LookRotation((vector7 - base.transform.position).normalized, vector8) * physic.PoseOverride.localRotation * pitchOffset;
                    // Calculate the difference between the adjusted target rotation and the current rotation
                    Quaternion rotationDifference = adjustedTargetRotation * Quaternion.Inverse(transform.rotation);
                    // Convert the rotation difference to an axis-angle representation
                    rotationDifference.ToAngleAxis(out float angle, out Vector3 axis);
                    // Apply torque to align with the adjusted target rotation
                    if (angle > 180f) angle -= 360f; // Adjust angle to ensure shortest path
                    Vector3 torque = axis * (angle * Mathf.Deg2Rad) * angularForce;
                    torque = Vector3.ClampMagnitude(torque, physic.RootRigidbody.mass * maxforce);
                    physic.RootRigidbody.AddTorque(torque, ForceMode.VelocityChange);
                    // Apply angular velocity damping
                    Vector3 dampingTorque = -physic.RootRigidbody.angularVelocity * angularDampening;
                    dampingTorque = Vector3.ClampMagnitude(dampingTorque, physic.RootRigidbody.mass * maxforce);
                    physic.RootRigidbody.AddTorque(dampingTorque, ForceMode.VelocityChange);
                    Debug.Log("1");
                }
                else
                {
                    Debug.Log("SingleHanded_AltGrip_NotHeld START");
                    SingleHandedGripFU();
                    Debug.Log("SingleHanded_AltGrip_NotHeld END");
                }
            }
            else
            {
                SingleHandedGripFU();
            }
            altheldprev = altheld;
            if (physic.IsAltHeld)
            {
                Debug.Log("Is AltHeld BEGIN");
                altheld = true;
                if (altheld == true && altheldprev == false)
                {
                    
                    altgripgameobject = new GameObject();
                    altgripgameobject.transform.position = physic.m_hand.transform.TransformPoint(new Vector3(0, 0, 0.008f));
                    altgripgameobject.transform.parent = this.transform;
                    Debug.Log("BeginInteractionThroughAltGrip");
                }
                Debug.DrawLine(physic.AltGrip.PoseOverride.position, physic.AltGrip.m_handPos);
                dampening = physic.RootRigidbody.GetPointVelocity(altgripgameobject.transform.position) * -0.2f * DampeningMuilt;
                error = (physic.m_hand.transform.position - altgripgameobject.transform.position) * ForceMuilt;
                error = Vector3.ClampMagnitude(error * 7, physic.RootRigidbody.mass * maxforce);
                physic.RootRigidbody.AddForceAtPosition(error, altgripgameobject.transform.position, ForceMode.VelocityChange);
                dampening = Vector3.ClampMagnitude(dampening, physic.RootRigidbody.mass * maxforce);
                physic.RootRigidbody.AddForceAtPosition(dampening, altgripgameobject.transform.position, ForceMode.VelocityChange);
            }
            else
            {
                Debug.Log("Not AltHeld BEGIN");
                altheld = false;
                if (physic.PoseOverride != null)
                {
                    Debug.Log("PoseOverride BEGIN");
                    if (isammo)
                    {
                        Debug.Log("IsAmmo BEGIN");
                        dampening = physic.RootRigidbody.velocity * -0.2f * DampeningMuilt;
                        error = (physic.m_hand.transform.position - physic.PoseOverride.position) * ForceMuilt;
                        error = Vector3.ClampMagnitude(error * 7, physic.RootRigidbody.mass * maxforce);
                        physic.RootRigidbody.AddForce(error, ForceMode.VelocityChange);
                        dampening = Vector3.ClampMagnitude(dampening, physic.RootRigidbody.mass * maxforce);
                        physic.RootRigidbody.AddForce(dampening, ForceMode.VelocityChange);
                    }
                    else
                    {
                        Debug.Log("Not Ammo BEGIN***");
                        // Keeps stuff off the floor.
                        if (latchGrabbed) {
                            dampening = physic.RootRigidbody.GetPointVelocity(
                                                                physic.PoseOverride.TransformPoint(new Vector3(0, 0, -0.0165f))) 
                                                                * -0.2f * DampeningMuilt;
                            error = (physic.m_hand.transform.TransformPoint(
                                                                new Vector3(0, 0, -0.1f)) 
                                                                - physic.PoseOverride.TransformPoint(new Vector3(0, 0, -0.1f))) * ForceMuilt;
                            error = Vector3.ClampMagnitude(error * 7, physic.RootRigidbody.mass * maxforce);
                            physic.RootRigidbody.AddForceAtPosition(error, 
                                                    physic.PoseOverride.TransformPoint(new Vector3(0, 0, -0.0165f)), 
                                                    ForceMode.VelocityChange);
                            dampening = Vector3.ClampMagnitude(dampening, physic.RootRigidbody.mass * maxforce);
                            physic.RootRigidbody.AddForceAtPosition(dampening, 
                                                    physic.PoseOverride.TransformPoint(new Vector3(0, 0, -0.0165f)), 
                                                    ForceMode.VelocityChange);
                        }
                    }
                }
                else
                {
                    Debug.Log("No PoseOverride BEGIN");
                    dampening = physic.RootRigidbody.GetPointVelocity(transform.position) * -0.2f * DampeningMuilt;
                    error = (physic.m_hand.transform.position - transform.position) * ForceMuilt;
                    error = Vector3.ClampMagnitude(error * 7, physic.RootRigidbody.mass * maxforce);
                    physic.RootRigidbody.AddForce(error, ForceMode.VelocityChange);
                    dampening = Vector3.ClampMagnitude(dampening, physic.RootRigidbody.mass * maxforce);
                    physic.RootRigidbody.AddForce(dampening, ForceMode.VelocityChange);
                }
            }
        }
    }
}