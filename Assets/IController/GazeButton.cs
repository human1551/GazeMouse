using UnityEngine;
using UnityEngine.UI;
using Experica;

[RequireComponent(typeof(Button))]

public class GazeButton: MonoBehaviour
{
    public Button Button { get; } 
    public Rect ScreenRect { get; set; } // 按钮在屏幕上的矩形范围（像素坐标）
    public float GazeDuration { get; set; } 
    public bool IsGazing { get; set; }

    public GazeButton(Button button)
    {
        Button = button;
        GazeDuration = 0;
        IsGazing = false;
    }
}
