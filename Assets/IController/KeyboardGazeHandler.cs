using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class KeyboardGazeHandler : MonoBehaviour
{
    [SerializeField] private UIDocument uiDoc;
    private List<Button> _keyButtons;
    private Dictionary<Button, Rect> _buttonScreenRects = new(); // 缓存屏幕矩形

    // 初始化：获取所有按键并监听布局变化
    private void OnEnable()
    {
        if (uiDoc == null) return;
        var root = uiDoc.rootVisualElement;
        _keyButtons = root.Query<Button>().Class("key-button").ToList();

        foreach (var btn in _keyButtons)
        {
            btn.RegisterCallback<GeometryChangedEvent>(_ => UpdateButtonRect(btn));
            UpdateButtonRect(btn); // 初始计算
        }
    }

    // 更新按钮屏幕坐标（纯UI Toolkit方法，兼容各版本）
    private void UpdateButtonRect(Button btn)
    {
        var root = uiDoc.rootVisualElement;
        var worldPos = btn.LocalToWorld(Vector2.zero); // UI根坐标系位置
        var worldSize = btn.LocalToWorld(btn.layout.size) - worldPos; // 实际大小

        // 转换为屏幕坐标（处理Y轴翻转）
        var screenPos = new Vector2(worldPos.x, Screen.height - worldPos.y);
        _buttonScreenRects[btn] = new Rect(screenPos, worldSize);
    }

    // 公开方法：判断注视点是否在某按钮上
    public Button GetGazedButton(Vector2 gazeScreenPos)
    {
        foreach (var (btn, rect) in _buttonScreenRects)
        {
            if (rect.Contains(gazeScreenPos)) return btn;
        }
        return null;
    }

    // 公开属性：获取所有按键（供Control初始化）
    public List<Button> KeyButtons => _keyButtons;
}