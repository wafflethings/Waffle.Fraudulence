using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Waffle.Fraudulence.Behaviours.Enemies.Cultist;

[ConfigureSingleton(SingletonFlags.NoAutoInstance)]
public class CultistVignetteEffect : MonoSingleton<CultistVignetteEffect>
{
    public float StartOpacity = 0.5f;
    public float FadeSpeed = 0.5f;
    public AudioSource SoundEffect;

    private Image _image;

    public void Flash()
    {
        _image ??= GetComponent<Image>();
        StopAllCoroutines();
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        SoundEffect?.Play();
        Color currentColour = _image.color;
        currentColour.a = StartOpacity;

        while (!Mathf.Approximately(currentColour.a, 0))
        {
            currentColour.a -= Time.deltaTime * FadeSpeed;
            _image.color = currentColour;
            yield return null;
        }

        currentColour.a = 0;
        _image.color = currentColour;
    }
}
