using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 날짜 : 2021-01-19 PM 5:01:57
// 작성자 : Rito

[ExecuteInEditMode]
public class PixelCamera : MonoBehaviour
{
    [Range(1, 100)]
    public int _pixelate = 5; // 기본값을 5로 변경하여 픽셀화 효과를 바로 확인할 수 있도록 합니다.

    public bool _showGUI = true;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        source.filterMode = FilterMode.Point;
        RenderTexture resultTexture = RenderTexture.GetTemporary(source.width / _pixelate, source.height / _pixelate, 0, source.format);
        resultTexture.filterMode = FilterMode.Point;

        Graphics.Blit(source, resultTexture);
        Graphics.Blit(resultTexture, destination);
        RenderTexture.ReleaseTemporary(resultTexture);
    }

    private void OnGUI()
    {
        if (!_showGUI) return;
        string text = $"Pixelate : {_pixelate,3}";

        Rect textRect = new Rect(60f, 60f, 440f, 100f);
        Rect boxRect = new Rect(40f, 40f, 460f, 120f);

        GUIStyle boxStyle = GUI.skin.box;
        GUI.Box(boxRect, "", boxStyle);

        GUIStyle textStyle = GUI.skin.label;
        textStyle.fontSize = 70;
        GUI.Label(textRect, text, textStyle); // 텍스트 표시에는 GUI.Label이 더 적절합니다.
    }
}