using Experica;
using Experica.Command;
using Experica.NetEnv;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class FixationSelect:Fixation
{
    
    public override void OnPlayerReady()
    {
        base.OnPlayerReady(); //获取Fixation.cs中注视点数据
                              //生成所有人可见的选择操作
        foreach (var sg in scalegrid) { sg.NetworkObject.NetworkShowOnlyTo(sg.ClientID); } //定位网格仅本机可见
    }
    protected override void PrepareCondition()
    {
        var pos = GetExParam<List<Vector3>>("ObjectPosition");
        if (pos == null || pos.Count == 0)
        {
            pos = new List<Vector3>() { Vector3.right * 10 };
        }
    }
    //核心状态机
    //时间轴
    

    //注视点出现，用户注视
    protected override void TurnOnTarget()
    {
        SetEnvActiveParam("Visible", true);
        base.TurnOnTarget();
    }

    protected override void TurnOffTarget()
    {
        SetEnvActiveParam("Visible", false);
        base.TurnOffTarget();
    }

    //free moving
    //眼动位置方差 1s-1.5s内小于某个阈值
    //眨眼：confidence判定--鼠标点击   一直不睁开眼睛就是一直hold
    //itracker
}
