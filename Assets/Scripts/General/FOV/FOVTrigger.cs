using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class FOVTrigger : FOVSensor
{
    private bool _targetInRange;
    public float followTime;
    private Coroutine _coroutine;
    private SphereCollider _sensorCollider;

    private void Awake()
    {
        _sensorCollider = GetComponent<SphereCollider>();
        _sensorCollider.isTrigger = true;
        UpdateColliderRadius();
    }

    private void OnValidate()
    {
        if (_sensorCollider == null) _sensorCollider = GetComponent<SphereCollider>();
            
        if (_sensorCollider != null) UpdateColliderRadius();
    }

    private void UpdateColliderRadius()
    {
        float maxScale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
        _sensorCollider.radius = _viewRadius / (maxScale > 0 ? maxScale : 1f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsAValidTarget(other.gameObject.layer, _targetMask)) return;

        if (_coroutine != null)
        {
            StopCoroutine(_coroutine);
            _coroutine = null;
        }

        _target = other.transform.root;
        _targetInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsAValidTarget(other.gameObject.layer, _targetMask)) return;

        if (_coroutine == null)
        {
            _coroutine = StartCoroutine(KeepLooking());
        }
    }

    private void Update()
    {
        CanSeeTarget = _targetInRange && _target != null && InViewAngle() && HasLineOfSight();
    }

    private IEnumerator KeepLooking()
    {
        yield return new WaitForSeconds(followTime);
        _targetInRange = false;
        _target = null;
        _coroutine = null;
    }
}