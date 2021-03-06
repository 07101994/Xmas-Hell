using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Timers;
using Sprite = MonoGame.Extended.Sprites.Sprite;
using SpriterDotNet;
using SpriterDotNet.MonoGame;
using SpriterDotNet.MonoGame.Content;
using SpriterDotNet.Providers;
using XmasHell.Physics;
using XmasHell.Physics.Collision;
using XmasHell.Spriter;
using Keyboard = Microsoft.Xna.Framework.Input.Keyboard;
using Xmas_Hell_Core.Controls;

namespace XmasHell.Entities
{
    public class Player : IPhysicsEntity
    {
        public bool Invincible;
        public TimeSpan _invincibleTimer;
        private bool _destroyed;

        private readonly XmasHell _game;
        private CollisionCircle _hitbox;
        private Sprite _hitboxSprite;

        private IList<MonoGameAnimator> _animators = new List<MonoGameAnimator>();
        public MonoGameAnimator CurrentAnimator;
        private CountdownTimer _idleAnimationTimer;

        private Vector2 _initialSpritePosition;
        private Point _initialTouchPosition;
        private Point _currentTouchPosition;
        private Point _previousTouchPosition;

        private Vector2 _currentDirection;

        private Vector2 _previousPosition;

        private TimeSpan _bulletFrequence;

        public Vector2 Position()
        {
            return CurrentAnimator.Position;
        }

        public virtual Vector2 LocalPosition()
        {
            return Vector2.Zero;
        }

        public float Rotation()
        {
            return CurrentAnimator.Rotation;
        }

        public Vector2 Origin()
        {
            return _hitboxSprite.Origin;
        }

        public Vector2 Scale()
        {
            return CurrentAnimator.Scale;
        }

        public bool Alive()
        {
            return !_destroyed;
        }

        public Player(XmasHell game)
        {
            _game = game;
            _currentDirection = Vector2.Zero;

            _idleAnimationTimer = new CountdownTimer(5);
            _idleAnimationTimer.Stop();
            _idleAnimationTimer.Completed += (sender, args) =>
            {
                Console.WriteLine("PLAY IDLE");
                CurrentAnimator.Play("Idle");
            };

            var playerHitboxTexture = Assets.GetTexture2D("Graphics/Sprites/Player/hitbox");

            var animatorConfig = new Config
            {
                MetadataEnabled = false,
                EventsEnabled = false,
                PoolingEnabled = true,
                TagsEnabled = false,
                VarsEnabled = false,
                SoundsEnabled = false
            };

            var factory = new DefaultProviderFactory<ISprite, SoundEffect>(animatorConfig, true);

            var loader = new SpriterContentLoader(_game.Content, "Graphics/Sprites/Player/player");
            loader.Fill(factory);

            Stack<SpriteDrawInfo> drawInfoPool = new Stack<SpriteDrawInfo>();

            foreach (var entity in loader.Spriter.Entities)
            {
                var animator = new CustomSpriterAnimator(_game, entity, factory, drawInfoPool);
                _animators.Add(animator);
            }

            CurrentAnimator = _animators.First();
            var spriteSize = new Vector2(60, 82);
            CurrentAnimator.Position = new Vector2(spriteSize.X / 2f, spriteSize.Y / 2f);
            CurrentAnimator.Play("Idle");

            CurrentAnimator.AnimationFinished += AnimationFinished;

            _hitboxSprite = new Sprite(playerHitboxTexture)
            {
                //Scale = new Vector2(
                //    (GameConfig.PlayerHitboxRadius * 2f) / playerHitboxTexture.Width,
                //    (GameConfig.PlayerHitboxRadius * 2f) / playerHitboxTexture.Height
                //)
            };
            _hitbox = new CollisionCircle(this, Vector2.Zero, GameConfig.PlayerHitboxRadius);

            // Don't forget to set the player position delegate to the MoverManager
            _game.GameManager.MoverManager.SetPlayerPositionDelegate(Position);
        }

        public void Initialize()
        {
            Invincible = false;
            _invincibleTimer = TimeSpan.FromSeconds(3f);

            _bulletFrequence = TimeSpan.Zero;
            _destroyed = false;

            _initialSpritePosition = new Vector2(
                GameConfig.VirtualResolution.X / 2f,
                GameConfig.VirtualResolution.Y - 150
            );

            CurrentAnimator.Position = _initialSpritePosition;
            _initialTouchPosition = _currentTouchPosition;

            _game.SpriteBatchManager.Player = this;
            _game.SpriteBatchManager.PlayerHitbox = _hitboxSprite;
            _game.GameManager.CollisionWorld.PlayerHitbox = _hitbox;
        }

        public void Dispose()
        {
            _game.SpriteBatchManager.Player = null;
            _game.SpriteBatchManager.PlayerHitbox = null;
            _game.GameManager.CollisionWorld.PlayerHitbox = null;
        }

        public void Destroy()
        {
            if (GameConfig.GodMode)
                return;

            _game.GameManager.ParticleManager.EmitPlayerDestroyedParticles(Position());
            _game.Camera.ZoomTo(3f, 0.25, Position());
            _game.GameManager.EndGame(true);

            _destroyed = true;

            _game.PlayerData.DeathCounter(_game.PlayerData.DeathCounter() + 1);

            Dispose();
        }

        private void AnimationFinished(string animationName)
        {
            if (animationName == "Left")
                CurrentAnimator.Play("LeftIdle");
            else if (animationName == "Right")
                CurrentAnimator.Play("RightIdle");
            else if (animationName == "Up")
                CurrentAnimator.Play("UpIdle");
            else if (animationName == "Down")
                CurrentAnimator.Play("DownIdle");
        }

        public void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();

            if (keyboardState.IsKeyDown(Keys.Enter))
                _game.GameManager.ParticleManager.EmitPlayerDestroyedParticles(Position());

            if (InputManager.KeyPressed(Keys.Z))
            {
                if (_game.Camera.Zoom == 1f)
                    _game.Camera.ZoomTo(5f, 0.5, Position());
                else
                    _game.Camera.ZoomTo(1f, 0.5, Position());
            }

            if (_invincibleTimer.TotalMilliseconds > 0)
                _invincibleTimer -= gameTime.ElapsedGameTime;
            else
                Invincible = false;

            CurrentAnimator.Update(gameTime.ElapsedGameTime.Milliseconds);

            _hitboxSprite.Position = _hitbox.GetCenter();

#if ANDROID
            UpdatePositionFromTouch(gameTime);
            //UpdateAnimationFromTouch();
#else
            UpdatePositionFromKeyboard(gameTime);
            //UpdateAnimationFromKeyboard();
#endif

            CheckOutOfBounds();
            UpdateShoot(gameTime);
        }

        private void UpdatePositionFromTouch(GameTime gameTime)
        {
            if (InputManager.TouchCount() == 1)
            {
                if (InputManager.TouchDown())
                {
                    _initialSpritePosition = Position();
                    _initialTouchPosition = _game.ViewportAdapter.PointToScreen(InputManager.TouchPosition());
                }

                _previousTouchPosition = _currentTouchPosition;
                _currentTouchPosition = _game.ViewportAdapter.PointToScreen(InputManager.TouchPosition());
                var globalTouchDelta = (_currentTouchPosition - _initialTouchPosition).ToVector2();

                _previousPosition = CurrentAnimator.Position;
                CurrentAnimator.Position = _initialSpritePosition + (globalTouchDelta * GameConfig.PlayerMoveSensitivity);
            }
            else
            {
                _initialSpritePosition = Vector2.Zero;
                _initialTouchPosition = Point.Zero;
                //CurrentAnimator.Play("Idle");
            }

            if (InputManager.TouchUp())
            {
                _initialSpritePosition = Vector2.Zero;
                _initialTouchPosition = Point.Zero;
            }
        }

        private void UpdatePositionFromKeyboard(GameTime gameTime)
        {
            var dt = (float) gameTime.ElapsedGameTime.TotalSeconds;
            _currentDirection = Vector2.Zero;

            if (InputManager.Up())
                _currentDirection.Y -= 1;
            if (InputManager.Down())
                _currentDirection.Y += 1;
            if (InputManager.Left())
                _currentDirection.X -= 1;
            if (InputManager.Right())
                _currentDirection.X += 1;

            var speed = GameConfig.PlayerSpeed;

            if (_currentDirection != Vector2.Zero)
                speed /= 1.5f;

            if (InputManager.KeyDown(Keys.LeftShift))
                speed = 250f;

            CurrentAnimator.Position += _currentDirection * speed * dt;
        }

        private void UpdateAnimationFromTouch()
        {
            // For touch motion
            var deltaPosition = CurrentAnimator.Position - _previousPosition;

            //Console.WriteLine("Delta position: " + deltaPosition);

            if (deltaPosition == Vector2.Zero)
            {
                if (_idleAnimationTimer.State == TimerState.Stopped)
                {
                    Console.WriteLine("Restart idle timer");
                    _idleAnimationTimer.Restart();
                }
            }
            else
            {
                if (_idleAnimationTimer.State == TimerState.Started)
                {
                    Console.WriteLine("Stop idle timer");
                    _idleAnimationTimer.Stop();
                }
            }

            if (deltaPosition.Y > 0f)
            {
                if (CurrentAnimator.CurrentAnimation.Name != "Down" &&
                    CurrentAnimator.CurrentAnimation.Name != "DownIdle")
                    CurrentAnimator.Play("Down");
            }
            else if (deltaPosition.Y < 0f)
            {
                if (CurrentAnimator.CurrentAnimation.Name != "Up" &&
                    CurrentAnimator.CurrentAnimation.Name != "UpIdle")
                    CurrentAnimator.Play("Up");
            }
            else if (deltaPosition.X < 0f)
            {
                if (CurrentAnimator.CurrentAnimation.Name != "Left" &&
                    CurrentAnimator.CurrentAnimation.Name != "LeftIdle")
                    CurrentAnimator.Play("Left");
            }
            else if (deltaPosition.X > 0f)
            {
                if (CurrentAnimator.CurrentAnimation.Name != "Right" &&
                    CurrentAnimator.CurrentAnimation.Name != "RightIdle")
                    CurrentAnimator.Play("Right");
            }
        }

        private void UpdateAnimationFromKeyboard()
        {
            // For keyboard
            // Down
            if (_currentDirection.Y > 0f)
            {
                if (CurrentAnimator.CurrentAnimation.Name != "Down" &&
                    CurrentAnimator.CurrentAnimation.Name != "DownIdle")
                {
                    Console.WriteLine("Play down animation");
                    CurrentAnimator.Play("Down");
                }
            }
            // Up
            else if (_currentDirection.Y < 0f)
            {
                if (CurrentAnimator.CurrentAnimation.Name != "Up" &&
                    CurrentAnimator.CurrentAnimation.Name != "UpIdle")
                {
                    Console.WriteLine("Play up animation");
                    CurrentAnimator.Play("Up");
                }
            }
            // Left
            else if (_currentDirection.X < 0f)
            {
                if (CurrentAnimator.CurrentAnimation.Name != "Left" &&
                    CurrentAnimator.CurrentAnimation.Name != "LeftIdle")
                {
                    Console.WriteLine("Play Left animation");
                    CurrentAnimator.Play("Left");
                }
            }
            // Right
            else if (_currentDirection.X > 0f)
            {
                if (CurrentAnimator.CurrentAnimation.Name != "Right" &&
                    CurrentAnimator.CurrentAnimation.Name != "RightIdle")
                {
                    Console.WriteLine("Play Right animation");
                    CurrentAnimator.Play("Right");
                }
            }
            else if (_currentDirection == Vector2.Zero)
            {
                CurrentAnimator.Play("Idle");
            }
        }

        private void CheckOutOfBounds()
        {
            CurrentAnimator.Position = new Vector2(
                MathHelper.Clamp(CurrentAnimator.Position.X, 0, GameConfig.VirtualResolution.X),
                MathHelper.Clamp(CurrentAnimator.Position.Y, 0, GameConfig.VirtualResolution.Y)
            );
        }

        private void UpdateShoot(GameTime gameTime)
        {
#if ANDROID
            if (InputManager.TouchCount() != 1 || !InputManager.TouchIsDown())
                return;
#else
            if (!InputManager.KeyDown(Keys.LeftControl))
                return;
#endif

            if (_bulletFrequence.TotalMilliseconds > 0)
                _bulletFrequence -= gameTime.ElapsedGameTime;
            else
            {
                _bulletFrequence = TimeSpan.FromTicks(GameConfig.PlayerShootFrequency.Ticks);

                var bullet1 = new PlayerBullet(_game, CurrentAnimator.Position, -MathHelper.PiOver4 / 4f, GameConfig.PlayerBulletSpeed);
                var bullet2 = new PlayerBullet(_game, CurrentAnimator.Position, -MathHelper.PiOver4 / 8f, GameConfig.PlayerBulletSpeed);
                var bullet3 = new PlayerBullet(_game, CurrentAnimator.Position, 0f, GameConfig.PlayerBulletSpeed);
                var bullet4 = new PlayerBullet(_game, CurrentAnimator.Position, MathHelper.PiOver4 / 8f, GameConfig.PlayerBulletSpeed);
                var bullet5 = new PlayerBullet(_game, CurrentAnimator.Position, MathHelper.PiOver4 / 4f, GameConfig.PlayerBulletSpeed);

                _game.GameManager.AddBullet(bullet1);
                _game.GameManager.AddBullet(bullet2);
                _game.GameManager.AddBullet(bullet3);
                _game.GameManager.AddBullet(bullet4);
                _game.GameManager.AddBullet(bullet5);
            }
        }

        public void TakeDamage(float damage)
        {
            // Nothing => is killed on the first touch
        }
    }
}