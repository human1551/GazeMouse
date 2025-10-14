using Experica;
using Experica.Command;
using Experica.NetEnv;
using IceInternal;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Control1: ExperimentLogic
{
    public double FixOnTime, FixTargetOnTime, FixDur, WaitForFixTimeOut;
    public double FixHold => TimeMS - FixOnTime;
    public double WaitForFix => TimeMS - FixTargetOnTime;
    public enum STATUS
    {
        None = 401,
        On,
        Hold,
        Click,
        DoubleClick,
    }
   

    [Serializable]
    public struct EyeEvent
    {
        public STATUS Status;
        public Vector2 Position;
        public float Time;
    }
    protected override void Enable()
    {
        MoveAction = InputSystem.actions.FindActionMap("Logic").FindAction("Move");

        EyeTracker = Experica.IEyeTracker.PupilLabsCore.TryGetPupilLabsCore();
    }

    protected override void Disable()
    {
        if (EyeTracker != null)
        {
            EyeTracker.Dispose();
            EyeTracker = null;
        }
    }

    public InputAction MoveAction;
    public IEyeTracker EyeTracker;
    public Vector2 FixPosition;
    protected NetworkVariable<Vector3> fixdotposition;
    protected List<ScaleGrid> scalegrid = new();
    protected bool updatefixtrail, recordgaze;
    Tag tag;
    DotTrail fixtrail;
    Circle fixcircle;


    public override void OnPlayerReady()
    {
            //Action<NetEnvVisual, Vector3> upxy = (o, p) => o.Position.Value = new(p.x, p.y, o.Position.Value.z);

        var fixradius = (float)ex.ExtendParam["FixRadius"];
            // here also spawn as a child of FixDot, so the circle would center FixDot
        fixcircle = envmgr.SpawnCircle(color: new(0.1f, 0.8f, 0.1f), size: new(2 * fixradius, 2 * fixradius, 1), parse: false, parent: fixdotposition.GetBehaviour().transform);
            // hook a ExtendParam to a NetworkVariable
        ex.extendproperties["FixRadius"].propertyChanged += (o, e) => fixcircle.Size.Value = new(2 * (float)ex.ExtendParam["FixRadius"], 2 * (float)ex.ExtendParam["FixRadius"], 1);
            // tracing fixation position
        fixtrail = envmgr.SpawnDotTrail(position: Vector3.back, size: new(0.25f, 0.25f, 1), color: new(1, 0.1f, 0.1f), parse: false);
    }

    protected override void OnUpdate() //获取真实/模拟眼动数据
    {
        if (ex.Input && envmgr.MainCamera.Count > 0 && MoveAction.phase == InputActionPhase.Started) //获取鼠标移动数据,或真实眼动数据
        {
            FixPosition += MoveAction.ReadValue<Vector2>();
            clampMove(ref FixPosition);
            updatefixtrail = true;
        }

        if (EyeTracker != null && envmgr.MainCamera.Count > 0)
        {
            FixPosition = surfacegaze2cameragaze(EyeTracker.Gaze2D, envmgr.MainCamera.First(), tag.TagSurfaceMargin); 
            if (recordgaze && ex.HasCondTestState()) //记录眼动数据
            {
                condtestmgr.AddInList(nameof(CONDTESTPARAM.Gaze), TimeMS, FixPosition);
            }
            updatefixtrail = true;
        }

        if (updatefixtrail && fixtrail != null && fixtrail.Visible.Value) //更新轨迹
        {
            fixtrail.Position.Value = FixPosition;
            updatefixtrail = false;
        }
    }
    Vector2 surfacegaze2cameragaze(Vector2 sg, INetEnvCamera camera, float surfacemargin) //坐标系转换函数
    {
        sg.x = sg.x - 0.5f;
        sg.y = sg.y - 0.5f;
        return new Vector2(sg.x * (camera.Width - 2 * surfacemargin), sg.y * (camera.Height - 2 * surfacemargin));
    }
    void clampMove(ref Vector2 pos)
    {
        var r = GetExParam<float>("MoveRadius");
        if (r == 0) { r = 50; }
        pos.x = Mathf.Clamp(pos.x, -r, r);
        pos.y = Mathf.Clamp(pos.y, -r, r);
    }

    public STATUS EyeStatus { get; private set; }
    protected virtual EnterStateCode Enter(STATUS value, bool sync = false)  //状态机
    {
        if (EyeStatus == value) return EnterStateCode.AlreadyIn;//如果当前已经是目标状态，直接返回 AlreadyIn，避免重复执行副作用。

        switch (value)
        {
            case STATUS.None:
                recordgaze = false;
                break;
            case STATUS.On:
                SetEnvActiveParam("FixDotVisible", true);
                WaitForFixTimeOut = GetExParam<double>("WaitForFixTimeOut");
                FixTargetOnTime = TimeMS;
                recordgaze = true;
                break;
            case STATUS.Hold:
                // 若闭眼后检测不到睁眼，则持续进入 Hold 状态，UI颜色改变
                SetEnvActiveParam("ColorChange", true);

                //若睁眼则退出Hold状态，恢复On状态
                break;
            case STATUS.Click:
                // 闭眼后在一定时间范围内检测到睁眼，进入 Click 状态
                //每眨一次眼睛判断视线点到哪个UI
                Debug.Log("Entered Click state");
                break;
            case STATUS.DoubleClick:
                // 在一定时间内检测到两次间断的瞳孔数据出现，进入 DoubleClick 状态
                Debug.Log("Entered DoubleClick state");
                break;
            default:
                Debug.LogWarning("Unknown state");
                break;
        }
        EyeStatus = value;
        if (sync) { SyncEvent(value.ToString()); }
        return EnterStateCode.Success;

        //EyeStatus = value;
        //if (sync)
        // {
        //var e = new EyeEvent
        //{
        //Status = EyeStatus,
        //Position = FixPosition,
        //Time = TimeMS
        // };
        //condtestmgr?.AddInList(nameof(CONDTESTPARAM.EyeEvent), TimeMS, e);
        // }
        // return EnterStateCode.Success;
    }

}

