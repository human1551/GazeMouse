using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

public class ButtonColorChanger1 : MonoBehaviour
{
    private Button KeyButton;
    private Color originalColor; // 保存原始颜色（白色）
    private Color hoverColor = new Color(0.5f, 0.5f, 0.5f); // 灰色（悬浮状态）
    private Color pressColor = Color.black; // 黑色（点击状态）c

    void OnEnable()
    {
        // 获取UI文档根元素
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        // 获取按钮引用
        
        KeyButton = root.Q<Button>("Q");
        
        //初始颜色为白色
        originalColor = Color.white;

        // 注册事件回调
        if (KeyButton != null)
        {
            // 鼠标进入（悬浮）事件
            KeyButton.RegisterCallback<MouseOverEvent>(OnMouseHover);
            // 鼠标离开事件
            KeyButton.RegisterCallback<MouseOutEvent>(OnMouseExit);
            // 鼠标按下（点击）事件
            KeyButton.RegisterCallback<MouseDownEvent>(OnMousePress);
            // 鼠标释放事件
            KeyButton.RegisterCallback<MouseUpEvent>(OnMouseRelease);

            // 初始设置为原始颜色
            SetButtonColor(KeyButton, originalColor);
        }
    }

    // 鼠标悬浮时
    private void OnMouseHover(MouseOverEvent evt)
    {
        SetButtonColor(KeyButton, hoverColor);
    }

    // 鼠标离开时
    private void OnMouseExit(MouseOutEvent evt)
    {
        SetButtonColor(KeyButton, originalColor);
    }

    // 鼠标按下时（点击）
    private void OnMousePress(MouseDownEvent evt)
    {
        SetButtonColor(KeyButton, pressColor);
    }

    // 鼠标释放时
    private void OnMouseRelease(MouseUpEvent evt)
    {
        // 释放时根据鼠标是否还在按钮上决定恢复哪种颜色
        if (KeyButton.ContainsPoint(evt.localMousePosition))
        {
            SetButtonColor(KeyButton, hoverColor); // 仍在按钮上则恢复悬浮色
        }
        else
        {
            SetButtonColor(KeyButton, originalColor); // 已离开则恢复原始色
        }
    }



   // 设置按钮颜色的通用方法
    private void SetButtonColor(Button button, Color color)
    {
        if (button == null) return;

        // 设置按钮背景颜色
        button.style.backgroundColor = color;

        // 同步调整文字颜色以保证可读性
        AdjustTextColor(button, color);
    }

    // 调整文字颜色确保可读性
    private void AdjustTextColor(Button button, Color bgColor)
    {
        // 计算亮度
        float brightness = (bgColor.r * 0.299f) + (bgColor.g * 0.587f) + (bgColor.b * 0.114f);

        // 根据背景亮度设置文字颜色（黑/白）
        Color textColor = brightness < 0.5f ? Color.white : Color.black;

        // 应用文字颜色
        button.style.color = textColor;
    }

    void OnDisable()
    {
        // 移除事件回调避免内存泄漏
        if (KeyButton != null)
        {
            KeyButton.UnregisterCallback<MouseOverEvent>(OnMouseHover);
            KeyButton.UnregisterCallback<MouseOutEvent>(OnMouseExit);
            KeyButton.UnregisterCallback<MouseDownEvent>(OnMousePress);
            KeyButton.UnregisterCallback<MouseUpEvent>(OnMouseRelease);
        }
    }
}