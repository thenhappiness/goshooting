
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 地牢地砖管理类, 提供一些操作 TileMap 和计算导航的接口
/// </summary>
public class DungeonTile
{
    /// <summary>
    /// TileMap 底板的层级
    /// </summary>
    public const int FloorMapLayer = 0;
    /// <summary>
    /// TileMap 中层的层级
    /// </summary>
    public const int MiddleMapLayer = 1;
    /// <summary>
    /// TileMap 上层的层级
    /// </summary>
    public const int TopMapLayer = 2;

    /// <summary>
    /// 配置层级的自定义数据名称
    /// </summary>
    public const string CustomTileLayerName = "TileLayer";
    
    //--------------------- 导航 -------------------------
    
    //已经标记过的点
    private readonly HashSet<Vector2> _usePoints = new HashSet<Vector2>();

    //导航区域数据
    private readonly List<NavigationPolygonData> _polygonDataList = new List<NavigationPolygonData>();
    
    //----------------------------------------------------

    private TileMap _tileRoot;
    
    public DungeonTile(TileMap tileRoot)
    {
        _tileRoot = tileRoot;
    }
    
    /// <summary>
    /// 根据 roomInfo 和 config 数据自动填充 tileMap 参数中的地图数据
    /// </summary>
    public void AutoFillRoomTile(AutoTileConfig config, RoomInfo roomInfo)
    {
        foreach (var info in roomInfo.Next)
        {
            AutoFillRoomTile(config, info);
        }
        
        //铺房间
        if (roomInfo.RoomSplit == null)
        {
            FillRect(FloorMapLayer, config.Ground, roomInfo.Position + Vector2.One,
                roomInfo.Size - new Vector2(2, 2));

            FillRect(TopMapLayer, config.IN_LT, roomInfo.Position, Vector2.One);
            FillRect(TopMapLayer, config.L, roomInfo.Position + new Vector2(0, 1),
                new Vector2(1, roomInfo.Size.Y - 2));
            FillRect(TopMapLayer, config.IN_LB, roomInfo.Position + new Vector2(0, roomInfo.Size.Y - 1),
                new Vector2(1, 1));
            FillRect(TopMapLayer, config.B, roomInfo.Position + new Vector2(1, roomInfo.Size.Y - 1),
                new Vector2(roomInfo.Size.X - 2, 1));
            FillRect(TopMapLayer, config.IN_RB,
                roomInfo.Position + new Vector2(roomInfo.Size.X - 1, roomInfo.Size.Y - 1),
                Vector2.One);
            FillRect(TopMapLayer, config.R, roomInfo.Position + new Vector2(roomInfo.Size.X - 1, 1),
                new Vector2(1, roomInfo.Size.Y - 2));
            FillRect(TopMapLayer, config.IN_RT, roomInfo.Position + new Vector2(roomInfo.Size.X - 1, 0),
                Vector2.One);
            FillRect(MiddleMapLayer, config.T, roomInfo.Position + Vector2.Right,
                new Vector2(roomInfo.Size.X - 2, 1));
        }
        else
        {
            var rectSize = roomInfo.RoomSplit.RoomInfo.Size;
            var rectPos = roomInfo.RoomSplit.RoomInfo.Position;
            var template = ResourceManager.Load<PackedScene>(roomInfo.RoomSplit.ScenePath);
            var tileInstance = template.Instantiate<DungeonRoomTemplate>();
            for (int i = 0; i < rectSize.X; i++)
            {
                for (int j = 0; j < rectSize.Y; j++)
                {
                    var coords = new Vector2I((int)(rectPos.X + i), (int)(rectPos.Y + j));
                    var atlasCoords = tileInstance.GetCellAtlasCoords(0, coords);
                    if (atlasCoords.X != -1 && atlasCoords.Y != -1)
                    {
                        //获取自定义层级
                        var layer = tileInstance.GetCellTileData(0, coords).GetCustomData(CustomTileLayerName)
                            .AsInt32();
                        layer = Mathf.Clamp(layer, FloorMapLayer, TopMapLayer);
                        _tileRoot.SetCell(layer, new Vector2I(roomInfo.Position.X + i, roomInfo.Position.Y + j),
                            1, atlasCoords);
                    }
                }
            }

            tileInstance.QueueFree();
        }

        //铺过道
        foreach (var doorInfo in roomInfo.Doors)
        {
            if (doorInfo.ConnectRoom.Id > roomInfo.Id)
            {
                //普通的直线连接
                var doorDir1 = doorInfo.Direction;
                var doorDir2 = doorInfo.ConnectDoor.Direction;
                if (!doorInfo.HasCross)
                {
                    //方向, 0横向, 1纵向
                    int dir = 0;
                    var rect = Utils.CalcRect(
                        doorInfo.OriginPosition.X,
                        doorInfo.OriginPosition.Y,
                        doorInfo.ConnectDoor.OriginPosition.X,
                        doorInfo.ConnectDoor.OriginPosition.Y
                    );
                    if (doorDir1 == DoorDirection.N || doorDir1 == DoorDirection.S)
                    {
                        rect.Size = new Vector2(GenerateDungeon.CorridorWidth, rect.Size.Y);
                        dir = 1;
                    }
                    else
                    {
                        rect.Size = new Vector2(rect.Size.X, GenerateDungeon.CorridorWidth);
                    }

                    if (dir == 0) //横向
                    {
                        FullHorizontalGalleryWall(config, rect);
                    }
                    else //纵向
                    {
                        FullVerticalGalleryWall(config, rect);
                    }
                }
                else //带交叉点
                {
                    //方向, 0横向, 1纵向
                    int dir1 = 0;
                    int dir2 = 0;

                    Rect2 rect;
                    Rect2 rect2;

                    //计算范围
                    switch (doorDir1)
                    {
                        case DoorDirection.E: //→
                            rect = new Rect2(
                                doorInfo.OriginPosition.X,
                                doorInfo.OriginPosition.Y,
                                doorInfo.Cross.X - doorInfo.OriginPosition.X,
                                GenerateDungeon.CorridorWidth
                            );
                            break;
                        case DoorDirection.W: //←
                            rect = new Rect2(
                                doorInfo.Cross.X + GenerateDungeon.CorridorWidth,
                                doorInfo.Cross.Y,
                                doorInfo.OriginPosition.X - (doorInfo.Cross.X + GenerateDungeon.CorridorWidth),
                                GenerateDungeon.CorridorWidth
                            );
                            break;
                        case DoorDirection.S: //↓
                            dir1 = 1;
                            rect = new Rect2(
                                doorInfo.OriginPosition.X,
                                doorInfo.OriginPosition.Y,
                                GenerateDungeon.CorridorWidth,
                                doorInfo.Cross.Y - doorInfo.OriginPosition.Y
                            );
                            break;
                        case DoorDirection.N: //↑
                            dir1 = 1;
                            rect = new Rect2(
                                doorInfo.Cross.X,
                                doorInfo.Cross.Y + GenerateDungeon.CorridorWidth,
                                GenerateDungeon.CorridorWidth,
                                doorInfo.OriginPosition.Y - (doorInfo.Cross.Y + GenerateDungeon.CorridorWidth)
                            );
                            break;
                        default:
                            rect = new Rect2();
                            break;
                    }

                    switch (doorDir2)
                    {
                        case DoorDirection.E: //→
                            rect2 = new Rect2(
                                doorInfo.ConnectDoor.OriginPosition.X,
                                doorInfo.ConnectDoor.OriginPosition.Y,
                                doorInfo.Cross.X - doorInfo.ConnectDoor.OriginPosition.X,
                                GenerateDungeon.CorridorWidth
                            );
                            break;
                        case DoorDirection.W: //←
                            rect2 = new Rect2(
                                doorInfo.Cross.X + GenerateDungeon.CorridorWidth,
                                doorInfo.Cross.Y,
                                doorInfo.ConnectDoor.OriginPosition.X -
                                (doorInfo.Cross.X + GenerateDungeon.CorridorWidth),
                                GenerateDungeon.CorridorWidth
                            );
                            break;
                        case DoorDirection.S: //↓
                            dir2 = 1;
                            rect2 = new Rect2(
                                doorInfo.ConnectDoor.OriginPosition.X,
                                doorInfo.ConnectDoor.OriginPosition.Y,
                                GenerateDungeon.CorridorWidth,
                                doorInfo.Cross.Y - doorInfo.ConnectDoor.OriginPosition.Y
                            );
                            break;
                        case DoorDirection.N: //↑
                            dir2 = 1;
                            rect2 = new Rect2(
                                doorInfo.Cross.X,
                                doorInfo.Cross.Y + GenerateDungeon.CorridorWidth,
                                GenerateDungeon.CorridorWidth,
                                doorInfo.ConnectDoor.OriginPosition.Y -
                                (doorInfo.Cross.Y + GenerateDungeon.CorridorWidth)
                            );
                            break;
                        default:
                            rect2 = new Rect2();
                            break;
                    }

                    FillRect(FloorMapLayer, config.Ground, doorInfo.Cross + Vector2.One,
                        new Vector2(GenerateDungeon.CorridorWidth - 2, GenerateDungeon.CorridorWidth - 2));

                    //墙壁
                    if (dir1 == 0)
                    {
                        FullHorizontalGalleryWall(config, rect);
                    }
                    else
                    {
                        FullVerticalGalleryWall(config, rect);
                    }

                    if (dir2 == 0)
                    {
                        FullHorizontalGalleryWall(config, rect2);
                    }
                    else
                    {
                        FullVerticalGalleryWall(config, rect2);
                    }

                    if ((doorDir1 == DoorDirection.N && doorDir2 == DoorDirection.E) || //↑→
                        (doorDir2 == DoorDirection.N && doorDir1 == DoorDirection.E))
                    {
                        FillRect(TopMapLayer, config.OUT_RT,
                            doorInfo.Cross + new Vector2(0, GenerateDungeon.CorridorWidth - 1),
                            Vector2.One);
                        FillRect(TopMapLayer, config.IN_RT, doorInfo.Cross + new Vector2(GenerateDungeon.CorridorWidth - 1, 0),
                            Vector2.One);
                        FillRect(MiddleMapLayer, config.T, doorInfo.Cross, new Vector2(GenerateDungeon.CorridorWidth - 1, 1));
                        FillRect(TopMapLayer, config.R, doorInfo.Cross + new Vector2(GenerateDungeon.CorridorWidth - 1, 1),
                            new Vector2(1, GenerateDungeon.CorridorWidth - 1));
                    }
                    else if ((doorDir1 == DoorDirection.E && doorDir2 == DoorDirection.S) || //→↓
                             (doorDir2 == DoorDirection.E && doorDir1 == DoorDirection.S))
                    {
                        FillRect(MiddleMapLayer, config.OUT_RB, doorInfo.Cross, Vector2.One);
                        FillRect(TopMapLayer, config.IN_RB,
                            doorInfo.Cross + new Vector2(GenerateDungeon.CorridorWidth - 1,
                                GenerateDungeon.CorridorWidth - 1),
                            Vector2.One);
                        FillRect(TopMapLayer, config.R, doorInfo.Cross + new Vector2(GenerateDungeon.CorridorWidth - 1, 0),
                            new Vector2(1, GenerateDungeon.CorridorWidth - 1));
                        FillRect(TopMapLayer, config.B, doorInfo.Cross + new Vector2(0, GenerateDungeon.CorridorWidth - 1),
                            new Vector2(GenerateDungeon.CorridorWidth - 1, 1));
                    }
                    else if ((doorDir1 == DoorDirection.S && doorDir2 == DoorDirection.W) || //↓←
                             (doorDir2 == DoorDirection.S && doorDir1 == DoorDirection.W))
                    {
                        FillRect(MiddleMapLayer, config.OUT_LB,
                            doorInfo.Cross + new Vector2(GenerateDungeon.CorridorWidth - 1, 0), Vector2.One);
                        FillRect(TopMapLayer, config.IN_LB, doorInfo.Cross + new Vector2(0, GenerateDungeon.CorridorWidth - 1),
                            Vector2.One);
                        FillRect(TopMapLayer, config.L, doorInfo.Cross, new Vector2(1, GenerateDungeon.CorridorWidth - 1));
                        FillRect(TopMapLayer, config.B, doorInfo.Cross + new Vector2(1, GenerateDungeon.CorridorWidth - 1),
                            new Vector2(GenerateDungeon.CorridorWidth - 1, 1));
                    }
                    else if ((doorDir1 == DoorDirection.W && doorDir2 == DoorDirection.N) || //←↑
                             (doorDir2 == DoorDirection.W && doorDir1 == DoorDirection.N))
                    {
                        FillRect(TopMapLayer, config.OUT_LT,
                            doorInfo.Cross + new Vector2(GenerateDungeon.CorridorWidth - 1,
                                GenerateDungeon.CorridorWidth - 1),
                            Vector2.One);
                        FillRect(TopMapLayer, config.IN_LT, doorInfo.Cross, Vector2.One);
                        FillRect(MiddleMapLayer, config.T, doorInfo.Cross + new Vector2(1, 0),
                            new Vector2(GenerateDungeon.CorridorWidth - 1, 1));
                        FillRect(TopMapLayer, config.L, doorInfo.Cross + new Vector2(0, 1),
                            new Vector2(1, GenerateDungeon.CorridorWidth - 1));
                    }

                    //在房间墙上开洞
                    switch (doorDir1)
                    {
                        case DoorDirection.E: //→
                            ClearRect(TopMapLayer, doorInfo.OriginPosition + new Vector2(-1, 1),
                                new Vector2(1, rect.Size.Y - 2));
                            break;
                        case DoorDirection.W: //←
                            ClearRect(TopMapLayer, doorInfo.OriginPosition + new Vector2(0, 1),
                                new Vector2(1, rect.Size.Y - 2));
                            break;
                        case DoorDirection.S: //↓
                            ClearRect(TopMapLayer, doorInfo.OriginPosition + new Vector2(1, -1),
                                new Vector2(rect.Size.X - 2, 1));
                            break;
                        case DoorDirection.N: //↑
                            ClearRect(MiddleMapLayer, doorInfo.OriginPosition + new Vector2(1, 2),
                                new Vector2(rect.Size.X - 2, 1));
                            break;
                    }

                    switch (doorDir2)
                    {
                        case DoorDirection.E: //→
                            ClearRect(TopMapLayer, doorInfo.ConnectDoor.OriginPosition + new Vector2(-1, 1),
                                new Vector2(1, rect2.Size.Y - 2));
                            break;
                        case DoorDirection.W: //←
                            ClearRect(TopMapLayer, doorInfo.ConnectDoor.OriginPosition + new Vector2(0, 1),
                                new Vector2(1, rect2.Size.Y - 2));
                            break;
                        case DoorDirection.S: //↓
                            ClearRect(TopMapLayer, doorInfo.ConnectDoor.OriginPosition + new Vector2(1, -1),
                                new Vector2(rect2.Size.X - 2, 1));
                            break;
                        case DoorDirection.N: //↑
                            ClearRect(MiddleMapLayer, doorInfo.ConnectDoor.OriginPosition + new Vector2(1, 0),
                                new Vector2(rect2.Size.X - 2, 1));
                            break;
                    }
                }
            }
        }
    }

    //填充tile区域
    private void FillRect(int layer, TileCellInfo info, Vector2 pos, Vector2 size)
    {
        for (int i = 0; i < size.X; i++)
        {
            for (int j = 0; j < size.Y; j++)
            {
                _tileRoot.SetCell(layer, new Vector2I((int)pos.X + i, (int)pos.Y + j), 1, info.AutotileCoord);
            }
        }
    }

    //清除tile区域
    private void ClearRect(int layer, Vector2 pos, Vector2 size)
    {
        for (int i = 0; i < size.X; i++)
        {
            for (int j = 0; j < size.Y; j++)
            {
                //tileMap.SetCell((int)pos.X + i, (int)pos.Y + j, -1);
                _tileRoot.SetCell(layer, new Vector2I((int)pos.X + i, (int)pos.Y + j), -1);
            }
        }
    }

    private void FullHorizontalGalleryWall(AutoTileConfig config, Rect2 rect)
    {
        FillRect(FloorMapLayer, config.Ground, rect.Position + new Vector2(0, 1), rect.Size - new Vector2(0, 2));
        FillRect(MiddleMapLayer, config.T, rect.Position, new Vector2(rect.Size.X, 1));
        FillRect(TopMapLayer, config.B, rect.Position + new Vector2(0, rect.Size.Y - 1), new Vector2(rect.Size.X, 1));
        //左
        ClearRect(TopMapLayer, rect.Position + new Vector2(-1, 1), new Vector2(1, rect.Size.Y - 2));
        //FillRect(FloorMapLayer, config.Ground, rect.Position + new Vector2(-1, 1), new Vector2(1, rect.Size.Y - 2));
        //右
        ClearRect(TopMapLayer, rect.Position + new Vector2(rect.Size.X, 1), new Vector2(1, rect.Size.Y - 2));
        //FillRect(FloorMapLayer, config.Ground, rect.Position + new Vector2(rect.Size.X, 1), new Vector2(1, rect.Size.Y - 2));
    }

    private void FullVerticalGalleryWall(AutoTileConfig config, Rect2 rect)
    {
        FillRect(FloorMapLayer, config.Ground, rect.Position + new Vector2(1, 0), rect.Size - new Vector2(2, 0));
        FillRect(TopMapLayer, config.L, rect.Position, new Vector2(1, rect.Size.Y));
        FillRect(TopMapLayer, config.R, rect.Position + new Vector2(rect.Size.X - 1, 0), new Vector2(1, rect.Size.Y));
        //上
        ClearRect(TopMapLayer, rect.Position + new Vector2(1, -1), new Vector2(rect.Size.X - 2, 1));
        //FillRect(FloorMapLayer, config.Ground, rect.Position + new Vector2(1, -1), new Vector2(rect.Size.X - 2, 1));
        //下
        ClearRect(MiddleMapLayer, rect.Position + new Vector2(1, rect.Size.Y), new Vector2(rect.Size.X - 2, 1));
        //FillRect(FloorMapLayer, config.Ground, rect.Position + new Vector2(1, rect.Size.Y), new Vector2(rect.Size.X - 2, 1));
    }
    
    private void FullTestData()
    {
        _polygonDataList.Clear();
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(328, -296), new Vector2(360, -296), new Vector2(360, -184), new Vector2(440, -184), new Vector2(440, -296), new Vector2(584, -296), new Vector2(584, -152), new Vector2(552, -152), new Vector2(552, -280), new Vector2(472, -280), new Vector2(472, -152), new Vector2(328, -152) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(168, -216), new Vector2(184, -216), new Vector2(184, -200), new Vector2(168, -200) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(216, -216), new Vector2(296, -216), new Vector2(296, -200), new Vector2(216, -200) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(168, -168), new Vector2(184, -168), new Vector2(184, -8), new Vector2(168, -8) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(680, -152), new Vector2(712, -152), new Vector2(712, -40), new Vector2(792, -40), new Vector2(792, -152), new Vector2(936, -152), new Vector2(936, -8), new Vector2(904, -8), new Vector2(904, -136), new Vector2(824, -136), new Vector2(824, -8), new Vector2(680, -8) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(968, -136), new Vector2(1080, -136), new Vector2(1080, -120), new Vector2(968, -120) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(1112, -136), new Vector2(1128, -136), new Vector2(1128, -120), new Vector2(1112, -120) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(424, -120), new Vector2(440, -120), new Vector2(440, -88), new Vector2(424, -88) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(1112, -88), new Vector2(1128, -88), new Vector2(1128, -56), new Vector2(1112, -56) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(424, -56), new Vector2(440, -56), new Vector2(440, -40), new Vector2(424, -40) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(472, -56), new Vector2(648, -56), new Vector2(648, -40), new Vector2(472, -40) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(1032, -24), new Vector2(1304, -24), new Vector2(1304, 136), new Vector2(1032, 136) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(1064, 8), new Vector2(1272, 8), new Vector2(1272, 104), new Vector2(1064, 104) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(24, 24), new Vector2(56, 24), new Vector2(56, 136), new Vector2(136, 136), new Vector2(136, 24), new Vector2(280, 24), new Vector2(280, 168), new Vector2(248, 168), new Vector2(248, 40), new Vector2(168, 40), new Vector2(168, 168), new Vector2(24, 168) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(312, 88), new Vector2(440, 88), new Vector2(440, 104), new Vector2(312, 104) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(472, 88), new Vector2(488, 88), new Vector2(488, 104), new Vector2(472, 104) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-136, 104), new Vector2(-120, 104), new Vector2(-120, 120), new Vector2(-136, 120) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-88, 104), new Vector2(-8, 104), new Vector2(-8, 120), new Vector2(-88, 120) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(472, 136), new Vector2(488, 136), new Vector2(488, 280), new Vector2(472, 280) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-136, 152), new Vector2(-120, 152), new Vector2(-120, 328), new Vector2(-136, 328) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(264, 312), new Vector2(536, 312), new Vector2(536, 472), new Vector2(264, 472) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(296, 344), new Vector2(504, 344), new Vector2(504, 440), new Vector2(296, 440) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-168, 360), new Vector2(120, 360), new Vector2(120, 648), new Vector2(-168, 648) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(-136, 392), new Vector2(-40, 392), new Vector2(-40, 488), new Vector2(-136, 488) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(-8, 392), new Vector2(88, 392), new Vector2(88, 488), new Vector2(-8, 488) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-520, 408), new Vector2(-504, 408), new Vector2(-504, 424), new Vector2(-520, 424) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-472, 408), new Vector2(-200, 408), new Vector2(-200, 424), new Vector2(-472, 424) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-520, 456), new Vector2(-504, 456), new Vector2(-504, 488), new Vector2(-520, 488) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(632, 472), new Vector2(904, 472), new Vector2(904, 632), new Vector2(632, 632) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(360, 504), new Vector2(376, 504), new Vector2(376, 536), new Vector2(360, 536) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(664, 504), new Vector2(872, 504), new Vector2(872, 600), new Vector2(664, 600) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-600, 520), new Vector2(-328, 520), new Vector2(-328, 680), new Vector2(-600, 680) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(-136, 520), new Vector2(-40, 520), new Vector2(-40, 616), new Vector2(-136, 616) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(-8, 520), new Vector2(88, 520), new Vector2(88, 616), new Vector2(-8, 616) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(-568, 552), new Vector2(-360, 552), new Vector2(-360, 648), new Vector2(-568, 648) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(360, 568), new Vector2(376, 568), new Vector2(376, 584), new Vector2(360, 584) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(408, 568), new Vector2(600, 568), new Vector2(600, 584), new Vector2(408, 584) })});
        
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(824, 664), new Vector2(840, 664), new Vector2(840, 904), new Vector2(824, 904) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(72, 680), new Vector2(88, 680), new Vector2(88, 744), new Vector2(72, 744) })});
        //_polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-1080, 712), new Vector2(-808, 712), new Vector2(-808, 872), new Vector2(-1080, 872) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-456, 712), new Vector2(-440, 712), new Vector2(-440, 792), new Vector2(-456, 792) })});
        //_polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(312, 728), new Vector2(584, 728), new Vector2(584, 888), new Vector2(312, 888) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(-1048, 744), new Vector2(-840, 744), new Vector2(-840, 840), new Vector2(-1048, 840) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(344, 760), new Vector2(552, 760), new Vector2(552, 856), new Vector2(344, 856) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(72, 776), new Vector2(88, 776), new Vector2(88, 792), new Vector2(72, 792) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(120, 776), new Vector2(280, 776), new Vector2(280, 792), new Vector2(120, 792) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-776, 824), new Vector2(-488, 824), new Vector2(-488, 840), new Vector2(-776, 840) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-456, 824), new Vector2(-440, 824), new Vector2(-440, 840), new Vector2(-456, 840) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(968, 840), new Vector2(1000, 840), new Vector2(1000, 952), new Vector2(1080, 952), new Vector2(1080, 840), new Vector2(1224, 840), new Vector2(1224, 984), new Vector2(1192, 984), new Vector2(1192, 856), new Vector2(1112, 856), new Vector2(1112, 984), new Vector2(968, 984) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(488, 920), new Vector2(504, 920), new Vector2(504, 1128), new Vector2(488, 1128) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(824, 936), new Vector2(840, 936), new Vector2(840, 952), new Vector2(824, 952) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(872, 936), new Vector2(936, 936), new Vector2(936, 952), new Vector2(872, 952) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(1320, 984), new Vector2(1352, 984), new Vector2(1352, 1096), new Vector2(1432, 1096), new Vector2(1432, 984), new Vector2(1576, 984), new Vector2(1576, 1128), new Vector2(1544, 1128), new Vector2(1544, 1000), new Vector2(1464, 1000), new Vector2(1464, 1128), new Vector2(1320, 1128) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(1032, 1016), new Vector2(1048, 1016), new Vector2(1048, 1048), new Vector2(1032, 1048) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(1032, 1080), new Vector2(1048, 1080), new Vector2(1048, 1096), new Vector2(1032, 1096) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(1080, 1080), new Vector2(1288, 1080), new Vector2(1288, 1096), new Vector2(1080, 1096) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(600, 1096), new Vector2(632, 1096), new Vector2(632, 1208), new Vector2(712, 1208), new Vector2(712, 1096), new Vector2(856, 1096), new Vector2(856, 1240), new Vector2(824, 1240), new Vector2(824, 1112), new Vector2(744, 1112), new Vector2(744, 1240), new Vector2(600, 1240) })});
        
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(488, 1160), new Vector2(504, 1160), new Vector2(504, 1176), new Vector2(488, 1176) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(536, 1160), new Vector2(568, 1160), new Vector2(568, 1176), new Vector2(536, 1176) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(888, 1208), new Vector2(920, 1208), new Vector2(920, 1224), new Vector2(888, 1224) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(952, 1208), new Vector2(968, 1208), new Vector2(968, 1224), new Vector2(952, 1224) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(952, 1256), new Vector2(968, 1256), new Vector2(968, 1400), new Vector2(952, 1400) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(632, 1272), new Vector2(648, 1272), new Vector2(648, 1336), new Vector2(632, 1336) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(168, 1304), new Vector2(440, 1304), new Vector2(440, 1464), new Vector2(168, 1464) })});
        //
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(200, 1336), new Vector2(408, 1336), new Vector2(408, 1432), new Vector2(200, 1432) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(472, 1368), new Vector2(600, 1368), new Vector2(600, 1384), new Vector2(472, 1384) })});
        
        //
        //_polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(632, 1368), new Vector2(648, 1368), new Vector2(648, 1384), new Vector2(632, 1384) })});
        // _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(712, 1432), new Vector2(984, 1432), new Vector2(984, 1592), new Vector2(712, 1592) })});
        
        
        //_polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(744, 1464), new Vector2(952, 1464), new Vector2(952, 1560), new Vector2(744, 1560) })});
        
        _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(-456, 712), new Vector2(-440, 712), new Vector2(-440, 792), new Vector2(-456, 792) })});
        _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.In, Points = new List<Vector2>(new []{ new Vector2(-1048, 744), new Vector2(-840, 744), new Vector2(-840, 840), new Vector2(-1048, 840) })});
        _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(488, 920), new Vector2(504, 920), new Vector2(504, 1128), new Vector2(488, 1128) })});
        _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(1320, 984), new Vector2(1352, 984), new Vector2(1352, 1096), new Vector2(1432, 1096), new Vector2(1432, 984), new Vector2(1576, 984), new Vector2(1576, 1128), new Vector2(1544, 1128), new Vector2(1544, 1000), new Vector2(1464, 1000), new Vector2(1464, 1128), new Vector2(1320, 1128) })});
        _polygonDataList.Add(new NavigationPolygonData(){Type = NavigationPolygonType.Out, Points = new List<Vector2>(new []{ new Vector2(712, 1432), new Vector2(984, 1432), new Vector2(984, 1592), new Vector2(712, 1592) })});

    }

    
    /// <summary>
    /// 计算网格区域, 并将导航区域挂载到 navigationRoot 上
    /// </summary>
    public void GenerateNavigationPolygon(Node2D navigationRoot)
    {
        GenerateNavigationPolygon();
        FullTestData();
        // 在 Godot4.0_rc6 中 如果将所有点都放在 NavigationPolygon 里面, 即使点是对的, 但调用 MakePolygonsFromOutlines 还是可能会报错, 这应该是个bug
        // for (var i = 0; i < _polygonDataList.Count; i++)
        // {
        //     var polygonData = _polygonDataList[i];
        //     var polygon = new NavigationPolygon();
        //     var array = polygonData.Points.ToArray();
        //     polygon.AddOutline(array);
        //     polygon.MakePolygonsFromOutlines();
        //     var navigationPolygon = new NavigationRegion2D();
        //     navigationPolygon.Name = "NavigationRegion" + i;
        //     navigationPolygon.NavigationPolygon = polygon;
        //     navigationRoot.AddChild(navigationPolygon);
        // }
        var polygon = new NavigationPolygon();

        for (var i = 0; i < _polygonDataList.Count; i++)
        {
            var polygonData = _polygonDataList[i];
            if (polygonData.Type == NavigationPolygonType.Out)
            {
                var list = new List<Vector2>(polygonData.Points);
                //list.Add(list[0]);
                var array = list.ToArray();
                polygon.AddOutline(array);
            }
        }
        
        for (var i = 0; i < _polygonDataList.Count; i++)
        {
            var polygonData = _polygonDataList[i];
            if (polygonData.Type == NavigationPolygonType.In)
            {
                var list = new List<Vector2>(polygonData.Points);
                //list.Add(list[0]);
                var array = list.ToArray();
                polygon.AddOutline(array);
            }
        }

        // for (var i = 0; i < _polygonDataList.Count; i++)
        // {
        //     var polygonData = _polygonDataList[i];
        //     var array = new Vector2[polygonData.Points.Count];
        //     for (var j = 0; j < polygonData.Points.Count; j++)
        //     {
        //         var p = polygonData.Points[j];
        //         array[j] = new Vector2(p.X + 2000, p.Y + 2000);
        //     }
        //     polygon.AddOutline(array);
        // }
        
        polygon.MakePolygonsFromOutlines();
        var navigationPolygon = new NavigationRegion2D();
        navigationPolygon.Name = "NavigationRegion";
        navigationPolygon.NavigationPolygon = polygon;
        navigationRoot.AddChild(navigationPolygon);
    }

    /// <summary>
    /// 获取导航点数据
    /// </summary>
    public NavigationPolygonData[] GetPolygonData()
    {
        return _polygonDataList.ToArray();
    }

    /// <summary>
    /// 返回指定位置的Tile是否为可以行走
    /// </summary>
    public bool IsWayTile(int x, int y)
    {
        return _tileRoot.GetCellTileData(DungeonTile.FloorMapLayer, new Vector2I(x, y)) != null;
    }

    /// <summary>
    /// 返回指定坐标下对应的Tile是否为可以行走
    /// </summary>
    public bool IsWayPosition(float x, float y)
    {
        var tileMapCellSize = _tileRoot.CellQuadrantSize;
        return IsWayTile((int)(x / tileMapCellSize), (int)(y / tileMapCellSize));
    }

    /// <summary>
    /// 自动生成导航区域
    /// </summary>
    private void GenerateNavigationPolygon()
    {
        var size = new Vector2(_tileRoot.CellQuadrantSize, _tileRoot.CellQuadrantSize);

        var rect = _tileRoot.GetUsedRect();

        var x = rect.Position.X;
        var y = rect.Position.Y;
        var w = rect.Size.X;
        var h = rect.Size.Y;

        for (int j = y; j < h; j++)
        {
            for (int i = x; i < w; i++)
            {
                if (IsWayTile(i, j))
                {
                    if (!_usePoints.Contains(new Vector2(i, j)))
                    {
                        NavigationPolygonData polygonData = null;

                        if (!IsWayTile(i, j - 1))
                        {
                            polygonData = CalcOutline(i, j, _tileRoot, size);
                        }
                        else if (!IsWayTile(i, j + 1))
                        {
                            polygonData = CalcInline(i, j, _tileRoot, size);
                        }

                        if (polygonData != null)
                        {
                            _polygonDataList.Add(polygonData);
                        }
                    }
                }
            }
        }
    }
    
    //计算导航网格外轮廓
    private NavigationPolygonData CalcOutline(int i, int j, TileMap tileMap, Vector2 size)
    {
        var polygonData = new NavigationPolygonData();
        polygonData.Type = NavigationPolygonType.Out;
        var points = polygonData.Points;
        // 0:右, 1:下, 2:左, 3:上
        var dir = 0;
        var offset = new Vector2(size.X * 0.5f, size.Y * 0.5f);
        //找到路, 向右开始找边界
        var startPos = new Vector2(i, j);

        var tempI = i;
        var tempJ = j;

        while (true)
        {
            switch (dir)
            {
                case 0: //右
                {
                    if (IsWayTile(tempI, tempJ - 1)) //先向上找
                    {
                        dir = 3;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempJ--;
                        break;
                    }
                    else if (IsWayTile(tempI + 1, tempJ)) //再向右找
                    {
                        if (points.Count == 0)
                        {
                            points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        }

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        PutUsePoint(new Vector2(tempI, tempJ));
                        tempI++;
                        break;
                    }
                    else if (IsWayTile(tempI, tempJ + 1)) //向下找
                    {
                        dir = 1;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempJ++;
                        break;
                    }

                    return null;
                }
                case 1: //下
                {
                    if (IsWayTile(tempI + 1, tempJ)) //先向右找
                    {
                        dir = 0;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempI++;
                        break;
                    }
                    else if (IsWayTile(tempI, tempJ + 1)) //再向下找
                    {
                        if (points.Count == 0)
                        {
                            points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        }

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        PutUsePoint(new Vector2(tempI, tempJ));
                        tempJ++;
                        break;
                    }
                    else if (IsWayTile(tempI - 1, tempJ)) //向左找
                    {
                        dir = 2;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempI--;
                        break;
                    }

                    return null;
                }
                case 2: //左
                {
                    if (IsWayTile(tempI, tempJ + 1)) //先向下找
                    {
                        dir = 1;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempJ++;
                        break;
                    }
                    else if (IsWayTile(tempI - 1, tempJ)) //再向左找
                    {
                        if (points.Count == 0)
                        {
                            points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        }

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        PutUsePoint(new Vector2(tempI, tempJ));
                        tempI--;
                        break;
                    }
                    else if (IsWayTile(tempI, tempJ - 1)) //向上找
                    {
                        dir = 3;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempJ--;
                        break;
                    }

                    return null;
                }
                case 3: //上
                {
                    if (IsWayTile(tempI - 1, tempJ)) //先向左找
                    {
                        dir = 2;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempI--;
                        break;
                    }
                    else if (IsWayTile(tempI, tempJ - 1)) //再向上找
                    {
                        if (points.Count == 0)
                        {
                            points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        }

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        PutUsePoint(new Vector2(tempI, tempJ));
                        tempJ--;
                        break;
                    }
                    else if (IsWayTile(tempI + 1, tempJ)) //向右找
                    {
                        dir = 0;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempI++;
                        break;
                    }

                    return null;
                }
            }
        }
    }

    //计算导航网格内轮廓
    private NavigationPolygonData CalcInline(int i, int j, TileMap tileMap, Vector2 size)
    {
        var polygonData = new NavigationPolygonData();
        polygonData.Type = NavigationPolygonType.In;
        var points = polygonData.Points;
        // 0:右, 1:下, 2:左, 3:上
        var dir = 0;
        var offset = new Vector2(size.X * 0.5f, size.Y * 0.5f);
        //找到路, 向右开始找边界
        var startPos = new Vector2(i - 1, j);

        var tempI = i;
        var tempJ = j;

        while (true)
        {
            switch (dir)
            {
                case 0: //右
                {
                    if (IsWayTile(tempI, tempJ + 1)) //向下找
                    {
                        dir = 1;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempJ++;
                        break;
                    }
                    else if (IsWayTile(tempI + 1, tempJ)) //再向右找
                    {
                        if (points.Count == 0)
                        {
                            points.Add(new Vector2((tempI - 1) * size.X, tempJ * size.Y) + offset);
                        }

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        PutUsePoint(new Vector2(tempI, tempJ));
                        tempI++;
                        break;
                    }
                    else if (IsWayTile(tempI, tempJ - 1)) //先向上找
                    {
                        dir = 3;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempJ--;
                        break;
                    }

                    return null;
                }
                case 1: //下
                {
                    if (IsWayTile(tempI - 1, tempJ)) //向左找
                    {
                        dir = 2;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempI--;
                        break;
                    }
                    else if (IsWayTile(tempI, tempJ + 1)) //再向下找
                    {
                        if (points.Count == 0)
                        {
                            points.Add(new Vector2((tempI - 1) * size.X, tempJ * size.Y) + offset);
                        }

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        PutUsePoint(new Vector2(tempI, tempJ));
                        tempJ++;
                        break;
                    }
                    else if (IsWayTile(tempI + 1, tempJ)) //先向右找
                    {
                        dir = 0;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempI++;
                        break;
                    }

                    return null;
                }
                case 2: //左
                {
                    if (IsWayTile(tempI, tempJ - 1)) //向上找
                    {
                        dir = 3;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempJ--;
                        break;
                    }
                    else if (IsWayTile(tempI - 1, tempJ)) //再向左找
                    {
                        if (points.Count == 0)
                        {
                            points.Add(new Vector2((tempI - 1) * size.X, tempJ * size.Y) + offset);
                        }

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        PutUsePoint(new Vector2(tempI, tempJ));
                        tempI--;
                        break;
                    }
                    else if (IsWayTile(tempI, tempJ + 1)) //先向下找
                    {
                        dir = 1;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempJ++;
                        break;
                    }

                    return null;
                }
                case 3: //上
                {
                    if (IsWayTile(tempI + 1, tempJ)) //向右找
                    {
                        dir = 0;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempI++;
                        break;
                    }
                    else if (IsWayTile(tempI, tempJ - 1)) //再向上找
                    {
                        if (points.Count == 0)
                        {
                            points.Add(new Vector2((tempI - 1) * size.X, tempJ * size.Y) + offset);
                        }

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        PutUsePoint(new Vector2(tempI, tempJ));
                        tempJ--;
                        break;
                    }
                    else if (IsWayTile(tempI - 1, tempJ)) //先向左找
                    {
                        dir = 2;

                        var pos = new Vector2(tempI, tempJ);
                        if (points.Count > 1 && pos == startPos)
                        {
                            return polygonData;
                        }

                        points.Add(new Vector2(tempI * size.X, tempJ * size.Y) + offset);
                        PutUsePoint(pos);

                        tempI--;
                        break;
                    }

                    return null;
                }
            }
        }
    }

    //记录导航网格中已经使用过的坐标
    private void PutUsePoint(Vector2 pos)
    {
        if (_usePoints.Contains(pos))
        {
            throw new Exception("生成导航多边形发生错误! 点: " + pos + "发生交错!");
        }

        _usePoints.Add(pos);
    }
}