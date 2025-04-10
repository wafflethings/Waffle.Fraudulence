using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Waffle.Fraudulence.Behaviours.Enemies.Cultist;

public class CultistFireController : MonoSingleton<CultistFireController>
{
    public const float ShotInterval = 2;
    public const float MinimumInterval = 6;
    public List<Cultist> CurrentCultists = new();
    private float _elapsedSinceBeginShots;
    private bool _shootCoroutineDone = true;

    private void Update()
    {
        _elapsedSinceBeginShots += Time.deltaTime;

        if (_elapsedSinceBeginShots > MinimumInterval && _shootCoroutineDone)
        {
            StartCoroutine(DoShots());
            _elapsedSinceBeginShots = 0;
        }
    }

    private IEnumerator DoShots()
    {
        _shootCoroutineDone = false;

        for (int i = 0; i < CurrentCultists.Count; i++)
        {
            if (i >= CurrentCultists.Count)
            {
                yield return null;
                continue;
            }

            Cultist cultist = CurrentCultists[i];
            Debug.Log($"{cultist.CanFire} can fire");

            if (!cultist.CanFire || cultist == null)
            {
                yield return null;
                continue;
            }

            cultist.Shoot(2);
            yield return new WaitForSeconds(ShotInterval);
        }

        _shootCoroutineDone = true;
    }

    public float IntervalPerCultist => Mathf.Max(ShotInterval * CurrentCultists.Count, MinimumInterval);
}
