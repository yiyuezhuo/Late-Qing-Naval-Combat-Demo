using NavalCombatCore;
using UnityEngine.UIElements;
using UnityEngine;

public class AutoDeploymentDialog
{
    // public LatLon initialAnchor = new LatLon(){LatDeg=37.5f, LonDeg=123.5f};
    // public float distanceYards = 12000;
    // public float angleDeg = 22.5f;
    // public AutoDeployment.ControlGroupLayoutType controlGroupLayoutType;
    public AutoDeployment autoDeployment = new();

    public void OnCreated(object sender, VisualElement root)
    {
        var ray = CameraController2.Instance.cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var hitPoint = hit.point;

            autoDeployment.initialAnchor = Utils.Vector3ToLatLon(hitPoint);
        }
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        // var autoDeployment = new AutoDeployment()
        // {
        //     distanceYards = distanceYards,
        //     angleDeg = angleDeg,
        //     initialAnchor=initialAnchor, // TODO: Replace it with the value determined the center of screen raycasting
        //     controlGroupLayoutType=controlGroupLayoutType
        // };

        var resultAnchor = autoDeployment.Execute();

        if(resultAnchor == null)
        {
            DialogRoot.Instance.PopupMessageDialog("Auto Deployment failed");
        }
        else
        {
            var c = CameraController2.Instance;
            var xRotation = resultAnchor.LatDeg;
            var yRotation = 360 - resultAnchor.LonDeg;

            c.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);

            if(GameManager.startupConfig.isFromSkirmish)
            {
                DialogRoot.Instance.PopupAIDialog();
            }
        }
    }
}