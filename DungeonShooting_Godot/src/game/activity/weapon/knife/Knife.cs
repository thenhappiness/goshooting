
using Godot;

/// <summary>
/// 近战武器,刀
/// </summary>
[Tool]
public partial class Knife : Weapon
{
    /// <summary>
    /// 近战攻击范围
    /// </summary>
    [Export]
    public int AttackRange { get; set; } = 41;

    /// <summary>
    /// 开始蓄力时武器抬起角度
    /// </summary>
    [Export]
    public int BeginChargeAngle { get; set; } = 120;
    
    private Area2D _hitArea;
    private int _attackIndex = 0;
    private CollisionPolygon2D _collisionPolygon;
    
    public override void OnInit()
    {
        base.OnInit();

        //没有Master时不能触发开火
        NoMasterCanTrigger = false;
        _hitArea = GetNode<Area2D>("HitArea");
        _collisionPolygon = new CollisionPolygon2D();
        var a = Mathf.Abs(-BeginChargeAngle + Attribute.UpliftAngle);
        var ca = Utils.ConvertAngle(-a / 2f);
        _collisionPolygon.Polygon = Utils.CreateSectorPolygon(ca, AttackRange, a, 6);
        _hitArea.AddChild(_collisionPolygon);
        
        _hitArea.Monitoring = false;
        _hitArea.Monitorable = false;
        _hitArea.BodyEntered += OnBodyEntered;

        //禁用自动播放动画
        IsAutoPlaySpriteFrames = false;
    }

    protected override void Process(float delta)
    {
        base.Process(delta);
        if (IsActive)
        {
            //让碰撞节点与武器挂载节点位置保持一致, 而不跟着武器走
            _hitArea.GlobalPosition = Master.MountPoint.GlobalPosition;
        }
    }

    protected override void PhysicsProcess(float delta)
    {
        base.PhysicsProcess(delta);
        //过去两个物理帧后就能关闭碰撞了
        if (++_attackIndex >= 2)
        {
            _hitArea.Monitoring = false;
        }
    }

    protected override void OnBeginCharge()
    {
        //开始蓄力时武器角度
        RotationDegrees = -BeginChargeAngle;
    }

    protected override void OnFire()
    {
        GD.Print("近战武器攻击! 蓄力时长: " + GetTriggerChargeTime() + ", 扳机按下时长: " + GetTriggerDownTime());
        //更新碰撞层级
        _hitArea.CollisionMask = GetAttackLayer() | PhysicsLayer.Bullet;
        //启用碰撞
        _hitArea.Monitoring = true;
        _attackIndex = 0;

        if (IsActive) //被使用
        {
            //播放挥刀特效
            SpecialEffectManager.Play(
                Master,
                ResourcePath.resource_spriteFrames_effect_KnifeHit1_tres, "default",
                Master.MountPoint.Position,
                Master.MountPoint.Rotation + Mathf.DegToRad(Attribute.UpliftAngle + 60),
                AnimatedSprite.Scale,
                new Vector2(17, 4), 1
            );
        }


        if (Master == Player.Current)
        {
            var r = Master.MountPoint.RotationDegrees;
            //创建屏幕抖动
            if (Master.Face == FaceDirection.Right)
            {
                //GameCamera.Main.DirectionalShake(Vector2.FromAngle(Mathf.DegToRad(r - 90)) * 5);
                GameCamera.Main.DirectionalShake(Vector2.FromAngle(Mathf.DegToRad(r - 180)) * Attribute.CameraShake);
            }
            else
            {
                //GameCamera.Main.DirectionalShake(Vector2.FromAngle(Mathf.DegToRad(270 - r)) * 5);
                GameCamera.Main.DirectionalShake(Vector2.FromAngle(Mathf.DegToRad(-r)) * Attribute.CameraShake);
            }
        }
    }

    protected override void OnShoot(float fireRotation)
    {
        
    }

    protected override int UseAmmoCount()
    {
        //这里要做判断, 如果没有碰到敌人, 则不消耗弹药 (耐久)
        return 0;
    }

    private void OnBodyEntered(Node2D body)
    {
        //GD.Print("碰到物体: " + body.Name);
        var activityObject = body.AsActivityObject();
        if (activityObject != null)
        {
            if (activityObject is Role role) //碰到角色
            {
                var damage = Utils.Random.RandomConfigRange(Attribute.HarmRange);
                damage = Master.RoleState.CallCalcDamageEvent(damage);
                //击退
                if (role is not Player) //目标不是玩家才会触发击退
                {
                    var attr = Master.IsAi ? AiUseAttribute : PlayerUseAttribute;
                    var repel = Utils.Random.RandomConfigRange(attr.RepelRnage);
                    var position = role.GlobalPosition - Master.MountPoint.GlobalPosition;
                    var v2 = position.Normalized() * repel;
                    role.MoveController.AddForce(v2, repel * 2);
                }
                
                //造成伤害
                role.CallDeferred(nameof(Role.Hurt), damage, (role.GetCenterPosition() - GlobalPosition).Angle());
            }
            else if (activityObject is Bullet bullet) //攻击子弹
            {
                var attackLayer = bullet.AttackLayer;
                if (Master.CollisionWithMask(attackLayer)) //是攻击玩家的子弹
                {
                    bullet.PlayDisappearEffect();
                    bullet.BasisVelocity = bullet.BasisVelocity.Rotated(Mathf.Pi);
                    bullet.Rotation += Mathf.Pi;
                    bullet.AttackLayer = Master.AttackLayer;
                }
            }
        }
    }
}
