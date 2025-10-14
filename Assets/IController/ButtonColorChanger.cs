using UnityEngine;
using UnityEngine.UIElements;

public class ButtonColorChanger : MonoBehaviour
{
    public Button startButton;

    void Start()
    {
        // 加载 UXML 文件
        var asset = Resources.Load<VisualTreeAsset>("TestButton");
        var root = asset.CloneTree();
        this.AddUIElements(root);

        void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            // 获取按钮引用
            startButton = root.Q<Button>("StartButton");

            // 添加点击事件监听器
            if (startButton != null)
            {
                startButton.clicked += () => ChangeButtonColor(startButton);

                // 可以初始设置一种颜色
                //SetButtonColor(startButton, new Color(0.2f, 0.6f, 1f)); // 初始蓝色
            }
        }
    }

    void ChangeButtonColor(Button button)
    {
        // 改变背景颜色和字体颜色
        button.style.backgroundColor = new StyleColor(Color.blue);
        button.style.color = new StyleColor(Color.white);
    }

    private void AddUIElements(VisualElement visualElement)
    {
        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument != null)
        {
            uiDocument.rootVisualElement.Add(visualElement);
        }
        else
        {
            Debug.LogError("UIDocument component not found on the GameObject.");
        }
    }
}