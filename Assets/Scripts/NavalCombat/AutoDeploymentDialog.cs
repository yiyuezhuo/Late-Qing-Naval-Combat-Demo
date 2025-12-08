using NavalCombatCore;
using UnityEngine.UIElements;

public class AutoDeploymentDialog
{
    public void OnCreated(object sender, VisualElement root)
    {
        
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        var autoDeployment = new AutoDeployment()
        {
            initialAnchor=new LatLon(){LatDeg=37.5f, LonDeg=123.5f} // TODO: Replace it with the value determined the center of screen raycasting
        };

        var ok = autoDeployment.Execute();

        if(!ok)
        {
            DialogRoot.Instance.PopupMessageDialog("Auto Deployment failed");
        }
    }
}