using System;
using UnityEngine;

namespace Game.Presentation.Input
{
    public interface ILocalGameplayInput
    {
        Vector2 Move { get; }
        float Zoom { get; }
        float Rotate { get; }
        Vector2 PanDelta { get; }
        bool IsPanPressed { get; }
        Vector2 PointerPosition { get; }
        event Action PrimaryClicked;
    }
}
