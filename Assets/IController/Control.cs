/*
Fixation.cs is part of the Experica.
Copyright (c) 2016 Li Alex Zhang and Contributors

Permission is hereby granted, free of charge, to any person obtaining a 
copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the 
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included 
in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF 
OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using Experica;
using Experica.Command;
using Experica.NetEnv;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Eye Fixation Task, with User Input Action mimicking eye movement, and helpful visual guides.
/// </summary>
public class Control : ExperimentLogic
{
    public double FixOnTime, FixTargetOnTime, FixDur, WaitForFixTimeOut;
    public double FixHold => TimeMS - FixOnTime;
    public double WaitForFix => TimeMS - FixTargetOnTime;

    public double RandPreITIDur => RNG.Next(GetExParam<int>("MinPreITIDur"), GetExParam<int>("MaxPreITIDur"));
    public double RandSufITIDur => RNG.Next(GetExParam<int>("MinSufITIDur"), GetExParam<int>("MaxSufITIDur"));
    public double RandFixDur => RNG.Next(GetExParam<int>("MinFixDur"), GetExParam<int>("MaxFixDur"));

    //[SerializeField]不在inspector上显示，屈服了……
    private List<Button> keyButtons = new List<Button>(); //只负责找到按键位置，不负责按键功能
    private const float FALL_THRESHOLD = 0.2f;   // 置信度下降阈值
    private const float RISE_THRESHOLD = 0.7f;   // 置信度上升阈值
    private float fixationTriggerThreshold = 0.5f; // 注视触发阈值（秒）

    private enum GazeState { Disabled, Enabled }
    private GazeState currentGazeState = GazeState.Disabled;
    private Dictionary<Button, float> buttonGazeTimes = new();
    private Button currentGazedButton;
    //private bool _isGazeHandlerEnabled = false; //
    //private bool _isBlinkDetected = false; //
    //private Button prevGazedButton;
    private bool isLeftBlinkHandled = false;


    private bool _hasFallenBelowThreshold = false;  
    private bool _blinkTriggered = false;  
    private float _lastConfidence = -1f;
    private DateTime _lastLeftBlinkTime = DateTime.MinValue;
    private DateTime _lastRightBlinkTime = DateTime.MinValue;

    public InputAction MoveAction;
    public IEyeTracker EyeTracker;
    public Vector2 FixPosition;
    //protected NetworkVariable<Vector3> fixdotposition;
    protected Vector3 fixdotposition;
    protected List<ScaleGrid> scalegrid = new();
    protected bool updatefixtrail, recordgaze;
    //TagLocal tag;
    DotTrailLocal fixtrail;
    Circle fixcircle;
    public GameObject tagPrefab; // 引用Tag预制体
    INetEnvCamera mainCamera;
    //private Tag spawnedTag;


    protected override void Enable()
    {
        base.Enable();
        var uiDocument = GameObject.Find("KeyboardUI")?.GetComponent<UIDocument>(); //键盘按钮是找到了的
        if (uiDocument == null)
        {
            return;
        }
        var root = uiDocument.rootVisualElement;
        var keyButtonsFromUxml = root.Query<Button>()
            .Where(btn => btn.ClassListContains("key-button"))
            .ToList();
        
        keyButtons.Clear(); 
        keyButtons.AddRange(keyButtonsFromUxml);
        if (keyButtons != null && keyButtons.Count > 0)
        {
            InitGazeTracking(); 
        }
        else
        {
            Debug.LogWarning("未添加键盘按钮，注视交互功能禁用");
        }

        MoveAction = InputSystem.actions.FindActionMap("Logic").FindAction("Move");
        EyeTracker = Experica.IEyeTracker.PupilLabsCore.TryGetPupilLabsCore(); //public class PupilLabsCore : IEyeTracker 在EyeTracker.cs里

        if (EyeTracker is Experica.IEyeTracker.PupilLabsCore pupil)
        {
            pupil.LeftEyeBlinked += OnLeftEyeBlinked;
            pupil.RightEyeBlinked += OnRightEyeBlinked;
        }
    }

    private void OnLeftEyeBlinked() //
    {
        if (currentGazeState == GazeState.Disabled)
        {
            currentGazeState = GazeState.Enabled;
            Debug.Log("左眼眨眼 - 启用注视交互");
        }
    }

    private void OnRightEyeBlinked()
    {
        if (currentGazeState == GazeState.Enabled)
        {
            //ResetGazeState();
            //Debug.Log("右眼眨眼 - 退出注视交互");
        }
    }

    private void InitGazeTracking()
    {
        // 缓存所有按钮的初始注视时间
        foreach (var btn in keyButtons)
        {
            if (btn == null) continue; 
            if (!buttonGazeTimes.ContainsKey(btn))
            {
                buttonGazeTimes[btn] = 0;
            }
        }
    }

    protected override void Disable()
    {
        base.Disable();

        if (EyeTracker is Experica.IEyeTracker.PupilLabsCore pupil)
        {
            pupil.LeftEyeBlinked -= OnLeftEyeBlinked;
            pupil.RightEyeBlinked -= OnRightEyeBlinked;
        }
        if (EyeTracker != null)
        {
            EyeTracker.Dispose();
            EyeTracker = null;
        }

    }
    [SerializeField] public int targetCameraIndex = 0;

    //public override void OnSceneReady(List<ulong> clientids)
    //{
        /*fixdotposition = envmgr.GetNetworkVariable<Vector3>("FixDotPosition");
        scalegrid.Clear();
        if (clientids.Count == 0) { return; }
        //这段改成本地相机（可手动设置是第几个相机）
        for (var i = 0; i < clientids.Count; i++)
        {
            var cname = $"OrthoCamera{(i == 0 ? "" : i)}";
            var oc = envmgr.SpawnTagMarkerOrthoCamera(cname, clientid: clientids[i]);
            oc.OnCameraChange += _ => appmgr.ui.UpdateView();
            // we want scalegrid to center on FixDot, so here spawn as a child of FixDot
            var sg = envmgr.SpawnScaleGrid(oc, clientid: clientids[i], parse: false, parent: fixdotposition.GetBehaviour().transform);
            scalegrid.Add(sg);
        }
        tag = envmgr.GetNetworkVariableByGameObject<float>("TagMargin", "OrthoCamera/Tag0").GetBehaviour() as Tag; //tag也在hierarchy里，找到它*/   
        
    //}


    private void Start()
    {
        mainCamera = GameObject.Find("TagMarkerOrthoCamera").GetComponent<INetEnvCamera>(); //找到本地相机 
        //var tagObj = GameObject.Find($"{cname}/Tag0");
        //if (tagObj != null)
        //{
        //    tag = tagObj.GetComponent<TagLocal>();
        //    tagObj.SetActive(true);


        //    if (tag != null && tag.GetType().GetProperty("Visible") != null)
        //    {
        //        tag.GetType().GetProperty("Visible").SetValue(tag, true);
        //    }
        //}
        //else
        //{
        //    Debug.LogWarning($"未在Hierarchy找到Tag，路径：{cname}/Tag0");
        //}

        var trailObj = GameObject.Find("DotTrail"); 
        if (trailObj != null)
        {
            fixtrail = trailObj.GetComponent<DotTrailLocal>();
            trailObj.SetActive(true);

            
            if (fixtrail != null)
            {
                fixtrail.Position = Vector3.back;
                fixtrail.Size = new(0.25f, 0.25f, 1);
                fixtrail.Color = new(1, 0.1f, 0.1f);
            }
        }
        else
        {
            Debug.LogWarning("未在Hierarchy找到FixTrail，请检查对象名是否为'FixTrail'");
        }
    }
    /// <summary>
    /// add helpful visual guides
    /// </summary>
    //public override void OnPlayerReady()
    //{
        //Action<NetEnvVisual, Vector3> upxy = (o, p) => o.Position.Value = new(p.x, p.y, o.Position.Value.z);
        //本地有tagmarker和dottrail，find game object instead of spawn new ones
        //var fixradius = (float)ex.ExtendParam["FixRadius"];
        // here also spawn as a child of FixDot, so the circle would center FixDot
        //fixcircle = envmgr.SpawnCircle(color: new(0.1f, 0.8f, 0.1f), size: new(2 * fixradius, 2 * fixradius, 1), parse: false, parent: fixdotposition.GetBehaviour().transform);
        // hook a ExtendParam to a NetworkVariable
        //ex.extendproperties["FixRadius"].propertyChanged += (o, e) => fixcircle.Size.Value = new(2 * (float)ex.ExtendParam["FixRadius"], 2 * (float)ex.ExtendParam["FixRadius"], 1);
        // tracing fixation position
        //fixtrail = envmgr.SpawnDotTrail(position: Vector3.back, size: new(0.25f, 0.25f, 1), color: new(1, 0.1f, 0.1f), parse: false); //find dottrail game object
      
    //}

    //这个可以改成在在注视点上将注视时间可视化的进度条
    protected override void PrepareCondition()
    {
        var pos = GetExParam<List<Vector3>>("FixDotPosition");
        if (pos == null || pos.Count == 0)
        {
            pos = new List<Vector3>() { Vector3.zero };
        }
        var cond = new Dictionary<string, List<object>>()
        {
            ["FixDotPosition"] = pos.Cast<object>().ToList(),
        };
        condmgr.PrepareCondition(cond);
    }

    public override bool Guide
    {
        get => scalegrid.Count > 0 && scalegrid.First().gameObject.activeSelf;
       
        //get
        //{
            //if (scalegrid.Count == 0) { return false; }
            //return scalegrid.First().Visible.Value;
        //}
        set
        {
            foreach (var sg in scalegrid) { sg.Visible.Value = value; }
            if (fixcircle != null) {/* fixcircle.Visible.Value = value;*/fixcircle.gameObject.SetActive(value); }
            if (fixtrail != null) { fixtrail.gameObject.SetActive(value); }
        }
    }

    //public override bool NetVisible
    //{
    //    get
    //    {
    //        if (scalegrid.Count == 0) { return false; }
    //        var sg = scalegrid.First();
    //        return !sg.NetworkObject.IsNetworkHideFromAll();
    //    }
    //    set
    //    {
    //        foreach (var sg in scalegrid)
    //        {
    //            if (value) { sg.NetworkObject.NetworkShowOnlyTo(sg.ClientID); }
    //            else { sg.NetworkObject.NetworkHideFromAll(); }
    //        }
    //        if (fixcircle != null) { fixcircle.NetworkObject.NetworkShowHideAll(value); }
    //        if (fixtrail != null) { fixtrail.NetworkObject.NetworkShowHideAll(value); }
    //    }
    //}


    [SerializeField][Min(0)] private float surfaceMargin = 5f; //继续调整surfaceMargin
    protected override void OnUpdate() //Onupdate里完全没有用到GazeState
    {
        base.OnUpdate();


        if (/*TrialState != TRIALSTATE.TRIAL ||*/ EyeTracker == null)
        {
            ResetGazeState();
            return;
        }

        var currentConfidence = EyeTracker.Confidence;

        var normalizedGaze = EyeTracker.Gaze2D;
       // normalizedGaze.x = 1.0f - normalizedGaze.x;
        normalizedGaze.y = 1.0f - normalizedGaze.y;
        Debug.Log($"原始 Gaze2D: X={normalizedGaze.x:F3}, Y={normalizedGaze.y:F3}");
        //float surfaceMargin = tag.TagSurfaceMargin;
        Vector2 screenGazePos = Vector2.zero;
        screenGazePos = surfacegaze2cameragaze(normalizedGaze, mainCamera, surfaceMargin);
        var newGazedButton = GetCurrentGazedButton(screenGazePos, mainCamera, surfaceMargin); //注视点在按钮上的时候发送hover，注视点离开按钮发送leave

        if (newGazedButton != currentGazedButton) //改成fixtrail位置和按钮位置?
        {
            if (currentGazedButton != null)
            {
                currentGazedButton.SendEvent(new PointerLeaveEvent());
            }

            if (newGazedButton != null)
            {
                newGazedButton.SendEvent(new PointerEnterEvent());
            }

            currentGazedButton = newGazedButton;
        }



        //处理注视计时（这个需要处理吗？KeyboardController里有）
        if (currentGazeState == GazeState.Enabled)
        {
            UpdateGazeTimer(EyeTracker.Confidence);
        }

        ////在屏幕任意位置眨左眼，启用注视交互 看起来没用但注释掉会出问题
        //if (_isBlinkDetected)
        //{
        //    currentGazeState = GazeState.Enabled;
        //    _isBlinkDetected = false;
        //    Debug.Log($"检测到任意位置眨左眼，启用注视交互");

        //}

        if (currentGazeState == GazeState.Enabled)
        {
            UpdateGazeTimer(currentConfidence);
        }
        _lastConfidence = currentConfidence;

        //if (ex.Input && /*envmgr.MainCamera.Count > 0*/mainCamera != null && MoveAction.phase == InputActionPhase.Started)
        //{
        //    FixPosition += MoveAction.ReadValue<Vector2>();
        //    clampMove(ref FixPosition);
        //    updatefixtrail = true;
        //}

        if (EyeTracker != null && mainCamera != null)
        {
            FixPosition = surfacegaze2cameragaze(EyeTracker.Gaze2D, /*envmgr.MainCamera.First()*/mainCamera, surfaceMargin); //envmgr.MainCamera.First()相关改掉
            if (recordgaze && ex.HasCondTestState())
            {
                condtestmgr.AddInList(nameof(CONDTESTPARAM.Gaze), TimeMS, FixPosition);
            }
            updatefixtrail = true;
        }

        if (updatefixtrail && fixtrail != null /*&& fixtrail.Visible.Value*/)
        {
            fixtrail.Position = FixPosition;
            updatefixtrail = false;
        }
    }

    private Vector2 ButtonPositionConverter(VisualElement button, INetEnvCamera camera, float surfacemargin)
    {
        Rect buttonWorldBounds = button.worldBound;
        return buttonWorldBounds.center;
    }
    //private Vector2 ButtonPositionConverter(VisualElement button, INetEnvCamera camera, float surfacemargin) //键盘转换成world space了这里的坐标转换函数要改动吗
    //{

    //    // 1. 获取按钮在UI中的世界坐标边界（包含中心位置）
    //    Rect buttonBounds = button.worldBound;
    //    Vector2 buttonCenter = buttonBounds.center; // 按钮中心的UI坐标（像素级）

    //    // 2. 计算相机有效区域（去除边距后的宽高）
    //    float effectiveWidth = camera.Width - 2 * surfacemargin;
    //    float effectiveHeight = camera.Height - 2 * surfacemargin;

    //    // 3. 归一化到 [0,1] 范围，翻转轴
    //    float normalizedX = buttonCenter.x / effectiveWidth;
    //    float normalizedY = buttonCenter.y / effectiveHeight; 

    //    // 用现有方法将键盘按钮的归一化坐标转换为相机坐标
    //    return surfacegaze2cameragaze(new Vector2(normalizedX, normalizedY), camera, surfacemargin);
    //}

    //将归一化坐标转换为屏幕坐标（边界，小区域）
    Vector2 surfacegaze2cameragaze(Vector2 sg, INetEnvCamera camera, float surfacemargin) 
    {
        //sg.x = sg.x - 0.5f;
        //sg.y = sg.y - 0.5f;
        //return new Vector2(sg.x * (camera.Width - 2 * surfacemargin), sg.y * (camera.Height - 2 * surfacemargin));
        Debug.Log($"camera.Height: {camera.Height}, camera.Width: {camera.Width}");
        float effectiveWidth = camera.Width - 2 * surfacemargin;
        float effectiveHeight = camera.Height - 2 * surfacemargin;

        // 2. 计算相对于视口中心的偏移量（世界单位）
        float offsetX = (sg.x - 0.5f) * effectiveWidth;
        float offsetY = (sg.y - 0.5f) * effectiveHeight;

        // 3. 将偏移量加到相机的世界位置上，得到最终的世界坐标
        Vector3 worldPos = Vector3.zero;
        worldPos.x += offsetX;
        worldPos.y += offsetY;
        return worldPos;
    }

    void clampMove(ref Vector2 pos)
    {
        var r = GetExParam<float>("MoveRadius");
        if (r == 0) { r = 50; }
        pos.x = Mathf.Clamp(pos.x, -r, r);
        pos.y = Mathf.Clamp(pos.y, -r, r);
    }

    private Button _previousHitButton;
    private Button GetCurrentGazedButton(Vector2 fixWorldPos, INetEnvCamera mainCamera, float surfaceMargin)
    {
        Button hitButton = null;  //hitBButton为什么一直是null？
        float minDistance = float.MaxValue; // 记录最小距离（优先命中最近的按钮）
        
        foreach (var button in keyButtons) //手动添加
        {
            if (button == null) continue;

            Vector2 buttonWorldPos = ButtonPositionConverter(button, mainCamera, surfaceMargin);
            //用fixtrail？
            //Vector3 fixPosWithZ = new Vector3(FixPosition.x, FixPosition.y, mainCamera.NearPlane);
            //Vector2 fixWorldPos = mainCamera.Camera.ScreenToWorldPoint(fixPosWithZ); // 转换为世界单位
            Rect buttonBounds = button.worldBound;
            //Vector3 buttonWorldCenter = buttonBounds.center; // 转 Vector3（WorldToScreenPoint 需 Z 轴）
            //Vector3 buttonScreenPos3D = mainCamera.Camera.WorldToScreenPoint(buttonWorldCenter);
            //Vector2 buttonScreenPos = new Vector2(buttonScreenPos3D.x, buttonScreenPos3D.y); // 转 Vector2
            Vector2 realButtonCenter = buttonBounds.center; // 按钮真实世界中心（World Space）
            Vector2 convertedCenter = ButtonPositionConverter(button, mainCamera, surfaceMargin); // 转换后坐标

            // 打印对比：两者应基本一致（误差 < 0.1，否则转换逻辑错了）
            Debug.Log($"按钮：{button.name} | 真实中心：{realButtonCenter} | 转换后：{convertedCenter}");
            //float hitRadius = Mathf.Max(buttonBounds.width, buttonBounds.height) / 0.8f;
            float hitRadius = buttonBounds.size.magnitude / 2f * 0.8f;
            // 计算注视点到按钮中心的距离
            float distance = Vector2.Distance(fixWorldPos, buttonWorldPos);

            Debug.Log($"按钮：{button.name} | 按钮世界中心：{buttonWorldPos} | 注视点：{fixWorldPos}");
            Debug.Log($"距离：{distance:F2} | 命中半径：{hitRadius:F2} | 距离是否达标：{distance <= hitRadius}");
            // 找到距离最近且命中的按钮
            if (distance <= hitRadius && distance < minDistance)
            {
                minDistance = distance;
                hitButton = button; //到这里了hitButton也是null
            }
        }
        //if (hitButton != _previousHitButton) //跟上面的重复了，但是怎么都没有用……
        //{
        //    _previousHitButton?.SendEvent(new PointerLeaveEvent()); 
        //    hitButton?.SendEvent(new PointerEnterEvent());  //卡在这了？
        //    _previousHitButton = hitButton;
        //}
        return hitButton;
    }

    //if(hitButton != null)
    //hitButton.SendEvent(ButtonHoverEvent? ButtonEnterEvent?)
    //if(hitButton = null)   //previousHitButton = hitButton,放在哪？
    //previousHitButton.SendEvent(ButtonLeaveEvent)


    protected virtual bool FixOnTarget => Vector2.Distance(fixdotposition, FixPosition) < (float)ex.ExtendParam["FixRadius"];

    //这是啥时候的代码了……算了先放着
    private void UpdateGazeTimer(float confidence)
    {
        // 低置信度或无注视目标时，重置计时
        if (confidence < RISE_THRESHOLD || currentGazedButton == null)
        {
            ResetButtonGazeTime(currentGazedButton);
            return;
        }

        // 累加注视时间
        buttonGazeTimes[currentGazedButton] += Time.deltaTime;

        // 达到阈值时触发点击
        if (buttonGazeTimes[currentGazedButton] >= fixationTriggerThreshold)
        {
            TriggerButtonClick(currentGazedButton);
            ResetButtonGazeTime(currentGazedButton); // 重置计时避免重复触发
        }
    }

    private void TriggerButtonClick(Button btn)
    {
        if (btn != null)
        {

            //btn.clicked?.Invoke();
            TriggerButtonClick(btn); //虽然有重复调用风险但实际上这个代码用没用上我不好说
            Debug.Log($"自动点击按钮：{btn.name}（注视时间：{fixationTriggerThreshold}秒）");
        }
    }

    private void ResetButtonGazeTime(Button btn)
    {
        if (btn != null && buttonGazeTimes.ContainsKey(btn))
        {
            buttonGazeTimes[btn] = 0;
        }
    }

    private void ResetGazeState()
    {
        currentGazeState = GazeState.Disabled;
        currentGazedButton = null;
        foreach (var btn in buttonGazeTimes.Keys.ToList())
        {
            buttonGazeTimes[btn] = 0;
        }
    }

    //检测到眨右眼，退出输入模式
    private void DetectBlinkRight()
    {
        if (EyeTracker != null && EyeTracker.Confidence < RISE_THRESHOLD)
        {
            ResetGazeState();
            Debug.Log("检测到眨右眼，退出输入模式");
        }
    }


    public enum TASKSTATE
    {
        NONE = 401,
        FIX_TARGET_ON,
        FIX_ACQUIRED
    }
    public TASKSTATE TaskState { get; private set; }

    protected virtual EnterStateCode EnterTaskState(TASKSTATE value, bool sync = false)
    {
        if (value == TaskState) { return EnterStateCode.AlreadyIn; }
        switch (value)
        {
            case TASKSTATE.NONE:
                recordgaze = false;
                break;
            case TASKSTATE.FIX_TARGET_ON:
                SetEnvActiveParam("FixDotVisible", true);
                WaitForFixTimeOut = GetExParam<double>("WaitForFixTimeOut");
                FixTargetOnTime = TimeMS;
                if (ex.HasCondTestState())
                {
                    condtestmgr.AddInList(nameof(CONDTESTPARAM.Event), value.ToString(), FixTargetOnTime);
                }
                recordgaze = true;
                break;
            case TASKSTATE.FIX_ACQUIRED:
                FixDur = RandFixDur;
                FixOnTime = TimeMS;
                if (ex.HasCondTestState())
                {
                    condtestmgr.AddInList(nameof(CONDTESTPARAM.Event), value.ToString(), FixOnTime);
                }
                break;
        }
        TaskState = value;
        if (sync) { SyncEvent(value.ToString()); }
        return EnterStateCode.Success;
    }

    protected bool IsTargetOn { get; set; }

    protected virtual void TurnOnTarget()
    {
        IsTargetOn = true;
    }

    protected virtual void TurnOffTarget()
    {
        IsTargetOn = false;
    }

    protected virtual void OnTimeOut()
    {
        if (ex.HasCondTestState())
        {
            condtestmgr.Add(nameof(CONDTESTPARAM.TaskResult), nameof(TASKRESULT.TIMEOUT));
        }
        // condition not tested, we repeat current condition by ignore condition sampling once
        condmgr.NSampleSkip = 1;
        Debug.LogWarning("TimeOut");
    }

    protected virtual void OnEarly()
    {
        if (ex.HasCondTestState())
        {
            condtestmgr.Add(nameof(CONDTESTPARAM.TaskResult), nameof(TASKRESULT.EARLY));
            condtestmgr.Add("FixHold", FixHold);
        }
        // condition may not completely tested in EARLY trial, so we repeat current condition by ignore condition sampling once
        condmgr.NSampleSkip = 1;
        ex.SufITI = RandSufITIDur;
        Debug.LogError("Early");
    }

    protected virtual void OnMiss()
    {
        if (ex.HasCondTestState())
        {
            condtestmgr.Add(nameof(CONDTESTPARAM.TaskResult), nameof(TASKRESULT.MISS));
            condtestmgr.Add("FixHold", FixHold);
        }
        ex.SufITI = RandSufITIDur;
        Debug.LogError("Miss");
    }

    protected virtual void OnHit()
    {
        if (ex.HasCondTestState())
        {
            condtestmgr.Add(nameof(CONDTESTPARAM.TaskResult), nameof(TASKRESULT.HIT));
            condtestmgr.Add("FixHold", FixHold);
        }
        Debug.Log("Hit");
    }

    protected override void OnStartExperiment()
    {
        base.OnStartExperiment();
        SetEnvActiveParam("FixDotVisible", false);
    }

    protected override void OnExperimentStopped()
    {
        base.OnExperimentStopped();
        SetEnvActiveParam("FixDotVisible", false);
    }


    /*protected override void Logic()
    {
        switch (TrialState)
        {
            case TRIALSTATE.NONE:
                ex.PreITI = RandPreITIDur;
                EnterTrialState(TRIALSTATE.PREITI);
                break;
            case TRIALSTATE.PREITI:
                if (PreITIHold >= ex.PreITI)
                {
                    EnterTrialState(TRIALSTATE.TRIAL);
                    EnterTaskState(TASKSTATE.FIX_TARGET_ON);
                }
                break;
            case TRIALSTATE.TRIAL:
                switch (TaskState)
                {
                    case TASKSTATE.FIX_TARGET_ON:
                        if (FixOnTarget)
                        {
                            EnterTaskState(TASKSTATE.FIX_ACQUIRED);
                        }
                        else if (WaitForFix >= WaitForFixTimeOut)
                        {
                            // Failed to acquire fixation
                            OnTimeOut();
                            SetEnvActiveParam("FixDotVisible", false);
                            EnterTaskState(TASKSTATE.NONE);
                            EnterTrialState(TRIALSTATE.NONE);
                        }
                        break;
                    case TASKSTATE.FIX_ACQUIRED:
                        if (!FixOnTarget)
                        {
                            // Fixation breaks in required period
                            OnEarly();
                            SetEnvActiveParam("FixDotVisible", false);
                            EnterTaskState(TASKSTATE.NONE);
                            EnterTrialState(TRIALSTATE.SUFITI); // long SUFITI as punishment
                        }
                        else if (FixHold >= FixDur)
                        {
                            // Successfully hold fixation in required period
                            OnHit();
                            SetEnvActiveParam("FixDotVisible", false); //"FixDot函数"改变UI颜色
                            EnterTaskState(TASKSTATE.NONE);
                            EnterTrialState(TRIALSTATE.NONE);
                        }
                        break;
                }
                break;
            case TRIALSTATE.SUFITI:
                if (SufITIHold >= ex.SufITI)
                {
                    EnterTrialState(TRIALSTATE.NONE);
                }
                break;
        }

    }*/
}