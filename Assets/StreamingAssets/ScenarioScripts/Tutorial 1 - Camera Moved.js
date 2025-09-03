if(phase === Phase.WaitForCameraMove)
{
    let msg = getLocalized(
        `Camera is Moved, now scroll the mouse wheel to zoom map`,
        `カメラが移動しました。マウスホイールをスクロールして地図をズームしてください。`,
        `相机已移动，现在滚动鼠标滚轮以缩放地图。`,
        `相機已移動，現在滾動滑鼠滾輪以縮放地圖。`
    );

    msgBoxDelay(msg, 0.2);
    phase = Phase.WaitForCameraZoom;
}

