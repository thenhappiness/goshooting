using Godot;

/// <summary>
/// 游戏主管理器, 自动加载
/// </summary>
public partial class GameManager : Node2D
{
    public static GameManager Instance { get; private set; }

    public GameManager()
    {
        Instance = this;

        //扫描并注册当前程序集下的武器
        WeaponManager.RegisterWeaponFromAssembly(GetType().Assembly);
    }

    public override void _Process(double delta)
    {
        InputManager.Update((float)delta);
    }
}