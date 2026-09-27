using UnityEngine;

/// <summary>
/// Drives a simple mouth mesh on the gingerbread cookie for the intro flyby dialogue.
/// The mouth is a thin dark ellipse that scales on Y to open/close while speaking.
/// </summary>
public sealed class MouthAnimator : MonoBehaviour
{
    public float OpenAmount { get; set; }
    public bool Visible { get; set; }

    Vector3 _baseScale;
    Vector3 _basePos;
    Renderer _rend;

    void Awake()
    {
        _rend = GetComponent<Renderer>();
        _baseScale = transform.localScale;
        _basePos = transform.localPosition;
    }

    void LateUpdate()
    {
        if (_rend != null) _rend.enabled = Visible;
        float s = Mathf.Lerp(0.12f, 1f, OpenAmount);
        transform.localScale = new Vector3(_baseScale.x, _baseScale.y * s, _baseScale.z);
        transform.localPosition = _basePos + Vector3.down * (_baseScale.y * (1f - s) * 0.5f);
    }
}