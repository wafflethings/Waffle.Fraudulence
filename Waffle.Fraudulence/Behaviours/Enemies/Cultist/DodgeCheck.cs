using System;
using UnityEngine;
using UnityEngine.Events;

namespace Waffle.Fraudulence.Behaviours.Enemies.Cultist;

public class DodgeCheck : MonoBehaviour
{
    public UltrakillEvent OnDodge;
    public int MinimumDifficultyToDodge;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == 14)
        {
            if (other.GetComponent<PhysicalShockwave>() != null)
            {
                return;
            }

            OnDodge.Invoke();
        }
    }
}
