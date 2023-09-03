using System;
using UnityEngine;

namespace FluffyResearchTree;

public class Edge<T1, T2> where T1 : Node where T2 : Node
{
    private T1 _in;

    private T2 _out;

    public Edge(T1 @in, T2 @out)
    {
        _in = @in;
        _out = @out;
        IsDummy = _out is DummyNode;
    }

    public T1 In
    {
        get => _in;
        set
        {
            _in = value;
            IsDummy = _out is DummyNode;
        }
    }

    public T2 Out
    {
        get => _out;
        set
        {
            _out = value;
            IsDummy = _out is DummyNode;
        }
    }

    public int Span => _out.X - _in.X;

    public float Length => Mathf.Abs(_in.Yf - _out.Yf) * (!IsDummy ? 1 : 10);

    public bool IsDummy { get; private set; }

    public int DrawOrder
    {
        get
        {
            if (Out.Highlighted)
            {
                return 3;
            }

            if (Out.Completed)
            {
                return 2;
            }

            return Out.Available ? 1 : 0;
        }
    }

    public void Draw(Rect visibleRect)
    {
        if (!In.IsVisible(visibleRect) && !Out.IsVisible(visibleRect))
        {
            return;
        }

        GUI.color = Out.EdgeColor;
        var right = In.Right;
        var left = Out.Left;
        if (Math.Abs(right.y - left.y) < Constants.Epsilon)
        {
            GUI.DrawTexture(new Rect(right.x, right.y - 2f, left.x - right.x, 4f), Assets.Lines.EW);
        }
        else
        {
            var num = Math.Min(right.y, left.y) + (Constants.NodeMargins.x / 4f);
            var num2 = Math.Max(right.y, left.y) - (Constants.NodeMargins.x / 4f);
            GUI.DrawTexture(new Rect(right.x, right.y - 2f, Constants.NodeMargins.x / 4f, 4f), Assets.Lines.EW);
            GUI.DrawTexture(new Rect(right.x + (Constants.NodeMargins.x / 2f) - 2f, num, 4f, num2 - num),
                Assets.Lines.NS);
            GUI.DrawTexture(
                new Rect(right.x + (Constants.NodeMargins.x / 4f * 3f), left.y - 2f,
                    left.x - right.x - (Constants.NodeMargins.x / 4f * 3f), 4f), Assets.Lines.EW);
            var position = new Rect(right.x + (Constants.NodeMargins.x / 4f), right.y - (Constants.NodeMargins.x / 4f),
                Constants.NodeMargins.x / 2f, Constants.NodeMargins.x / 2f);
            var position2 = new Rect(right.x + (Constants.NodeMargins.x / 4f), left.y - (Constants.NodeMargins.x / 4f),
                Constants.NodeMargins.x / 2f, Constants.NodeMargins.x / 2f);
            if (right.y < left.y)
            {
                GUI.DrawTextureWithTexCoords(position, Assets.Lines.Circle, new Rect(0.5f, 0.5f, 0.5f, 0.5f));
                GUI.DrawTextureWithTexCoords(position2, Assets.Lines.Circle, new Rect(0f, 0f, 0.5f, 0.5f));
            }
            else
            {
                GUI.DrawTextureWithTexCoords(position, Assets.Lines.Circle, new Rect(0.5f, 0f, 0.5f, 0.5f));
                GUI.DrawTextureWithTexCoords(position2, Assets.Lines.Circle, new Rect(0f, 0.5f, 0.5f, 0.5f));
            }
        }

        if (!IsDummy)
        {
            GUI.DrawTexture(new Rect(left.x - Constants.HubSize, left.y - 8f, Constants.HubSize, Constants.HubSize),
                Assets.Lines.End);
        }
        else
        {
            GUI.DrawTexture(new Rect(left.x, left.y - 2f, Constants.NodeSize.x, 4f), Assets.Lines.EW);
        }

        GUI.color = Color.white;
    }

    public override string ToString()
    {
        return $"{_in} -> {_out}";
    }
}