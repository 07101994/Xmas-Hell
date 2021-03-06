using System;
using System.Collections.Generic;
using System.Linq;
using BulletML;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using MonoGame.Extended;
using MonoGame.Extended.TextureAtlases;
using Sprite = MonoGame.Extended.Sprites.Sprite;
using SpriterDotNet;
using SpriterDotNet.MonoGame;
using SpriterDotNet.Providers;
using SpriterDotNet.MonoGame.Content;
using XmasHell.Geometry;
using XmasHell.Physics;
using XmasHell.Spriter;
using XmasHell.BulletML;
using XmasHell.Extensions;
using XmasHell.GUI;

namespace XmasHell.Entities.Bosses
{
    public enum ScreenSide
    {
        Left,
        Top,
        Right,
        Bottom
    };

    // TODO: Inherit from AbstractEntity
    public abstract class Boss : ISpriterPhysicsEntity, IDisposable
    {
        public XmasHell Game;
        public readonly BossType BossType;
        public Vector2 InitialPosition;
        protected float InitialLife;
        protected float Life;
        private Sprite _hpBar;
        private AbstractGuiLabel _timerLabel;
        private TimeSpan _timer;

        public bool Invincible;

        public Vector2 Direction = Vector2.Zero; // values in radians
        public float Speed;
        public Vector2 Acceleration = Vector2.One;
        public float AngularVelocity = 5f;

        // Relative to position targeting
        public bool TargetingPosition = false;
        private Vector2 _initialPosition = Vector2.Zero;
        private Vector2 _targetPosition = Vector2.Zero;
        private TimeSpan _targetPositionTimer = TimeSpan.Zero;
        private TimeSpan _targetPositionTime = TimeSpan.Zero;
        private Vector2 _targetDirection = Vector2.Zero;

        // Relative to angle targeting
        public bool TargetingAngle = false;
        private float _initialAngle = 0f;
        private float _targetAngle = 0f;
        private TimeSpan _targetAngleTimer = TimeSpan.Zero;
        private TimeSpan _targetAngleTime = TimeSpan.Zero;

        private readonly PositionDelegate _playerPositionDelegate;

        private TimeSpan _hitTimer = TimeSpan.Zero;
        public bool Tinted;
        public Color HitColor;

        private readonly Line _leftWallLine;
        private readonly Line _bottomWallLine;
        private readonly Line _upWallLine;
        private readonly Line _rightWallLine;

        // Behaviours
        protected readonly List<AbstractBossBehaviour> Behaviours;
        protected int PreviousBehaviourIndex;
        protected int CurrentBehaviourIndex;

        // New position timer
        public bool StartNewPositionTimer = false;
        private TimeSpan NewPositionTimer = TimeSpan.Zero;
        public float NewPositionTimerTime = 0f;
        public event EventHandler<float> NewPositionTimerFinished = null;

        // Shoot timer
        public bool StartShootTimer = false;
        private TimeSpan ShootTimer = TimeSpan.Zero;
        public float ShootTimerTime = 0f;
        public event EventHandler<float> ShootTimerFinished = null;

        public bool IsOutside = false;

        // BulletML
        protected readonly List<string> BulletPatternFiles;

        // Spriter
        protected string SpriterFilename;
        protected static readonly Config DefaultAnimatorConfig = new Config
        {
            MetadataEnabled = true,
            EventsEnabled = true,
            PoolingEnabled = true,
            TagsEnabled = false,
            VarsEnabled = false,
            SoundsEnabled = false
        };

        private readonly IList<CustomSpriterAnimator> _animators = new List<CustomSpriterAnimator>();
        public CustomSpriterAnimator CurrentAnimator;

        #region Getters

        public virtual Vector2 Position()
        {
            return CurrentAnimator.Position;
        }

        public virtual float Rotation()
        {
            return CurrentAnimator.Rotation;
        }

        public virtual Vector2 Origin()
        {
            return new Vector2(Width() / 2f, Height() / 2f);
        }

        public virtual Vector2 Scale()
        {
            return CurrentAnimator.Scale;
        }

        public CustomSpriterAnimator GetCurrentAnimator()
        {
            return CurrentAnimator;
        }

        public Vector2 ActionPointPosition()
        {
            if (CurrentAnimator.FrameData != null)
            {
                foreach (var pointData in CurrentAnimator.FrameData.PointData)
                {
                    if (pointData.Key.StartsWith("action_point"))
                    {
                        var actionPoint = new Vector2(pointData.Value.X, -pointData.Value.Y);
                        var rotatedActionPoint = MathExtension.RotatePoint(actionPoint, Rotation());
                        return Position() + rotatedActionPoint;
                    }
                }


            }

            return Position();
        }

        public float ActionPointDirection()
        {
            if (CurrentAnimator.FrameData != null && CurrentAnimator.FrameData.PointData.ContainsKey("action_point"))
            {
                var actionPoint = CurrentAnimator.FrameData.PointData["action_point"];
                return MathHelper.ToRadians(actionPoint.Angle - 90f) + Rotation();
            }

            return Rotation();
        }

        public virtual int Width()
        {
            if (CurrentAnimator.SpriteProvider != null)
            {
                return (int)CurrentAnimator.SpriteProvider.Get(0, 0).Height();
            }

            return 0;
        }

        public virtual int Height()
        {
            if (CurrentAnimator.SpriteProvider != null)
            {
                return (int)CurrentAnimator.SpriteProvider.Get(0, 0).Width();
            }

            return 0;
        }

        protected float GetSpritePartWidth(string name)
        {
            if (CurrentAnimator != null)
            {
                var spriteBodyPart = Array.Find(CurrentAnimator.Entity.Spriter.Folders[0].Files, (file) => file.Name == name);
                return spriteBodyPart.Width;
            }

            return 0f;
        }

        protected float GetSpritePartHeight(string name)
        {
            if (CurrentAnimator != null)
            {
                var spriteBodyPart = Array.Find(CurrentAnimator.Entity.Spriter.Folders[0].Files, (file) => file.Name == name);
                return spriteBodyPart.Height;
            }

            return 0f;
        }

        #endregion

        #region Setters

        public void Position(Vector2 value)
        {
            CurrentAnimator.Position = value;
        }

        public void Rotation(float value)
        {
            CurrentAnimator.Rotation = value;
        }

        public void Scale(Vector2 value)
        {
            CurrentAnimator.Scale = value;
        }

        #endregion

        protected Boss(XmasHell game, BossType type, PositionDelegate playerPositionDelegate)
        {
            Game = game;
            BossType = type;
            _playerPositionDelegate = playerPositionDelegate;

            InitialPosition = new Vector2(
                Game.ViewportAdapter.VirtualWidth / 2f,
                Game.ViewportAdapter.VirtualHeight * 0.15f
            );
            InitialLife = GameConfig.BossDefaultLife;

            // Behaviours
            Behaviours = new List<AbstractBossBehaviour>();

            // BulletML
            BulletPatternFiles = new List<string>();

            HitColor = Color.White * 0.5f;

            _hpBar = new Sprite(
                new TextureRegion2D(
                    Assets.GetTexture2D("pixel"),
                    0, 0, GameConfig.VirtualResolution.X, 50
                )
            )
            {
                Origin = Vector2.Zero,
                Color = Color.Red
            };

            Game.SpriteBatchManager.Boss = this;
            Game.SpriteBatchManager.UISprites.Add(_hpBar);

            _timerLabel = new AbstractGuiLabel("00:00", Assets.GetFont("Graphics/Fonts/ui-small"), new Vector2(Game.ViewportAdapter.VirtualWidth / 2f, 25), Color.White, true);
            Game.SpriteBatchManager.UILabels.Add(_timerLabel);

            // To compute line/wall intersection
            _bottomWallLine = new Line(
                new Vector2(0f, GameConfig.VirtualResolution.Y),
                new Vector2(GameConfig.VirtualResolution.X, GameConfig.VirtualResolution.Y)
            );

            _leftWallLine = new Line(
                new Vector2(0f, 0f),
                new Vector2(0f, GameConfig.VirtualResolution.Y)
            );

            _rightWallLine = new Line(
                new Vector2(GameConfig.VirtualResolution.X, 0f),
                new Vector2(GameConfig.VirtualResolution.X, GameConfig.VirtualResolution.Y)
            );

            _upWallLine = new Line(
                new Vector2(0f, 0f),
                new Vector2(GameConfig.VirtualResolution.X, 0f)
            );
        }

        public virtual void Initialize()
        {
            LoadBulletPatterns();
            LoadSpriterSprite();
            InitializePhysics();

            Reset();
        }

        public void Dispose()
        {
            Game.SpriteBatchManager.Boss = null;
            Game.SpriteBatchManager.UISprites.Remove(_hpBar);
            Game.SpriteBatchManager.UILabels.Remove(_timerLabel);
            Game.SpriteBatchManager.BossBullets.Clear();

            Game.GameManager.CollisionWorld.ClearBossHitboxes();
            Game.GameManager.CollisionWorld.ClearBossBullets();
        }

        protected virtual void LoadSpriterSprite()
        {
            if (SpriterFilename == string.Empty)
                throw new Exception("You need to specify a path to the spriter file of this boss");

            var factory = new DefaultProviderFactory<ISprite, SoundEffect>(DefaultAnimatorConfig, true);

            var loader = new SpriterContentLoader(Game.Content, SpriterFilename);
            loader.Fill(factory);

            foreach (var entity in loader.Spriter.Entities)
            {
                var animator = new CustomSpriterAnimator(Game, entity, factory);
                _animators.Add(animator);
            }

            CurrentAnimator = _animators.First();
            CurrentAnimator.Position = InitialPosition;
        }

        protected virtual void InitializePhysics()
        {
        }

        protected virtual void Reset()
        {
            Game.GameManager.MoverManager.Clear();
            Life = InitialLife;
            CurrentAnimator.Position = InitialPosition;
            Invincible = false;
            Tinted = false;
            _timer = TimeSpan.Zero;

            Direction = Vector2.Zero;
            Speed = GameConfig.BossDefaultSpeed;
            CurrentBehaviourIndex = 0;
            PreviousBehaviourIndex = -1;
        }

        private void RestoreDefaultState()
        {
            Direction = Vector2.Zero;
            CurrentAnimator.Play("Idle");
        }

        private void LoadBulletPatterns()
        {
            foreach (var bulletPatternFile in BulletPatternFiles)
            {
                if (Game.GameManager.MoverManager.FindPattern(bulletPatternFile) == null)
                {
                    var pattern = new BulletPattern();
                    var stream = Assets.GetPattern(bulletPatternFile);
                    pattern.ParseStream(bulletPatternFile, stream);
                    Game.GameManager.MoverManager.AddPattern(bulletPatternFile, pattern);
                }
            }
        }

        public void Destroy()
        {
            Game.GameManager.ParticleManager.EmitBossDestroyedParticles(CurrentAnimator.Position);
            Reset();
        }

        // Move to a given position in "time" seconds
        public void MoveTo(Vector2 position, float time, bool force = false)
        {
            if (TargetingPosition && !force)
                return;

            TargetingPosition = true;
            _targetPositionTimer = TimeSpan.FromSeconds(time);
            _targetPositionTime = TimeSpan.FromSeconds(time);
            _targetPosition = position;
            _initialPosition = CurrentAnimator.Position;
        }

        // Move to a given position keeping the actual speed
        public void MoveTo(Vector2 position, bool force = false)
        {
            if (TargetingPosition && !force)
                return;

            TargetingPosition = true;
            _targetPosition = position;
            _targetDirection = Vector2.Normalize(position - CurrentAnimator.Position);
        }

        public void MoveToCenter(float time, bool force = false)
        {
            MoveTo(new Vector2(GameConfig.VirtualResolution.X / 2f, GameConfig.VirtualResolution.Y / 2f), time, force);
        }

        public void MoveToCenter(bool force = false)
        {
            MoveTo(new Vector2(GameConfig.VirtualResolution.X / 2f, GameConfig.VirtualResolution.Y / 2f), force);
        }

        public void MoveToInitialPosition(float time, bool force = false)
        {
            MoveTo(InitialPosition, time, force);
        }

        public void MoveToInitialPosition(bool force = false)
        {
            MoveTo(InitialPosition, force);
        }

        public void MoveOutside(float time, bool force = true)
        {
            MoveTo(GetNearestOutsidePosition(), time, force);
        }

        public void MoveOutside(bool force = true)
        {
            MoveTo(GetNearestOutsidePosition(), force);
        }

        public Vector2 GetNearestOutsidePosition()
        {
            // Get the nearest border
            var newPosition = Position();
            ScreenSide side = GetNearestBorder();

            switch (side)
            {
                case ScreenSide.Left:
                    newPosition.X = -Width();
                    break;
                case ScreenSide.Top:
                    newPosition.Y = -Height();
                    break;
                case ScreenSide.Right:
                    newPosition.X = Game.ViewportAdapter.VirtualWidth + Width();
                    break;
                case ScreenSide.Bottom:
                    newPosition.Y = Game.ViewportAdapter.VirtualHeight + Height();
                    break;
                default:
                    break;
            }

            return newPosition;
        }

        public ScreenSide GetNearestBorder()
        {
            if (Position().X < Game.ViewportAdapter.VirtualWidth / 2f)
            {
                if (Position().Y < Game.ViewportAdapter.VirtualHeight / 2f)
                {
                    if (Position().Y < Position().X)
                    {
                        return ScreenSide.Top;
                    }
                }
                else
                {
                    if (Game.ViewportAdapter.VirtualHeight - Position().Y < Position().X)
                    {
                        return ScreenSide.Bottom;
                    }
                }

                return ScreenSide.Left;
            }
            else
            {
                if (Position().Y < Game.ViewportAdapter.VirtualHeight / 2f)
                {
                    if (Position().Y < Game.ViewportAdapter.VirtualWidth - Position().X)
                    {
                        return ScreenSide.Top;
                    }
                }
                else
                {
                    if (Game.ViewportAdapter.VirtualHeight - Position().Y <
                        Game.ViewportAdapter.VirtualWidth - Position().X)
                    {
                        return ScreenSide.Bottom;
                    }
                }

                return ScreenSide.Right;
            }
        }

        public void RotateTo(float angle, float time, bool force = false)
        {
            if (TargetingAngle && !force)
                return;

            TargetingAngle = true;
            _targetAngle = angle;
            _initialAngle = CurrentAnimator.Rotation;
            _targetAngleTimer = TimeSpan.FromSeconds(time);
            _targetAngleTime = TimeSpan.FromSeconds(time);
        }

        public void RotateTo(float angle, bool force = false)
        {
            if (TargetingAngle && !force)
                return;

            TargetingAngle = true;
            _targetAngle = angle;
        }

        public void StopMoving()
        {
            TargetingPosition = false;
            _targetPositionTime = TimeSpan.Zero;
            _targetDirection = Vector2.Zero;
        }

        public Vector2 GetPlayerPosition()
        {
            return _playerPositionDelegate();
        }

        public Vector2 GetPlayerDirection()
        {
            var playerPosition = GetPlayerPosition();
            var currentPosition = CurrentAnimator.Position;
            var angle = (currentPosition - playerPosition).ToAngle();

            angle += MathHelper.PiOver2;

            return MathExtension.AngleToDirection(angle);
        }

        public float GetPlayerDirectionAngle()
        {
            var playerPosition = GetPlayerPosition();
            var currentPosition = CurrentAnimator.Position;
            var angle = (currentPosition - playerPosition).ToAngle();

            return angle;
        }

        public bool GetLineWallIntersectionPosition(Line line, ref Vector2 newPosition)
        {
            // Make sure the line go out of the screen
            var maxDistance = (float) Math.Sqrt(
                GameConfig.VirtualResolution.X * GameConfig.VirtualResolution.X +
                GameConfig.VirtualResolution.Y * GameConfig.VirtualResolution.Y
            );
            var direction = Vector2.Normalize(line.Second - line.First);
            line.Second += (direction * maxDistance);

            return
                MathExtension.LinesIntersect(_bottomWallLine, line, ref newPosition) ||
                MathExtension.LinesIntersect(_leftWallLine, line, ref newPosition) ||
                MathExtension.LinesIntersect(_rightWallLine, line, ref newPosition) ||
                MathExtension.LinesIntersect(_upWallLine, line, ref newPosition);
        }

        public void TakeDamage(float amount)
        {
            if (Invincible)
                return;

            Life -= amount;

            if (Life < 0f)
                Destroy();

            _hitTimer = TimeSpan.FromMilliseconds(20);
        }

        public void TriggerPattern(string patternName, BulletType type, bool clear = false, Vector2? position = null, float? direction = null)
        {
            Game.GameManager.MoverManager.TriggerPattern(patternName, type, clear, position, direction);
        }

        public virtual void Update(GameTime gameTime)
        {
            UpdatePosition(gameTime);
            UpdateRotation(gameTime);
            UpdateBehaviour(gameTime);

            // Is outside of the screen?
            IsOutside = Game.GameManager.IsOutside(Position());

            // New position timer
            if (StartNewPositionTimer && NewPositionTimerFinished != null)
            {
                if (NewPositionTimer.TotalMilliseconds > 0)
                    NewPositionTimer -= gameTime.ElapsedGameTime;
                else
                {
                    NewPositionTimer = TimeSpan.FromSeconds(NewPositionTimerTime);
                    NewPositionTimerFinished.Invoke(this, NewPositionTimerTime);
                }
            }

            // Shoot timer
            if (StartShootTimer && ShootTimerFinished != null)
            {
                if (ShootTimer.TotalMilliseconds > 0)
                    ShootTimer -= gameTime.ElapsedGameTime;
                else
                {
                    ShootTimer = TimeSpan.FromSeconds(ShootTimerTime);
                    ShootTimerFinished.Invoke(this, ShootTimerTime);
                }
            }

            if (_hitTimer.TotalMilliseconds > 0)
                _hitTimer -= gameTime.ElapsedGameTime;

            Tinted = _hitTimer.TotalMilliseconds > 0;

            var portion = (InitialLife/Behaviours.Count);
            var value = Life - (InitialLife - (CurrentBehaviourIndex + 1) * portion);

            _hpBar.Scale = new Vector2(value / portion, 1f);
            _hpBar.Color = Tinted ? Color.White : GameConfig.BossHPBarColors[CurrentBehaviourIndex];

            _timer += gameTime.ElapsedGameTime;
            _timerLabel.Text = _timer.ToString("mm\\:ss");

            CurrentAnimator.Update(gameTime.ElapsedGameTime.Milliseconds);
        }

        private void UpdatePosition(GameTime gameTime)
        {
            if (TargetingPosition)
            {
                if (!_targetDirection.Equals(Vector2.Zero))
                {
                    var currentPosition = CurrentAnimator.Position;
                    var distance = Vector2.Distance(currentPosition, _targetPosition);
                    var deltaDistance = Speed * gameTime.GetElapsedSeconds();

                    if (distance < deltaDistance)
                    {
                        TargetingPosition = false;
                        _targetDirection = Vector2.Zero;
                        CurrentAnimator.Position = _targetPosition;
                    }
                    else
                    {
                        // TODO: Perform some cubic interpolation
                        CurrentAnimator.Position = currentPosition + (_targetDirection * deltaDistance) * Acceleration;
                    }
                }
                else
                {
                    var newPosition = Vector2.Zero;
                    var lerpAmount = (float) (_targetPositionTime.TotalSeconds/_targetPositionTimer.TotalSeconds);

                    newPosition.X = MathHelper.SmoothStep(_targetPosition.X, _initialPosition.X, lerpAmount);
                    newPosition.Y = MathHelper.SmoothStep(_targetPosition.Y, _initialPosition.Y, lerpAmount);

                    if (lerpAmount < 0.001f)
                    {
                        TargetingPosition = false;
                        _targetPositionTime = TimeSpan.Zero;
                        CurrentAnimator.Position = _targetPosition;
                    }
                    else
                        _targetPositionTime -= gameTime.ElapsedGameTime;

                    CurrentAnimator.Position = newPosition;
                }
            }
            else
            {
                CurrentAnimator.Position += Speed * gameTime.GetElapsedSeconds() * Acceleration * Direction;
            }
        }

        private void UpdateRotation(GameTime gameTime)
        {
            if (TargetingAngle)
            {
                // TODO: Add some logic to know if the boss has to turn to the left or to the right

                if (_targetAngleTimer.TotalMilliseconds <= 0)
                {
                    var currentRotation = CurrentAnimator.Rotation;
                    var distance = Math.Abs(currentRotation - _targetAngle);
                    var deltaDistance = AngularVelocity*gameTime.GetElapsedSeconds();

                    if (distance < deltaDistance)
                    {
                        TargetingAngle = false;
                        CurrentAnimator.Rotation = _targetAngle;
                    }
                    else
                    {
                        var factor = (currentRotation < _targetAngle) ? 1 : -1;
                        CurrentAnimator.Rotation = currentRotation + (factor * deltaDistance);
                    }
                }
                else
                {
                    var lerpAmount = (float)(_targetAngleTime.TotalSeconds / _targetAngleTimer.TotalSeconds);
                    var newAngle = MathHelper.Lerp(_targetAngle, _initialAngle, lerpAmount);

                    if (lerpAmount < 0.001f)
                    {
                        TargetingAngle = false;
                        _targetAngleTimer = TimeSpan.Zero;
                        CurrentAnimator.Rotation = _targetAngle;
                    }
                    else
                        _targetAngleTime -= gameTime.ElapsedGameTime;

                    CurrentAnimator.Rotation = newAngle;
                }
            }
        }

        private void UpdateBehaviour(GameTime gameTime)
        {
            UpdateBehaviourIndex();

            if (CurrentBehaviourIndex != PreviousBehaviourIndex)
            {
                if (PreviousBehaviourIndex == Behaviours.Count - 1)
                {
                    Game.PlayerData.BossBeatenCounter(BossType, Game.PlayerData.BossBeatenCounter(BossType) + 1);

                    // Don't do that here, call a method from GameManager to end the game
                    Game.ScreenManager.Back();
                    return;
                }

                if (PreviousBehaviourIndex >= 0)
                    Behaviours[PreviousBehaviourIndex].Stop();

                Game.GameManager.MoverManager.Clear();
                RestoreDefaultState();

                if (Behaviours.Count > 0)
                    Behaviours[CurrentBehaviourIndex].Start();
            }

            if (Behaviours.Count > 0)
                Behaviours[CurrentBehaviourIndex].Update(gameTime);

            PreviousBehaviourIndex = CurrentBehaviourIndex;
        }

        protected virtual void UpdateBehaviourIndex()
        {
            if (Behaviours.Count == 0)
                return;

            CurrentBehaviourIndex = (int)Math.Floor((1f - (Life / InitialLife)) * Behaviours.Count) % Behaviours.Count;
        }

        public virtual void Draw()
        {
            CurrentAnimator.Draw(Game.SpriteBatch);

            if (CurrentBehaviourIndex < Behaviours.Count)
                Behaviours[CurrentBehaviourIndex].Draw(Game.SpriteBatch);
        }
    }
}