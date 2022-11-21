using System.Collections.Generic;
using Godot;

/// <summary>
/// 房间管理器
/// </summary>
public class RoomManager : Navigation2D
{
    /// <summary>
    /// 玩家对象
    /// </summary>
    public Role Player { get; private set; }

    //对象根节点
    private Node2D _objectRoot;

    //对象根节点, 带y轴排序功能
    private YSort _sortRoot;

    private Node2D _mapRoot;

    private NavigationPolygonInstance _navigationPolygon;
    private Enemy _enemy;

    private List<int> _wayIds = new List<int>(new[] { 129 });
    private List<Vector2> _points = new List<Vector2>();

    public override void _EnterTree()
    {
        Input.MouseMode = Input.MouseModeEnum.Hidden;

        _sortRoot = GetNode<YSort>("SortRoot");
        _objectRoot = GetNode<Node2D>("ObjectRoot");

        _navigationPolygon = GetNode<NavigationPolygonInstance>("NavigationPolygonInstance");

        //初始化地图
        _mapRoot = GetNode<Node2D>("MapRoot");
        var node = _mapRoot.GetChild(0).GetNode("Config");
        Color color = (Color)node.GetMeta("ClearColor");
        VisualServer.SetDefaultClearColor(color);

        //创建玩家
        Player = new Player();
        Player.Position = new Vector2(100, 100);
        Player.Name = "Player";
        Player.PutDown();

        _enemy = new Enemy();
        _enemy.Name = "Enemy";
        _enemy.PutDown(new Vector2(150, 150));
    }

    public override void _Ready()
    {
        GenerateNavigationPolygon();

        //播放bgm
        SoundManager.PlayMusic(ResourcePath.resource_sound_bgm_Intro_ogg, -17f);
        _enemy.PickUpWeapon(WeaponManager.GetGun("1001"));

        WeaponManager.GetGun("1001").PutDown(new Vector2(80, 100));
        WeaponManager.GetGun("1001").PutDown(new Vector2(80, 80));
        WeaponManager.GetGun("1002").PutDown(new Vector2(80, 120));
        WeaponManager.GetGun("1003").PutDown(new Vector2(120, 80));

        WeaponManager.GetGun("1003").PutDown(new Vector2(180, 80));
        WeaponManager.GetGun("1003").PutDown(new Vector2(180, 180));
        WeaponManager.GetGun("1002").PutDown(new Vector2(180, 120));

        WeaponManager.GetGun("1004").PutDown(new Vector2(220, 120));
    }

    public override void _Process(float delta)
    {
        if (GameApplication.Instance.Debug)
        {
            Update();
        }
    }

    public override void _Draw()
    {
        if (GameApplication.Instance.Debug)
        {
            if (_points != null && _points.Count >= 2)
            {
                DrawPolyline(_points.ToArray(), Colors.Red);
            }
        }
    }

    /// <summary>
    /// 获取房间根节点
    /// </summary>
    /// <param name="useYSort">是否获取 YSort 节点</param>
    /// <returns></returns>
    public Node2D GetRoot(bool useYSort = false)
    {
        return useYSort ? _sortRoot : _objectRoot;
    }

    /// <summary>
    /// 自动生成导航区域
    /// </summary>
    private void GenerateNavigationPolygon()
    {
        //129
        var tileMap = _mapRoot.GetChild(0).GetNode<TileMap>("Wall");
        var size = tileMap.CellSize;

        var rect = tileMap.GetUsedRect();

        var x = (int)rect.Position.x;
        var y = (int)rect.Position.y;
        var w = (int)rect.Size.x;
        var h = (int)rect.Size.y;

        for (int i = x; i < w; i++)
        {
            for (int j = y; j < h; j++)
            {
                var tileId = tileMap.GetCell(i, j);
                if (tileId != -1 && _wayIds.Contains(tileId))
                {
                    //---------------------------------------

                    // 0:右, 1:下, 2:左, 3:上
                    var dir = 0;
                    _points.Clear();
                    //找到路, 向右开始找边界
                    _points.Add(new Vector2(i * size.x + size.x * 0.5f, j * size.y + size.y * 0.5f));

                    var tempI = i;
                    var tempJ = j;

                    var prevI = i;
                    var prevJ = j;

                    var len = 1;
                    
                    while (true)
                    {
                        switch (dir)
                        {
                            case 0:
                                tempI++;
                                break;
                            case 1:
                                tempJ++;
                                break;
                            case 2:
                                tempI--;
                                break;
                            case 3:
                                tempJ--;
                                break;
                        }

                        bool flag = true;
                        int nextCellId = -1;

                        switch (dir)
                        {
                            case 0: //右
                            {
                                //向右找
                                nextCellId = tileMap.GetCell(tempI, tempJ);
                                flag = isWayCell(nextCellId);
                                if (!flag)
                                {
                                    if (isWayCell(tileMap.GetCell(tempI, tempJ - 1))) //向上找
                                    {
                                        dir = 3 - 1;
                                    }
                                }
                            }
                                break;
                            case 1: //下
                            {
                                nextCellId = tileMap.GetCell(tempI, tempJ);
                                flag = isWayCell(nextCellId);
                            }
                                break;
                            case 2: //左
                            {
                                // nextCellId = tileMap.GetCell(tempI, tempJ + 1);//向下找
                                // flag = isWayCell(nextCellId);
                                // if (flag)
                                // {
                                //     dir = 1 - 1;
                                // }
                                // else
                                // {
                                    nextCellId = tileMap.GetCell(tempI, tempJ); //向左找
                                    flag = isWayCell(nextCellId); 
                                //}
                            }
                                break;
                            case 3: //上
                            {
                                nextCellId = tileMap.GetCell(tempI, tempJ);
                                flag = isWayCell(nextCellId);
                            }
                                break;
                        }
                        
                        if (!flag) //下一个不是可行走区域
                        {

                            if (len <= 1)
                            {
                                //有问题, 不支持单格道路!
                                GD.PrintErr("不支持单格道路: " + tempI + ", " + tempJ);
                            }
                            
                            tempI = prevI;
                            tempJ = prevJ;
                            //转向
                            dir++;
                            //记录当前点
                            _points.Add(new Vector2(tempI * size.x + size.x * 0.5f, tempJ * size.y + size.y * 0.5f));
                            if (dir == 4)
                            {
                                goto a;
                            }
                        }
                        else
                        {
                            len++;
                        }

                        prevI = tempI;
                        prevJ = tempJ;
                    }
                    //---------------------------------------
                }
            }
        }

        a: ;

    }

    private bool isWayCell(int cellId)
    {
        return cellId != -1 && _wayIds.Contains(cellId);
    }
}