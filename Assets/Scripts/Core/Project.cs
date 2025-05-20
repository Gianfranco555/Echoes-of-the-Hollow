using System;
using UnityEngine;

[Serializable]
public class Project
{
    public string title;
    [Range(0f,1f)]
    public float progress;
    public float workRate;
    public float deadlineSeconds;
    public float reward;
    public bool isComplete;
    public bool isExpired;
}
