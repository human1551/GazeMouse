using UnityEngine;
using UnityEngine.UIElements;   
using System.Collections;
using System.Collections.Generic;

public class Click : MonoBehaviour
{
    private UIDocument _document;
    private Button _button;
    private List<Button> _menuButtons = new List<Button>();

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
        _button = _document.rootVisualElement.Q<Button>("StartButton");
        _button.RegisterCallback<ClickEvent>(OnClick);
    }

    private void OnDisable()
    {
        _button.UnregisterCallback<ClickEvent>(OnClick);
    }

    private void OnClick(ClickEvent evt)
    {
        Debug.Log("Button Clicked!");
    }
}
   

