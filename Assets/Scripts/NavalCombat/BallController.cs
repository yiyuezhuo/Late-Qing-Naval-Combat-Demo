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
    Vector3 _targetOffsetWu;
    bool _hit;
    bool _targetIsLandBattery;
    float _shellDiameterInch;
    bool _initialized;
    Action<BallController> _onArrived;
    int _lastAdvancedFrame = -1;
    int _spawnedFrame = -1;

    public bool IsSpawnedThisFrame => _spawnedFrame == Time.frameCount;
    public bool Hit => _hit;
    public bool TargetIsLandBattery => _targetIsLandBattery;
    public float ShellDiameterInch => _shellDiameterInch;
    public string TargetObjectId => _targetObjectId;
    public Vector3 TargetPositionWu => _targetPosWu;

    public void Setup(
        Vector3 startPos,
        Vector3 endPos,
        float speedWuPerSecond,
        string targetObjectId,
        float targetHeightFoot,
        Vector3 targetOffsetWu,
        bool hit,
        bool targetIsLandBattery,
        float shellDiameterInch,
        Action<BallController> onArrived = null
    )
    {
        transform.position = startPos;
        _targetPosWu = endPos;
        _speedWuPerSecond = Mathf.Max(0.0001f, speedWuPerSecond);
        _targetObjectId = targetObjectId;
        _targetHeightFoot = targetHeightFoot;
        _targetOffsetWu = targetOffsetWu;
        _hit = hit;
        _targetIsLandBattery = targetIsLandBattery;
        _shellDiameterInch = shellDiameterInch;
        _onArrived = onArrived;
        _initialized = true;
        _lastAdvancedFrame = -1;
        _spawnedFrame = Time.frameCount;
        gameObject.SetActive(true);
    }

    public void ResetState()
    {
        _initialized = false;
        _targetObjectId = null;
        _targetPosWu = default;
        _targetHeightFoot = 0f;
        _targetOffsetWu = default;
        _hit = false;
        _targetIsLandBattery = false;
        _shellDiameterInch = 0f;
        _onArrived = null;
        _lastAdvancedFrame = -1;
        _spawnedFrame = -1;
    }

    void SyncTargetPosition()
    {
        if (!_initialized)
            return;

        if (!string.IsNullOrEmpty(_targetObjectId))
        {
            var target = EntityManager.Instance.Get<ShipLog>(_targetObjectId);
            if (target != null)
            {
                var targetBasePosWu = Utils.LatitudeLongitudeDegHeightFootToVector3(
                    target.position.LatDeg,
                    target.position.LonDeg,
                    _targetHeightFoot
                );
                var tangentOffsetWu = Vector3.ProjectOnPlane(_targetOffsetWu, targetBasePosWu.normalized);
                _targetPosWu = targetBasePosWu + tangentOffsetWu;
            }
        }
    }

    public void AdvanceBySimulationSeconds(float simulationSeconds)
    {
        if (!_initialized || simulationSeconds <= 0f)
            return;
        if (_lastAdvancedFrame == Time.frameCount)
            return;
        _lastAdvancedFrame = Time.frameCount;

        SyncTargetPosition();

        var direction = _targetPosWu - transform.position;
        if (direction.sqrMagnitude > 1e-9f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, transform.position.normalized);
        }

        var maxStep = _speedWuPerSecond * simulationSeconds;
        transform.position = Vector3.MoveTowards(transform.position, _targetPosWu, maxStep);

        if ((transform.position - _targetPosWu).sqrMagnitude <= 1e-6f)
        {
            _onArrived?.Invoke(this);
        }
    }

    void Update()
    {
        if (!_initialized || _lastAdvancedFrame == Time.frameCount)
            return;

        if (GameManager.Instance != null)
            return;

        var dt = Time.unscaledDeltaTime;
        if (dt <= 0f)
            return;
        AdvanceBySimulationSeconds(dt);
    }
}
