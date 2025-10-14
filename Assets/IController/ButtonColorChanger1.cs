using UnityEngine;
using UnityEngine.UIElements;

public class ButtonColorChanger1 : MonoBehaviour
{
    private Button startButton;

    void OnEnable()
    {
        // 获取UI文档根元素
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        // 获取按钮引用
        startButton = root.Q<Button>("StartButton");

        // 添加点击事件监听器
        if (startButton != null)
        {
            startButton.clicked += () => ChangeButtonColor(startButton);

            // 可以初始设置一种颜色
            SetButtonColor(startButton, new Color(0.2f, 0.6f, 1f)); // 初始蓝色
        }
    }

    // 改变按钮颜色的方法
    private void ChangeButtonColor(Button button)
    {
        // 生成随机颜色
        Color randomColor = new Color(
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            Random.Range(0f, 1f)
        );

        SetButtonColor(button, randomColor);
    }

    // 设置按钮颜色的通用方法
    private void SetButtonColor(Button button, Color color)
    {
        if (button == null) return;

        // 直接设置按钮的背景颜色
        button.style.backgroundColor = color;

        // 可以同时调整文本颜色以确保可读性
        AdjustTextColor(button, color);
    }

    // 调整文本颜色以确保与背景的对比度
    private void AdjustTextColor(Button button, Color bgColor)
    {
        // 计算颜色亮度
        float brightness = (bgColor.r * 0.299f) + (bgColor.g * 0.587f) + (bgColor.b * 0.114f);

        // 如果背景较暗，文本用白色；否则用黑色
        Color textColor = brightness < 0.5f ? Color.white : Color.black;

        // 应用文本颜色
        button.style.color = textColor;
    }
}
