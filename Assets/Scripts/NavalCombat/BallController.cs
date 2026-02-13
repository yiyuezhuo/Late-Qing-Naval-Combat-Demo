using UnityEngine;
using NavalCombatCore;
using CoreUtils;
using System;

public class BallController : MonoBehaviour
{
    Vector3 _targetPosWu;
    float _speedWuPerSecond;
    string _targetObjectId;
    float _targetHeightFoot;
    bool _initialized;
    Action<BallController> _onArrived;

    public void Setup(
        Vector3 startPos,
        Vector3 endPos,
        float speedWuPerSecond,
        string targetObjectId,
        float targetHeightFoot,
        Action<BallController> onArrived = null
    )
    {
        transform.position = startPos;
        _targetPosWu = endPos;
        _speedWuPerSecond = Mathf.Max(0.0001f, speedWuPerSecond);
        _targetObjectId = targetObjectId;
        _targetHeightFoot = targetHeightFoot;
        _onArrived = onArrived;
        _initialized = true;
        gameObject.SetActive(true);
    }

    public void ResetState()
    {
        _initialized = false;
        _targetObjectId = null;
        _onArrived = null;
    }

    void Update()
    {
        if (!_initialized)
            return;

        if (!string.IsNullOrEmpty(_targetObjectId))
        {
            var target = EntityManager.Instance.Get<ShipLog>(_targetObjectId);
            if (target != null)
            {
                _targetPosWu = Utils.LatitudeLongitudeDegHeightFootToVector3(
                    target.position.LatDeg,
                    target.position.LonDeg,
                    _targetHeightFoot
                );
            }
        }

        var dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        var ratio = GameManager.Instance != null ? GameManager.Instance.GetCurrentSimulationAdvanceRatio() : 1f;
        if (ratio <= 0f)
            return;

        var direction = _targetPosWu - transform.position;
        if (direction.sqrMagnitude > 1e-9f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, transform.position.normalized);
        }

        var maxStep = _speedWuPerSecond * dt * ratio;
        transform.position = Vector3.MoveTowards(transform.position, _targetPosWu, maxStep);

        if ((transform.position - _targetPosWu).sqrMagnitude <= 1e-6f)
        {
            var onArrived = _onArrived;
            ResetState();
            onArrived?.Invoke(this);
        }
    }
}
