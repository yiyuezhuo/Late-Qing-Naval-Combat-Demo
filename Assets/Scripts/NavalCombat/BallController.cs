using UnityEngine;
using NavalCombatCore;
using CoreUtils;

public class BallController : MonoBehaviour
{
    Vector3 _targetPosWu;
    float _speedWuPerSecond;
    string _targetObjectId;
    float _targetHeightFoot;
    bool _initialized;

    public void Setup(
        Vector3 startPos,
        Vector3 endPos,
        float speedWuPerSecond,
        string targetObjectId,
        float targetHeightFoot
    )
    {
        transform.position = startPos;
        _targetPosWu = endPos;
        _speedWuPerSecond = Mathf.Max(0.0001f, speedWuPerSecond);
        _targetObjectId = targetObjectId;
        _targetHeightFoot = targetHeightFoot;
        _initialized = true;
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

        var maxStep = _speedWuPerSecond * dt * ratio;
        transform.position = Vector3.MoveTowards(transform.position, _targetPosWu, maxStep);

        if ((transform.position - _targetPosWu).sqrMagnitude <= 1e-6f)
        {
            Destroy(gameObject);
        }
    }
}
