﻿using Config;

namespace UI.MapEditorSelectObject;

public class ObjectButtonCell : UiCell<MapEditorSelectObject.ObjectButton, ExcelConfig.ActivityObject>
{
    public override void OnInit()
    {
        CellNode.L_Select.Instance.Visible = false;
    }

    public override void OnSetData(ExcelConfig.ActivityObject data)
    {
        CellNode.L_ObjectName.Instance.Text = data.Name;
        if (!string.IsNullOrEmpty(data.Icon))
        {
            CellNode.L_PreviewImage.Instance.Visible = true;
            CellNode.L_PreviewImage.Instance.Texture = ResourceManager.LoadTexture2D(data.Icon);
        }
        else
        {
            CellNode.L_PreviewImage.Instance.Visible = false;
        }
    }

    public override void OnSelect()
    {
        CellNode.L_Select.Instance.Visible = true;
    }

    public override void OnUnSelect()
    {
        CellNode.L_Select.Instance.Visible = false;
    }
}