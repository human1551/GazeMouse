using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using UnityEngine;

public class WindowsInputSimulator
{
    // 导入 Windows API
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

    // 鼠标事件常量
    private const uint MOUSEEVENTF_LEFTDOWN = 0x02; // 左键按下
    private const uint MOUSEEVENTF_LEFTUP = 0x04;   // 左键抬起

    // 模拟鼠标左键点击
    public static void SimulateLeftClick()
    {
        // 按下左键
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        // 延迟
        System.Threading.Thread.Sleep(50);
        // 抬起左键
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }
    public static void SimulateRightClick()
    {
        // 按下右键
        mouse_event(0x08, 0, 0, 0, 0);
        // 延迟
        System.Threading.Thread.Sleep(50);
        // 抬起右键
        mouse_event(0x10, 0, 0, 0, 0);
    }
}




