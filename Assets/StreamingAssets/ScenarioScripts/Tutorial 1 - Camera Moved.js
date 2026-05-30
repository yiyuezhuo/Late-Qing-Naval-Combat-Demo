if(phase === Phase.WaitForCameraMove)
{
    let msg = getLocalized(
        `Camera is moved. Scroll the mouse wheel to zoom the map. On phones and tablets, pinch with two fingers.`,
        `カメラが移動しました。マウスホイールをスクロールして地図をズームしてください。モバイル端末では2本指でピンチしてズームできます。`,
        `相机已移动。现在滚动鼠标滚轮缩放地图；在移动端，可以双指捏合缩放。`,
        `相機已移動。現在滾動滑鼠滾輪縮放地圖；在行動裝置上，可以雙指捏合縮放。`
    );

    msgBoxDelay(msg, 0.2);
    phase = Phase.WaitForCameraZoom;
}

