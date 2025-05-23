
using UnityEngine;
using System;
using System.Collections.Generic;
using NavalCombatCore;

public class IDF3ModelViewer: MonoBehaviour
{
    public IDF3Model model;
    public float scaleFactor = 1;
    public GameObject headingArrow;
    protected Material material;


    public bool hovering;
    public bool selected;
    protected SelectState oldSelectState;

    public void Awake()
    {
        var meshRenderer = GetComponent<MeshRenderer>();
        material = meshRenderer.material = meshRenderer.material; // copy material
    }

    public enum SelectState
    {
        None = 0,
        Hovering = 1,
        Selected = 2
    }

    public void SyncSelectState(SelectState state)
    {
        var value = (float)(int)state;
        material.SetFloat("_ShowBorder", value);
    }

    public SelectState GetSelectState()
    {
        if(selected)
            return SelectState.Selected;
        else if(hovering)
            return SelectState.Hovering;
        return SelectState.None;
    }

    public void Update()
    {
        if(model == null)
            return;

        // var cam = CameraController.Instance.camIcon;
        var cam = CameraController2.Instance.cam;

        transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward,
                         cam.transform.rotation * Vector3.up);

        transform.localScale = Vector3.one * cam.orthographicSize * scaleFactor;

        transform.localPosition = Utils.LatitudeLongitudeDegToVector3(model.GetLatitudeDeg(), model.GetLongitudeDeg());


        var selectState = GetSelectState();
        if(selectState != oldSelectState)
        {
            SyncSelectState(selectState);
            oldSelectState = selectState;
        }

        var headingArrowR = 0.8f;
        var zDeg = Utils.TrueNorthClockwiseDegToUnityDeg(model.GetHeadingDeg());
        headingArrow.transform.localEulerAngles = new Vector3(0, 0, zDeg);
        
        var zRag = zDeg * Mathf.Deg2Rad;
        var xOffset = Mathf.Cos(zRag) * headingArrowR;
        var yOffset = Mathf.Sin(zRag) * headingArrowR;
        headingArrow.transform.localPosition = new Vector3(xOffset, yOffset, 0);

        // TODO: Handle left click navigation here?
    }
}