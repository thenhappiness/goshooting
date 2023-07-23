﻿using Godot;

namespace UI.MapEditorProject;

public class GroupButtonCell : UiCell<MapEditorProject.GroupButton, DungeonRoomGroup>
{
    public override void OnInit()
    {
        CellNode.Instance.Pressed += OnClick;
    }

    public override void OnSetData(DungeonRoomGroup info)
    {
        CellNode.Instance.Text = info.GroupName;
        CellNode.Instance.TooltipText = "路径: " + MapProjectManager.CustomMapPath + "/" + info.GroupName;
    }

    public override void OnRefreshIndex()
    {
        GD.Print("刷新索引: " + Index);
    }

    //选中工程
    private void OnClick()
    {
        Grid.SelectIndex = Index;
        ((MapEditorProjectPanel)CellNode.UiPanel).SelectGroup(Data);
    }
}