using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Waffle.Fraudulence.Behaviours.Enemies.Cultist;

public class RifleTracking : MonoBehaviour
{
    public GameObject TrackedObject;
    public bool UseTrackedObjectRotation = true;
    public bool LookAtAlternateTarget;
    public float AlternateRotationSpeed = 3;
    public Transform AlternateTarget;

    private void Start() => transform.parent = null;

    private void LateUpdate()
    {
        transform.position = TrackedObject.transform.position;
        // transform.localScale = TrackedObject.transform.localScale;

        if (UseTrackedObjectRotation)
        {
            transform.rotation = TrackedObject.transform.rotation;
        }

        if (LookAtAlternateTarget)
        {
            if (AlternateTarget == null)
            {
                Debug.LogError("RifleTracking AlternateTarget is null.");
                return;
            }

            Vector3 oldRotation = transform.localRotation.eulerAngles;

            transform.forward = Vector3.MoveTowards(transform.forward,
                (AlternateTarget.position - transform.position).normalized,
                Time.deltaTime * AlternateRotationSpeed);

            transform.localRotation = Quaternion.Euler(oldRotation.x, TrackedObject.transform.rotation.eulerAngles.y,
                TrackedObject.transform.rotation.eulerAngles.z);
        }
    }
}
