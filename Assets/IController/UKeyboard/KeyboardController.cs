using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Keyboard with grouped letters and hover-duration selection:
/// each of the first 8 buttons => 3 letters, last letter button => 2 letters.
/// Hover time windows: [0,0.5) => first, [0.5,1) => second, [1,+inf) => third.
/// While hovering shows preview "点击将输出: X".
/// </summary>
public class KeyboardController : MonoBehaviour
{
    private Label displayText;
    private Label previewText;
    private string inputText = "";

    // mapping for 9 letter buttons
    private readonly string[][] groups = new string[][]
    {
        new[] {"A","B","C"},
        new[] {"D","E","F"},
        new[] {"G","H","I"},
        new[] {"J","K","L"},
        new[] {"M","N","O"},
        new[] {"P","Q","R"},
        new[] {"S","T","U"},
        new[] {"V","W","X"},
        new[] {"Y","Z"} // last group has 2 letters
    };

    // punctuation group for comma button
    private readonly string[] commaGroup = new[] { ",", "." };

    // track hover start times and scheduled items per element
    private readonly Dictionary<VisualElement, float> hoverStart = new Dictionary<VisualElement, float>();
    private readonly Dictionary<VisualElement, IVisualElementScheduledItem> scheduled = new Dictionary<VisualElement, IVisualElementScheduledItem>();

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        displayText = root.Q<Label>("display-text");
        previewText = root.Q<Label>("preview-text");

        if (displayText != null) displayText.text = inputText;
        if (previewText != null) previewText.text = "";

        var grid = root.Q<VisualElement>("keyboard-grid");
        if (grid == null) return;

        foreach (var child in grid.Children())
        {
            if (child is Button btn)
            {
                var captured = btn;

                // handle comma button with two-choice hover logic
                if (captured.name == "btn-comma")
                {
                    captured.RegisterCallback<PointerEnterEvent>((evt) =>
                    {
                        hoverStart[captured] = Time.time;
                        if (!scheduled.ContainsKey(captured))
                        {
                            var item = captured.schedule.Execute(() => {
                                if (!hoverStart.ContainsKey(captured)) return;
                                float dt = Time.time - hoverStart[captured];
                                string candidate = dt < 0.5f ? commaGroup[0] : commaGroup[1];
                                if (previewText != null) previewText.text = $"点击将输出: {candidate}";
                            }).Every(100);
                            scheduled[captured] = item;
                        }
                    });

                    captured.RegisterCallback<PointerLeaveEvent>((evt) =>
                    {
                        if (hoverStart.TryGetValue(captured, out float start))
                        {
                            float dt = Time.time - start;
                            string chosen = dt < 0.5f ? commaGroup[0] : commaGroup[1];
                            inputText += chosen;
                            if (displayText != null) displayText.text = inputText;
                        }
                        hoverStart.Remove(captured);
                        if (scheduled.TryGetValue(captured, out var item))
                        {
                            item.Pause();
                            scheduled.Remove(captured);
                        }
                        ClearPreview();
                        //hoverStart.Remove(captured);
                        //if (scheduled.TryGetValue(captured, out var item))
                        //{
                            //item.Pause();
                            //scheduled.Remove(captured);
                        //}
                       // ClearPreview();
                    });

                    /*captured.clicked += () =>
                    {
                        float start = hoverStart.ContainsKey(captured) ? hoverStart[captured] : -1f;
                        float dt = (start > 0f) ? (Time.time - start) : 0f;
                        string chosen = dt < 0.5f ? commaGroup[0] : commaGroup[1];
                        inputText += chosen;
                        if (displayText != null) displayText.text = inputText;

                        hoverStart.Remove(captured);
                        if (scheduled.TryGetValue(captured, out var item))
                        {
                            item.Pause();
                            scheduled.Remove(captured);
                        }
                        ClearPreview();
                    };*/

                    continue; // done with this button
                }

                // only handle letter buttons btn1..btn9 for grouped letters
                if (captured.name.StartsWith("btn") && TryParseButtonIndex(captured.name, out int index) && index >= 1 && index <= 9)
                {
                    // pointer enter: start timing and begin scheduled preview updates
                    captured.RegisterCallback<PointerEnterEvent>((evt) =>
                    {
                        hoverStart[captured] = Time.time;
                        // schedule periodic preview updates (every 100ms)
                        if (!scheduled.ContainsKey(captured))
                        {
                            var item = captured.schedule.Execute(() => UpdatePreviewForButton(captured)).Every(100);
                            scheduled[captured] = item;
                        }
                    });

                    // pointer leave: stop timing and clear preview
                    captured.RegisterCallback<PointerLeaveEvent>((evt) =>
                    {
                        /*hoverStart.Remove(captured);
                        if (scheduled.TryGetValue(captured, out var item))
                        {
                        item.Pause();
                        scheduled.Remove(captured);
                        }*/
                        // pointer leave: handle input based on hover duration
                        if (hoverStart.TryGetValue(captured, out float start))
                        {
                            float dt = Time.time - start;
                            int groupIndex = index - 1;
                            string chosen = ChooseLetterFromGroup(groupIndex, dt);
                            if (chosen != null)
                            {
                                inputText += chosen;
                                if (displayText != null) displayText.text = inputText;
                            }
                        }
                        hoverStart.Remove(captured);
                        if (scheduled.TryGetValue(captured, out var item))
                        {
                            item.Pause();
                            scheduled.Remove(captured);
                        }
                        ClearPreview();
                    });

                    // click handling: choose letter based on hover duration
                    /*captured.clicked += () =>
                    {
                        float start = hoverStart.ContainsKey(captured) ? hoverStart[captured] : -1f;
                        float dt = (start > 0f) ? (Time.time - start) : 0f;
                        int groupIndex = index - 1;
                        string chosen = ChooseLetterFromGroup(groupIndex, dt);
                        if (chosen != null)
                        {
                            inputText += chosen;
                            if (displayText != null) displayText.text = inputText;
                        }

                        // clear hover/schedule/preview
                        hoverStart.Remove(captured);
                        if (scheduled.TryGetValue(captured, out var item))
                        {
                            item.Pause();
                            scheduled.Remove(captured);
                        }
                        ClearPreview();
                    };*/
                }
                else
                {
                    // handle other non-letter buttons (space, delete) as before
                    if (captured.name == "btn-space" || captured.name == "btn-delete")
                    {
                        /*captured.clicked += () =>
                        {
                            if (captured.name == "btn-delete")
                            {
                                if (inputText.Length > 0) inputText = inputText.Substring(0, inputText.Length - 1);
                            }
                            else if (captured.name == "btn-space")
                            {
                                inputText += " ";
                            }

                            if (displayText != null) displayText.text = inputText;
                            ClearPreview();
                        };*/
                        captured.RegisterCallback<PointerEnterEvent>((evt) =>
                        {
                            if (previewText != null)
                            {
                                string hint = captured.name == "btn-delete" ? "离开将删除最后一位" : "离开将插入空格";
                                previewText.text = hint;
                            }
                        });

                        /*captured.RegisterCallback<PointerEnterEvent>((evt) =>
                        {
                            if (previewText != null)
                            {
                                string hint = captured.name == "btn-delete" ? "点击删除最后一位" : "点击插入空格";
                                previewText.text = hint;
                            }
                        });
                        captured.RegisterCallback<PointerLeaveEvent>((evt) => ClearPreview());*/
                        captured.RegisterCallback<PointerLeaveEvent>((evt) =>
                        {
                            if (captured.name == "btn-delete")
                            {
                                if (inputText.Length > 0) inputText = inputText.Substring(0, inputText.Length - 1);
                            }
                            else if (captured.name == "btn-space")
                            {
                                inputText += " ";
                            }

                            if (displayText != null) displayText.text = inputText;
                            ClearPreview();
                        });
                    }
                }
            }
        }
    }

    private void UpdatePreviewForButton(VisualElement btn)
    {
        if (!hoverStart.ContainsKey(btn)) return;
        float dt = Time.time - hoverStart[btn];
        if (!TryParseButtonIndex(btn.name, out int idx)) return;
        int groupIndex = idx - 1;
        string candidate = ChooseLetterFromGroup(groupIndex, dt);
        if (candidate != null && previewText != null)
        {
            previewText.text = $"离开将输出: {candidate}";
        }
    }

    // choose letter according to duration windows
    private string ChooseLetterFromGroup(int groupIndex, float dt)
    {
        if (groupIndex < 0 || groupIndex >= groups.Length) return null;
        var arr = groups[groupIndex];
        //int pick = 0;
        float letterDuration = 0.8f; 
        float cycleDuration = letterDuration * arr.Length; 
        float remainder = dt % cycleDuration; 
        int pick = Mathf.FloorToInt(remainder / letterDuration); // 计算当前循环到的字母索引
        //if (dt < 0.8f) pick = 0;
        //else if (dt < 1.6f) pick = 1;
        //else pick = 2;

        // clamp to available letters
        if (pick >= arr.Length) pick = arr.Length - 1;
        return arr[pick];
    }

    private void ClearPreview()
    {
        if (previewText != null) previewText.text = "";
    }

    // parse names like "btn1" -> index 1
    private bool TryParseButtonIndex(string name, out int index)
    {
        index = -1;
        if (string.IsNullOrEmpty(name)) return false;
        if (name.StartsWith("btn") && int.TryParse(name.Substring(3), out index)) return true;
        return false;
    }
}
